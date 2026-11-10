namespace Horizon.Shared;

// Heure_Stanby non nullable en base, 00:00 si absent
public class CreatePointageDto
{
    [Id] public Opt<string> IDPERSONNELS { get; set; }
    [Id] public Opt<string> IDType_Statut { get; set; }
    [Text] public Opt<string> Date_application { get; set; }
    [Text] public Opt<string> Date_heure_debut { get; set; }
    [Text] public Opt<string> Date_heure_fin { get; set; }
    [Text] public Opt<string> Heure_coupure { get; set; }
    [Text] public Opt<string> Heure_liaison { get; set; }
    [Text] public Opt<string> Heure_nuit { get; set; }
    [Text] public Opt<string> Heure_Stanby { get; set; }
    [Text] public Opt<string> Debut_pause { get; set; }
    [Text] public Opt<string> Fin_Pause { get; set; }
    [Text] public Opt<string> Heure_jour { get; set; }
    [Num] public Opt<double> Nuitee { get; set; }
    [Text] public Opt<string> Remarque { get; set; }
}

[Partial]
public class UpdatePointageDto : CreatePointageDto;
