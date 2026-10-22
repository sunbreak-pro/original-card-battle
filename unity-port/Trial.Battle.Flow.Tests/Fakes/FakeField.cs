// A stand-in for the field module (field.js, IField) for the player's flow tests: figures on fixed x by side and unit, a
// figure is "away" from its home from a lunge or an enemy motion until it is sent home (returnHome / snapHome, at once),
// and every beat resolves after the ms the player hands it on env.Clock. It fills env.Geom's field slots and records
// the calls the tests count (lunges, returns, falls, floats, bumps, free-step marks).
using System;
using System.Collections.Generic;
using System.Linq;
using Depiction;

namespace Trial.Battle.Flow.Tests.Fakes
{
    public sealed class FakeField : IField
    {
        private readonly BattleEnv _env;
        private readonly HashSet<(UnitSide, int)> _away = new HashSet<(UnitSide, int)>();
        private readonly HashSet<(UnitSide, int)> _down = new HashSet<(UnitSide, int)>();
        private readonly HashSet<(UnitSide, int)> _glow = new HashSet<(UnitSide, int)>();
        private List<int> _freeDirs = new List<int>();

        public FakeField(BattleEnv env)
        {
            _env = env;
            env.Geom.Figure = (side, unit) => FigureOf(side, unit);
            env.Geom.Receiver = unit => new ScreenBox(XOf(UnitSide.Enemy, unit) - 170, 220, 340, 340);
            env.Geom.Receivers = () => Array.Empty<ReceiverBox>();
            env.Geom.CellBox = cell => cell > 0 ? new StageBox(320 + 160 * (cell - 1), 560, 160, 40) : (StageBox?)null;
            env.Geom.FreeStepBoxes = () => Array.Empty<FreeStepBox>();
        }

        public readonly List<(UnitSide Side, int Unit, float At)> Lunges = new List<(UnitSide, int, float)>();
        public readonly List<(UnitSide Side, int Unit, float At)> Returns = new List<(UnitSide, int, float)>();
        public readonly List<(UnitSide Side, int Unit, bool KeepFoot)> Falls = new List<(UnitSide, int, bool)>();
        public readonly List<(string Text, FxRole Role, float Size, FloatOpts O)> Floats = new List<(string, FxRole, float, FloatOpts)>();
        public readonly List<(UnitSide Side, int Dir)> Bumps = new List<(UnitSide, int)>();
        /// <summary>The scene time of every bump.</summary>
        public readonly List<float> BumpAt = new List<float>();
        /// <summary>Every ghost flown (a played card to its pile, the discard pile onto the draw pile) with its scene time.</summary>
        public readonly List<(GhostKind Kind, float At)> Ghosts = new List<(GhostKind, float)>();
        public readonly List<(UnitSide Side, int Dir)> WallHits = new List<(UnitSide, int)>();
        public readonly List<int> GapsShown = new List<int>();
        public int EnemyMotions;
        public bool ShadeOn;
        public bool? LowHp;
        public bool FreeShown;
        public FreeStepWays ShownWays;
        public int FreeHides;

        private static int Key(UnitSide side, int unit) => side == UnitSide.Player ? -1 : Math.Max(0, unit);

        private static float XOf(UnitSide side, int unit) => side == UnitSide.Player ? 560f : 1260f + 200f * Math.Max(0, unit);

        private FigureGeom FigureOf(UnitSide side, int unit)
        {
            float x = XOf(side, unit);
            float height = 226f;
            bool away = _away.Contains((side, Key(side, unit)));
            float now = away ? x + (side == UnitSide.Player ? L.LungePx : -L.LungePx) : x;
            return new FigureGeom
            {
                X = now, FeetY = L.FeetY, Height = height, Width = 120, HomeX = x, Top = L.FeetY - height, ArtX0 = now - 60, ArtX1 = now + 60,
                Chest = new StagePoint(now, L.FeetY - height * L.Chest), Head = new StagePoint(now, L.FeetY - height * L.Head),
                Unit = side == UnitSide.Player ? -1 : Key(side, unit), Down = _down.Contains((side, Key(side, unit))),
            };
        }

        private Promise Wait(float ms) => _env.Clock.Wait(ms);

        public bool IsAway(UnitSide side, int unit = -1) => _away.Contains((side, Key(side, unit)));

        public void ApplyFrame(DepictionFrame frame, bool first = false)
        {
            if (frame == null) return;
            if (frame.Enemies != null)
                foreach (UnitFrame uf in frame.Enemies)
                    if (uf.Down) _down.Add((UnitSide.Enemy, Math.Max(0, uf.Unit)));
        }

        public void ShowGapTo(int unit) => GapsShown.Add(unit);
        public IReadOnlyList<int> EnemyUnits() => Array.Empty<int>();
        public void SetExtras(IReadOnlyList<object> list) { }
        public Promise DrawAim(DepictionFrame frame, int unit, bool now = false) => now ? Promise.Resolved() : Wait(30);
        public void ShowReach(IReadOnlyList<int> cells) { }
        public Promise RetireAim(float ms) => Wait(ms);
        public void RetireAimOf(int unit) { }
        public void FocusAim(int unit) { }
        public void HideReach() { }
        public void SetShift(int dir) { }
        public float ShiftX => 0f;
        public float NudgeOf(UnitSide side, int unit = -1) => 0f;

        public Promise SetLowHp(bool on)
        {
            LowHp = on;
            return Promise.Resolved();
        }

        public Promise PulseLowHp(float ms) => Wait(ms);
        public Promise AppearEnemies(float ms, float from = 0f) => Wait(ms);
        public void SetEnemyTurnShade(bool on) => ShadeOn = on;
        public void SetPose(UnitSide side, FigurePose pose, int unit = -1) { }

        public void SetDown(UnitSide side, bool down, int unit = -1)
        {
            if (down) _down.Add((side, Key(side, unit)));
            else _down.Remove((side, Key(side, unit)));
        }

        public bool EnemyAway(int unit = -1) => IsAway(UnitSide.Enemy, unit) && !_down.Contains((UnitSide.Enemy, Key(UnitSide.Enemy, unit)));
        public void SetEnemyHomeCell(int cell, int size, int unit = -1) { }
        public void SnapHome(UnitSide side, int unit = -1)
        {
            Motion((side, Key(side, unit)));
            _away.Remove((side, Key(side, unit)));
        }
        public bool StanceGlowShown(UnitSide side, int unit = -1) => _glow.Contains((side, Key(side, unit)));

        public void SetStanceGlow(UnitSide side, bool on, int unit = -1)
        {
            if (on) _glow.Add((side, Key(side, unit)));
            else _glow.Remove((side, Key(side, unit)));
        }

        public Promise FadeStanceGlow(UnitSide side, bool on, float ms, int unit = -1)
        {
            SetStanceGlow(side, on, unit);
            return Wait(ms);
        }

        public Promise Lunge(UnitSide side, float px, float ms, int unit = -1)
        {
            Lunges.Add((side, Key(side, unit), _env.Clock.Now));
            Motion((side, Key(side, unit)));
            _away.Add((side, Key(side, unit)));
            return Wait(ms);
        }

        /// <summary>As field.js: the figure is away (off its home) until the walk back has arrived; a newer motion takes it over.</summary>
        public Promise ReturnHome(UnitSide side, float ms, int unit = -1)
        {
            var key = (side, Key(side, unit));
            Returns.Add((side, key.Item2, _env.Clock.Now));
            int token = Motion(key);
            if (ms <= 0f)
            {
                _away.Remove(key);
                return Promise.Resolved();
            }
            return Wait(ms).Then(() => { if (_motion[key] == token) _away.Remove(key); });
        }

        private readonly Dictionary<(UnitSide, int), int> _motion = new Dictionary<(UnitSide, int), int>();

        private int Motion((UnitSide, int) key)
        {
            _motion.TryGetValue(key, out int n);
            _motion[key] = ++n;
            return n;
        }

        public Promise EnemyMotion(StrikeSystem system, float ms, int unit = -1)
        {
            EnemyMotions++;
            Motion((UnitSide.Enemy, Key(UnitSide.Enemy, unit)));
            _away.Add((UnitSide.Enemy, Key(UnitSide.Enemy, unit)));
            return Wait(ms);
        }

        public Promise Recoil(UnitSide side, bool strong, float ms, int unit = -1) => Wait(ms);
        public Promise Flash(UnitSide side, FxRole role, float ms, int unit = -1) => Wait(ms);
        public Promise Heave(UnitSide side, float ms, int unit = -1) => Wait(ms);

        public Promise Fall(UnitSide side, float ms, int unit = -1, bool keepFoot = false)
        {
            Falls.Add((side, Key(side, unit), keepFoot));
            return Wait(ms).Then(() => { if (!keepFoot) SetDown(side, true, unit); });
        }

        public Promise FadeFoot(UnitSide side, float ms, int unit = -1) => Wait(ms).Then(() => SetDown(side, true, unit));
        public Promise DimFeet(float ms) => Wait(ms);

        public Promise Bump(UnitSide side, int dir, float ms, int unit = -1)
        {
            Bumps.Add((side, dir));
            BumpAt.Add(_env.Clock.Now);
            return Wait(ms);
        }

        public Promise ShakeArena(float ms) => Wait(ms);

        public Promise MoveOnFloor(Cue cue, int cells, float ms) => Wait(ms);
        public void SnapOnFloor(Cue cue) { }

        public Promise ShowFreeStep(int playerCell, FreeStepWays ways)
        {
            FreeShown = true;
            ShownWays = ways;
            _freeDirs = new List<int>();
            if (ways.Back) _freeDirs.Add(-1);
            _freeDirs.Add(0);
            if (ways.Forward) _freeDirs.Add(1);
            FreeStepFocus = null;
            return Wait(150);
        }

        public int? FocusFreeStep(int dir)
        {
            if (!_freeDirs.Contains(dir)) return null;
            FreeStepFocus = dir;
            return dir;
        }

        public Promise HideFreeStep(float? ms = null)
        {
            FreeHides++;
            FreeShown = false;
            _freeDirs = new List<int>();
            FreeStepFocus = null;
            return Wait(ms ?? 120);
        }

        public bool FreeStepShown => FreeShown;
        public int? FreeStepFocus { get; private set; }
        public IReadOnlyList<int> FreeStepDirs() => _freeDirs.ToList();
        public StagePoint? FreeStepCentre(int dir) => _freeDirs.Contains(dir) ? new StagePoint(560 + 160 * dir, 540) : (StagePoint?)null;
        public Promise RecountGap(int gap, int playerCell, int playerSize, int enemyCell, float ms) => Wait(ms);
        public void SetGap(int gap, int playerCell, int playerSize, int enemyCell) { }
        public Promise Strike(UnitSide side, StrikeOpts o) => Wait(o.Ms);

        public Promise WallHit(UnitSide side, int dir, float ms, int unit = -1)
        {
            WallHits.Add((side, dir));
            return Wait(ms);
        }

        public Promise TraitAccent(UnitSide side, float ms, int unit = -1) => Wait(ms);
        public Promise Mark(UnitSide side, MarkOpts o) => Wait(o.Ms);
        public void Burst(StagePoint at, FxRole role, float size) { }

        public Promise FloatText(StagePoint at, string text, FxRole role, float fontSize, FloatOpts o = null)
        {
            Floats.Add((text, role, fontSize, o));
            o?.OnPlaced?.Invoke(new FloatSpan { X0 = at.X - 40, X1 = at.X + 40, Y = at.Y, Rise = o.Rise ?? 43, YNow = () => at.Y });
            return Wait(600);
        }

        public Promise ShieldBlock(UnitSide side, string amountText, float ms, int unit = -1, bool broken = false) => Wait(ms);
        public StagePoint ShieldAt(UnitSide side, int unit = -1) => FigureOf(side, unit).Chest;
        public Promise Ghost(StagePoint from, StagePoint to, GhostOpts o)
        {
            Ghosts.Add((o.Kind, _env.Clock.Now));
            return Wait(o.Ms);
        }
        public void Destroy() { }
    }
}
