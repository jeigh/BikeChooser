# Bike Energy Optimization Model — Plan

## Objective

Build an **ASP.NET Core MVC** web application (C#) that computes the **total human-supplied energy (in kilojoules)** required for a given ride across three bike configurations, and identifies the optimal choice.

The three configurations are:

| Config | Label | Weight | Tires | Motor | Battery |
|--------|-------|--------|-------|-------|---------|
| A | Racing Bike | 20 lbs (9.07 kg) | Road tires, 100 PSI | None | None |
| B | E-Bike | 30 lbs (13.61 kg) | Gravel tires, 50 PSI | 250W HyDrive hub, 40Nm | 250 Wh (900,000 J) |
| C | E-Bike + Extender | 39 lbs (17.69 kg) | Gravel tires, 50 PSI | 250W HyDrive hub, 40Nm | 500 Wh (1,800,000 J) |

The bike is a **Trek Domane+ AL5** (2022–2024 model year) with the HyDrive system. Battery capacities are manufacturer-rated at 250 Wh each (main + range extender). The Hyena app reports lower usable figures (~6.1 Ah and ~11.5 Ah), likely reflecting a reserve margin for battery longevity. The model uses the full manufacturer figures as the theoretical maximum available energy.

---

## Physics Model

### Power Required to Maintain Target Speed

At steady state on a given segment, the total mechanical power to maintain ground speed `v` is:

```
P_total = P_rolling + P_aero + P_grade
```

The power the rider (plus motor) must deliver at the pedals/drivetrain is:

```
P_pedal = P_total / η_drivetrain
```

#### Rolling Resistance

```
P_rolling = C_rr × m_total × g × v
```

- `C_rr` depends on tire type (see constants)
- `m_total = m_rider + m_bike` in kg
- `g = 9.81 m/s²`
- `v` = ground speed in m/s

#### Aerodynamic Drag

```
P_aero = 0.5 × CdA × ρ × v_air² × v_ground
```

- `CdA` = drag coefficient × frontal area (same for all bikes; see constants)
- `ρ` = air density (see constants)
- `v_air` = effective airspeed (depends on wind; see Wind Model below)
- `v_ground` = target ground speed

**Important:** drag *force* depends on airspeed², but *power* = force × ground speed. The formula uses `v_air² × v_ground`, not `v_air³`.

#### Grade Resistance

```
P_grade = m_total × g × grade × v
```

- `grade` = decimal slope (positive = uphill, negative = downhill)
- For small grades typical of mid-Michigan, `sin(θ) ≈ grade`

### Human Power

**Racing Bike (all segments):**
```
P_human = P_pedal
```

**E-Bike configs, when motor is active (ground speed ≤ `v_cutoff` AND battery has charge):**
```
P_human = P_pedal - (P_motor_setting × η_motor)
```

If `P_human` computes to less than zero (motor provides more than needed), clamp to zero — the rider coasts, and excess motor capacity is wasted.

**E-Bike configs, when motor is inactive (ground speed > `v_cutoff` OR battery depleted):**
```
P_human = P_pedal
```

### Total Human Energy

```
E_human = Σ (P_human_segment × t_segment)
```

Sum across all ride segments (outbound/return legs, motor-on/motor-off phases).

---

## Wind Model

Wind affects only the aerodynamic component. The model supports two wind scenarios.

### Scenario 1: Head/Tailwind (wind aligned with direction of travel)

The ride is split into two legs of equal distance.

- **Outbound (tailwind):** `v_air = v_ground - v_wind`
  - If `v_air < 0`, treat as 0 (wind faster than rider; pure tailwind push)
- **Return (headwind):** `v_air = v_ground + v_wind`

Due to the cubic-ish relationship between airspeed and aero power, the headwind penalty always exceeds the tailwind benefit. Wind always increases total energy on a round trip.

**Motor interaction:** Ground speed is what the motor cutoff checks, not airspeed. A strong tailwind may keep the rider's ground speed above `v_cutoff` (motor off when least needed), while the headwind leg keeps ground speed below `v_cutoff` (motor on when most needed). This is a favorable asymmetry for the e-bike. Note: this asymmetry is most pronounced with lower cutoff speeds (Class 1, 20 mph). At higher cutoffs (Class 3, 28 mph) or uncapped motors, wind is less likely to push ground speed past the threshold.

### Scenario 2: Crosswind (wind perpendicular to direction of travel)

A pure crosswind increases apparent wind magnitude symmetrically on both legs.

```
v_air = sqrt(v_ground² + v_wind²)
```

This applies identically to both outbound and return legs. The penalty is smaller than a head/tailwind of the same speed. At 15 mph ground speed with 10 mph crosswind, aero power increases ~20% vs. still air, compared to roughly doubling for a 10 mph headwind.

The motor is unaffected (ground speed doesn't change with crosswind).

### No Wind

If wind speed is 0, `v_air = v_ground` on all segments. This is the default/simplest case.

---

## Elevation Model

For a round trip, net elevation change is zero, but total climbing still costs energy because:

1. Heavier bikes pay more gravitational cost climbing
2. Descending does not perfectly recover climbing energy (braking, speed limits, drivetrain losses)
3. The motor helps on climbs (speed typically stays below `v_cutoff`) — this partially offsets the weight penalty for e-bikes

### Simplified Elevation Approach

Rather than simulating a terrain profile, use a single input: **total elevation gain** (feet, converted to meters internally). Model the net gravitational energy cost as:

```
E_gravity_net = m_total × g × h × (1 - η_descent_recovery)
```

- `h` = total elevation gain in meters
- `η_descent_recovery` = fraction of potential energy recovered on descents (see constants)

This net cost is added to the flat-ground energy calculation. It is higher for heavier configurations.

### Motor Contribution on Climbs

On climbing segments, speed typically stays below `v_cutoff`, so the motor is active. The motor's share of climbing energy reduces the human contribution for e-bike configs. The model accounts for this by treating climbing power as part of the overall P_pedal calculation where the motor offset applies.

For the simplified model, assume the motor is active for all climbing segments (reasonable for mid-Michigan grades at typical target speeds, especially with Class 3 or uncapped cutoffs).

---

## Battery Depletion Logic

The motor assist wattage setting determines how long the battery lasts:

```
battery_duration_hours = battery_energy_Wh / P_motor_setting_W
```

Compare to total ride time:

```
ride_time_hours = ride_distance_miles / target_speed_mph
```

**If `battery_duration ≥ ride_time`:** motor assists for the entire ride at the set wattage. Some battery energy remains unused.

**If `battery_duration < ride_time`:** motor assists for `battery_duration` hours, then shuts off. The remaining ride is human-powered only, carrying the dead weight of the e-bike.

### Optimal Motor Setting

The setting that minimizes human energy (assuming target speed stays ≤ `v_cutoff`) is:

```
P_motor_optimal = battery_energy_Wh / ride_time_hours
```

This spreads the full battery evenly across the ride. The app should compute and display this value alongside the user's chosen setting.

### Key Insight

If the full battery is consumed during the ride, total motor energy contribution is identical regardless of how the wattage is distributed over time. Distribution only matters when:
- The ride is too short to use the full battery (short ride → racing bike territory)
- Variable speed causes the motor to cut off at > `v_cutoff`, wasting assist window

---

## Constants

| Constant | Symbol | Value | Notes |
|----------|--------|-------|-------|
| Racing bike weight | `m_bike_racing` | 9.07 kg (20 lbs) | |
| E-bike weight | `m_bike_ebike` | 13.61 kg (30 lbs) | |
| E-bike + extender weight | `m_bike_ebike_ext` | 17.69 kg (39 lbs) | |
| Rolling resistance (road tires) | `C_rr_road` | 0.005 | 100 PSI skinny tires on rough pavement |
| Rolling resistance (gravel tires) | `C_rr_gravel` | 0.007 | 50 PSI gravel tires on rough pavement |
| Drag area (all bikes) | `CdA` | 0.40 m² | Comfortable upright-ish position, same for all configs |
| Air density | `ρ` | 1.225 kg/m³ | Sea level standard; Michigan is close enough |
| Drivetrain efficiency | `η_drivetrain` | 0.95 | 5% chain/gear losses |
| Motor mechanical efficiency | `η_motor` | 0.82 | Electrical-to-mechanical conversion loss in hub motor |
| Battery energy (single) | `E_batt_single` | 250 Wh (900,000 J) | Trek Domane+ AL5 main battery, manufacturer rated |
| Battery energy (double) | `E_batt_double` | 500 Wh (1,800,000 J) | Main + range extender |
| Descent energy recovery | `η_descent` | 0.85 | Fraction of gravitational PE recovered on descents |
| Max motor output | `P_motor_max` | 250 W | HyDrive hub motor maximum |
| Gravitational acceleration | `g` | 9.81 m/s² | |

---

## User Inputs (Variables)

| Input | Unit | Description |
|-------|------|-------------|
| Ride distance | miles (round trip) | Total distance of the ride |
| Target speed | mph | Constant speed the rider attempts to maintain |
| Motor assist wattage | watts (0–250) | Rider-selected motor output level |
| Motor cutoff speed | mph | Ground speed above which the motor provides no assist. Class 1 = 20 mph, Class 3 = 28 mph. Use a large value (e.g. 9999) for uncapped motors or for the racing bike config where it is irrelevant. |
| Wind speed | mph | Speed of wind; 0 for calm conditions |
| Wind direction | enum: `None`, `HeadTail`, `Crosswind` | Relationship of wind to direction of travel |
| Total elevation gain | feet | Cumulative climbing over the ride (not net; net is 0 for round trip) |
| Rider weight | lbs | Rider's body weight (converted to kg internally) |

---

## Unit Conversions (internal)

All physics calculations use SI units internally. Convert on input/output boundaries.

| From | To | Factor |
|------|-----|--------|
| miles | meters | × 1609.34 |
| mph | m/s | × 0.44704 |
| lbs | kg | × 0.453592 |
| feet | meters | × 0.3048 |
| Wh | J | × 3600 |
| J | kJ | ÷ 1000 |

---

## Computation Steps (Algorithm)

### Step 1: Derive Totals

For each of the three bike configs, compute:
- `m_total = m_rider_kg + m_bike_kg`
- `C_rr` (road for racing bike, gravel for both e-bike configs)
- Available battery energy (0 for racing bike, 900,000 J or 1,800,000 J for e-bikes)

### Step 2: Compute Segment Powers

Split the ride into segments based on wind scenario:

**No wind:** single segment, full ride distance, `v_air = v_ground`

**Head/Tailwind:** two segments of equal distance
- Outbound: `v_air = max(v_ground - v_wind, 0)`
- Return: `v_air = v_ground + v_wind`

**Crosswind:** single effective segment (symmetric), `v_air = sqrt(v_ground² + v_wind²)`

For each segment, compute:
- `P_rolling = C_rr × m_total × g × v_ground`
- `P_aero = 0.5 × CdA × ρ × v_air² × v_ground`
- `P_pedal = (P_rolling + P_aero) / η_drivetrain`
- Segment time = segment distance / v_ground

### Step 3: Add Elevation Cost

```
h_meters = elevation_gain_feet × 0.3048
E_gravity_net = m_total × g × h_meters × (1 - η_descent)
```

This is added to the total energy for each config. For e-bike configs, reduce by the motor's contribution to climbing (motor active on climbs):
```
E_gravity_human = E_gravity_net - min(E_motor_available_for_climbing, E_gravity_net × η_motor)
```

Note: the motor energy used for climbing reduces the battery energy available for flat segments.

### Step 4: Determine Motor Duration

For each e-bike config:
```
P_motor_effective = P_motor_setting × η_motor    // mechanical watts delivered
battery_energy_J = battery_Wh × 3600
motor_duration_s = battery_energy_J / P_motor_setting  // electrical draw rate
ride_time_s = ride_distance_m / v_ground
```

If `motor_duration_s ≥ ride_time_s`: motor assists full ride.  
If `motor_duration_s < ride_time_s`: motor assists for `motor_duration_s`, then off.

### Step 5: Compute Human Energy Per Segment

For each segment and each config:

**Racing bike:**
```
E_human_segment = P_pedal × t_segment
```

**E-bike (motor active portion of segment):**
```
P_human = max(P_pedal - P_motor_effective, 0)
E_human_motor_on = P_human × t_motor_on
```

**E-bike (motor depleted portion of segment):**
```
E_human_motor_off = P_pedal × t_motor_off
```

### Step 6: Sum Total Human Energy

```
E_human_total = Σ E_human_segments + E_gravity_human
```

Convert from joules to kilojoules for display.

### Step 7: Compute Optimal Motor Setting

```
P_motor_optimal = battery_energy_Wh / ride_time_hours
```

Clamp to [0, 250] range.

---

## Application Architecture (ASP.NET Core MVC)

### Project Structure

```
BikeEnergyModel/
├── BikeEnergyModel.csproj
├── Program.cs
├── Controllers/
│   └── HomeController.cs
├── Models/
│   ├── RideInputModel.cs
│   ├── BikeConfig.cs
│   ├── BikeResult.cs
│   └── RideResultsViewModel.cs
├── Services/
│   ├── IEnergyCalculator.cs
│   └── EnergyCalculator.cs
└── Views/
    └── Home/
        └── Index.cshtml
```

### Models

**`RideInputModel.cs`** — binds to the form. All fields are `double` or `string` with `[Required]` and `[Range]` validation attributes. Fields:

| Property | Type | Validation | Default |
|----------|------|------------|---------|
| `RideDistanceMiles` | double | Required, > 0 | — |
| `TargetSpeedMph` | double | Required, > 0 | — |
| `MotorAssistWatts` | double | Required, 0–250 | — |
| `MotorCutoffSpeedMph` | double | Required, > 0 | 20 |
| `WindSpeedMph` | double | Required, ≥ 0 | 0 |
| `WindDirection` | string | Required, one of "None", "HeadTail", "Crosswind" | "None" |
| `ElevationGainFeet` | double | Required, ≥ 0 | 0 |
| `RiderWeightLbs` | double | Required, > 0 | — |

**`BikeConfig.cs`** — represents one of the three bike configurations. Contains the constants for that config (weight, C_rr, battery capacity). Can be a simple POCO or record. The three instances are constructed in code, not from user input.

**`BikeResult.cs`** — result for a single config:

| Property | Type | Description |
|----------|------|-------------|
| `ConfigName` | string | "Racing Bike", "E-Bike", or "E-Bike + Extender" |
| `HumanEnergyKJ` | double | Total human-supplied energy in kilojoules |
| `MotorAssistDuration` | TimeSpan? | How long the motor ran, null for racing bike |
| `BatteryRemainingWh` | double? | Unused battery energy, null for racing bike |
| `OptimalMotorWatts` | double? | Setting that fully depletes battery over the ride, null for racing bike |
| `IsRecommended` | bool | True for the config with the lowest HumanEnergyKJ |

**`RideResultsViewModel.cs`** — wraps the input model and a `List<BikeResult>` so the view can redisplay the form with results below.

### Services

**`IEnergyCalculator.cs`** — interface with a single method:
```csharp
List<BikeResult> Calculate(RideInputModel input);
```

**`EnergyCalculator.cs`** — implements the physics model. This is where all the math from the Physics Model, Wind Model, Elevation Model, and Battery Depletion sections of this document lives. Registered in DI as a singleton (it has no state).

The calculator should:
1. Define the three `BikeConfig` instances internally (constants from the Constants table).
2. Convert all user inputs from imperial to SI.
3. For each config, compute P_rolling, P_aero, and P_grade per segment.
4. Apply motor offset where applicable (ground speed ≤ v_cutoff, battery has charge).
5. Handle battery depletion timing.
6. Sum human energy across segments, add elevation cost.
7. Compute optimal motor setting.
8. Flag the lowest-energy config as recommended.

### Controller

**`HomeController.cs`** — two actions:

```
GET  /  → Index()        → renders the form with empty results
POST /  → Index(model)   → validates, calls EnergyCalculator, renders form + results
```

On POST, if `ModelState.IsValid`, call `_calculator.Calculate(model)` and attach results to the view model. If validation fails, redisplay the form with validation messages. The form values should persist on POST so the user can tweak inputs without re-entering everything.

### View

**`Index.cshtml`** — single page, two sections:

**Top section: Input form.** All fields are text inputs (`<input type="text" />`). Wind direction uses a dropdown (`<select>`). A "Calculate" button submits the form via POST. Labels should include units (e.g., "Ride Distance (miles, round trip)"). Pre-populate the motor cutoff speed with 20 as a sensible default.

**Bottom section: Results (conditionally rendered).** Only shown when results exist. Display a simple table or card layout with one row/card per config:

| Column | Format |
|--------|--------|
| Config Name | text |
| Human Energy | X.X kJ |
| Motor Duration | H:MM or "—" |
| Battery Remaining | X.X Wh or "—" |
| Optimal Motor Setting | X.X W or "—" |

Highlight the recommended row (lowest human energy) with a distinct background color or a "★ Recommended" badge. Keep styling minimal — default Bootstrap from the MVC template is fine.

### Application Output

For each of the three configs, the results section displays:

1. **Config name** (Racing Bike / E-Bike / E-Bike + Extender)
2. **Total human energy** (kJ)
3. **Motor assist duration** (hours:minutes, or "—" for racing bike)
4. **Battery remaining** (Wh, or "—")
5. **Optimal motor setting** (watts — the setting that uses the full battery over the full ride, or "—")

The config with the **lowest human energy** is highlighted as the recommended choice.

---

## Future Considerations (Out of Scope for v1)

- **Human fatigue modeling:** sustainable power output decreases over multi-hour rides. A rider producing 150W in hour 1 might only sustain 120W by hour 4. This would affect whether the rider can actually maintain target speed, but adds significant complexity.
- **Variable terrain profiles:** instead of a single elevation gain number, model specific route profiles with grade changes. Would require GPX file parsing.
- **Temperature effects:** air density varies with temperature (~1.29 kg/m³ at 0°C vs ~1.16 kg/m³ at 35°C). Michigan seasonal variation could be meaningful.
- **Stop-and-go segments:** intersections, turns, and speed changes cost kinetic energy proportional to mass. Negligible for long rural rides but relevant for urban routes.
- **Torque-sensor behavior:** the Domane+ AL5 uses a torque sensor, meaning actual motor output may scale with rider effort rather than being a fixed wattage. The current model assumes fixed wattage presets via the Hyena app, which is how the rider has configured it.
