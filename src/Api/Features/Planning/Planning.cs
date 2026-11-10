using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Planning;

// dates en ISO, ids en string
public partial class PlanningService(AppDbContext db)
{
    // limite du front + un peu de marge
    private const int MaxRangeDays = 62;
    private const int CommandesLimit = 500;
    private const int DechargementsLimit = 200;
    // si ni l'exécution ni la prestation routière n'ont de durée
    private const int DefaultDurationMinutes = 120;
    // marchandise sans CouleurPlanning
    private static readonly string[] FallbackColors = ["#8b5cf6", "#10b981", "#f59e0b", "#0ea5e9", "#ef4444", "#64748b", "#a8a26a", "#ec4899"];

    private sealed class PrestationRow
    {
        public long IDPRESTATIONS { get; init; }
        public decimal? Prix_unitaire { get; init; }
        public bool HasMarchandise { get; init; }
        public long IDMARCHANDISES { get; init; }
        public string? Nom_marchandise { get; init; }
        public long? CouleurPlanning { get; init; }
        // chargée à part par LoadRoutiersAsync
        public RoutierRow? Routier { get; set; }
    }

    private sealed record RoutierRow(long IDPRESTATIONS, long? IDPOINTDEPART, long? IDPOINTARRIVEE, TimeSpan? Duree);

    private sealed class ExecutionRow
    {
        public long IDEXECUTIONS { get; init; }
        public long? IDCOMMANDES { get; init; }
        public long? IDCHAUFFEUR { get; init; }
        public long? IDTRACTEUR { get; init; }
        public long? IDREMORQUE { get; init; }
        public string? Immat_tracteur { get; init; }
        public string? Immat_remorque { get; init; }
        public DateTime? Date_Execution { get; init; }
        public DateTime? Date_Fin_Execution { get; init; }
        public DateTime? Date_execution_liee { get; init; }
        public TimeSpan? Duree { get; init; }
        public string? Reference_client { get; init; }
        public string? RefPlanning { get; init; }
        public string? Instruction_planning { get; init; }
        public string? Statut { get; init; }
        public int? Execution_dechargement { get; init; }
        public long? IDEXECUTIONS_LIEE { get; init; }
        public string? ContratSociete { get; init; }
        public bool HasCommande { get; init; }
        public string? CommandeNumRef { get; init; }
        public string? CommandeSociete { get; init; }
        public PrestationRow? Prestation { get; init; }
    }

    private sealed class CommandeRow
    {
        public long IDCOMMANDES { get; init; }
        public DateTime? Date_commande { get; init; }
        public string? NumRef { get; init; }
        public int? QT { get; init; }
        public int? QT_planifie { get; init; }
        public string? Instruction { get; init; }
        public string? ContratSociete { get; init; }
        public PrestationRow? Prestation { get; init; }
    }

    // id vers libellé, colonnes sans FK donc pas de navigation possible
    private sealed record Lookups(Dictionary<long, string> Points, Dictionary<long, string> Immats);

    public async Task<object> GetPlanningAsync(string? fromRaw, string? toRaw, string? jourDebutRaw, string? jourFinRaw, CancellationToken ct)
    {
        var from = ParseDate(fromRaw, "from");
        var to = ParseDate(toRaw, "to");
        if (to <= from) throw new BadRequestException("La fin de la période doit être après son début.");
        if ((to - from).TotalMilliseconds > MaxRangeDays * 86_400_000d) throw new BadRequestException($"La période ne peut pas dépasser {MaxRangeDays} jours.");
        // colonne DATE, minuit UTC
        var jourDebut = ParseDate($"{jourDebutRaw ?? "undefined"}T00:00:00Z", "jourDebut");
        var jourFin = ParseDate($"{jourFinRaw ?? "undefined"}T00:00:00Z", "jourFin");

        // chevauchement, sans date de fin : début dans la période
        var executions = await SelectExecutions(db.Execution
                .Where(e => e.Date_Execution < to && (e.Date_Fin_Execution > from || (e.Date_Fin_Execution == null && e.Date_Execution >= from)))
                .OrderBy(e => e.Date_Execution))
            .ToListAsync(ct);
        // déchargements différés pas encore placés sur le planning
        var dechargements = await SelectExecutions(db.Execution
                .Where(e => e.Execution_dechargement == 1 && e.Date_Execution == null)
                .OrderBy(e => e.IDEXECUTIONS)
                .Take(DechargementsLimit))
            .ToListAsync(ct);
        var commandes = await db.Commande
            .Where(c => c.Date_commande >= jourDebut && c.Date_commande <= jourFin && c.QT > 0)
            .OrderBy(c => c.Date_commande).ThenBy(c => c.IDCOMMANDES)
            .Take(CommandesLimit)
            .Select(c => new CommandeRow
            {
                IDCOMMANDES = c.IDCOMMANDES,
                Date_commande = c.Date_commande,
                NumRef = c.NumRef,
                QT = c.QT,
                QT_planifie = c.QT_planifie,
                Instruction = c.Instruction,
                ContratSociete = c.Contrat!.Societe!.Nom_societe,
                Prestation = c.Prestation == null ? null : new PrestationRow
                {
                    IDPRESTATIONS = c.Prestation.IDPRESTATIONS,
                    Prix_unitaire = c.Prestation.Prix_unitaire,
                    HasMarchandise = c.Prestation.Marchandise != null,
                    IDMARCHANDISES = c.Prestation.Marchandise!.IDMARCHANDISES,
                    Nom_marchandise = c.Prestation.Marchandise!.Nom_marchandise,
                    CouleurPlanning = c.Prestation.Marchandise!.CouleurPlanning,
                },
            })
            .ToListAsync(ct);
        var chauffeurs = await db.Chauffeur
            .Where(c => c.Archive == 0 || c.Archive == null)
            .OrderBy(c => c.Nom_chauffeur)
            .Select(c => new
            {
                c.IDCHAUFFEURS,
                c.IDPERSONNELS,
                c.Nom_chauffeur,
                HasPersonnel = c.Personnel != null,
                c.Personnel!.Nom_Personnel,
                c.Personnel!.Prenom_Personnel,
                Societe = c.Societe!.Nom_societe,
            })
            .ToListAsync(ct);
        // attelage de référence le plus récent par chauffeur
        var chauffeurIds = chauffeurs.Select(c => (long?)c.IDCHAUFFEURS).ToList();
        var references = (await db.AttelageReference.Where(a => chauffeurIds.Contains(a.IDCHAUFFEUR))
                .Select(a => new { a.IDATTELAGE_REFERENCE, a.IDCHAUFFEUR, a.IDTRACTEUR, a.IDREMORQUE })
                .ToListAsync(ct))
            .GroupBy(a => a.IDCHAUFFEUR!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.IDATTELAGE_REFERENCE).First());
        // Vehicule.Type pas documenté, on prend ceux déjà attelés en remorque
        var remorquesAttelees = await db.Vehicule.Where(v => v.AttelagesRemorque.Any()).Select(v => v.IDVEHICULES).ToListAsync(ct);

        await LoadRoutiersAsync(executions.Select(e => e.Prestation).Concat(dechargements.Select(e => e.Prestation)).Concat(commandes.Select(c => c.Prestation)), ct);

        var remorqueIds = remorquesAttelees.Cast<long?>()
            .Concat(references.Values.Select(r => r.IDREMORQUE))
            .Concat(executions.Select(e => e.IDREMORQUE))
            .Concat(dechargements.Select(e => e.IDREMORQUE))
            .Where(IsId).Select(id => id!.Value).Distinct().ToList();
        var lookups = await LoadLookupsAsync(
            executions.Concat(dechargements).Select(e => e.Prestation).Concat(commandes.Select(c => c.Prestation)),
            executions.Concat(dechargements).SelectMany(e => new[] { e.IDTRACTEUR, e.IDREMORQUE })
                .Concat(references.Values.SelectMany(r => new[] { r.IDTRACTEUR, r.IDREMORQUE }))
                .Concat(remorqueIds.Cast<long?>()),
            ct);
        var vehicules = await db.Vehicule.Where(v => remorqueIds.Contains(v.IDVEHICULES)).OrderBy(v => v.Num_immat)
            .Select(v => new { v.IDVEHICULES, v.Num_immat, v.Marque, v.Modele })
            .ToListAsync(ct);

        return new
        {
            chauffeurs = chauffeurs.Select(c =>
            {
                var reference = references.GetValueOrDefault(c.IDCHAUFFEURS);
                var personnel = c.HasPersonnel ? string.Join(' ', new[] { c.Prenom_Personnel, c.Nom_Personnel }.Where(part => !string.IsNullOrEmpty(part))) : "";
                var nom = c.Nom_chauffeur?.Trim();
                return new
                {
                    id = Text(c.IDCHAUFFEURS),
                    nom = !string.IsNullOrEmpty(nom) ? nom : personnel.Length > 0 ? personnel : $"Chauffeur {c.IDCHAUFFEURS}",
                    // pas de fiche Personnel = sous-traitant
                    sousTraitant = IsId(c.IDPERSONNELS) ? null : c.Societe,
                    remorqueParDefautId = reference is not null && IsId(reference.IDREMORQUE) ? Text(reference.IDREMORQUE!.Value) : null,
                    tracteurParDefautId = reference is not null && IsId(reference.IDTRACTEUR) ? Text(reference.IDTRACTEUR!.Value) : null,
                    tracteurParDefaut = reference is not null && IsId(reference.IDTRACTEUR) ? lookups.Immats.GetValueOrDefault(reference.IDTRACTEUR!.Value) : null,
                };
            }),
            remorques = vehicules.Select(v =>
            {
                var type = string.Join(' ', new[] { v.Marque, v.Modele }.Where(part => !string.IsNullOrEmpty(part)));
                return new { id = Text(v.IDVEHICULES), immat = v.Num_immat ?? $"Véhicule {v.IDVEHICULES}", type = type.Length > 0 ? type : "Remorque" };
            }),
            executions = executions.Select(e => ToExecution(e, lookups)),
            dechargements = dechargements.Select(e => ToDechargement(e, lookups)),
            commandes = commandes.Select(c => ToCommande(c, lookups)),
        };
    }

    // déplacement / redimensionnement d'une carte
    public async Task<object> MoveExecutionAsync(long id, MovePlanningExecutionDto dto, CancellationToken ct)
    {
        var start = ParseDate(dto.start.Value, "start");
        var end = ParseDate(dto.end.Value, "end");
        if (end <= start) throw new BadRequestException("La fin de l'exécution doit être après son début.");

        var execution = await db.Execution.FirstOrDefaultAsync(e => e.IDEXECUTIONS == id, ct) ?? throw new NotFoundException("Exécution introuvable.");
        if (IsInvoiced(execution.Facture, execution.IDFACTURES)) throw new ConflictException("Exécution déjà facturée : elle ne peut plus être déplacée.");

        execution.Date_Execution = start;
        execution.Date_Fin_Execution = end;
        execution.Date_heure_modification = DateTime.UtcNow;
        if (dto.chauffeurId.IsSet)
        {
            execution.IDCHAUFFEUR = string.IsNullOrEmpty(dto.chauffeurId.Value) ? null : ParseId(dto.chauffeurId.Value);
        }
        if (dto.remorqueId.IsSet)
        {
            long? remorqueId = string.IsNullOrEmpty(dto.remorqueId.Value) ? null : ParseId(dto.remorqueId.Value);
            execution.IDREMORQUE = remorqueId ?? 0;
            execution.Immat_remorque = remorqueId is null ? null : await ImmatOfAsync(remorqueId.Value, ct);
        }
        await db.SaveChangesAsync(ct);
        return await ExecutionViewAsync(id, ct);
    }

    // commande glissée sur le planning, +1 unité planifiée
    public async Task<object> CreateExecutionAsync(CreatePlanningExecutionDto dto, CancellationToken ct)
    {
        var commandeId = ParseId(dto.commandeId.Value);
        var start = ParseDate(dto.start.Value, "start");
        var end = ParseDate(dto.end.Value, "end");
        if (end <= start) throw new BadRequestException("La fin de l'exécution doit être après son début.");
        long? chauffeurId = string.IsNullOrEmpty(dto.chauffeurId.Value) ? null : ParseId(dto.chauffeurId.Value);
        long? tracteurId = string.IsNullOrEmpty(dto.tracteurId.Value) ? null : ParseId(dto.tracteurId.Value);
        long? remorqueId = string.IsNullOrEmpty(dto.remorqueId.Value) ? null : ParseId(dto.remorqueId.Value);
        var immatTracteur = tracteurId is null ? null : await ImmatOfAsync(tracteurId.Value, ct);
        var immatRemorque = remorqueId is null ? null : await ImmatOfAsync(remorqueId.Value, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var commande = await db.Commande.FirstOrDefaultAsync(c => c.IDCOMMANDES == commandeId, ct) ?? throw new NotFoundException("Commande introuvable.");
        var planifie = commande.QT_planifie ?? 0;
        if (planifie >= (commande.QT ?? 0)) throw new ConflictException("Cette commande est déjà entièrement planifiée.");

        commande.QT_planifie = planifie + 1;
        await db.SaveChangesAsync(ct);
        var now = DateTime.UtcNow;
        // FK à 0 par défaut en base, on force NULL
        var execution = new Execution
        {
            IDCOMMANDES = commandeId,
            IDCONTRATS = IsId(commande.IDCONTRATS) ? commande.IDCONTRATS : null,
            IDPRESTATIONS = IsId(commande.IDPRESTATIONS) ? commande.IDPRESTATIONS : null,
            IDCHAUFFEUR = chauffeurId,
            IDTRACTEUR = tracteurId ?? 0,
            IDREMORQUE = remorqueId ?? 0,
            Immat_tracteur = immatTracteur,
            Immat_remorque = immatRemorque,
            Date_Execution = start,
            Date_Fin_Execution = end,
            Reference_client = !string.IsNullOrEmpty(commande.NumRef) && commande.NumRef != "0" ? commande.NumRef : null,
            IDSOCIETES_FOURNISSEUR = null,
            IDFACTURES = null,
            IDUTILISATEURS_createur = null,
            IDUTILISATEURS_modificateur = null,
            Date_heure_creation = now,
            Date_heure_modification = now,
        };
        db.Execution.Add(execution);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ExecutionViewAsync(execution.IDEXECUTIONS, ct);
    }

    // libère aussi l'unité sur la commande
    public async Task DeleteExecutionAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.Execution.Where(e => e.IDEXECUTIONS == id)
            .Select(e => new { e.IDCOMMANDES, e.Facture, e.IDFACTURES, Bons = e.BonExecutions.Count, Supplements = e.ExecutionSupplementaires.Count })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Exécution introuvable.");
        if (IsInvoiced(existing.Facture, existing.IDFACTURES)) throw new ConflictException("Exécution déjà facturée : elle ne peut pas être retirée du planning.");
        if (existing.Bons > 0 || existing.Supplements > 0)
        {
            throw new ConflictException("Exécution liée à des bons ou prestations supplémentaires : retirez-les avant de la supprimer.");
        }

        await db.Execution.Where(e => e.IDEXECUTIONS == id).DeleteOrThrowAsync(ct);
        if (IsId(existing.IDCOMMANDES))
        {
            var commande = await db.Commande.FirstOrDefaultAsync(c => c.IDCOMMANDES == existing.IDCOMMANDES, ct);
            if (commande is not null)
            {
                commande.QT_planifie = Math.Max(0, (commande.QT_planifie ?? 0) - 1);
                await db.SaveChangesAsync(ct);
            }
        }
        await tx.CommitAsync(ct);
    }

    private async Task<object> ExecutionViewAsync(long id, CancellationToken ct)
    {
        var row = await SelectExecutions(db.Execution.Where(e => e.IDEXECUTIONS == id)).FirstOrThrowAsync(ct);
        await LoadRoutiersAsync([row.Prestation], ct);
        return ToExecution(row, await LoadLookupsAsync([row.Prestation], [row.IDTRACTEUR, row.IDREMORQUE], ct));
    }

    private async Task<string?> ImmatOfAsync(long vehiculeId, CancellationToken ct)
    {
        var vehicule = await db.Vehicule.Where(v => v.IDVEHICULES == vehiculeId).Select(v => new { v.Num_immat }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Véhicule introuvable.");
        return vehicule.Num_immat;
    }

    private static IQueryable<ExecutionRow> SelectExecutions(IQueryable<Execution> query) => query.Select(e => new ExecutionRow
    {
        IDEXECUTIONS = e.IDEXECUTIONS,
        IDCOMMANDES = e.IDCOMMANDES,
        IDCHAUFFEUR = e.IDCHAUFFEUR,
        IDTRACTEUR = e.IDTRACTEUR,
        IDREMORQUE = e.IDREMORQUE,
        Immat_tracteur = e.Immat_tracteur,
        Immat_remorque = e.Immat_remorque,
        Date_Execution = e.Date_Execution,
        Date_Fin_Execution = e.Date_Fin_Execution,
        Date_execution_liee = e.Date_execution_liee,
        Duree = e.Duree,
        Reference_client = e.Reference_client,
        RefPlanning = e.RefPlanning,
        Instruction_planning = e.Instruction_planning,
        Statut = e.Statut,
        Execution_dechargement = e.Execution_dechargement,
        IDEXECUTIONS_LIEE = e.IDEXECUTIONS_LIEE,
        ContratSociete = e.Contrat!.Societe!.Nom_societe,
        HasCommande = e.Commande != null,
        CommandeNumRef = e.Commande!.NumRef,
        CommandeSociete = e.Commande!.Contrat!.Societe!.Nom_societe,
        Prestation = e.Prestation == null ? null : new PrestationRow
        {
            IDPRESTATIONS = e.Prestation.IDPRESTATIONS,
            Prix_unitaire = e.Prestation.Prix_unitaire,
            HasMarchandise = e.Prestation.Marchandise != null,
            IDMARCHANDISES = e.Prestation.Marchandise!.IDMARCHANDISES,
            Nom_marchandise = e.Prestation.Marchandise!.Nom_marchandise,
            CouleurPlanning = e.Prestation.Marchandise!.CouleurPlanning,
        },
    });

    private async Task LoadRoutiersAsync(IEnumerable<PrestationRow?> prestations, CancellationToken ct)
    {
        var rows = prestations.Where(p => p is not null).Select(p => p!).ToList();
        var ids = rows.Select(p => p.IDPRESTATIONS).Distinct().ToList();
        if (ids.Count == 0) return;
        var first = (await db.PrestationRoutier.Where(r => ids.Contains(r.IDPRESTATIONS)).OrderBy(r => r.IDPRESTATIONS_ROUTIER)
                .Select(r => new RoutierRow(r.IDPRESTATIONS, r.IDPOINTDEPART, r.IDPOINTARRIVEE, r.Duree))
                .ToListAsync(ct))
            .GroupBy(r => r.IDPRESTATIONS)
            .ToDictionary(g => g.Key, g => g.First());
        foreach (var row in rows) row.Routier = first.GetValueOrDefault(row.IDPRESTATIONS);
    }

    private async Task<Lookups> LoadLookupsAsync(IEnumerable<PrestationRow?> prestations, IEnumerable<long?> vehiculeIds, CancellationToken ct)
    {
        var pointIds = new HashSet<long>();
        foreach (var routier in prestations.Select(p => p?.Routier).Where(r => r is not null))
        {
            if (IsId(routier!.IDPOINTDEPART)) pointIds.Add(routier.IDPOINTDEPART!.Value);
            if (IsId(routier.IDPOINTARRIVEE)) pointIds.Add(routier.IDPOINTARRIVEE!.Value);
        }
        var ids = vehiculeIds.Where(IsId).Select(id => id!.Value).Distinct().ToList();
        var points = pointIds.Count > 0
            ? await db.Point.Where(p => pointIds.Contains(p.IDPOINTS)).Select(p => new { p.IDPOINTS, p.Libelle, p.Nom_societe }).ToListAsync(ct)
            : [];
        var vehicules = ids.Count > 0
            ? await db.Vehicule.Where(v => ids.Contains(v.IDVEHICULES)).Select(v => new { v.IDVEHICULES, v.Num_immat }).ToListAsync(ct)
            : [];
        return new Lookups(
            points.ToDictionary(p => p.IDPOINTS, p =>
            {
                var libelle = p.Libelle?.Trim();
                var societe = p.Nom_societe?.Trim();
                return !string.IsNullOrEmpty(libelle) ? libelle : !string.IsNullOrEmpty(societe) ? societe : $"Point {p.IDPOINTS}";
            }),
            vehicules.Where(v => !string.IsNullOrEmpty(v.Num_immat)).ToDictionary(v => v.IDVEHICULES, v => v.Num_immat!));
    }

    private static object ToExecution(ExecutionRow e, Lookups lookups)
    {
        var start = e.Date_Execution!.Value;
        var minutes = DurationMinutes(e);
        var end = e.Date_Fin_Execution is { } fin && fin > start ? fin : start.AddMinutes(minutes);
        var (depart, arrivee) = Route(e.Prestation, lookups);
        return new
        {
            id = Text(e.IDEXECUTIONS),
            commandeId = IsId(e.IDCOMMANDES) ? Text(e.IDCOMMANDES!.Value) : null,
            chauffeurId = IsId(e.IDCHAUFFEUR) ? Text(e.IDCHAUFFEUR!.Value) : null,
            remorqueId = IsId(e.IDREMORQUE) ? Text(e.IDREMORQUE!.Value) : null,
            tracteur = Immat(e.IDTRACTEUR, lookups) ?? e.Immat_tracteur,
            remorqueImmat = Immat(e.IDREMORQUE, lookups) ?? e.Immat_remorque,
            start,
            end,
            client = Client(e),
            marchandise = ToMarchandise(e.Prestation),
            reference = Reference(e),
            depart,
            arrivee,
            statut = ToStatut(e.Statut),
            nature = e.Execution_dechargement == 1 ? "dechargement" : IsId(e.IDEXECUTIONS_LIEE) ? "chargement" : "complete",
            montant = ToNumber(e.Prestation?.Prix_unitaire),
            refPlanning = e.RefPlanning ?? "",
            instruction = string.IsNullOrEmpty(e.Instruction_planning) ? null : e.Instruction_planning,
        };
    }

    private static object ToDechargement(ExecutionRow e, Lookups lookups)
    {
        var (depart, arrivee) = Route(e.Prestation, lookups);
        return new
        {
            id = Text(e.IDEXECUTIONS),
            client = Client(e),
            marchandise = ToMarchandise(e.Prestation),
            reference = Reference(e),
            depart,
            arrivee,
            tracteur = Immat(e.IDTRACTEUR, lookups) ?? e.Immat_tracteur,
            remorqueId = IsId(e.IDREMORQUE) ? Text(e.IDREMORQUE!.Value) : null,
            remorqueImmat = Immat(e.IDREMORQUE, lookups) ?? e.Immat_remorque,
            chauffeurId = IsId(e.IDCHAUFFEUR) ? Text(e.IDCHAUFFEUR!.Value) : null,
            dateChargement = e.Date_execution_liee,
            dureeMinutes = DurationMinutes(e),
            montant = ToNumber(e.Prestation?.Prix_unitaire),
        };
    }

    private static object ToCommande(CommandeRow c, Lookups lookups)
    {
        var (depart, arrivee) = Route(c.Prestation, lookups);
        var duree = TimeToMinutes(c.Prestation?.Routier?.Duree);
        return new
        {
            id = Text(c.IDCOMMANDES),
            numero = Text(c.IDCOMMANDES),
            date = c.Date_commande,
            client = c.ContratSociete ?? "Client inconnu",
            marchandise = ToMarchandise(c.Prestation),
            reference = !string.IsNullOrEmpty(c.NumRef) && c.NumRef != "0" ? c.NumRef : null,
            depart,
            arrivee,
            qt = c.QT ?? 0,
            qtPlanifie = c.QT_planifie ?? 0,
            dureeMinutes = duree != 0 ? duree : DefaultDurationMinutes,
            prixUnitaire = ToNumber(c.Prestation?.Prix_unitaire),
            instruction = string.IsNullOrEmpty(c.Instruction) ? null : c.Instruction,
        };
    }

    private static string Client(ExecutionRow e) => e.ContratSociete ?? e.CommandeSociete ?? "Client inconnu";

    private static string? Reference(ExecutionRow e) =>
        !string.IsNullOrEmpty(e.Reference_client)
            ? e.Reference_client
            : e.HasCommande && !string.IsNullOrEmpty(e.CommandeNumRef) && e.CommandeNumRef != "0" ? e.CommandeNumRef : null;

    private static int DurationMinutes(ExecutionRow e)
    {
        var own = TimeToMinutes(e.Duree);
        if (own != 0) return own;
        var routier = TimeToMinutes(e.Prestation?.Routier?.Duree);
        return routier != 0 ? routier : DefaultDurationMinutes;
    }

    private static string? Immat(long? vehiculeId, Lookups lookups) => IsId(vehiculeId) ? lookups.Immats.GetValueOrDefault(vehiculeId!.Value) : null;

    private static (string Depart, string Arrivee) Route(PrestationRow? prestation, Lookups lookups)
    {
        var routier = prestation?.Routier;
        string Label(long? id) => IsId(id) ? lookups.Points.GetValueOrDefault(id!.Value) ?? "—" : "—";
        return (Label(routier?.IDPOINTDEPART), Label(routier?.IDPOINTARRIVEE));
    }

    private static object ToMarchandise(PrestationRow? prestation)
    {
        if (prestation is not { HasMarchandise: true }) return new { id = "0", nom = "Sans marchandise", couleur = "#94a3b8" };
        return new
        {
            id = Text(prestation.IDMARCHANDISES),
            nom = prestation.Nom_marchandise ?? "Sans nom",
            couleur = WindevColor.ToHex(prestation.CouleurPlanning) ?? FallbackColors[(int)(prestation.IDMARCHANDISES % FallbackColors.Length)],
        };
    }

    // texte libre en base, jamais réécrit
    private static string ToStatut(string? statut)
    {
        var s = CombiningMarks().Replace((statut ?? "").Normalize(NormalizationForm.FormD), "").ToLowerInvariant();
        if (s.Contains("cours")) return "en_cours";
        if (s.Contains("termin") || s.Contains("realis") || s.Contains("effectu") || s.Contains("livr")) return "termine";
        return "planifie";
    }

    private static int TimeToMinutes(TimeSpan? time) => time is { } value ? value.Hours * 60 + value.Minutes : 0;

    private static double ToNumber(decimal? value) =>
        value is null ? 0 : double.Parse(value.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    // BIGINT en base
    private static string Text(long id) => id.ToString(CultureInfo.InvariantCulture);

    // WinDev met 0 et pas NULL quand il n'y a pas de lien
    private static bool IsId(long? value) => value is > 0;

    private static bool IsInvoiced(int? facture, long? idFactures) => facture == 1 || IsId(idFactures);

    private static long ParseId(string? raw)
    {
        if (raw is null || !Digits().IsMatch(raw)) throw new BadRequestException($"Identifiant invalide : {raw}");
        return Dto.Id(raw);
    }

    private static DateTime ParseDate(string? raw, string field) =>
        !string.IsNullOrEmpty(raw) && JsDate.TryParse(raw, out var date) ? date : throw new BadRequestException($"Date invalide pour « {field} ».");

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex Digits();

    [GeneratedRegex(@"[̀-ͯ]")]
    private static partial Regex CombiningMarks();
}

[Route("planning")]
public class PlanningController(PlanningService service) : ApiControllerBase
{
    // from/to (to exclu) pour les TIMESTAMP, jourDebut/jourFin (inclus) pour les DATE
    [HttpGet]
    public Task<object> GetPlanning([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? jourDebut, [FromQuery] string? jourFin, CancellationToken ct) =>
        service.GetPlanningAsync(from, to, jourDebut, jourFin, ct);

    [HttpPost("executions")]
    public Task<IActionResult> CreateExecution([Body] CreatePlanningExecutionDto dto, CancellationToken ct) => Created201(service.CreateExecutionAsync(dto, ct));

    [HttpPatch("executions/{id}")]
    public Task<object> MoveExecution(string id, [Body] MovePlanningExecutionDto dto, CancellationToken ct) =>
        service.MoveExecutionAsync(Params.ParseId(id), dto, ct);

    [HttpDelete("executions/{id}")]
    public Task DeleteExecution(string id, CancellationToken ct) => service.DeleteExecutionAsync(Params.ParseId(id), ct);
}
