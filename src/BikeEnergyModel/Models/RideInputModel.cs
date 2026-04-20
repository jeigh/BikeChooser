using System.ComponentModel.DataAnnotations;

namespace BikeEnergyModel.Models;

public class RideInputModel
{
    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "Target speed must be greater than 0.")]
    [Display(Name = "Target Speed (mph)")]
    public double TargetSpeedMph { get; set; }

[Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "Motor cutoff speed must be greater than 0.")]
    [Display(Name = "Motor Cutoff Speed (mph)")]
    public double MotorCutoffSpeedMph { get; set; } = 20;

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Elevation gain must be 0 or greater.")]
    [Display(Name = "Total Elevation Gain (feet)")]
    public double ElevationGainFeet { get; set; }

    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "Rider weight must be greater than 0.")]
    [Display(Name = "Rider Weight (lbs)")]
    public double RiderWeightLbs { get; set; }

    public List<RideLeg> Legs { get; set; } = [new()];
}
