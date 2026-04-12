FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/services/PricingService/PricingService.API/PricingService.API.csproj", "src/services/PricingService/PricingService.API/"]
COPY ["src/services/PricingService/PricingService.Application/PricingService.Application.csproj", "src/services/PricingService/PricingService.Application/"]
COPY ["src/services/PricingService/PricingService.Infrastructure/PricingService.Infrastructure.csproj", "src/services/PricingService/PricingService.Infrastructure/"]
RUN dotnet restore "src/services/PricingService/PricingService.API/PricingService.API.csproj"
COPY . .
WORKDIR "/src/src/services/PricingService/PricingService.API"
RUN dotnet build "PricingService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PricingService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PricingService.API.dll"]
