# Mobile Game Playbook: Building and Shipping with Claude Code

Based on a real project: **Arrow Breaker** (internal name Circuit Breaker), a tap-away arrow puzzle for Android, built solo with Claude Code and live on Google Play.

> **Tip for your friend:** paste this whole file into Claude at the start of your project and say "Use this as our playbook."

---

## 1. Tech stack used

| Area | Choice |
|---|---|
| Engine | Unity 6000.3.17, URP 2D, new Input System only |
| Language | C# |
| Editor control | UnityMCP (Claude drives the Unity Editor: scripts, scenes, menus, tests, builds) |
| Ads | Google AdMob (Google Mobile Ads plugin 11.6.0 via OpenUPM), UMP consent |
| Analytics | Firebase Analytics 14.1.0 + GA4, gated behind consent |
| In-app purchases | Unity IAP 5.5.0 (kept on a separate `store` git branch until tested) |
| Updates | Google Play in-app update API |
| Source control | Git, `master` for release, feature branches for risky work |
| Hosting | GitHub Pages for privacy policy, terms, app-ads.txt |
| Store | Google Play Console (closed test, then production), AAB builds, upload keystore |
| Browser automation | Claude in Chrome, used to fill and read Play Console / Firebase pages |

## 2. How the work was done (workflow)

1. **Write a design doc first** (`GDD.md`): modes, economy, UI direction, monetization. Claude works far better with one source of truth.
2. **Keep memory notes** so a new session knows the state (decisions, version codes, links, what the user still owns).
3. **Build in small commits.** One feature, one commit, clear message. Easy to roll back.
4. **Test after every change.** A built-in "Run Self Tests" menu (335 checks) runs before each release build.
5. **Show work with screenshots.** An offscreen capture tool renders the UI to PNG so you can review without a device.
6. **QA on a real phone** with a test APK (test ads, jump-to-level and coin cheats) installed via adb.
7. **Release builds are one menu click**: run tests, bump version code, sign, produce AAB.
8. **Ask Claude to review its own work** (`/code-review`, `/simplify`) before releases.

## 3. Architecture choices that paid off

- **Scene holds only a camera.** A `GameManager` boots at runtime and builds the board, UI, audio and ads from code. No scene or prefab merge conflicts, and Claude can edit everything as text.
- **Core logic in plain C#** (Board, Solver, Economy, Stats, Achievements, Save). No Unity dependency means it is fast to test and hard to break.
- **All tunable numbers in one file** (`Economy.cs`). Balancing the game is then a one-file change.
- **Versioned data** (`BuildInfo`: app, save, generator, economy versions) so old saves do not crash new builds.
- **Ads behind an interface** (`IAdProvider`): a stub for the Editor and tests, real AdMob for release. No ad SDK needed to develop.
- **Procedural levels**: generated per level index, checked by a solver so every level is winnable, never stored on disk.

## 4. Optimization techniques

**Performance**
- Level generation runs **off the main thread**, so there is no hitch when a level loads.
- Levels are generated on demand, so there are **no large level files** in the build.
- UI built once in code with a shared helper kit (`UiKit`), not hundreds of scene objects.
- Thin neon line graphics and a minimal palette: very cheap to draw, low overdraw.
- Only one banner, in a reserved opaque strip, so no layout reflow or overdraw over gameplay.
- Ad and analytics code compiled only where needed (`UNITY_ANDROID && !UNITY_EDITOR`, `CB_ADMOB` defines), keeping the Editor fast.

**Build size and stability**
- Only needed packages in the manifest; IAP kept on its own branch until ready.
- Self-tests run before every release build to catch regressions.
- Save sandboxing in tests so tests never touch real player data.

**Economy and retention**
- Playtesting found an exploit (uncapped combo coins let a player buy refills to level 800). Fixed with flat coin rewards per tier. **Always playtest the economy for loops.**
- Hearts scale with difficulty; hardest tier is one-mistake "iron man".
- Rewarded ads are capped (cooldowns, daily limit) so they stay a choice, not a nag.
- Interstitials have a minimum gap and never appear in the first levels.

## 5. What a mobile game should include

**Gameplay**
- A clear core loop learnable in under 30 seconds; first 5 levels forgiving
- Difficulty tiers and milestone levels
- Daily challenge, a free-play mode, and a score-attack mode for replay value
- One-time tip sheets when a new mechanic first appears
- Resume where you left off

**Progression and economy**
- Soft currency, boosters (hint, etc.), lives, achievements, stats
- Every number tunable in one place

**UX**
- Minimal, consistent visual style; safe-area handling for notches
- Settings, sound toggle, restart, clear fail and win screens
- Rating prompt after the player is happy (after level 10, asked at most twice)

**Monetization**
- Rewarded ads for hints, coins, hearts (player chooses)
- One banner, no forced ads beyond a rare interstitial
- Optional coin packs (IAP), with a clear refund policy
- Consent form (UMP) for EEA/UK before any ad or analytics

**Live ops**
- Analytics events: level_win, level_fail, hint_used, ad_rewarded, refill_used and so on
- In-app update prompt, so players move to fixed builds
- Optional: block sideloaded release builds

## 6. Publishing checklist (Google Play)

**Before you build**
- [ ] Unique package id (cannot change later)
- [ ] Upload keystore created, **backed up in two places**
- [ ] Real AdMob app id and ad unit ids (test ids only in dev)
- [ ] Version name and **version code** (see rule below)
- [ ] 512px icon, feature graphic 1024x500, phone screenshots, short and full description

**Legal and web**
- [ ] Privacy policy URL (live, mentions ads, analytics, purchases)
- [ ] Terms page if you sell anything
- [ ] `app-ads.txt` on your developer website, listed in AdMob
- [ ] Support email

**Play Console forms**
- [ ] Content rating questionnaire
- [ ] **Data Safety** form (must match what ads and analytics really collect; update it when you add an SDK)
- [ ] Ads declaration, target audience, news/COVID/government declarations
- [ ] Merchant account if you sell in-app purchases (bank and tax details)

**Release path**
1. Internal or closed test with real testers (Google requires a minimum tester period for new personal accounts)
2. Fix feedback, then promote to production
3. Staged rollout, watch crashes and reviews
4. Google review can take days; plan for it

**Rules learned the hard way**
- **Only release (AAB) builds bump the version code.** Version codes only go up, never reuse or waste them on test builds.
- Do not commit test-ad settings to `master`.
- Test APKs and Play AABs are different: a production build can refuse to run if not installed from Play.
- Without the right billing mode file, Unity IAP uses a FAKE store, so test purchases on a real device.
- Analytics stays off until consent is given.

## 7. Claude Code tips that saved time

- Give Claude **autonomy** for routine steps, but keep a rule that it must **never submit Play Console forms or releases without your OK**.
- Keep a **memory file** for decisions, ids, links and "what I still own". New sessions resume instantly.
- Keep a `Docs/Links.html` page with every console link in one place.
- When Claude says "done", ask for proof: test count, screenshot, or log line.
- Ask for **short, plain answers** if you skim; say what changed and what did not.
- Unity play mode stalls when the Editor is unfocused. Design tests to call game logic directly instead of waiting on frames.
- Long builds make the MCP call time out. That is normal; do not retry, wait for the output file.
- Use branches for risky features (IAP) so the release branch stays clean.

## 8. Suggested timeline for a new game

| Stage | What to do |
|---|---|
| Day 1 | Pick one core mechanic, write the GDD, prototype the loop |
| Week 1 | Procedural or hand-made levels, win/fail, save system, self-tests |
| Week 2 | UI polish, sound, economy, tutorial tips |
| Week 3 | Ads + consent, analytics, QA on a real phone |
| Week 4 | Store assets, policy pages, closed test |
| Then | Fix, staged production rollout, watch analytics, update every 2 to 3 weeks |

## 9. Copy-paste starter prompt for your friend's Claude

```
I am building a mobile game in Unity (or your engine). Use the attached playbook.
First: write a one-page design doc (core loop, modes, economy, monetization).
Rules: core logic in plain testable C#; all tunable numbers in one Economy file;
ads behind an interface with a stub for the Editor; consent before ads/analytics;
add a self-test menu and run it before each release; commit small; keep a memory
file of decisions and ids; never submit store forms or releases without asking me;
only release builds bump the version code. Explain things plainly and briefly.
```
