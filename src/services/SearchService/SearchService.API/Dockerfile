FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/services/SearchService/SearchService.API/SearchService.API.csproj", "src/services/SearchService/SearchService.API/"]
COPY ["src/services/SearchService/SearchService.Application/SearchService.Application.csproj", "src/services/SearchService/SearchService.Application/"]
RUN dotnet restore "src/services/SearchService/SearchService.API/SearchService.API.csproj"
COPY . .
WORKDIR "/src/src/services/SearchService/SearchService.API"
RUN dotnet build "SearchService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SearchService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY data/iata/airports.dat ./data/iata/airports.dat
ENTRYPOINT ["dotnet", "SearchService.API.dll"]
