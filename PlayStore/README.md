# Play Store assets: Arrow Breaker: Circuit Puzzle

Everything for the Play Console listing in one place (outside `Assets`, so Unity does not import it).

| Folder | Contents | Play Console slot |
| :--- | :--- | :--- |
| `Graphics/icon_512.png` | app icon, 512 x 512 | App icon |
| `Graphics/feature_graphic_1024x500.png` | feature graphic | Feature graphic |
| `Screenshots/01_menu.png` ... `11_nightmare.png` | phone screenshots, 1080 x 1920 | Phone screenshots (use 4 to 8) |

Listing text, privacy policy and Data safety answers are in `Docs/Store`.

Suggested screenshot order: 02_board_heart, 03_board_human, 05_win, 07_daily, 06_records, 10_overload, 11_nightmare, 01_menu.

To regenerate the screenshots: enter Play Mode in the Editor and run `CircuitBreaker.EditorTools.StoreShots.Run()` (and `FeatureGraphic()`); they use a throwaway save, never yours.
