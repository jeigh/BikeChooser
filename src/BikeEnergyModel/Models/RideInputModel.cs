using System.ComponentModel.DataAnnotations;

namespace BikeEnergyModel.Models;

public class RideInputModel
{
    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "Target speed must be greater than 0.")]
    [Display(Name = "Target Speed (mph)")]
    public double TargetSpeedMph { get; set; } = 20;

[Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "Motor cutoff speed must be greater than 0.")]
    [Display(Name = "Motor Cutoff Speed (mph)")]
    public double MotorCutoffSpeedMph { get; set; } = 20;

    [Required]
    [Range(1, 1000, ErrorMessage = "Max motor assist must be between 1 and 1000 W.")]
    [Display(Name = "Max Motor Assist (watts)")]
    public double MaxMotorAssistWatts { get; set; } = 250;

    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "Rider weight must be greater than 0.")]
    [Display(Name = "Rider Weight (lbs)")]
    public double RiderWeightLbs { get; set; } = 200;

    public List<RideLeg> Legs { get; set; } = [new()];

    public List<BikeInput> Bikes { get; set; } =
    [
        new() { Name = "Racing Bike",        WeightLbs = 20.0, RollingResistance = 0.005, BatteryCapacityWh = 0   },
        new() { Name = "E-Bike",             WeightLbs = 30.0, RollingResistance = 0.007, BatteryCapacityWh = 250 },
        new() { Name = "E-Bike + Extender",  WeightLbs = 39.0, RollingResistance = 0.007, BatteryCapacityWh = 500 },
    ];
}
