using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Common;

public record AdresseView(string? Adresse1, string? Adresse2, string? Adresse3, string? CP, string? Localite, string? Pays, string? Pays_full_name)
{
    public static AdresseView? From(Adresse? a) =>
        a is null ? null : new AdresseView(a.Adresse1, a.Adresse2, a.Adresse3, a.CP, a.Localite, a.Pays, a.Pays_full_name);
}

public static class Adresses
{
    public static readonly string[] FieldNames = ["Adresse1", "Adresse2", "Adresse3", "CP", "Localite", "Pays", "Pays_full_name"];

    // seulement les champs envoyés, vide = NULL
    public static Dictionary<string, string?> Pick(IAdresseFields? source)
    {
        var data = new Dictionary<string, string?>();
        if (source is null) return data;
        void Add(string name, Opt<string> field)
        {
            if (field.IsSet) data[name] = string.IsNullOrWhiteSpace(field.Value) ? null : field.Value;
        }
        Add("Adresse1", source.Adresse1);
        Add("Adresse2", source.Adresse2);
        Add("Adresse3", source.Adresse3);
        Add("CP", source.CP);
        Add("Localite", source.Localite);
        Add("Pays", source.Pays);
        Add("Pays_full_name", source.Pays_full_name);
        return data;
    }

    // currentId 0 = pas d'adresse (défaut en base)
    public static async Task<long?> SaveAsync(AppDbContext db, long? currentId, Dictionary<string, string?> data, CancellationToken ct = default)
    {
        var id = currentId is null or 0 ? null : currentId;
        if (data.Count == 0) return id;
        if (id is not null)
        {
            var existing = await db.Adresse.FirstOrDefaultAsync(a => a.IDADRESSES == id, ct) ?? throw new RecordNotFoundException();
            Assign(existing, data);
            existing.Date_heure_modification = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return id;
        }
        // pas d'adresse vide
        if (!data.Values.Any(value => !string.IsNullOrWhiteSpace(value))) return null;
        // NULL, le défaut 0 casserait la FK
        var created = new Adresse { Date_heure_creation = DateTime.UtcNow, IDUTILISATEURS_createur = null, IDUTILISATEURS_modificateur = null };
        Assign(created, data);
        db.Adresse.Add(created);
        await db.SaveChangesAsync(ct);
        return created.IDADRESSES;
    }

    private static void Assign(Adresse adresse, Dictionary<string, string?> data)
    {
        foreach (var (field, value) in data)
        {
            switch (field)
            {
                case "Adresse1": adresse.Adresse1 = value; break;
                case "Adresse2": adresse.Adresse2 = value; break;
                case "Adresse3": adresse.Adresse3 = value; break;
                case "CP": adresse.CP = value; break;
                case "Localite": adresse.Localite = value; break;
                case "Pays": adresse.Pays = value; break;
                case "Pays_full_name": adresse.Pays_full_name = value; break;
            }
        }
    }
}
