using System.Reflection;
using Api.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MySql.Data.MySqlClient;

namespace Api.Tests.Common;

public class ExceptionMiddlewareTests
{
    private static (int Status, string Message) Run(Exception exception) => ExceptionMiddleware.Translate(exception, NullLogger.Instance);

    // pas de constructeur public avec le code d'erreur
    private static MySqlException MySqlError(int number)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var constructor = typeof(MySqlException).GetConstructor(flags, [typeof(string), typeof(int)]);
        if (constructor is not null) return (MySqlException)constructor.Invoke(["détail interne", number]);

        var exception = (MySqlException)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MySqlException));
        var field = typeof(MySqlException).GetFields(flags).First(f => f.FieldType == typeof(int) && f.Name.Contains("Number", StringComparison.OrdinalIgnoreCase));
        field.SetValue(exception, number);
        return exception;
    }

    [Fact]
    public void Garde_le_statut_et_le_message_des_HttpException()
    {
        Assert.Equal((404, "Société introuvable"), Run(new NotFoundException("Société introuvable")));
        Assert.Equal((404, "Enregistrement introuvable."), Run(new RecordNotFoundException()));
    }

    [Theory]
    [InlineData(1062, 409)] // doublon
    [InlineData(1451, 409)] // ligne encore référencée
    [InlineData(1452, 409)] // référence introuvable
    [InlineData(1406, 400)] // valeur trop longue
    public void Traduit_les_erreurs_MySQL_previsibles_sans_exposer_de_detail(int number, int expected)
    {
        var (status, message) = Run(new DbUpdateException("échec", MySqlError(number)));
        Assert.Equal(expected, status);
        Assert.DoesNotContain("détail interne", message);
    }

    [Theory]
    [InlineData(1045)] // identifiants refusés
    [InlineData(1049)] // base inconnue
    [InlineData(1042)] // serveur injoignable
    [InlineData(1040)] // trop de connexions
    public void Renvoie_503_quand_la_base_est_indisponible(int number)
    {
        Assert.Equal((503, "Base de données indisponible, réessayez dans quelques instants."), Run(MySqlError(number)));
    }

    [Fact]
    public void Renvoie_404_quand_la_ligne_modifiee_a_disparu()
    {
        Assert.Equal((404, "Enregistrement introuvable."), Run(new DbUpdateConcurrencyException("0 ligne modifiée")));
    }

    [Fact]
    public void Renvoie_500_generique_pour_une_erreur_non_repertoriee_ou_un_bug()
    {
        Assert.Equal(500, Run(MySqlError(1146)).Status); // table inconnue
        Assert.Equal((500, "Erreur interne du serveur"), Run(new InvalidOperationException("boom")));
    }
}
