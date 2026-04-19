using BikeEnergyModel.Models;

namespace BikeEnergyModel.Services;

public class EnergyCalculator : IEnergyCalculator
{
    // Physics constants
    private const double G = 9.81;
    private const double CdA = 0.40;
    private const double Rho = 1.225;
    private const double EtaDrivetrain = 0.95;
    private const double EtaMotor = 0.82;
    private const double EtaDescent = 0.85;

    // Unit conversion
    private const double MilesToMeters = 1609.34;
    private const double MphToMs = 0.44704;
    private const double LbsToKg = 0.453592;
    private const double FeetToMeters = 0.3048;

    private static readonly BikeConfig[] Configs =
    [
        new("Racing Bike",      9.07,  0.005,       0.0, 0.0),
        new("E-Bike",          13.61,  0.007,  900_000.0, 250.0),
        new("E-Bike + Extender", 17.69, 0.007, 1_800_000.0, 250.0),
    ];

    public List<BikeResult> Calculate(RideInputModel input)
    {
        double vGroundMs  = input.TargetSpeedMph * MphToMs;
        double vWindMs    = input.WindSpeedMph * MphToMs;
        double vCutoffMs  = input.MotorCutoffSpeedMph * MphToMs;
        double rideDistM  = input.RideDistanceMiles * MilesToMeters;
        double hMeters    = input.ElevationGainFeet * FeetToMeters;
        double mRiderKg   = input.RiderWeightLbs * LbsToKg;
        double pMotorW    = input.MotorAssistWatts;
        double rideTimeS  = rideDistM / vGroundMs;

        // Build wind segments: (distance_m, v_air_ms)
        var segments = BuildSegments(input.WindDirection, rideDistM, vGroundMs, vWindMs);

        var results = new List<BikeResult>();

        foreach (var config in Configs)
        {
            double mTotal = mRiderKg + config.BikeWeightKg;
            bool motorActive = config.BatteryEnergyJ > 0
                               && pMotorW > 0
                               && vGroundMs <= vCutoffMs;

            // --- Step 3: Elevation cost ---
            double eGravityNet = mTotal * G * hMeters * (1.0 - EtaDescent);
            double eGravityHuman = eGravityNet;
            double batteryForFlat = config.BatteryEnergyJ;

            if (motorActive)
            {
                // Motor assists climbing; contribution limited by battery and gravity cost
                double motorClimbMechanical = Math.Min(config.BatteryEnergyJ * EtaMotor, eGravityNet);
                double motorClimbElectrical = motorClimbMechanical / EtaMotor;
                eGravityHuman = eGravityNet - motorClimbMechanical;
                batteryForFlat = config.BatteryEnergyJ - motorClimbElectrical;
                batteryForFlat = Math.Max(batteryForFlat, 0);
            }

            // --- Step 4: Motor duration on flat ---
            double motorDurationS = 0;
            if (motorActive && pMotorW > 0)
            {
                motorDurationS = batteryForFlat / pMotorW;
                motorDurationS = Math.Min(motorDurationS, rideTimeS);
            }
            double fractionMotorOn = (rideTimeS > 0) ? motorDurationS / rideTimeS : 0;

            // --- Steps 2 & 5: Flat human energy across segments ---
            double pMotorEffective = pMotorW * EtaMotor;
            double eFlatHuman = 0;

            foreach (var (segDistM, vAirMs) in segments)
            {
                double tSeg = segDistM / vGroundMs;
                double pRolling = config.RollingResistance * mTotal * G * vGroundMs;
                double pAero    = 0.5 * CdA * Rho * vAirMs * vAirMs * vGroundMs;
                double pPedal   = (pRolling + pAero) / EtaDrivetrain;

                double pHuman = motorActive
                    ? Math.Max(pPedal - pMotorEffective, 0)
                    : pPedal;

                double tMotorOn  = tSeg * fractionMotorOn;
                double tMotorOff = tSeg - tMotorOn;

                eFlatHuman += pHuman * tMotorOn + pPedal * tMotorOff;
            }

            // --- Step 6: Total human energy ---
            double eTotalHuman = eFlatHuman + eGravityHuman;

            // --- Battery remaining ---
            double batteryUsedFlat = pMotorW * motorDurationS;
            double batteryUsedClimb = config.BatteryEnergyJ - batteryForFlat;
            double batteryRemainingJ = config.BatteryEnergyJ - batteryUsedFlat - batteryUsedClimb;
            batteryRemainingJ = Math.Max(batteryRemainingJ, 0);

            // --- Step 7: Optimal motor setting ---
            double? optimalMotorW = null;
            if (config.BatteryEnergyJ > 0)
            {
                double rideTimeHours = rideTimeS / 3600.0;
                double batteryWh = config.BatteryEnergyJ / 3600.0;
                double optimal = batteryWh / rideTimeHours;
                optimalMotorW = Math.Clamp(optimal, 0, 250);
            }

            results.Add(new BikeResult
            {
                ConfigName          = config.Name,
                HumanEnergyKJ       = eTotalHuman / 1000.0,
                MotorAssistDuration = motorActive ? TimeSpan.FromSeconds(motorDurationS) : null,
                BatteryRemainingWh  = config.BatteryEnergyJ > 0 ? batteryRemainingJ / 3600.0 : null,
                OptimalMotorWatts   = optimalMotorW,
            });
        }

        double minEnergy = results.Min(r => r.HumanEnergyKJ);
        foreach (var r in results)
            r.IsRecommended = r.HumanEnergyKJ == minEnergy;

        return results;
    }

    private static List<(double DistM, double VAirMs)> BuildSegments(
        string windDirection, double rideDistM, double vGroundMs, double vWindMs)
    {
        return windDirection switch
        {
            "HeadTail" => [
                (rideDistM / 2, Math.Max(vGroundMs - vWindMs, 0)),
                (rideDistM / 2, vGroundMs + vWindMs),
            ],
            "Crosswind" => [(rideDistM, Math.Sqrt(vGroundMs * vGroundMs + vWindMs * vWindMs))],
            _           => [(rideDistM, vGroundMs)],
        };
    }
}
