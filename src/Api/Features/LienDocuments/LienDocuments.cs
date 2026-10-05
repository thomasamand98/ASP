using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Api.Common;
using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Features.LienDocuments;

public record FichierDocument(byte[] Contenu, string NomFichier);

// ===== SERVICE =====
// Documents joints à une fiche (onglets « Documents » des fiches Société,
// Personnel, Véhicule — voir frontend/src/features/liens-documents/).
//
// Deux tables : `documents` décrit le fichier, `lien_documents` le rattache
// à un enregistrement — TYPE (voir DocumentCategorie) dit de quelle table
// vient IDReferent. Un même document peut avoir plusieurs liens.
//
// Le contenu du fichier est enregistré sur disque, pas en base
// (Document_physique reste vide) : documents.Emplacement garde son chemin,
// relatif au dossier de stockage (PDF_STORAGE_DIR).
public partial class LienDocumentService(AppDbContext db, IOptions<AppOptions> options)
{
    private const string SousRepertoireDocuments = "documents";

    // Tailles des colonnes VARCHAR.
    private const int MaxNom = 100;
    private const int MaxExtension = 50;

    private string RacineStockage => options.Value.StorageRoot;

    // Chemin absolu d'un Emplacement, ou null s'il sort du dossier de stockage
    // (chemin absolu d'un poste WinDev, « .. ») : rien n'est lu ni supprimé
    // en dehors de ce dossier.
    private string? CheminFichier(string? emplacement)
    {
        if (string.IsNullOrEmpty(emplacement)) return null;
        var racine = RacineStockage;
        var chemin = Path.GetFullPath(Path.Combine(racine, emplacement));
        return chemin.StartsWith(racine.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? chemin : null;
    }

    public async Task<object> GetLiensDocumentsAsync(int type, long referentId, CancellationToken ct)
    {
        var liens = await db.LienDocument.Where(l => l.TYPE == type && l.IDReferent == referentId)
            .OrderByDescending(l => l.Date_heure_creation)
            .Select(l => new { l.IDLIEN_DOCUMENTS, l.IDDOCUMENTS, l.Nom_lien, l.Date_heure_creation })
            .ToListAsync(ct);
        // Pas de relation entre les deux tables : jointure faite ici.
        var ids = liens.Where(l => l.IDDOCUMENTS is not null).Select(l => l.IDDOCUMENTS!).ToList();
        var documents = ids.Count == 0
            ? []
            : await db.Document.Where(d => ids.Contains(d.IDDOCUMENTS))
                .Select(d => new { d.IDDOCUMENTS, d.Nom, d.NomFichier, d.Extension_Document, d.Commentaire })
                .ToListAsync(ct);
        var parId = documents.ToDictionary(d => Convert.ToHexString(d.IDDOCUMENTS));

        return new
        {
            documents = liens.Select(lien =>
            {
                var document = lien.IDDOCUMENTS is null ? null : parId.GetValueOrDefault(Convert.ToHexString(lien.IDDOCUMENTS));
                return new
                {
                    lien.IDLIEN_DOCUMENTS,
                    Nom = lien.Nom_lien ?? document?.Nom,
                    document?.NomFichier,
                    Extension = document?.Extension_Document,
                    document?.Commentaire,
                    lien.Date_heure_creation,
                };
            }),
        };
    }

    // Enregistre le fichier sur disque, puis crée le document et son lien. Le
    // fichier est nommé d'après l'identifiant du document (pas d'après son nom
    // d'origine) : deux fichiers de même nom ne s'écrasent pas.
    public async Task<object> CreateLienDocumentAsync(int type, long referentId, byte[] contenu, string originalName, CancellationToken ct)
    {
        var nomFichier = originalName.Length > MaxNom ? originalName[^MaxNom..] : originalName;
        // L'extension finit dans un nom de fichier sur disque : lettres et
        // chiffres seulement, sinon aucune.
        var extensionBrute = ExtName(nomFichier).TrimStart('.').ToLowerInvariant();
        var extension = SafeExtension().IsMatch(extensionBrute) ? extensionBrute[..Math.Min(extensionBrute.Length, MaxExtension)] : "";
        var id = RandomNumberGenerator.GetBytes(32);
        var emplacement = $"{SousRepertoireDocuments}/{Convert.ToHexString(id).ToLowerInvariant()}{(extension.Length > 0 ? $".{extension}" : "")}";
        var chemin = Path.GetFullPath(Path.Combine(RacineStockage, emplacement));
        Directory.CreateDirectory(Path.GetDirectoryName(chemin)!);
        await File.WriteAllBytesAsync(chemin, contenu, ct);

        var maintenant = DateTime.UtcNow;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // IDUTILISATEURS_createur/modificateur ont un défaut DB de 0, qui viole
            // leur contrainte de clé étrangère : mis à null explicitement.
            db.Document.Add(new Document
            {
                Date_heure_creation = maintenant,
                Date_heure_modification = maintenant,
                IDUTILISATEURS_createur = null,
                IDUTILISATEURS_modificateur = null,
                IDDOCUMENTS = id,
                Nom = nomFichier,
                NomFichier = nomFichier,
                Extension_Document = extension,
                Categorie = type,
                Emplacement = emplacement,
                Version = 1,
            });
            var lien = new LienDocument
            {
                Date_heure_creation = maintenant,
                Date_heure_modification = maintenant,
                IDUTILISATEURS_createur = null,
                IDUTILISATEURS_modificateur = null,
                TYPE = type,
                IDReferent = referentId,
                IDDOCUMENTS = id,
                Nom_lien = nomFichier,
            };
            db.LienDocument.Add(lien);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new { lien.IDLIEN_DOCUMENTS };
        }
        catch
        {
            File.Delete(chemin);
            throw;
        }
    }

    public async Task<FichierDocument> GetFichierAsync(long id, CancellationToken ct)
    {
        var lien = await db.LienDocument.Where(l => l.IDLIEN_DOCUMENTS == id).Select(l => new { l.IDDOCUMENTS }).FirstOrDefaultAsync(ct);
        var document = lien?.IDDOCUMENTS is null
            ? null
            : await db.Document.Where(d => d.IDDOCUMENTS == lien.IDDOCUMENTS).Select(d => new { d.Nom, d.NomFichier, d.Emplacement }).FirstOrDefaultAsync(ct);
        var chemin = CheminFichier(document?.Emplacement);
        if (document is null || chemin is null) throw new NotFoundException("Document introuvable.");
        try
        {
            var nom = !string.IsNullOrEmpty(document.NomFichier) ? document.NomFichier : !string.IsNullOrEmpty(document.Nom) ? document.Nom : "document";
            return new FichierDocument(await File.ReadAllBytesAsync(chemin, ct), nom);
        }
        catch (IOException)
        {
            throw new NotFoundException("Le fichier de ce document est introuvable sur le serveur.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new NotFoundException("Le fichier de ce document est introuvable sur le serveur.");
        }
    }

    // Supprime le lien ; le document et son fichier ne sont supprimés que s'il
    // n'est plus rattaché à aucune autre fiche.
    public async Task DeleteLienDocumentAsync(long id, CancellationToken ct)
    {
        var lien = await db.LienDocument.Where(l => l.IDLIEN_DOCUMENTS == id).Select(l => new { l.IDDOCUMENTS }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Document introuvable.");
        var idDocument = lien.IDDOCUMENTS;

        string? emplacement = null;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await db.LienDocument.Where(l => l.IDLIEN_DOCUMENTS == id).DeleteOrThrowAsync(ct);
            if (idDocument is not null && !await db.LienDocument.AnyAsync(l => l.IDDOCUMENTS == idDocument, ct))
            {
                var document = await db.Document.Where(d => d.IDDOCUMENTS == idDocument).Select(d => new { d.Emplacement }).FirstOrDefaultAsync(ct);
                if (document is not null)
                {
                    await db.Document.Where(d => d.IDDOCUMENTS == idDocument).DeleteOrThrowAsync(ct);
                    emplacement = document.Emplacement;
                }
            }
            await tx.CommitAsync(ct);
        }

        var chemin = CheminFichier(emplacement);
        if (chemin is not null) File.Delete(chemin);
    }

    // path.extname de Node : extension du dernier segment, point compris ;
    // vide pour un nom sans point ou commençant par un point.
    public static string ExtName(string path)
    {
        var name = path[(path.LastIndexOfAny(['/', '\\']) + 1)..];
        var dot = name.LastIndexOf('.');
        return dot <= 0 ? "" : name[dot..];
    }

    [GeneratedRegex("^[a-z0-9]+$")]
    private static partial Regex SafeExtension();
}

// ===== CONTROLLER =====
public class LienDocumentController(LienDocumentService service) : ApiControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    private static int ParseType(string? raw)
    {
        var type = JsNumber.Parse(raw);
        if (string.IsNullOrEmpty(raw) || !DocumentCategorie.IsValid(type)) throw new BadRequestException("Catégorie de document invalide.");
        return (int)type;
    }

    // NestJS : GET lien-documents
    // ?type=6&referentId=X (obligatoires) : documents liés à cet
    // enregistrement — voir DocumentCategorie pour les types.
    [HttpGet("lien-documents")]
    public Task<object> GetLiensDocuments([FromQuery] string? type, [FromQuery] string? referentId, CancellationToken ct)
    {
        var referent = Params.ParseId(referentId);
        return service.GetLiensDocumentsAsync(ParseType(type), referent, ct);
    }

    // NestJS : POST lien-documents (multipart, champ « file »)
    // Champs texte « type » et « referentId » envoyés avec le fichier. Limite
    // de 50 Mo ; au-delà, 413.
    [HttpPost("lien-documents")]
    public async Task<IActionResult> CreateLienDocument(CancellationToken ct)
    {
        var form = await Uploads.ReadAsync(Request, ct);
        var referent = Params.ParseId(form.Field("referentId"));
        if (form.File is null) throw new BadRequestException("Fichier requis (champ \"file\")");
        var type = ParseType(form.Field("type"));
        return StatusCode(StatusCodes.Status201Created, await service.CreateLienDocumentAsync(type, referent, form.File.Buffer, form.File.OriginalName, ct));
    }

    // NestJS : GET lien-documents/:id/fichier
    // Envoie le contenu du fichier tel quel. `attachment` + `nosniff` : un
    // fichier déposé (HTML, SVG...) n'est jamais interprété par le navigateur
    // s'il est ouvert directement depuis l'API.
    [HttpGet("lien-documents/{id}/fichier")]
    public async Task<IActionResult> GetFichier(string id, CancellationToken ct)
    {
        var fichier = await service.GetFichierAsync(Params.ParseId(id), ct);
        Response.Headers.ContentDisposition = $"attachment; filename*=UTF-8''{EncodeUriComponent(fichier.NomFichier)}";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(fichier.Contenu, ContentTypeOf(fichier.NomFichier));
    }

    // NestJS : DELETE lien-documents/:id
    [HttpDelete("lien-documents/{id}")]
    public Task DeleteLienDocument(string id, CancellationToken ct) => service.DeleteLienDocumentAsync(Params.ParseId(id), ct);

    // Type MIME d'après l'extension, comme res.type() d'Express (jeu de
    // caractères précisé pour les types texte).
    private static string ContentTypeOf(string nomFichier)
    {
        var extension = LienDocumentService.ExtName(nomFichier);
        if (extension.Length == 0 || !ContentTypes.TryGetContentType(extension, out var type)) return "application/octet-stream";
        return type.StartsWith("text/", StringComparison.Ordinal) || type is "application/json" or "application/javascript" ? $"{type}; charset=utf-8" : type;
    }

    // encodeURIComponent de JavaScript.
    private static string EncodeUriComponent(string value) =>
        Uri.EscapeDataString(value).Replace("%21", "!").Replace("%27", "'").Replace("%28", "(").Replace("%29", ")").Replace("%2A", "*");
}
