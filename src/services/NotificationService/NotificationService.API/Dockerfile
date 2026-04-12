FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/services/NotificationService/NotificationService.API/NotificationService.API.csproj", "src/services/NotificationService/NotificationService.API/"]
COPY ["src/services/NotificationService/NotificationService.Application/NotificationService.Application.csproj", "src/services/NotificationService/NotificationService.Application/"]
COPY ["src/services/NotificationService/NotificationService.Infrastructure/NotificationService.Infrastructure.csproj", "src/services/NotificationService/NotificationService.Infrastructure/"]
RUN dotnet restore "src/services/NotificationService/NotificationService.API/NotificationService.API.csproj"
COPY . .
WORKDIR "/src/src/services/NotificationService/NotificationService.API"
RUN dotnet build "NotificationService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NotificationService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NotificationService.API.dll"]
