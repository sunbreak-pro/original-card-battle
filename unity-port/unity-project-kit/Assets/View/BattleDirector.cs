// Turns the reducer's event list into the §5 animation sequence, then settles the
// screen on the view model. Numbers are never invented here: every value shown
// comes from an event (what happened) or the view model (what is now true).
#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using BattleCore;
using UnityEngine;

public sealed class BattleDirector
{
    private readonly MonoBehaviour _host;
    private readonly ArenaView _arena;
    private readonly BattleHud _hud;
    private readonly HandView _hand;
    private readonly JournalDrawer _journal;
    private readonly ResultOverlay _overlay;

    private readonly Queue<BattleViewModel> _queue = new Queue<BattleViewModel>();
    private bool _playing;
    private bool _first = true;
    private OmenView _currentOmen;
    private int _disclosure = 1;
    private float _baseSpeed = 1f;
    private BattleViewModel _latest;

    public bool Playing => _playing;
    public Action OnSettled;

    public BattleDirector(MonoBehaviour host, ArenaView arena, BattleHud hud, HandView hand, JournalDrawer journal, ResultOverlay overlay)
    {
        _host = host;
        _arena = arena;
        _hud = hud;
        _hand = hand;
        _journal = journal;
        _overlay = overlay;
    }

    public void SetReduceMotion(bool reduce)
    {
        _baseSpeed = reduce ? 1000f : 1f;
        UiTween.Speed = _baseSpeed;
    }

    public void FastForward()
    {
        if (_playing) UiTween.Speed = _baseSpeed * 8f;
    }

    /// <summary>Called on every store notification. Sequences are played one after another.</summary>
    public void Enqueue(BattleViewModel vm)
    {
        _latest = vm;
        _queue.Enqueue(vm);
        if (!_playing) _host.StartCoroutine(Drain());
    }

    private IEnumerator Drain()
    {
        _playing = true;
        Lock(true);
        while (_queue.Count > 0)
        {
            var vm = _queue.Dequeue();
            bool restart = !_first && HasEvent<TurnStartedEvent>(vm.Events, e => e.Turn == 1) && HasEvent<OmenDeclaredEvent>(vm.Events, _ => true) && vm.Log.Count <= 2;
            if (_first || restart)
            {
                yield return PlayInitial(vm);
                _first = false;
            }
            else
            {
                yield return PlaySequence(vm);
            }
            Settle(vm);
        }
        UiTween.Speed = _baseSpeed;
        _playing = false;
        Lock(false);
        OnSettled?.Invoke();
    }

    private static bool HasEvent<T>(IReadOnlyList<BattleEvent> events, Func<T, bool> pred) where T : BattleEvent
    {
        foreach (var e in events) if (e is T t && pred(t)) return true;
        return false;
    }

    private void Lock(bool on)
    {
        _overlay.SetCatcher(on);
        _hand.SetInteractable(!on && _latest != null && !_latest.BattleOver && !_journal.IsOpen);
        _hud.SetInteractable(!on && _latest != null && !_latest.BattleOver);
    }

    // ---- settled --------------------------------------------------------------------------

    private void Settle(BattleViewModel vm)
    {
        _disclosure = vm.Disclosure;
        _currentOmen = vm.Omen;
        _arena.SetFloor(vm.Floor);
        _arena.PlaceFigures(Combat.IndexToRange(vm.DistanceIndex));
        _arena.HideGhost();
        _arena.SetOmen(vm.Omen);
        _hud.ApplyStatic(vm);
        _hud.SetPhase(vm.BattleOver ? (vm.Result == GameResult.Won ? "撃破" : "力尽きた") : "あなたの番");
        _hand.Rebuild(vm.Hand);
        _journal.Apply(vm.Journal);
        _overlay.SetLowHp(vm.PlayerHp * 10 < vm.PlayerMaxHp * 3);
        if (!vm.BattleOver) _overlay.HideAll();
    }

    private IEnumerator PlayInitial(BattleViewModel vm)
    {
        _overlay.HideAll();
        _arena.ResetFigures();
        _hud.SetPhase("あなたの番");
        _hud.ApplyStatic(vm);
        _arena.SetFloor(vm.Floor);
        _arena.PlaceFigures(Combat.IndexToRange(vm.DistanceIndex));
        _arena.SetOmen(vm.Omen);
        _currentOmen = vm.Omen;
        _hand.Rebuild(vm.Hand);
        _host.StartCoroutine(_overlay.ShowPhase("あなたの番", 300f));
        yield return UiTween.Wait(200f);
        _host.StartCoroutine(_arena.BannerDrop());
        yield return _hand.SlideIn();
    }

    // ---- event playback ---------------------------------------------------------------------

    private IEnumerator PlaySequence(BattleViewModel vm)
    {
        foreach (var ev in vm.Events)
        {
            switch (ev)
            {
                case TurnStartedEvent e:
                    _hud.SetPhase("あなたの番");
                    _host.StartCoroutine(_overlay.ShowPhase("あなたの番", 300f));
                    yield return _hud.FadeShield(_hud.PlayerBar);
                    yield return _hud.LightPips(_hud.StaminaShown, vm.PlayerStamina);
                    break;

                case CardsDrawnEvent _:
                    _hand.Rebuild(vm.Hand);
                    yield return _hand.SlideIn();
                    yield return _arena.BannerBlink();
                    break;

                case HandDiscardedEvent _:
                    yield return _hand.DropAll();
                    break;

                case CardPlayedEvent e:
                    _hud.SetPreview(0);
                    _arena.HideGhost();
                    yield return _hud.SpendPips(e.Invest);
                    yield return _hand.FlyOut(e.InstanceId);
                    break;

                case AttackResolvedEvent e:
                    yield return PlayAttack(e);
                    break;

                case MovedEvent e:
                    yield return _arena.MoveTo(Combat.IndexToRange(e.To), e.Clamped);
                    RefreshBand(e.To);
                    break;

                case GuardGainedEvent e:
                    yield return _hud.PopShield(Bar(e.Who), e.Total);
                    _arena.SpawnNumber(e.Who, $"Guard +{e.Amount}", BattleTheme.Guard, 24, e.Who == Actor.Player ? 70f : -70f);
                    break;

                case ReserveGuardEvent e:
                    if (e.Who == Actor.Player) yield return _hud.GlowReservePips();
                    yield return _hud.PopShield(Bar(e.Who), Bar(e.Who).ShownGuard + e.Amount);
                    _arena.SpawnNumber(e.Who, $"構え +{e.Amount}", BattleTheme.Guard, 24, e.Who == Actor.Player ? 70f : -70f);
                    break;

                case HealedEvent e:
                    _arena.SpawnNumber(e.Who, $"+{e.Amount}", BattleTheme.Accent, 40);
                    yield return _hud.DrainHp(Bar(e.Who), e.HpAfter);
                    break;

                case StaminaBrokenEvent e:
                    yield return _hud.ShatterPip(e.Target, e.Target == Actor.Enemy ? vm.EnemyMaxStamina : vm.PlayerMaxStamina);
                    _arena.SpawnNumber(e.Target, $"崩し −{e.Amount}", BattleTheme.Omen, 26, e.Target == Actor.Player ? 70f : -70f);
                    yield return _arena.Stagger(e.Target);
                    break;

                case CalmTriggeredEvent e:
                    _arena.SpawnNumber(Actor.Player, $"冷静 +{e.Bonus}", BattleTheme.Accent, 22, 70f);
                    yield return UiTween.Wait(200f);
                    break;

                case EnemyPhaseStartedEvent e:
                    _hud.SetPhase("敵の番");
                    yield return _overlay.ShowPhase("敵の番", 400f);
                    _host.StartCoroutine(_hud.FadeShield(_hud.EnemyBar));
                    yield return _hud.EnemyLightPips(_hud.EnemyStaminaShown,
                        Mathf.Min(vm.EnemyMaxStamina, _hud.EnemyStaminaShown + e.Recovery), vm.EnemyMaxStamina);
                    yield return UiTween.Wait(600f);
                    break;

                case OmenExecutedEvent e:
                    yield return _arena.BannerFill();
                    yield return _hud.EnemySpendPips(e.Invest, vm.EnemyMaxStamina);
                    break;

                case OmenWhiffedEvent _:
                    yield return _arena.BannerStrike("空振り → 間合い取り直し");
                    yield return UiTween.Wait(400f);
                    break;

                case EnemyRestedEvent _:
                    _arena.BannerNote("休み");
                    yield return UiTween.Wait(400f);
                    break;

                case OmenDeclaredEvent _:
                    yield return UiTween.Wait(300f);
                    _currentOmen = vm.Omen;
                    _arena.SetOmen(vm.Omen);
                    yield return _arena.BannerDrop();
                    yield return UiTween.Wait(300f);
                    break;

                case BattleEndedEvent e:
                    if (e.Result == GameResult.Won)
                    {
                        _hud.SetPhase("撃破");
                        yield return _arena.Collapse(Actor.Enemy);
                        yield return _overlay.PlayVictory(vm);
                    }
                    else
                    {
                        _hud.SetPhase("力尽きた");
                        yield return _arena.ShrinkGlow();
                        yield return _overlay.PlayDefeat(vm.Floor);
                    }
                    break;
            }
        }
    }

    private HpBar Bar(Actor who) => who == Actor.Player ? _hud.PlayerBar : _hud.EnemyBar;

    private IEnumerator PlayAttack(AttackResolvedEvent e)
    {
        Actor target = e.Attacker == Actor.Player ? Actor.Enemy : Actor.Player;
        var bar = Bar(target);
        float side = target == Actor.Player ? 60f : -60f;

        yield return _arena.StepIn(e.Attacker);
        yield return UiTween.Wait(80f);

        if (e.Diff < Constants.WhiffDiff)
        {
            _host.StartCoroutine(_arena.HitFlash(target, e.Diff == 0 ? 3 : 1));
            if (e.GuardAbsorbed > 0)
            {
                _arena.SpawnNumber(target, $"Guard −{e.GuardAbsorbed}", BattleTheme.Whiff, 26, side);
                _host.StartCoroutine(_hud.CrackShield(bar, Mathf.Max(0, bar.ShownGuard - e.Raw)));
            }
            if (e.Damage > 0)
            {
                _arena.SpawnNumber(target, $"−{e.Damage}", BattleTheme.Omen, 46, e.GuardAbsorbed > 0 ? -side : 0f);
                if (e.Mult < 1.0) _arena.SpawnNumber(target, ViewModel.MultLabel(e.Mult), BattleTheme.Whiff, 20, -side * 2f);
                if (e.Desperate) _arena.SpawnNumber(target, "死力", BattleTheme.Warm, 20, side * 2f);
                yield return _hud.DrainHp(bar, e.TargetHpAfter);
                if (target == Actor.Player && e.TargetHpAfter * 10 < _hud.PlayerBar.Max * 3)
                {
                    _overlay.SetLowHp(true);
                    _host.StartCoroutine(_overlay.PulseVignette());
                }
            }
            else
            {
                yield return UiTween.Wait(180f);
            }
        }
        else
        {
            _arena.SpawnNumber(target, $"空振り {e.Damage}", BattleTheme.Whiff, 24, side);
            if (e.Damage > 0) yield return _hud.DrainHp(bar, e.TargetHpAfter);
            else yield return UiTween.Wait(300f);
        }

        yield return _arena.StepBack(e.Attacker);
    }

    /// <summary>Recolour the floor band for the current omen at a (new) distance.</summary>
    private void RefreshBand(int distanceIndex)
    {
        if (_currentOmen == null) return;
        var omen = new Omen(_currentOmen.ActionId, _currentOmen.TargetRange);
        var view = ViewModel.DescribeOmen(omen, distanceIndex, _disclosure);
        _arena.SetBand(view.Diff, view.FloorText);
    }

    // ---- hover preview (called by the screen while idle) --------------------------------------

    public void PreviewTier(TierView tier)
    {
        if (_playing) return;
        if (tier == null)
        {
            _hud.SetPreview(0);
            _arena.HideGhost();
            if (_latest != null) RefreshBand(_latest.DistanceIndex);
            return;
        }
        _hud.SetPreview(tier.Invest);
        if (tier.Shift != 0 && !tier.Clamped)
        {
            _arena.ShowGhost(Combat.IndexToRange(tier.DistanceAfter));
            RefreshBand(tier.DistanceAfter);
        }
        else
        {
            _arena.HideGhost();
            if (_latest != null) RefreshBand(_latest.DistanceIndex);
        }
    }
}
#endif
