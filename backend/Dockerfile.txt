FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY TrumpStockAlert.Api.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish TrumpStockAlert.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "TrumpStockAlert.Api.dll"]