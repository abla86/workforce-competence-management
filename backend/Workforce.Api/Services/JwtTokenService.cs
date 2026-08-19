using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Workforce.Api.Services;

public sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, RefreshTokenRecord> _refreshTokens = new();

    public JwtTokenService(IConfiguration configuration) => _configuration = configuration;

    public string CreateAccessToken(string userId, string displayName, IEnumerable<string> roles)
    {
        var secret = _configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey is missing.");
        var key = new SymmetricSecurityKey(Convert.FromBase64String(secret));
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
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken(string userId, IEnumerable<string> roles)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _refreshTokens[token] = new RefreshTokenRecord(userId, roles.ToArray(), DateTime.UtcNow.AddDays(7));
        return token;
    }

    public bool TryUseRefreshToken(string token, out RefreshTokenRecord record)
    {
        record = default!;
        if (!_refreshTokens.TryRemove(token, out var stored) || stored.ExpiresAt <= DateTime.UtcNow)
            return false;
        record = stored;
        return true;
    }

    public sealed record RefreshTokenRecord(string UserId, string[] Roles, DateTime ExpiresAt);
}
