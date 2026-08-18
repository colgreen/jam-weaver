# Zynthian validation

> Status: generic melodic MIDI routing is hardware-validated.

Zynthian has been exercised as the second physical target through the same USB
MIDI output path used for the original Circuit.

## Confirmed behavior

- Channel 1 receives an individual middle-C Note On/Off and stops cleanly.
- A fixed-seed generated pattern transformed to the middle musical role plays
  for multiple bars under the application's internal clock.
- The transport advances without a reported playback error.
- Stop and Panic leave the instrument silent.

This establishes that the core melodic path is not specific to the Circuit.

## Not yet established

- External-clock playback with Zynthian as the note destination.
- Program changes, CC mappings, multi-channel layers, engine selection, or
  Zynthian-specific configuration.
- Long-duration timing or behavior under load.

Related: [Circuit validation](circuit-validation.md).
