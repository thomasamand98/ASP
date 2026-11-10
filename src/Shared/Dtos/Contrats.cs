namespace Horizon.Shared;

// Taux_tva en texte (Decimal en base)
public class CreateContratDto
{
    [Text] public Opt<string> Num_contrat { get; set; }
    [Text] public Opt<string> Description_projet { get; set; }
    [Date] public Opt<string> Date_debut { get; set; }
    [Date] public Opt<string> Date_fin { get; set; }
    [Id] public Opt<string> IDSOCIETES { get; set; }
    [Id] public Opt<string> IDTYPES_FACTURE { get; set; }
    [Text] public Opt<string> Annee_archivage { get; set; }
    [Num] public Opt<double> Offre_de_prix { get; set; }
    [Text] public Opt<string> Note_confidentielle { get; set; }
    [Text] public Opt<string> Instruction_CMR { get; set; }
    [Text] public Opt<string> Version_contrat { get; set; }
    [Num] public Opt<double> Archive { get; set; }
    [Text] public Opt<string> Reference_client { get; set; }
    [Dec] public Opt<string> Taux_tva { get; set; }
    [Num] public Opt<double> Qt_client_facturation { get; set; }
    [Num] public Opt<double> Suivant { get; set; }
    [Id] public Opt<string> IDMARCHANDISES { get; set; }
    [Text] public Opt<string> Commissionnaire { get; set; }
    [Text] public Opt<string> Type_contrat { get; set; }
}

[Partial]
public class UpdateContratDto : CreateContratDto;
