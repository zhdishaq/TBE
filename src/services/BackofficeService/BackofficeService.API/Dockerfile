FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/services/BackofficeService/BackofficeService.API/BackofficeService.API.csproj", "src/services/BackofficeService/BackofficeService.API/"]
COPY ["src/services/BackofficeService/BackofficeService.Application/BackofficeService.Application.csproj", "src/services/BackofficeService/BackofficeService.Application/"]
COPY ["src/services/BackofficeService/BackofficeService.Infrastructure/BackofficeService.Infrastructure.csproj", "src/services/BackofficeService/BackofficeService.Infrastructure/"]
RUN dotnet restore "src/services/BackofficeService/BackofficeService.API/BackofficeService.API.csproj"
COPY . .
WORKDIR "/src/src/services/BackofficeService/BackofficeService.API"
RUN dotnet build "BackofficeService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BackofficeService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BackofficeService.API.dll"]
