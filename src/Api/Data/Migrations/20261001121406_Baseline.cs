using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <summary>
    /// Migration de référence : point de départ de l'historique EF Core.
    ///
    /// Le schéma existe déjà (base héritée de WinDev, puis scripts de
    /// db/schema) : cette migration ne
    /// crée ni ne modifie donc rien. Elle enregistre seulement, dans
    /// l'instantané du modèle (AppDbContextModelSnapshot), l'état du schéma au
    /// moment de la migration vers ASP.NET Core, pour que les migrations
    /// suivantes ne contiennent que les changements réels.
    ///
    /// L'appliquer (`dotnet ef database update`) crée la table
    /// `__EFMigrationsHistory` et y inscrit cette migration, sans toucher aux
    /// tables existantes. Sur une base vide, créer d'abord le schéma avec les
    /// scripts SQL de db/schema (voir docs/base-de-donnees.md).
    /// </summary>
    public partial class Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
