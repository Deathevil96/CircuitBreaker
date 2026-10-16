using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CircuitBreaker.Ads;
using CircuitBreaker.Data;
using CircuitBreaker.Fx;
using CircuitBreaker.Gameplay;
using CircuitBreaker.Generation;
using CircuitBreaker.UI;
using Debug = UnityEngine.Debug;

namespace CircuitBreaker.Core
{
    public enum GameMode { Classic, Daily, Freeplay, Overload }

    /// <summary>
    /// Game flow for every mode: loading boards, hearts, combo, power-ups, resume, restart ad policy,
    /// stats and achievements. Builds everything it needs at startup.
    /// </summary>
    public partial class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>Tests set this so boards load immediately instead of over several frames.</summary>
        public static bool SynchronousLoading;

        public const float ComboWindow = 2f;
        private const int HintUnlock = Economy.HintUnlock, ExtractUnlock = Economy.ExtractUnlock, RulerUnlock = Economy.RulerUnlock;

        public BoardView Board { get; private set; }
        public CameraController Cam { get; private set; }
        public GameUI UI { get; private set; }
        public LevelData Level { get; private set; }
        public GameMode Mode { get; private set; }
        public int LevelIndex { get; private set; }          // classic level number (or the spec index for other modes)
        public int Hearts { get; private set; }
        public bool InputLocked { get; private set; }
        public string BoardKey { get; private set; } = "";

        private LevelSpec _spec;
        private int _dailyDate;
        private Difficulty _freeTier;
        private bool _replay;                 // classic level below the frontier: no rewards
        private bool _debugForced;            // jumped to from the debug panel: nothing is saved
        private int _maxHearts, _combo, _bestCombo, _heartsLost, _bumps;
        private float _lastEscapeTime;
        private int _levelToken;
        private float _lastGenMs;
        private int _tapCount, _validTaps;
        private bool _finished;               // the current board is won or failed
        private bool _outcomeShown;           // the win/fail screen for this board has been handled

        // session-wide ad pacing (reset every app launch)
        private int _continueAdsUsed;
        private int _restartsThisSession, _restartAdTarget;
        private bool _restartAdShown;
        private string _getMoreId;

        // overload (score attack)
        private bool _ovRunning;
        private int _ovScore, _ovRound, _ovBoardBumps;
        private float _ovTime;
        private const float OvStartTime = 45f, OvBoardBonus = 8f, OvBumpPenalty = 4f, OvEscapeBonus = 0.3f;

        public readonly List<string> LogLines = new List<string>();
        public int Combo => _combo;

        /// <summary>Logs to the Unity console ([CB] prefix) and the on-screen debug panel.</summary>
        public void Log(string message)
        {
            Debug.Log("[CB] " + message);
            LogLines.Add($"{Time.time,6:F1}  {message}");
            if (LogLines.Count > 9) LogLines.RemoveAt(0);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<GameManager>() != null) return;
            new GameObject("[CircuitBreaker]").AddComponent<GameManager>();
        }

        // =====================================================================
        // Startup
        // =====================================================================
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Application.logMessageReceived += OnLogMessage;

            string migration = SaveSystem.Migrate();
            if (migration != null) Log(migration);
#if CB_ADMOB && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            AdService.Init(gameObject.AddComponent<AdMobProvider>());    // real AdMob (test ad units until production IDs are set)
#elif UNITY_EDITOR || DEVELOPMENT_BUILD || CB_TEST_ADS
            AdService.Init(gameObject.AddComponent<StubAdProvider>());   // clearly labelled test ads
#else
            AdService.Init(null);                                         // no SDK wired: no ads, and no ad rewards are granted
#endif
            _restartAdTarget = UnityEngine.Random.Range(2, 4);            // the restart ad appears on the 2nd or 3rd reset

            SetupCamera();
            var sfx = gameObject.AddComponent<Sfx>();
            sfx.Muted = SaveSystem.Muted;

            var boardGo = new GameObject("BoardView");
            boardGo.transform.SetParent(transform, false);
            Board = boardGo.AddComponent<BoardView>();
            Board.Escaped += OnEscaped;
            Board.Collided += OnCollided;
            Board.Extracted += OnExtracted;

            UI = new GameObject("GameUI").AddComponent<GameUI>();
            UI.transform.SetParent(transform, false);
            UI.Build();
            WireUi();

            Cam.Tapped = OnScreenTap;
            Cam.PointerAllowed = UI.PointerInPlayArea;
            Achievements.Unlocked += UI.ShowAchievement;
        }

        private void WireUi()
        {
            UI.BackClicked += OnBack;
            UI.RestartClicked += OnRestartPressed;
            UI.HintClicked += UseHint;
            UI.ExtractClicked += UseExtract;
            UI.RulerClicked += UseRuler;
            UI.FitClicked += () => Cam.FitBoard();

            UI.ContinueClicked += OnContinue;
            UI.DailyClicked += () => UI.ShowDaily();
            UI.DailyDayPicked += StartDaily;
            UI.FreeplayClicked += () => UI.ShowFreeplay();
            UI.FreeplayStartClicked += StartFreeplay;
            UI.OverloadClicked += () => UI.ShowOverloadIntro(Stats.OverloadBest, Stats.OverloadBestDate);
            UI.OverloadStartClicked += StartOverload;
            UI.OverloadAgainClicked += StartOverload;
            UI.OverloadMenuClicked += OpenMenu;
            UI.LevelsClicked += () => UI.ShowLevels(SaveSystem.Progress, SaveSystem.Progress);
            UI.LevelPicked += level => StartClassic(level);
            UI.RecordsClicked += () => UI.ShowRecords();
            UI.SettingsClicked += () => UI.ShowSettings();
            UI.SoundChanged += on => { SaveSystem.Muted = !on; Sfx.Instance.Muted = !on; SaveSystem.Save(); };
            UI.VibrationChanged += on => { SaveSystem.Haptics = on; SaveSystem.Save(); };
            UI.GlowChanged += on => { SaveSystem.Glow = on; SaveSystem.Save(); ApplyGlow(); };
            UI.QuitRequested += QuitGame;
            UI.ResetProgressConfirmed += ResetEverything;

            UI.NextClicked += OnNext;
            UI.ReplayClicked += OnReplay;
            UI.ContinueAdClicked += ContinueWithAd;
            UI.RefillClicked += RefillHearts;
            UI.FailRestartClicked += OnFailRestart;
            UI.GetMoreAdClicked += GetMoreWatchAd;
            UI.GetMoreCoinsClicked += GetMoreBuy;
            UI.GetMoreTick += RefreshGetMore;
        }

        private void Start()
        {
            Stats.RegisterSessionStart();
            string gift = Economy.BeginSession(Clock.Now);
            LoadForContinue();
            OpenMenu();
            Log($"START {BuildInfo.Display}, sessions {SaveSystem.Sessions}, day streak {Stats.DayStreak}");
            if (gift != null) { Log("SESSION GIFT: " + gift); UI.ShowNotice("Welcome back", gift); }
            Achievements.Evaluate();
            SaveSystem.Save();
        }

        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.backgroundColor = Theme.Bg;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0, 0, -10);
            Cam = cam.GetComponent<CameraController>() ?? cam.gameObject.AddComponent<CameraController>();

            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var volGo = new GameObject("PostFX");
            volGo.transform.SetParent(transform, false);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.72f);
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.65f);
            _bloom = bloom;
            vol.sharedProfile = profile;
            ApplyGlow();
        }

        private Bloom _bloom;

        private void ApplyGlow()
        {
            if (_bloom != null) _bloom.active = SaveSystem.Glow;
        }

        private void QuitGame()
        {
            SaveRun();
            SaveSystem.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Android back button arrives as Escape. Overlays close first; in a board it leads to the menu.</summary>
        private void HandleBackKey()
        {
            if (AdService.IsAdShowing) return;
            if (UI.HandleBack()) return;
            OnBack();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) { OnResumedFromBackground(); return; }
            SaveRun();
            SaveSystem.Save();
        }

        /// <summary>Coming back after midnight counts as a new day: streak, daily hint and the calendar all move on.</summary>
        private void OnResumedFromBackground()
        {
            if (Level == null || Clock.Today == Stats.LastPlayDay) return;
            Stats.RegisterSessionStart();
            string gift = Economy.BeginSession(Clock.Now);
            Log($"NEW DAY on resume: day streak {Stats.DayStreak}");
            if (gift != null) { UI.ShowNotice("Welcome back", gift); RefreshBoosters(); }
            Achievements.Evaluate();
            UI.RefreshDaily();
            if (UI.MenuOpen) UI.ShowMenu(BuildMenuData());
            SaveSystem.Save();
        }

        private void OnApplicationQuit()
        {
            SaveRun();
            SaveSystem.Save();
        }

        private void OnDestroy() => Application.logMessageReceived -= OnLogMessage;

        private readonly Queue<string> _errorLog = new Queue<string>();
        private void OnLogMessage(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            _errorLog.Enqueue(condition);
            if (_errorLog.Count > 20) _errorLog.Dequeue();
        }

        // =====================================================================
        // Loading boards
        // =====================================================================
        private static string ClassicKey(int level) => "classic:" + level;

        /// <summary>Resumes the most recent saved board, or starts the frontier level.</summary>
        private void LoadForContinue()
        {
            var run = RunSave.Load();
            if (run != null)
            {
                var parts = run.key.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int id))
                {
                    if (parts[0] == "classic") { StartClassic(id, run); return; }
                    if (parts[0] == "daily") { StartDaily(id, run); return; }
                }
            }
            StartClassic(SaveSystem.Progress);
        }

        public void StartClassic(int level, RunState resume = null, bool force = false)
        {
            level = Mathf.Max(1, level);
            if (!force && level > SaveSystem.Progress) level = SaveSystem.Progress;
            _replay = !force && level < SaveSystem.Progress;
            _debugForced = force;
            if (resume == null && !force)
            {
                var saved = RunSave.LoadFamily("classic");
                if (saved != null && saved.key == ClassicKey(level)) resume = saved;   // picking the level you left resumes it
            }
            Begin(GameMode.Classic, LevelSpec.Classic(level), ClassicKey(level), resume);
        }

        public void StartDaily(int date)
        {
            var saved = RunSave.LoadFamily("daily");
            StartDaily(date, saved != null && saved.key == Daily.KeyFor(date) ? saved : null);
        }

        private void StartDaily(int date, RunState resume)
        {
            _dailyDate = date;
            _replay = false; _debugForced = false;
            UI.HideAllOverlays();
            Begin(GameMode.Daily, Daily.SpecFor(date), Daily.KeyFor(date), resume);
        }

        public void StartFreeplay(Difficulty tier)
        {
            _freeTier = tier;
            _replay = false; _debugForced = false;
            var rng = new System.Random(Environment.TickCount);
            var shapes = ShapeLibrary.Names;
            int refLevel = DifficultyInfo.ReferenceLevel(tier);
            var spec = new LevelSpec
            {
                index = Mathf.Max(6, refLevel + rng.Next(-4, 5)),
                difficulty = tier,
                shape = tier == Difficulty.Easy && rng.Next(3) == 0 ? "Rectangle" : shapes[rng.Next(shapes.Length)],
                salt = rng.Next(1, 1000000)
            };
            UI.HideAllOverlays();
            Begin(GameMode.Freeplay, spec, "free:" + spec.salt, null);
        }

        private LevelSpec OverloadSpec(int round)
        {
            var tier = round <= 2 ? Difficulty.Easy : round <= 5 ? Difficulty.Medium : round <= 8 ? Difficulty.Hard : round <= 11 ? Difficulty.SuperHard : Difficulty.UltraHard;
            var shapes = ShapeLibrary.Names;
            return new LevelSpec
            {
                index = Mathf.Min(120, 6 + round * 5),
                difficulty = tier,
                shape = round <= 2 ? "Rectangle" : shapes[(round * 7) % shapes.Length],
                salt = 9000 + round * 31 + Stats.OverloadBest % 97
            };
        }

        public void StartOverload()
        {
            _replay = false; _debugForced = false;
            UI.HideAllOverlays();
            _ovScore = 0; _ovRound = 1; _ovTime = OvStartTime; _ovRunning = true; _ovBoardBumps = 0;
            Begin(GameMode.Overload, OverloadSpec(1), "overload:1", null);
        }

        /// <summary>
        /// The single place a board is built, for every mode. Generation runs on a worker thread so large
        /// boards never freeze the interface; input stays locked until the board is ready.
        /// </summary>
        private void Begin(GameMode mode, LevelSpec spec, string key, RunState resume)
        {
            _levelToken++;
            StopAllCoroutines();
            UI.HideAllOverlays();
            Mode = mode;
            _spec = spec;
            BoardKey = key;
            LevelIndex = spec.index;
            _finished = false;
            InputLocked = true;
            _outcomeShown = false;
            UI.HoldBanner("");
            if (mode != GameMode.Overload) _ovRunning = false;
            var routine = BeginRoutine(mode, spec, key, resume, _levelToken);
            if (SynchronousLoading) { while (routine.MoveNext()) System.Threading.Thread.Sleep(1); }
            else StartCoroutine(routine);
        }

        private IEnumerator BeginRoutine(GameMode mode, LevelSpec spec, string key, RunState resume, int token)
        {
            var clock = Stopwatch.StartNew();
            bool classic = mode == GameMode.Classic && spec.shape == null && spec.salt == 0;
            LevelData generated;
            if (classic && LevelCache.TryGet(spec.index, out generated)) { /* ready in the rolling window */ }
            else
            {
                float shown = 0f;
                var task = System.Threading.Tasks.Task.Run(() => LevelGenerator.Generate(spec));
                while (!task.IsCompleted)
                {
                    shown += Time.unscaledDeltaTime;
                    if (shown > 0.25f) UI.ShowLoading("Building level " + spec.index);   // only for slow waits
                    yield return null;
                }
                if (token != _levelToken) { UI.HideLoading(); yield break; }
                if (task.IsFaulted)
            {
                    Debug.LogError("[CB] level generation failed, retrying on the main thread: " + task.Exception?.GetBaseException().Message);
                    generated = LevelGenerator.Generate(spec);
                }
                else generated = task.Result;
                if (classic) LevelCache.Store(spec.index, generated);
            }
            if (token != _levelToken) { UI.HideLoading(); yield break; }
            UI.HideLoading();
            if (classic) LevelCache.Focus(spec.index);
            _lastGenMs = (float)clock.Elapsed.TotalMilliseconds;

            Level = generated;
            if (mode == GameMode.Overload) Level.hearts = 0;      // bumps cost time, not hearts
            _tapCount = _validTaps = _bumps = 0;

            _maxHearts = Level.hearts > 0 ? Level.hearts : 3;
            Hearts = _maxHearts;
            _combo = 0; _bestCombo = 0; _heartsLost = 0;
            InputLocked = false;
            Cam.InputEnabled = true;

            Board.Build(Level);
            Cam.Frame(Level.width, Level.height);

            if (resume != null && resume.key == key)
            {
                Board.RestoreRemoved(resume.removed);
                Hearts = Mathf.Clamp(resume.hearts, 0, _maxHearts);
                _heartsLost = resume.heartsLost; _bumps = resume.bumps; _bestCombo = resume.bestCombo;
                Log($"RESUME {key}: {Board.Model.Remaining}/{Level.arrows.Count} arrows left, hearts {Hearts}");
            }
            else if (mode == GameMode.Classic || mode == GameMode.Daily)
            {
                // Only restarting the very board that was saved discards that run. Replays, debug boards and other
                // modes leave a saved run alone, so Continue always brings the player back to where they left.
                var existing = RunSave.LoadFamily(RunSave.FamilyOf(key));
                if (existing != null && existing.key == key) RunSave.ClearFamily(RunSave.FamilyOf(key));
            }

            ApplyHeader();
            UI.SetOverloadMode(mode == GameMode.Overload);
            UI.SetTopButtonsVisible(true);
            UI.SetHearts(Hearts, _maxHearts, Level.hearts == 0 || mode == GameMode.Overload);
            UI.SetCoins(SaveSystem.Coins);
            UI.HoldBanner("");
            UI.ShowCombo(0);
            RefreshBoosters();
            if (mode == GameMode.Overload) UI.SetOverload(_ovScore, _ovTime, OvStartTime);

            Log($"LOAD {key} [{mode}]: {Level.shape} {Level.width}x{Level.height}, {Level.arrows.Count} arrows, depth {Level.depth}, seed {Level.seed}, gen {_lastGenMs:F0} ms, {DifficultyInfo.Name((Difficulty)Level.difficulty)}");

            if (mode == GameMode.Classic && !_debugForced && !_replay && resume == null) ClassicLevelIntro();
            // A menu opened during loading stays on top; the board simply finishes behind it.
        }

        private void ApplyHeader()
        {
            var diff = (Difficulty)Level.difficulty;
            switch (Mode)
            {
                case GameMode.Classic:
                {
                    string tier = DifficultyInfo.Name(diff);
                    UI.SetHeader("Level " + LevelIndex, diff, _replay ? tier + "  ·  replay" : tier);
                    break;
                }
                case GameMode.Daily:
                    UI.SetHeader("Daily Run", diff, Clock.FromInt(_dailyDate).ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture) + "  ·  " + DifficultyInfo.Name(diff));
                    break;
                case GameMode.Freeplay:
                    UI.SetHeader("Freeplay", diff, DifficultyInfo.Name(diff) + "  ·  " + Level.shape);
                    break;
                default:
                    UI.SetHeader("Overload", null, "Board " + _ovRound);
                    break;
            }
        }

        private void ClassicLevelIntro()
        {
            string unlock = Economy.CheckUnlockGift(LevelIndex);
            if (unlock != null) { Log("UNLOCK GIFT: " + unlock); RefreshBoosters(); UI.ShowNotice("Unlocked", unlock); }
            var diff = LevelCatalog.DifficultyOf(LevelIndex);
            if (DifficultyInfo.IsMilestone(diff)) UI.Toast(DifficultyInfo.Name(diff) + " level. Clear it to earn " + Economy.TierRewardText(diff) + ".", 4.5f);
            if (LevelIndex <= 3) Board.ShowHint(out _, 3600f);       // pulse the best first move until it is used
            if (LevelIndex == 1) UI.Toast("Tap an arrow with a clear path to send it out.", 4f);
            else if (LevelIndex == 2) UI.Toast("A blocked arrow bumps. Free the ones in front first.", 4f);
            else if (LevelIndex == 21) UI.Toast("From here boards take a shape. Arrows leave through the open space.", 4f);
            else if (LevelIndex == 20) UI.Toast("Ice frees after N other arrows have escaped.", 4f);
            else if (LevelIndex == 35) UI.Toast("Key arrows (ringed) unlock the padlocked arrow.", 4f);
        }

        // =====================================================================
        // Menu, back, continue
        // =====================================================================
        public void OpenMenu()
        {
            SaveRun();
            UI.HideAllOverlays();
            Board.HideRuler();
            UI.ShowMenu(BuildMenuData());
            SaveSystem.Save();
        }

        private MenuData BuildMenuData()
        {
            var d = new MenuData { streak = Stats.DayStreak, coins = SaveSystem.Coins };
            var run = RunSave.Load();
            if (run != null)
            {
                d.continueTitle = "Continue";
                d.continueSub = DescribeKey(run.key);
            }
            else
            {
                d.continueTitle = SaveSystem.Progress <= 1 ? "Play" : "Continue";
                int lvl = SaveSystem.Progress;
                d.continueSub = "Level " + lvl + "  ·  " + DifficultyInfo.Name(LevelCatalog.DifficultyOf(lvl));
            }
            int today = Clock.Today;
            int ty = today / 10000, tm = (today / 100) % 100;
            d.dailySub = Daily.IsCleared(today) ? "Today cleared  ·  " + Daily.ClearedInMonth(ty, tm) + " of " + Daily.DaysInMonth(ty, tm) + " this month"
                : "Today  ·  " + DifficultyInfo.Name(Daily.TierFor(today)) + "  ·  " + Daily.ClearedInMonth(ty, tm) + " of " + Daily.DaysInMonth(ty, tm) + " this month";
            d.freeplaySub = "Pick a difficulty, play endlessly";
            d.overloadSub = Stats.OverloadBest > 0 ? "Score attack  ·  best " + Stats.OverloadBest.ToString("N0") : "Score attack against the clock";
            return d;
        }

        private static string DescribeKey(string key)
        {
            var p = key.Split(':');
            if (p.Length == 2 && int.TryParse(p[1], out int id))
            {
                if (p[0] == "classic") return "Level " + id + "  ·  " + DifficultyInfo.Name(LevelCatalog.DifficultyOf(id)) + "  ·  in progress";
                if (p[0] == "daily") return "Daily Run  ·  " + Clock.Format(id) + "  ·  in progress";
            }
            return "In progress";
        }

        private void OnContinue()
        {
            var run = RunSave.Load();
            string targetKey = run != null ? run.key : ClassicKey(SaveSystem.Progress);
            UI.HideMenu();
            // Already showing that exact board (and it is still live): just resume.
            if (BoardKey == targetKey && !_finished && Mode != GameMode.Overload) { Board.HideRuler(); return; }
            LoadForContinue();
        }

        private void OnBack()
        {
            if (UI.ModalOpen) return;
            if (Mode == GameMode.Overload && _ovRunning)
            {
                // Leaving a score attack abandons it, without a result screen.
                _ovRunning = false;
                Log("OVERLOAD abandoned");
            }
            OpenMenu();
        }

        // =====================================================================
        // Restart (with the once-per-session restart ad)
        // =====================================================================
        private void OnRestartPressed()
        {
            if (UI.ModalOpen || Mode == GameMode.Overload) return;
            RestartBoard(countAttempt: true);
        }

        private void OnFailRestart()
        {
            UI.HideSheets();
            RestartBoard(countAttempt: false);       // the failure already counted as an attempt
        }

        private void RestartBoard(bool countAttempt)
        {
            bool progressed = Board.RemovedOrder.Count > 0 || _bumps > 0;
            if (countAttempt && progressed && Mode == GameMode.Classic && !_replay && !_debugForced)
            {
                Stats.AddAttempt(LevelIndex);
                Log($"ATTEMPT {Stats.Attempts(LevelIndex)} on level {LevelIndex} (restart)");
            }

            _restartsThisSession++;
            bool showAd = ShouldShowRestartAd();
            Log($"RESTART #{_restartsThisSession} (ad target {_restartAdTarget}, ad shown {_restartAdShown}) -> {(showAd ? "AD" : "no ad")}");
            Action reset = () => Begin(Mode, _spec, BoardKey, null);
            if (showAd)
            {
                _restartAdShown = true;
                InputLocked = true;
                AdService.ShowInterstitial("restart", () => { InputLocked = false; reset(); });
            }
            else reset();
        }

        /// <summary>The restart ad appears once per session, on the 2nd or 3rd reset, never in the tutorial.</summary>
        private bool ShouldShowRestartAd()
        {
            if (_restartAdShown || _restartsThisSession < _restartAdTarget) return false;
            if (Mode == GameMode.Classic && LevelIndex <= 5) return false;
            return AdService.InterstitialReady;
        }

        // =====================================================================
        // Taps and events
        // =====================================================================
        private void OnScreenTap(Vector2 screen)
        {
            if (InputLocked || UI.ModalOpen) return;
            var world = Cam.ScreenToWorld(screen);
            var result = Board.Tap(world);
            _tapCount++;
            Log($"TAP #{_tapCount} -> {result}");
            if (result == TapResult.None) { UI.HoldBanner(""); RefreshBoosters(); }
        }

        private void OnEscaped(ArrowDef arrow, int remaining)
        {
            _combo = Time.time - _lastEscapeTime <= ComboWindow ? _combo + 1 : 1;
            _lastEscapeTime = Time.time;
            _bestCombo = Mathf.Max(_bestCombo, _combo);
            _validTaps++;
            Sfx.Blip(Mathf.Min(1.6f, 1f + 0.05f * _combo));
            UI.ShowCombo(_combo);

            if (Mode == GameMode.Overload)
            {
                int mult = 1 + Mathf.Min(4, _combo / 3);
                _ovScore += 10 * mult;
                _ovTime += OvEscapeBonus;
                if (remaining <= 0) StartCoroutine(OverloadBoardCleared());
                return;
            }
            SaveRun();
            if (remaining <= 0) StartCoroutine(WinRoutine());
        }

        private void OnExtracted(ArrowDef arrow)
        {
            Sfx.Hint();
            UI.HoldBanner("");
            RefreshBoosters();
            SaveRun();
            if (Board.Model.Remaining <= 0) StartCoroutine(WinRoutine());
        }

        private void OnCollided(ArrowDef arrow)
        {
            _combo = 0;
            _bumps++;
            UI.ShowCombo(0);
            Haptic();
            if (Mode == GameMode.Overload)
            {
                _ovTime = Mathf.Max(0f, _ovTime - OvBumpPenalty);
                _ovBoardBumps++;
                return;
            }
            if (Level.hearts == 0) return;      // tutorial: bumps teach, they don't cost
            Hearts = Mathf.Max(0, Hearts - 1);
            _heartsLost++;
            UI.SetHearts(Hearts, _maxHearts, false);
            SaveRun();
            if (Hearts <= 0) StartCoroutine(FailRoutine());
        }

        // =====================================================================
        // Winning
        // =====================================================================
        private IEnumerator WinRoutine()
        {
            InputLocked = true;
            _finished = true;
            int token = _levelToken;
            yield return new WaitForSeconds(0.75f);
            if (token != _levelToken) yield break;
            FinishWin();
        }

        /// <summary>Applies a win: rewards, stats, achievements and the result screen. Runs once per board.</summary>
        public void FinishWin()
        {
            if (_outcomeShown) return;
            _outcomeShown = true;
            InputLocked = true;
            _finished = true;

            bool perfect = _heartsLost == 0;
            var diff = (Difficulty)Level.difficulty;
            var win = new WinData();
            win.rows.Add(new KeyValuePair<string, string>("Best combo", "x" + _bestCombo));
            win.rows.Add(new KeyValuePair<string, string>("Depth", Level.depth.ToString()));
            Log($"WIN {BoardKey}: bumps {_bumps}, best combo {_bestCombo}, perfect {perfect}");

            switch (Mode)
            {
                case GameMode.Classic: WinClassic(win, perfect, diff); break;
                case GameMode.Daily: WinDaily(win); break;
                default: WinFreeplay(win); break;
            }

            Sfx.Win();
            UI.SetCoins(SaveSystem.Coins);
            RefreshBoosters();
            UI.ShowWin(win);
            var fresh = Achievements.Evaluate();
            foreach (var a in fresh) Log("ACHIEVEMENT " + a.title);
            SaveSystem.Save();
        }

        private void WinClassic(WinData win, bool perfect, Difficulty diff)
        {
            win.subtitle = "Level " + LevelIndex + "  ·  " + DifficultyInfo.Name(diff);
            win.title = perfect ? "Perfect clear" : "Level complete";
            RunSave.ClearFamily("classic");

            if (_debugForced || _replay)
            {
                win.title = _replay ? "Replay complete" : "Debug level complete";
                win.rewards.Add(_replay ? "No rewards for replays" : "Nothing saved");
                win.primaryText = "Next level";
                return;
            }

            bool firstTry = Stats.Attempts(LevelIndex) == 0;
            Stats.RegisterWin(LevelIndex, diff, perfect, firstTry);
            var r = Economy.ResolveWin(LevelIndex, perfect, _bestCombo);
            SaveSystem.BestCombo = _bestCombo;
            SaveSystem.Progress = LevelIndex + 1;
            Stats.ClearAttempts(LevelIndex);
            win.rows.Insert(0, new KeyValuePair<string, string>("Coins", "+" + r.coins));
            if (firstTry) win.rows.Add(new KeyValuePair<string, string>("First attempt", "Yes"));
            win.rewards.AddRange(r.lines);
            if (DifficultyInfo.IsMilestone(diff)) win.rewards.Insert(0, DifficultyInfo.Name(diff) + " level cleared");
            Log($"REWARDS coins {r.coins}, hints +{r.hints}, rulers +{r.rulers}, extracts +{r.extracts} | firstTry {firstTry}, streak {SaveSystem.PerfectStreak}");
        }

        private void WinDaily(WinData win)
        {
            win.title = "Daily Run complete";
            win.subtitle = Clock.FromInt(_dailyDate).ToString("d MMMM", System.Globalization.CultureInfo.InvariantCulture);
            RunSave.ClearFamily("daily");
            win.primaryText = "Back to calendar";
            win.secondaryText = "Play again";
            if (Daily.IsCleared(_dailyDate)) { win.rewards.Add("Already cleared. No new reward."); return; }

            bool monthDone = Daily.MarkCleared(_dailyDate);
            Stats.RegisterModeWin(daily: true);
            int added = SaveSystem.AddStock("hint", 1, Economy.HintCap);
            int coins = 30 + (added == 0 ? Economy.OverflowCoins("hint") : 0);
            SaveSystem.Coins += coins;
            win.rows.Insert(0, new KeyValuePair<string, string>("Coins", "+" + coins));
            if (added > 0) win.rewards.Add("+1 hint");
            if (monthDone)
            {
                SaveSystem.Coins += 200;
                SaveSystem.AddStock("hint", 2, Economy.HintCap);
                win.rewards.Add("Monthly badge earned: +200 coins, +2 hints");
            }
            UI.RefreshDaily();
        }

        private void WinFreeplay(WinData win)
        {
            win.title = "Board cleared";
            win.subtitle = "Freeplay  ·  " + DifficultyInfo.Name(_freeTier);
            win.primaryText = "New board";
            win.secondaryText = "Menu";
            Stats.RegisterModeWin(daily: false);
            win.rewards.Add("Freeplay does not give rewards");
        }

        private void OnNext()
        {
            UI.HideSheets();
            switch (Mode)
            {
                case GameMode.Classic: StartClassic(Mathf.Min(LevelIndex + 1, Mathf.Max(SaveSystem.Progress, 1)), null, _debugForced); break;
                case GameMode.Daily: OpenMenuThenDaily(); break;
                case GameMode.Freeplay: StartFreeplay(_freeTier); break;
            }
        }

        private void OpenMenuThenDaily() { OpenMenu(); UI.ShowDaily(); }

        private void OnReplay()
        {
            UI.HideSheets();
            if (Mode == GameMode.Freeplay) { OpenMenu(); return; }
            Begin(Mode, _spec, BoardKey, null);
        }

        // =====================================================================
        // Failing, continue, refill
        // =====================================================================
        private IEnumerator FailRoutine()
        {
            InputLocked = true;
            _finished = true;
            int token = _levelToken;
            yield return new WaitForSeconds(0.7f);
            if (token != _levelToken) yield break;
            FinishFail();
        }

        /// <summary>Applies a failure: stats, attempt counting and the fail screen. Runs once per board.</summary>
        public void FinishFail()
        {
            if (_outcomeShown) return;
            _outcomeShown = true;
            InputLocked = true;
            _finished = true;
            Stats.RegisterLoss();
            if (Mode == GameMode.Classic && !_replay && !_debugForced) Stats.AddAttempt(LevelIndex);
            RunSave.ClearFamily(RunSave.FamilyOf(BoardKey));
            Log($"FAIL {BoardKey}: out of hearts after {_bumps} bumps (win streak reset)");
            ShowFailModal();
            SaveSystem.Save();
        }

        private void ShowFailModal()
        {
            string label;
            bool enabled;
            if (_continueAdsUsed >= Economy.ContinueAdsPerSession) { label = "Ad used this session"; enabled = false; }
            else if (!AdService.RewardedReady) { label = "Ad unavailable"; enabled = false; }
            else { label = "Watch ad  ·  +1 heart"; enabled = true; }
            UI.ShowFail(SaveSystem.Coins, Economy.RefillCost, label, enabled);
        }

        private void ContinueWithAd()
        {
            if (_continueAdsUsed >= Economy.ContinueAdsPerSession || !AdService.RewardedReady) { ShowFailModal(); return; }
            AdService.ShowRewarded("continue_heart", ok =>
            {
                if (!ok) { UI.Toast("Ad not completed. No reward."); ShowFailModal(); return; }
                _continueAdsUsed++;
                Log("AD REWARD: +1 heart (continue), used this session " + _continueAdsUsed);
                GrantHearts(1);
            });
        }

        private void RefillHearts()
        {
            if (SaveSystem.Coins < Economy.RefillCost) return;
            SaveSystem.Coins -= Economy.RefillCost;
            SaveSystem.Save();
            UI.SetCoins(SaveSystem.Coins);
            GrantHearts(_maxHearts);
        }

        private void GrantHearts(int n)
        {
            Hearts = Mathf.Min(_maxHearts, Hearts + n);
            _finished = false;
            _outcomeShown = false;
            InputLocked = false;
            UI.HideSheets();
            UI.SetHearts(Hearts, _maxHearts, false);
            SaveRun();
        }

        // =====================================================================
        // Resume
        // =====================================================================
        private void SaveRun()
        {
            if (Level == null || _finished || _debugForced) return;
            if (Mode != GameMode.Classic && Mode != GameMode.Daily) return;
            if (Mode == GameMode.Classic && _replay) return;                                  // replays are never saved
            if (Board.RemovedOrder.Count == 0 && _bumps == 0 && _heartsLost == 0) return;   // nothing to resume yet
            RunSave.Save(new RunState
            {
                key = BoardKey, hearts = Hearts, heartsLost = _heartsLost, bumps = _bumps, bestCombo = _bestCombo,
                removed = new List<int>(Board.RemovedOrder)
            });
        }

        // =====================================================================
        // Overload (score attack)
        // =====================================================================
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) HandleBackKey();
            if (!_ovRunning || Mode != GameMode.Overload || InputLocked || UI.ModalOpen) return;
            _ovTime -= Time.deltaTime * (1f + 0.12f * (_ovRound - 1));       // the clock speeds up every board
            UI.SetOverload(_ovScore, _ovTime, OvStartTime);
            if (_ovTime <= 0f) EndOverload();
        }

        private IEnumerator OverloadBoardCleared()
        {
            InputLocked = true;
            int token = _levelToken;
            yield return new WaitForSeconds(0.55f);
            if (token != _levelToken || !_ovRunning) yield break;
            AdvanceOverloadBoard();
        }

        public void AdvanceOverloadBoard()
        {
            if (!_ovRunning) return;
            int bonus = 100 * _ovRound + (_ovBoardBumps == 0 ? 100 : 0);
            _ovScore += bonus;
            _ovTime += OvBoardBonus;
            Log($"OVERLOAD board {_ovRound} cleared: +{bonus}, score {_ovScore}, time {_ovTime:F1}");
            _ovRound++;
            _ovBoardBumps = 0;
            Begin(GameMode.Overload, OverloadSpec(_ovRound), "overload:" + _ovRound, null);
        }

        public void EndOverload()
        {
            _ovRunning = false;
            InputLocked = true;
            _finished = true;
            int rounds = _ovRound - 1;
            bool record = Stats.RegisterOverload(_ovScore, rounds);
            int coins = _ovScore / 200;
            SaveSystem.Coins += coins;
            Stats.RegisterModeWin(daily: false);     // an Overload run counts toward wins and the win streak
            Log($"OVERLOAD over: score {_ovScore}, boards {rounds}, record {record}");
            UI.SetCoins(SaveSystem.Coins);
            UI.ShowOverloadResult(_ovScore, rounds, record, Stats.OverloadBest, coins);
            Achievements.Evaluate();
            SaveSystem.Save();
        }

        // =====================================================================
        // Power-ups
        // =====================================================================
        private void RefreshBoosters()
        {
            int frontier = SaveSystem.Progress;
            UI.SetPower("hint", SaveSystem.Stock("hint"), frontier < HintUnlock, HintUnlock);
            UI.SetPower("extract", SaveSystem.Stock("extract"), frontier < ExtractUnlock, ExtractUnlock, Board.ExtractMode);
            UI.SetPower("ruler", SaveSystem.Stock("ruler"), frontier < RulerUnlock, RulerUnlock, Board != null && Board.RulerActive);
        }

        private bool PowerAllowed(int unlock)
        {
            if (InputLocked || UI.ModalOpen || Mode == GameMode.Overload) return false;
            if (SaveSystem.Progress < unlock) { UI.Toast("Unlocks at level " + unlock); return false; }
            return true;
        }

        public void UseHint()
        {
            if (!PowerAllowed(HintUnlock)) return;
            if (SaveSystem.Stock("hint") <= 0) { OpenGetMore("hint"); return; }
            if (Board.Model.FindBestHint(out int unlocks) == null)
            {
                UI.Toast("No clear move. Wait for ice, or find the key.");
                return;
            }
            SaveSystem.TakeStock("hint");
            SaveSystem.Save();
            var view = Board.ShowHint(out unlocks);
            if (view != null) Cam.FocusOn(view.CenterWorld);
            Sfx.Hint();
            UI.Toast(unlocks > 0 ? "Best move: unlocks " + unlocks : "A safe move", 2.4f);
            RefreshBoosters();
        }

        public void UseExtract()
        {
            if (!PowerAllowed(ExtractUnlock)) return;
            if (Board.ExtractMode)
            {
                Board.ExtractMode = false;
                UI.HoldBanner("");
                RefreshBoosters();
                return;
            }
            if (SaveSystem.Stock("extract") <= 0) { OpenGetMore("extract"); return; }
            SaveSystem.TakeStock("extract");
            SaveSystem.Save();
            Board.ExtractMode = true;
            UI.HoldBanner("Tap any arrow to extract it");
            RefreshBoosters();
        }

        public void UseRuler()
        {
            if (!PowerAllowed(RulerUnlock)) return;
            if (Board.RulerActive) { Board.HideRuler(); RefreshBoosters(); return; }
            if (SaveSystem.Stock("ruler") <= 0) { OpenGetMore("ruler"); return; }
            SaveSystem.TakeStock("ruler");
            SaveSystem.Save();
            Board.ShowRuler(15f);
            UI.Toast("Green: clear path. Red: blocked.", 3f);
            RefreshBoosters();
            StartCoroutine(RefreshAfter(15.2f));
        }

        private IEnumerator RefreshAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            RefreshBoosters();
        }

        // ----- get more (rewarded ad / coins) -----
        private void OpenGetMore(string id)
        {
            _getMoreId = id;
            Log("GET MORE opened for " + id);
            UI.ShowGetMore("Out of " + id + "s", "", null, false, "", false);
            RefreshGetMore();
        }

        private void RefreshGetMore()
        {
            if (_getMoreId == null || !UI.GetMoreOpen) return;
            string id = _getMoreId;
            int price = Economy.Price(id, SaveSystem.Progress);
            bool canBuy = SaveSystem.Coins >= price && SaveSystem.Stock(id) < Economy.Cap(id) + Economy.PurchaseOverCap;
            string coinLabel = $"Buy 1  ·  {price} coins";

            string body, adLabel = null;
            bool adOn = false;
            if (id == "hint")
            {
                var st = Economy.HintAdStatus(out int cd);
                if (st == AdStatus.Ready) { adLabel = $"Watch ad  ·  +{Economy.AdHintAmount} hints"; adOn = true; }
                else if (st == AdStatus.Cooldown) adLabel = $"Next ad in {cd / 60}:{cd % 60:00}";
                else adLabel = "Ads unavailable";
                body = "Hints show the best next move. You also earn them from Hard levels and your daily bonus.";
            }
            else if (id == "ruler") body = "The ruler shows every arrow's path for 15 seconds. It drops from perfect clears and higher difficulties.";
            else body = "Extract removes any arrow instantly. Earned from perfect streaks and the hardest levels.";
            UI.UpdateGetMore(body, adLabel, adOn, coinLabel, canBuy);
        }

        private void GetMoreWatchAd()
        {
            if (_getMoreId != "hint") return;
            if (Economy.HintAdStatus(out _) != AdStatus.Ready) { RefreshGetMore(); return; }
            AdService.ShowRewarded("hint", ok =>
            {
                if (!ok) { UI.Toast("Ad not completed. No reward."); RefreshGetMore(); return; }
                SaveSystem.LastHintAdUtc = DateTime.UtcNow;
                int added = SaveSystem.AddStock("hint", Economy.AdHintAmount, Economy.HintCap);
                SaveSystem.Save();
                Log($"AD REWARD: +{added} hint(s), next ad in {Economy.AdCooldownSeconds}s");
                RefreshBoosters();
                UI.CloseGetMore();
                UI.Toast($"+{added} hints. Next ad in {Economy.AdCooldownSeconds / 60} min.", 3f);
            });
        }

        private void GetMoreBuy()
        {
            if (_getMoreId == null) return;
            if (Economy.Buy(_getMoreId, SaveSystem.Progress))
            {
                Log("BOUGHT 1 " + _getMoreId);
                UI.SetCoins(SaveSystem.Coins);
                RefreshBoosters();
                RefreshGetMore();
                UI.Toast("Bought 1 " + _getMoreId, 2f);
            }
        }

        // =====================================================================
        // Settings
        // =====================================================================
        public void ResetEverything()
        {
            SaveSystem.ResetAll();
            RunSave.ClearAll();
            Clock.Override = null;
            _continueAdsUsed = 0;
            Log("ALL PROGRESS RESET");
            Stats.RegisterSessionStart();
            StartClassic(1);
            OpenMenu();
        }

        private static void Haptic()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (SaveSystem.Haptics) Handheld.Vibrate();
#endif
        }
    }
}
