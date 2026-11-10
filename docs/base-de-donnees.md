# Base de données

Base MySQL héritée de WinDev, schéma inchangé. L'API y accède avec EF Core ;
les entités de `src/Api/Data/Entities` décrivent les tables existantes.

## Connexion

Chaîne de connexion dans `DATABASE_URL`, au format
`mysql://utilisateur:motdepasse@hôte:3306/base` (voir `src/Api/.env.example`).
En dev elle est lue dans le `.env` de `src/Api` ou de la racine. Une variable
d'environnement passe toujours avant le fichier.

## Migrations

L'historique EF Core commence par la migration `Baseline`
(`src/Api/Data/Migrations`). Elle ne crée et ne modifie rien, elle sert juste
de point de départ pour que les migrations suivantes ne contiennent que de
vrais changements.

Sur une base existante, l'appliquer une fois :

```bash
dotnet tool restore
dotnet ef database update --project src/Api
```

Ça crée `__EFMigrationsHistory` et y inscrit `Baseline`, sans toucher au
reste. `dotnet ef migrations list --project src/Api` affiche les migrations
appliquées ou en attente.

Pour faire évoluer le schéma : modifier les entités, lancer
`dotnet ef migrations add NomDeLaMigration --project src/Api`, relire le code
généré, puis `dotnet ef database update`.

## Créer une base vide

Les scripts de `db/schema` créent tout le schéma, dans l'ordre des noms :

| Script | Contenu |
|---|---|
| `01_init.sql` | Tables WinDev (62 tables, index, clés étrangères) |
| `02_document_templates.sql` | Table des modèles de document |
| `03_document_template_header_footer.sql` | En-tête et pied de page des modèles |
| `04_ef_baseline.sql` | Inscrit `Baseline` dans `__EFMigrationsHistory` |

1. Créer la base en `utf8mb4` :
   ```sql
   CREATE DATABASE horizon CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
   ```
2. Passer les scripts dans l'ordre :
   ```bash
   for f in db/schema/*.sql; do mysql -u root -p horizon < "$f"; done
   ```
3. Renseigner `DATABASE_URL`. Pas besoin d'appliquer `Baseline`, c'est déjà
   fait par `04_ef_baseline.sql` (`dotnet ef database update` ne fera rien).

`04_ef_baseline.sql` contient en dur le `MigrationId` et le `ProductVersion`
de `Baseline`. Si la migration est renommée ou qu'EF Core change de version,
mettre le script à jour aussi, sinon une base créée avec ces scripts
rejouerait `Baseline` ou la croirait appliquée à tort.

Avec `docker compose up` (voir le README), le conteneur MySQL lance ces
scripts tout seul au premier démarrage, sur un volume vide.

La base obtenue est vide : les référentiels (pays, villes, énumérations,
statuts de pointage...) sont à importer depuis la prod.
