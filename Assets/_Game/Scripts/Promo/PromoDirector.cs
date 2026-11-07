#if UNITY_EDITOR || DEVELOPMENT_BUILD || CB_DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;
using CircuitBreaker.Core;
using CircuitBreaker.Data;
using CircuitBreaker.Fx;
using CircuitBreaker.Gameplay;
using CircuitBreaker.Generation;
using CircuitBreaker.UI;

namespace CircuitBreaker.Promo
{
    /// <summary>
    /// Plays scripted, recordable ad scenes (reels) on a private board, with a fake HUD, a hand cursor,
    /// big captions and a store end card. Nothing here touches the save, stats, achievements or ads:
    /// the real game board and UI are hidden while a scene plays and restored afterwards.
    /// Debug builds only (this file is removed from the release branch).
    /// </summary>
    public partial class PromoDirector : MonoBehaviour
    {
        public static PromoDirector Instance { get; private set; }
        public static bool Playing => Instance != null && Instance._playing;

        // assets (PlayStore/Promo in the Editor, StreamingAssets/Promo on a device)
        private const string IconFile = "app_icon.png";
        private const string BadgeFile = "google_play_badge_clean.png";
        private const string BadgeUrl = "https://play.google.com/intl/en_us/badges/static/images/badges/en_badge_web_generic.png";

        private static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);
        private static readonly Color Red = new Color(1f, 0.23f, 0.36f, 1f);
        private static readonly Color Green = new Color(0.24f, 1f, 0.48f, 1f);

        private bool _playing, _abort;
        private Action _onDone;
        private Texture2D _icon, _badge;

        // stage
        private RectTransform _root, _stage, _hud, _hand, _endCard;
        private Image _cover, _flash;
        private Text _caption, _stamp, _combo, _level, _sub;
        private readonly List<Image> _hearts = new List<Image>();
        private RectTransform _meter; private Image _meterFill; private Text _meterLabel;
        private BoardView _board;
        private int _hp, _maxHp;

        // restore state
        private bool _camInput;
        private Canvas _gameCanvas;

        public static PromoDirector Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("PromoDirector");
            DontDestroyOnLoad(go);
            return go.AddComponent<PromoDirector>();
        }

        private void Awake()
        {
            Instance = this;
            BuildStage();
            StartCoroutine(LoadAssets());
        }

        // =====================================================================
        // Public entry
        // =====================================================================
        public void Play(Scenario scene, Action onDone)
        {
            if (_playing) return;
            _onDone = onDone;
            StartCoroutine(Run(scene));
        }

        private IEnumerator Run(Scenario scene)
        {
            var gm = Gm;
            _playing = true; _abort = false;

            // hide the real game
            _gameCanvas = gm.UI.GetComponentInChildren<Canvas>(true);
            if (_gameCanvas != null) _gameCanvas.enabled = false;
            gm.Board.gameObject.SetActive(false);
            _camInput = gm.Cam.InputEnabled;
            gm.Cam.InputEnabled = false;
            CameraController.TopOverride = 0.33f;        // caption + HUD band
            CameraController.BottomOverride = 0.16f;     // combo / meter band

            ResetStage();
            _stage.gameObject.SetActive(true);
            _cover.color = Color.black;

            var body = ScenarioBody(scene);
            while (!_abort && body.MoveNext()) yield return body.Current;

            // restore
            ClearBoard();
            _stage.gameObject.SetActive(false);
            CameraController.TopOverride = -1f;
            CameraController.BottomOverride = -1f;
            gm.Board.gameObject.SetActive(true);
            if (gm.Level != null) gm.Cam.Frame(gm.Level.width, gm.Level.height);
            gm.Cam.InputEnabled = _camInput;
            if (_gameCanvas != null) _gameCanvas.enabled = true;
            _playing = false;
            var done = _onDone; _onDone = null;
            done?.Invoke();
        }

        private void Update()
        {
            // Back / Escape stops a scene early.
            if (_playing && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) _abort = true;
        }

        // =====================================================================
        // Board helpers
        // =====================================================================
        private IEnumerator Setup(LevelSpec spec, int hearts, string levelText, bool hud = true)
        {
            var task = Task.Run(() => LevelGenerator.Generate(spec));
            while (!task.IsCompleted) yield return null;
            var level = task.IsFaulted ? LevelGenerator.Generate(spec) : task.Result;
            level.hearts = hearts;
            _shown = level;

            ClearBoard();
            var go = new GameObject("PromoBoard");
            go.transform.SetParent(transform, false);
            _board = go.AddComponent<BoardView>();
            _board.Build(level);
            Gm.Cam.Frame(level.width, level.height);

            _maxHp = hearts; _hp = hearts;
            _level.text = levelText;
            _hud.gameObject.SetActive(hud);
            RefreshHearts();
            yield return null;
        }

        private void ClearBoard()
        {
            if (_board != null) Destroy(_board.gameObject);
            _board = null;
        }

        private Board Model => _board.Model;

        private static GameManager Gm => GameManager.Instance != null ? GameManager.Instance : FindFirstObjectByType<GameManager>();

        private Vector3 Mid(ArrowDef a) => _board.GridToWorld(a.cells[a.cells.Count / 2]);

        private ArrowDef Movable(int pick = 0)
        {
            var m = Model.MovableArrows();
            if (m.Count == 0) return null;
            m.Sort((x, y) => Mid(x).sqrMagnitude.CompareTo(Mid(y).sqrMagnitude));     // centre first: reads better on camera
            return m[Mathf.Clamp(pick, 0, m.Count - 1)];
        }

        /// <summary>A blocked (not inert) arrow near the centre: tapping it bumps and costs a heart.</summary>
        private ArrowDef Blocked(int pick = 0)
        {
            var list = new List<ArrowDef>();
            foreach (var a in Model.Arrows.Values)
                if (!Model.IsInert(a.id) && !Model.IsMovable(a)) list.Add(a);
            if (list.Count == 0) return null;
            list.Sort((x, y) => Mid(x).sqrMagnitude.CompareTo(Mid(y).sqrMagnitude));
            return list[Mathf.Clamp(pick, 0, list.Count - 1)];
        }

        /// <summary>Next arrow to tap to free <paramref name="target"/>: walks the chain of first blockers.</summary>
        private ArrowDef NextToward(ArrowDef target)
        {
            var cur = target;
            for (int guard = 0; guard < 64 && cur != null; guard++)
            {
                if (Model.IsMovable(cur)) return cur;
                if (Model.IsInert(cur.id)) break;
                if (Model.CastRay(cur, out _, out _, out int blocker) || !Model.Arrows.TryGetValue(blocker, out var next)) break;
                cur = next;
            }
            return Movable();
        }

        private int ChainLength(ArrowDef target)
        {
            var cur = target; int n = 0;
            for (int guard = 0; guard < 64 && cur != null; guard++, n++)
            {
                if (Model.IsMovable(cur)) return n;
                if (Model.IsInert(cur.id)) return -1;
                if (Model.CastRay(cur, out _, out _, out int blocker) || !Model.Arrows.TryGetValue(blocker, out cur)) return -1;
            }
            return -1;
        }

        /// <summary>Instantly removes arrows (under the cover) until only <paramref name="keep"/> remain, keeping a blocked pair.</summary>
        private void Thin(int keep)
        {
            var b = Blocked();
            var save = new HashSet<int>();
            if (b != null)
            {
                save.Add(b.id);
                if (!Model.CastRay(b, out _, out _, out int blocker)) save.Add(blocker);
            }
            var ids = new List<int>(Model.Arrows.Keys);
            ids.Sort((x, y) => Mid(Model.Arrows[y]).sqrMagnitude.CompareTo(Mid(Model.Arrows[x]).sqrMagnitude));   // outer arrows go first
            foreach (int id in ids)
            {
                if (Model.Remaining <= keep) break;
                if (save.Contains(id)) continue;
                _board.Extract(id);
            }
        }

        /// <summary>Hand travels to the arrow, presses, and the tap is applied to the board.</summary>
        private IEnumerator Tap(ArrowDef a, float travel = 0.45f)
        {
            if (a == null) yield break;
            var world = Mid(a);
            yield return HandTo(world, travel);
            yield return Press();
            var result = _board.Tap(world);
            if (result == TapResult.Blocked) yield return Hurt();
            else if (result == TapResult.Escaped) Sfx.Blip(1f);
        }

        private IEnumerator Hurt()
        {
            _hp = Mathf.Max(0, _hp - 1);
            RefreshHearts(true);
            StartCoroutine(Flash(Red, 0.35f));
            StartCoroutine(Shake(_hud, 14f, 0.35f));
            yield return Wait(0.2f);
        }

        /// <summary>Clears the board by itself, faster and faster, with a combo counter and rising pitch.</summary>
        private IEnumerator Cascade(float start = 0.32f, float end = 0.05f, int limit = 999, bool hideHand = true)
        {
            if (hideHand) StartCoroutine(HandAway());
            float interval = start;
            int combo = 0;
            while (Model.Remaining > 0 && limit-- > 0 && !_abort)
            {
                var a = Movable(UnityEngine.Random.Range(0, 3));
                if (a == null) break;
                _board.Tap(Mid(a));
                combo++;
                Sfx.Blip(Mathf.Min(2f, 1f + combo * 0.04f));
                if (combo >= 3) Combo("x" + combo);
                yield return Wait(interval);
                interval = Mathf.Max(end, interval * 0.86f);
            }
        }

        // =====================================================================
        // Stage FX
        // =====================================================================
        private IEnumerator FadeIn(float t = 0.35f) { yield return FadeImage(_cover, 1f, 0f, t); }
        private IEnumerator FadeOut(float t = 0.35f) { yield return FadeImage(_cover, _cover.color.a, 1f, t); }

        private IEnumerator Wait(float t)
        {
            float end = Time.unscaledTime + t;
            while (Time.unscaledTime < end && !_abort) yield return null;
        }

        private void Caption(string text, Color? color = null)
        {
            _caption.text = text;
            _caption.color = color ?? Color.white;
            StartCoroutine(Pop(_caption.rectTransform, 1.18f, 0.22f));
        }

        private void Sub(string text, Color? color = null)
        {
            _sub.text = text;
            _sub.color = color ?? new Color(1f, 1f, 1f, 0.85f);
            if (!string.IsNullOrEmpty(text)) StartCoroutine(Pop(_sub.rectTransform, 1.12f, 0.2f));
        }

        private IEnumerator Stamp(string text, Color color, float hold = 0.9f, int size = 150)
        {
            _stamp.text = text; _stamp.color = color; _stamp.fontSize = size;
            var r = _stamp.rectTransform;
            float t = 0f;
            while (t < 0.18f) { t += Time.unscaledDeltaTime; float k = t / 0.18f; r.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, EaseOut(k)); SetAlpha(_stamp, k); yield return null; }
            r.localScale = Vector3.one; SetAlpha(_stamp, 1f);
            yield return Wait(hold);
            t = 0f;
            while (t < 0.2f) { t += Time.unscaledDeltaTime; SetAlpha(_stamp, 1f - t / 0.2f); yield return null; }
            SetAlpha(_stamp, 0f);
        }

        private void Combo(string text)
        {
            _combo.text = text;
            SetAlpha(_combo, 1f);
            StartCoroutine(Pop(_combo.rectTransform, 1.4f, 0.16f));
        }

        private void RefreshHearts(bool lostOne = false)
        {
            for (int i = 0; i < _hearts.Count; i++)
            {
                bool show = i < _maxHp;
                _hearts[i].gameObject.SetActive(show);
                _hearts[i].color = i < _hp ? Theme.Heart : Theme.A(Theme.Faint, 0.6f);   // same colours as the in-game hearts
                if (lostOne && i == _hp) StartCoroutine(Pop(_hearts[i].rectTransform, 1.7f, 0.3f));
            }
        }

        private void Meter(float value, string label)
        {
            _meter.gameObject.SetActive(true);
            StartCoroutine(MeterTo(value));
            _meterLabel.text = label;
        }

        private IEnumerator MeterTo(float target)
        {
            float from = _meterFill.fillAmount, t = 0f;
            while (t < 0.6f) { t += Time.unscaledDeltaTime; _meterFill.fillAmount = Mathf.Lerp(from, target, EaseOut(t / 0.6f)); yield return null; }
            _meterFill.fillAmount = target;
            _meterFill.color = Color.Lerp(Red, Green, target);
        }

        private IEnumerator Flash(Color c, float t)
        {
            float e = 0f;
            while (e < t) { e += Time.unscaledDeltaTime; _flash.color = new Color(c.r, c.g, c.b, 0.3f * (1f - e / t)); yield return null; }
            _flash.color = Color.clear;
        }

        private IEnumerator Shake(RectTransform r, float amp, float t)
        {
            var basePos = r.anchoredPosition; float e = 0f;
            while (e < t) { e += Time.unscaledDeltaTime; r.anchoredPosition = basePos + UnityEngine.Random.insideUnitCircle * amp * (1f - e / t); yield return null; }
            r.anchoredPosition = basePos;
        }

        private IEnumerator Pop(RectTransform r, float peak, float t)
        {
            float e = 0f;
            while (e < t) { e += Time.unscaledDeltaTime; float k = e / t; r.localScale = Vector3.one * Mathf.Lerp(peak, 1f, EaseOut(k)); yield return null; }
            r.localScale = Vector3.one;
        }

        private static IEnumerator FadeImage(Image img, float from, float to, float t)
        {
            float e = 0f; var c = img.color;
            while (e < t) { e += Time.unscaledDeltaTime; c.a = Mathf.Lerp(from, to, e / t); img.color = c; yield return null; }
            c.a = to; img.color = c;
        }

        private static IEnumerator FadeGroup(CanvasGroup g, float to, float t)
        {
            float from = g.alpha, e = 0f;
            while (e < t) { e += Time.unscaledDeltaTime; g.alpha = Mathf.Lerp(from, to, e / t); yield return null; }
            g.alpha = to;
        }

        private static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
        private static float EaseOut(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }
        private static float EaseInOut(float k) { k = Mathf.Clamp01(k); return k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f; }

        // ---------- hand ----------
        private Vector2 WorldToCanvas(Vector3 world)
        {
            var screen = Camera.main.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
            return local;
        }

        private IEnumerator HandTo(Vector3 world, float t) => HandToCanvas(WorldToCanvas(world), t);

        private IEnumerator HandToCanvas(Vector2 target, float t)
        {
            if (!_hand.gameObject.activeSelf)
            {
                _hand.gameObject.SetActive(true);
                _hand.anchoredPosition = new Vector2(620f, -1100f);       // enters from the bottom right
            }
            var from = _hand.anchoredPosition; float e = 0f;
            while (e < t && !_abort)
            {
                e += Time.unscaledDeltaTime;
                float k = EaseInOut(e / t);
                var p = Vector2.Lerp(from, target, k);
                p.y += Mathf.Sin(k * Mathf.PI) * 60f;                    // slight arc, like a real thumb
                _hand.anchoredPosition = p;
                yield return null;
            }
            _hand.anchoredPosition = target;
        }

        private IEnumerator Press()
        {
            var ring = UiKit.Img(_stage, new Color(1f, 1f, 1f, 0.55f), ProcSprites.Ring);
            var rr = ring.rectTransform;
            rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f);
            rr.anchoredPosition = _hand.anchoredPosition;
            rr.sizeDelta = new Vector2(70, 70);
            float e = 0f;
            while (e < 0.12f) { e += Time.unscaledDeltaTime; _hand.localScale = Vector3.one * Mathf.Lerp(1f, 0.86f, e / 0.12f); yield return null; }
            StartCoroutine(RingOut(ring));
            e = 0f;
            while (e < 0.1f) { e += Time.unscaledDeltaTime; _hand.localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, e / 0.1f); yield return null; }
            _hand.localScale = Vector3.one;
        }

        private IEnumerator RingOut(Image ring)
        {
            float e = 0f;
            while (e < 0.35f) { e += Time.unscaledDeltaTime; float k = e / 0.35f; ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(70f, 190f, k); SetAlpha(ring, 0.55f * (1f - k)); yield return null; }
            Destroy(ring.gameObject);
        }

        /// <summary>Thumb wavers between two arrows, the classic "which one?!" beat.</summary>
        private IEnumerator Hesitate(ArrowDef a, ArrowDef b, int swings = 3, float speed = 0.38f)
        {
            if (a == null || b == null) yield break;
            for (int i = 0; i < swings && !_abort; i++)
            {
                yield return HandTo(Mid(a) + Vector3.up * 0.3f, speed);
                yield return Wiggle(0.25f);
                yield return HandTo(Mid(b) + Vector3.up * 0.3f, speed);
                yield return Wiggle(0.25f);
                speed *= 0.85f;
            }
        }

        private IEnumerator Wiggle(float t)
        {
            var basePos = _hand.anchoredPosition; float e = 0f;
            while (e < t) { e += Time.unscaledDeltaTime; _hand.anchoredPosition = basePos + new Vector2(Mathf.Sin(e * 40f) * 8f, 0f); yield return null; }
            _hand.anchoredPosition = basePos;
        }

        private IEnumerator HandAway(float t = 0.4f)
        {
            if (!_hand.gameObject.activeSelf) yield break;
            yield return HandToCanvas(new Vector2(700f, -1200f), t);
            _hand.gameObject.SetActive(false);
        }

        // =====================================================================
        // End card: tap the icon, splash-style name, "Get it on Google Play"
        // =====================================================================
        private IEnumerator EndCard(bool waitForTap = true)
        {
            yield return HandAway(0.25f);
            _hud.gameObject.SetActive(false);
            _meter.gameObject.SetActive(false);
            _caption.text = ""; _sub.text = "";
            yield return FadeOut(0.4f);
            ClearBoard();

            _endCard.gameObject.SetActive(true);
            foreach (Transform c in _endCard) Destroy(c.gameObject);

            // 1. a home-screen icon, tapped by the thumb
            var iconRoot = UiKit.Rect("Icon", _endCard);
            iconRoot.anchorMin = iconRoot.anchorMax = new Vector2(0.5f, 0.5f);
            iconRoot.sizeDelta = new Vector2(250, 250);
            iconRoot.anchoredPosition = new Vector2(0, -60);
            IconInto(iconRoot, 250f);
            var iconLabel = UiKit.Label(_endCard, BuildInfo.ProductName, 40, Color.white, TextAnchor.MiddleCenter);
            iconLabel.rectTransform.anchorMin = iconLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            iconLabel.rectTransform.anchoredPosition = new Vector2(0, -230);
            iconLabel.rectTransform.sizeDelta = new Vector2(800, 60);

            var iconGroup = iconRoot.gameObject.AddComponent<CanvasGroup>();
            iconGroup.alpha = 0f;
            _cover.color = Color.black;
            StartCoroutine(FadeGroup(iconGroup, 1f, 0.3f));
            yield return Pop(iconRoot, 0.6f, 0.35f);
            yield return Wait(0.25f);
            yield return HandToCanvas(iconRoot.anchoredPosition + new Vector2(10, -10), 0.5f);
            yield return Press();
            Sfx.Hint();
            yield return HandAway(0.25f);

            // icon grows to fill the screen, like an app opening
            Destroy(iconLabel.gameObject);
            float e = 0f;
            while (e < 0.35f) { e += Time.unscaledDeltaTime; float k = EaseInOut(e / 0.35f); iconRoot.localScale = Vector3.one * Mathf.Lerp(1f, 9f, k); iconGroup.alpha = 1f - k; yield return null; }
            Destroy(iconRoot.gameObject);
            yield return Wait(0.2f);

            // 2. the name, centred, fading in like an engine splash
            var nameGroup = UiKit.Rect("Name", _endCard).gameObject.AddComponent<CanvasGroup>();
            var nameRect = (RectTransform)nameGroup.transform;
            nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 0.5f);
            nameRect.sizeDelta = new Vector2(1000, 400);
            nameRect.anchoredPosition = new Vector2(0, 160);
            nameGroup.alpha = 0f;
            if (_icon != null)
            {
                var small = UiKit.Rect("SmallIcon", nameRect);
                Center(small, new Vector2(0, 150), new Vector2(150, 150));
                IconInto(small, 150f);
            }
            var title = UiKit.Label(nameRect, BuildInfo.ProductName.ToUpperInvariant(), 104, Color.white, TextAnchor.MiddleCenter, bold: true);
            title.rectTransform.sizeDelta = new Vector2(1000, 130);
            title.rectTransform.anchoredPosition = new Vector2(0, -20);
            var glow = title.gameObject.AddComponent<Shadow>(); glow.effectColor = new Color(0.1f, 0.9f, 0.95f, 0.6f); glow.effectDistance = new Vector2(0, -4);
            var tag = UiKit.Label(nameRect, "Circuit puzzle", 40, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
            tag.rectTransform.sizeDelta = new Vector2(1000, 60);
            tag.rectTransform.anchoredPosition = new Vector2(0, -110);
            e = 0f;
            while (e < 0.8f) { e += Time.unscaledDeltaTime; float k = EaseOut(e / 0.8f); nameGroup.alpha = k; nameRect.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, k); yield return null; }
            yield return Wait(0.5f);

            // 3. "available now" + the Google Play badge as a pulsing button
            var avail = UiKit.Label(_endCard, "AVAILABLE NOW", 44, Gold, TextAnchor.MiddleCenter, bold: true);
            avail.rectTransform.anchorMin = avail.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            avail.rectTransform.sizeDelta = new Vector2(1000, 70);
            avail.rectTransform.anchoredPosition = new Vector2(0, -170);
            StartCoroutine(Pop(avail.rectTransform, 1.3f, 0.25f));

            var badgeRoot = UiKit.Rect("Badge", _endCard);
            badgeRoot.anchorMin = badgeRoot.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRoot.anchoredPosition = new Vector2(0, -330);
            var badge = badgeRoot.gameObject.AddComponent<Image>();
            if (_badge != null)
            {
                badge.sprite = Sprite.Create(_badge, new Rect(0, 0, _badge.width, _badge.height), new Vector2(0.5f, 0.5f));
                badge.preserveAspect = true;
                badgeRoot.sizeDelta = new Vector2(600, 600f * _badge.height / _badge.width);
            }
            else
            {
                badge.sprite = ProcSprites.Rounded; badge.type = Image.Type.Sliced; badge.color = Color.black;
                badgeRoot.sizeDelta = new Vector2(600, 180);
                var fb = UiKit.Label(badgeRoot, "GET IT ON  <b>Google Play</b>", 48, Color.white, TextAnchor.MiddleCenter);
                UiKit.Stretch(fb.rectTransform);
            }
            yield return Pop(badgeRoot, 0.5f, 0.3f);
            StartCoroutine(Pulse(badgeRoot));
            yield return Wait(0.6f);
            yield return HandToCanvas(badgeRoot.anchoredPosition + new Vector2(60, -20), 0.5f);
            yield return Press();
            Sfx.Win();
            yield return Wait(0.4f);
            yield return HandAway(0.3f);

            if (waitForTap)
            {
                yield return Wait(1.2f);
                // Stay on the end card until the screen is tapped, so the recording can be stopped calmly.
                while (!_abort && !(Pointer.current != null && Pointer.current.press.wasPressedThisFrame)) yield return null;
            }
            _endCard.gameObject.SetActive(false);
        }

        /// <summary>Launcher-style icon: the square art clipped to a rounded square.</summary>
        private void IconInto(RectTransform r, float size)
        {
            var mask = r.gameObject.AddComponent<Image>();
            mask.sprite = ProcSprites.Rounded; mask.type = Image.Type.Sliced;
            mask.pixelsPerUnitMultiplier = 22f / (size * 0.22f);
            mask.color = _icon != null ? Color.white : Theme.Accent;
            if (_icon == null) return;
            r.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var art = UiKit.Img(r, Color.white, Sprite.Create(_icon, new Rect(0, 0, _icon.width, _icon.height), new Vector2(0.5f, 0.5f)));
            UiKit.Stretch(art.rectTransform);
        }

        private IEnumerator Pulse(RectTransform r)
        {
            while (r != null)
            {
                r.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.045f);
                yield return null;
            }
        }

        // =====================================================================
        // Stage construction
        // =====================================================================
        private void BuildStage()
        {
            var canvasGo = new GameObject("PromoCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)canvasGo.transform;

            _stage = UiKit.Rect("Stage", _root);
            UiKit.Stretch(_stage);

            // fake HUD (hearts + level), top of the screen
            _hud = UiKit.Rect("Hud", _stage);
            Center(_hud, new Vector2(0, 410), new Vector2(1000, 120));
            _level = MakeText(_hud, 40, new Vector2(0, 40), new Vector2(1000, 60), bold: true);
            for (int i = 0; i < 5; i++)
            {
                var h = UiKit.Icon(_hud, "heart", 62, Theme.Heart);          // the very same heart icon the game HUD uses
                h.rectTransform.anchoredPosition = new Vector2((i - 1) * 78f, -30f);
                _hearts.Add(h);
            }

            // IQ / brain meter (some scenes)
            _meter = UiKit.Rect("Meter", _stage);
            Center(_meter, new Vector2(0, -820), new Vector2(760, 90));
            var track = UiKit.Rounded(_meter, new Color(1f, 1f, 1f, 0.12f), 22f);
            Center(track.rectTransform, new Vector2(0, -16), new Vector2(760, 40));
            _meterFill = UiKit.Img(track.rectTransform, Red);
            UiKit.Stretch(_meterFill.rectTransform, 4, 4, 4, 4);
            _meterFill.type = Image.Type.Filled; _meterFill.fillMethod = Image.FillMethod.Horizontal; _meterFill.fillAmount = 0f;
            _meterLabel = MakeText(_meter, 34, new Vector2(0, 36), new Vector2(760, 50), bold: true);

            // captions
            _caption = MakeText(_stage, 80, new Vector2(0, 740), new Vector2(1000, 240), bold: true, outline: true);
            _sub = MakeText(_stage, 44, new Vector2(0, 565), new Vector2(1000, 80), bold: true, outline: true);
            _combo = MakeText(_stage, 110, new Vector2(330, -560), new Vector2(400, 160), bold: true, outline: true);
            _combo.color = Gold;

            _flash = UiKit.Img(_stage, Color.clear); UiKit.Stretch(_flash.rectTransform);
            _cover = UiKit.Img(_stage, Color.black); UiKit.Stretch(_cover.rectTransform);

            _endCard = UiKit.Rect("EndCard", _stage);
            UiKit.Stretch(_endCard);

            _stamp = MakeText(_stage, 150, new Vector2(0, 80), new Vector2(1080, 400), bold: true, outline: true);

            // hand: fingertip is the pivot, palm below, tilted like a right thumb
            _hand = UiKit.Rect("Hand", _stage);
            Center(_hand, Vector2.zero, Vector2.zero);
            _hand.localRotation = Quaternion.Euler(0, 0, 18f);
            Part(_hand, new Vector2(0, -70), new Vector2(64, 150), 32f);       // finger
            Part(_hand, new Vector2(18, -190), new Vector2(150, 140), 46f);    // palm
            Part(_hand, new Vector2(-62, -170), new Vector2(56, 100), 28f);    // thumb
            _hand.gameObject.SetActive(false);

            _stage.gameObject.SetActive(false);
        }

        private void Part(RectTransform hand, Vector2 pos, Vector2 size, float radius)
        {
            var outline = UiKit.Rounded(hand, new Color(0.08f, 0.08f, 0.1f, 1f), radius + 6f);
            Center(outline.rectTransform, pos, size + new Vector2(14, 14));
            var fill = UiKit.Rounded(hand, new Color(1f, 0.93f, 0.86f, 1f), radius);
            Center(fill.rectTransform, pos, size);
        }

        private static void Center(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos; r.sizeDelta = size;
        }

        private static Text MakeText(Transform parent, int size, Vector2 pos, Vector2 box, bool bold = false, bool outline = false)
        {
            var t = UiKit.Label(parent, "", size, Color.white, TextAnchor.MiddleCenter, bold, wrap: true);
            Center(t.rectTransform, pos, box);
            if (outline)
            {
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.95f);
                o.effectDistance = new Vector2(5, -5);
                var s = t.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0f, 0f, 0f, 0.6f);
                s.effectDistance = new Vector2(0, -9);
            }
            return t;
        }

        private void ResetStage()
        {
            _caption.text = ""; _sub.text = ""; _stamp.text = ""; _combo.text = "";
            _flash.color = Color.clear;
            _hud.gameObject.SetActive(false);
            _meter.gameObject.SetActive(false);
            _meterFill.fillAmount = 0f;
            _hand.gameObject.SetActive(false);
            _endCard.gameObject.SetActive(false);
            foreach (Transform c in _endCard) Destroy(c.gameObject);
        }

        // =====================================================================
        // Assets
        // =====================================================================
        private IEnumerator LoadAssets()
        {
            yield return LoadTexture(IconFile, t => _icon = t);
            yield return LoadTexture(BadgeFile, t => _badge = t);
            if (_badge == null)
            {
                // Not bundled: fetch the official badge from Google Play.
                using (var req = UnityWebRequestTexture.GetTexture(BadgeUrl))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success) _badge = DownloadHandlerTexture.GetContent(req);
                }
            }
        }

        private static IEnumerator LoadTexture(string file, Action<Texture2D> done)
        {
#if UNITY_EDITOR
            string local = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PlayStore", "Promo", file));
            if (File.Exists(local))
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                t.LoadImage(File.ReadAllBytes(local));
                done(t);
                yield break;
            }
#endif
            string url = Path.Combine(Application.streamingAssetsPath, "Promo", file);
            if (!url.Contains("://")) url = "file://" + url;
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();
                done(req.result == UnityWebRequest.Result.Success ? DownloadHandlerTexture.GetContent(req) : null);
            }
        }
    }
}
#endif
