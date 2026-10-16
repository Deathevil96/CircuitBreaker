# Changelog

Version numbers live in `Assets/_Game/Scripts/Core/BuildInfo.cs`. Four numbers are tracked separately:

| Number | Meaning | Bump when |
| :--- | :--- | :--- |
| App version | what players see in the store | every release (also PlayerSettings.bundleVersion, Android version code, iOS build number) |
| Save schema | layout of saved data | save keys change (add a step in `SaveSystem.Migrate`) |
| Generator version | what each level number produces | generator, catalog, or shape changes alter levels |
| Economy version | rewards, caps, prices, ad rules | any number in `Economy.cs` changes |

## 1.0.0 - release (branch master)
Same game as 0.1.0 with all levels, minus the debug tools, level test and debug APK menu. Level window, loading screens and branding are unchanged.

## 0.1.0 - first store build: closed testing (save 3, gen 5, eco 2)
Store numbering starts here. Closed testing uses 0.x on branch `main`; the public release is 1.0.0 on branch `master`. Earlier entries below were internal milestones.
- Renamed to **Arrow Breaker: Circuit Puzzle**. Company: Misal Interactive. Package `com.misalinteractive.arrowbreaker`.
- Version code is a build counter (`ProjectSettings/BuildNumber.txt`), starting at 1 and growing by one per build.
- Studio logo splash replaces "Made with Unity". New bold-arrow launcher icon.
- Debug tools on `main` only: level test (levels 1-1000 in the background), visible auto-play through every level, jumps of 1, 10 and 100. All debug code lives in `GameManager.Debug.cs`, `DebugPanel.cs` and `LevelTester.cs`, which are removed on `master`.
- `LevelValidator` plays every generated level to the end by tapping and checks board rules (used by the debug test and the editor menu).
- Debug APK build menu (test ads plus debug tools), closed-test and release bundle menus.
- Level generation is now fully deterministic: a fixed number of attempts per board size, no time budget. A level number is the same board on every phone (generator version 5).
- "Preparing board..." shows while a big board is generated on a worker thread.
- Play Store assets collected in `PlayStore/` (screenshots, feature graphic, icon).

## Internal 0.4 - modes, shapes, records, professional UI, closed-test build (save 3, gen 4, eco 2)
- New monochrome minimal interface: main menu, top bar with Back and Restart, power-up bar, sheets, pages, line icons, system font.
- Six difficulty tiers: Easy, Medium, Hard (every 20), Super Hard (every 30), Ultra Hard (every 50), Nightmare (every 100, two hearts). Better rewards the harder the tier.
- Shapes from level 21: 29 silhouettes (heart, cat, fish, human, rabbit, owl, turtle, key, bird, flower, rocket, sailboat, paw and more). Grid fitted to each shape.
- Grid dots hidden except along the silhouette edge; they light up under moving arrows and fade.
- Leave to the menu and continue where you left (resume), including hearts and removed arrows. One slot each for classic and daily runs.
- Restart resets the board and counts as an attempt. A restart interstitial appears once per session on the 2nd or 3rd reset (never in tutorial levels).
- Modes: Daily Run with calendar and monthly badge, Freeplay (any difficulty), Overload (score attack).
- Records: day streak, win streak, most wins in a day, Overload best, totals, each with its date. 61 achievements including first-attempt awards for specific levels and Nightmare clears.
- Banner policy: opaque reserved strip at the bottom, power-up bar and board layout above it with a gap; the strip also covers the home-indicator inset.
- Android back button support, glow setting for low-end phones, shared line materials (no per-level leaks), board generation on a worker thread.
- AdMob adapter (test units) behind the `CB_ADMOB` define, plus Google consent (UMP). Stub test ads for closed testing.
- Build tooling: one-click Android setup, generated icons, upload keystore, closed-test and release bundles, store screenshot capture, 315 self tests.
- Store pack: listing text, privacy policy, Data safety answers, QA checklist.

## Internal 0.3 - monetization and economy (save 2, gen 3, eco 1)
- Rewarded-ad hint refill: 3 starting hints; when out, watch an ad for +2 hints. 5-minute cooldown that survives restarts.
- One banner slot, always visible at the bottom (test placeholder in the Editor and dev builds). No interstitials.
- Session gifts: first launch of each day gives +1 hint (up to 3). If hints are exhausted on a restart, +1 hint, with a 20-minute guard against restart farming.
- Out-of-hearts screen: one rewarded "+1 heart" ad per app launch, coin refill, or restart.
- Milestone levels: Hard every 20 levels (+1 hint), Expert every 50 (+2 hints, +1 ruler, +1 extract). Free hint stock caps at 3; overflow becomes coins.
- Ruler drops from perfect clears (chance falls with level, with a pity timer). Extract comes from perfect streaks, Expert levels and coins.
- Coin shop with prices that rise with level. Level-end reward lines on the win screen.
- Version tracking (`BuildInfo`), save migration, version shown in the level list and debug panel.
- Debug panel: empty/fill stock, reset ad cooldown, simulate new session.
- Level tiers renamed (Advanced, Master) so "Hard" and "Expert" mean the milestone levels. Boss-every-10 removed.
- Removed the per-level free hint.

## Internal 0.2 - look, shapes, devices (save 1, gen 2)
- Single thin neon line per arrow plus a small head. No plate or frame. Glow from bloom only.
- Rectangles for levels 1-20, silhouettes from level 21 (heart first).
- Arrowhead always points along the last segment, and the arrow moves exactly that way.
- Layout verified on phones and tablets (9:21 to 3:4), safe-area handling, re-fit on resize.
- Debug panel and `[CB]` console logging.

## Internal 0.1 - rebuild (save 1, gen 1)
- Full rewrite after audit: new Input System only, grid-space tap picking, reverse-insertion level generator with solver check, code-built UI, Cyber-Circuit theme.
