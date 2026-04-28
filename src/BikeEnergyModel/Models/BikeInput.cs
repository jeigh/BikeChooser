using System.ComponentModel.DataAnnotations;

namespace BikeEnergyModel.Models;

public class BikeInput
{
    public static readonly (string Label, double Value)[] CrrOptions =
    [
        ("Track / Tubular",          0.003),
        ("Performance Road",         0.004),
        ("Road Bike",                0.005),
        ("Hybrid",                   0.006),
        ("Gravel Bike",              0.007),
        ("Cyclocross",               0.008),
        ("Light Mountain Bike",      0.009),
        ("Mountain Bike",            0.010),
        ("Knobby / Off-road",        0.012),
        ("Plus Tire (2.8\"–3.0\")", 0.013),
        ("Fat Bike (4\"+)",          0.015),
    ];

    [Required(ErrorMessage = "Bike name is required.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "Bike weight must be greater than 0.")]
    [Display(Name = "Weight (lbs)")]
    public double WeightLbs { get; set; }

    [Required]
    [Range(0.003, 0.015, ErrorMessage = "Coefficient of Rolling Resistance must be one of the dropdown values.")]
    [Display(Name = "Coefficient of Rolling Resistance")]
    public double RollingResistance { get; set; } = 0.005;

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Battery capacity must be 0 or greater.")]
    [Display(Name = "Battery Capacity (Wh)")]
    public double BatteryCapacityWh { get; set; }
}
