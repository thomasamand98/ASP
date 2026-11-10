using Horizon.Shared.Models;
using Horizon.Web.Features.Pointages;

namespace Horizon.Web.Tests.Features;

public class FeuilleCalcTests
{
    // colonne TIME telle que renvoyée par l'API
    private static string Time(string hhmm) => $"1970-01-01T{hhmm}:00.000Z";

    [Fact]
    public void TimeMinutes_lit_heures_et_minutes_d_une_colonne_TIME()
    {
        Assert.Equal(456, FeuilleCalc.TimeMinutes(Time("07:36")));
        Assert.Equal(0, FeuilleCalc.TimeMinutes(null));
    }

    [Fact]
    public void FormatDuration_formate_une_journee_et_un_total()
    {
        Assert.Equal("07:36", FeuilleCalc.FormatDuration(456));
        Assert.Equal("45 h 36", FeuilleCalc.FormatDuration(2736, total: true));
    }

    [Fact]
    public void FormatDuration_garde_le_signe_d_une_duree_negative() =>
        Assert.Equal("-01:30", FeuilleCalc.FormatDuration(-90));

    [Fact]
    public void WorkedMinutes_prend_le_forfait_Heure_jour_quand_il_est_renseigne()
    {
        var p = new PointageFeuille { Heure_jour = Time("07:36"), Date_heure_debut = "2026-09-21T06:00:00Z", Date_heure_fin = "2026-09-21T18:00:00Z" };
        Assert.Equal(456, FeuilleCalc.WorkedMinutes(p));
    }

    [Fact]
    public void WorkedMinutes_retire_coupure_et_pause_du_total()
    {
        var p = new PointageFeuille
        {
            Date_heure_debut = "2026-09-21T06:00:00Z",
            Date_heure_fin = "2026-09-21T16:00:00Z",
            Heure_coupure = Time("00:45"),
            Debut_pause = Time("12:00"),
            Fin_Pause = Time("12:30"),
        };
        Assert.Equal(600 - 45 - 30, FeuilleCalc.WorkedMinutes(p));
    }

    [Fact]
    public void WorkedMinutes_ne_descend_jamais_sous_zero()
    {
        var p = new PointageFeuille { Date_heure_debut = "2026-09-21T06:00:00Z", Date_heure_fin = "2026-09-21T06:30:00Z", Heure_coupure = Time("01:00") };
        Assert.Equal(0, FeuilleCalc.WorkedMinutes(p));
    }

    // sam. 19 et dim. 20/09/2026, lun. 21 sans pointage
    private static readonly DateTime[] Jours = [new(2026, 9, 19), new(2026, 9, 20), new(2026, 9, 21)];

    private static readonly PointageFeuille[] Pointages =
    [
        new() { Date_application = "2026-09-19T00:00:00.000Z", Heure_jour = Time("08:00"), Nuitee = 1, Heure_nuit = Time("02:00") },
        new() { Date_application = "2026-09-20T00:00:00.000Z", Heure_jour = Time("04:00"), Heure_Stanby = Time("01:30") },
    ];

    [Fact]
    public void BuildDays_range_les_pointages_par_jour() =>
        Assert.Equal([1, 1, 0], FeuilleCalc.BuildDays(Jours, Pointages).Select(d => d.Pointages.Count));

    [Fact]
    public void ComputeTotals_compte_jours_prestes_samedis_dimanches_et_nuitees()
    {
        var totals = FeuilleCalc.ComputeTotals(FeuilleCalc.BuildDays(Jours, Pointages));
        Assert.Equal(720, totals.Worked);
        Assert.Equal(2, totals.JoursPrestes);
        Assert.Equal(1, totals.Samedis);
        Assert.Equal(240, totals.HeuresDimanche);
        Assert.Equal(1, totals.Nuitees);
        Assert.Equal(120, totals.Nuit);
        Assert.Equal(90, totals.Standby);
    }

    [Fact]
    public void ComputeAmounts_calcule_les_montants_a_partir_des_taux()
    {
        var totals = FeuilleCalc.ComputeTotals(FeuilleCalc.BuildDays(Jours, Pointages));
        var taux = new FeuilleTaux(null, null, null, Taux_horaire_nuitee: "2.5", Taux_nuitee: "30", Taux_samedi: "10", Taux_cheque_repas: "8", Taux_stanby: "4");
        Assert.Equal(new SheetAmounts(ChequeRepas: 16, Nuitees: 30, HeuresNuit: 5, Samedis: 10, Standby: 6), FeuilleCalc.ComputeAmounts(totals, taux));
    }

    [Fact]
    public void ComputeAmounts_ne_calcule_rien_sans_contrat() =>
        Assert.Null(FeuilleCalc.ComputeAmounts(FeuilleCalc.ComputeTotals(FeuilleCalc.BuildDays(Jours, Pointages)), null));
}
