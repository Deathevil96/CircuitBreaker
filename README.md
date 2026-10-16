# Arrow Breaker

A minimal arrow-logic puzzle for Android and iOS, made in Unity 6 (URP 2D, new Input System).

- Design: `GDD.md`
- Closed testing on Google Play: `Docs/ClosedTesting.md`
- Store text, privacy policy, Data safety: `Docs/Store`
- Manual test plan: `Docs/QA-Checklist.md`
- Release history: `CHANGELOG.md`

## Open and run
Open the project in Unity 6000.3.x and press Play. The scene `Assets/_Game/Scenes/MainGame.unity` contains only a camera; the game builds itself at startup (`GameManager`).

In the Editor and development builds a **DBG** tab opens the debug panel (or press F1): jump levels, auto-solve, fake a new day, reset ad cooldowns, unlock achievements. Debug jumps never touch the real save.

## Tests
`CircuitBreaker > Run Self Tests` runs 300+ logic checks (shapes, generator, economy, streaks, achievements, saving, daily). They run in a separate save sandbox. `CircuitBreaker > Validate Levels 1-300` checks level solvability.

## Build for Android
`CircuitBreaker > Android >` has the builds (closed-test with test ads, AdMob test ads, release). Each one runs the self tests first. Details in `Docs/ClosedTesting.md`.

## Where things are
| Folder | Contents |
| :--- | :--- |
| `Scripts/Core` | game flow, economy, stats, achievements, saving, daily mode |
| `Scripts/Generation` | level generator, solver, shapes, difficulty catalog |
| `Scripts/Gameplay` | board and arrow views, camera, input |
| `Scripts/UI` | the whole interface, built from code |
| `Scripts/Ads` | ad service, test ads, AdMob adapter |
| `Scripts/Editor` | tests, build tools, store screenshots |

## Versions
All in `Scripts/Core/BuildInfo.cs`: app, save schema, generator, economy. Rules in `CHANGELOG.md`.
