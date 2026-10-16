using System;
using System.Collections.Generic;
using UnityEngine;
using CircuitBreaker.Core;
using CircuitBreaker.Data;
using CircuitBreaker.Fx;

namespace CircuitBreaker.Gameplay
{
    public enum TapResult { None, Escaped, Blocked, Frozen, Extracted, Busy }

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
        private const float EdgePadAlpha = 0.4f, PulseAlpha = 0.95f, PadFadePerSecond = 1.6f;

        /// <summary>Arrows that have left, in order. Saved so a board can be resumed exactly.</summary>
        public readonly List<int> RemovedOrder = new List<int>();

        private SpriteRenderer[] _pads;
        private float[] _padBase, _padAlpha;
        private readonly List<int> _litPads = new List<int>();

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
            _litPads.Clear();
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
            _views.Clear();
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

            // Grid pads: only the silhouette's edge stays visible. Every other pad is hidden and lights up briefly
            // when an arrow slides across it, then fades again.
            _pads = new SpriteRenderer[w * h];
            _padBase = new float[w * h];
            _padAlpha = new float[w * h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (!level.InShape(x, y)) continue;
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1
                                || !level.InShape(x - 1, y) || !level.InShape(x + 1, y) || !level.InShape(x, y - 1) || !level.InShape(x, y + 1);
                    int idx = x * h + y;
                    var pad = new GameObject("Pad").AddComponent<SpriteRenderer>();
                    pad.transform.SetParent(_decor, false);
                    pad.transform.position = GridToWorld(new Vector2Int(x, y));
                    pad.transform.localScale = Vector3.one * (edge ? 0.14f : 0.12f);
                    pad.sprite = ProcSprites.Circle;
                    pad.sortingOrder = -10;
                    _pads[idx] = pad;
                    _padBase[idx] = edge ? EdgePadAlpha : 0f;
                    _padAlpha[idx] = _padBase[idx];
                    SetPad(idx);
                }

            // Dotted outline along the shape boundary. Arrows run through cell centres, so the cell edges are never
            // covered by them: the silhouette stays readable from the first move.
            var corners = new HashSet<long>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (!level.InShape(x, y)) continue;
                    // (dx,dy) = direction to the neighbour; the edge runs perpendicular to it.
                    int[,] dirs = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + dirs[d, 0], ny = y + dirs[d, 1];
                        bool outside = nx < 0 || ny < 0 || nx >= w || ny >= h || !level.InShape(nx, ny);
                        if (!outside) continue;
                        // edge midpoint and its two corners, in half-cell units
                        int mx = 2 * x + dirs[d, 0], my = 2 * y + dirs[d, 1];
                        int ex = dirs[d, 1] != 0 ? 1 : 0, ey = dirs[d, 0] != 0 ? 1 : 0;
                        foreach (var p in new[] { new Vector2Int(mx, my), new Vector2Int(mx - ex, my - ey), new Vector2Int(mx + ex, my + ey) })
                            if (corners.Add(((long)(p.x + 4) << 20) | (uint)(p.y + 4)))
                            {
                                var dot = new GameObject("Outline").AddComponent<SpriteRenderer>();
                                dot.transform.SetParent(_decor, false);
                                dot.transform.position = new Vector3(p.x * 0.5f - (w - 1) * 0.5f - 0.0f, p.y * 0.5f - (h - 1) * 0.5f, 0f);
                                dot.transform.localScale = Vector3.one * 0.09f;
                                dot.sprite = ProcSprites.Circle;
                                var oc = CyberTheme.Pad; oc.a = 0.42f;
                                dot.color = oc;
                                dot.sortingOrder = -9;
                            }
                    }
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
            Collided?.Invoke(def);
            return TapResult.Blocked;
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

        /// <summary>Lights the grid pad nearest to a world point (called for every point of a moving arrow).</summary>
        public void Pulse(Vector3 world)
        {
            if (_pads == null || Model == null) return;
            int x = Mathf.RoundToInt(world.x + (Model.Width - 1) * 0.5f);
            int y = Mathf.RoundToInt(world.y + (Model.Height - 1) * 0.5f);
            if (x < 0 || y < 0 || x >= Model.Width || y >= Model.Height) return;
            int idx = x * Model.Height + y;
            if (_pads[idx] == null) return;
            if (_padAlpha[idx] < PulseAlpha - 0.001f)
            {
                if (_padAlpha[idx] <= _padBase[idx] + 0.001f) _litPads.Add(idx);
                _padAlpha[idx] = PulseAlpha;
                SetPad(idx);
            }
            else _padAlpha[idx] = PulseAlpha;
        }

        /// <summary>Removes arrows that were already cleared in a saved run, instantly and without animation.</summary>
        public void RestoreRemoved(IList<int> ids)
        {
            foreach (int id in ids)
            {
                if (!Model.Arrows.ContainsKey(id)) continue;
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
            if (_litPads.Count > 0)
            {
                float step = PadFadePerSecond * Time.deltaTime;
                for (int i = _litPads.Count - 1; i >= 0; i--)
                {
                    int idx = _litPads[i];
                    _padAlpha[idx] = Mathf.MoveTowards(_padAlpha[idx], _padBase[idx], step * (_padAlpha[idx] > 0.8f ? 0.4f : 1f));
                    SetPad(idx);
                    if (Mathf.Approximately(_padAlpha[idx], _padBase[idx])) _litPads.RemoveAt(i);
                }
            }

            if (_rulerUntil > 0f && Time.time >= _rulerUntil)
            {
                _rulerUntil = 0f;
                ClearRuler();
            }
        }
    }
}
