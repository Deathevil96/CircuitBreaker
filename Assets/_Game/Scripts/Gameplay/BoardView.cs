using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CircuitBreaker.Core;
using CircuitBreaker.Data;
using CircuitBreaker.Fx;

namespace CircuitBreaker.Gameplay
{
    public enum TapResult { None, Escaped, Blocked, Queued, Frozen, Extracted, Busy }

    /// <summary>Owns the visual board (PCB backdrop, pads, arrows) and applies tap rules to the Board model.</summary>
    public class BoardView : MonoBehaviour
    {
        public Board Model { get; private set; }
        public LevelData Level { get; private set; }
        public bool ExtractMode { get; set; }

        public event Action<ArrowDef, int> Escaped;        // arrow, remaining count
        public event Action<ArrowDef> Collided;            // arrow that bumped
        public event Action<ArrowDef> Extracted;           // remaining count via Model
        public event Action Changed;                        // any structural change (for ruler refresh)

        private const float PickRadius = 0.62f;
        private const float TrailAlpha = 0.6f;      // a cell an arrow has slid across keeps this dot until the level ends

        /// <summary>Arrows that have left, in order. Saved so a board can be resumed exactly.</summary>
        public readonly List<int> RemovedOrder = new List<int>();

        private SpriteRenderer[] _pads;
        private float[] _padAlpha;
        private Coroutine _finale;

        private readonly Dictionary<int, ArrowView> _views = new Dictionary<int, ArrowView>();
        private Transform _root, _decor, _rulerRoot;
        private float _rulerUntil;
        private Material _flat;

        public Vector3 GridToWorld(Vector2Int c) =>
            new Vector3(c.x - (Model.Width - 1) * 0.5f, c.y - (Model.Height - 1) * 0.5f, 0f);

        public ArrowView ViewOf(int id) => _views.TryGetValue(id, out var v) ? v : null;
        public bool RulerActive => Time.time < _rulerUntil;

        public void Build(LevelData level)
        {
            Clear();
            Level = level;
            RemovedOrder.Clear();
            Model = new Board(level);
            _flat = ProcSprites.LineMaterial;

            _root = new GameObject("Board").transform;
            _root.SetParent(transform, false);
            _decor = new GameObject("Decor").transform;
            _decor.SetParent(_root, false);
            _rulerRoot = new GameObject("Ruler").transform;
            _rulerRoot.SetParent(_root, false);

            BuildBackdrop(level);

            foreach (var a in level.arrows)
            {
                var v = ArrowView.Create(_root, a, GridToWorld, Model.Width, Model.Height, Pulse);
                _views[a.id] = v;
            }
        }

        public void Clear()
        {
            if (_finale != null) { StopCoroutine(_finale); _finale = null; }
            _views.Clear();
            CancelQueue();
            if (_root != null)
            {
                _root.gameObject.SetActive(false);      // gone immediately; Destroy only takes effect at the end of the frame
                Destroy(_root.gameObject);
            }
            _root = null;
            _rulerUntil = 0;
            ExtractMode = false;
        }

        // ---------- backdrop ----------

        private void BuildBackdrop(LevelData level)
        {
            int w = Model.Width, h = Model.Height;
            var rng = new System.Random(level.seed ^ 0x5bd1e995);

            // No grid dots at the start. A pad is created for every playable cell but stays invisible until an arrow
            // slides across it; then it shows a dot that stays until the level ends (the arrow's trail).
            _pads = new SpriteRenderer[w * h];
            _padAlpha = new float[w * h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (!level.InShape(x, y)) continue;
                    int idx = x * h + y;
                    var pad = new GameObject("Pad").AddComponent<SpriteRenderer>();
                    pad.transform.SetParent(_decor, false);
                    pad.transform.position = GridToWorld(new Vector2Int(x, y));
                    pad.transform.localScale = Vector3.one * 0.13f;
                    pad.sprite = ProcSprites.Circle;
                    pad.sortingOrder = -10;
                    _pads[idx] = pad;
                    _padAlpha[idx] = 0f;
                    SetPad(idx);
                }

            // Faint PCB traces in the background.
            int traceCount = Mathf.RoundToInt((w + h) * 1.6f);
            float halfW = w * 0.5f + 5f, halfH = h * 0.5f + 8f;
            for (int i = 0; i < traceCount; i++)
            {
                var pts = new List<Vector3>();
                var p = new Vector3(Mathf.Round((float)(rng.NextDouble() * 2 - 1) * halfW), Mathf.Round((float)(rng.NextDouble() * 2 - 1) * halfH), 0);
                int heading = rng.Next(8);
                int segs = rng.Next(3, 6);
                pts.Add(p);
                for (int s = 0; s < segs; s++)
                {
                    var d = Octant(heading);
                    p += d * rng.Next(2, 6);
                    pts.Add(p);
                    heading = (heading + (rng.Next(2) == 0 ? 1 : 7)) % 8;
                }
                var lr = new GameObject("Trace").AddComponent<LineRenderer>();
                lr.transform.SetParent(_decor, false);
                lr.useWorldSpace = true;
                lr.sharedMaterial = _flat;
                lr.positionCount = pts.Count;
                lr.SetPositions(pts.ToArray());
                lr.widthMultiplier = 0.04f;
                lr.numCornerVertices = 2;
                var c = CyberTheme.Trace; c.a = 0.14f + 0.22f * (float)rng.NextDouble();
                lr.startColor = lr.endColor = c;
                lr.sortingOrder = -30;

                var via = new GameObject("Via").AddComponent<SpriteRenderer>();
                via.transform.SetParent(_decor, false);
                via.transform.position = pts[pts.Count - 1];
                via.transform.localScale = Vector3.one * 0.2f;
                via.sprite = ProcSprites.Ring;
                via.color = c;
                via.sortingOrder = -29;
            }
        }

        private static Vector3 Octant(int i)
        {
            switch (i % 8)
            {
                case 0: return new Vector3(1, 0, 0);
                case 1: return new Vector3(1, 1, 0);
                case 2: return new Vector3(0, 1, 0);
                case 3: return new Vector3(-1, 1, 0);
                case 4: return new Vector3(-1, 0, 0);
                case 5: return new Vector3(-1, -1, 0);
                case 6: return new Vector3(0, -1, 0);
                default: return new Vector3(1, -1, 0);
            }
        }

        private LineRenderer MakeLoop(string name, float w, float h, float cut, Color color, float width, int order)
        {
            var lr = new GameObject(name).AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = _flat;
            float x = w * 0.5f, y = h * 0.5f;
            var pts = new[]
            {
                new Vector3(-x + cut, -y), new Vector3(x - cut, -y), new Vector3(x, -y + cut),
                new Vector3(x, y - cut), new Vector3(x - cut, y), new Vector3(-x + cut, y),
                new Vector3(-x, y - cut), new Vector3(-x, -y + cut)
            };
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
            lr.loop = true;
            lr.widthMultiplier = width;
            lr.startColor = lr.endColor = color;
            lr.sortingOrder = order;
            return lr;
        }

        // ---------- input ----------

        public int PickArrow(Vector3 world)
        {
            if (Model == null) return -1;
            var origin = new Vector2(world.x + (Model.Width - 1) * 0.5f, world.y + (Model.Height - 1) * 0.5f);
            var center = new Vector2Int(Mathf.RoundToInt(origin.x), Mathf.RoundToInt(origin.y));
            int best = -1;
            float bestDist = PickRadius;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var c = new Vector2Int(center.x + dx, center.y + dy);
                    int id = Model.OccupantAt(c);
                    if (id < 0) continue;
                    float d = Vector2.Distance(origin, c);
                    if (d < bestDist) { bestDist = d; best = id; }
                }
            return best;
        }

        public TapResult Tap(Vector3 world)
        {
            int id = PickArrow(world);
            if (id < 0)
            {
                if (ExtractMode) ExtractMode = false;
                return TapResult.None;
            }
            var def = Model.Arrows[id];
            var view = _views[id];
            if (view.IsBusy) return TapResult.Busy;

            if (ExtractMode)
            {
                ExtractMode = false;
                Extract(id);
                return TapResult.Extracted;
            }

            if (Model.IsInert(id))
            {
                view.Shake();
                Sfx.Zap();
                return TapResult.Frozen;
            }

            if (Model.CastRay(def, out int free, out Vector2Int blockCell, out int blockId))
            {
                Release(id, view, extract: false);
                return TapResult.Escaped;
            }

            var blocker = ViewOf(blockId);
            Vector3 contact = GridToWorld(blockCell);
            view.PlayBlocked(free, contact, () =>
            {
                if (blocker != null) blocker.Flash(CyberTheme.BlockedFlash, 0.35f);
                Sfx.Thud();
            });

            // The tap is remembered: once the arrows in front are gone this one leaves on its own.
            // The first blocked tap still counts as a mistake; tapping it again while it waits does not.
            bool alreadyWaiting = !_queued.Add(id);
            view.SetQueued(true);
            if (alreadyWaiting) return TapResult.Queued;
            Collided?.Invoke(def);
            return TapResult.Blocked;
        }

        // ---------- remembered taps ----------
        private readonly HashSet<int> _queued = new HashSet<int>();
        private Coroutine _queueRoutine;
        public int QueuedCount => _queued.Count;

        /// <summary>Forget every waiting arrow (level over, restart, out of hearts).</summary>
        public void CancelQueue()
        {
            if (_queueRoutine != null) { StopCoroutine(_queueRoutine); _queueRoutine = null; }
            foreach (int id in _queued) { var v = ViewOf(id); if (v != null) v.SetQueued(false); }
            _queued.Clear();
        }

        private void StartQueueCheck()
        {
            if (_queued.Count > 0 && _queueRoutine == null) _queueRoutine = StartCoroutine(QueueRoutine());
        }

        private IEnumerator QueueRoutine()
        {
            yield return new WaitForSeconds(0.14f);
            int waits = 0;
            while (_queued.Count > 0 && waits < 40)
            {
                int next = -1; bool busy = false;
                foreach (int qid in _queued)
                {
                    if (!Model.Arrows.TryGetValue(qid, out var d)) continue;
                    if (Model.IsInert(qid) || !Model.RayClear(d)) continue;
                    if (!_views.TryGetValue(qid, out var v) || v == null || v.IsBusy) { busy = true; continue; }
                    next = qid; break;
                }
                if (next >= 0)
                {
                    waits = 0;
                    var view = _views[next];
                    _queued.Remove(next);
                    view.SetQueued(false);
                    Release(next, view, extract: false);        // counts as a normal escape (combo, win check, save)
                    yield return new WaitForSeconds(0.14f);
                    continue;
                }
                if (!busy) break;
                waits++;
                yield return new WaitForSeconds(0.1f);
            }
            _queued.RemoveWhere(q => !Model.Arrows.ContainsKey(q));
            _queueRoutine = null;
        }

        public void Extract(int id)
        {
            if (!Model.Arrows.TryGetValue(id, out _)) return;
            Release(id, _views[id], extract: true);
        }

        private void Release(int id, ArrowView view, bool extract)
        {
            var def = Model.Arrows[id];
            var changed = Model.Remove(id);
            _queued.Remove(id);
            RemovedOrder.Add(id);
            foreach (int cid in changed)
            {
                var cv = ViewOf(cid);
                if (cv != null) cv.RefreshMechanics(Model.IceLeft(cid), Model.IsLocked(cid));
            }
            _views.Remove(id);
            Vfx.Burst(view.HeadWorld, CyberTheme.Arrow(def.colorIndex), extract ? 26 : 10, extract ? 5f : 3f);
            if (extract) view.PlayExtract(null);
            else view.PlayEscape(null);

            if (extract) Extracted?.Invoke(def);
            else Escaped?.Invoke(def, Model.Remaining);
            Changed?.Invoke();
            if (RulerActive) RebuildRuler();
            StartQueueCheck();
        }

        // ---------- hint / ruler ----------

        public ArrowView ShowHint(out int unlocks, float seconds = 2.4f)
        {
            unlocks = 0;
            var best = Model.FindBestHint(out unlocks);
            if (best == null) return null;
            var v = ViewOf(best.id);
            v?.Hint(seconds);
            return v;
        }

        public void ShowRuler(float seconds)
        {
            _rulerUntil = Time.time + seconds;
            RebuildRuler();
        }

        public void HideRuler()
        {
            _rulerUntil = 0;
            ClearRuler();
        }

        private void ClearRuler()
        {
            if (_rulerRoot == null) return;
            for (int i = _rulerRoot.childCount - 1; i >= 0; i--) Destroy(_rulerRoot.GetChild(i).gameObject);
        }

        private void RebuildRuler()
        {
            ClearRuler();
            foreach (var a in Model.Arrows.Values)
            {
                bool clear = Model.CastRay(a, out int free, out Vector2Int blockCell, out _);
                Vector3 from = GridToWorld(a.Head);
                Vector3 dir = new Vector3(a.dir.x, a.dir.y, 0);
                Vector3 to = clear ? from + dir * (free + 1.2f) : GridToWorld(blockCell) - dir * 0.45f;
                if (Model.IsInert(a.id)) clear = false;

                var lr = new GameObject("RulerLine").AddComponent<LineRenderer>();
                lr.transform.SetParent(_rulerRoot, false);
                lr.useWorldSpace = true;
                lr.sharedMaterial = _flat;
                lr.positionCount = 2;
                lr.SetPosition(0, from + dir * 0.45f);
                lr.SetPosition(1, to);
                lr.widthMultiplier = 0.05f;
                var col = clear ? CyberTheme.Good : CyberTheme.BlockedFlash;
                col.a = 0.75f;
                lr.startColor = lr.endColor = col;
                lr.sortingOrder = 30;
            }
        }

        private void SetPad(int idx)
        {
            var c = CyberTheme.Pad;
            c.a = _padAlpha[idx];
            _pads[idx].color = c;
        }

        /// <summary>Leaves a trail dot on the grid cell nearest to a world point (called for every point of a moving arrow).</summary>
        public void Pulse(Vector3 world)
        {
            if (_pads == null || Model == null) return;
            int x = Mathf.RoundToInt(world.x + (Model.Width - 1) * 0.5f);
            int y = Mathf.RoundToInt(world.y + (Model.Height - 1) * 0.5f);
            if (x < 0 || y < 0 || x >= Model.Width || y >= Model.Height) return;
            LightPad(x * Model.Height + y);
        }

        /// <summary>
        /// Level cleared: the whole silhouette appears as coloured dots sweeping in from one random direction,
        /// holds for a moment, then vanishes sweeping out toward another random direction.
        /// </summary>
        public void PlayShapeFinale()
        {
            if (_pads == null || Model == null) return;
            if (_finale != null) StopCoroutine(_finale);
            _finale = StartCoroutine(ShapeFinale());
        }

        private static readonly Vector2[] SweepDirs =
        {
            new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
            new Vector2(1, 1).normalized, new Vector2(-1, 1).normalized, new Vector2(1, -1).normalized, new Vector2(-1, -1).normalized,
        };

        private IEnumerator ShapeFinale()
        {
            int w = Model.Width, h = Model.Height;
            Vector2 vin = SweepDirs[UnityEngine.Random.Range(0, SweepDirs.Length)];
            Vector2 vout = SweepDirs[UnityEngine.Random.Range(0, SweepDirs.Length)];
            float half = Mathf.Max(1f, (w + h) * 0.5f);
            const float appear = 0.45f, hold = 0.35f, vanish = 0.5f, soft = 0.3f;
            float total = appear + hold + vanish, t = 0f;

            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                float f1 = Mathf.Min(1f, t / appear) * (1f + soft);
                float f2 = Mathf.Max(0f, (t - appear - hold) / vanish) * (1f + soft);
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        int idx = x * h + y;
                        var pad = _pads != null && idx < _pads.Length ? _pads[idx] : null;
                        if (pad == null) continue;
                        float px = x - (w - 1) * 0.5f, py = y - (h - 1) * 0.5f;
                        float tin = Mathf.Clamp01((px * vin.x + py * vin.y) / half * 0.5f + 0.5f);
                        float tout = Mathf.Clamp01((px * vout.x + py * vout.y) / half * 0.5f + 0.5f);
                        float a = Mathf.Clamp01((f1 - tin) / soft) * (1f - Mathf.Clamp01((f2 - tout) / soft));
                        var c = CyberTheme.Arrow((x * 3 + y * 2) % 8); c.a = a;
                        pad.color = c;
                        pad.transform.localScale = Vector3.one * (0.13f + 0.2f * a);
                    }
                yield return null;
            }
            if (_pads != null)
                for (int i = 0; i < _pads.Length; i++)
                    if (_pads[i] != null) { var c = _pads[i].color; c.a = 0f; _pads[i].color = c; _pads[i].transform.localScale = Vector3.one * 0.13f; _padAlpha[i] = 0f; }
            _finale = null;
        }

        private void LightPad(int idx)
        {
            if (_pads == null || idx < 0 || idx >= _pads.Length || _pads[idx] == null || _padAlpha[idx] >= TrailAlpha) return;
            _padAlpha[idx] = TrailAlpha;
            SetPad(idx);
        }

        /// <summary>Removes arrows that were already cleared in a saved run, instantly and without animation.</summary>
        public void RestoreRemoved(IList<int> ids)
        {
            foreach (int id in ids)
            {
                if (!Model.Arrows.ContainsKey(id)) continue;
                foreach (var c in Model.Arrows[id].cells) LightPad(c.x * Model.Height + c.y);      // a resumed board shows the trail of what already left
                var changed = Model.Remove(id);
                RemovedOrder.Add(id);
                if (_views.TryGetValue(id, out var v) && v != null) Destroy(v.gameObject);
                _views.Remove(id);
                foreach (int cid in changed)
                {
                    var cv = ViewOf(cid);
                    if (cv != null) cv.RefreshMechanics(Model.IceLeft(cid), Model.IsLocked(cid), silent: true);
                }
            }
        }

        private void Update()
        {
            if (_rulerUntil > 0f && Time.time >= _rulerUntil)
            {
                _rulerUntil = 0f;
                ClearRuler();
            }
        }
    }
}
