namespace Horizon.Shared;

public class CreateVehiculeDto
{
    [Num] public Opt<double> Type { get; set; }
    [Text] public Opt<string> Marque { get; set; }
    [Text] public Opt<string> Modele { get; set; }
    [Text] public Opt<string> Num_police_assurance { get; set; }
    [Text] public Opt<string> Num_immat { get; set; }
    [Text] public Opt<string> Num_chassis { get; set; }
    [Date] public Opt<string> Date_validite_assurance { get; set; }
    [Text] public Opt<string> Num_licence_transport { get; set; }
    [Date] public Opt<string> Date_validite_licence { get; set; }
    [Date] public Opt<string> Date_modification_licence { get; set; }
    [Date] public Opt<string> Date_inspection_auto { get; set; }
    [Date] public Opt<string> Date_radiation_immatriculation { get; set; }
    [Date] public Opt<string> Date_vente { get; set; }
    [Id] public Opt<string> IDSOCIETES { get; set; }
    [Date] public Opt<string> Date_premiere_mise_en_circulation { get; set; }
    [Date] public Opt<string> Date_validite_tachygeaphe { get; set; }
    [Num] public Opt<double> Avec_compresseur { get; set; }
}

[Partial]
public class UpdateVehiculeDto : CreateVehiculeDto;
