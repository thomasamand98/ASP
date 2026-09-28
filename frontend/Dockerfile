# Image de production du frontend (Google Cloud Run) : Vite compile l'app,
# puis nginx sert les fichiers statiques sur le port 8080.
FROM node:24-bookworm-slim AS build

WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Les variables VITE_* sont figées dans le code au moment de la compilation :
# l'URL de l'API est fournie par cloudbuild.yaml (--build-arg).
ARG VITE_API_URL
ENV VITE_API_URL=$VITE_API_URL
RUN test -n "$VITE_API_URL" || (echo "VITE_API_URL manquant : renseigner _API_URL dans le déclencheur Cloud Build" && exit 1)
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
