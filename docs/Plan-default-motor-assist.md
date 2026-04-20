# Plan: Remove Motor Assist Input, Make It Output Only

## Summary

Remove the Motor Assist Watts input field entirely. The app should always compute and use the optimal motor setting for each e-bike configuration internally. The optimal value is displayed in the results as an output field per config.

## Current Behavior

The user manually enters a motor assist wattage (0–250W) as an input. The same value is used for both e-bike configurations. The results section separately displays an "Optimal Motor Setting" output.

## Desired Behavior

1. **Remove the Motor Assist Watts input field** from the form. The user no longer provides or sees this value before calculation.

2. **The calculator computes the optimal motor setting independently for each e-bike config:**
   - `optimal_watts = battery_energy_Wh / ride_time_hours`, clamped to [0, 250]
   - Since the two e-bike configs have different battery capacities (250 Wh vs 500 Wh), they will have different optimal values.
   - The racing bike config does not have a motor setting.

3. **Use the computed optimal value** as the motor assist wattage for each e-bike config's energy calculation. This always produces the lowest possible human energy for that config, because the full battery is spread evenly across the entire ride.

4. **Display the optimal motor setting per config in the results table** so the rider knows what wattage to set on the bike for that ride.

## Implementation Notes

### Model Change

- Remove `MotorAssistWatts` from `RideInputModel`.

### Service/Calculator Change

- In `EnergyCalculator.Calculate()`, compute optimal watts per e-bike config:
  ```
  ride_time_hours = ride_distance_miles / target_speed_mph
  optimal_watts = battery_Wh / ride_time_hours
  optimal_watts = Math.Clamp(optimal_watts, 0, 250)
  ```
- Use the computed optimal value as the motor wattage for that config's power calculations.

### Controller Change

- Remove any references to `MotorAssistWatts` from the input model binding.
- No changes to how results are passed to the view.

### View Change

- Remove the Motor Assist Watts text input and its label from the form.
- The Optimal Motor Setting column in the results table remains as-is — it now serves as the only place the user sees the motor wattage, and it's specific to each config.

## Edge Cases

- **Target speed above motor cutoff:** The motor never activates regardless of computed wattage. Optimal watts is still computed and displayed, but has no effect on human energy.
- **Very short rides:** Optimal watts may compute above 250W. Clamped to 250. Battery won't fully drain, some energy is stranded, and the racing bike may win.
- **Very long rides at low speed:** Optimal watts may compute to a very small number (e.g., 3W). This is mathematically correct. The results table should display it so the rider can assess whether their bike can actually deliver that level of assist. If it can't, the real-world result will be slightly worse than what the model shows.