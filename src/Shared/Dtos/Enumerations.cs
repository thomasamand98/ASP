namespace Horizon.Shared;

// Champs qu'un client peut envoyer pour créer/modifier une énumération.
// IDCATEGORIES_ENUMERATION est une string côté JSON. Valeur_system est un
// code 0/1 (pas un booléen) : 1 = valeur utilisée par le code applicatif,
// non supprimable.
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
