using System.Collections.Generic;
using System.Text;

namespace CircuitBreaker.Core
{
    /// <summary>A board in progress, saved so the player can leave to the menu and continue exactly where they were.</summary>
    public class RunState
    {
        public string key;                 // "classic:24" or "daily:20261016"
        public int hearts, heartsLost, bumps, bestCombo;
        public List<int> removed = new List<int>();   // arrows already gone, in order
        public string Family => RunSave.FamilyOf(key);
    }

    /// <summary>
    /// One slot per family (classic, daily) so starting a Daily Run never wipes a classic board in progress.
    /// "Continue" resumes the family that was saved most recently.
    /// </summary>
    public static class RunSave
    {
        public static string FamilyOf(string key)
        {
            int i = key == null ? -1 : key.IndexOf(':');
            return i < 0 ? key : key.Substring(0, i);
        }

        public static void Save(RunState r)
        {
            var sb = new StringBuilder();
            sb.Append(BuildInfo.GeneratorVersion).Append('|').Append(r.key).Append('|')
              .Append(r.hearts).Append('|').Append(r.heartsLost).Append('|').Append(r.bumps).Append('|').Append(r.bestCombo).Append('|')
              .Append(string.Join(",", r.removed));
            SaveSystem.SetString("run_" + r.Family, sb.ToString());
            SaveSystem.SetString("run_last", r.Family);
        }

        /// <summary>Returns null when there is no run, or when it was made by a different level generator.</summary>
        public static RunState LoadFamily(string family)
        {
            string s = SaveSystem.GetString("run_" + family, "");
            if (string.IsNullOrEmpty(s)) return null;
            var p = s.Split('|');
            if (p.Length < 7) return null;
            if (!int.TryParse(p[0], out int gen) || gen != BuildInfo.GeneratorVersion) return null;
            var r = new RunState { key = p[1] };
            int.TryParse(p[2], out r.hearts); int.TryParse(p[3], out r.heartsLost);
            int.TryParse(p[4], out r.bumps); int.TryParse(p[5], out r.bestCombo);
            if (p[6].Length > 0)
                foreach (var t in p[6].Split(',')) if (int.TryParse(t, out int id)) r.removed.Add(id);
            return r;
        }

        /// <summary>The most recently saved run, or null.</summary>
        public static RunState Load()
        {
            string last = SaveSystem.GetString("run_last", "");
            var r = string.IsNullOrEmpty(last) ? null : LoadFamily(last);
            if (r != null) return r;
            foreach (var fam in new[] { "classic", "daily" }) { r = LoadFamily(fam); if (r != null) return r; }
            return null;
        }

        public static bool Has(string key) { var r = LoadFamily(FamilyOf(key)); return r != null && r.key == key; }
        public static void ClearFamily(string family) => SaveSystem.DeleteKey("run_" + family);
        public static void ClearAll() { ClearFamily("classic"); ClearFamily("daily"); SaveSystem.DeleteKey("run_last"); }
    }
}
