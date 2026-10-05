namespace Horizon.Shared;

// Champs qu'un client peut envoyer pour créer/modifier une société. Les
// adresses (société, facturation client/fournisseur) sont saisies champ par
// champ ; leurs IDADRESSES sont gérés par le serveur (voir
// Common/Adresses.cs). Prospect/Archive sont des codes 0/1 (pas des booléens).
public class CreateSocieteDto : AdresseDto
{
    [Text] public Opt<string> Nom_societe { get; set; }
    [Text] public Opt<string> Denomination { get; set; }
    [Text] public Opt<string> TVA { get; set; }
    [Text] public Opt<string> Activite { get; set; }
    [Text] public Opt<string> Site_web { get; set; }
    [Text] public Opt<string> Note { get; set; }
    [Num] public Opt<double> Prospect { get; set; }
    [Num] public Opt<double> Archive { get; set; }

    // Données de facturation du Client lié, éditées directement depuis la
    // fiche Société (voir SocieteForm.tsx, sous-onglet « Client »). Si la
    // société n'a pas encore de Client lié et que `Client_Numero_client` est
    // fourni, le service en crée un et le lie automatiquement ; sinon le
    // Client déjà lié est mis à jour avec ces champs.
    [Text] public Opt<string> Client_Numero_client { get; set; }
    [Text] public Opt<string> Client_Delai_paiement { get; set; }
    [Dec] public Opt<string> Client_Taux_tva { get; set; }
    [Text] public Opt<string> Client_E_mail_comptabilite { get; set; }
    [Num] public Opt<double> Client_Facture_mail { get; set; }
    [Num] public Opt<double> Client_Adresse_facturation_societe { get; set; }
    [Nested] public Opt<AdresseDto> Client_Adresse_facturation { get; set; }
    [Id] public Opt<string> Client_IDCONTACTS_Comptabilite { get; set; }

    // Idem pour le Fournisseur lié (sous-onglet « Fournisseur »).
    [Text] public Opt<string> Fournisseur_Numero_fournisseur { get; set; }
    [Text] public Opt<string> Fournisseur_Delai_paiement { get; set; }
    [Dec] public Opt<string> Fournisseur_Taux_tva { get; set; }
    [Text] public Opt<string> Fournisseur_E_mail_comptabilite { get; set; }
    [Num] public Opt<double> Fournisseur_Facture_mail { get; set; }
    [Num] public Opt<double> Fournisseur_Adresse_facturation_societe { get; set; }
    [Nested] public Opt<AdresseDto> Fournisseur_Adresse_facturation { get; set; }
    [Id] public Opt<string> Fournisseur_IDCONTACTS_Comptabilite { get; set; }
    [Text] public Opt<string> Fournisseur_Iban { get; set; }
    [Text] public Opt<string> Fournisseur_Bic { get; set; }
}

[Partial]
public class UpdateSocieteDto : CreateSocieteDto;
