namespace Horizon.Shared;

// grilles_horaires (IDLIENS = IDPOINTS), Jour_semaine 1 = lundi comme WinDev
public class HorairePointDto : IRefinable
{
    private const string Hhmm = @"^([01]\d|2[0-3]):[0-5]\d$";
    private const string HhmmMessage = "heure « HH:MM » attendue";

    [StrictInt(1, 7, Required = true)] public Opt<int> Jour_semaine { get; set; }
    [Pattern(Hhmm, HhmmMessage, Required = true)] public Opt<string> Heure_debut { get; set; }
    [Pattern(Hhmm, HhmmMessage, Required = true)] public Opt<string> Heure_fin { get; set; }

    public void Refine(Action<string, string> addIssue)
    {
        if (Heure_debut.IsSet && Heure_fin.IsSet && string.CompareOrdinal(Heure_fin.Value, Heure_debut.Value) <= 0)
        {
            addIssue(nameof(Heure_fin), "la fin doit suivre le début");
        }
    }
}

public class CreatePointDto : IAdresseFields
{
    [Text] public Opt<string> Libelle { get; set; }
    [Text] public Opt<string> Nom_societe { get; set; }
    [Text] public Opt<string> Telephone { get; set; }
    [Id] public Opt<string> IDSOCIETES { get; set; }
    [Num] public Opt<double> Archive { get; set; }
    [Text] public Opt<string> Lien_googleMap { get; set; }
    [Text] public Opt<string> Instruction { get; set; }
    [Id] public Opt<string> IDCONTACTS_DEFAUTS { get; set; }
    // adresse aplatie
    [Text] public Opt<string> Adresse1 { get; set; }
    [Text] public Opt<string> Adresse2 { get; set; }
    [Text] public Opt<string> Adresse3 { get; set; }
    [Text] public Opt<string> CP { get; set; }
    [Text] public Opt<string> Localite { get; set; }
    [Text] public Opt<string> Pays { get; set; }
    [Text] public Opt<string> Pays_full_name { get; set; }
    // si fourni, remplace toutes les plages
    [Nested] public Opt<List<HorairePointDto>> Horaires { get; set; }
}

[Partial]
public class UpdatePointDto : CreatePointDto;

// lien point/contact (point_contacts)
public class UpdatePointContactDto
{
    [Num] public Opt<double> Recevoir_Mail_Planning { get; set; }
}
