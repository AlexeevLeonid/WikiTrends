FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["WikiTrends.sln", "./"]
COPY ["WikiTrends.Contracts/WikiTrends.Contracts.csproj", "WikiTrends.Contracts/"]
COPY ["WikiTrends.Frontend/WikiTrends.Frontend.csproj", "WikiTrends.Frontend/"]

RUN dotnet restore "WikiTrends.Frontend/WikiTrends.Frontend.csproj"

COPY ["WikiTrends.Contracts/", "WikiTrends.Contracts/"]
COPY ["WikiTrends.Frontend/", "WikiTrends.Frontend/"]

WORKDIR "/src/WikiTrends.Frontend"
RUN dotnet build "WikiTrends.Frontend.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WikiTrends.Frontend.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN mkdir -p /app/logs && chown -R 1000:1000 /app

COPY --from=publish /app/publish .

RUN chown -R 1000:1000 /app

USER 1000

ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "WikiTrends.Frontend.dll"]
