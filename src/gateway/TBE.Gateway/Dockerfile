FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/gateway/TBE.Gateway/TBE.Gateway.csproj", "src/gateway/TBE.Gateway/"]
RUN dotnet restore "src/gateway/TBE.Gateway/TBE.Gateway.csproj"
COPY . .
WORKDIR "/src/src/gateway/TBE.Gateway"
RUN dotnet build "TBE.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TBE.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TBE.Gateway.dll"]
