namespace Horizon.Shared;

// onglet Contrat de la fiche personnel, dates en AAAA-MM-JJ
public class TauxHoraireDto
{
    // absent = nouveau taux
    [Id] public Opt<string> IDTAUX_HORAIRE { get; set; }
    [Date] public Opt<string> Date_application { get; set; }
    [Amount] public Opt<object> Taux_horaire { get; set; }
    [Amount] public Opt<object> Taux_horaire_supplementaire { get; set; }
    [Amount] public Opt<object> Taux_horaire_nuitee { get; set; }
    [Amount] public Opt<object> Taux_nuitee { get; set; }
    [Amount] public Opt<object> Taux_samedi { get; set; }
    [Amount] public Opt<object> Taux_cheque_repas { get; set; }
    [Amount] public Opt<object> Taux_stanby { get; set; }
}

public class ContratTravailDto
{
    [Text] public Opt<string> TypeContrat { get; set; }
    [Text] public Opt<string> Qualification { get; set; }
    [Date] public Opt<string> Date_debut { get; set; }
    [Date] public Opt<string> Date_fin { get; set; }
    [Num] public Opt<double> Routier { get; set; }
    [Num] public Opt<double> Manutention { get; set; }
    [Num] public Opt<double> Atelier { get; set; }
    // liste complète, taux absents supprimés
    [Nested] public Opt<List<TauxHoraireDto>> Taux { get; set; }
}
