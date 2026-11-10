using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Api.Data;

// mapping dans AppDbContext.Model.g.cs
public partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) => ConfigureGeneratedModel(modelBuilder);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<WallClockConverter>();
    }

    // pas de conversion de fuseau par le pilote MySQL, tout est en UTC
    private sealed class WallClockConverter() : ValueConverter<DateTime, DateTime>(
        value => DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
        value => DateTime.SpecifyKind(value, DateTimeKind.Unspecified));
}
