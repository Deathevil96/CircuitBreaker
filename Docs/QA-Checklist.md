# QA checklist (closed test)

Run on at least one small phone, one tall (20:9 or more) phone and, if possible, a tablet. Note device, Android version and build number (Settings page, bottom).

## First run
- [ ] Fresh install opens on the menu, "Play", Level 1, 1 day streak.
- [ ] Level 1 pulses the best first move and shows the tutorial hint.
- [ ] Levels 1 to 5 never cost hearts. Blocked arrows bump.
- [ ] Winning level 1 shows the Level complete sheet, coins, and "Next level" works.

## Core play
- [ ] Taps never trigger while dragging or pinching the board.
- [ ] Pinch zoom, drag pan, Fit button.
- [ ] Hint highlights a legal move. Ruler shows green and red paths. Extract removes any arrow.
- [ ] Combo counter, hearts, game over sheet, refill with coins.
- [ ] Ice thaws after N escapes. Key arrows unlock padlocked arrows.
- [ ] From level 21 the board has a shape. The edge dots stay; other dots appear only under moving arrows.

## Leave and return
- [ ] Back arrow (top left) goes to the menu. Continue returns to the same board with the same arrows, hearts and combo reset only.
- [ ] Android back button: closes overlays, then goes to the menu, then asks before exiting.
- [ ] Force-close the app mid-level, reopen: Continue resumes the board.
- [ ] Restart (top right) resets the board. Starting a different level never wipes a saved board.

## Modes
- [ ] Daily Run: today plays, earlier days this month can be played, future days are locked, clearing marks the day, the month badge appears when all days are cleared.
- [ ] Freeplay: each difficulty starts a fresh board; "New board" gives another.
- [ ] Overload: clock runs, bumps cost time, boards chain, result shows score, best and date.

## Progress and records
- [ ] Records page: day streak, win streak, most wins in a day, each with a date. Achievements unlock with a toast and reward.
- [ ] First-attempt awards unlock only when the level was won without a restart or failure.
- [ ] Change the phone date forward a day: day streak increases. Skip a day: it resets, the record stays.

## Ads (test ads in this build)
- [ ] A banner strip is visible at the bottom and never overlaps the board or buttons, including on zoomed boards.
- [ ] With 0 hints, tapping Hint offers a rewarded ad (+2) and coins. After the ad a 5 minute cooldown shows.
- [ ] Restart ad appears once per session, on the 2nd or 3rd reset, not in levels 1 to 5, and never again that session.
- [ ] Out of hearts: one "+1 heart" ad per session.
- [ ] Closing an ad early gives no reward.

## Robustness
- [ ] Airplane mode: everything works except ads (buttons say unavailable).
- [ ] Incoming call or switching apps, then returning: nothing lost, Overload does not skip time.
- [ ] Low storage / low memory phone: Settings > Glow effect off improves smoothness.
- [ ] Rotate the phone: stays portrait.
- [ ] Large boards (Hard and above) load without freezing the interface.
- [ ] Settings: Sound, Vibration, Glow toggles persist after restart. Reset progress asks for confirmation.
