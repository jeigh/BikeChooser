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
    private const double WhToJ = 3600.0;

    public List<BikeResult> Calculate(RideInputModel input)
    {
        double vGroundMs = input.TargetSpeedMph * MphToMs;
        double vCutoffMs = input.MotorCutoffSpeedMph * MphToMs;
        double mRiderKg  = input.RiderWeightLbs * LbsToKg;

        // Compute total ride distance/time and climb/descent totals from legs
        double totalDistM = 0;
        double climbM = 0;
        double descentM = 0;
        foreach (var leg in input.Legs)
        {
            totalDistM += leg.DistanceMiles * MilesToMeters;
            double hM = leg.ElevationGainFeet * FeetToMeters;
            if (hM > 0) climbM += hM;
            else        descentM += -hM;
        }
        double rideTimeS = totalDistM / vGroundMs;

        var results = new List<BikeResult>();

        foreach (var bike in input.Bikes)
        {
            double bikeWeightKg = bike.WeightLbs * LbsToKg;
            double batteryEnergyJ = bike.BatteryCapacityWh * WhToJ;

            // Optimal motor setting
            double? optimalMotorW = null;
            double pMotorW = 0;
            if (batteryEnergyJ > 0)
            {
                double rideTimeHours = rideTimeS / 3600.0;
                double batteryWh = bike.BatteryCapacityWh;
                optimalMotorW = Math.Clamp(batteryWh / rideTimeHours, 0, input.MaxMotorAssistWatts);
                pMotorW = optimalMotorW.Value;
            }

            double mTotal = mRiderKg + bikeWeightKg;
            bool motorActive = batteryEnergyJ > 0
                               && pMotorW > 0
                               && vGroundMs <= vCutoffMs;

            // --- Elevation cost ---
            // Generalized for asymmetric routes: full mgh on climbs,
            // η_descent recovery on descents. Clamped at 0 so a wildly
            // net-downhill ride doesn't generate negative human cost.
            double eGravityNet = Math.Max(
                mTotal * G * (climbM - EtaDescent * descentM),
                0);
            double eGravityHuman = eGravityNet;
            double batteryForFlat = batteryEnergyJ;

            if (motorActive)
            {
                double motorClimbMechanical = Math.Min(batteryEnergyJ * EtaMotor, eGravityNet);
                double motorClimbElectrical = motorClimbMechanical / EtaMotor;
                eGravityHuman = eGravityNet - motorClimbMechanical;
                batteryForFlat = batteryEnergyJ - motorClimbElectrical;
                batteryForFlat = Math.Max(batteryForFlat, 0);
            }

            // --- Motor duration on flat ---
            double motorDurationS = 0;
            if (motorActive && pMotorW > 0)
            {
                motorDurationS = batteryForFlat / pMotorW;
                motorDurationS = Math.Min(motorDurationS, rideTimeS);
            }
            double fractionMotorOn = (rideTimeS > 0) ? motorDurationS / rideTimeS : 0;

            // --- Flat human energy across legs ---
            double pMotorEffective = pMotorW * EtaMotor;
            double eFlatHuman = 0;

            foreach (var leg in input.Legs)
            {
                double segDistM = leg.DistanceMiles * MilesToMeters;
                double vWindMs  = leg.WindSpeedMph * MphToMs;
                double vAirMs   = ComputeAirspeed(leg.WindDirection, vGroundMs, vWindMs);

                double tSeg     = segDistM / vGroundMs;
                double pRolling = bike.RollingResistance * mTotal * G * vGroundMs;
                double pAero    = 0.5 * CdA * Rho * vAirMs * vAirMs * vGroundMs;
                double pPedal   = (pRolling + pAero) / EtaDrivetrain;

                double pHuman = motorActive
                    ? Math.Max(pPedal - pMotorEffective, 0)
                    : pPedal;

                double tMotorOn  = tSeg * fractionMotorOn;
                double tMotorOff = tSeg - tMotorOn;

                eFlatHuman += pHuman * tMotorOn + pPedal * tMotorOff;
            }

            // --- Total human energy ---
            double eTotalHuman = eFlatHuman + eGravityHuman;

            // --- Battery remaining ---
            double batteryUsedFlat = pMotorW * motorDurationS;
            double batteryUsedClimb = batteryEnergyJ - batteryForFlat;
            double batteryRemainingJ = batteryEnergyJ - batteryUsedFlat - batteryUsedClimb;
            batteryRemainingJ = Math.Max(batteryRemainingJ, 0);

            results.Add(new BikeResult
            {
                ConfigName          = bike.Name,
                HumanEnergyKJ       = eTotalHuman / 1000.0,
                MotorAssistDuration = motorActive ? TimeSpan.FromSeconds(motorDurationS) : null,
                BatteryRemainingWh  = batteryEnergyJ > 0 ? batteryRemainingJ / 3600.0 : null,
                OptimalMotorWatts   = optimalMotorW,
            });
        }

        if (results.Count > 0)
        {
            double minEnergy = results.Min(r => r.HumanEnergyKJ);
            foreach (var r in results)
                r.IsRecommended = r.HumanEnergyKJ == minEnergy;
        }

        return results;
    }

    private static double ComputeAirspeed(string windDirection, double vGroundMs, double vWindMs)
    {
        // Bike-relative angle: 0° = headwind, 180° = tailwind. Diagonal options
        // collapse left/right because aero drag depends only on magnitude.
        double angleDeg = windDirection switch
        {
            "Headwind"          => 0,
            "DiagonalHeadwind"  => 45,
            "Crosswind"         => 90,
            "DiagonalTailwind"  => 135,
            "Tailwind"          => 180,
            _                   => double.NaN, // None
        };

        if (double.IsNaN(angleDeg)) return vGroundMs;

        double angleRad = angleDeg * Math.PI / 180.0;
        double vParallel  = vWindMs * Math.Cos(angleRad); // + = headwind component
        double vPerp      = vWindMs * Math.Sin(angleRad); // sign irrelevant; squared
        double vEffective = vGroundMs + vParallel;
        if (vEffective < 0) vEffective = 0;               // preserve tailwind-faster-than-bike clamp
        return Math.Sqrt(vEffective * vEffective + vPerp * vPerp);
    }
}
