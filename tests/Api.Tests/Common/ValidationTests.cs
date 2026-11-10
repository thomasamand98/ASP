using System.Text.Json;
using Api.Common;
using Api.Features.Chauffeurs;
using Api.Features.Contrats;
using Api.Features.DocumentTemplates;
using Api.Features.Personnels;
using Api.Features.Planning;
using Api.Features.Points;

namespace Api.Tests.Common;

public class ValidationTests
{
    private static JsonElement? Json(string? json) => json is null ? null : JsonDocument.Parse(json).RootElement.Clone();

    private static T Parse<T>(string? json) where T : class, new() => Zod.Parse<T>(Json(json));

    private static string MessageOf<T>(string? json) where T : class, new() =>
        Assert.Throws<BadRequestException>(() => Parse<T>(json)).Message;

    [Fact]
    public void Laisse_passer_un_corps_valide_et_ignore_les_champs_inconnus()
    {
        var dto = Parse<CreateChauffeurDto>("""{ "Nom_chauffeur": "Dupont", "IDSOCIETES": "12", "Archive": 0, "Inconnu": "x" }""");
        Assert.Equal("Dupont", dto.Nom_chauffeur.Value);
        Assert.Equal("12", dto.IDSOCIETES.Value);
        Assert.Equal(0, dto.Archive.Value);
        Assert.False(dto.Telephone.IsSet);
        Assert.False(dto.IDPERSONNELS.IsSet);
    }

    [Fact]
    public void Accepte_les_conversions_tolerees()
    {
        // nombre/texte interchangeables, booléen en 0/1
        var dto = Parse<CreateChauffeurDto>("""{ "Telephone": 471000000, "IDSOCIETES": 12, "Archive": true }""");
        Assert.Equal("471000000", dto.Telephone.Value);
        Assert.Equal("12", dto.IDSOCIETES.Value);
        Assert.Equal(1, dto.Archive.Value);
        Assert.Equal(1, Parse<CreateChauffeurDto>("""{ "Archive": "1" }""").Archive.Value);
        Assert.Equal(21.5, Parse<CreateChauffeurDto>("""{ "Archive": " 21,5 " }""").Archive.Value);
    }

    [Fact]
    public void Traite_un_nombre_vide_comme_non_envoye()
    {
        Assert.False(Parse<CreateChauffeurDto>("""{ "Archive": "" }""").Archive.IsSet);
    }

    [Fact]
    public void Refuse_un_identifiant_non_numerique()
    {
        Assert.Equal("« IDSOCIETES » : identifiant invalide.", MessageOf<CreateChauffeurDto>("""{ "IDSOCIETES": "abc" }"""));
    }

    [Fact]
    public void Refuse_un_mauvais_type_avec_un_message_en_francais()
    {
        Assert.Equal("« Nom_chauffeur » : texte attendu.", MessageOf<CreateChauffeurDto>("""{ "Nom_chauffeur": { "x": 1 } }"""));
        Assert.Equal("« Archive » : nombre attendu.", MessageOf<CreateChauffeurDto>("""{ "Archive": "oui" }"""));
        Assert.Equal("« Nom_chauffeur » : texte attendu.", MessageOf<CreateChauffeurDto>("""{ "Nom_chauffeur": null }"""));
    }

    [Fact]
    public void Refuse_un_corps_qui_n_est_pas_un_objet()
    {
        Assert.Equal("Corps de requête invalide.", MessageOf<CreateChauffeurDto>("[1, 2]"));
        Assert.Equal("Corps de requête invalide.", MessageOf<CreateChauffeurDto>(null));
    }

    [Fact]
    public void Signale_les_champs_obligatoires_et_les_valeurs_hors_liste()
    {
        Assert.Equal(
            "« Nom » : champ obligatoire. « Type_document » : valeur non autorisée (CONTRAT).",
            MessageOf<CreateDocumentTemplateDto>("""{ "Type_document": "FACTURE", "Contenu_html": "" }"""));
        // [Partial] : plus rien d'obligatoire
        Assert.False(Parse<UpdateDocumentTemplateDto>("{}").Nom.IsSet);
    }

    [Fact]
    public void Valide_les_listes_imbriquees_et_normalise_les_dates_seules()
    {
        var dto = Parse<ContratTravailDto>("""{ "Date_debut": "2026-01-15", "Taux": [{ "Taux_horaire": "15,5", "IDTAUX_HORAIRE": "3" }] }""");
        Assert.Equal("2026-01-15T00:00:00.000Z", dto.Date_debut.Value);
        var taux = Assert.Single(dto.Taux.Value);
        Assert.Equal("15,5", taux.Taux_horaire.Value);
        Assert.Equal("3", taux.IDTAUX_HORAIRE.Value);

        Assert.Equal("", Parse<ContratTravailDto>("""{ "Date_fin": "" }""").Date_fin.Value);
        Assert.Equal("« Taux » : liste attendue.", MessageOf<ContratTravailDto>("""{ "Taux": "x" }"""));
        Assert.Equal("« Taux.0.IDTAUX_HORAIRE » : identifiant invalide.", MessageOf<ContratTravailDto>("""{ "Taux": [{ "IDTAUX_HORAIRE": "1;DROP" }] }"""));
        Assert.Equal("« Date_debut » : date invalide.", MessageOf<ContratTravailDto>("""{ "Date_debut": "2026-13-45" }"""));
    }

    [Fact]
    public void Accepte_null_la_ou_le_DTO_le_prevoit()
    {
        var dto = Parse<MovePlanningExecutionDto>("""{ "start": "a", "end": "b", "chauffeurId": null }""");
        Assert.Equal("a", dto.start.Value);
        Assert.True(dto.chauffeurId.IsSet);
        Assert.Null(dto.chauffeurId.Value);
        Assert.False(dto.remorqueId.IsSet);
    }

    [Fact]
    public void Decimal_normalise_la_virgule_et_accepte_le_vide()
    {
        Assert.Equal("21.5", Parse<CreateContratDto>("""{ "Taux_tva": "21,5" }""").Taux_tva.Value);
        Assert.Equal("6", Parse<CreateContratDto>("""{ "Taux_tva": 6 }""").Taux_tva.Value);
        Assert.Equal("", Parse<CreateContratDto>("""{ "Taux_tva": "" }""").Taux_tva.Value);
        Assert.Equal("« Taux_tva » : nombre décimal attendu.", MessageOf<CreateContratDto>("""{ "Taux_tva": "vingt" }"""));
    }

    [Fact]
    public void Evalue_les_regles_entre_champs_quand_les_types_sont_bons()
    {
        Assert.Equal(
            "« Horaires.0.Jour_semaine » : Number must be less than or equal to 7. « Horaires.0.Heure_debut » : heure « HH:MM » attendue. "
            + "« Horaires.0.Heure_fin » : la fin doit suivre le début. « Horaires.1.Jour_semaine » : champ obligatoire. « Horaires.1.Heure_debut » : texte attendu. "
            + "« Horaires.1.Heure_fin » : champ obligatoire.",
            MessageOf<CreatePointDto>("""{ "Horaires": [{ "Jour_semaine": 8, "Heure_debut": "8:00", "Heure_fin": "07:00" }, { "Heure_debut": 12 }] }"""));
    }
}
