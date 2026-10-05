namespace Horizon.Shared;

// Champs qu'un client peut envoyer pour créer/modifier une condition CMR d'un
// contrat. IDCONTRATS est une string côté JSON. Boite_a_cocher est un code
// 0/1 (pas un booléen).
public class CreateConditionCmrDto
{
    [Id] public Opt<string> IDCONTRATS { get; set; }
    [Text] public Opt<string> Libelle { get; set; }
    [Num] public Opt<double> Boite_a_cocher { get; set; }
}

// IDCONTRATS n'est pas modifiable : une condition reste attachée au contrat
// pour lequel elle a été créée.
public class UpdateConditionCmrDto
{
    [Text] public Opt<string> Libelle { get; set; }
    [Num] public Opt<double> Boite_a_cocher { get; set; }
}
