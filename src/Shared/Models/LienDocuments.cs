namespace Horizon.Shared.Models;

// Réponses de l'API — documents liés à une fiche (GET /lien-documents).
public sealed record DocumentRow(string IDLIEN_DOCUMENTS, string? Nom, string? NomFichier, string? Extension, string? Commentaire, string? Date_heure_creation);

public sealed record DocumentsResponse(List<DocumentRow> documents);
