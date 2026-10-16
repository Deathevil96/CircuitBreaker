using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CircuitBreaker.Data;
using CircuitBreaker.Fx;

namespace CircuitBreaker.Gameplay
{
    /// <summary>Visual for one arrow: glow + segmented packet body + LED head. Moves like a snake along its own path.</summary>
    public class ArrowView : MonoBehaviour
    {
        public ArrowDef Def { get; private set; }
        public bool IsBusy { get; private set; }

        private LineRenderer _core;
        private SpriteRenderer _headChevron, _keyRing, _lockIcon;
        private Transform _head;
        private TextMesh _iceLabel;

        private readonly List<Vector3> _cells = new List<Vector3>();
        private readonly List<Vector3> _track = new List<Vector3>();
        private Vector3[] _pts;
        private Color _baseColor;
        private Vector3 _dir;
        private int _distToEdge;
        private bool _inert;
        private bool _gone;
        private int _iceShown = -1;
        private Coroutine _colorRoutine;
        private Action<Vector3> _trail;      // reports where the line currently is, so the board can light the cells it crosses

        private const float CoreWidth = 0.10f;

        public static ArrowView Create(Transform parent, ArrowDef def, Func<Vector2Int, Vector3> toWorld, int boardW, int boardH, Action<Vector3> trail = null)
        {
            var go = new GameObject("Arrow_" + def.id);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<ArrowView>();
            v._trail = trail;
            v.Build(def, toWorld, boardW, boardH);
            return v;
        }

        private void Build(ArrowDef def, Func<Vector2Int, Vector3> toWorld, int boardW, int boardH)
        {
            Def = def;
            _dir = new Vector3(def.dir.x, def.dir.y, 0);
            foreach (var c in def.cells) _cells.Add(toWorld(c));

            // Steps from the head until it leaves the board along dir.
            _distToEdge = 1;
            var p = def.Head + def.dir;
            while (p.x >= 0 && p.y >= 0 && p.x < boardW && p.y < boardH) { _distToEdge++; p += def.dir; }

            int ext = _distToEdge + _cells.Count + 3;
            _track.AddRange(_cells);
            for (int i = 1; i <= ext; i++) _track.Add(_cells[_cells.Count - 1] + _dir * i);
            _pts = new Vector3[_cells.Count];

            // One thin neon line plus a small arrowhead. The glow comes from bloom only.
            var flat = ProcSprites.LineMaterial;
            _core = MakeLine("Line", flat, CoreWidth, 12);

            var headGo = new GameObject("Head");
            headGo.transform.SetParent(transform, false);
            _head = headGo.transform;
            _headChevron = MakeSprite("Chevron", ProcSprites.Triangle, 0.36f, 15, _head);
            _headChevron.transform.localPosition = new Vector3(0, 0.02f, 0);

            if (def.kind == ArrowKind.Key)
            {
                _keyRing = MakeSprite("KeyRing", ProcSprites.Ring, 0.62f, 16, _head);
                _keyRing.color = CyberTheme.Hint;
            }
            if (def.kind == ArrowKind.Locked)
            {
                _lockIcon = MakeSprite("Lock", ProcSprites.Lock, 0.32f, 18);
                _lockIcon.transform.position = _cells[_cells.Count / 2];
                _lockIcon.color = CyberTheme.Hint;
            }
            if (def.kind == ArrowKind.Ice)
            {
                var go = new GameObject("IceLabel");
                go.transform.SetParent(transform, false);
                go.transform.position = _cells[_cells.Count / 2] + new Vector3(0, 0, -0.1f);
                _iceLabel = go.AddComponent<TextMesh>();
                _iceLabel.font = UiFont.Get();
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = _iceLabel.font.material;
                mr.sortingOrder = 20;
                _iceLabel.anchor = TextAnchor.MiddleCenter;
                _iceLabel.alignment = TextAlignment.Center;
                _iceLabel.fontSize = 64;
                _iceLabel.characterSize = 0.05f;
                _iceLabel.color = Color.white;
                _iceLabel.text = def.counter.ToString();
            }

            _inert = def.kind == ArrowKind.Ice || def.kind == ArrowKind.Locked;
            RefreshBaseColor();
            Place(0f);
        }

        private LineRenderer MakeLine(string name, Material m, float width, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = m;
            lr.widthMultiplier = width;
            lr.numCapVertices = 6;
            lr.numCornerVertices = 6;
            lr.sortingOrder = order;
            lr.positionCount = _cells.Count;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        private SpriteRenderer MakeSprite(string name, Sprite s, float scale, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            return sr;
        }

        private void RefreshBaseColor()
        {
            var own = CyberTheme.Arrow(Def.colorIndex);
            _baseColor = Def.kind == ArrowKind.Ice && _inert ? Color.Lerp(own, CyberTheme.IceColor, 0.7f)
                       : Def.kind == ArrowKind.Locked && _inert ? CyberTheme.LockColor
                       : own;
            Paint(_baseColor);
        }

        private void Paint(Color c)
        {
            _core.startColor = _core.endColor = c;
            _headChevron.color = c;
        }

        // ---------- positioning ----------

        /// <summary>Places the body so the head has advanced `travel` cells along the track.</summary>
        private void Place(float travel)
        {
            int whole = Mathf.FloorToInt(travel);
            float frac = travel - whole;
            int last = _track.Count - 1;
            for (int i = 0; i < _pts.Length; i++)
            {
                int a = Mathf.Min(i + whole, last);
                int b = Mathf.Min(i + whole + 1, last);
                _pts[i] = Vector3.Lerp(_track[a], _track[b], frac);
            }
            for (int i = 0; i < _pts.Length; i++)
            {
                _core.SetPosition(i, _pts[i]);
                if (_trail != null && travel > 0.001f) _trail(_pts[i]);
            }
            var head = _pts[_pts.Length - 1];
            // Face along the track ahead of the head, so at rest the chevron points exactly where the arrow will go.
            int hi = Mathf.Min((_cells.Count - 1) + whole, last - 1);
            var facing = _track[hi + 1] - _track[hi];
            if (facing.sqrMagnitude < 0.0004f) facing = _dir;
            _head.position = transform.TransformPoint(head);
            _head.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - 90f);
        }

        // ---------- state ----------

        public void RefreshMechanics(int iceLeft, bool locked, bool silent = false)
        {
            if (_iceLabel != null && _iceShown != iceLeft)
            {
                _iceShown = iceLeft;
                _iceLabel.text = iceLeft.ToString();
                _iceLabel.gameObject.SetActive(iceLeft > 0);
            }
            bool wasInert = _inert;
            _inert = iceLeft > 0 || locked;
            if (_lockIcon != null) _lockIcon.gameObject.SetActive(locked);
            if (wasInert && !_inert)
            {
                RefreshBaseColor();
                if (silent) return;       // restoring a saved board: no thaw effect
                Flash(Color.white, 0.25f);
                Vfx.Burst(_cells[_cells.Count - 1], CyberTheme.Arrow(Def.colorIndex), 12, 3f);
            }
        }

        /// <summary>Colour override that restores the base colour afterwards.</summary>
        public void Flash(Color c, float seconds)
        {
            if (_gone) return;
            if (_colorRoutine != null) StopCoroutine(_colorRoutine);
            _colorRoutine = StartCoroutine(FlashRoutine(c, seconds));
        }

        private IEnumerator FlashRoutine(Color c, float seconds)
        {
            Paint(c);
            yield return new WaitForSeconds(seconds);
            Paint(_baseColor);
            _colorRoutine = null;
        }

        public void Hint(float seconds = 2.4f)
        {
            if (_gone) return;
            if (_colorRoutine != null) StopCoroutine(_colorRoutine);
            _colorRoutine = StartCoroutine(HintRoutine(seconds));
        }

        private IEnumerator HintRoutine(float seconds)
        {
            float t = 0;
            while (t < seconds)
            {
                t += Time.deltaTime;
                Paint(Color.Lerp(_baseColor, CyberTheme.Hint, 0.5f + 0.5f * Mathf.Sin(t * 9f)));
                yield return null;
            }
            Paint(_baseColor);
            _colorRoutine = null;
        }

        /// <summary>Feedback for frozen / locked arrows: a wobble, no heart lost.</summary>
        public void Shake()
        {
            if (IsBusy || _gone) return;
            StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            IsBusy = true;
            Flash(Color.Lerp(_baseColor, CyberTheme.BlockedFlash, 0.55f), 0.3f);
            float t = 0;
            while (t < 0.28f)
            {
                t += Time.deltaTime;
                transform.localPosition = new Vector3(Mathf.Sin(t * 70f) * 0.07f * (1f - t / 0.28f), 0, 0);
                yield return null;
            }
            transform.localPosition = Vector3.zero;
            IsBusy = false;
        }

        // ---------- animations ----------

        public void PlayEscape(Action onGone, float speed = 22f)
        {
            Begin();
            Paint(_baseColor);
            StartCoroutine(EscapeRoutine(onGone, speed));
        }

        public void PlayExtract(Action onGone)
        {
            Begin();
            Paint(CyberTheme.Hint);
            StartCoroutine(ExtractRoutine(onGone));
        }

        private void Begin()
        {
            IsBusy = true;
            _gone = true;
            if (_colorRoutine != null) StopCoroutine(_colorRoutine);
            StopAllCoroutines();
        }

        private IEnumerator EscapeRoutine(Action onGone, float speed)
        {
            float total = (_cells.Count - 1) + _distToEdge + 2;
            float travel = 0;
            _core.widthMultiplier = CoreWidth * 1.3f;   // brighter streak while escaping
            while (travel < total)
            {
                travel += Time.deltaTime * speed;
                Place(travel);
                yield return null;
            }
            onGone?.Invoke();
            Destroy(gameObject);
        }

        private IEnumerator ExtractRoutine(Action onGone)
        {
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 2.6f;
                transform.localPosition = Vector3.up * (t * t * 7f);
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, t);
                yield return null;
            }
            onGone?.Invoke();
            Destroy(gameObject);
        }

        /// <summary>Glide toward the blocker (snake-follow), spark, recoil. `onContact` fires at impact.</summary>
        public void PlayBlocked(int freeSteps, Vector3 contactWorld, Action onContact)
        {
            if (IsBusy || _gone) return;
            IsBusy = true;
            StartCoroutine(BlockedRoutine(freeSteps, contactWorld, onContact));
        }

        private IEnumerator BlockedRoutine(int freeSteps, Vector3 contactWorld, Action onContact)
        {
            float reach = freeSteps + 0.42f;
            float travel = 0;
            while (travel < reach)
            {
                travel = Mathf.Min(reach, travel + Time.deltaTime * 20f);
                Place(travel);
                yield return null;
            }
            Flash(CyberTheme.BlockedFlash, 0.35f);
            onContact?.Invoke();
            Vfx.Burst(contactWorld, CyberTheme.BlockedFlash, 22, 5.5f);
            while (travel > 0)
            {
                travel = Mathf.Max(0, travel - Time.deltaTime * 14f);
                Place(travel);
                yield return null;
            }
            Place(0);
            IsBusy = false;
        }

        public Vector3 HeadWorld => transform.TransformPoint(_cells[_cells.Count - 1]);
        public Vector3 CenterWorld
        {
            get
            {
                var sum = Vector3.zero;
                foreach (var c in _cells) sum += c;
                return transform.TransformPoint(sum / _cells.Count);
            }
        }
    }
}
