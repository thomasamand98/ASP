using Api.Common;
using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Common;

// Portage de backend/src/common/adresse.spec.ts.
public class AdressesTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public void Pick_ne_garde_que_les_champs_envoyes_et_met_les_textes_vides_a_null()
    {
        var data = Adresses.Pick(new AdresseDto { Adresse1 = "Rue 1", CP = "  " });
        Assert.Equal(new Dictionary<string, string?> { ["Adresse1"] = "Rue 1", ["CP"] = null }, data);
    }

    [Fact]
    public async Task Creation_de_fiche_cree_l_adresse_et_renvoie_son_id()
    {
        await using var db = NewDb();
        var id = await Adresses.SaveAsync(db, null, new() { ["Adresse1"] = "Rue 1" });
        var adresse = await db.Adresse.SingleAsync();
        Assert.Equal(adresse.IDADRESSES, id);
        Assert.Equal("Rue 1", adresse.Adresse1);
        Assert.NotNull(adresse.Date_heure_creation);
        Assert.Null(adresse.IDUTILISATEURS_createur);
    }

    [Fact]
    public async Task Modification_met_a_jour_l_adresse_pointee_par_la_fiche()
    {
        await using var db = NewDb();
        db.Adresse.Add(new Adresse { IDADRESSES = 7, Adresse1 = "Rue 1" });
        await db.SaveChangesAsync();

        Assert.Equal(7, await Adresses.SaveAsync(db, 7, new() { ["Localite"] = "Liège" }));
        var adresse = await db.Adresse.SingleAsync();
        Assert.Equal("Liège", adresse.Localite);
        Assert.Equal("Rue 1", adresse.Adresse1);
        Assert.NotNull(adresse.Date_heure_modification);
    }

    [Fact]
    public async Task Modification_d_une_fiche_sans_adresse_cree_puis_renvoie_le_nouvel_id()
    {
        await using var db = NewDb();
        // 0 est le défaut des colonnes IDADRESSES : équivaut à « aucune ».
        var id = await Adresses.SaveAsync(db, 0, new() { ["CP"] = "4000" });
        Assert.NotNull(id);
        Assert.Equal("4000", (await db.Adresse.SingleAsync()).CP);
    }

    [Fact]
    public async Task Adresse_entierement_vide_rien_n_est_cree()
    {
        await using var db = NewDb();
        Assert.Null(await Adresses.SaveAsync(db, null, new() { ["Adresse1"] = null, ["CP"] = null }));
        Assert.Empty(await db.Adresse.ToListAsync());
    }

    [Fact]
    public async Task Aucun_champ_d_adresse_envoye_l_id_reste_inchange()
    {
        await using var db = NewDb();
        Assert.Equal(7, await Adresses.SaveAsync(db, 7, []));
        Assert.Empty(await db.Adresse.ToListAsync());
    }

    [Fact]
    public async Task Modification_d_une_adresse_disparue_renvoie_404()
    {
        await using var db = NewDb();
        await Assert.ThrowsAsync<RecordNotFoundException>(() => Adresses.SaveAsync(db, 99, new() { ["CP"] = "4000" }));
    }
}
