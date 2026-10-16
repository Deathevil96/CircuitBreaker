using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using CircuitBreaker.Data;
using CircuitBreaker.Generation;

namespace CircuitBreaker.Core
{
    /// <summary>
    /// Rolling window of ready Classic levels. Only `Capacity` levels exist in memory at a time; the window size is
    /// chosen from the phone's RAM and CPU. Once the player is near the end of the window (90%), older levels are
    /// dropped and the next block is generated in the background, so levels never need a long wait and memory stays flat.
    /// A level is identical whether it came from the cache or was generated on demand (generation is deterministic).
    /// </summary>
    public static class LevelCache
    {
        private const int KeepBehind = 2;                       // allows a quick replay of the last levels
        private static readonly Dictionary<int, LevelData> Items = new Dictionary<int, LevelData>();
        private static readonly object Lock = new object();
        private static CancellationTokenSource _cts;
        private static int _current = 1;
        private static int _windowEnd;                          // exclusive: levels below this are cached or queued
        private static int _capacity;

        public static int Capacity
        {
            get
            {
                if (_capacity == 0) _capacity = ForDevice(SystemInfo.systemMemorySize, SystemInfo.processorCount);
                return _capacity;
            }
        }

        public static int Count { get { lock (Lock) return Items.Count; } }
        public static int WindowStart => Mathf.Max(1, _current - KeepBehind);
        public static int WindowEnd => _windowEnd;

        /// <summary>Window size for a device: small phones keep a few levels, strong ones up to 50.</summary>
        public static int ForDevice(int ramMb, int cores)
        {
            int n = ramMb <= 0 ? 20 : ramMb < 3000 ? 10 : ramMb < 4500 ? 20 : ramMb < 7000 ? 30 : 50;
            if (cores > 0 && cores <= 4) n = Mathf.Min(n, 20);
            return Mathf.Max(5, n);
        }

        public static void OverrideCapacity(int n) { _capacity = Mathf.Max(0, n); }

        public static bool TryGet(int level, out LevelData data)
        {
            lock (Lock) return Items.TryGetValue(level, out data);
        }

        public static void Store(int level, LevelData data)
        {
            lock (Lock) { if (level >= WindowStart) Items[level] = data; }
        }

        /// <summary>The player is on `level`. Slides the window when they get near its end and keeps prefetching ahead.</summary>
        public static void Focus(int level)
        {
            _current = Mathf.Max(1, level);
            int cap = Capacity;
            bool slide = _windowEnd == 0 || _current >= _windowEnd - Mathf.Max(1, Mathf.CeilToInt(cap * 0.1f)) || _current < WindowStart || _current >= _windowEnd;
            if (!slide) return;
            _windowEnd = _current + cap;
            lock (Lock)
            {
                var drop = new List<int>();
                foreach (var k in Items.Keys) if (k < WindowStart || k >= _windowEnd + cap) drop.Add(k);
                foreach (var k in drop) Items.Remove(k);
            }
            Prefetch();
        }

        private static void Prefetch()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            int from = _current, to = _windowEnd;
            Task.Run(() =>
            {
                for (int l = from; l < to && !token.IsCancellationRequested; l++)
                {
                    bool have; lock (Lock) have = Items.ContainsKey(l);
                    if (have) continue;
                    try { Store(l, LevelGenerator.Generate(l)); } catch (System.Exception e) { Debug.LogWarning("[CB] prefetch failed for level " + l + ": " + e.Message); }
                    Thread.Sleep(5);                             // leave the CPU to the game
                }
            }, token);
        }

        public static void Clear()
        {
            _cts?.Cancel();
            lock (Lock) Items.Clear();
            _windowEnd = 0;
        }
    }
}
