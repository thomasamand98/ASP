namespace Horizon.Shared;

// Plage horaire d'ouverture du point (table `grilles_horaires`, liée par
// IDLIENS = IDPOINTS). Jour_semaine : 1 = lundi … 7 = dimanche (convention
// WinDev) ; heures en « HH:MM ».
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

// Champs qu'un client peut envoyer pour créer/modifier un point.
// IDSOCIETES/IDCONTACTS_DEFAUTS sont des strings côté JSON. IDADRESSES est
// géré par le serveur (voir Common/Adresses.cs). Archive est un code 0/1.
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
    // Adresse du point (table `adresses`, liée par IDADRESSES) : champs
    // aplatis, enregistrés à part par le service.
    [Text] public Opt<string> Adresse1 { get; set; }
    [Text] public Opt<string> Adresse2 { get; set; }
    [Text] public Opt<string> Adresse3 { get; set; }
    [Text] public Opt<string> CP { get; set; }
    [Text] public Opt<string> Localite { get; set; }
    [Text] public Opt<string> Pays { get; set; }
    [Text] public Opt<string> Pays_full_name { get; set; }
    // Plages horaires : remplacent toutes celles du point quand fournies.
    [Nested] public Opt<List<HorairePointDto>> Horaires { get; set; }
}

[Partial]
public class UpdatePointDto : CreatePointDto;

// Lien point ↔ contact (table point_contacts), modifié depuis les cartes de
// l'onglet Contacts de la fiche Point.
public class UpdatePointContactDto
{
    [Num] public Opt<double> Recevoir_Mail_Planning { get; set; }
}
