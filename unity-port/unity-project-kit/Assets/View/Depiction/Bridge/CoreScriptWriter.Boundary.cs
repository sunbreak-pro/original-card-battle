// CoreScriptWriter (see CoreScriptWriter.cs): where a screen event begins.
using BattleCore;

namespace Depiction.Bridge
{
    public sealed partial class CoreScriptWriter
    {
        // ---- where a screen event begins ----------------------------------------------------

        // js: CoreScriptWriter.js:392
        private DepictionEvent OpenIfBoundary(BattleEvent e, DepictionEvent current)
        {
            switch (e)
            {
                case WallHit wall when current != null && current.Kind == DepictionEventKind.EnemyAction:
                    // §7.3: the player, shoved into the end of the line. Inside a player's card the wall
                    // is the enemy's and fits in the card's own event.
                    return About(NewEvent(DepictionEventKind.EnemyAction, "壁に当たる"), current.Unit);

                case TurnStarted started when started.Actor == Actor.Player:
                    _turn = started.Turn;
                    _playerActs = true;
                    _enemyTurn = false;
                    return NewEvent(DepictionEventKind.TurnStart, "ターン開始");

                case CardPlayed played:
                {
                    DepictionEvent ev = NewEvent(DepictionEventKind.PlayCard, played.Card.Def.Name);
                    ev.CardId = played.Card.InstanceId;
                    ev.Aim = played.Card.Def.Targets == TargetKind.One ? CardAim.Single : CardAim.Self;
                    // The enemy a card aimed at one was played on (CardPlayed.Unit).
                    if (ev.Aim == CardAim.Single) ev.Unit = played.Unit;
                    return ev;
                }

                // Step 7 opens the turn end: a turn-end stance (根渡り, 霞み足, #188) fires before 構え
                // and belongs to the same event, so 構え does not open a second one.
                case StanceFired fired when fired.Actor == Actor.Player && fired.Hook == StanceHook.TurnEnd:
                    _playerActs = false;
                    _enemyTurn = true;
                    return NewEvent(DepictionEventKind.TurnEnd, "ターン終了");

                case ReserveChecked reserve when reserve.Actor == Actor.Player
                    && (current == null || current.Kind != DepictionEventKind.TurnEnd):
                    _playerActs = false;
                    _enemyTurn = true;
                    return NewEvent(DepictionEventKind.TurnEnd, "ターン終了");

                case GuardCleared cleared when cleared.Actor == Actor.Enemy:
                    _enemyActed = false;
                    return About(NewEvent(DepictionEventKind.EnemyAction, "敵の番"), cleared.Unit);

                // roster §1.3 (#189): an elite's or a boss's second action is a beat of its own, so
                // neither blow is squeezed into the 2.0 s one event may take.
                case ActionExecuted second when current != null && current.Kind == DepictionEventKind.EnemyAction
                    && _enemyActed:
                    return About(NewEvent(DepictionEventKind.EnemyAction, second.Action.Name), second.Unit);

                // #189: an enemy turn start that did something of its own (a stance's Guard, 再生 or
                // 出血) is its beat; the action follows as the next one.
                case ActionExecuted first when current != null && current.Kind == DepictionEventKind.EnemyAction
                    && current.Cues.Exists(c => c.Kind == CueKind.GuardGain || c.Kind == CueKind.HpChange):
                    return About(NewEvent(DepictionEventKind.EnemyAction, first.Action.Name), first.Unit);

                // §5.4 順 2 (#587): an enemy that strikes and then moves itself (退き撃ち, 踏み込み斬り) keeps
                // the move in the blow's event, so the move can start on the number + 80 with no return
                // first (EffectPlan.EnemyMoveFollows). The whole 1 手 stays inside the 2.0 s (§5.0).

                // §2.4 hits (#189): the second blow of an enemy face is a beat of its own (二段斬り),
                // opened where the 脆化 its first blow left is spent for it.
                case StatusConsumed used when used.Actor == Actor.Player && used.Kind == StatusKind.Fragile
                    && current != null && current.Kind == DepictionEventKind.EnemyAction
                    && current.Cues.Exists(c => c.Kind == CueKind.Slash && c.Source == UnitSide.Enemy):
                    return About(NewEvent(DepictionEventKind.EnemyAction, current.Title + "（2 撃目）"), current.Unit);

                case DamageDealt blow when blow.Actor == Actor.Enemy && current != null
                    && current.Kind == DepictionEventKind.EnemyAction
                    && current.Cues.Exists(c => c.Kind == CueKind.Slash && c.Source == UnitSide.Enemy):
                    return About(NewEvent(DepictionEventKind.EnemyAction, current.Title + "（2 撃目）"), blow.Unit);

                // #50: a 予定変更 is a beat of its own, shown before the second action acts. An enemy read
                // to 0 shows no plan (#455), so its change has no beat and costs no gap between events.
                case PlanChanged changed when current != null && OmenDisclosure.ShowsPlan(DisclosureOf(changed.Unit)):
                    return About(NewEvent(DepictionEventKind.EnemyAction, DepictionText.PlanChangedTag), changed.Unit);

                case OmenSet omen when omen.Decided && _order > 0:
                    return About(NewEvent(DepictionEventKind.NextOmen, "次の予兆"), omen.Unit);

                default:
                    return null;
            }
        }

        /// <summary>js: CoreScriptWriter.js:485 — <paramref name="ev"/> with DepictionEvent.Unit set.</summary>
        private static DepictionEvent About(DepictionEvent ev, int unit)
        {
            ev.Unit = unit;
            return ev;
        }
    }
}
