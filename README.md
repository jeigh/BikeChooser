# BikeChooser

A web app that answers a simple question: **for a given ride, which bike should I take?**

It compares three configurations — a lightweight racing bike, an e-bike, and the same e-bike with an extra battery — and tells you which one requires the least human effort, measured in kilojoules.

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

| Input | What it controls |
|-------|-----------------|
| Ride distance (miles, round trip) | Total distance — longer rides favor the e-bike if the battery lasts |
| Target speed (mph) | Speed above the motor cutoff means the motor is dead weight |
| Motor assist (watts) | How much motor power to use (0–250W) |
| Motor cutoff speed (mph) | Class 1 = 20 mph, Class 3 = 28 mph, or any value for other setups |
| Wind speed (mph) | Affects aerodynamic drag asymmetrically on round trips |
| Wind direction | Head/tailwind vs. crosswind vs. no wind |
| Elevation gain (feet) | Total climbing — penalizes heavier bikes |
| Rider weight (lbs) | Combined with bike weight for all force calculations |

## Output

For each of the three configurations, the app displays the total human-supplied energy in kilojoules, motor assist duration, remaining battery, and the optimal motor setting (the wattage that would spread the full battery evenly across the ride). The lowest-energy option is highlighted.

## The Three Configurations

| Config | Weight | Tires | Motor | Battery |
|--------|--------|-------|-------|---------|
| Racing Bike | 20 lbs | Road, 100 PSI | None | None |
| E-Bike | 30 lbs | Gravel, 50 PSI | 250W hub | 250 Wh |
| E-Bike + Extender | 39 lbs | Gravel, 50 PSI | 250W hub | 500 Wh |

The e-bike is a Trek Domane+ AL5 with the HyDrive system and an optional 250 Wh range extender battery.

## Tech Stack

ASP.NET Core MVC (C#). The physics calculations live in a service class; the UI is a single page with a form and a results table.

## Background

The mathematical model was designed collaboratively in conversation with Claude, then the application was vibe-coded using Claude Code. The full planning document is in the `docs` directory.
