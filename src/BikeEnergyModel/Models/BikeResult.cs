namespace BikeEnergyModel.Models;

public class BikeResult
{
    public string ConfigName { get; set; } = "";
    public double HumanEnergyKJ { get; set; }
    public TimeSpan? MotorAssistDuration { get; set; }
    public double? BatteryRemainingWh { get; set; }
    public double? OptimalMotorWatts { get; set; }
    public bool IsRecommended { get; set; }
}
