# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/TOEICMaster.Domain/*.csproj src/TOEICMaster.Domain/packages.lock.json src/TOEICMaster.Domain/
COPY src/TOEICMaster.Application/*.csproj src/TOEICMaster.Application/packages.lock.json src/TOEICMaster.Application/
COPY src/TOEICMaster.Infrastructure/*.csproj src/TOEICMaster.Infrastructure/packages.lock.json src/TOEICMaster.Infrastructure/
COPY src/TOEICMaster.Api/*.csproj src/TOEICMaster.Api/packages.lock.json src/TOEICMaster.Api/
RUN dotnet restore src/TOEICMaster.Api/TOEICMaster.Api.csproj --locked-mode
COPY src ./src
RUN dotnet publish src/TOEICMaster.Api/TOEICMaster.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TOEICMaster.Api.dll"]
