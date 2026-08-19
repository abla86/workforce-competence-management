using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Workforce.Api.Services;

public sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, RefreshTokenRecord> _refreshTokens = new();

    public JwtTokenService(IConfiguration configuration) => _configuration = configuration;

    public string CreateAccessToken(string userId, string displayName, IEnumerable<string> roles)
    {
        var secret = _configuration["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Jwt:SecretKey must be configured for JWT authentication.");

        var key = new SymmetricSecurityKey(Convert.FromBase64String(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, displayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken(string userId, IEnumerable<string> roles)
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(bytes);
        _refreshTokens[token] = new RefreshTokenRecord(userId, roles.ToArray(), DateTime.UtcNow.AddDays(7));
        return token;
    }

    public bool TryUseRefreshToken(string refreshToken, out RefreshTokenRecord record)
    {
        record = default!;
        if (!_refreshTokens.TryRemove(refreshToken, out var stored) || stored.ExpiresAt <= DateTime.UtcNow)
            return false;
        record = stored;
        return true;
    }

    public sealed record RefreshTokenRecord(string UserId, string[] Roles, DateTime ExpiresAt);
}
