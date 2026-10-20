# Stage 1: Build & Publish (.NET 10 SDK on Linux)
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props Infostructure.MyBigBro.Web.sln ./
COPY Infostructure.MyBigBro.Domain/Infostructure.MyBigBro.Domain.csproj Infostructure.MyBigBro.Domain/
COPY Infostructure.MyBigBro.DataModel/Infostructure.MyBigBro.DataModel.csproj Infostructure.MyBigBro.DataModel/
COPY Infostructure.MyBigBro.ImageStorageServiceAgent/Infostructure.MyBigBro.ImageStorageServiceAgent.csproj Infostructure.MyBigBro.ImageStorageServiceAgent/
COPY Infostructure.MyBigBro.BusinessLogic/Infostructure.MyBigBro.BusinessLogic.csproj Infostructure.MyBigBro.BusinessLogic/
COPY Infostructure.MyBigBro.CaptureWorker/Infostructure.MyBigBro.CaptureWorker.csproj Infostructure.MyBigBro.CaptureWorker/

RUN dotnet restore Infostructure.MyBigBro.CaptureWorker/Infostructure.MyBigBro.CaptureWorker.csproj

COPY Infostructure.MyBigBro.Domain/ Infostructure.MyBigBro.Domain/
COPY Infostructure.MyBigBro.DataModel/ Infostructure.MyBigBro.DataModel/
COPY Infostructure.MyBigBro.ImageStorageServiceAgent/ Infostructure.MyBigBro.ImageStorageServiceAgent/
COPY Infostructure.MyBigBro.BusinessLogic/ Infostructure.MyBigBro.BusinessLogic/
COPY Infostructure.MyBigBro.CaptureWorker/ Infostructure.MyBigBro.CaptureWorker/
COPY Data/harbourbridge.jpg /src/Data/harbourbridge.jpg

RUN dotnet publish Infostructure.MyBigBro.CaptureWorker/Infostructure.MyBigBro.CaptureWorker.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false && \
    cp /src/Data/harbourbridge.jpg /app/publish/fallback-webcam.jpg

# Stage 2: Runtime (.NET 10 Runtime on Ubuntu 24.04 Noble)
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble AS runtime
WORKDIR /app

USER $APP_UID
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Infostructure.MyBigBro.CaptureWorker.dll"]
