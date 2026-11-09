# Game Dev Playbook: Code, Optimization, Legal and Technical (2D and 3D, Unity + Claude Code)

Apply to any Unity mobile game, 2D or 3D. Items marked (used) were done in a shipped game (Arrow Breaker, Android, live on Google Play). Items marked (recommended) are standard practice, not yet proven in that game. Do not assume the render pipeline: check the project (URP, HDRP, Built-in, 2D or 3D) before suggesting settings.

---

## 1. Professional code practices

- **Separate logic from Unity (used).** Core rules in plain C# classes (no MonoBehaviour): board, solver, economy, stats, save. Fast to unit test, easy to reason about, hard to break.
- **One config file for every tunable number (used).** Rewards, costs, cooldowns, difficulty curves in one `Economy`/`Config` class. Balancing is then a one-file change.
- **Versioned save data (used).** Keep app, save, generator and economy version numbers; migrate old saves instead of crashing. Never change a save format without a migration.
- **Interfaces around third-party SDKs (used).** `IAdProvider` with a stub for Editor/tests and the real SDK for release. Same for analytics and IAP.
- **Compile defines for platform and build type (used).** `UNITY_ANDROID && !UNITY_EDITOR`, plus custom ones (test ads vs production). Test builds can never ship test settings by accident.
- **Self-test menu run before every release build (used).** 335 checks (rules, saves, economy, generators). Release build refuses to proceed if tests fail.
- **Test sandbox for saves (used).** Tests write to a throwaway save prefix, never the real player data.
- **Deterministic generation (used).** Procedural content from a seed + level index, validated by a solver so every level is winnable. No stored level files.
- **Small commits, feature branches for risky work (used).** Keep release branch clean; IAP lived on its own branch until tested.
- **Assembly definitions (recommended).** Split code into asmdefs (Core, Gameplay, UI, Platform). Faster compile, enforced dependencies.
- **Events or small services, not singletons everywhere (recommended).** Avoid `FindObjectOfType` and hidden global state.
- **No magic strings (recommended).** Constants or enums for event names, save keys, tags, layers.
- **Fail loudly in dev, quietly in release (recommended).** Assertions and logs in dev; try/catch and safe defaults around save load and SDK calls in release.
- **Strip logs in release (recommended).** `Debug.Log` costs time and allocations on device; wrap in a conditional attribute.

## 2. Optimization techniques

### CPU and memory
- **Do heavy work off the main thread (used).** Level generation ran on a background thread; no frame hitch. (Unity API calls must stay on main thread; pass plain data.)
- **Generate instead of store (used).** No large content files in the build.
- **Build UI once, reuse (used).** Created in code through a shared helper kit; no per-frame rebuilds.
- **Avoid per-frame allocations (recommended).** No `new` lists, LINQ, string concatenation, or boxing in `Update`. Cache components and references. Use `StringBuilder`, pre-sized lists, `NonAlloc` physics calls.
- **Object pooling (recommended)** for bullets, effects, enemies, floating text, UI rows.
- **Do not run logic every frame if it is not needed (recommended).** Timers, events, coroutines with cached waits, or lower-frequency ticks.
- **Cap and set frame rate (recommended).** `Application.targetFrameRate` 60 (30 for idle menus), `vSyncCount = 0` on mobile.
- **Garbage collector (recommended).** Use incremental GC; keep GC spikes out of gameplay.
- **Profile before optimizing (recommended).** Unity Profiler, Memory Profiler, Frame Debugger, and profile on a real mid-range device, not the Editor.

### Rendering (2D and 3D)
- **Cheap visuals by design (used).** Thin line art, minimal palette, low overdraw.
- **No ad or UI overdraw over gameplay (used).** One opaque reserved banner strip.
- **Batching (recommended).** SRP Batcher (URP/HDRP), GPU instancing, static batching for static meshes, sprite atlases for 2D, shared materials.
- **Reduce draw calls and overdraw (recommended).** Combine meshes, atlas textures, avoid large transparent layers, avoid full-screen post effects on mobile.
- **3D specifics (recommended).** LODs, occlusion culling, baked lighting over realtime lights, 1 realtime light max on mobile, simple shadows or blob shadows, mesh and texture budgets per scene, avoid Standard-shader-heavy materials on low-end devices (use simple Lit/Unlit).
- **Textures (recommended).** ASTC compression on Android, power-aware sizes, mipmaps for 3D, disable Read/Write, max size per platform, crunch compression for size.
- **Audio (recommended).** Compressed Vorbis for music (streaming), ADPCM/short clips decompress-on-load for SFX, mono for SFX.

### Build size and startup
- **Only needed packages in the manifest (used).** Remove unused modules.
- **IL2CPP + ARM64 + strip engine code (recommended).** Required for Play (64-bit). Use Managed Stripping Low/Medium and a `link.xml` for reflection-dependent SDKs.
- **Addressables or asset bundles for big content (recommended).** Keep the base AAB small.
- **Check the Editor build report (recommended).** Find the largest assets and delete or compress them.
- **Android App Bundle (AAB) (used).** Play serves per-device splits.

### Battery, heat, network
- **Lower frame rate on static screens (recommended).**
- **Batch analytics events (used by Firebase).** Do not fire network calls per frame.
- **Do not load ads while gameplay is rendering heavy frames (recommended).** Preload at safe moments (menu, win screen).

### Economy and game design optimization (used)
- **Playtest the economy for loops.** Uncapped combo coins let a player buy refills to level 800; fixed with flat rewards per tier.
- **Cooldowns and daily caps on rewarded ads (used).** A choice, not a nag.
- **Interstitials: minimum gap, never in the first levels (used).**
- **Scale lives with difficulty (used).**

## 3. Legal checklist

| Item | Details |
|---|---|
| Privacy policy | Live public URL. Must name ads, analytics, purchases, data collected, retention, contact email, children's policy. Update whenever you add an SDK. |
| Terms of service | Needed if you sell anything: purchase terms, refund policy, governing law, seller identity. |
| Refund policy | Decide it in writing. Google refunds within 48 h regardless; consumed virtual currency is usually non-refundable after. |
| Consent (GDPR/UK/EEA) | Use Google UMP (CMP) before any ad or analytics. Collect nothing until consent is given (used: Firebase collection OFF in manifest until UMP consent). |
| COPPA / children | Decide the target audience honestly. If children are included, ads and analytics rules get much stricter (Families policy). |
| Data Safety form (Play) | Must match what your SDKs really collect (AdMob, Firebase). Re-check after every SDK add or update. |
| Ads declaration | Declare that the app contains ads. |
| app-ads.txt | Host on your developer website root, link the site in Play listing; prevents ad fraud and lost revenue. |
| Content rating | IARC questionnaire in Play Console; be truthful. |
| Trademark and assets | Do not use another game's name, icon style, audio or art. Check fonts, SFX, music and AI-generated assets for licence terms; keep a credits and licence file. |
| Open-source licences | List third-party SDK and package licences (Unity, Firebase, AdMob, plugins); include a credits screen if required. |
| Developer identity | Play requires verified identity; personal accounts have a closed-test minimum (testers and days) before production. Business accounts need D-U-N-S. |
| Tax and payments | Merchant account, bank and tax details needed before selling IAP. You own this; Claude must not fill it in. |
| Store fees | Google takes 15% (first $1M) or 30%; some regions add billing fees. Price accordingly. |
| Regional rules | Some regions restrict stores or loot-box style mechanics (hide shop in EEA/UK/CH if unsure; check local gambling-like rules). |
| Gambling-style mechanics | Random paid rewards (loot boxes) need legal review per country. |

## 4. Technical publishing checklist (Google Play)

**Project settings**
- Unique package id (cannot change later)
- Version name (human) and **version code** (integer, only goes up; bump only on release AAB builds, never on test APKs)
- Min SDK per Unity default, Target SDK meeting current Play requirement
- IL2CPP, ARM64 (and ARMv7 if you still support it), AAB output
- Keystore: **upload keystore created once, backed up in two places** (loss means you cannot update the app). Opt in to Play App Signing.
- Never commit the keystore or passwords to git (git-ignore it)

**Dependencies**
- Android Resolver (External Dependency Manager) resolved before build; generated local repos git-ignored
- Check each SDK's required Android permissions and target SDK compatibility
- Use real AdMob app id and ad unit ids only in the production build; Google test ids in dev

**Store listing**
- 512x512 icon, 1024x500 feature graphic, 2 to 8 phone screenshots (plus 7"/10" tablet if offered), short (80 char) and full description, category, tags, support email, privacy policy URL
- Screenshots should show real gameplay, not mockups

**Testing path**
1. Internal testing (fast, small team)
2. Closed test (required tester count and duration for new personal accounts)
3. Production with **staged rollout** (start 5 to 20%), watch crashes and ANRs in Android vitals
4. Google review can take days; plan buffer

**Release gate checklist**
- [ ] Self-tests pass
- [ ] Release build uses production ad ids, not test ids
- [ ] Analytics and ads gated by consent
- [ ] Version code is the next unused number
- [ ] Data Safety, privacy policy and listing match the build
- [ ] Tested on a real low-end and a real recent device
- [ ] Release notes written (and a changelog file in the repo)
- [ ] Crash reporting on (Firebase Crashlytics, recommended)

**After launch**
- In-app update API so players move to fixed builds (used)
- Analytics events for the funnel: level start, win, fail, ad shown, purchase (used)
- Optional: block sideloaded release builds that pirate or bypass ads (used, only when the production define is set)
- Watch Android vitals (crash rate, ANR rate); bad vitals reduce store visibility

## 5. Claude Code working rules (what made this efficient)

- Write a design doc first (core loop, modes, economy, monetization, UI direction); keep it updated.
- Keep memory notes of decisions, ids, links and "what the human still owns".
- Claude must **never submit store forms, releases, purchases or legal declarations without the human's explicit OK**.
- When Claude says "done", ask for proof: test count, screenshot, log line.
- Run `/code-review` and `/simplify` before a release.
- Unity Editor playing while unfocused can stall frames; write tests that call game logic directly.
- Long Unity builds time out the MCP call; do not retry, wait for the output file.
- Commit small. Use branches for risky features.

## 6. Starter prompt for a new project

```
I am making a [2D/3D] mobile game in Unity ([URP/HDRP/Built-in]). Follow my playbook.
First check the project's render pipeline and settings; do not assume.
Rules: core logic in plain testable C#; all tunable numbers in one config file;
third-party SDKs behind interfaces with stubs for the Editor; consent before ads or
analytics; self-test menu run before each release; no per-frame allocations; profile
on a real device before optimizing; pooling for spawned objects; versioned saves with
migration; small commits; keep a memory file; never submit store forms or releases
without my OK; only release builds bump the version code; give short, plain answers.
```
