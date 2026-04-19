using System.ComponentModel.DataAnnotations;

namespace BikeEnergyModel.Models;

public class RideInputModel
{
    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "Ride distance must be greater than 0.")]
    [Display(Name = "Ride Distance (miles, round trip)")]
    public double RideDistanceMiles { get; set; }

    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "Target speed must be greater than 0.")]
    [Display(Name = "Target Speed (mph)")]
    public double TargetSpeedMph { get; set; }

    [Required]
    [Range(0, 250, ErrorMessage = "Motor assist must be between 0 and 250 W.")]
    [Display(Name = "Motor Assist (watts, 0–250)")]
    public double MotorAssistWatts { get; set; }

    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "Motor cutoff speed must be greater than 0.")]
    [Display(Name = "Motor Cutoff Speed (mph)")]
    public double MotorCutoffSpeedMph { get; set; } = 20;

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Wind speed must be 0 or greater.")]
    [Display(Name = "Wind Speed (mph)")]
    public double WindSpeedMph { get; set; }

    [Required]
    [Display(Name = "Wind Direction")]
    public string WindDirection { get; set; } = "None";

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Elevation gain must be 0 or greater.")]
    [Display(Name = "Total Elevation Gain (feet)")]
    public double ElevationGainFeet { get; set; }

    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "Rider weight must be greater than 0.")]
    [Display(Name = "Rider Weight (lbs)")]
    public double RiderWeightLbs { get; set; }
}
