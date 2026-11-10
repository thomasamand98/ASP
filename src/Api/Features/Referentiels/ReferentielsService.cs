using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Referentiels;

// listes en lecture seule pour les sélecteurs
public class ReferentielsService(AppDbContext db)
{
    // courte liste, affinée à la saisie
    private const int DechetSearchLimit = 30;

    // ~240 lignes, tout chargé d'un coup
    public async Task<object> GetPaysAsync(CancellationToken ct)
    {
        var pays = await db.Pays.OrderBy(p => p.Nom).Select(p => new { p.IDPAYS, p.ISO, p.Nom }).ToListAsync(ct);
        return new { pays };
    }

    // ~39 000 villes, recherche par CP exact
    public async Task<object> SearchVillesAsync(string cp, CancellationToken ct)
    {
        var villes = await db.Ville.Where(v => v.CP == cp).Take(20).Select(v => new { v.IDVILLES, v.CP, v.Nom_ville }).ToListAsync(ct);
        return new { villes };
    }

    public async Task<object> GetTypesFactureAsync(CancellationToken ct)
    {
        var typesFacture = await db.TypeFacture.OrderBy(t => t.IDTYPES_FACTURE).Select(t => new { t.IDTYPES_FACTURE, t.Nom }).ToListAsync(ct);
        return new { typesFacture };
    }

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

    // onglet Messages envoyés de la fiche société
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
