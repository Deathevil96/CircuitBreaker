using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using CircuitBreaker.Ads;
using CircuitBreaker.Core;
using CircuitBreaker.Fx;
using CircuitBreaker.Gameplay;
using CircuitBreaker.Generation;

namespace CircuitBreaker.UI
{
    /// <summary>
    /// The whole interface, built from code. Split across partial files: core (this file), HUD, menu, pages, sheets.
    /// It is a view: it raises events and GameManager decides what they mean.
    /// </summary>
    public partial class GameUI : MonoBehaviour
    {
        // ----- HUD -----
        public event Action BackClicked, RestartClicked, HintClicked, ExtractClicked, RulerClicked, FitClicked;
        // ----- menu -----
        public event Action ContinueClicked, DailyClicked, FreeplayClicked, OverloadClicked, LevelsClicked, RecordsClicked, SettingsClicked;
        // ----- sheets -----
        public event Action NextClicked, ReplayClicked, ContinueAdClicked, RefillClicked, FailRestartClicked;
        public event Action GetMoreAdClicked, GetMoreCoinsClicked, GetMoreTick;
        public event Action OverloadStartClicked, OverloadAgainClicked, OverloadMenuClicked, ResetProgressConfirmed;
        public event Action<int> LevelPicked, DailyDayPicked;
        public event Action<Difficulty> FreeplayStartClicked;
        public event Action<bool> SoundChanged, VibrationChanged, GlowChanged;
        public event Action QuitRequested;

        public const float DefaultBannerUnits = 130f;      // 50dp on a typical 1080-wide phone
        private float _bannerUnits = DefaultBannerUnits;

        public bool ModalOpen => _open.Count > 0;
        public bool MenuOpen => _menuRoot != null && _menuRoot.activeSelf;
        public Transform SafeRoot => _safe;

        private Canvas _canvas;
        private RectTransform _safe, _canvasRect, _overlay;
        private readonly HashSet<GameObject> _open = new HashSet<GameObject>();
        private readonly List<GameObject> _stack = new List<GameObject>();      // open layers, bottom to top
        private Rect _lastSafe;
        private Vector2Int _lastScreen;
        private bool _bannerApplied;
        private float _tick;

        // ---------- build ----------

        public void Build()
        {
            EnsureEventSystem();
            var canvasGo = new GameObject("UI");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _canvasRect = (RectTransform)canvasGo.transform;

            _safe = UiKit.Rect("Safe", canvasGo.transform);
            UiKit.Stretch(_safe);
            ApplySafeArea();

            BuildHud();
            // Menu, pages, sheets and toasts live above the banner strip, never under it.
            _overlay = UiKit.Rect("Overlay", _safe);
            UiKit.Stretch(_overlay);
            BuildAdBar(canvasGo.transform);
            BuildMenu();
            BuildPages();
            BuildSheets();
            BuildToast();
            ApplyBannerLayout();
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        // ---------- safe area and banner ----------

        private void ApplySafeArea()
        {
            float w = Screen.width, h = Screen.height;
            if (w <= 0 || h <= 0) return;
            var sa = Screen.safeArea;
            _lastSafe = sa;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            // Real portrait phones only have small cutout insets. Anything bigger is a stale value (the editor
            // reports one after a Game view resize), so that edge is ignored.
            float left = Mathf.Clamp01(sa.xMin / w), right = Mathf.Clamp01(1f - sa.xMax / w);
            float bottom = Mathf.Clamp01(sa.yMin / h), top = Mathf.Clamp01(1f - sa.yMax / h);
            if (left > 0.05f) left = 0f;
            if (right > 0.05f) right = 0f;
            if (bottom > 0.1f) bottom = 0f;
            if (top > 0.1f) top = 0f;
            _safe.anchorMin = new Vector2(left, bottom);
            _safe.anchorMax = new Vector2(1f - right, 1f - top);
            _safe.offsetMin = _safe.offsetMax = Vector2.zero;
            UpdateAdBarSize();
        }

        private void Update()
        {
            if (_safe != null && (Screen.safeArea != _lastSafe || Screen.width != _lastScreen.x || Screen.height != _lastScreen.y))
                ApplySafeArea();

            if (_canvas != null)
            {
                // A real banner reports its true height; the reserved strip follows it.
                float native = AdService.BannerNativeHeightPx;
                float units = native > 1f ? native / _canvas.scaleFactor : DefaultBannerUnits;
                if (Mathf.Abs(units - _bannerUnits) > 1f) { _bannerUnits = units; _bannerApplied = !AdService.BannerVisible; }
                AdService.BannerHeightPx = (_bannerUnits + SafeBottomUnits()) * _canvas.scaleFactor;
            }
            if (AdService.BannerVisible != _bannerApplied) ApplyBannerLayout();

            if (Time.unscaledTime >= _tick)
            {
                _tick = Time.unscaledTime + 0.5f;
                if (_getMoreSheet != null && _getMoreSheet.activeSelf) GetMoreTick?.Invoke();
            }
            UpdateToast();
            CenterMenu();
        }

        /// <summary>Canvas units between the screen bottom and the safe area (home indicator).</summary>
        private float SafeBottomUnits() => _safe != null && _canvasRect != null ? _safe.anchorMin.y * _canvasRect.rect.height : 0f;

        // ---------- layers (menu, pages, sheets) ----------

        private void SetLayer(GameObject go, bool on)
        {
            go.SetActive(on);
            _stack.Remove(go);
            if (on) { _open.Add(go); _stack.Add(go); }
            else _open.Remove(go);
        }

        public void HideAllOverlays()
        {
            foreach (var go in new List<GameObject>(_open)) if (go != null) go.SetActive(false);
            _open.Clear();
            _stack.Clear();
        }

        /// <summary>Android back button / Escape. Returns true when an overlay consumed it.</summary>
        public bool HandleBack()
        {
            if (_stack.Count == 0) return false;
            var top = _stack[_stack.Count - 1];
            // These screens are decisions, not detours: the player has to choose.
            if (top == _winSheet || top == _failSheet || top == _ovResultSheet) return true;
            if (top == _menuRoot)
            {
                ShowConfirm("Exit Arrow Breaker?", "Your progress is saved.", "Exit", () => QuitRequested?.Invoke(), false);
                return true;
            }
            SetLayer(top, false);
            return true;
        }

        /// <summary>Only taps inside the play area reach the board.</summary>
        public bool PointerInPlayArea(Vector2 screen)
        {
            if (ModalOpen) return false;
            float fy = screen.y / Mathf.Max(1, Screen.height);
            return fy > CameraController.BottomSafe && fy < 1f - CameraController.TopSafe;
        }
    }
}
