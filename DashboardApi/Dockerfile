# --- 1. BASE (Первая строка обязана быть .NET!) ---
# Этот этап используется Visual Studio при нажатии F5
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Устанавливаем Node.js ПРЯМО ЗДЕСЬ (в базовый образ)
# aspnet:8.0 основан на Debian, поэтому используем apt-get
RUN apt-get update && \
    apt-get install -y curl && \
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# --- 2. BUILD ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DashboardApi/DashboardApi.csproj", "DashboardApi/"]
RUN dotnet restore "DashboardApi/DashboardApi.csproj"
COPY . .
WORKDIR "/src/DashboardApi"
RUN dotnet build "DashboardApi.csproj" -c Release -o /app/build

# --- 3. PUBLISH ---
FROM build AS publish
RUN dotnet publish "DashboardApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# --- 4. FINAL (Используется в продакшене/docker-compose без отладки) ---
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Копируем скрипты (для продакшена)
COPY --from=build /src/DashboardApi/NodeScripts ./NodeScripts

# Устанавливаем зависимости JS (для продакшена)
WORKDIR /app/NodeScripts
RUN npm install
WORKDIR /app

ENTRYPOINT ["dotnet", "DashboardApi.dll"]