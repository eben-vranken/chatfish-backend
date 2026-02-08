FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY "BackEnd.csproj" .
RUN dotnet restore "BackEnd.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "./BackEnd.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BackEnd.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final

ARG APP_UID=1000
ARG APP_GID=1000

# Install Node.js for seed script
RUN apt-get update && apt-get install -y curl && \
    curl -fsSL https://deb.nodesource.com/setup_18.x | bash - && \
    apt-get install -y nodejs && \
    rm -rf /var/lib/apt/lists/*

RUN groupadd -g $APP_GID appgroup \
 && useradd -m -u $APP_UID -g appgroup -s /bin/sh appuser \
 && mkdir -p /app \
 && chown -R appuser:appgroup /app

WORKDIR /app
COPY --from=publish /app/publish .
COPY .env .
# Copy seed files (excluding node_modules)
COPY seed/package.json seed/package-lock.json* ./seed/
COPY seed/seed-mongodb.js ./seed/
COPY seed/seed.sh ./seed/
COPY seed/files ./seed/files
# Install dependencies in container for correct platform
RUN cd /app/seed && npm install && \
    chown -R appuser:appgroup /app/seed
USER appuser

ENTRYPOINT ["dotnet", "BackEnd.dll"]
