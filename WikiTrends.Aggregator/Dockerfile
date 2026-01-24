 FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
 WORKDIR /src
 
 COPY ["WikiTrends.sln", "./"]
 COPY ["WikiTrends.Contracts/WikiTrends.Contracts.csproj", "WikiTrends.Contracts/"]
 COPY ["WikiTrends.Infrastructure/WikiTrends.Infrastructure.csproj", "WikiTrends.Infrastructure/"]
 COPY ["WikiTrends.Aggregator/WikiTrends.Aggregator.csproj", "WikiTrends.Aggregator/"]
 
 RUN dotnet restore "WikiTrends.Aggregator/WikiTrends.Aggregator.csproj"
 
 COPY ["WikiTrends.Contracts/", "WikiTrends.Contracts/"]
 COPY ["WikiTrends.Infrastructure/", "WikiTrends.Infrastructure/"]
 COPY ["WikiTrends.Aggregator/", "WikiTrends.Aggregator/"]
 
 WORKDIR "/src/WikiTrends.Aggregator"
 RUN dotnet build "WikiTrends.Aggregator.csproj" -c Release -o /app/build
 
 FROM build AS publish
 RUN dotnet publish "WikiTrends.Aggregator.csproj" -c Release -o /app/publish /p:UseAppHost=false
 
 FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
 WORKDIR /app
 
 RUN mkdir -p /app/logs && chown -R 1000:1000 /app
 
 COPY --from=publish /app/publish .
 
 RUN chown -R 1000:1000 /app
 
 USER 1000
 
 ENV DOTNET_RUNNING_IN_CONTAINER=true
 
 ENTRYPOINT ["dotnet", "WikiTrends.Aggregator.dll"]
