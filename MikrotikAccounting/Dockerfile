#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["MikrotikAccounting/MikrotikAccounting.csproj", "MikrotikAccounting/"]
RUN dotnet restore "MikrotikAccounting/MikrotikAccounting.csproj"
COPY . .
WORKDIR "/src/MikrotikAccounting"
RUN dotnet build "MikrotikAccounting.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MikrotikAccounting.csproj" -c Release -o /app/publish

FROM base AS final
VOLUME /app/appsettings.json
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MikrotikAccounting.dll"]