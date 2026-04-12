FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/services/BookingService/BookingService.API/BookingService.API.csproj", "src/services/BookingService/BookingService.API/"]
COPY ["src/services/BookingService/BookingService.Application/BookingService.Application.csproj", "src/services/BookingService/BookingService.Application/"]
COPY ["src/services/BookingService/BookingService.Infrastructure/BookingService.Infrastructure.csproj", "src/services/BookingService/BookingService.Infrastructure/"]
COPY ["src/shared/TBE.Contracts/TBE.Contracts.csproj", "src/shared/TBE.Contracts/"]
COPY ["src/shared/TBE.Common/TBE.Common.csproj", "src/shared/TBE.Common/"]
RUN dotnet restore "src/services/BookingService/BookingService.API/BookingService.API.csproj"
COPY . .
WORKDIR "/src/src/services/BookingService/BookingService.API"
RUN dotnet build "BookingService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BookingService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BookingService.API.dll"]
