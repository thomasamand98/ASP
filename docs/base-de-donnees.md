# Base de données

L'application utilise une base **MySQL** héritée de WinDev. Son schéma ne
change pas avec la migration vers ASP.NET Core : l'API (`src/Api`) y accède
avec Entity Framework Core, dont les entités (`src/Api/Data/Entities`)
décrivent les tables existantes.

## Connexion

L'API lit la chaîne de connexion dans la variable `DATABASE_URL`, au format
`mysql://utilisateur:motdepasse@hôte:3306/base` (voir `src/Api/.env.example`).
En développement, elle est lue dans le fichier `.env` de `src/Api` ou de la
racine du dépôt ; une variable d'environnement définie l'emporte toujours sur
le fichier.

## Historique des migrations

L'historique EF Core commence par la migration `Baseline`
(`src/Api/Data/Migrations`). Elle ne crée ni ne modifie aucune table : elle
fige l'état du schéma au moment de la migration, pour que les migrations
suivantes ne contiennent que de vrais changements.

Sur une base existante, l'appliquer une fois :

```bash
dotnet tool restore
dotnet ef database update --project src/Api
```

La commande crée la table `__EFMigrationsHistory` et y inscrit `Baseline`,
sans toucher aux autres tables. `dotnet ef migrations list --project src/Api`
indique les migrations appliquées ou en attente.

Pour une évolution du schéma : modifier les entités, puis
`dotnet ef migrations add NomDeLaMigration --project src/Api`, relire le code
généré et l'appliquer avec `dotnet ef database update`.

## Créer une base vide

Les scripts de `db/schema` créent le schéma complet, dans l'ordre de leur
nom :

| Script | Contenu |
|---|---|
| `01_init.sql` | Tables héritées de WinDev (62 tables, index, clés étrangères) |
| `02_document_templates.sql` | Table des modèles de document |
| `03_document_template_header_footer.sql` | En-tête et pied de page des modèles |

1. Créer la base, en `utf8mb4` :
   ```sql
   CREATE DATABASE horizon CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
   ```
2. Exécuter les scripts dans l'ordre :
   ```bash
   for f in db/schema/*.sql; do mysql -u root -p horizon < "$f"; done
   ```
3. Renseigner `DATABASE_URL`, puis appliquer `Baseline` comme ci-dessus.

Avec `docker compose up` (voir le README), le conteneur MySQL exécute
lui-même ces scripts au premier démarrage, sur un volume vide.

La base créée ainsi est vide : les référentiels (pays, villes, énumérations,
statuts de pointage...) sont à importer depuis la base de production.
