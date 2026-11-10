namespace Horizon.Shared;

// champs d'adresse d'un DTO, tous optionnels
public interface IAdresseFields
{
    Opt<string> Adresse1 { get; }
    Opt<string> Adresse2 { get; }
    Opt<string> Adresse3 { get; }
    Opt<string> CP { get; }
    Opt<string> Localite { get; }
    Opt<string> Pays { get; }
    Opt<string> Pays_full_name { get; }
}

public class AdresseDto : IAdresseFields
{
    [Text] public Opt<string> Adresse1 { get; set; }
    [Text] public Opt<string> Adresse2 { get; set; }
    [Text] public Opt<string> Adresse3 { get; set; }
    [Text] public Opt<string> CP { get; set; }
    [Text] public Opt<string> Localite { get; set; }
    [Text] public Opt<string> Pays { get; set; }
    [Text] public Opt<string> Pays_full_name { get; set; }
}
