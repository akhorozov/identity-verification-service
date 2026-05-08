# syntax=docker/dockerfile:1.7
# ─────────────────────────────────────────────────────────────────────────────
# Stage 1 – restore (cached layer, only invalidated when .csproj files change)
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

# Copy central package management and solution manifest first for better caching
COPY Directory.Packages.props   ./
COPY Directory.Build.props      ./
COPY IdentityVerification.slnx  ./

# Copy only project files to restore dependencies before copying source
COPY src/AddressValidation.Api/AddressValidation.Api.csproj                         src/AddressValidation.Api/
COPY AddressValidation.AppHost/AddressValidation.AppHost.csproj                     AddressValidation.AppHost/
COPY AddressValidation.ServiceDefaults/AddressValidation.ServiceDefaults.csproj     AddressValidation.ServiceDefaults/

RUN dotnet restore src/AddressValidation.Api/AddressValidation.Api.csproj

# ─────────────────────────────────────────────────────────────────────────────
# Stage 2 – build & publish
# ─────────────────────────────────────────────────────────────────────────────
FROM restore AS publish
WORKDIR /src

# Copy full source
COPY src/AddressValidation.Api/               src/AddressValidation.Api/
COPY AddressValidation.ServiceDefaults/       AddressValidation.ServiceDefaults/

RUN dotnet publish src/AddressValidation.Api/AddressValidation.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

# ─────────────────────────────────────────────────────────────────────────────
# Stage 3 – runtime (minimal image, non-root user)
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Security: run as non-root
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

# Health-check — liveness probe matches GET /health/live
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD wget -qO- http://localhost:8080/health/live || exit 1

COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "AddressValidation.Api.dll"]
