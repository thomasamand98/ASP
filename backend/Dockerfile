# Image de production du backend (Google Cloud Run).
# Puppeteer (génération des PDF) a besoin de Chrome et de ses bibliothèques
# système : on les installe ici, ce que les Buildpacks ne font pas.
FROM node:24-bookworm-slim

WORKDIR /app

# Chrome est téléchargé dans l'application plutôt que dans ~/.cache, pour
# qu'il reste présent dans l'image finale.
ENV PUPPETEER_CACHE_DIR=/app/.cache/puppeteer

# Polices : sans elles, le texte des PDF s'afficherait mal.
RUN apt-get update \
  && apt-get install -y --no-install-recommends fonts-liberation fonts-dejavu-core ca-certificates \
  && rm -rf /var/lib/apt/lists/*

COPY package.json package-lock.json prisma7.config.ts ./
COPY prisma ./prisma
# `prisma generate` (postinstall) lit la config Prisma : une URL factice suffit,
# la vraie est fournie par Cloud Run au démarrage.
RUN DATABASE_URL="mysql://build:build@localhost:3306/build" npm ci

RUN apt-get update \
  && npx puppeteer browsers install chrome --install-deps \
  && rm -rf /var/lib/apt/lists/*

COPY . .
RUN npm run build && npm prune --omit=dev

ENV NODE_ENV=production
CMD ["node", "dist/main"]
