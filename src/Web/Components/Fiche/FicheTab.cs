namespace Horizon.Web.Components.Fiche;

// Count : pastille après le libellé, Title : info-bulle
public sealed record FicheTab(string Id, string Label, int? Count = null, bool Disabled = false, string? Title = null);
