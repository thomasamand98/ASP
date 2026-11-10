# Horizon

Appli de gestion pour une société de transport et logistique : sociétés et
contacts, contrats et offres de prix, marchandises, attelages (chauffeurs,
véhicules), commandes et planning, personnel et pointage, modèles de documents
et impression PDF.

Elle remplace une appli WinDev et tourne sur sa base MySQL existante, sans
changement de schéma.

## Architecture

```
Navigateur ── Blazor WebAssembly (src/Web) ── HTTP/JSON ──▶ API ASP.NET Core (src/Api) ──▶ MySQL
                        │                       JWT                   │
                        └──────────── types partagés (src/Shared) ─────┘
```

| Projet | Contenu |
|---|---|
| `src/Api` | API REST ASP.NET Core (.NET 10), 109 endpoints. EF Core sur MySQL, auth JWT, PDF via Chromium headless (PuppeteerSharp), OCR Mistral. |
| `src/Web` | Front Blazor WebAssembly (site statique) : une page par écran, onglets, thème clair/sombre. |
| `src/Shared` | DTO d'entrée, modèles de réponse, validation et config JSON, utilisés par l'API et le front. |
| `tests/Api.Tests` | Tests de l'API (xUnit). |
| `tests/Web.Tests` | Tests du front (xUnit + bUnit). |

Dans `src/Api` et `src/Web` le code est rangé par fonctionnalité
(`Features/Contrats`, `Features/Planning`...). Ce qui sert à plusieurs écrans
est dans `Common/` et `Components/`.

## Lancer avec Docker

```bash
docker compose up --build
```

- Front : http://localhost:8080
- API : http://localhost:3000 (Swagger en dev sur `/swagger`)

Au premier démarrage MySQL passe les scripts de `db/schema` et crée un schéma
vide. Les référentiels (pays, villes, énumérations, statuts de pointage...)
sont à importer depuis la prod. Les données restent dans le volume `db-data`,
`docker compose down -v` pour repartir de zéro.

On peut surcharger dans un `.env` à la racine ou dans l'environnement :
`MYSQL_PASSWORD`, `MYSQL_ROOT_PASSWORD`, `JWT_SECRET`, `MISTRAL_API_KEY`,
`LOGIN_SCREEN`. Pour accéder à la pile autrement que par
`http://localhost:8080` (via `127.0.0.1` ou depuis une autre machine), il faut
renseigner `API_URL` et `CORS_ORIGIN` ensemble.

## Développement local

Il faut le [SDK .NET 10](https://dotnet.microsoft.com/download), un MySQL 8
(ou juste le conteneur `db` du `docker-compose.yml`) et Chromium pour les PDF
(PuppeteerSharp le télécharge la première fois).

1. Copier `src/Api/.env.example` en `src/Api/.env` (ou `.env` à la racine) et
   renseigner `DATABASE_URL`. Une variable d'environnement passe avant le
   fichier.

2. Sur une base existante, inscrire une fois la migration de référence :

   ```bash
   dotnet tool restore
   dotnet ef database update --project src/Api
   ```

   Pour partir d'une base vide, voir [docs/base-de-donnees.md](docs/base-de-donnees.md).

3. Lancer l'API (port 3000) :

   ```bash
   dotnet run --project src/Api
   ```

4. Lancer le front (port 5180) dans un autre terminal :

   ```bash
   dotnet run --project src/Web
   ```

   Puis ouvrir http://localhost:5180. Si `ApiUrl` est vide, le front appelle
   l'hôte de la page sur le port 3000, donc rien à régler en local.

   Pour ouvrir l'appli depuis une autre machine du réseau, deux choses en plus.
   Le serveur de dev n'écoute que sur `localhost`, il faut donc le lancer avec
   `dotnet run --project src/Web -- --urls http://0.0.0.0:5180`. Et l'API
   n'accepte par défaut que `localhost` et `127.0.0.1`, il faut donc ajouter
   `CORS_ORIGIN=http://<ip-du-poste>:5180`.

### Tests et build

```bash
dotnet test Horizon.slnx
dotnet build Horizon.slnx
```

Les warnings sont traités comme des erreurs
([`Directory.Build.props`](Directory.Build.props)). On ne commit pas sans un
build propre.

## Configuration

Le front lit `src/Web/wwwroot/appsettings.json` :

| Clé | Rôle |
|---|---|
| `ApiUrl` | URL de l'API. Vide : hôte de la page, port 3000. |
| `LoginScreen` | `true` pour afficher l'écran de connexion. Par défaut `false` : session ouverte automatiquement. |

Dans l'image Docker du front, ce fichier est réécrit au démarrage à partir de
`API_URL` et `LOGIN_SCREEN`, ce qui permet d'utiliser la même image partout.

Côté API, les variables d'environnement (voir
[`src/Api/.env.example`](src/Api/.env.example)) ou les clés équivalentes de
`src/Api/appsettings.json` :

| Variable | Rôle |
|---|---|
| `DATABASE_URL` | Obligatoire. `mysql://utilisateur:motdepasse@hôte:3306/base` |
| `PORT` | Port d'écoute, 3000 par défaut. |
| `CORS_ORIGIN` | Origines autorisées, séparées par des virgules. Vide : `localhost` sur n'importe quel port. |
| `JWT_SECRET` | Secret de signature des jetons, 32 caractères minimum. Sans lui, secret temporaire et sessions perdues à chaque redémarrage. |
| `COOKIE_SAMESITE`, `COOKIE_SECURE` | Cookie de renouvellement de session. `none` impose HTTPS. |
| `THROTTLE_LIMIT`, `LOGIN_THROTTLE_LIMIT` | Requêtes par IP et par minute (300 / 5). |
| `ACCESS_TOKEN_TTL_SECONDS` | Durée de vie du jeton d'accès (900 s). |
| `PDF_STORAGE_DIR` | Dossier des fichiers du serveur (PDF générés, documents joints aux fiches). À sauvegarder avec la base. |
| `CGV_URL` | Lien imprimé en pied de page des contrats PDF. |
| `MISTRAL_API_KEY` | Clé [console.mistral.ai](https://console.mistral.ai), nécessaire pour l'OCR. |
| `TRUST_PROXY` | Nombre de proxys devant l'API, pour que la limitation de débit voie la vraie IP. |

**Attention, la connexion est factice** : `POST /auth/login` accepte n'importe
quel identifiant, l'API n'est donc pas vraiment protégée. Voir
[docs/securite-authentification.md](docs/securite-authentification.md) pour
brancher une vraie authentification.

## Arborescence

```
db/schema/      Scripts de création d'une base vide (lancés par le conteneur MySQL)
docs/           Documentation
src/Api/        API ASP.NET Core
src/Shared/     Types partagés API / front
src/Web/        Front Blazor WebAssembly
tests/          Tests xUnit et bUnit
Horizon.slnx    Solution
docker-compose.yml
```

## Docs

- [Base de données](docs/base-de-donnees.md) : connexion, migrations EF Core, base vide.
- [Sécurité et authentification](docs/securite-authentification.md) : JWT, limitation de débit, CORS, déploiement, et comment remplacer la connexion factice.
- [Impression PDF](docs/impression-pdf.md) : modèles de documents, fusion, rendu Chromium, ajouter l'impression à un autre type de fiche.
- [Migration vers Blazor](docs/migration-blazor.md) : notes de la migration, écran par écran.
- [Améliorations](docs/ameliorations.md) : défauts connus gardés tels quels pendant la migration.
- [Journal des modifications](docs/modifications.md) : énumérations, fiche contrat, PDF, sécurité.
- [Endpoints](docs/ENDPOINTS.md) : liste établie pendant la migration, avant le retrait des endpoints inutilisés. La liste à jour est dans `/swagger` (API lancée en dev).
