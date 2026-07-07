FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore Nutrition.Web.sln
RUN dotnet publish src/Nutrition.Web/Nutrition.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
EXPOSE 8080

ENTRYPOINT ["dotnet", "Nutrition.Web.dll"]
