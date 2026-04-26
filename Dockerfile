# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY ["Api/GarageFlow.Api.csproj", "Api/"]
COPY ["Application/GarageFlow.Application.csproj", "Application/"]
COPY ["Infrastructure/GarageFlow.Infrastructure.csproj", "Infrastructure/"]
COPY ["Domain/GarageFlow.Domain.csproj", "Domain/"]
COPY ["BuildingBlocks/GarageFlow.BuildingBlocks.csproj", "BuildingBlocks/"]

RUN dotnet restore "Api/GarageFlow.Api.csproj"

FROM restore AS build
COPY . .
RUN dotnet build "Api/GarageFlow.Api.csproj" -c Release --no-restore

FROM build AS publish
RUN dotnet publish "Api/GarageFlow.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-build

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .

RUN useradd --create-home --uid 10001 --shell /usr/sbin/nologin appuser \
    && chown -R appuser:appuser /app

USER appuser

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "GarageFlow.Api.dll"]
