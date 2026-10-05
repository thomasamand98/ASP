namespace Horizon.Shared;

// Champs qu'un client peut envoyer pour créer/modifier un pointage.
// IDPERSONNELS/IDType_Statut sont des strings côté JSON, les Date_*/Heure_*
// des strings — converties par le service. Nuitee est un code 0/1 (pas un
// booléen). Heure_Stanby n'est PAS nullable en base — un défaut 00:00 est
// appliqué par le service si omis.
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
