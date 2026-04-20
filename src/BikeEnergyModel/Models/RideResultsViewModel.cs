namespace BikeEnergyModel.Models;

public class RideResultsViewModel
{
    public RideInputModel Input { get; set; } = new();
    public List<BikeResult>? Results { get; set; }
    public double? TotalRideDistanceMiles { get; set; }
}
