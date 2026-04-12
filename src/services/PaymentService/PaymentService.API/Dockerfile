FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/services/PaymentService/PaymentService.API/PaymentService.API.csproj", "src/services/PaymentService/PaymentService.API/"]
COPY ["src/services/PaymentService/PaymentService.Application/PaymentService.Application.csproj", "src/services/PaymentService/PaymentService.Application/"]
COPY ["src/services/PaymentService/PaymentService.Infrastructure/PaymentService.Infrastructure.csproj", "src/services/PaymentService/PaymentService.Infrastructure/"]
RUN dotnet restore "src/services/PaymentService/PaymentService.API/PaymentService.API.csproj"
COPY . .
WORKDIR "/src/src/services/PaymentService/PaymentService.API"
RUN dotnet build "PaymentService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PaymentService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PaymentService.API.dll"]
