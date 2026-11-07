#if UNITY_EDITOR || DEVELOPMENT_BUILD || CB_DEBUG
using System.Collections;
using UnityEngine;
using CircuitBreaker.Core;
using CircuitBreaker.Data;
using CircuitBreaker.Fx;
using CircuitBreaker.Generation;

namespace CircuitBreaker.Promo
{
    /// <summary>
    /// The scripted reels. Each is built on a hook that performs for puzzle ads:
    /// fail and near-miss (the viewer "knows" the right move), IQ challenge, satisfying cascade,
    /// benefit-led opener, goal/rescue, and the level 1 vs level 999 contrast. Many stop before the
    /// board is solved, because unfinished progress is what makes people want to try.
    /// Everything shown is the real board and the real rules; only the player's choices are staged.
    /// </summary>
    public enum Scenario { GeniusFail, LastMove, IqTest, Satisfying, BrainRelax, RageTap, RescueGold, Level1vs999, EndCardOnly }

    public partial class PromoDirector
    {
        public static readonly (Scenario id, string label)[] All =
        {
            (Scenario.GeniusFail, "99% fail"),
            (Scenario.LastMove, "Last move"),
            (Scenario.IqTest, "IQ test 5s"),
            (Scenario.Satisfying, "Satisfying"),
            (Scenario.BrainRelax, "Relax intro"),
            (Scenario.RageTap, "Rage taps"),
            (Scenario.RescueGold, "Free gold"),
            (Scenario.Level1vs999, "Lvl 1 vs 999"),
            (Scenario.EndCardOnly, "End card"),
        };

        private LevelData _shown;

        private IEnumerator ScenarioBody(Scenario s)
        {
            switch (s)
            {
                case Scenario.GeniusFail: return GeniusFail();
                case Scenario.LastMove: return LastMove();
                case Scenario.IqTest: return IqTest();
                case Scenario.Satisfying: return Satisfying();
                case Scenario.BrainRelax: return BrainRelax();
                case Scenario.RageTap: return RageTap();
                case Scenario.RescueGold: return RescueGold();
                case Scenario.Level1vs999: return Level1vs999();
                default: return EndCardOnly();
            }
        }

        private static LevelSpec Spec(int index, string shape, Difficulty? difficulty = null, int salt = 0) =>
            new LevelSpec { index = index, shape = shape, difficulty = difficulty, salt = salt };

        // ---------------------------------------------------------------------
        // 1. "99% FAIL THIS LEVEL": confident start, hesitation, three wrong taps, level failed. (~18 s)
        // ---------------------------------------------------------------------
        private IEnumerator GeniusFail()
        {
            yield return Setup(Spec(45, "Butterfly", Difficulty.Medium, 7), 3, "LEVEL 45");
            Caption("99% FAIL\nTHIS LEVEL");
            Sub("Only geniuses pass");
            yield return FadeIn();
            yield return Wait(0.7f);
            for (int i = 0; i < 4 && !_abort; i++) { yield return Tap(Movable(i % 2), 0.38f); yield return Wait(0.12f); }

            Caption("Easy... right?"); Sub("");
            yield return Hesitate(Movable(), Blocked(), 2);
            yield return Tap(Blocked(), 0.25f);
            StartCoroutine(Stamp("WRONG!", Red, 0.5f));
            Caption("NOOO!", Red);
            yield return Wait(0.8f);

            yield return Tap(Blocked(1), 0.35f);
            Caption("1 heart left...", Gold);
            yield return Wait(0.5f);
            yield return Hesitate(Movable(), Blocked(2), 2, 0.3f);
            yield return Tap(Blocked(2), 0.25f);
            yield return Stamp("LEVEL\nFAILED", Red, 0.9f, 130);
            Caption("Can YOU do better?");
            yield return Wait(1.3f);
            yield return EndCard();
        }

        // ---------------------------------------------------------------------
        // 2. "ONE MOVE TO WIN": three arrows, one heart, long hesitation, wrong pick. (~12 s)
        // ---------------------------------------------------------------------
        private IEnumerator LastMove()
        {
            yield return Setup(Spec(60, "Heart", Difficulty.Medium, 3), 1, "LEVEL 60");
            Thin(3);
            yield return Wait(1.0f);                     // extraction plays out under the cover
            Caption("ONE MOVE\nTO WIN");
            Sub("1 heart left. Choose wisely.");
            yield return FadeIn();
            yield return Wait(0.5f);
            var good = Movable(); var bad = Blocked() ?? Movable(1);
            yield return Hesitate(good, bad, 3, 0.42f);
            yield return Tap(bad, 0.2f);
            yield return Stamp("SO CLOSE!", Red, 0.8f);
            Caption("Which one would\nYOU tap?");
            _board.ViewOf(good.id)?.Hint(2f);
            yield return Wait(1.6f);
            yield return EndCard();
        }

        // ---------------------------------------------------------------------
        // 3. IQ test with a 5 second countdown and a brain meter. (~16 s)
        // ---------------------------------------------------------------------
        private IEnumerator IqTest()
        {
            yield return Setup(Spec(80, "Owl", Difficulty.Medium, 11), 3, "IQ TEST");
            Caption("Which arrow\ncan escape?");
            Sub("You have 5 seconds");
            Meter(0.15f, "IQ  85");
            yield return FadeIn();
            for (int n = 5; n >= 1 && !_abort; n--)
            {
                StartCoroutine(Stamp(n.ToString(), Color.white, 0.5f, 230));
                Sfx.Zap();
                var any = Blocked(UnityEngine.Random.Range(0, 6)) ?? Movable();
                yield return HandTo(Mid(any) + Vector3.up * 0.3f, 0.45f);
                yield return Wait(0.5f);
            }
            yield return Stamp("TIME'S UP", Red, 0.5f, 140);

            var good = Movable();
            _board.ViewOf(good.id)?.Hint(1.4f);
            Caption("THIS one!", Gold); Sub("");
            yield return Wait(0.6f);
            yield return Tap(good, 0.3f);
            Meter(0.55f, "IQ  120");
            yield return Cascade(0.22f, 0.06f, 14);
            Meter(1f, "IQ  160  GENIUS");
            Caption("Did you find it?");
            yield return Wait(1.3f);
            yield return EndCard();
        }

        // ---------------------------------------------------------------------
        // 4. "Wait for it...": one tap starts a full, accelerating chain clear. (~14 s)
        // ---------------------------------------------------------------------
        private IEnumerator Satisfying()
        {
            yield return Setup(Spec(90, "Sun", Difficulty.Medium, 5), 3, "", hud: false);
            Caption("Wait for it...");
            yield return FadeIn();
            yield return Wait(0.8f);
            var key = Model.FindBestHint(out _) ?? Movable();
            yield return Tap(key, 0.6f);
            Caption("");
            yield return Cascade(0.3f, 0.035f);
            Sfx.Win();
            yield return Stamp("PERFECT!", Green, 0.9f);
            Caption("So satisfying");
            yield return Wait(0.8f);
            yield return EndCard();
        }

        // ---------------------------------------------------------------------
        // 5. Benefit-led opener, close-up "guess the shape", zoom out reveal, clear. (~18 s)
        // ---------------------------------------------------------------------
        private IEnumerator BrainRelax()
        {
            yield return Setup(Spec(70, "Cat", Difficulty.Medium, 2), 3, "", hud: false);
            var cam = Gm.Cam;
            cam.FocusOn(new Vector3(-1.5f, 1.5f, 0f), 3.4f);
            foreach (var line in new[] { "Relax your mind", "Train your brain", "Play offline, anytime" })
            {
                Caption(line, Gold);
                yield return Wait(0.95f);
            }
            Caption("Guess the shape");
            yield return FadeIn(0.4f);
            yield return Wait(0.5f);
            yield return Tap(Movable(), 0.45f);
            yield return Tap(Movable(), 0.35f);
            cam.FitBoard();                                // smooth zoom out: the silhouette appears
            yield return Wait(0.9f);
            Caption("A " + _shown.shape.ToUpperInvariant() + "!", Gold);
            yield return Cascade(0.26f, 0.05f);
            Caption("Your daily brain break");
            yield return Wait(1f);
            yield return EndCard();
        }

        // ---------------------------------------------------------------------
        // 6. Rage taps: three confident wrong taps in a row, game over, the answer was right there. (~12 s)
        // ---------------------------------------------------------------------
        private IEnumerator RageTap()
        {
            yield return Setup(Spec(50, "Fish", Difficulty.Medium, 9), 3, "LEVEL 50");
            Caption("When you're SURE\nit's this one");
            yield return FadeIn();
            yield return Wait(0.6f);
            yield return Tap(Blocked(), 0.4f);
            Caption("...it's NOT", Red);
            yield return Wait(0.35f);
            yield return Tap(Blocked(1), 0.22f);
            yield return Wait(0.2f);
            yield return Tap(Blocked(2), 0.18f);
            yield return Stamp("GAME OVER", Red, 0.8f, 140);
            Caption("The right one was\nRIGHT THERE");
            var good = Movable();
            _board.ViewOf(good.id)?.Hint(2.2f);
            yield return HandTo(Mid(good) + Vector3.up * 0.3f, 0.4f);
            yield return Wiggle(0.5f);
            yield return Wait(0.8f);
            yield return EndCard();
        }

        // ---------------------------------------------------------------------
        // 7. Goal: free the glowing gold arrow, stop one move before the end. (~15 s)
        // ---------------------------------------------------------------------
        private IEnumerator RescueGold()
        {
            yield return Setup(Spec(75, "Rocket", Difficulty.Medium, 4), 3, "LEVEL 75");
            ArrowDef target = null; int best = -1;
            foreach (var a in Model.Arrows.Values)
            {
                int n = ChainLength(a);
                if (n > best && n <= 6) { best = n; target = a; }
            }
            if (target == null) target = Movable();
            StartCoroutine(GlowTarget(target.id));
            Caption("Free the\nGOLD arrow!", Gold);
            Sub("Can you do it in " + (best + 1) + " moves?");
            yield return FadeIn();
            yield return Wait(0.8f);

            int moves = 0;
            for (int guard = 0; guard < 12 && !_abort && Model.Arrows.ContainsKey(target.id); guard++)
            {
                var next = NextToward(target);
                if (next == null) break;
                if (next.id == target.id)
                {
                    Caption("ONE MOVE LEFT...", Gold);
                    yield return HandTo(Mid(target) + Vector3.up * 0.3f, 0.4f);
                    yield return Wiggle(0.6f);
                    yield return Wait(0.6f);
                    break;
                }
                yield return Tap(next, 0.42f);
                moves++;
                Sub(moves + (moves == 1 ? " move" : " moves"));
                yield return Wait(0.15f);
            }
            yield return EndCard();
        }

        private IEnumerator GlowTarget(int id)
        {
            while (_board != null && Model.Arrows.ContainsKey(id) && !_abort)
            {
                _board.ViewOf(id)?.Flash(Gold, 0.45f);
                yield return Wait(0.55f);
            }
        }

        // ---------------------------------------------------------------------
        // 8. Level 1 vs Level 999: trivial board, then a nightmare board and panic. (~14 s)
        // ---------------------------------------------------------------------
        private IEnumerator Level1vs999()
        {
            yield return Setup(LevelSpec.Classic(1), 0, "");
            Caption("LEVEL 1"); Sub("Easy");
            yield return FadeIn(0.25f);
            for (int i = 0; i < 12 && Model.Remaining > 0 && !_abort; i++) yield return Tap(Movable(), 0.22f);
            yield return Stamp("TOO EASY", Green, 0.4f);
            yield return HandAway(0.2f);
            yield return FadeOut(0.15f);

            yield return Setup(Spec(200, "Owl", Difficulty.Nightmare, 1), 3, "");
            Caption("LEVEL 999", Red); Sub("Nightmare");
            yield return FadeIn(0.15f);
            StartCoroutine(Shake(_caption.rectTransform, 12f, 0.5f));
            yield return Wait(0.6f);
            yield return Hesitate(Movable(), Blocked(), 2, 0.34f);
            Caption("...wait, WHAT?!", Red);
            yield return Stamp("???", Gold, 0.7f, 200);
            Caption("How far can YOU get?");
            yield return Wait(1f);
            yield return EndCard();
        }

        private IEnumerator EndCardOnly()
        {
            _cover.color = Color.black;
            yield return EndCard();
        }
    }
}
#endif
