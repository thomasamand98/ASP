namespace Horizon.Shared;

// Champs qu'un client peut envoyer pour créer/modifier une marchandise.
// CouleurPlanning et IDDECHETS sont des strings côté JSON. Is_dechet/Archive
// sont des codes 0/1 (pas des booléens).
public class CreateMarchandiseDto
{
    [Text] public Opt<string> Nom_marchandise { get; set; }
    [IntegerText] public Opt<string> CouleurPlanning { get; set; }
    [Id] public Opt<string> IDDECHETS { get; set; }
    [Num] public Opt<double> Is_dechet { get; set; }
    [Num] public Opt<double> Archive { get; set; }
}

[Partial]
public class UpdateMarchandiseDto : CreateMarchandiseDto;
