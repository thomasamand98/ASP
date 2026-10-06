# Horizon

Application de gestion pour une société de transport et de logistique :
sociétés et contacts, contrats et offres de prix, marchandises, attelages
(chauffeurs, véhicules), commandes et planning d'exécution, personnel et
pointage, modèles de documents et impression PDF.

Elle remplace une application WinDev et travaille sur **sa base MySQL
existante**, dont le schéma n'a pas changé.

## Architecture

```
Navigateur ── Blazor WebAssembly (src/Web) ── HTTP/JSON ──▶ API ASP.NET Core (src/Api) ──▶ MySQL
                        │                       JWT                   │
                        └──────────── types partagés (src/Shared) ─────┘
```

| Projet | Rôle |
|---|---|
| `src/Api` | API REST ASP.NET Core (.NET 10) : 109 endpoints, Entity Framework Core sur MySQL, authentification JWT, génération de PDF (Chromium headless via PuppeteerSharp), OCR Mistral. |
| `src/Web` | Front Blazor WebAssembly : site statique, une page par écran, onglets façon navigateur, thème clair/sombre. |
| `src/Shared` | DTO d'entrée, modèles de réponse, règles de validation et sérialisation JSON communs aux deux — une seule définition du contrat d'API. |
| `tests/Api.Tests` | Tests de l'API (xUnit). |
| `tests/Web.Tests` | Tests de composants et de logique du front (xUnit + bUnit). |

Dans `src/Api` comme dans `src/Web`, le code est rangé **par fonctionnalité**
(`Features/Contrats`, `Features/Planning`…) ; `Common/` et `Components/`
contiennent ce qui est partagé entre écrans.

## Démarrage rapide (Docker)

Tout l'environnement — base MySQL vide, API et front — en une commande :

```bash
docker compose up --build
```

- Front : http://localhost:8080
- API : http://localhost:3000 (Swagger en développement : `/swagger`)

Au premier démarrage, MySQL exécute les scripts de `db/schema` et crée un
schéma **vide** : les référentiels (pays, villes, énumérations, statuts de
pointage…) sont à importer depuis la base de production. Les données
persistent dans le volume `db-data` ; `docker compose down -v` repart de zéro.

Les valeurs se surchargent dans un fichier `.env` à la racine ou dans
l'environnement : `MYSQL_PASSWORD`, `MYSQL_ROOT_PASSWORD`, `JWT_SECRET`,
`MISTRAL_API_KEY`, `LOGIN_SCREEN`. Pour atteindre la pile autrement que par
`http://localhost:8080` — par `127.0.0.1`, ou depuis une autre machine du
réseau — renseigner `API_URL` **et** `CORS_ORIGIN` ensemble.

## Développement local

**Prérequis** : [SDK .NET 10](https://dotnet.microsoft.com/download), MySQL 8
(ou le conteneur `db` du `docker-compose.yml`), et un navigateur Chromium pour
l'impression PDF (PuppeteerSharp le télécharge au premier usage).

1. **Configurer l'accès à la base.** Copier `src/Api/.env.example` en
   `src/Api/.env` (ou `.env` à la racine du dépôt) et renseigner
   `DATABASE_URL`. Une variable d'environnement l'emporte toujours sur le
   fichier.

2. **Préparer la base.** Sur une base existante, inscrire une fois la
   migration de référence :

   ```bash
   dotnet tool restore
   dotnet ef database update --project src/Api
   ```

   Pour partir d'une base vide, voir [docs/base-de-donnees.md](docs/base-de-donnees.md).

3. **Lancer l'API** (port 3000) :

   ```bash
   dotnet run --project src/Api
   ```

4. **Lancer le front** (port 5180), dans un second terminal :

   ```bash
   dotnet run --project src/Web
   ```

   → http://localhost:5180. Sans `ApiUrl` configuré, le front appelle l'hôte
   de la page sur le port 3000 : rien à régler en local.

   Pour ouvrir l'application depuis une autre machine du réseau, il faut deux
   réglages de plus : exposer le serveur de développement
   (`dotnet run --project src/Web -- --urls http://0.0.0.0:5180`, car il
   n'écoute que sur `localhost`) et autoriser l'origine côté API
   (`CORS_ORIGIN=http://<ip-du-poste>:5180` — par défaut, seules les pages
   ouvertes sur `localhost` ou `127.0.0.1` sont acceptées).

### Tests

```bash
dotnet test Horizon.slnx
```

### Construire la solution

```bash
dotnet build Horizon.slnx
```

Les avertissements sont traités comme des erreurs
([`Directory.Build.props`](Directory.Build.props)) : un build propre est la
condition d'un commit.

## Configuration

Le front lit `src/Web/wwwroot/appsettings.json` :

| Clé | Rôle |
|---|---|
| `ApiUrl` | URL de l'API. Vide : hôte de la page + port 3000. |
| `LoginScreen` | `true` pour afficher l'écran de connexion. `false` (défaut) : session ouverte automatiquement au chargement. |

En production, l'image Docker du front réécrit ce fichier au démarrage à
partir des variables `API_URL` et `LOGIN_SCREEN` : une même image sert
plusieurs environnements.

L'API accepte les variables d'environnement suivantes (toutes documentées dans
[`src/Api/.env.example`](src/Api/.env.example)), ou les clés équivalentes de
`src/Api/appsettings.json` :

| Variable | Rôle |
|---|---|
| `DATABASE_URL` | Connexion MySQL, `mysql://utilisateur:motdepasse@hôte:3306/base`. **Requise.** |
| `PORT` | Port d'écoute (3000 par défaut). |
| `CORS_ORIGIN` | Origines autorisées, séparées par des virgules. Vide : `localhost` sur n'importe quel port. |
| `JWT_SECRET` | Secret de signature des jetons (32 caractères minimum). Absent : secret temporaire, sessions perdues à chaque redémarrage. |
| `COOKIE_SAMESITE`, `COOKIE_SECURE` | Cookie de renouvellement de session. `none` impose HTTPS. |
| `THROTTLE_LIMIT`, `LOGIN_THROTTLE_LIMIT` | Requêtes par IP et par minute (300 / 5). |
| `ACCESS_TOKEN_TTL_SECONDS` | Durée de vie du jeton d'accès (900 s). |
| `PDF_STORAGE_DIR` | Dossier des fichiers du serveur : PDF générés et documents joints aux fiches. **À sauvegarder avec la base.** |
| `CGV_URL` | Cible du lien imprimé en pied de page des contrats PDF. |
| `MISTRAL_API_KEY` | Clé [console.mistral.ai](https://console.mistral.ai), requise pour l'OCR. |
| `TRUST_PROXY` | Nombre de proxys devant l'API, pour que la limitation de débit voie la vraie adresse IP. |

> ⚠️ **La connexion est factice** : `POST /auth/login` accepte n'importe quel
> identifiant. L'API n'est donc pas réellement protégée. Voir
> [docs/securite-authentification.md](docs/securite-authentification.md) pour
> la remplacer par une vraie authentification.

## Structure du dépôt

```
db/schema/      Scripts de création d'une base vide (exécutés par le conteneur MySQL)
docs/           Documentation (voir ci-dessous)
src/Api/        API ASP.NET Core
src/Shared/     Types partagés API ↔ front
src/Web/        Front Blazor WebAssembly
tests/          Tests xUnit et bUnit
Horizon.slnx    Solution
docker-compose.yml
```

## Documentation

- [Base de données](docs/base-de-donnees.md) — connexion, migrations EF Core, création d'une base vide.
- [Sécurité et authentification](docs/securite-authentification.md) — JWT, limitation de débit, CORS, en-têtes, déploiement, remplacement de la connexion factice.
- [Impression PDF](docs/impression-pdf.md) — modèles de documents, moteur de fusion, rendu Chromium, ajout de l'impression à un autre type de fiche.
- [Migration du front React vers Blazor](docs/migration-blazor.md) — correspondance écran par écran, choix techniques, interop JavaScript.
- [Améliorations repérées](docs/ameliorations.md) — défauts connus conservés à l'identique pendant la migration, et pistes.
- [Journal des modifications](docs/modifications.md) — historique des lots fonctionnels (énumérations, fiche contrat, PDF, sécurité).
- [Endpoints](docs/ENDPOINTS.md) — correspondance avec l'ancien backend NestJS, établie pendant la migration (antérieure à la suppression des endpoints qu'aucun écran n'appelait). Pour la liste à jour, lancer l'API en développement et ouvrir `/swagger`.
