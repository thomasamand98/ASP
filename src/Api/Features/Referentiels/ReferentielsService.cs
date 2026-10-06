using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Referentiels;

// Listes de référence en lecture seule, utilisées pour peupler les
// sélecteurs des formulaires. Regroupe les services NestJS PaysService,
// VilleService, TypeFactureService, DechetService, ClientService,
// FournisseurService et MessageService.
public class ReferentielsService(AppDbContext db)
{
    // Nombre de déchets renvoyés par recherche : le sélecteur en affiche une
    // courte liste, affinée par la saisie.
    private const int DechetSearchLimit = 30;

    // ~240 lignes seulement — chargées en une fois, contrairement aux villes —
    // alimente le menu déroulant Pays de EntiteForm.tsx.
    public async Task<object> GetPaysAsync(CancellationToken ct)
    {
        var pays = await db.Pays.OrderBy(p => p.Nom).Select(p => new { p.IDPAYS, p.ISO, p.Nom }).ToListAsync(ct);
        return new { pays };
    }

    // Recherche par code postal exact — la table compte ~39 000 lignes,
    // impossible à charger en une fois côté frontend.
    public async Task<object> SearchVillesAsync(string cp, CancellationToken ct)
    {
        var villes = await db.Ville.Where(v => v.CP == cp).Take(20).Select(v => new { v.IDVILLES, v.CP, v.Nom_ville }).ToListAsync(ct);
        return new { villes };
    }

    // Types de facture, proposés dans le sélecteur « Type facture » de la
    // fiche contrat.
    public async Task<object> GetTypesFactureAsync(CancellationToken ct)
    {
        var typesFacture = await db.TypeFacture.OrderBy(t => t.IDTYPES_FACTURE).Select(t => new { t.IDTYPES_FACTURE, t.Nom }).ToListAsync(ct);
        return new { typesFacture };
    }

    // Déchets, proposés dans le sélecteur « Déchets » de la fiche marchandise.
    public async Task<object> GetDechetsAsync(string? search, CancellationToken ct)
    {
        var dechets = await db.Dechet
            .WhereSearch(search, p => d => EF.Functions.Like(d.Code, p) || EF.Functions.Like(d.Description_dechet, p))
            .OrderBy(d => d.Code)
            .Take(DechetSearchLimit)
            .Select(d => new { d.IDDECHETS, d.Code, d.Description_dechet })
            .ToListAsync(ct);
        return new { dechets };
    }

    // Alimente l'onglet « Messages envoyés » de la fiche Société
    // (SocieteForm.tsx) — lecture seule. search : champ Rechercher de l'onglet.
    public async Task<object> GetMessagesBySocieteAsync(long societeId, string? search, CancellationToken ct)
    {
        var messages = await db.Message
            .Where(m => m.IDSOCIETES == societeId)
            .WhereSearch(search, p => m =>
                EF.Functions.Like(m.Sujet, p) || EF.Functions.Like(m.Destinataire, p) || EF.Functions.Like(m.Type_message, p) || EF.Functions.Like(m.NomContact, p))
            .OrderByDescending(m => m.Date_heure_creation)
            .Select(m => new { m.IDMESSAGES, m.Sujet, m.Destinataire, m.Type_message, m.Date_message, m.Heure_message, m.NomContact })
            .ToListAsync(ct);
        return new { messages };
    }
}
