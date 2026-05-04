using System.ComponentModel.DataAnnotations;

namespace BikeEnergyModel.Models;

public class RideLeg
{
    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "Distance must be greater than 0.")]
    [Display(Name = "Distance (miles)")]
    public double DistanceMiles { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Wind speed must be 0 or greater.")]
    [Display(Name = "Wind Speed (mph)")]
    public double WindSpeedMph { get; set; }

    [Required]
    [Display(Name = "Wind Direction")]
    public string WindDirection { get; set; } = "None";

    [Required]
    [Range(-30000, 30000, ErrorMessage = "Elevation gain must be between -30000 and 30000 feet.")]
    [Display(Name = "Elevation Gain (ft)")]
    public double ElevationGainFeet { get; set; }
}
