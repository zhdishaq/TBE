FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/services/FlightConnectorService/FlightConnectorService.API/FlightConnectorService.API.csproj", "src/services/FlightConnectorService/FlightConnectorService.API/"]
COPY ["src/services/FlightConnectorService/FlightConnectorService.Application/FlightConnectorService.Application.csproj", "src/services/FlightConnectorService/FlightConnectorService.Application/"]
RUN dotnet restore "src/services/FlightConnectorService/FlightConnectorService.API/FlightConnectorService.API.csproj"
COPY . .
WORKDIR "/src/src/services/FlightConnectorService/FlightConnectorService.API"
RUN dotnet build "FlightConnectorService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FlightConnectorService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FlightConnectorService.API.dll"]
