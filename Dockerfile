# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY ["Adapters.Api/GarageFlow.Adapters.Api.csproj", "Adapters.Api/"]
COPY ["Application/GarageFlow.Application.csproj", "Application/"]
COPY ["Adapters.Infrastructure/GarageFlow.Adapters.Infrastructure.csproj", "Adapters.Infrastructure/"]
COPY ["Domain/GarageFlow.Domain.csproj", "Domain/"]
COPY ["SharedKernel/GarageFlow.SharedKernel.csproj", "SharedKernel/"]

RUN dotnet restore "Adapters.Api/GarageFlow.Adapters.Api.csproj"

FROM restore AS build
COPY . .
RUN dotnet build "Adapters.Api/GarageFlow.Adapters.Api.csproj" -c Release --no-restore

FROM build AS publish
RUN dotnet publish "Adapters.Api/GarageFlow.Adapters.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-build

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .
COPY scripts/seed-local.sql /app/scripts/seed-local.sql

RUN useradd --create-home --uid 10001 --shell /usr/sbin/nologin appuser \
    && chown -R appuser:appuser /app

USER appuser

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "GarageFlow.Adapters.Api.dll"]
