namespace Horizon.Shared.Models;

// documents liés à une fiche
public sealed record DocumentRow(string IDLIEN_DOCUMENTS, string? Nom, string? NomFichier, string? Extension, string? Commentaire, string? Date_heure_creation);

public sealed record DocumentsResponse(List<DocumentRow> documents);
