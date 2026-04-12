FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/services/CrmService/CrmService.API/CrmService.API.csproj", "src/services/CrmService/CrmService.API/"]
COPY ["src/services/CrmService/CrmService.Application/CrmService.Application.csproj", "src/services/CrmService/CrmService.Application/"]
COPY ["src/services/CrmService/CrmService.Infrastructure/CrmService.Infrastructure.csproj", "src/services/CrmService/CrmService.Infrastructure/"]
RUN dotnet restore "src/services/CrmService/CrmService.API/CrmService.API.csproj"
COPY . .
WORKDIR "/src/src/services/CrmService/CrmService.API"
RUN dotnet build "CrmService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CrmService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CrmService.API.dll"]
