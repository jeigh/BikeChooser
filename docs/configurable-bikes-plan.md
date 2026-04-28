# Configurable Bikes Plan

Make bike-specific values (name, weight, rolling resistance, battery) user-editable as a repeating section, mirroring the existing Ride Legs UI. Variable bike count (≥ 2 — comparing fewer than two bikes is pointless). Defaults preserve the current 3 presets.

## 1. New input model: `BikeInput`

Add `Models/BikeInput.cs` with user-friendly units (consistent with the rest of the form, which uses lbs/Wh):

| Field | Unit | Validation |
|---|---|---|
| `Name` | string | Required |
| `WeightLbs` | lbs | > 0 |
| `RollingResistance` | unitless (Crr) | one of the dropdown options below (0.003–0.015, step 0.001) |
| `BatteryCapacityWh` | Wh | ≥ 0 (0 = non-electric) |

## 2. `RideInputModel` change

Add `List<BikeInput> Bikes { get; set; }` defaulting to the current 3 presets (converted to lbs/Wh):

| Name | Weight (lbs) | Crr | Battery (Wh) |
|---|---|---|---|
| Racing Bike | 20.0 | 0.005 | 0 |
| E-Bike | 30.0 | 0.007 | 250 |
| E-Bike + Extender | 39.0 | 0.007 | 500 |

## 3. `EnergyCalculator` changes

- Drop the static `Configs` array; iterate over `input.Bikes` instead.
- Convert `WeightLbs → kg` and `BatteryCapacityWh → J` inside the calculator (same boundary-conversion pattern already used for rider weight).
- Drop the unused `MaxMotorWatts` field from `BikeConfig` (currently dead — the calculator uses `input.MaxMotorAssistWatts` for the global cap). Recommend deleting `BikeConfig` entirely and passing `BikeInput` directly — one fewer model to keep in sync.

## 4. View changes (`Views/Home/Index.cshtml`)

- New **"Bikes"** repeating section above "Ride Legs" — same table-with-add/remove pattern as legs. Columns: Name, Weight (lbs), **Coefficient of Rolling Resistance** (dropdown — see options below), Battery (Wh), remove button.

  Crr dropdown options (label → value):

  | Label | Crr |
  |---|---|
  | Track / Tubular | 0.003 |
  | Performance Road | 0.004 |
  | Road Bike | 0.005 |
  | Hybrid | 0.006 |
  | Gravel Bike | 0.007 |
  | Cyclocross | 0.008 |
  | Light Mountain Bike | 0.009 |
  | Mountain Bike | 0.010 |
  | Knobby / Off-road | 0.012 |
  | Plus Tire (2.8"–3.0") | 0.013 |
  | Fat Bike (4"+) | 0.015 |

  Bounds: 0.003 (lower-than-this is unrealistic outside lab tubulars) to 0.015 (fat-bike on pavement at moderate pressure — real tests range 0.013–0.018; 0.015 is a defensible middle). Step size 0.001 satisfies the precision requirement. Defaults map to existing presets: Racing Bike → "Road Bike" (0.005); both E-Bikes → "Gravel Bike" (0.007).
- JS add/remove mirroring the legs handler (re-index on remove).
- **Minimums** enforced client-side: remove button is disabled when only 1 leg remains, and when only 2 bikes remain (a comparison needs at least two).
- **Results display**: switch from a 3-column card grid to a single results **table** (one row per bike). It scales cleanly to N bikes; the recommended row gets a green highlight. Columns: Bike, Human Energy (kJ), Motor Duration, Battery Remaining, Optimal Motor.

## 5. Doc updates

Update `CLAUDE.md` (Architecture / Physics Model sections — the line *"three bike configs are constructed there as a static array"* will no longer be true) and `README.md`.

## Resolved decisions

- **Crr input**: dropdown labeled "Coefficient of Rolling Resistance". Options span 0.003–0.015 with friendly tire/bike-type labels (Road Bike = 0.005, Gravel Bike = 0.007, Fat Bike = 0.015, etc.). Server-side validation pins the value to that allowed set.
- **Min legs**: ≥ 1 (already implicit; keep enforced).
- **Min bikes**: ≥ 2 — comparing one bike to itself is meaningless. Remove button hidden/disabled when at the minimum.
