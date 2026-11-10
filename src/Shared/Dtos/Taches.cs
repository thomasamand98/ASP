namespace Horizon.Shared;

// date du jour fournie par le navigateur, serveur en UTC (Cloud Run)
public class TraiterTacheDto
{
    [Date(Required = true, RejectBlank = true)] public Opt<string> Date_traitement { get; set; }
}

public class ReprogrammerTacheDto
{
    [Date(Required = true, RejectBlank = true)] public Opt<string> Date_tache { get; set; }
}
