namespace Horizon.Shared;

// Les dates « du jour » sont envoyées par le navigateur (AAAA-MM-JJ, date
// locale de l'utilisateur) plutôt que calculées ici : le serveur peut
// tourner dans un autre fuseau horaire (Cloud Run = UTC).
public class TraiterTacheDto
{
    [Date(Required = true, RejectBlank = true)] public Opt<string> Date_traitement { get; set; }
}

public class ReprogrammerTacheDto
{
    [Date(Required = true, RejectBlank = true)] public Opt<string> Date_tache { get; set; }
}
