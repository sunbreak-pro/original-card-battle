// A stand-in for the hud module (hud.js, IHud) for the player's flow tests: number panels that keep the HP the player
// animates and the frames apply, one omen badge per enemy, the end-turn plate, banners and the result card. Every beat
// resolves after the ms the player hands it on env.Clock. It fills env.Geom's hud slots and records what the tests read.
using System;
using System.Collections.Generic;
using System.Linq;
using Depiction;

namespace Trial.Battle.Flow.Tests.Fakes
{
    public sealed class FakeStatus : IStatusPanel
    {
        private readonly BattleEnv _env;

        public FakeStatus(BattleEnv env) { _env = env; }

        public int Hp { get; set; }
        public bool Shown => true;
        public int Guard;
        public int HpLossShown;
        public bool Down;
        public int GreyOuts;
        public readonly List<(string Label, int Stacks)> Statuses = new List<(string, int)>();

        public PanelBox Box() => new PanelBox(0, 614, 220, 60, 0);
        public void SetGuard(int n) { if (n >= 0) Guard = n; }

        public Promise PopGuard(int n, float ms = 260f)
        {
            SetGuard(n);
            return _env.Clock.Wait(ms);
        }

        public Promise CrackGuard(float ms) => _env.Clock.Wait(ms);
        public void SetHp(int hp) => Hp = hp;

        public Promise AnimateHp(int hp, float ms)
        {
            Hp = hp;
            return _env.Clock.Wait(ms);
        }

        public Promise AnimateTrail(float ms) => _env.Clock.Wait(ms);
        public void SnapTrail() { }
        public void SetHpKeepingTrail(int hp) => Hp = hp;
        public void ShowHpLoss(int n) => HpLossShown = n;
        public void SetStatus(string label, int stacks) => Statuses.Add((label, stacks));

        public Promise PlayStatus(string label, int stacks, EffectId beat, float ms)
        {
            Statuses.Add((label, stacks));
            return _env.Clock.Wait(ms);
        }

        public void SetStamina(int stamina, int max) { }
        // The real panel lights the recovered pips one at a time; the fake plays no longer than one pip, so the player's
        // hold is the plan's wait alone (a beat shorter than its wait shows a wait the player forgot to hold).
        public Promise LightPips(int stamina, int max, float perPipMs) => _env.Clock.Wait(perPipMs);
        public Promise PopPips(float ms) => _env.Clock.Wait(ms);

        public Promise GreyOut(float ms)
        {
            GreyOuts++;
            return _env.Clock.Wait(ms).Then(() => Down = true);
        }

        public void SetDown(bool on) => Down = on;
        public void SetFocus(bool on) { }
    }

    public sealed class FakeBadge : IOmenBadge
    {
        private readonly BattleEnv _env;

        public FakeBadge(BattleEnv env, int unit)
        {
            _env = env;
            Unit = unit;
        }

        public readonly int Unit;
        public OmenFrame Bound;
        public bool Acting;
        public int Blinks;
        /// <summary>The scene time of every blink.</summary>
        public readonly List<float> BlinkAt = new List<float>();
        public int Flares;
        public int FadeOuts;
        public int Promotes;
        public readonly List<string> TraitWords = new List<string>();

        public void Bind(OmenFrame o) { if (o != null) Bound = o; }
        public void SetSwatchShown(bool on, bool aimed) { }
        public void SetTail(float? dx) { }
        public void SetBehind(bool on) { }
        public void SetMoveDir(int dir) { }

        public Promise<bool> PromotePlan(float ms)
        {
            Promotes++;
            var s = new PromiseSource<bool>();
            _env.Clock.Wait(ms).Then(() => s.TryResolve(false)).Forget();
            return s.Promise;
        }

        public Promise PopIn(float ms) => _env.Clock.Wait(ms);

        public Promise Blink(float ms)
        {
            Blinks++;
            BlinkAt.Add(_env.Clock.Now);
            return _env.Clock.Wait(ms);
        }

        public Promise Flare(float ms)
        {
            Flares++;
            return _env.Clock.Wait(ms);
        }

        public Promise Reveal(float ms) => _env.Clock.Wait(ms);

        public Promise TraitWord(string word, float ms)
        {
            TraitWords.Add(word);
            return _env.Clock.Wait(ms);
        }

        public void SetActing(bool on) => Acting = on;

        public Promise FadeOut(float ms)
        {
            FadeOuts++;
            return _env.Clock.Wait(ms);
        }

        public void HideNow() { }
        public Promise FadeOutAll(float ms) => _env.Clock.Wait(ms);
        public void HideAllNow() { }
        public Promise StrikeSide(float ms) => _env.Clock.Wait(ms);
        public void StrikeSideNow() { }
        public StageBox Box() => new StageBox(1160 + 200 * Unit, 120, L.Omen.W, L.Omen.H);
        public bool Visible => Bound != null && Bound.Visible;
    }

    public sealed class FakeHud : IHud, IOmenFacade, IHudStamina, IHudEndTurn
    {
        private readonly BattleEnv _env;
        private readonly FakeStatus _player;
        private readonly Dictionary<int, FakeStatus> _enemies = new Dictionary<int, FakeStatus>();
        private readonly Dictionary<int, FakeBadge> _badges = new Dictionary<int, FakeBadge>();

        public FakeHud(BattleEnv env)
        {
            _env = env;
            _player = new FakeStatus(env);
            env.Geom.PileMiddle = kind => new StagePoint(64 + 100 * (int)kind, 990);
            env.Geom.OmenBox = unit => BadgeOf(unit).Box();
            env.Geom.OmenShown = unit => BadgeOf(unit).Visible;
            env.Geom.OmenBoxes = () => _badges.Values.Select(b => b.Box()).ToList();
            env.Geom.EndTurnBox = () => new StageBox(L.EndTurn.X, L.EndTurn.Y, L.EndTurn.W, L.EndTurn.H);
        }

        public readonly List<(string Text, BannerSide Side)> Banners = new List<(string, BannerSide)>();
        public readonly List<string> Hints = new List<string>();
        public readonly List<int> Costs = new List<int>();
        public int Refuses;
        public int Flashes;
        public int Pops;
        public bool EnemyTurnOn;
        public BattleOutcome? ResultOutcome;
        public ResultInfo Result;
        public Action ResultButton;
        public float ResultUpAt = -1;
        public readonly List<(string Title, string Name, float Scale)> ChainBanners = new List<(string, string, float)>();
        public readonly List<string> LossWords = new List<string>();
        public IReadOnlyList<string> Log = Array.Empty<string>();
        public string SpeedLabel;
        public int SpeedChevrons;
        public bool SpeedVisible = true;
        public bool Drawer;
        public int KeptOmen = -2;

        public FakeStatus StatusOf(UnitSide side, int unit = -1)
        {
            if (side == UnitSide.Player) return _player;
            int k = Math.Max(0, unit);
            if (!_enemies.TryGetValue(k, out FakeStatus s)) _enemies[k] = s = new FakeStatus(_env);
            return s;
        }

        public FakeBadge BadgeOf(int unit)
        {
            int k = Math.Max(0, unit);
            if (!_badges.TryGetValue(k, out FakeBadge b)) _badges[k] = b = new FakeBadge(_env, k);
            return b;
        }

        public void ApplyFrame(DepictionFrame frame, bool first = false, int keepOmen = -1)
        {
            if (frame == null) return;
            KeptOmen = keepOmen;
            _player.Hp = frame.Player.Hp;
            if (frame.Enemies != null && frame.Enemies.Count > 0)
            {
                for (int k = 0; k < frame.Enemies.Count; k++) StatusOf(UnitSide.Enemy, frame.Enemies[k].Unit >= 0 ? frame.Enemies[k].Unit : k).Hp = frame.Enemies[k].Hp;
            }
            else StatusOf(UnitSide.Enemy).Hp = frame.Enemy.Hp;
            if (frame.Omens != null && frame.Omens.Count > 0)
            {
                for (int k = 0; k < frame.Omens.Count; k++) BadgeOf(frame.Omens[k].Unit).Bind(frame.Omens[k]);
            }
            else BadgeOf(0).Bind(frame.Omen);
        }

        public void Follow(FollowArgs o) { }
        public IStatusPanel Status(UnitSide side, int unit = -1) => StatusOf(side, unit);
        public IOmenFacade Omen => this;
        public void SyncOmenLook(DepictionFrame frame, int unit) { }
        public IOmenBadge OmenOf(int unit = -1) => BadgeOf(unit);
        public IReadOnlyList<int> EnemyUnits() => _enemies.Keys.ToList();
        public IHudStamina Stamina => this;
        public void ReserveHint(string text) => Hints.Add(text ?? "");
        public void SetEnemyTurn(bool on) => EnemyTurnOn = on;
        public IHudEndTurn EndTurn => this;

        public Promise Banner(string text, BannerSide side, float ms)
        {
            Banners.Add((text, side));
            return _env.Clock.Wait(ms);
        }

        public Promise ResultCard(BattleOutcome outcome, float ms, ResultInfo info, Action onButton)
        {
            ResultOutcome = outcome;
            Result = info;
            ResultButton = onButton;
            return _env.Clock.Wait(ms).Then(() => ResultUpAt = _env.Real.Now);
        }

        public void ResultCardNow(BattleOutcome outcome, ResultInfo info, Action onButton)
        {
            ResultOutcome = outcome;
            Result = info;
            ResultButton = onButton;
            ResultUpAt = _env.Real.Now;
        }

        public Promise ChainBanner(string title, string name, float scale, Action onOut)
        {
            ChainBanners.Add((title, name, scale));
            _env.Clock.Wait(1000 * scale).Then(() => onOut?.Invoke()).Forget();
            return _env.Clock.Wait(1400 * scale);
        }

        public Promise LossWordOut(float ms) => _env.Clock.Wait(ms);
        public Promise SettleEnd(float ms, BattleOutcome outcome) => _env.Clock.Wait(ms);
        public void SettleEndNow(BattleOutcome outcome) { }
        public Promise RetireOmens(float ms) => _env.Clock.Wait(ms);
        public void LiftOmen(IReadOnlyList<int> units) { }
        public void SetRestFocus(int unit) { }
        public void SetLog(IReadOnlyList<string> lines) => Log = lines;
        public Promise FlyOmenToStances(int unit, float ms) => _env.Clock.Wait(ms);

        public Promise LossInk(string word, float inkMs, float wordMs)
        {
            LossWords.Add(word);
            return _env.Clock.Wait(inkMs + wordMs);
        }

        public void Speed(string label, int chevrons)
        {
            SpeedLabel = label;
            SpeedChevrons = chevrons;
        }

        public void SetSpeedVisible(bool on) => SpeedVisible = on;
        public bool ClosePinned() => false;
        public bool DrawerOpen() => Drawer;
        public void CloseDrawer() => Drawer = false;
        public void OnPinDown(BattlePointer p) { }
        public void OnPinContext(BattlePointer p) { }
        public void OnPointerMove(BattlePointer p) { }
        public void Destroy() { }

        // ---- hud.omen: the leader's badge, except Bind (by the omen's Unit) and SetActing(false) (every badge) ----

        void IOmenFacade.Bind(OmenFrame o) => BadgeOf(o != null ? o.Unit : 0).Bind(o);
        Promise IOmenFacade.PopIn(float ms) => BadgeOf(0).PopIn(ms);
        Promise IOmenFacade.Blink(float ms) => BadgeOf(0).Blink(ms);
        Promise IOmenFacade.Flare(float ms) => BadgeOf(0).Flare(ms);

        void IOmenFacade.SetActing(bool on)
        {
            if (on) BadgeOf(0).SetActing(true);
            else foreach (FakeBadge b in _badges.Values) b.SetActing(false);
        }

        Promise IOmenFacade.FadeOut(float ms) => BadgeOf(0).FadeOut(ms);
        void IOmenFacade.HideNow() => BadgeOf(0).HideNow();
        Promise IOmenFacade.FadeOutAll(float ms) => BadgeOf(0).FadeOutAll(ms);
        void IOmenFacade.HideAllNow() => BadgeOf(0).HideAllNow();
        Promise IOmenFacade.StrikeSide(float ms) => BadgeOf(0).StrikeSide(ms);
        void IOmenFacade.StrikeSideNow() => BadgeOf(0).StrikeSideNow();

        // ---- hud.stamina / hud.endTurn ----

        public void ShowCost(int n) => Costs.Add(n);
        public Promise GlowReserve(float ms) => _env.Clock.Wait(ms);

        public Promise Pop(float ms)
        {
            Pops++;
            return _env.Clock.Wait(ms);
        }

        public Promise Flash(float ms)
        {
            Flashes++;
            return _env.Clock.Wait(ms);
        }

        public Promise Refuse(float ms)
        {
            Refuses++;
            return _env.Clock.Wait(ms);
        }
    }
}
