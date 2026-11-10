namespace Horizon.Shared;

public class CreateCommandeDto
{
    [Date] public Opt<string> Date_commande { get; set; }
    [Id] public Opt<string> IDCONTRATS { get; set; }
    [Id] public Opt<string> IDPRESTATIONS { get; set; }
    [Num] public Opt<double> QT { get; set; }
    [Num] public Opt<double> QT_planifie { get; set; }
    [Text] public Opt<string> Statut { get; set; }
    [Text] public Opt<string> NumRef { get; set; }
    [Text] public Opt<string> Instruction { get; set; }
}

[Partial]
public class UpdateCommandeDto : CreateCommandeDto;
