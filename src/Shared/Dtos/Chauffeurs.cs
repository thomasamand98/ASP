namespace Horizon.Shared;

// Champs qu'un client peut envoyer pour créer/modifier un chauffeur.
// IDPERSONNELS/IDSOCIETES sont des strings côté JSON. Archive est un code
// 0/1 (pas un booléen).
public class CreateChauffeurDto
{
    [Text] public Opt<string> Nom_chauffeur { get; set; }
    [Text] public Opt<string> Telephone { get; set; }
    [Text] public Opt<string> Categorie { get; set; }
    [Num] public Opt<double> Archive { get; set; }
    [Id] public Opt<string> IDPERSONNELS { get; set; }
    [Id] public Opt<string> IDSOCIETES { get; set; }
}

[Partial]
public class UpdateChauffeurDto : CreateChauffeurDto;
