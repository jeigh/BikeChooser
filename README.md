# BikeChooser

A web app that answers a simple question: **for a given ride, which bike should I take?**

It compares any number of bike configurations (minimum two) — out of the box, a lightweight racing bike, an e-bike, and the same e-bike with an extra battery — and tells you which one requires the least human effort, measured in kilojoules. You can edit the defaults, add new bikes, or remove any of them.

## The Problem

Choosing between a racing bike and an e-bike isn't obvious. The racing bike is lighter and has less rolling resistance, but you're doing all the work. The e-bike has a motor, but it's heavier, runs gravel tires, and the motor cuts off above a speed threshold. Add a range extender battery and you get more motor time but even more weight. The answer depends on how far you're going, how fast, how windy it is, and how hilly the terrain is.

This app does the math.

## How It Works

The model computes the total mechanical power needed to maintain a target speed, then subtracts whatever the e-bike motor contributes (when eligible) to arrive at the human-supplied energy for each configuration.

The power equation accounts for three forces:

- **Rolling resistance** — proportional to total weight and tire type. The racing bike's skinny road tires at 100 PSI roll easier than the e-bike's gravel tires at 50 PSI, but the e-bike is also 10–19 lbs heavier.
- **Aerodynamic drag** — proportional to the cube of airspeed. Wind direction matters: a headwind/tailwind round trip always costs more total energy than calm air (the headwind penalty outweighs the tailwind savings due to the cubic relationship). Crosswinds are modeled separately.
- **Elevation** — heavier bikes pay more to climb. On a round trip the net elevation is zero, but total climbing still costs energy because descending doesn't perfectly recover what climbing spent.

For the e-bike configurations, the motor offsets some of the required power — but only when ground speed is at or below the motor cutoff threshold and the battery still has charge. Once the battery is depleted, the e-bike becomes a heavy bike with high-friction tires and no motor. The app computes exactly when that crossover happens.

## Inputs

### Ride parameters

| Input | What it controls |
|-------|-----------------|
| Target speed (mph) | Speed above the motor cutoff means the motor is dead weight |
| Motor cutoff speed (mph) | Class 1 = 20 mph, Class 3 = 28 mph, or any value for other setups |
| Max motor assist (watts) | Caps the optimal-wattage solver |
| Total elevation gain (feet) | Total climbing — penalizes heavier bikes |
| Rider weight (lbs) | Combined with bike weight for all force calculations |

### Ride legs (≥ 1, repeatable)

Each leg has its own distance, wind speed, and wind direction (None, Headwind, Tailwind, Crosswind). Total ride distance is the sum of leg distances.

### Bikes (≥ 2, repeatable)

| Input | What it controls |
|-------|-----------------|
| Name | Free-text label shown in the results table |
| Weight (lbs) | Combined with rider weight for rolling and elevation costs |
| Coefficient of Rolling Resistance | Dropdown of tire/bike-type presets, 0.003 (Track / Tubular) through 0.015 (Fat Bike) in 0.001 steps |
| Battery capacity (Wh) | 0 = no motor; > 0 = electric, used for motor-assist calculations |

## Output

For each configured bike, a single results table shows the total human-supplied energy in kilojoules, motor assist duration, remaining battery, and the optimal motor setting — the wattage that spreads the full battery evenly across the ride, which is what the calculator uses internally. The lowest-energy row is highlighted.

## Default Bike Configurations

| Config | Weight | Crr (label → value) | Battery |
|--------|--------|--------------------|---------|
| Racing Bike | 20 lbs | Road Bike → 0.005 | 0 Wh |
| E-Bike | 30 lbs | Gravel Bike → 0.007 | 250 Wh |
| E-Bike + Extender | 39 lbs | Gravel Bike → 0.007 | 500 Wh |

The e-bike defaults are modeled on a Trek Domane+ AL5 with the HyDrive system and an optional 250 Wh range extender battery. All three are starting points — edit, remove, or add bikes as needed.

## Tech Stack

ASP.NET Core MVC (C#). The physics calculations live in a service class; the UI is a single page with a form and a results table.

## Background

The mathematical model was designed collaboratively in conversation with Claude, then the application was vibe-coded using Claude Code. The full planning document is in the `docs` directory.
