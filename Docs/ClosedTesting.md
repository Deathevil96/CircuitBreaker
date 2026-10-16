# Arrow Breaker: Google Play closed testing guide

Everything needed to put build 0.1.0 in front of testers. Steps marked **(you)** need your Google account or a decision; everything else is already done in the project.

## 1. What is already done

| Area | State |
| :--- | :--- |
| Package | `com.misalinteractive.arrowbreaker` (change it in `BuildTools.cs` before the first upload if you want another; it can never change afterwards) |
| Build | IL2CPP, ARM64, min Android 7.1 (API 25), target API = newest installed, Android App Bundle (AAB), portrait only |
| Signing | Upload keystore generated at `Keystore/circuitbreaker-upload.keystore`, passwords in `Keystore/keystore.local.json` (both git-ignored). **Back both up now.** |
| Version | App 0.1.0. The version code is computed from the build time, so every build is higher than the last (Play requires that) |
| Icons | Adaptive launcher icon and a 512 px store icon generated in `Assets/_Game/Art/Icons` |
| Ads | Closed-test build ships clearly labelled **test ads** (banner strip, a fake rewarded ad, a fake restart ad). See section 6 for real AdMob |
| Logic tests | 300+ automated checks run before every build (`CircuitBreaker > Run Self Tests`). The build stops if any fail |
| Crash reports | Free from Play Console (Android vitals). Debug symbols are included in the bundle |
| Feedback | Settings > Send feedback opens an email with version and device details filled in |

## 2. Build the bundle

In the Unity menu: **CircuitBreaker > Android > Build Closed-Test AAB (test ads)**.

- It runs the self tests, applies the Android settings, signs with the upload key and writes `Builds/Android/CircuitBreaker_<version>_<code>_closedtest.aab`.
- The first Android build compiles IL2CPP for several minutes. Unity stays busy meanwhile.
- **Build Test APK** makes an installable file for a USB-connected phone (`adb install`), useful before uploading.
- **Build Release AAB** is the same without test ads. Until a real ad SDK is added it shows no ads at all (see 6).

## 3. Create the app in Play Console **(you)**

1. Play Console > Create app. Name: Arrow Breaker. Default language: English. App (not game category is fine, choose **Game**). Free.
2. Accept the declarations.
3. Set up your app (Dashboard checklist). Answers to use are in `Docs/Store`:
   - **Privacy policy**: host `Docs/Store/privacy-policy.html` somewhere public (GitHub Pages works) and paste the URL. Put the same URL into `AppLinks.PrivacyPolicyUrl` in code.
   - **App access**: all functionality available without login.
   - **Ads**: yes, contains ads.
   - **Content rating**: questionnaire answers in `Docs/Store/listing.md` (expect PEGI 3 / Everyone).
   - **Target audience**: 13 and over. Do not choose "children" (that triggers the Families policy and different ad rules).
   - **Data safety**: use `Docs/Store/data-safety.md`.
   - **Government / financial / health apps**: not applicable.
4. Store listing: text in `Docs/Store/listing.md`. Images: icon `PlayStore/Graphics/icon_512.png`, feature graphic and phone screenshots are in `PlayStore/Screenshots`.

## 4. Start the closed test **(you)**

1. Testing > Closed testing > Create track (or use the default "Closed testing - Alpha").
2. Testers: create an email list (up to 100 Google accounts) or a Google Group.
3. Create a release, upload the `.aab`. Release name e.g. `0.1.0 (closed test 1)`. Paste the release notes from `Docs/Store/listing.md`.
4. Play App Signing: accept (recommended). Your keystore becomes the **upload key**.
5. Review and roll out. The first review can take a few days. When approved, copy the opt-in link and send it to testers.
6. Personal developer accounts created after November 2023 must run a closed test with **at least 12 testers for 14 days** before applying for production. Start this early.

## 5. What to ask testers to check

`Docs/QA-Checklist.md` is the test plan. The questions that matter most for this game:

- Do the first five levels teach the game without help?
- Does anything feel unfair (blocked arrows, ice, keys, nightmares)?
- Are the ads (banner, rewarded hint, restart ad) acceptable, and does the banner ever cover something?
- Any slowness loading large boards, or crashes? (Android vitals will also show these.)

## 6. Ads: from test ads to real AdMob

- A closed-test build uses the **test ad layer** built into the game, so testers can try every ad flow. Nothing real is shown or billed.
- For production create an AdMob account, add the app, and create three ad units (banner, rewarded, interstitial).
- The project can use the Google Mobile Ads plugin (`com.google.ads.mobile`). If it was added successfully, `Assets/_Game/Scripts/Ads/AdMobProvider.cs` is the adapter. Set your real App ID in Assets > Google Mobile Ads > Settings and put your ad unit IDs in `AdUnits.cs`. Until then only Google's public test IDs are used.
- Before real ads: show the consent form (UMP) in the EU/UK, add the Play "Advertising ID" declaration, and keep the ad placements as designed (banner never over gameplay, one restart ad per session at most).

## 7. Updating later

Bump the app version in `BuildInfo.cs` (and `CHANGELOG.md`), build again, upload to the same track. Never reuse a version code (done automatically).

## 8. Branches and versions
| Branch | Purpose | App version | Contents |
| :--- | :--- | :--- | :--- |
| `main` | development and closed testing | 0.1.0 (0.x for tests) | everything, including debug tools and the level test |
| `master` | store release | 1.0.0 | all levels, no debug tools, no test ads |

The version code is a plain counter in `ProjectSettings/BuildNumber.txt`. The first build is 1. Each build adds 1, whatever the type (APK or AAB), so Play always sees a higher number.
