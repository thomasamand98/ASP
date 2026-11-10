namespace Horizon.Shared;

// DOCUMENT_CATEGORIE_* WinDev = lien_documents.TYPE, dit la table de IDReferent (6 société, 3 personnel...)
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
