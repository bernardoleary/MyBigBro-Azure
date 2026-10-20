# Stage 1: Build & Publish (.NET 10 SDK on Linux)
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props Infostructure.MyBigBro.Web.sln ./
COPY Infostructure.MyBigBro.Domain/Infostructure.MyBigBro.Domain.csproj Infostructure.MyBigBro.Domain/
COPY Infostructure.MyBigBro.DataModel/Infostructure.MyBigBro.DataModel.csproj Infostructure.MyBigBro.DataModel/
COPY Infostructure.MyBigBro.ImageStorageServiceAgent/Infostructure.MyBigBro.ImageStorageServiceAgent.csproj Infostructure.MyBigBro.ImageStorageServiceAgent/
COPY Infostructure.MyBigBro.BusinessLogic/Infostructure.MyBigBro.BusinessLogic.csproj Infostructure.MyBigBro.BusinessLogic/
COPY Infostructure.MyBigBro.Web4/Infostructure.MyBigBro.Web4.csproj Infostructure.MyBigBro.Web4/

RUN dotnet restore Infostructure.MyBigBro.Web4/Infostructure.MyBigBro.Web4.csproj

COPY Infostructure.MyBigBro.Domain/ Infostructure.MyBigBro.Domain/
COPY Infostructure.MyBigBro.DataModel/ Infostructure.MyBigBro.DataModel/
COPY Infostructure.MyBigBro.ImageStorageServiceAgent/ Infostructure.MyBigBro.ImageStorageServiceAgent/
COPY Infostructure.MyBigBro.BusinessLogic/ Infostructure.MyBigBro.BusinessLogic/
COPY Infostructure.MyBigBro.Web4/ Infostructure.MyBigBro.Web4/

RUN dotnet publish Infostructure.MyBigBro.Web4/Infostructure.MyBigBro.Web4.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Stage 2: Runtime (.NET 10 ASP.NET Core on Ubuntu 24.04 Noble)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app

USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Infostructure.MyBigBro.Web4.dll"]
