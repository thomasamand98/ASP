namespace Horizon.Shared;

// CouleurPlanning : couleur WinDev en texte
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
