-- Historique des migrations EF Core : inscrit la migration Baseline, qui ne
-- crée rien (le schéma vient des scripts précédents). Équivaut à
-- `dotnet ef database update` sur cette base (voir docs/base-de-donnees.md).

CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) NOT NULL,
    `ProductVersion` varchar(32) NOT NULL,
    PRIMARY KEY (`MigrationId`)
);

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261001121406_Baseline', '10.0.12');
