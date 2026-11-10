# depuis la racine : docker build -f src/Api/Dockerfile -t horizon-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Shared/Shared.csproj src/Shared/
COPY src/Api/Api.csproj src/Api/
RUN dotnet restore src/Api/Api.csproj

COPY src/Shared/ src/Shared/
COPY src/Api/ src/Api/
RUN dotnet publish src/Api/Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# sans TZ, une date à 22 h 30 sort la veille sur les PDF
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium \
    ASPNETCORE_ENVIRONMENT=Production \
    TZ=Europe/Brussels

# chromium pour PuppeteerSharp, tzdata sinon TZ ignorée
RUN apt-get update \
  && apt-get install -y --no-install-recommends chromium fonts-liberation fonts-dejavu-core ca-certificates tzdata \
  && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# /app/storage à monter sur un volume
EXPOSE 3000
CMD ["dotnet", "Api.dll"]
