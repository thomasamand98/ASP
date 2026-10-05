namespace Horizon.Shared;

// Champs qu'un client peut envoyer pour créer/modifier une catégorie
// d'énumération. Enum_system est un code 0/1 (pas un booléen) : 1 =
// catégorie utilisée par le code applicatif, non supprimable.
public class CreateCategorieEnumerationDto
{
    [Text] public Opt<string> Nom { get; set; }
    [Text] public Opt<string> Nom_affiche { get; set; }
    [Num] public Opt<double> Enum_system { get; set; }
}

[Partial]
public class UpdateCategorieEnumerationDto : CreateCategorieEnumerationDto;
