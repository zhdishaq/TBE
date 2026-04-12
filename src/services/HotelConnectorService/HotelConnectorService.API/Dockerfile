FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/services/HotelConnectorService/HotelConnectorService.API/HotelConnectorService.API.csproj", "src/services/HotelConnectorService/HotelConnectorService.API/"]
COPY ["src/services/HotelConnectorService/HotelConnectorService.Application/HotelConnectorService.Application.csproj", "src/services/HotelConnectorService/HotelConnectorService.Application/"]
RUN dotnet restore "src/services/HotelConnectorService/HotelConnectorService.API/HotelConnectorService.API.csproj"
COPY . .
WORKDIR "/src/src/services/HotelConnectorService/HotelConnectorService.API"
RUN dotnet build "HotelConnectorService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HotelConnectorService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HotelConnectorService.API.dll"]
