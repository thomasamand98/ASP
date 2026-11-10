namespace Horizon.Shared;

// table attelages_reference, "" = aucun lien
public class CreateAttelageDto
{
    [Id] public Opt<string> IDCHAUFFEUR { get; set; }
    [Id] public Opt<string> IDTRACTEUR { get; set; }
    [Id] public Opt<string> IDREMORQUE { get; set; }
    [Id] public Opt<string> IDSOCIETES { get; set; }
}

[Partial]
public class UpdateAttelageDto : CreateAttelageDto;
