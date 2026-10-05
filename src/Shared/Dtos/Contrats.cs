namespace Horizon.Shared;

// Champs qu'un client peut envoyer pour créer/modifier un contrat.
// IDSOCIETES/IDMARCHANDISES/IDTYPES_FACTURE sont des strings côté JSON,
// Date_debut/Date_fin des strings ISO, et Taux_tva une string (Decimal en
// base). Offre_de_prix/Archive/Qt_client_facturation/Suivant sont des codes
// 0/1 (pas des booléens).
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
