# depuis la racine : docker build -f src/Web/Dockerfile -t horizon-web .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Shared/Shared.csproj src/Shared/
COPY src/Web/Web.csproj src/Web/
RUN dotnet restore src/Web/Web.csproj

COPY src/Shared/ src/Shared/
COPY src/Web/ src/Web/
RUN dotnet publish src/Web/Web.csproj -c Release -o /app --no-restore

FROM nginx:1.27-alpine
COPY src/Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/wwwroot /usr/share/nginx/html

# appsettings réécrit au démarrage, on retire ses versions précompressées
COPY src/Web/docker/40-appsettings.sh /docker-entrypoint.d/
RUN sed -i 's/\r$//' /docker-entrypoint.d/40-appsettings.sh \
  && chmod +x /docker-entrypoint.d/40-appsettings.sh \
  && rm -f /usr/share/nginx/html/appsettings*.json.br /usr/share/nginx/html/appsettings*.json.gz

EXPOSE 8080
