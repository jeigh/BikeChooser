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
    Models/          # RideInputModel, BikeConfig, BikeResult, RideResultsViewModel
    Services/        # IEnergyCalculator, EnergyCalculator
    Views/Home/Index.cshtml
```

Physics constants (bike weights, Crr, CdA, ρ, η values) all live in `EnergyCalculator.cs`. The three bike configs are constructed there as a static array — they are not configurable at runtime.

## Architecture

The app is a single-page form (`GET /` → show form, `POST /` → calculate and redisplay with results). The view model is `RideResultsViewModel`, which wraps `RideInputModel` (bound on POST) and a nullable `List<BikeResult>`. ASP.NET tag helpers generate `name="Input.FieldName"` attributes, so the POST action must accept `RideResultsViewModel`, not `RideInputModel` directly.

`EnergyCalculator` (singleton DI) does all the math:

1. **Leg-based model** — the ride is composed of one or more legs, each with its own distance, wind speed, and wind direction (None, Headwind, Tailwind, Crosswind). Total ride distance is the sum of leg distances. `ComputeAirspeed()` maps each leg's wind direction to effective `v_air`. Aerodynamic power uses `v_air² × v_ground`, not `v_air³`.
2. **Elevation** — net gravity cost `= m × g × h × (1 − η_descent)`. For e-bikes, the motor covers climbing first, reducing the battery available for flat segments.
3. **Optimal wattage** — `battery_Wh / ride_time_hours`, clamped to [0, 250]. Computed per e-bike config before the energy calculation; used as `P_motor_W` for that config.
4. **Motor duration** — `battery_remaining_after_climbing / P_motor_W` gives seconds of flat assist. Motor is active only when `target_speed ≤ v_cutoff` and battery has charge. Motor fraction is distributed proportionally across legs.
5. **Human energy** — per leg: `P_human × t_motor_on + P_pedal × t_motor_off`, then sum across legs and add elevation human cost.

## Physics Model Reference

Full derivation is in `docs/bike-energy-model-plan.md`. Key formulas:

- `P_rolling = C_rr × m_total × g × v_ground`
- `P_aero = 0.5 × CdA × ρ × v_air² × v_ground`
- `P_pedal = (P_rolling + P_aero) / η_drivetrain`
- `P_human = max(P_pedal − P_motor × η_motor, 0)` when motor active

All internal math uses SI units (m, kg, m/s, J, W). Imperial conversions happen at the `Calculate()` boundary.
