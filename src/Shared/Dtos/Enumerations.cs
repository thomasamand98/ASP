namespace Horizon.Shared;

// Valeur_system = 1 : utilisée par le code, non supprimable
public class CreateEnumerationDto
{
    [Id] public Opt<string> IDCATEGORIES_ENUMERATION { get; set; }
    [Text] public Opt<string> Valeur_affiche { get; set; }
    [Text] public Opt<string> Valeur { get; set; }
    [Num] public Opt<double> Ordre { get; set; }
    [Text] public Opt<string> Valeur_associee { get; set; }
    [Num] public Opt<double> Valeur_system { get; set; }
}

[Partial]
public class UpdateEnumerationDto : CreateEnumerationDto;
