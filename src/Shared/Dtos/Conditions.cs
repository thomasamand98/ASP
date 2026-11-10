namespace Horizon.Shared;

public class CreateConditionDto
{
    [Num] public Opt<double> Type_Prestation { get; set; }
    [Num] public Opt<double> CMR_or_FDR { get; set; }
    [Text] public Opt<string> Libelle { get; set; }
}

[Partial]
public class UpdateConditionDto : CreateConditionDto;
