# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["WikiTrends.sln", "./"]
COPY ["WikiTrends.Contracts/WikiTrends.Contracts.csproj", "WikiTrends.Contracts/"]
COPY ["WikiTrends.Infrastructure/WikiTrends.Infrastructure.csproj", "WikiTrends.Infrastructure/"]
COPY ["WikiTrends.Collector/WikiTrends.Collector.csproj", "WikiTrends.Collector/"]

# Restore dependencies
RUN dotnet restore "WikiTrends.Collector/WikiTrends.Collector.csproj"

# Copy source code
COPY ["WikiTrends.Contracts/", "WikiTrends.Contracts/"]
COPY ["WikiTrends.Infrastructure/", "WikiTrends.Infrastructure/"]
COPY ["WikiTrends.Collector/", "WikiTrends.Collector/"]

# Build
WORKDIR "/src/WikiTrends.Collector"
RUN dotnet build "WikiTrends.Collector.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "WikiTrends.Collector.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create non-root user
RUN mkdir -p /app/logs && chown -R 1000:1000 /app

# Copy published app
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R 1000:1000 /app

USER 1000

# Environment
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "WikiTrends.Collector.dll"]