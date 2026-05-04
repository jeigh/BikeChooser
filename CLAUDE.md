# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## After Every Enhancement

After completing any feature change or enhancement, update both `CLAUDE.md` and `README.md` to reflect the new behavior before considering the task done.

## Build & Run

All commands run from `src/BikeEnergyModel/`:

```bash
dotnet build                  # compile
dotnet run                    # run dev server (port printed at startup)
dotnet run --no-build         # skip rebuild
```

There are no automated tests yet. Build output goes to `bin/`, never commit it.

## Project Layout

```
src/
  BikeChooser.slnx              # solution file
  BikeEnergyModel/
    Controllers/HomeController.cs
    Models/          # RideInputModel, RideLeg, BikeInput, BikeResult, RideResultsViewModel
    Services/        # IEnergyCalculator, EnergyCalculator
    Views/Home/Index.cshtml
```

Bike configs are user-editable at runtime via a repeating section in the form (≥ 2 bikes required). `RideInputModel.Bikes` defaults to three presets (Racing Bike, E-Bike, E-Bike + Extender) but the user can add, remove, or modify any of them. Aerodynamic and environmental constants (CdA, ρ, η values) still live as `const` in `EnergyCalculator.cs`. The Crr dropdown options (Track / Tubular through Fat Bike, 0.003–0.015) are exposed as `BikeInput.CrrOptions` so the view can render them.

## Architecture

The app is a single-page form (`GET /` → show form, `POST /` → calculate and redisplay with results). The view model is `RideResultsViewModel`, which wraps `RideInputModel` (bound on POST) and a nullable `List<BikeResult>`. ASP.NET tag helpers generate `name="Input.FieldName"` attributes, so the POST action must accept `RideResultsViewModel`, not `RideInputModel` directly.

`EnergyCalculator` (singleton DI) does all the math:

1. **Leg-based model** — the ride is composed of one or more legs, each with its own distance, wind speed, wind direction, and signed elevation gain. Total ride distance is the sum of leg distances. Wind direction options are None / Headwind / DiagonalHeadwind / Crosswind / DiagonalTailwind / Tailwind, stored bike-relative; `ComputeAirspeed()` maps each to a bike-relative angle (0°/45°/90°/135°/180°) and decomposes the wind into parallel + perpendicular components: `v_air = sqrt((v_ground + v_wind·cos θ)² + (v_wind·sin θ)²)`, with `v_ground + v_wind·cos θ` clamped at 0. Diagonals collapse left/right because aero drag depends only on magnitude. Aerodynamic power uses `v_air² × v_ground`, not `v_air³`.
2. **Elevation** — per-leg signed elevation gain (positive = climb, negative = descent). The calculator sums `climbM = Σ max(leg.h, 0)` and `descentM = Σ max(-leg.h, 0)`, then `eGravityNet = max(m × g × (climbM − η_descent · descentM), 0)`. For balanced round trips this reduces to `m × g × h × (1 − η_descent)` (the previous formula); for one-way climbs it correctly charges the full `m × g × h`. For e-bikes, the motor covers climbing first, reducing the battery available for flat segments.
3. **Optimal wattage** — `battery_Wh / ride_time_hours`, clamped to [0, `Input.MaxMotorAssistWatts`]. Computed per e-bike config before the energy calculation; used as `P_motor_W` for that config.
4. **Motor duration** — `battery_remaining_after_climbing / P_motor_W` gives seconds of flat assist. Motor is active only when `target_speed ≤ v_cutoff` and battery has charge. Motor fraction is distributed proportionally across legs.
5. **Human energy** — per leg: `P_human × t_motor_on + P_pedal × t_motor_off`, then sum across legs and add elevation human cost.

## Physics Model Reference

Full derivation is in `docs/bike-energy-model-plan.md`. Key formulas:

- `P_rolling = C_rr × m_total × g × v_ground`
- `P_aero = 0.5 × CdA × ρ × v_air² × v_ground`
- `P_pedal = (P_rolling + P_aero) / η_drivetrain`
- `P_human = max(P_pedal − P_motor × η_motor, 0)` when motor active

All internal math uses SI units (m, kg, m/s, J, W). Imperial conversions happen at the `Calculate()` boundary.
