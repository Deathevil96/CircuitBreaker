#if UNITY_EDITOR || DEVELOPMENT_BUILD || CB_DEBUG
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CircuitBreaker.Generation;

namespace CircuitBreaker.Core
{
    /// <summary>Background test of a range of levels, with live progress for the debug panel.</summary>
    public static class LevelTester
    {
        public static int From, To, Done, Failures, SlowestLevel, BiggestLevel, BiggestArrows;
        public static long SlowestMs;
        public static double TotalMs;
        public static bool Running, Finished;
        public static float StartedAt;
        public static readonly List<string> Problems = new List<string>();
        private static CancellationTokenSource _cts;
        private static readonly object Lock = new object();

        public static void Start(int from, int to, int degree)
        {
            if (Running) return;
            From = from; To = to; Done = Failures = SlowestLevel = BiggestLevel = BiggestArrows = 0;
            SlowestMs = 0; TotalMs = 0; Finished = false; Running = true;
            StartedAt = UnityEngine.Time.realtimeSinceStartup;
            lock (Lock) Problems.Clear();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            Task.Run(() =>
            {
                try
                {
                    var opts = new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = token };
                    Parallel.For(from, to + 1, opts, level =>
                    {
                        var r = LevelValidator.Validate(level);
                        lock (Lock)
                        {
                            Done++;
                            TotalMs += r.generationMs;
                            if (r.generationMs > SlowestMs) { SlowestMs = r.generationMs; SlowestLevel = level; }
                            if (r.arrows > BiggestArrows) { BiggestArrows = r.arrows; BiggestLevel = level; }
                            if (!r.Ok) { Failures++; if (Problems.Count < 40) Problems.Add("L" + level + ": " + r.problem); }
                        }
                    });
                }
                catch (System.OperationCanceledException) { }
                finally { Running = false; Finished = true; }
            }, token);
        }

        public static void Stop() { _cts?.Cancel(); }

        public static string Summary()
        {
            var sb = new StringBuilder();
            lock (Lock)
            {
                sb.Append(Running ? "RUNNING " : Finished ? "DONE " : "idle ");
                sb.AppendLine($"levels {From}-{To}: {Done}/{To - From + 1}, failures {Failures}");
                if (Done > 0) sb.AppendLine($"avg {TotalMs / Done:F0} ms, slowest L{SlowestLevel} {SlowestMs} ms, biggest L{BiggestLevel} ({BiggestArrows} arrows)");
                foreach (var p in Problems) sb.AppendLine("  " + p);
            }
            return sb.ToString();
        }
    }
}
#endif
