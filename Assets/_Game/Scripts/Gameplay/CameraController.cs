using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CircuitBreaker.Gameplay
{
    /// <summary>Pan / pinch-zoom camera and tap detection, using the new Input System only.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        // Fractions of screen height reserved for the HUD (top) and booster bar (bottom),
        // plus any device cutout / home-indicator inset reported by Screen.safeArea.
        public static float TopSafe { get { Insets(out float t, out _); return 0.115f + t; } }
        public static float BottomSafe { get { Insets(out _, out float b); return 0.165f + b + Ads.AdService.BannerFraction; } }

        private static void Insets(out float top, out float bottom)
        {
            top = bottom = 0f;
            if (Screen.height <= 0) return;
            var sa = Screen.safeArea;
            top = Mathf.Clamp01(1f - sa.yMax / Screen.height);
            bottom = Mathf.Clamp01(sa.yMin / Screen.height);
            if (top > 0.1f) top = 0f;          // implausible value (editor can report a stale safe area)
            if (bottom > 0.1f) bottom = 0f;
        }

        public Action<Vector2> Tapped;
        public Func<Vector2, bool> PointerAllowed;
        public bool InputEnabled = true;

        private Camera _cam;
        private float _fitSize = 6f, _minSize = 3f, _targetSize = 6f;
        private Vector3 _targetPos = new Vector3(0, 0, -10);
        private Vector2 _bounds = new Vector2(4, 4);
        private float _boardCenterY;

        private int _boardW, _boardH;
        private float _lastAspect, _lastTop, _lastBottom;
        private bool _tracking, _dragging, _pinchedThisPress;
        private Vector2 _startPos, _lastPos;
        private float _lastPinchDist;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
        }

        public void Frame(int w, int h)
        {
            _boardW = w; _boardH = h;
            _lastAspect = _cam.aspect; _lastTop = TopSafe; _lastBottom = BottomSafe;
            float aspect = Mathf.Max(0.3f, _cam.aspect);
            const float margin = 1.6f;
            float usable = 1f - TopSafe - BottomSafe;
            float forWidth = (w + margin) / (2f * aspect);
            float forHeight = (h + margin) / (2f * usable);
            _fitSize = Mathf.Max(forWidth, forHeight);
            _minSize = Mathf.Min(_fitSize, 3.4f);
            _targetSize = _fitSize;
            _bounds = new Vector2(w * 0.5f, h * 0.5f);
            _boardCenterY = 0f;
            _targetPos = FitPosition(_fitSize);
            _cam.orthographicSize = _targetSize;
            transform.position = _targetPos;
        }

        private Vector3 FitPosition(float size)
        {
            // Lower the camera a little so the board sits in the middle of the safe area.
            float centerFrac = 0.5f + (BottomSafe - TopSafe) * 0.5f;
            return new Vector3(0, _boardCenterY - (centerFrac - 0.5f) * 2f * size, -10);
        }

        public void FitBoard()
        {
            _targetSize = _fitSize;
            _targetPos = FitPosition(_fitSize);
        }

        public void FocusOn(Vector3 world, float size = -1f)
        {
            if (size > 0) _targetSize = Mathf.Clamp(size, _minSize, _fitSize);
            if (_targetSize >= _fitSize * 0.97f) return;     // already showing the whole board
            _targetPos = ClampPos(new Vector3(world.x, world.y, -10));
        }

        public Vector3 ScreenToWorld(Vector2 screen)
        {
            var p = _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_cam.transform.position.z));
            p.z = 0;
            return p;
        }

        private Vector3 ClampPos(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, -_bounds.x, _bounds.x);
            p.y = Mathf.Clamp(p.y, -_bounds.y, _bounds.y);
            p.z = -10;
            return p;
        }

        private void Update()
        {
            // Window resized, device rotated, foldable opened, safe area changed: re-fit the board.
            if (_boardW > 0 && (Mathf.Abs(_cam.aspect - _lastAspect) > 0.001f
                                 || Mathf.Abs(TopSafe - _lastTop) > 0.001f || Mathf.Abs(BottomSafe - _lastBottom) > 0.001f))
            {
                bool wasAtFit = _targetSize >= _fitSize * 0.97f;
                float zoomRatio = _targetSize / _fitSize;
                Frame(_boardW, _boardH);
                if (!wasAtFit) { _targetSize = Mathf.Clamp(_fitSize * zoomRatio, _minSize, _fitSize); _cam.orthographicSize = _targetSize; }
            }
            if (InputEnabled) ReadInput();
            float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetSize, k);
            transform.position = Vector3.Lerp(transform.position, _targetPos, k);
        }

        private void ReadInput()
        {
            var ts = Touchscreen.current;
            int touches = 0;
            if (ts != null)
                foreach (var t in ts.touches) if (t.isInProgress) touches++;

            if (touches >= 2)
            {
                Vector2 p0 = default, p1 = default; int found = 0;
                foreach (var t in ts.touches)
                {
                    if (!t.isInProgress) continue;
                    if (found == 0) p0 = t.position.ReadValue(); else if (found == 1) p1 = t.position.ReadValue();
                    found++;
                    if (found == 2) break;
                }
                float dist = Vector2.Distance(p0, p1);
                if (_pinchedThisPress && _lastPinchDist > 1f)
                    Zoom((_lastPinchDist - dist) / Screen.height * _cam.orthographicSize * 2.2f);
                _lastPinchDist = dist;
                _pinchedThisPress = true;
                _tracking = false;
                return;
            }
            _lastPinchDist = 0;

            bool down, pressedNow, releasedNow; Vector2 pos;
            if (ts != null && (ts.primaryTouch.press.isPressed || ts.primaryTouch.press.wasReleasedThisFrame || ts.primaryTouch.press.wasPressedThisFrame))
            {
                var pt = ts.primaryTouch;
                down = pt.press.isPressed; pressedNow = pt.press.wasPressedThisFrame; releasedNow = pt.press.wasReleasedThisFrame;
                pos = pt.position.ReadValue();
            }
            else if (Mouse.current != null)
            {
                var m = Mouse.current;
                down = m.leftButton.isPressed; pressedNow = m.leftButton.wasPressedThisFrame; releasedNow = m.leftButton.wasReleasedThisFrame;
                pos = m.position.ReadValue();
                float scroll = m.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && PointerAllowedSafe(pos)) Zoom(-Mathf.Sign(scroll) * _cam.orthographicSize * 0.12f);
            }
            else return;

            if (touches == 0 && !down) _pinchedThisPress = false;

            if (pressedNow)
            {
                _pinchedThisPress = false;
                _tracking = PointerAllowedSafe(pos);
                _dragging = false;
                _startPos = _lastPos = pos;
            }
            else if (_tracking && down)
            {
                float threshold = Mathf.Max(12f, (Screen.dpi > 0 ? Screen.dpi : 160f) * 0.07f);
                if (!_dragging && (pos - _startPos).magnitude > threshold) _dragging = true;
                if (_dragging)
                {
                    Vector2 delta = pos - _lastPos;
                    float worldPerPixel = _cam.orthographicSize * 2f / Screen.height;
                    _targetPos = ClampPos(_targetPos - new Vector3(delta.x, delta.y, 0) * worldPerPixel);
                }
                _lastPos = pos;
            }

            if (releasedNow)
            {
                if (_tracking && !_dragging && !_pinchedThisPress) Tapped?.Invoke(_startPos);
                _tracking = false; _dragging = false;
            }
        }

        private bool PointerAllowedSafe(Vector2 p) => PointerAllowed == null || PointerAllowed(p);

        private void Zoom(float delta)
        {
            _targetSize = Mathf.Clamp(_targetSize + delta, _minSize, _fitSize);
            if (_targetSize >= _fitSize * 0.995f) _targetPos = FitPosition(_fitSize);
        }
    }
}
