# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS restore
WORKDIR /src

COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY ["GarageFlow.slnx", "."]

COPY ["Host/GarageFlow.Host.csproj", "Host/"]
COPY ["Adapters.Api/GarageFlow.Adapters.Api.csproj", "Adapters.Api/"]
COPY ["Adapters.Infrastructure/GarageFlow.Adapters.Infrastructure.csproj", "Adapters.Infrastructure/"]
COPY ["Application/GarageFlow.Application.csproj", "Application/"]
COPY ["Domain/GarageFlow.Domain.csproj", "Domain/"]
COPY ["SharedKernel/GarageFlow.SharedKernel.csproj", "SharedKernel/"]

RUN dotnet restore "Host/GarageFlow.Host.csproj"

FROM restore AS build
COPY . .
RUN dotnet build "Host/GarageFlow.Host.csproj" -c Release --no-restore

FROM build AS publish
RUN dotnet publish "Host/GarageFlow.Host.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-build

FROM mcr.microsoft.com/dotnet/aspnet:10.0.9 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --uid 10001 --shell /usr/sbin/nologin appuser

COPY --from=publish --chown=appuser:appuser /app/publish .
COPY --chown=appuser:appuser scripts/seed-local.sql /app/scripts/seed-local.sql

USER appuser

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "GarageFlow.Host.dll"]
