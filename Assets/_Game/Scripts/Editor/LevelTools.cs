#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using CircuitBreaker.Generation;

namespace CircuitBreaker.EditorTools
{
    public static class LevelTools
    {
        [MenuItem("CircuitBreaker/Validate Levels 1-1000")]
        public static void ValidateLevels()
        {
            var sb = new StringBuilder();
            int bad = 0;
            long slowest = 0; int slowLevel = 0;
            System.Threading.Tasks.Parallel.For(1, 1001, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 }, l =>
            {
                var r = LevelValidator.Validate(l);
                lock (sb)
                {
                    if (r.generationMs > slowest) { slowest = r.generationMs; slowLevel = l; }
                    if (!r.Ok) { bad++; sb.AppendLine("L" + l + ": " + r.problem); }
                }
            });
            Debug.Log($"[CircuitBreaker] Validated levels 1-1000: {bad} problems. Slowest L{slowLevel} {slowest} ms.\n{sb}");
        }

        [MenuItem("CircuitBreaker/Reset Save Data")]
        public static void ResetSave()
        {
            CircuitBreaker.Core.SaveSystem.ResetAll();
            Debug.Log("[CircuitBreaker] Save data cleared.");
        }
    }
}
#endif
