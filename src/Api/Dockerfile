# Image de production de l'API ASP.NET Core. À construire depuis la racine du
# dépôt (l'API dépend de src/Shared) :
#   docker build -f src/Api/Dockerfile -t horizon-api .
# La génération des PDF (PuppeteerSharp) a besoin d'un navigateur : on installe
# le Chromium de Debian, avec ses bibliothèques système, et on indique son
# chemin via PUPPETEER_EXECUTABLE_PATH.
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

# TZ : les dates imprimées sont formatées dans le fuseau du serveur (voir
# MergeHtml.cs, « jour dans le fuseau du serveur »). Sans cette variable,
# TimeZoneInfo.Local vaut UTC et une date de 22 h 30 s'imprime la veille.
# Surchargeable au lancement (-e TZ=...).
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium \
    ASPNETCORE_ENVIRONMENT=Production \
    TZ=Europe/Brussels

# Polices : sans elles, le texte des PDF s'afficherait mal. tzdata : base des
# fuseaux horaires, sans laquelle la variable TZ ci-dessus serait ignorée.
RUN apt-get update \
  && apt-get install -y --no-install-recommends chromium fonts-liberation fonts-dejavu-core ca-certificates tzdata \
  && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# Port d'écoute : variable PORT (injectée par Cloud Run), 3000 par défaut.
# Fichiers du serveur (PDF générés, documents joints) : /app/storage, sauf
# PDF_STORAGE_DIR — à monter sur un volume pour les conserver.
EXPOSE 3000
CMD ["dotnet", "Api.dll"]
