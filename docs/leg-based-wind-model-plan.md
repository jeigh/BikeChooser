# Leg-Based Wind Model — Plan

## Summary

Refactor the single-segment wind model into a repeatable **leg-based** model. Each leg defines its own distance, wind speed, and wind direction relative to the rider. This replaces the current approach where wind direction implied ride geometry (a "HeadTail" enum that auto-split the ride into two equal halves).

The overall ride distance input is removed — total distance is the sum of all leg distances. Total ride distance becomes a computed output.

---

## Current State

The current model accepts:
- `RideDistanceMiles` — total round-trip distance (single value)
- `WindSpeedMph` — single wind speed for the entire ride
- `WindDirection` — one of `None`, `HeadTail`, `Crosswind`

Internally, `EnergyCalculator.BuildSegments()` creates one or two segments based on wind direction:
- `None` → 1 segment, full distance, `v_air = v_ground`
- `HeadTail` → 2 segments of equal distance (outbound tailwind, return headwind)
- `Crosswind` → 1 segment, full distance, `v_air = sqrt(v_ground² + v_wind²)`

This is limiting because:
- All legs must have the same wind speed
- The head/tail split is always 50/50
- Real rides have varying wind exposure across different road orientations
- There's no way to model a one-way ride or a loop with multiple wind directions

---

## Proposed Model

### Leg Definition

A **leg** represents a contiguous section of the ride with uniform wind conditions. Each leg has:

| Field | Type | Validation | Description |
|-------|------|------------|-------------|
| `DistanceMiles` | double | Required, > 0 | Length of this leg |
| `WindSpeedMph` | double | Required, ≥ 0 | Wind speed for this leg |
| `WindDirection` | enum | Required | Wind relative to rider on this leg |

### Wind Direction Enum

Simplified to four explicit values (no more implied geometry):

| Value | Effect on `v_air` | Description |
|-------|-------------------|-------------|
| `None` | `v_air = v_ground` | Calm air, no wind effect |
| `Headwind` | `v_air = v_ground + v_wind` | Wind opposes rider |
| `Tailwind` | `v_air = max(v_ground - v_wind, 0)` | Wind assists rider |
| `Crosswind` | `v_air = sqrt(v_ground² + v_wind²)` | Wind perpendicular to rider |

This is more explicit than the current model. The old `HeadTail` scenario is now expressed as two legs with the same distance and wind speed but opposite directions (one `Tailwind`, one `Headwind`).

### Example: Out-and-Back with 10 mph Wind

| Leg | Distance | Wind Speed | Direction |
|-----|----------|------------|-----------|
| 1 (outbound) | 15 mi | 10 mph | Tailwind |
| 2 (return) | 15 mi | 10 mph | Headwind |

### Example: Loop with Varying Wind

| Leg | Distance | Wind Speed | Direction |
|-----|----------|------------|-----------|
| 1 (north) | 8 mi | 12 mph | Headwind |
| 2 (east) | 6 mi | 12 mph | Crosswind |
| 3 (south) | 8 mi | 12 mph | Tailwind |
| 4 (west) | 6 mi | 12 mph | Crosswind |

---

## Changes Required

### Models

**Remove from `RideInputModel`:**
- `RideDistanceMiles`
- `WindSpeedMph`
- `WindDirection`

**Add to `RideInputModel`:**
- `List<RideLeg> Legs` — at least one leg required

**New model `RideLeg.cs`:**
```csharp
public class RideLeg
{
    [Required]
    [Range(0.001, double.MaxValue)]
    [Display(Name = "Distance (miles)")]
    public double DistanceMiles { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    [Display(Name = "Wind Speed (mph)")]
    public double WindSpeedMph { get; set; }

    [Required]
    [Display(Name = "Wind Direction")]
    public string WindDirection { get; set; } = "None";
}
```

**Add to `BikeResult`:**
- `TotalRideDistanceMiles` (double) — sum of all leg distances, displayed in results

### Services / EnergyCalculator

**Remove:** `BuildSegments()` method (no longer needed as a separate step).

**Replace with:** Iterate directly over `input.Legs`. Each leg becomes one segment:

```
foreach leg in input.Legs:
    v_air = compute from leg.WindDirection and leg.WindSpeedMph
    segment_distance = leg.DistanceMiles × 1609.34
    t_segment = segment_distance / v_ground
    P_rolling, P_aero, P_pedal computed per-segment (same formulas)
```

Battery depletion and motor duration logic remains the same — the motor-on fraction is still computed against total ride time (sum of all segment times), and distributed proportionally across segments.

**Derived values:**
- `totalRideDistanceM = sum of all leg distances in meters`
- `rideTimeS = totalRideDistanceM / vGroundMs`

The rest of the algorithm (elevation, battery duration, optimal motor setting) is unchanged — they depend on total distance and total time, not individual segments.

### Controller

No structural changes. The POST action still accepts `RideResultsViewModel` with the nested `Input` property. ASP.NET model binding handles indexed collections (`Input.Legs[0].DistanceMiles`, `Input.Legs[1].WindSpeedMph`, etc.).

### View (UI)

**Remove:** The single wind speed, wind direction, and ride distance fields.

**Add:** A repeating "Legs" section with:
- A table or card list showing each leg's three fields (distance, wind speed, wind direction dropdown)
- An "Add Leg" button that appends a new row (client-side JavaScript)
- A "Remove" button on each leg (minimum 1 leg required)
- Leg rows use indexed field names: `Input.Legs[0].DistanceMiles`, etc.

**JavaScript requirements:**
- Adding a leg clones the field template with incremented index
- Removing a leg re-indexes remaining rows so model binding doesn't break
- Default state: 1 empty leg row visible on page load

**Results section:**
- Add "Total Ride Distance" to the output (displayed once above the cards, or in each card)

### Validation

- At least one leg is required (custom validation or check in controller)
- Each leg independently validates distance > 0 and wind speed ≥ 0
- Wind direction must be one of the four enum values

---

## Migration Path

The old `HeadTail` scenario maps to:
```
Leg 1: distance = RideDistanceMiles / 2, wind = WindSpeedMph, direction = Tailwind
Leg 2: distance = RideDistanceMiles / 2, wind = WindSpeedMph, direction = Headwind
```

The old `Crosswind` scenario maps to:
```
Leg 1: distance = RideDistanceMiles, wind = WindSpeedMph, direction = Crosswind
```

The old `None` scenario maps to:
```
Leg 1: distance = RideDistanceMiles, wind = 0, direction = None
```

---

## Output Changes

**New response field in `BikeResult`:**

| Property | Type | Description |
|----------|------|-------------|
| `TotalRideDistanceMiles` | double | Sum of all leg distances |

This value is the same for all three bike configs (they all ride the same route), so it could alternatively live on `RideResultsViewModel` directly rather than being repeated per config. Either approach works — placing it on the view model is slightly cleaner.

---

## What Does Not Change

- All physics formulas (P_rolling, P_aero, P_pedal, elevation model)
- Battery depletion logic (still global across the full ride)
- Motor cutoff check (still based on target ground speed vs. cutoff)
- Optimal motor wattage formula
- Elevation model (still a single total elevation gain input)
- Rider weight, target speed, motor assist, motor cutoff inputs
- The three bike configurations and their constants
