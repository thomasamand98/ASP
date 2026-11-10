using System.Globalization;
using Api.Common;
using Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Statistiques;

// facturé : hors proformas, NC en négatif ; production : prix prestation + suppléments
public class StatistiqueService(AppDbContext db)
{
    public async Task<object> GetChiffreAffairesAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var granularite = ChiffreAffaires.GranulariteDe(from, to);
        var (fromN1, toN1) = (ChiffreAffaires.DecalerAnnee(from, -1), ChiffreAffaires.DecalerAnnee(to, -1));
        var facture = ChiffreAffaires.CumulerParIntervalle(await LignesFactureesAsync(from, to, ct), granularite);
        var factureN1 = ChiffreAffaires.CumulerParIntervalle(await LignesFactureesAsync(fromN1, toN1, ct), granularite, 1);
        var production = ChiffreAffaires.CumulerParIntervalle(await LignesProductionAsync(from, to, ct), granularite);
        var productionN1 = ChiffreAffaires.CumulerParIntervalle(await LignesProductionAsync(fromN1, toN1, ct), granularite, 1);

        var points = ChiffreAffaires.Intervalles(from, to, granularite).Select(cle => new
        {
            cle,
            facture = ChiffreAffaires.ArrondiCentimes(facture.GetValueOrDefault(cle)),
            factureN1 = ChiffreAffaires.ArrondiCentimes(factureN1.GetValueOrDefault(cle)),
            production = ChiffreAffaires.ArrondiCentimes(production.GetValueOrDefault(cle)),
            productionN1 = ChiffreAffaires.ArrondiCentimes(productionN1.GetValueOrDefault(cle)),
        });
        return new { granularite, points };
    }

    private async Task<List<(DateTime?, double)>> LignesFactureesAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var factures = await db.Facture
            .Where(f => f.Date_Facture >= from && f.Date_Facture <= to && (f.Proformat == 0 || f.Proformat == null))
            .Select(f => new { f.Date_Facture, f.Montant_Facture_HT, f.Note_de_Credit })
            .ToListAsync(ct);
        return factures.Select(f =>
        {
            var montant = Math.Abs(ToNumber(f.Montant_Facture_HT));
            return (f.Date_Facture, f.Note_de_Credit is not (null or 0) ? -montant : montant);
        }).ToList();
    }

    private async Task<List<(DateTime?, double)>> LignesProductionAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var end = to.AddDays(1);
        var executions = await db.Execution
            .Where(e => e.Date_Execution >= from && e.Date_Execution < end && (e.Execution_dechargement == 0 || e.Execution_dechargement == null))
            .Select(e => new
            {
                e.IDEXECUTIONS,
                e.Date_Execution,
                e.Qt_depart,
                e.Qt_arrivee,
                HasPrestation = e.Prestation != null,
                e.Prestation!.Prix_unitaire,
                e.Prestation!.Qt_client_facturation,
            })
            .ToListAsync(ct);
        var ids = executions.Select(e => (long?)e.IDEXECUTIONS).ToList();
        var supplements = (await db.ExecutionSupplementaire.Where(s => ids.Contains(s.IDEXECUTIONS))
                .Select(s => new { s.IDEXECUTIONS, s.Quantite, s.Prix_unitaire_ES })
                .ToListAsync(ct))
            .ToLookup(s => s.IDEXECUTIONS!.Value);

        return executions.Select(e =>
        {
            var prix = e.HasPrestation ? ToNumber(e.Prix_unitaire) : 0;
            // qté d'arrivée, sinon de départ
            var quantite = e.HasPrestation && e.Qt_client_facturation is not (null or 0)
                ? (e.Qt_arrivee is not (null or 0) ? e.Qt_arrivee.Value : e.Qt_depart is not (null or 0) ? e.Qt_depart.Value : 0)
                : 1;
            var total = supplements[e.IDEXECUTIONS].Aggregate(0d, (sum, s) => sum + (s.Quantite ?? 0) * ToNumber(s.Prix_unitaire_ES));
            return (e.Date_Execution, prix * quantite + total);
        }).ToList();
    }

    // via string pour avoir le double le plus proche
    private static double ToNumber(decimal? value) =>
        value is null ? 0 : double.Parse(value.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}

public class StatistiqueController(StatistiqueService service) : ApiControllerBase
{
    // bornes incluses
    [HttpGet("statistiques/chiffre-affaires")]
    public Task<object> GetChiffreAffaires([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        var period = Params.ParsePeriod(from, to);
        if (period?.Gte is null || period.Lte is null) throw new BadRequestException("Les paramètres « from » et « to » sont obligatoires.");
        return service.GetChiffreAffairesAsync(period.Gte.Value, period.Lte.Value, ct);
    }
}
