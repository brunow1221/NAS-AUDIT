FROM mcr.microsoft.com/dotnet/sdk:10.0.402-alpine3.24 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["backend/BCMetals.NasAudit.slnx", "backend/"]
COPY ["backend/src/BCMetals.NasAudit.Domain/BCMetals.NasAudit.Domain.csproj", "backend/src/BCMetals.NasAudit.Domain/"]
COPY ["backend/src/BCMetals.NasAudit.Application/BCMetals.NasAudit.Application.csproj", "backend/src/BCMetals.NasAudit.Application/"]
COPY ["backend/src/BCMetals.NasAudit.Infrastructure/BCMetals.NasAudit.Infrastructure.csproj", "backend/src/BCMetals.NasAudit.Infrastructure/"]
COPY ["backend/src/BCMetals.NasAudit.Api/BCMetals.NasAudit.Api.csproj", "backend/src/BCMetals.NasAudit.Api/"]
RUN dotnet restore backend/src/BCMetals.NasAudit.Api/BCMetals.NasAudit.Api.csproj
COPY backend/ backend/
RUN dotnet publish backend/src/BCMetals.NasAudit.Api/BCMetals.NasAudit.Api.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-alpine3.24 AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/keys \
    && chown -R ${APP_UID}:0 /app \
    && chmod -R u=rwX,go=rX /app \
    && chmod 700 /app/keys
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "BCMetals.NasAudit.Api.dll"]

