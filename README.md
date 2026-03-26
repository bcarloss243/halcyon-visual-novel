# Halcyon Academy

A narrative RPG set in a retrofuturistic, Venice-style flooded New Orleans enclosed under a dome called the Cloche.

You play as **Molly**, a student whose atmospheric weather sensitivity — inherited from her paternal grandmother and misdiagnosed her entire life — is tracked through the game's central mechanic: the **Pressure Gauge**. The mass-prescribed wellness product everyone relies on (Vapeur) doesn't work on her. The game is about discovering why.

## Status

**Phase 1: Core Loop Lock** (in progress)

What's working:
- Pressure Gauge system (Art Deco brass instrument, 0–100, zone thresholds, visual feedback)
- Daily activity selector (7 activities wired to pressure changes)
- Day cycle manager

What's next:
- Ink dialogue integration
- Activity scene buildout
- Cloche map (parallax 2D, perspective camera)

## Tech Stack

| Purpose | Tool |
|---|---|
| Engine | Unity 2022.3 LTS, Universal 2D |
| Dialogue | Ink by Inkle |
| Art — backgrounds | Midjourney with `--sref` |
| Art — characters | Scenario.gg + manual cleanup |
| Art — compositing | Photoshop |
| Code + design | Claude / Claude Code |
| Version control | Git / GitHub (this repo) |

## Project Structure

```
Assets/
├── Scripts/
│   ├── Systems/        PressureSystem, DayCycleManager, ActivityDefExtensions
│   ├── UI/             PressureGaugeUI, DailyScheduleUI, ActivityButtonUI
│   └── Debug/          GaugeDebugController
├── Data/
│   ├── Activities/     ActivityDef ScriptableObjects (7)
│   └── Config/         ZoneConfig asset
├── Prefabs/
│   └── ActivityButton
├── Art/
│   └── Gauge/          Gauge face, needle, center cap, zone segments
└── Scenes/
```

## Running the Project

1. Open in **Unity 2022.3 LTS**
2. Open the main scene
3. Play — click activity cards to see pressure changes on the gauge

## Design References

- **Structural reference:** Citizen Sleeper (daily cycle + resource pressure + narrative)
- **Visual benchmark:** Jen Zee's work on Hades
- **Art direction:** Painterly retro-futurist comic illustration — Studio Ghibli softness meets French bande dessinée linework, atomic-age pastel grittiness

## Private Repository

This repo is private. The game, its world, characters, and mechanics are original creative work in active development.
