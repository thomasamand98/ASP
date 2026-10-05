namespace Horizon.Shared;

// Champs qu'un client peut envoyer pour créer/modifier un attelage de
// référence (table attelages_reference). Les IDs sont des strings côté
// JSON, "" = aucun lien.
public class CreateAttelageDto
{
    [Id] public Opt<string> IDCHAUFFEUR { get; set; }
    [Id] public Opt<string> IDTRACTEUR { get; set; }
    [Id] public Opt<string> IDREMORQUE { get; set; }
    [Id] public Opt<string> IDSOCIETES { get; set; }
}

[Partial]
public class UpdateAttelageDto : CreateAttelageDto;
