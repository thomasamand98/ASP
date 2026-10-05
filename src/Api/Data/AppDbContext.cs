using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Api.Data;

// Accès aux données (équivalent de PrismaService) : une instance par requête
// HTTP, injectée dans les services — jamais directement dans un contrôleur.
// Les entités et leur configuration sont générées depuis schema.prisma
// (voir tools/prisma-to-ef.mjs → AppDbContext.Model.g.cs).
public partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) => ConfigureGeneratedModel(modelBuilder);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<WallClockConverter>();
    }

    // Comme Prisma, les dates sont échangées avec MySQL telles quelles, sans
    // conversion de fuseau : la valeur écrite en base est l'heure UTC, et la
    // valeur lue est renvoyée au front comme de l'UTC. Sans cela, le pilote
    // MySQL convertit les colonnes TIMESTAMP selon le fuseau de la session.
    private sealed class WallClockConverter() : ValueConverter<DateTime, DateTime>(
        value => DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
        value => DateTime.SpecifyKind(value, DateTimeKind.Unspecified));
}
