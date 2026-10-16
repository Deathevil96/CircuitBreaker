# Play Console: Data safety form (answers for the AdMob build)

These answers match the app as built. If you remove ads, or add analytics or purchases, update them. Google lists exactly what AdMob collects at https://developers.google.com/admob/android/privacy/play-data-disclosure, and that page wins if it differs.

## Overview questions
| Question | Answer |
| :--- | :--- |
| Does your app collect or share any of the required user data types? | Yes |
| Is all of the user data collected by your app encrypted in transit? | Yes |
| Do you provide a way for users to request that their data be deleted? | Not applicable: no data is held by us. Data collected by AdMob is handled by Google; users can reset their advertising ID in Android settings |

## Data types
| Data type | Collected | Shared | Purpose | Optional |
| :--- | :--- | :--- | :--- | :--- |
| Approximate location (derived from IP) | Yes (by AdMob) | Yes (with Google) | Advertising or marketing, Analytics | No |
| Device or other IDs (advertising ID) | Yes (by AdMob) | Yes (with Google) | Advertising or marketing | No |
| App interactions (ad interactions) | Yes (by AdMob) | Yes (with Google) | Advertising or marketing, Analytics | No |
| Crash logs / Diagnostics | Yes (by AdMob, Play vitals) | Yes (with Google) | Analytics, App functionality | No |
| Personal info, financial info, health, messages, photos, audio, files, contacts, calendar | No | No | | |

## Other declarations
- **Ads**: the app contains ads.
- **Advertising ID** (Android 13+): declare that the app uses it, for advertising.
- **Target audience**: 13+ (not designed for children).
- **App access**: no restricted features, no login.
- **Financial features / Health / News / Government**: none.
