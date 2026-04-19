namespace BikeEnergyModel.Models;

public record BikeConfig(
    string Name,
    double BikeWeightKg,
    double RollingResistance,
    double BatteryEnergyJ,
    double MaxMotorWatts
);
