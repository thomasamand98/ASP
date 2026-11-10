namespace Horizon.Shared;

// Boite_a_cocher en 0/1, pas un booléen
public class CreateConditionCmrDto
{
    [Id] public Opt<string> IDCONTRATS { get; set; }
    [Text] public Opt<string> Libelle { get; set; }
    [Num] public Opt<double> Boite_a_cocher { get; set; }
}

// pas d'IDCONTRATS : condition liée à vie à son contrat
public class UpdateConditionCmrDto
{
    [Text] public Opt<string> Libelle { get; set; }
    [Num] public Opt<double> Boite_a_cocher { get; set; }
}
