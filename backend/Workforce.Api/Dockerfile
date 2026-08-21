FROM mcr.microsoft.com/dotnet/sdk:10.0.11 AS build
WORKDIR /src
COPY Workforce.Api.csproj ./
RUN dotnet restore Workforce.Api.csproj
COPY . .
RUN dotnet publish Workforce.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Workforce.Api.dll"]
