# Image de production du backend (Google Cloud Run).
# Puppeteer (génération des PDF) a besoin d'un navigateur : on installe le
# Chromium de Debian, avec ses bibliothèques système, et on indique à
# Puppeteer de l'utiliser au lieu de télécharger son propre Chrome.
FROM node:24-bookworm-slim

WORKDIR /app

ENV PUPPETEER_SKIP_DOWNLOAD=true \
    PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium

# Polices : sans elles, le texte des PDF s'afficherait mal.
RUN apt-get update \
  && apt-get install -y --no-install-recommends chromium fonts-liberation fonts-dejavu-core ca-certificates \
  && rm -rf /var/lib/apt/lists/*

COPY package.json package-lock.json prisma7.config.ts ./
COPY prisma ./prisma
# `prisma generate` (postinstall) lit la config Prisma : une URL factice suffit,
# la vraie est fournie par Cloud Run au démarrage.
RUN DATABASE_URL="mysql://build:build@localhost:3306/build" npm ci

COPY . .
RUN npm run build && npm prune --omit=dev

ENV NODE_ENV=production
CMD ["node", "dist/main"]
