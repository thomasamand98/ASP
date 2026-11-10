using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Api.Common;
using Api.Configuration;
using Api.Data;
using Api.Features.Contrats;
using Api.Features.DocumentMerge;
using Api.Features.DocumentTemplates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Features.Performance;

// dureeMs/octets cumulés sur tous les exemplaires
public class ImpressionContrat
{
    public required string id { get; init; }
    public string? numero { get; init; }
    public int exemplaires { get; set; }
    public double dureeMs { get; set; }
    public double octets { get; set; }
}

public record EchecImpression(string id, string? numero, string message);

public record AvancementImpression(int faits, int total);

// Corps envoyé tel quel, déjà compressé ou non
public record Transfert(byte[] Corps, int OctetsJson, double GenerationMs, double JsonMs, double CompressionMs);

// singleton, lu par le ping, une seule impression à la fois
public class ImpressionState
{
    private AvancementImpression? _avancement;

    public AvancementImpression? Avancement => Volatile.Read(ref _avancement);

    // false si déjà en cours
    public bool TryStart() => Interlocked.CompareExchange(ref _avancement, new AvancementImpression(0, 0), null) is null;

    public void Set(int faits, int total) => Volatile.Write(ref _avancement, new AvancementImpression(faits, total));

    public void Clear() => Volatile.Write(ref _avancement, null);
}

// tests de charge : "attente" async, "cpu" bloque un thread (pas tout le serveur)
public partial class PerformanceService(
    AppDbContext db,
    ContratService contratService,
    DocumentTemplateService documentTemplateService,
    DocumentMergeService documentMergeService,
    ImpressionState impression,
    IOptions<AppOptions> options,
    ILogger<PerformanceService> logger)
{
    // sous PDF_STORAGE_DIR, vidé à chaque impression, contrat-<id>-ex2.pdf etc. pour les exemplaires
    private const string SousRepertoireContrats = "contrats";

    public AvancementImpression? GetAvancementImpression() => impression.Avancement;

    public async Task<object> LancerTraitementLongAsync(string mode, int dureeMs, CancellationToken ct)
    {
        var debut = DateTime.UtcNow;
        long? iterations = null;

        if (mode == "attente") await Task.Delay(dureeMs, ct);
        else iterations = CalculerPendant(dureeMs);

        var fin = DateTime.UtcNow;
        var resultat = new Dictionary<string, object?>
        {
            ["mode"] = mode,
            ["dureeDemandeeMs"] = dureeMs,
            ["dureeReelleMs"] = Math.Truncate((fin - debut).TotalMilliseconds),
            ["debut"] = debut,
            ["fin"] = fin,
        };
        if (iterations is not null) resultat["iterations"] = (double)iterations.Value;
        return resultat;
    }

    // génération chronométrée à part pour la retirer du temps vu par le navigateur
    public async Task<Transfert> PreparerTransfertAsync(int tailleKo, bool compression, CancellationToken ct)
    {
        var depart = Stopwatch.GetTimestamp();
        var lignes = GenererLignes(tailleKo * 1024);
        var generationMs = Stopwatch.GetElapsedTime(depart).TotalMilliseconds;

        depart = Stopwatch.GetTimestamp();
        var json = Encoding.UTF8.GetBytes($"{{\"lignes\":[{string.Join(',', lignes)}]}}");
        var jsonMs = Stopwatch.GetElapsedTime(depart).TotalMilliseconds;

        depart = Stopwatch.GetTimestamp();
        var corps = json;
        if (compression)
        {
            using var buffer = new MemoryStream();
            await using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            {
                await gzip.WriteAsync(json, ct);
            }
            corps = buffer.ToArray();
        }
        var compressionMs = compression ? Stopwatch.GetElapsedTime(depart).TotalMilliseconds : 0;

        return new Transfert(corps, json.Length, generationMs, jsonMs, compressionMs);
    }

    // séquentiel pour ne pas fausser les durées, chaque exemplaire refait tout (base, fusion, rendu)
    public async Task<object> ImprimerTousLesContratsAsync(long templateId, int exemplaires, CancellationToken ct)
    {
        if (!impression.TryStart()) throw new ConflictException("Une impression de tous les contrats est déjà en cours.");
        try
        {
            // 404 direct plutôt qu'un échec par contrat
            await documentTemplateService.GetDocumentTemplateContentAsync(templateId, ct);

            var contrats = await db.Contrat.OrderBy(c => c.IDCONTRATS).Select(c => new { c.IDCONTRATS, c.Num_contrat }).ToListAsync(ct);
            var aImprimer = contrats.Count * exemplaires;
            impression.Set(0, aImprimer);
            var debut = DateTime.UtcNow;
            var imprimes = new List<ImpressionContrat>();
            var echecs = new List<EchecImpression>();
            var durees = new List<double>();
            var faits = 0;

            var dossier = Path.Combine(options.Value.StorageRoot, SousRepertoireContrats);
            Directory.CreateDirectory(dossier);
            // sinon il resterait des exemplaires du lancement précédent
            foreach (var fichier in Directory.EnumerateFiles(dossier).Where(f => FichierContrat().IsMatch(Path.GetFileName(f))).ToList())
            {
                File.Delete(fichier);
            }

            foreach (var contrat in contrats)
            {
                var id = contrat.IDCONTRATS.ToString(CultureInfo.InvariantCulture);
                var imprime = new ImpressionContrat { id = id, numero = contrat.Num_contrat };
                try
                {
                    for (var exemplaire = 1; exemplaire <= exemplaires; exemplaire++)
                    {
                        var depart = Stopwatch.GetTimestamp();
                        var context = await contratService.GetContratMergeContextAsync(contrat.IDCONTRATS, ct);
                        var pdf = await documentMergeService.RenderPdfAsync(templateId, context, ct);
                        await File.WriteAllBytesAsync(Path.Combine(dossier, $"contrat-{id}{(exemplaire > 1 ? $"-ex{exemplaire}" : "")}.pdf"), pdf, ct);
                        var dureeMs = Math.Round(Stopwatch.GetElapsedTime(depart).TotalMilliseconds, MidpointRounding.AwayFromZero);
                        durees.Add(dureeMs);
                        imprime.exemplaires++;
                        imprime.dureeMs += dureeMs;
                        imprime.octets += pdf.Length;
                        impression.Set(++faits, aImprimer);
                    }
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    // exemplaires restants abandonnés mais comptés dans l'avancement
                    logger.LogWarning("Impression du contrat {Id} impossible : {Message}", id, Newlines().Replace(error.Message, " | "));
                    echecs.Add(new EchecImpression(id, contrat.Num_contrat, error.Message));
                    faits += exemplaires - imprime.exemplaires;
                    impression.Set(faits, aImprimer);
                }
                if (imprime.exemplaires > 0) imprimes.Add(imprime);
            }

            var fin = DateTime.UtcNow;
            return new
            {
                dossier,
                total = contrats.Count,
                exemplaires,
                // PDF réellement enregistrés
                impressions = durees.Count,
                dureeTotaleMs = Math.Truncate((fin - debut).TotalMilliseconds),
                // succès seulement, null si aucun
                dureeMoyenneMs = durees.Count > 0 ? Math.Round(durees.Sum() / durees.Count, MidpointRounding.AwayFromZero) : (double?)null,
                dureeMinMs = durees.Count > 0 ? durees.Min() : (double?)null,
                dureeMaxMs = durees.Count > 0 ? durees.Max() : (double?)null,
                octetsTotal = imprimes.Sum(c => c.octets),
                debut,
                fin,
                contrats = imprimes,
                echecs,
            };
        }
        finally
        {
            impression.Clear();
        }
    }

    private static readonly string[] Noms = ["Transports Dubois", "Logistique Lambert", "Martin Frères", "Euro Cargo", "Benelux Express", "Ardenne Fret", "Meuse Transit", "Nord Vrac"];
    private static readonly string[] Formes = ["SA", "SRL", "SPRL", "NV", "BV"];
    private static readonly string[] Villes = ["Liège", "Namur", "Bruxelles", "Charleroi", "Anvers", "Gand", "Mons", "Arlon", "Luxembourg", "Lille"];
    private static readonly string[] Statuts = ["En cours", "Planifié", "Terminé", "Facturé", "Annulé"];

    // graine fixe, en double exprès (produit > 2^53 arrondi, en long on aurait d'autres données)
    public static List<string> GenererLignes(int octetsVoulus)
    {
        double graine = 42;
        int Hasard(int max)
        {
            graine = (graine * 1_103_515_245d + 12_345d) % 2_147_483_648d;
            return (int)(graine % max);
        }

        var lignes = new List<string>();
        // {"lignes":[]} = 12, puis +1 par virgule
        long octets = 12;
        while (octets < octetsVoulus)
        {
            var societe = $"{Noms[Hasard(Noms.Length)]} {Formes[Hasard(Formes.Length)]}";
            var ville = Villes[Hasard(Villes.Length)];
            var statut = Statuts[Hasard(Statuts.Length)];
            var numero = $"C-{2020 + Hasard(7)}-{Hasard(100_000).ToString("00000", CultureInfo.InvariantCulture)}";
            var montant = Hasard(1_000_000) / 100d;
            var (annee, mois, jour) = (2020 + Hasard(7), Hasard(12), 1 + Hasard(28));
            var date = new DateTime(annee, mois + 1, jour, 0, 0, 0, DateTimeKind.Utc);
            var archive = Hasard(10) == 0;
            var ligne = $"{{\"id\":{lignes.Count + 1},\"societe\":\"{societe}\",\"ville\":\"{ville}\",\"statut\":\"{statut}\",\"numero\":\"{numero}\","
                + $"\"montant\":{JsNumber.ToString(montant)},\"date\":\"{JsonConfig.FormatDate(date)}\",\"archive\":{(archive ? "true" : "false")}}}";
            lignes.Add(ligne);
            octets += Encoding.UTF8.GetByteCount(ligne) + 1;
        }
        return lignes;
    }

    // synchrone exprès
    private static long CalculerPendant(int dureeMs)
    {
        var finPrevue = Stopwatch.GetTimestamp() + (long)(dureeMs / 1000d * Stopwatch.Frequency);
        long iterations = 0;
        double accumulateur = 0;
        while (Stopwatch.GetTimestamp() < finPrevue)
        {
            for (var i = 0; i < 10_000; i++) accumulateur += Math.Sqrt(i) * Math.Sin(i);
            iterations++;
        }
        // sinon l'optimiseur vire la boucle
        return double.IsFinite(accumulateur) ? iterations : 0;
    }

    [GeneratedRegex(@"^contrat-\d+(-ex\d+)?\.pdf$")]
    private static partial Regex FichierContrat();

    [GeneratedRegex(@"\s*\n\s*")]
    private static partial Regex Newlines();
}

[Route("performance")]
public class PerformanceController(PerformanceService service) : ApiControllerBase
{
    private const int DureeParDefautS = 60;
    private const int DureeMaxS = 300;
    private const int ExemplairesMax = 20;
    // 50 Mo
    private const int TailleMaxKo = 51_200;

    // duree en secondes, 60 par défaut, max 300
    [HttpPost("traitement-long")]
    public async Task<IActionResult> LancerTraitementLong([FromQuery] string? mode, [FromQuery] string? duree, CancellationToken ct)
    {
        var modeTraitement = mode is null ? "attente" : ParseMode(mode);
        var dureeS = duree is null ? DureeParDefautS : JsNumber.Parse(duree);
        if (!JsNumber.IsInteger(dureeS) || dureeS < 1 || dureeS > DureeMaxS)
        {
            throw new BadRequestException($"Paramètre « duree » invalide (entier de 1 à {DureeMaxS} secondes attendu).");
        }
        return StatusCode(StatusCodes.Status201Created, await service.LancerTraitementLongAsync(modeTraitement, (int)dureeS * 1000, ct));
    }

    // ne répond qu'une fois tout imprimé
    [HttpPost("impression-contrats")]
    public async Task<IActionResult> ImprimerTousLesContrats([FromQuery] string? templateId, [FromQuery] string? exemplaires, CancellationToken ct)
    {
        var template = Params.ParseId(templateId);
        var nombre = exemplaires is null ? 1 : JsNumber.Parse(exemplaires);
        if (!JsNumber.IsInteger(nombre) || nombre < 1 || nombre > ExemplairesMax)
        {
            throw new BadRequestException($"Paramètre « exemplaires » invalide (entier de 1 à {ExemplairesMax} attendu).");
        }
        return StatusCode(StatusCodes.Status201Created, await service.ImprimerTousLesContratsAsync(template, (int)nombre, ct));
    }

    // mesures serveur dans les en-têtes X-Perf-* (ms, octets)
    [HttpGet("transfert")]
    public async Task Transfert([FromQuery] string? taille, [FromQuery] string? compression, CancellationToken ct)
    {
        var tailleKo = taille is null ? double.NaN : JsNumber.Parse(taille);
        if (!JsNumber.IsInteger(tailleKo) || tailleKo < 1 || tailleKo > TailleMaxKo)
        {
            throw new BadRequestException($"Paramètre « taille » invalide (entier de 1 à {TailleMaxKo} Ko attendu).");
        }
        var compresse = compression == "1";
        var transfert = await service.PreparerTransfertAsync((int)tailleKo, compresse, ct);

        var entetes = new Dictionary<string, string>
        {
            ["Cache-Control"] = "no-store",
            ["X-Perf-Generation-Ms"] = transfert.GenerationMs.ToString("0.00", CultureInfo.InvariantCulture),
            ["X-Perf-Json-Ms"] = transfert.JsonMs.ToString("0.00", CultureInfo.InvariantCulture),
            ["X-Perf-Compression-Ms"] = transfert.CompressionMs.ToString("0.00", CultureInfo.InvariantCulture),
            ["X-Perf-Octets-Json"] = transfert.OctetsJson.ToString(CultureInfo.InvariantCulture),
            ["X-Perf-Octets-Envoyes"] = transfert.Corps.Length.ToString(CultureInfo.InvariantCulture),
        };
        if (compresse) entetes["Content-Encoding"] = "gzip";
        entetes["Access-Control-Expose-Headers"] = string.Join(", ", entetes.Keys.Where(nom => nom.StartsWith("X-Perf-", StringComparison.Ordinal)));
        foreach (var (nom, valeur) in entetes) Response.Headers[nom] = valeur;
        Response.ContentType = "application/json; charset=utf-8";
        Response.ContentLength = transfert.Corps.Length;
        await Response.Body.WriteAsync(transfert.Corps, ct);
    }

    // pour voir si le serveur répond encore pendant un traitement long
    [HttpGet("ping")]
    public object Ping() => new { heure = DateTime.UtcNow, impression = service.GetAvancementImpression() };

    private static string ParseMode(string mode) =>
        mode is "attente" or "cpu" ? mode : throw new BadRequestException("Paramètre « mode » invalide (« attente » ou « cpu » attendu).");
}
