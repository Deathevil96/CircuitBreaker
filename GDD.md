# ARROW BREAKER (internal name: Circuit Breaker): Game Design Document v2.2

Working title: **Circuit Breaker** (project name). Genre: tap-away dependency puzzle (Arrows – Puzzle Escape / Arrow Escape family).
Platform: iOS + Android, portrait 9:16, 60 FPS. Unity 6000.3 (URP 2D Renderer), **new Input System only**.
Launch theme (**final**): **Cyber-Circuit Data Packets**. The theme system supports more later.

v2 supersedes the Downloads draft (`arrows_escape_unity_mcp_gdd.md`). Sections 1 and 2 record what changed and why.

---

## 1. Audit of v1 and decisions

| v1 problem | v2 decision |
| :--- | :--- |
| Used `Input.touchCount` / `Input.mousePosition`. The project is set to Input System only, so this throws every frame. | All input goes through `UnityEngine.InputSystem` (Touchscreen / Mouse). |
| Taps relied on `IPointerClickHandler`, a `Physics2DRaycaster` and per-cell `BoxCollider2D`s. Fragile, slow with 100+ arrows, and misses taps at small sizes. | **Grid-space picking.** A tap becomes a world position, then the nearest occupied cell within slop radius 0.62 wins. No colliders, no raycaster. |
| Generator filled the board randomly, then "repaired" cycles by flipping heads or **deleting arrows**. Boards were ugly and the result was unpredictable. | **Reverse-insertion generation.** Each new arrow is only placed if its exit ray is clear of everything already placed. Solvable by construction. A solver then verifies it and measures depth. |
| "Hint" returned the first movable arrow. | Hint picks the movable arrow that **unlocks the most other arrows**. |
| Scene wiring was a manual 30-step checklist. | Scene = Camera + one `Bootstrap` object. HUD, board and audio are built from code. Nothing to wire by hand, nothing to break. |
| Theme was a colour palette. | Theme is a full skin: background PCB traces, glow, packet-segment bodies, LED heads, spark VFX, synth SFX. |
| Snake movement was only specified for the exit. | Blocked bumps and the hint pulse also follow the arrow's own path. |

## 2. Gaps in the reference games, and our answers

Research on Arrows – Puzzle Escape (about 4.7★, large install base) and its clones shows the fun is the clean untangle and the snake-glide animation. The recurring complaints are ads, repetition, bad large-board UX and accidental taps while scrolling.

| Gap in reference games | Circuit Breaker answer |
| :--- | :--- |
| Ads after every level past ~L15. | **No forced interstitials.** Ads are only opt-in rewards (continue, bonus coins). Offline-first. |
| Accidental arrow moves when trying to pan or scroll. | **Tap vs drag threshold** (14 px / 0.25 s). A drag never fires a move. Optional "Confirm tap" setting for kids and accessibility. |
| Large boards cannot be zoomed out enough, and thin arrows are hard to tap. | Camera zoom-out always fits the whole board. **Fit Board** button. Tap slop and nearest-segment pick. |
| Late-game levels repeat or swing between trivial and huge. | Seeded generator with **minimum dependency depth per tier**. Best-of-N selection, so no near-trivial boards. Boss level every 10th. |
| Failure feels like a pure punishment (hearts then ad-wall). | A blocked move is a **free "bump"** at first. Hearts start at L6. Out of hearts offers Continue (+1 heart), coin refill or Restart. |
| Hint is a vague nudge. | Hint highlights the **best next move** and says why ("unlocks 3"). Hint is never charged if the board is already solved or stuck. |
| No sense of progress in a pure level list. | Packet-collection meta: every cleared level "uploads" data. Chapters of 10 levels with a boss node and a chapter reward. |
| Colour-only arrow identity. | Distinct colour **and** pattern (dashed, solid, double-line) so colour-blind players can tell arrows apart. Setting in v1.1. |
| Anxiety for relaxation players. | No timer by default. Optional Speedrun stopwatch records per-grid-size personal bests. |

## 3. Core rules

### 3.1 Board
- Grid `W × H`, origin bottom-left. `occ[x,y] = arrowId` or `-1`.
- An arrow is an ordered list of orthogonally adjacent cells (`cells[0]` tail, `cells[^1]` head, length ≥ 2) plus a `dir` (cardinal exit direction of the head).
- **The arrowhead points along its last body segment, and `dir` always equals that direction. Arrows never turn at the head, so what you see is exactly where it moves.** The ray is cast from the head cell along `dir`. The body bends anywhere behind the head.

### 3.2 Move resolution (tap on arrow A)
1. Cast ray: from `head + dir`, step by `dir` until out of bounds.
2. Any occupied cell, including A's own body, blocks.
3. **Clear:** free A's cells immediately (so chain-taps work), animate A gliding along its own body path then straight off-board at 22 u/s. Combo +1 if within 2 s of the previous valid tap. Win check.
4. **Blocked:** A glides forward to the blocker (snake-follow), sparks at the contact, both arrows flash red, A recoils. Lose 1 heart, combo resets.
5. **Frozen / locked arrows** shake and do not cost a heart.

### 3.3 Hearts
- Levels 1-5: **no heart loss** (tutorial, bumps only teach).
- Level 6+: 3 hearts. Zero hearts opens the Out of Hearts panel.

### 3.4 Mechanics by unlock level
| Level | Mechanic |
| :--- | :--- |
| 1 | Hint booster |
| 8 | **Extract** booster: tap any arrow to lift it out, ignoring blockers |
| 15 | **Ruler** booster: for 15 s, rays from every head show green (clear) or red (blocked, with blocker) |
| 20 | **Ice** arrows: counter 1-3, decrements each time another arrow escapes. Frozen until 0. |
| 35 | **Key / Lock** pairs: a locked arrow opens when its key arrow escapes |
| 50 | **Direction Toggle** tiles (v1.1) |

Booster stock and sources are defined in section 13 (monetization and economy).

### 3.5 Economy
- Coins: `10 + 2 × bestCombo`, double if cleared with all hearts, plus a chapter bonus.
- Refill hearts: 50 coins, or free Continue (+1 heart) once per level through a rewarded ad (stubbed).
- No energy, no waiting timers.

## 4. Level generation (reverse insertion)

Insertion order is the **reverse** of solve order. When arrow *k* is solved, only arrows inserted before *k* still block it, so a new arrow is placed only if its ray hits nothing already on the board.

```
repeat until fill target or attempt budget:
  pick random empty cell; grow a self-avoiding walk of length L ∈ [Lmin, Lmax]
      with ≤ Bmax bends (straight-biased)
  for head ∈ {end, start} (random order):  dir = head − neck   (last segment direction only)
      if ray(head, dir) crosses no existing cell and no own cell → accept
```
Post-passes:
1. **Solver** (`Solver.cs`): greedy waves. Removal is monotone, so greedy decides solvability exactly. Wave count = `dependencyDepth`.
2. Ice / Key / Lock are assigned only if the solver still clears the board, otherwise the assignment is reverted.
3. **Best-of-N** under a 300 ms budget: retry with `seed + k` until `depth ≥ minDepth` for the tier, keep the deepest. Depth targets are calibrated to what reverse insertion produces; `CircuitBreaker > Validate Levels` reports solvability (must be 0 failures) and depth-target misses (informational).
4. Inward-pointing arrows are inserted first and the required exit corridor shrinks as the board fills, which fills the centre and builds deep chains.

All levels come from `LevelCatalog.For(levelIndex)` and are deterministic per index. Infinite levels, no asset files.

| Tier | Levels | Grid | Len | Bends | Min depth | Fill |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| Tutorial | 1-5 | 5-6 | 2-3 | 0-1 | 2-3 | 55% |
| Easy | 6-20 | 7-9 | 2-4 | 1-2 | 4-6 | 60% |
| Medium | 21-60 | 10-13 | 2-5 | 1-3 | 6-9 | 65% |
| Hard | 61-150 | 14-18 | 2-6 | 2-4 | 10-16 | 70% |
| Expert | 151+ | 20-24 | 2-7 | 2-4 | 17+ | 72% |
| Boss | every 10th | +2 grid size | | | +1 depth | |

## 5. Theme: Cyber-Circuit Data Packets (FINAL)
- **Palette:** background `#05070D`. Arrow neons: cyan `#00F0FF`, magenta `#FF2E93`, lime `#3DFF7A`, amber `#FFC400`, violet `#8A5CFF`. Blocked flash `#FF3B5C`. Hint gold `#FFD23F`.
- **Board:** faint PCB traces with via rings in the background. Grid cells are copper-dim solder pads.
- **Arrows:** exactly one thin neon line (width 0.10 of a cell) plus a small arrowhead. Nothing else is drawn near an arrow: no halo, no thread, no end dots. The glow comes only from post-process bloom.
- **Board:** no plate or frame. Tiny dots mark the playable cells, and on shaped levels they trace the silhouette.
- **Feedback:**
  - Blocked: electric spark burst at the contact, amber then red pulse, low static thud.
  - Escape: light-streak, rising synth blip (pitch climbs with combo), tiny haptic.
  - Win: all remaining pads pulse, arpeggio.
- **Post FX:** URP Bloom on bright neon.
- All art is procedural (no external assets); audio is synthesized at startup.

## 6. UX
- Portrait. The safe play area is between the top HUD (12%) and the bottom bar (16%).
- **Top HUD:** hearts (left), level label (centre), coins (right), combo pop under the label.
- **Bottom bar:** Hint, Extract, Ruler, Fit Board, Restart.
- **Panels:** Level Clear (coins, depth, next), Out of Hearts (Continue / Refill / Restart), Level Select (paged grid).
- Tap vs drag separation, pinch zoom, mouse wheel zoom and drag pan for desktop testing.
- First-run tutorial overlay: pointing finger on the best-first move for L1 to L3.

## 7. Architecture (as built)
```
Assets/_Game/Scripts/
  Data/        LevelData.cs (ArrowDef, LevelData, ArrowKind)
  Generation/  LevelCatalog.cs, LevelGenerator.cs, Solver.cs
  Core/        Board.cs (pure logic), GameManager.cs, SaveSystem.cs, Bootstrap.cs
  Gameplay/    BoardView.cs, ArrowView.cs, CameraController.cs, BoosterController.cs
  Fx/          ProcSprites.cs, Sfx.cs, Vfx.cs, CyberTheme.cs
  UI/          GameUI.cs (builds the whole Canvas in code)
  Editor/      PreviewTools.cs (renders level contact sheets), tests
```
`Board` and `Solver` have no UnityEngine dependency beyond `Vector2Int`, so they are edit-mode testable.

## 8. Build plan
1. Data + generator + solver + tests, then render level previews to review difficulty.
2. Board view, arrow view, camera, input, VFX/SFX.
3. HUD, panels, boosters, save system.
4. Ice, Key/Lock, level select, tutorial overlay, polish.
5. Device build settings (portrait, icons, safe area).

## 9. Out of scope for v1.0
Daily Challenge, Monthly Trophy, Direction Toggle tiles, extra themes (Sushi, Vine, Rail, Yarn), IAP and real ads. All keep clean hooks.

## 10. Debug tools (Editor and Development builds only)
- **DBG button** (top-left) or **F1** opens the panel: live stats (level, grid, arrows left, depth, seed, generation ms, hearts, combo, taps, save values, FPS), a rolling event log, and buttons: Reset Save, Reload, Prev/Next, ±10 levels, Auto-Solve, Force Win, Force Fail, +500 Coins, Unlock All, Copy Info.
- Debug level jumps are **forced loads**: they never change saved progress, coins or boosters.
- Every event is also logged to the Unity console with the `[CB]` prefix (LOAD, TAP, ESCAPE, BUMP, WIN, FAIL).
- Menu items: `CircuitBreaker > Reset Save Data`, `CircuitBreaker > Validate Levels 1-300`.

## 11. Screen sizes and safe areas
- **Target range:** portrait from 9:21 phones (about 0.43 aspect) to 3:4 tablets (0.75) and near-square foldables.
- **Verified sizes:** 750×1334, 1080×1920, 1080×2400, 1080×2520, 1170×2532, 1290×2796, 1536×2048, 1668×2388, 1840×2208, 720×1280.
- **UI:** Canvas Scaler 1080×1920 reference, match 0.5. The bottom bar splits the width into fifths (never clips), modal panels cap at 95% of screen width, and the Level Select grid scales down on narrow screens. Everything sits inside `Screen.safeArea` (notch, home indicator). Implausible safe-area values are ignored (the Editor reports stale ones after a resize).
- **Camera:** the board is fitted into the area between the HUD and the booster bar, including cutout insets. It re-fits automatically when the aspect or safe area changes (rotation, foldable, window resize) and keeps the zoom level.
- **Orientation:** portrait only.


## 12. Board shapes (superseded by section 16 where it differs)
- **Levels 1-20:** plain rectangles, slightly taller than wide (for example 5x6 up to 11x13) so they fill the portrait screen.
- **From level 21 (`ShapeLibrary.ShapeStartLevel`):** the playable area is a silhouette, cycling Heart, Star, Circle, Diamond, Plus, Hexagon, House, Ring, Triangle. Level 21 is the heart.
- Arrows only exist on cells inside the silhouette. Cells outside it are empty space, so an arrow whose ray crosses outside cells still escapes, and dots are drawn only for playable cells.
- Shaped boards use a finer grid (1.75x the tier size, more for thin shapes such as the star, capped at 32) and a higher fill (up to 88%), so the outline reads clearly.
- Solvability is unchanged: reverse insertion with the mask applied, then the solver check.


## 13. Monetization and economy

Principles: the game must stay fair and fun first. No interstitial ads, ever (the reference game was criticised for ads after every level). Ads are opt-in rewards plus one banner. Everything below is tuned in `Economy.cs` (one file); bump `BuildInfo.EconomyVersion` after any change.

### 13.1 Ads
| Placement | Type | Rule |
| :--- | :--- | :--- |
| Bottom of screen | Banner | Always visible, outside the play area (the camera and booster bar leave room for it). A Remove-Ads purchase (`SaveSystem.AdsRemoved`) hides only the banner. |
| Out of hints | Rewarded | +2 hints. 5-minute cooldown, stored as a UTC timestamp so closing the app does not reset it. |
| Out of hearts | Rewarded | +1 heart, once per app launch. |
| Anywhere else | none | No interstitials. No ad is ever forced. |

The game talks to ads only through `IAdProvider` (`Ads/AdService.cs`). The Editor and development builds use `StubAdProvider` (fake banner and a 3-second test ad). A release build with no SDK provider shows no ads and grants no ad rewards, so a missing SDK can never hand out free items.

### 13.2 Power-ups
| Power-up | Unlock | Start | Free cap | Free sources | Coins (L1 / L100) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| Hint | L1 | 3 | 3 | daily bonus, restart gift when empty, rewarded ad (+2), Hard level (+1), Expert level (+2) | 30 / 60 |
| Ruler | L15 | 1 (gift) | 3 | perfect-clear drop, Expert level (+1) | 60 / 120 |
| Extract | L8 | 1 (gift) | 2 | perfect streak, Expert level (+1) | 120 / 240 |

- Coins can buy up to cap + 2. Free rewards never exceed the cap; the overflow converts to coins (hint 15, ruler 25, extract 50) so nothing is wasted.
- **Why Extract is rare:** it removes any arrow, so it is the most powerful item. It is never available from ads, only from skilled play (perfect streaks), Expert levels or coins.
- **Why Ruler is a drop:** it is a convenience, so it comes randomly from perfect clears.

### 13.3 Difficulty tiers and milestone levels
Every level has a tier. Early levels are Easy (1 to 20) and then Medium. Special levels recur along the way and are tougher than their neighbours:

| Tier | Where | Board | Rewards (free stock caps apply) |
| :--- | :--- | :--- | :--- |
| Easy / Medium | normal levels | scales with level | coins only |
| Hard | every 20th level | +2 size, longer arrows | 1 hint, 25 coins |
| Super Hard | every 30th | +3 size, deeper dependencies | 2 hints, 40 coins |
| Ultra Hard | every 50th | +4 size | 2 hints, 1 ruler, 1 extract, 60 coins |
| Nightmare | every 100th | +5 size, more ice and keys, **2 hearts** | 3 hints, 2 rulers, 1 extract, 150 coins |

On shared multiples the higher tier wins (100 is Nightmare, 150 Ultra Hard, 60 Super Hard). The HUD shows the tier as dots (1 to 6) and a label; a toast at the start of the level says what clearing it earns.

### 13.4 Getting harder to earn with progress
- Ruler drop chance on a perfect clear: 22% at L1, falling about 0.14% per level to a floor of 5% (L15 20%, L100 8%, L300 5%). Pity timer: guaranteed after 10 + level/30 dry perfect clears.
- Extract streak: 5 perfect clears in a row at the start, rising to 10 by about L300.
- Coin prices scale up to 2.5x with level.
- Simulated for a perfect player, rulers per 100 levels fall from 25 (L15-50) to 9 (L201-300). A player who never clears perfectly gets about 2 rulers and 2 extracts per 100 levels.

### 13.5 Session gifts
- First launch of a calendar day: +1 hint (up to the cap of 3).
- Any restart with 0 hints: +1 hint, at most once per 20 minutes (`SessionGiftMinGapMinutes`), so quitting and relaunching is not an infinite hint tap.
- Then the rewarded ad is the next source (5-minute cooldown).

### 13.5.1 Hearts
Levels 1-5 never cost hearts. From level 6, three hearts per level; at zero the player can watch one ad for +1 heart (once per launch), pay 50 coins to refill, or restart.

### 13.6 Before shipping (must-do)
- Plug in a real SDK behind `IAdProvider` (AdMob or LevelPlay) and use test ad units until release.
- Consent: UMP/GDPR consent form, iOS App Tracking Transparency prompt, and child-directed settings if the game targets under-13s.
- A Remove-Ads in-app purchase (flag already in the save) and a Restore Purchases button.
- Suggested but not built: a "double coins" rewarded ad on the win screen (optional, never required).

## 14. Versioning
Four numbers, all in `Core/BuildInfo.cs` and documented in `CHANGELOG.md`: app version, save schema, generator version, economy version. The save stores which generator and economy versions it last saw, and `SaveSystem.Migrate` upgrades older saves at startup. The version string is shown at the bottom of the level list and in the debug panel.

**Rule:** changing the generator, shape or catalog changes what level numbers produce, so bump the generator version. Changing any number in `Economy.cs` bumps the economy version. Releasing bumps the app version, Android version code and iOS build number together.


## 15. Game modes
| Mode | What it is | Rewards |
| :--- | :--- | :--- |
| Levels (classic) | The endless ladder. One board per level number, solvable by construction. | coins, items, achievements, progress |
| Daily Run | One board per calendar day, same for everyone, tier rotates Medium / Hard / Super Hard. Calendar shows cleared days. Past days of the current month can be played to catch up. Clearing every day of a month earns the monthly badge. | first clear: 1 hint + 30 coins; badge: 200 coins + 2 hints |
| Freeplay | Pick any of the six tiers; a fresh random board every time. | none (practice), counts toward totals and achievements |
| Overload | Score attack. 45 s clock that drains faster each board (+12% per board). Arrows score 10 x multiplier (combo). Board clear: +8 s and 100 x board number (+100 for no bumps). A bump costs 4 s. No hearts, no power-ups. | coins = score / 200, personal best with date |

Research basis: the reference games offer a daily puzzle, a monthly trophy for clearing every daily, an endless mode, a score mode with combos and rising pressure, and difficulty tiers up to Nightmare. All are re-themed here under new names.

## 16. Shapes
- Levels 1 to 20 are rectangles (slightly taller than wide).
- From level 21 each level uses a silhouette, in this order: Heart, Cat, Star, Fish, Human, Moon, Butterfly, House, Rabbit, Rocket, Circle, Owl, Tree, Crown, Turtle, Key, Bird, Flower, Diamond, Penguin, Sailboat, Paw, Sun, Mushroom, Plus, Bolt, Hexagon, Ring, Triangle (29), then the cycle repeats.
- Shapes are unions of ellipses, boxes, capsules and polygons in a unit square, then fitted to their tight bounding box so the grid is filled. A self test checks each shape is one connected piece.
- **Grid reveal:** only cells on the silhouette's edge keep a visible dot. Every other dot is hidden and lights up briefly under a moving arrow.

## 17. Interface principles
- Monochrome: near-black, white, grey, one muted red for danger. The arrows carry all the colour.
- One stroke weight for line icons, 2 px hairline borders, one corner radius, system font (SF / Roboto).
- Layout groups everywhere, so spacing is consistent and nothing is hand-placed.
- Top bar: Back (to menu, saving the board), title with difficulty dots, Restart. Second row: hearts and coins. Bottom: Hint, Ruler, Extract, Fit.
- Menu: Continue (resumes the saved board), Daily Run, Freeplay, Overload, Levels, Records, Settings.
- Sheets for decisions (win, out of hearts, get more, confirm), full pages for lists (levels, records, daily, settings), and a top toast for achievements.
- Android back button closes overlays, then leaves the board, then asks before exiting.

## 18. Records and achievements
- **Records (each with its date):** best day streak, best win streak, most wins in one day, Overload best. Also totals: wins, perfect clears, first-attempt wins, highest level.
- **Day streak:** consecutive calendar days the game was opened. **Win streak:** consecutive wins without running out of hearts.
- **First attempt:** a level won without any restart or failure on it. Resuming does not count as an attempt.
- **Achievements (61):** reach levels 10 to 1000; first-attempt wins on specific levels (10, 25, 50, 100, 150, 200, 250, 300, 400, 500) and first-attempt streaks; Hard, Super Hard and Ultra Hard totals; Nightmare clears (1, 3, 5, 10, 25) and first-attempt Nightmare clears; win and day streaks; win and perfect totals; Daily Run, monthly badges, Overload scores, Freeplay. Each pays coins and sometimes hints.

## 19. Leaving, resuming, restarting
- Leaving to the menu saves the board (removed arrows, hearts, bumps). Continue restores it exactly. One slot for classic and one for Daily Run, so one never wipes the other. Replays and debug boards are never saved.
- Restart resets the board, counts as an attempt (if anything had happened), and clears the saved run.
- **Restart ad:** one interstitial per app session, shown on the 2nd or 3rd reset (random), never in levels 1 to 5, respecting Remove-Ads. Hearts-out "+1 heart" ad is once per session; hint ad has a 5 minute persisted cooldown.

## 20. Platform and release
Portrait Android (min 7.1, ARM64, IL2CPP, AAB) and iOS. Closed-test builds ship test ads. See `Docs/ClosedTesting.md`. Banner policy: the banner never overlaps gameplay or controls (opaque reserved strip, content laid out above it with a gap). Versions in `BuildInfo.cs`.
