namespace Horizon.Shared;

// Catégories de document (constantes DOCUMENT_CATEGORIE_* de WinDev) :
// valeur de lien_documents.TYPE et de documents.Categorie. Le TYPE dit à quel
// genre d'enregistrement se rapporte lien_documents.IDReferent (TYPE 6 :
// IDReferent = IDSOCIETES, TYPE 3 : IDPERSONNELS, TYPE 4 : IDVEHICULES...).
// Utilisées par l'API (validation) et par le front (onglet Documents des fiches).
public static class DocumentCategorie
{
    public const int Contrat = 1;
    public const int Facture = 2;
    public const int Personnel = 3;
    public const int Vehicule = 4;
    public const int Client = 5;
    public const int Societe = 6;
    public const int Divers = 7;
    public const int NoteDeCredit = 8;
    public const int PieceJointeMail = 9;
    public const int Execution = 10;

    public static bool IsValid(double value) => value is >= Contrat and <= Execution && JsNumber.IsInteger(value);
}
