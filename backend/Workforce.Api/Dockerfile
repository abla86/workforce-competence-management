FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Workforce.Api.csproj ./
RUN dotnet restore Workforce.Api.csproj
COPY . .
RUN dotnet publish Workforce.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Workforce.Api.dll"]
