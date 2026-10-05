namespace Horizon.Web.Components.Fiche;

// Onglet d'une fiche. Count : pastille après le libellé (ex. prestations
// d'un contrat) ; Title : info-bulle (ex. raison de la désactivation).
public sealed record FicheTab(string Id, string Label, int? Count = null, bool Disabled = false, string? Title = null);
