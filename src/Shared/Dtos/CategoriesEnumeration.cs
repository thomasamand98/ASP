namespace Horizon.Shared;

// Enum_system = 1 : utilisée par le code, non supprimable
public class CreateCategorieEnumerationDto
{
    [Text] public Opt<string> Nom { get; set; }
    [Text] public Opt<string> Nom_affiche { get; set; }
    [Num] public Opt<double> Enum_system { get; set; }
}

[Partial]
public class UpdateCategorieEnumerationDto : CreateCategorieEnumerationDto;
