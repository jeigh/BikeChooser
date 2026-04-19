using BikeEnergyModel.Models;

namespace BikeEnergyModel.Services;

public interface IEnergyCalculator
{
    List<BikeResult> Calculate(RideInputModel input);
}
