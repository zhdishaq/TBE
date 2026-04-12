FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy all service Infrastructure projects that DbMigrator references
COPY ["src/tools/TBE.DbMigrator/TBE.DbMigrator.csproj", "src/tools/TBE.DbMigrator/"]
COPY ["src/services/BookingService/BookingService.Infrastructure/BookingService.Infrastructure.csproj", "src/services/BookingService/BookingService.Infrastructure/"]
COPY ["src/services/BookingService/BookingService.Application/BookingService.Application.csproj", "src/services/BookingService/BookingService.Application/"]
COPY ["src/services/PaymentService/PaymentService.Infrastructure/PaymentService.Infrastructure.csproj", "src/services/PaymentService/PaymentService.Infrastructure/"]
COPY ["src/services/PaymentService/PaymentService.Application/PaymentService.Application.csproj", "src/services/PaymentService/PaymentService.Application/"]
COPY ["src/services/PricingService/PricingService.Infrastructure/PricingService.Infrastructure.csproj", "src/services/PricingService/PricingService.Infrastructure/"]
COPY ["src/services/PricingService/PricingService.Application/PricingService.Application.csproj", "src/services/PricingService/PricingService.Application/"]
COPY ["src/services/NotificationService/NotificationService.Infrastructure/NotificationService.Infrastructure.csproj", "src/services/NotificationService/NotificationService.Infrastructure/"]
COPY ["src/services/NotificationService/NotificationService.Application/NotificationService.Application.csproj", "src/services/NotificationService/NotificationService.Application/"]
COPY ["src/services/CrmService/CrmService.Infrastructure/CrmService.Infrastructure.csproj", "src/services/CrmService/CrmService.Infrastructure/"]
COPY ["src/services/CrmService/CrmService.Application/CrmService.Application.csproj", "src/services/CrmService/CrmService.Application/"]
COPY ["src/services/BackofficeService/BackofficeService.Infrastructure/BackofficeService.Infrastructure.csproj", "src/services/BackofficeService/BackofficeService.Infrastructure/"]
COPY ["src/services/BackofficeService/BackofficeService.Application/BackofficeService.Application.csproj", "src/services/BackofficeService/BackofficeService.Application/"]
COPY ["src/shared/TBE.Contracts/TBE.Contracts.csproj", "src/shared/TBE.Contracts/"]
COPY ["src/shared/TBE.Common/TBE.Common.csproj", "src/shared/TBE.Common/"]
RUN dotnet restore "src/tools/TBE.DbMigrator/TBE.DbMigrator.csproj"

COPY . .
WORKDIR "/src/src/tools/TBE.DbMigrator"
RUN dotnet publish "TBE.DbMigrator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TBE.DbMigrator.dll"]
