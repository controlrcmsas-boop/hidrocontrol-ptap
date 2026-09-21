# ==============================================================================
# HIDROCONTROL PTAP - DOCKERFILE PARA DESPLIEGUE EN LA NUBE 24/7
# Compatible con Render, Railway, Fly.io, Azure Container Apps, VPS
# ==============================================================================

# Etapa 1: Compilación con .NET 8 SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Restaurar dependencias
COPY dashboard/PTAPControl/PTAPControl.csproj dashboard/PTAPControl/
RUN dotnet restore dashboard/PTAPControl/PTAPControl.csproj

# Copiar código fuente y compilar
COPY dashboard/PTAPControl/ dashboard/PTAPControl/
WORKDIR /app/dashboard/PTAPControl
RUN dotnet publish -c Release -o /out /p:UseAppHost=false

# Etapa 2: Runtime final ASP.NET 8
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out .

# Puerto estándar de servicios cloud (Render, Railway, Fly.io leen PORT / 8080)
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "PTAPControl.dll"]
