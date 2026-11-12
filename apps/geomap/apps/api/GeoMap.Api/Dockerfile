FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["apps/api/GeoMap.Api/GeoMap.Api.csproj", "apps/api/GeoMap.Api/"]
RUN dotnet restore "apps/api/GeoMap.Api/GeoMap.Api.csproj"

COPY . .
RUN dotnet publish "apps/api/GeoMap.Api/GeoMap.Api.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GeoMap.Api.dll"]
