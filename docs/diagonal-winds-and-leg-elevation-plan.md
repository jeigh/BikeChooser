# Diagonal Winds + Per-Leg Elevation Plan

Two related changes to the leg-based ride model:

1. **Diagonal wind directions** (NE/SE/NW/SW relative to the bike) for more realistic crosswind situations.
2. **Per-leg elevation gain** as a signed value, replacing the single ride-level `ElevationGainFeet`.

Both changes touch `RideLeg`, `EnergyCalculator`, and the legs section of `Index.cshtml`. They're bundled because they require the same UI rework.

---

## Part 1 — Diagonal Winds

### Storage convention

Wind direction stays as a string enum on `RideLeg.WindDirection`, **bike-relative** (the existing convention). Two new options join the list:

| Label                | Bike-relative angle | Aero notes |
|----------------------|---------------------|------------|
| None                 | n/a                 | `v_air = v_ground` |
| Headwind             | 0°                  | full headwind |
| **Diagonal Headwind** | **45°**             | front-quartering — covers NE *and* NW (symmetric) |
| Crosswind            | 90°                 | pure side wind |
| **Diagonal Tailwind** | **135°**            | rear-quartering — covers SE *and* SW (symmetric) |
| Tailwind             | 180°                | full tailwind |

**Why bike-relative**: the aero formula needs the wind angle relative to the bike's motion. Storing absolute compass directions would also require a bike-heading per leg, then the calculator would subtract them — two fields encoding what's effectively one number.

**Why left/right collapse**: aero drag depends only on the magnitude of the relative-air vector, so a wind from the left vs right of equal magnitude produces identical resistance. NE and NW (relative to a bike heading "north") are aero-equivalent, as are SE and SW. Five distinct cases plus "None" cover everything.

### Math

Generalize `ComputeAirspeed` to accept any angle:

```csharp
private static double ComputeAirspeed(string windDirection, double vGroundMs, double vWindMs)
{
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
    double vParallel = vWindMs * Math.Cos(angleRad);   // + = headwind component
    double vPerp     = vWindMs * Math.Sin(angleRad);   // sign irrelevant; squared
    double vEffective = vGroundMs + vParallel;
    if (vEffective < 0) vEffective = 0;                 // preserve existing tailwind clamp
    return Math.Sqrt(vEffective * vEffective + vPerp * vPerp);
}
```

Sanity-check:
- 0° → `v_ground + v_wind` ✓
- 90° → `sqrt(v_ground² + v_wind²)` ✓
- 180° → `max(v_ground − v_wind, 0)` ✓ (clamp preserved)
- 45° → `sqrt((v_ground + v_wind/√2)² + (v_wind/√2)²)` — between headwind and crosswind, as expected
- 135° → `sqrt((v_ground − v_wind/√2)² + (v_wind/√2)²)` (or 0 + crosswind component if bike is slower than the parallel tailwind component)

### UI

Append two `<option>` entries to the wind direction `<select>` in [Index.cshtml](../src/BikeEnergyModel/Views/Home/Index.cshtml) (both the C# render block and the JS template for new rows):

- `Diagonal Headwind`
- `Diagonal Tailwind`

Order in the dropdown should follow angle (None, Headwind, Diagonal Headwind, Crosswind, Diagonal Tailwind, Tailwind) so the list reads as a continuous sweep from "wind in your face" to "wind at your back."

---

## Part 2 — Per-Leg Elevation Gain

### Why move it

The current `RideInputModel.ElevationGainFeet` is one number for the whole ride, and the formula `m × g × h × (1 − η_descent)` silently assumes a balanced round trip (every foot climbed is also descended). One-way uphill rides are under-charged.

Per-leg signed elevation lets us model asymmetric routes correctly and reduces to the existing math when climb = descent.

### Storage

- New field on `RideLeg`: `public double ElevationGainFeet { get; set; }`
- Signed: positive = net climb on that leg, negative = net descent. Range ±30 000 ft.
- Remove `RideInputModel.ElevationGainFeet` and the corresponding field in the ride-parameters block of the form.

### Math

In `EnergyCalculator.Calculate()`, replace the single `hMeters` with two sums:

```csharp
double climbM = 0;
double descentM = 0;
foreach (var leg in input.Legs)
{
    double hM = leg.ElevationGainFeet * FeetToMeters;
    if (hM > 0) climbM += hM;
    else        descentM += -hM;
}

double eGravityNet = Math.Max(
    mTotal * G * (climbM - EtaDescent * descentM),
    0   // a wildly net-downhill ride doesn't generate negative human cost
);
```

This is a strict generalization of the current formula:

| Scenario | Old behavior | New behavior |
|---|---|---|
| Balanced round trip (climb = descent = h) | `m·g·h·(1−η_descent)` | `m·g·(h − η_descent·h)` = same ✓ |
| Pure uphill (descent = 0) | undermodeled | full `m·g·climb` ✓ |
| Pure downhill (climb = 0) | undermodeled | clamped to 0 (no free energy from gravity) |

The motor-allocation logic doesn't change: `motorClimbMechanical = Math.Min(batteryEnergyJ × η_motor, eGravityNet)`. `eGravityNet` is just sourced from per-leg sums now.

### UI

Add a column to the legs table in [Index.cshtml](../src/BikeEnergyModel/Views/Home/Index.cshtml):

| Distance (mi) | Wind (mph) | Direction | **Elevation (ft)** |        |
|---------------|------------|-----------|--------------------|--------|

- Input is a number field. Placeholder `0` (or `-200` to hint at signed values).
- Add the same column to the JS template for newly-added rows.
- Drop the `Total Elevation Gain (feet)` field from the top "ride parameters" `<div class="row g-3 mb-4">` block.

---

## Files to change

- `Models/RideLeg.cs` — add `ElevationGainFeet` (double, signed). Existing `WindDirection` string accepts the two new values.
- `Models/RideInputModel.cs` — remove `ElevationGainFeet`.
- `Services/EnergyCalculator.cs` — generalize `ComputeAirspeed` to use cos/sin; replace single-h elevation block with the two-sum version.
- `Views/Home/Index.cshtml` — add Elevation column to legs table (and JS template); remove ride-level Elevation field; add two wind-direction options to the `<select>` (and JS template).
- `CLAUDE.md`, `README.md` — reflect the new wind options and per-leg elevation.

## Open questions

- **Default value for new legs**: `0` for both new fields. Existing default leg is fine.
- **Validation on `ElevationGainFeet`**: `[Range(-30000, 30000)]` is plenty for any real route on Earth.
- **Wind direction default**: stays "None."
