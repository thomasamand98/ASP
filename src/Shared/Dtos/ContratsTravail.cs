namespace Horizon.Shared;

// Formes attendues par ContratTravailController (fiche personnel, onglet
// Contrat). Dates en « AAAA-MM-JJ », taux en nombres (ou strings
// numériques), Routier/Manutention/Atelier en codes 0/1 comme sur Personnel.
public class TauxHoraireDto
{
    // Absent pour un nouveau taux ; présent pour un taux existant à modifier.
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
    // Liste complète des taux du contrat : ceux absents de la liste sont
    // supprimés, ceux sans IDTAUX_HORAIRE créés, les autres modifiés.
    [Nested] public Opt<List<TauxHoraireDto>> Taux { get; set; }
}
