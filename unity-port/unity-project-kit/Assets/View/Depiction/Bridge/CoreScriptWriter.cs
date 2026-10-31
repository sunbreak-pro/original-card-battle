// Turns the core's event stream (BattleCore.BattleEvent) into the script the screen plays
// (DepictionEvent / Cue / DepictionFrame). Pure C#: BattleCore + Depiction.Script, no UnityEngine.
//
// Nothing here works a rule out. Every number on a cue is copied from an event's settled value
// (StaminaAfter, TargetHpAfter, GuardAfter …), and what a held card or a shown omen would do comes
// from the core's own TurnLoop.Preview / PreviewOmen. What this class does decide is presentation:
// where one screen event ends and the next begins, and which beat shows which number.
//
// Several enemies (port of the browser trial's JS extension, docs/port-notes/depiction-multi-enemy.md,
// js: src/bridge/depiction/CoreScriptWriter.js). The writer used to model the first enemy only (one
// _enemy, one _omen that took every OmenSet whichever enemy it was about) and threw
// KeyNotFoundException in the two-enemy battle the exploration hands in on layers 2〜3. It now keeps
// what it kept of that one enemy once per enemy (EnemyTrack), reads each event's enemy from its Unit,
// and writes every enemy into the frame in #589's shape (DepictionFrame.Enemies as EnemyFrame, Cue.Unit),
// with the extension's DepictionEvent.Unit and Cue.UnitCellAfter / UnitGapAfter. The single-valued members
// (Frame.Enemy, Frame.Omen, Floor.EnemyCell …) are the first enemy's share while it stands first in the
// line, and then the first enemy standing in the line; in a one-enemy battle every member written before
// comes out exactly as it did (MultiEnemyTests replays a golden of the one-enemy output taken before the change).
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;

namespace Depiction.Bridge
{
    public sealed class CoreScriptWriter
    {
        /// <summary>The numbers one unit shows, copied forward from events as they are read.</summary>
        private sealed class UnitModel
        {
            public int Hp;
            public int HpMax;
            public int Guard;
            public int Stamina;
            public int StaminaMax;
            public int Cell;
            public int Size;
            /// <summary>The unit's fall has been written (#288); the frames from its Defeat beat on show it gone.</summary>
            public bool Down;
            public readonly SortedDictionary<StatusKind, int> Statuses = new SortedDictionary<StatusKind, int>();

            /// <summary>§4 (v4.4): the name of each stance on the list of permanent effects, in the order they were put on; a repeat is listed again.</summary>
            public readonly List<string> Stances = new List<string>();

            // js: CoreScriptWriter.js:108
            public void CopyFrom(CombatantState unit, EnemyDef enemy)
            {
                Hp = unit.Hp;
                HpMax = unit.MaxHp;
                Guard = unit.Guard;
                Stamina = unit.Stamina;
                StaminaMax = unit.MaxStamina;
                Cell = unit.Cell;
                Size = unit.Size;
                Down = false;
                Statuses.Clear();
                foreach (StatusKind kind in unit.Statuses.Kinds) Statuses[kind] = unit.Statuses.Stacks(kind);
                Stances.Clear();
                foreach (StanceEntry entry in unit.StanceList) Stances.Add(StanceName(entry.Source, enemy));
            }
        }

        /// <summary>
        /// js: CoreScriptWriter.js:130 — what the writer keeps of one enemy. The one-enemy writer kept these
        /// once, as private fields, for the first enemy (_enemyDef, _enemy, _omen, _omenVisible,
        /// _omenPreview, _omenPreviewFor, _plan, _planVisible, _planChanged, _enemyReserveHeld, and
        /// _enemyFalls as FallPending); track 0 is exactly those.
        /// </summary>
        private sealed class EnemyTrack
        {
            public readonly EnemyDef Def;
            public readonly UnitModel Model = new UnitModel();
            public Omen Omen;
            public bool OmenVisible;

            /// <summary>
            /// The core's reading of the omen on screen (#248, TurnLoop.PreviewOmen), and the omen it was
            /// taken for. It is only taken on a state from the player's turn: a frame is read against the
            /// state a whole move left, and after an enemy phase that state is past the turn end the
            /// number already counts — even when the enemy chose the same action again. Until the omen
            /// shown is spent it keeps the number the player last saw; a newly decided omen starts over.
            /// </summary>
            public OmenPreview OmenPreview;
            public Omen OmenPreviewFor;

            /// <summary>#50: the 予定 (the second action an elite or a boss plans), whether it is on screen, and whether a 予定変更 has replaced it.</summary>
            public Omen Plan;
            public bool PlanVisible;
            public bool PlanChanged;

            /// <summary>The enemy's 構え, held back so it plays with its next omen and not inside the attack (see <see cref="Write"/>).</summary>
            public ReserveChecked ReserveHeld;

            /// <summary>The core said this enemy fell in the move being written; its fall is written as a beat of its own (#288).</summary>
            public bool FallPending;

            public EnemyTrack(EnemyDef def)
            {
                Def = def;
            }
        }

        private readonly UnitModel _player = new UnitModel();

        /// <summary>One track per enemy, in the core's enemy order. Track 0 is the first enemy's; the others are taken from the state at Opening (or at the first Write).</summary>
        private readonly List<EnemyTrack> _tracks = new List<EnemyTrack>();
        private readonly List<CardInstance> _hand = new List<CardInstance>();
        private int _turn;

        /// <summary>The power the card being written has put on the enemy it reads so far (#248), or −1 before its first blow.</summary>
        private int _playedPower = -1;
        private int _playedUnit;
        private bool _playerActs;

        /// <summary>
        /// battle-visual-v1 §5.5 (#455): from the player's turn end until the next turn start the enemy
        /// has the turn. Unlike <see cref="_playerActs"/> it is false before turn 1 opens.
        /// </summary>
        private bool _enemyTurn;
        private int _order;

        /// <summary>The enemy action being written whiffed (§6): its trait miss has nothing to strike off.</summary>
        private bool _whiffed;

        /// <summary>#455: the core has just made the draw pile again (DeckReshuffled); the next Drawn marks its beat.</summary>
        private bool _rebuildPending;

        /// <summary>The system (§5.3) of the card or action whose blows are being written.</summary>
        private StrikeSystem _strike = StrikeSystem.Slash;

        /// <summary>#57 (§5.9 J2): a trait of the card or action being written fired, so its blows carry the short cue.</summary>
        private bool _traitLit;

        /// <summary>The core event read just before the one being read is a trait's judgement (#588).</summary>
        private bool _afterTraits;

        /// <summary>
        /// battle_ui_ux_v2 §5.9 (#588): the pips a fired trait gives back. The core adds them as soon as
        /// the traits are judged, before any face; the screen shows them after the faces, so the beat
        /// waits here until the card's faces are written (<see cref="ReleaseTraitStamina"/>).
        /// </summary>
        private Cue _traitStaminaHeld;

        /// <summary>
        /// The enemy whose phase is being written has carried out an action in it already (#189: an
        /// elite's second one opens a beat of its own). Enemies take their phases one by one (§7.4), so
        /// one flag serves them all: each phase's GuardCleared clears it.
        /// </summary>
        private bool _enemyActed;

        /// <summary>The falls the core reported in the move being written, in the order they fell (units).</summary>
        private readonly List<int> _falls = new List<int>();

        /// <summary>The enemy whose Defeat beat was written last — the one Frame.Enemy shows once none stands.</summary>
        private int _lastDown;

        /// <summary>§12 連戦 (#191): which battle of the chain this is, and how many there are; 1 / 1 alone.</summary>
        public int ChainIndex { get; set; } = 1;

        public int ChainTotal { get; set; } = 1;

        /// <summary>
        /// battle-visual-v1 §4.4 (#455): how far the journal has read this enemy (開示度 0〜2). Below 2 the
        /// omen's numbers and statuses are left off the script, and at 0 its plan (OmenDisclosure). CoreBattleSource sets
        /// it once as the battle starts (#500); left unset it is 2, everything shown. One level for every
        /// enemy of the battle (EncounterBattle hands in the lowest of them).
        /// </summary>
        public int Disclosure { get; set; } = OmenDisclosure.Full;

        /// <param name="enemyDef">The first enemy's definition (track 0's). The tracks of the enemies behind it are read off the state.</param>
        public CoreScriptWriter(EnemyDef enemyDef)
        {
            if (enemyDef == null) throw new ArgumentNullException(nameof(enemyDef));
            _tracks.Add(new EnemyTrack(enemyDef));
        }

        /// <summary>js: CoreScriptWriter.js:223 — one track per enemy of <paramref name="state"/>, track 0 kept; a later enemy's track reads its def off the state.</summary>
        internal void EnsureTracks(BattleState state)
        {
            for (int k = _tracks.Count; k < state.Enemies.Count; k++) _tracks.Add(new EnemyTrack(state.Enemies[k].Def));
        }

        /// <summary>
        /// js: CoreScriptWriter.js:235 — the enemy the single-valued members of a frame describe
        /// (Frame.Enemy, Frame.Omen, Floor.EnemyCell / Gap / AimCells …, Player.Range): the first one
        /// standing in the line — the lowest cell among the enemies the core still has standing. An enemy
        /// that fell in the move being written (its Defeat beat still to come) is passed over: the core has
        /// already taken its cells, so the player may stand past it. When it is the last one (the battle is
        /// won), it is that one; once every fall has been written, the one that fell last. Always 0 in a
        /// one-enemy battle.
        /// </summary>
        private int FocusUnit
        {
            get
            {
                int focus = -1;
                int falling = -1;
                for (int k = 0; k < _tracks.Count; k++)
                {
                    EnemyTrack track = _tracks[k];
                    if (track.Model.Down) continue;
                    if (track.FallPending)
                    {
                        if (falling < 0 || track.Model.Cell < _tracks[falling].Model.Cell) falling = k;
                        continue;
                    }
                    if (focus < 0 || track.Model.Cell < _tracks[focus].Model.Cell) focus = k;
                }
                return focus >= 0 ? focus : falling >= 0 ? falling : _lastDown;
            }
        }

        /// <summary>js: CoreScriptWriter.js:251 — enemy <paramref name="unit"/> has fallen in the core: its fall written (Down) or still to come.</summary>
        private bool Fallen(int unit)
        {
            EnemyTrack track = _tracks[unit];
            return track.Model.Down || track.FallPending;
        }

        /// <summary>js: CoreScriptWriter.js:257 — §7.2: N as the models stand now, the player and the enemy the frame shows (<see cref="FocusUnit"/>).</summary>
        private int Gap => GapTo(FocusUnit);

        /// <summary>js: CoreScriptWriter.js:262 — §7.2, N between the player and enemy <paramref name="unit"/> as the models stand now.</summary>
        private int GapTo(int unit)
        {
            return Field.Gap(_player.Cell, _player.Size, _tracks[unit].Model.Cell);
        }

        /// <summary>
        /// js: CoreScriptWriter.js:270 — the screen before turn 1. The first omen is already decided
        /// (TurnLoop.Start) but stays hidden: step 5 of the first turn is what shows it.
        /// </summary>
        public DepictionFrame Opening(BattleState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            _turn = Math.Max(1, state.Turn);
            _player.CopyFrom(state.Player, null);
            // Every enemy. A track of a later enemy whose def changed (a writer reused for another battle)
            // starts afresh; one the state no longer has goes.
            for (int k = 1; k < _tracks.Count && k < state.Enemies.Count; k++)
            {
                if (!ReferenceEquals(_tracks[k].Def, state.Enemies[k].Def)) _tracks[k] = new EnemyTrack(state.Enemies[k].Def);
            }
            int keep = Math.Max(1, Math.Min(_tracks.Count, state.Enemies.Count));
            if (_tracks.Count > keep) _tracks.RemoveRange(keep, _tracks.Count - keep);
            EnsureTracks(state);
            for (int k = 0; k < _tracks.Count; k++)
            {
                EnemyTrack track = _tracks[k];
                EnemyUnit unit = k < state.Enemies.Count ? state.Enemies[k] : null;
                if (unit != null) track.Model.CopyFrom(unit.Body, track.Def);
                track.Omen = unit?.Omen;
                track.Plan = unit?.Plan;
                track.PlanVisible = false;
                track.PlanChanged = false;
                track.OmenVisible = false;
            }
            _lastDown = 0;
            _hand.Clear();
            _hand.AddRange(state.Hand);
            _playerActs = state.Phase == BattlePhase.PlayerAction;
            _enemyTurn = false;
            return Snapshot(state);
        }

        /// <summary>
        /// js: CoreScriptWriter.js:316 — one move of the loop → the screen events it plays as.
        /// BeginPlayerTurn and PlayCard are one event each; EndTurn is three for the slice (turn end, the
        /// enemy's action, the next omen), the same beats the screen already knows. A few moments take a
        /// beat of their own so each event stays inside the 2.0 s it may take: the enemy's push driving
        /// the player into the wall, and (#189) an enemy turn start that did something, an elite's second
        /// action, the second blow of 二段斬り. The step an enemy takes after its blow stays in the blow's event (#587).
        ///
        /// With several enemies EndTurn plays each enemy's phase in turn: its 敵の番 beat, its action's
        /// beats and its next omen. An enemy that falls while others stand falls as a beat of its own
        /// right after the beat it fell in.
        ///
        /// <paramref name="after"/> is the state the move left. It is only asked what the cards still
        /// in the hand and the omen on screen would do (the lamp, the omen's number), never for a
        /// number an event already carries.
        /// </summary>
        public List<DepictionEvent> Write(IReadOnlyList<BattleEvent> events, BattleState after)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (after == null) throw new ArgumentNullException(nameof(after));
            EnsureTracks(after);

            var written = new List<DepictionEvent>();
            DepictionEvent current = null;

            foreach (BattleEvent e in events)
            {
                DepictionEvent opened = OpenIfBoundary(e, current);
                if (opened != null)
                {
                    ReleaseTraitStamina(current);
                    Close(current, after);
                    // (#288 for several enemies) an enemy that fell in the beat just closed, with others
                    // still standing (EnemyDefeated), falls before the next beat, so its figure and omen
                    // are gone before the next enemy acts. The beat that opened was numbered first; it plays after.
                    if (current != null && _falls.Count > 0)
                    {
                        int first = opened.Order;
                        List<DepictionEvent> falls = WriteFalls(after);
                        for (int i = 0; i < falls.Count; i++) falls[i].Order = first + i;
                        opened.Order = first + falls.Count;
                        written.AddRange(falls);
                    }
                    current = opened;
                    written.Add(current);
                    // The enemy's held 構え plays at the head of its next omen (see ApplyReserve). The
                    // one-enemy writer flushed it at the end of the move, which with one enemy is the same
                    // event and the same place in it.
                    if (opened.Kind == DepictionEventKind.NextOmen) FlushHeldReserve(current, opened.Unit);
                }
                if (current == null) continue; // TurnLoop.Start's first omen: folded into Opening, not played.
                Apply(e, current);
                _afterTraits = e is TraitEvaluated;
            }

            // A held enemy 構え plays at the head of the last event: the next omen, or — when the
            // battle ended and no omen follows — the enemy's own action.
            if (current != null) FlushHeldReserves(current);
            ReleaseTraitStamina(current);
            Close(current, after);

            // #288: the fall is a beat of its own after everything the move did, so the 800 ms never
            // lengthens the blow's event past its 2.0 s, and the figure stays up until it plays.
            written.AddRange(WriteFalls(after));
            return written;
        }

        /// <summary>js: CoreScriptWriter.js:360 — every fall waiting, in the order the core reported them.</summary>
        private List<DepictionEvent> WriteFalls(BattleState after)
        {
            var written = new List<DepictionEvent>();
            while (_falls.Count > 0)
            {
                int unit = _falls[0];
                _falls.RemoveAt(0);
                written.Add(WriteFall(unit, after));
            }
            return written;
        }

        // js: CoreScriptWriter.js:367
        private DepictionEvent WriteFall(int unit, BattleState after)
        {
            EnemyTrack track = _tracks[unit];
            track.FallPending = false;
            track.Model.Down = true;
            track.OmenVisible = false; // a fallen enemy takes its omen with it (TurnLoop.OnFall)
            track.PlanVisible = false; // a fallen elite takes its plan with it, now that the badge draws it (#349)
            _lastDown = unit;
            DepictionEvent ev = NewEvent(DepictionEventKind.Defeat, "倒れ");
            ev.Unit = unit;
            ev.Cues.Add(new Cue { Kind = CueKind.Defeat, Target = UnitSide.Enemy, Unit = unit });
            Close(ev, after);
            return ev;
        }

        /// <summary>js: CoreScriptWriter.js:382 — the core said enemy <paramref name="unit"/> fell; its fall waits to be written.</summary>
        private void QueueFall(int unit)
        {
            EnemyTrack track = _tracks[unit];
            if (track.FallPending || track.Model.Down) return;
            track.FallPending = true;
            _falls.Add(unit);
        }

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
                case PlanChanged changed when current != null && OmenDisclosure.ShowsPlan(Disclosure):
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

        // ---- one core event → its beats -----------------------------------------------------

        /// <summary>
        /// One core event's beats. #589: each beat says which enemy it is about, the event's own enemy end
        /// (BattleEvent.Unit: the enemy acting, or the one a card read or hit; 0 for the player alone).
        /// </summary>
        internal void Apply(BattleEvent e, DepictionEvent ev)
        {
            int before = ev.Cues.Count;
            ApplyEvent(e, ev);
            for (int i = before; i < ev.Cues.Count; i++) ev.Cues[i].Unit = e.Unit;
        }

        // js: CoreScriptWriter.js:493
        private void ApplyEvent(BattleEvent e, DepictionEvent ev)
        {
            switch (e)
            {
                case GuardCleared cleared:
                {
                    UnitModel unit = Unit(cleared.Actor, cleared.Unit);
                    unit.Guard = 0;
                    if (cleared.Cleared > 0)
                    {
                        ev.Cues.Add(new Cue
                        {
                            Kind = CueKind.GuardReset, Target = CoreText.Side(cleared.Actor), GuardAfter = 0,
                            Unit = cleared.Unit,
                        });
                    }
                    break;
                }

                case StaminaRecovered recovered:
                    SetStamina(ev, recovered.Actor, recovered.Unit, recovered.Amount, recovered.StaminaAfter);
                    break;

                case StaminaSpent spent:
                    SetStamina(ev, spent.Actor, spent.Unit, -spent.Amount, spent.StaminaAfter);
                    break;

                case StaminaGained gained:
                {
                    Cue gain = Grammar(ev, SetStamina(ev, gained.Actor, gained.Unit, gained.Amount, gained.StaminaAfter), FxSystem.Breath);
                    // §5.9 (#588): a fired trait's stamina +1 is shown after all the faces, not where the
                    // core adds it (straight after the traits are judged). The model already holds it, so
                    // the frame is right whenever the beat plays.
                    if (gain != null && _afterTraits)
                    {
                        ev.Cues.Remove(gain);
                        gain.Unit = gained.Unit;
                        _traitStaminaHeld = gain;
                    }
                    break;
                }

                case StatusTicked ticked:
                    SetStatus(ev, ticked.Actor, ticked.Unit, ticked.Kind, -1, ticked.StacksAfter);
                    break;

                case StatusApplied applied:
                    // #205: a ターンで減る型 word stops at its cap, so what landed can be less than what
                    // was given, or nothing; the core says how much.
                    if (applied.Landed > 0)
                    {
                        Grammar(ev, SetStatus(ev, applied.Target, applied.Unit, applied.Kind, applied.Landed, applied.StacksAfter), FxSystem.Spirit);
                    }
                    break;

                case MaxStaminaChanged lowered:
                {
                    // #51 瘴気纏い: the pips lose their top, and what is held is cut to the new maximum.
                    UnitModel unit = Unit(lowered.Actor, lowered.Unit);
                    int cut = lowered.StaminaAfter - unit.Stamina;
                    unit.StaminaMax = lowered.MaxAfter;
                    SetStamina(ev, lowered.Actor, lowered.Unit, cut, lowered.StaminaAfter);
                    break;
                }

                case StatusCleared cleared:
                    // #51 root_sk: the enemy shook the word off.
                    SetStatus(ev, cleared.Actor, cleared.Unit, cleared.Kind, -cleared.Stacks, 0);
                    break;

                case Drawn drawn:
                {
                    // §5.9's later group in its table's order: the trait's stamina +1, then the draw.
                    ReleaseTraitStamina(ev);
                    _hand.Add(drawn.Card);
                    // A hand of six is six core events and one beat on screen.
                    Cue last = ev.Cues.Count > 0 ? ev.Cues[ev.Cues.Count - 1] : null;
                    if (last == null || last.Kind != CueKind.DrawHand)
                    {
                        last = Grammar(ev, new Cue { Kind = CueKind.DrawHand, Target = UnitSide.Player, Amount = 0 }, FxSystem.Breath);
                        ev.Cues.Add(last);
                    }
                    // §3.14 (#455): the core made the draw pile again just before this card; the cards
                    // already drawn in this beat came off the old pile.
                    if (_rebuildPending && last.RebuildAfter == Cue.Unchanged) last.RebuildAfter = last.Amount;
                    _rebuildPending = false;
                    last.Amount += 1;
                    break;
                }

                // §3.14 山札切れ (#455): the discard pile is shuffled into the draw pile; the next Drawn is its first card.
                case DeckReshuffled _:
                    _rebuildPending = true;
                    break;

                // battle-visual-v1 §4.7 / battle_ui_ux_v2 §5.11 順 4 (#455): the played stance card goes to the exile pile.
                case CardExiled exiled when exiled.Actor == Actor.Player && ev.Kind == DepictionEventKind.PlayCard:
                    ev.PlayedTo = PileKind.Exile;
                    break;

                case OmenSet omen:
                {
                    EnemyTrack track = _tracks[omen.Unit];
                    bool alreadyShown = track.OmenVisible && Equals(track.Omen, omen.Omen);
                    // Step 12's omen is a new one even when it is the same action: the old number goes.
                    if (omen.Decided)
                    {
                        track.OmenPreviewFor = null;
                        track.Plan = null; // #50: the plan of the phase that just ended; a PlanSet follows if the new omen has one.
                        track.PlanVisible = false;
                        track.PlanChanged = false;
                    }
                    track.Omen = omen.Omen;
                    track.OmenVisible = true;
                    // Step 5 re-announces the omen step 12 already put up; the screen shows it once.
                    if (!alreadyShown) ev.Cues.Add(new Cue { Kind = CueKind.OmenShow, Target = UnitSide.Enemy, Unit = omen.Unit });
                    break;
                }

                case CardPlayed played:
                    _hand.Remove(played.Card);
                    _strike = CoreText.SystemOf(played.Card.Def);
                    _traitLit = false;
                    ev.Systems.Clear();
                    ev.Systems.AddRange(CardSystems.Of(played.Card.Def));
                    _playedPower = -1;
                    _playedUnit = played.Unit;
                    break;

                // #50: the plan is shown from the omen until the second action acts; a phase that stops
                // at one action (a rest, a stop at the first blow) replaces it with 予定変更.
                case PlanSet planned:
                {
                    EnemyTrack track = _tracks[planned.Unit];
                    track.Plan = planned.Plan;
                    track.PlanVisible = true;
                    track.PlanChanged = false;
                    break;
                }

                case PlanChanged changed:
                {
                    EnemyTrack track = _tracks[changed.Unit];
                    track.Plan = changed.Now;
                    track.PlanVisible = true;
                    track.PlanChanged = true;
                    // #455: an enemy read to 0 shows no plan (battle_ui_ux_v2 §7), so its change has nothing to pop.
                    if (OmenDisclosure.ShowsPlan(Disclosure))
                    {
                        ev.Cues.Add(new Cue { Kind = CueKind.OmenShow, Target = UnitSide.Enemy, Text = DepictionText.PlanChangedTag, Unit = changed.Unit });
                    }
                    break;
                }

                case ActionExecuted executed:
                    if (_enemyActed) _tracks[executed.Unit].PlanVisible = false; // the second action of the phase spends the plan
                    _enemyActed = true;
                    ev.Title = executed.Action.Name;
                    _strike = CoreText.SystemOf(executed.Action);
                    _traitLit = false;
                    ev.Systems.Clear();
                    ev.Systems.AddRange(CardSystems.Of(executed.Action));
                    ev.Cues.Add(new Cue { Kind = CueKind.EnemyWindup, Target = UnitSide.Enemy, System = _strike, Unit = executed.Unit });
                    break;

                case Rested _:
                    ev.Title = "敵は動けない";
                    break;

                case TraitEvaluated trait:
                    if (trait.Outcome.Triggered) _traitLit = true;
                    ApplyTrait(ev, trait);
                    break;

                case DamageDealt damage:
                    ApplyDamage(ev, damage);
                    break;

                case GuardGained guard:
                {
                    Unit(guard.Actor, guard.Unit).Guard = guard.GuardAfter;
                    Cue cue = GuardGain(guard.Actor, guard.Amount, guard.GuardAfter);
                    cue.Unit = guard.Unit;
                    ev.Cues.Add(Grammar(ev, cue, FxSystem.Shield));
                    break;
                }

                case CellsMoved moved:
                {
                    // Whichever side moved, the screen shows the walk on the floor: both figures to the
                    // cells they stand on after it, and the new N.
                    Unit(moved.Actor, moved.Unit).Cell = moved.To;
                    int focus = FocusUnit;
                    var cue = new Cue
                    {
                        Kind = CueKind.RangeSwitch, Target = UnitSide.Player, Pushed = moved.Pushed,
                        RangeGlyphAfter = CoreText.GapGlyph(Gap),
                        // battle-visual-v1 §4.2 (#242): where both stand after the move, for the floor.
                        PlayerCellAfter = _player.Cell, EnemyCellAfter = _tracks[focus].Model.Cell, GapAfter = Gap,
                        // battle_ui_ux_v2 §5.7 順 1 (#586): the walk takes 160 ms a cell.
                        CellsMoved = Math.Abs(moved.To - moved.From),
                    };
                    // The enemy that moved, and where it stands now.
                    if (moved.Actor == Actor.Enemy)
                    {
                        cue.Unit = moved.Unit;
                        cue.UnitCellAfter = _tracks[moved.Unit].Model.Cell;
                        cue.UnitGapAfter = GapTo(moved.Unit);
                    }
                    ev.Cues.Add(cue);
                    break;
                }

                case WallHit wall:
                    ApplyWallHit(ev, wall);
                    break;

                case MoveBlocked blocked:
                {
                    // #335: 呪縛 / 鉤爪 stopped the move; nothing moved, so the line is all there is to show.
                    string line = CoreText.MoveBlockedLine(blocked.By);
                    if (line.Length > 0)
                    {
                        ev.Cues.Add(new Cue
                        {
                            Kind = CueKind.TraitFire, Target = CoreText.Side(blocked.Actor), Text = line,
                            Unit = blocked.Unit,
                        });
                    }
                    break;
                }

                case ActionWhiffed whiff:
                    // The blow finds nobody: the omen's number is struck off, the way a missed side bonus is.
                    _whiffed = true;
                    ev.Cues.Add(new Cue
                    {
                        Kind = CueKind.SideBonusMiss, Target = UnitSide.Enemy,
                        Amount = ShownOmenPower(whiff.SourceId, whiff.Unit), Text = "空振り", Unit = whiff.Unit,
                    });
                    break;

                case ReserveChecked reserve:
                    ApplyReserve(ev, reserve);
                    break;

                case HandDiscarded discarded:
                    _hand.Clear();
                    if (discarded.Count > 0)
                    {
                        ev.Cues.Add(new Cue { Kind = CueKind.DiscardHand, Target = UnitSide.Player, Amount = discarded.Count });
                    }
                    break;

                // ---- the demo vocabulary (#188) ----

                case StatusConsumed consumed:
                    SetStatus(ev, consumed.Actor, consumed.Unit, consumed.Kind, -1, consumed.StacksAfter);
                    break;

                case StatusHpChanged changed:
                    SetHp(ev, changed.Actor, changed.Unit, changed.Amount, changed.HpAfter, changed.Kind.ToLabel());
                    break;

                case Healed healed:
                    Grammar(ev, SetHp(ev, healed.Actor, healed.Unit, healed.Amount, healed.HpAfter, "回復"), FxSystem.Breath);
                    break;

                case StaminaBroken broken:
                    SetStamina(ev, broken.Target, broken.Unit, -broken.Amount, broken.StaminaAfter);
                    break;

                case Reflected reflected:
                {
                    UnitModel struck = Unit(reflected.Target, reflected.Unit);
                    struck.Guard = reflected.TargetGuardAfter;
                    if (reflected.Absorbed > 0)
                    {
                        ev.Cues.Add(new Cue
                        {
                            Kind = CueKind.GuardBlock, Target = CoreText.Side(reflected.Target),
                            Amount = reflected.Absorbed, GuardAfter = reflected.TargetGuardAfter, Unit = reflected.Unit,
                        });
                    }
                    SetHp(ev, reflected.Target, reflected.Unit, -reflected.Damage, reflected.TargetHpAfter, "見切り");
                    break;
                }

                case StanceSet set:
                    Unit(set.Actor, set.Unit).Stances.Add(set.Name);
                    ev.Cues.Add(Grammar(ev, new Cue
                    {
                        Kind = CueKind.TraitFire, Target = CoreText.Side(set.Actor), Text = "構え・" + set.Name,
                        Unit = set.Unit,
                    }, FxSystem.Form));
                    break;

                // #334 (roster §6.4 root_st): one permanent effect stops for a turn, then comes back.
                case StanceStopped stopped:
                    ev.Cues.Add(new Cue
                    {
                        Kind = CueKind.TraitFire, Target = CoreText.Side(stopped.Actor),
                        Text = CoreText.StanceStoppedLine(StanceName(stopped.SourceId, DefOf(stopped.Unit))),
                        Unit = stopped.Unit,
                    });
                    break;

                case StanceResumed resumed:
                    ev.Cues.Add(new Cue
                    {
                        Kind = CueKind.TraitFire, Target = CoreText.Side(resumed.Actor),
                        Text = CoreText.StanceResumedLine(StanceName(resumed.SourceId, DefOf(resumed.Unit))),
                        Unit = resumed.Unit,
                    });
                    break;

                // #288: an enemy that falls with others standing is EnemyDefeated; the last one to fall
                // ends the battle instead (TurnLoop.OnFall), and BattleEnded is all that says so — for every
                // enemy whose fall has not been written yet (the one-enemy writer marked the first enemy only).
                case EnemyDefeated fell:
                    QueueFall(fell.Unit);
                    break;

                case BattleEnded ended when ended.Result == GameResult.Won:
                    for (int k = 0; k < _tracks.Count; k++) QueueFall(k);
                    break;
            }

            // The omen is spent once the enemy has acted on it (or rested through it).
            if (e is ActionExecuted || e is Rested) _tracks[e.Unit].OmenVisible = false;
        }

        // js: CoreScriptWriter.js:696
        private void ApplyTrait(DepictionEvent ev, TraitEvaluated trait)
        {
            UnitSide side = CoreText.Side(trait.Actor);
            if (trait.Outcome.Triggered)
            {
                ev.Cues.Add(new Cue { Kind = CueKind.TraitFire, Target = side, Text = CoreText.TraitLine(trait.Trait), Unit = trait.Unit });
                return;
            }

            // An enemy blow that reads the gap and found the player outside its threshold: the screen
            // strikes the bonus off the omen badge, so stepping away (or in) visibly paid off. A blow
            // that whiffed has already had its whole number struck off.
            if (!_whiffed
                && trait.Actor == Actor.Enemy
                && (trait.Trait.Condition == BattleCore.TraitCondition.GapAtLeast
                    || trait.Trait.Condition == BattleCore.TraitCondition.GapAtMost)
                && trait.Trait.Effect == TraitEffect.PowerBonus)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.SideBonusMiss, Target = UnitSide.Enemy,
                    Amount = trait.Trait.Amount, Text = "+" + trait.Trait.Amount, Unit = trait.Unit,
                });
            }
        }

        /// <summary>
        /// js: CoreScriptWriter.js:723 — one attack, settled. When Guard swallows part of it — and for
        /// every enemy blow — it plays as three beats (swing, shield, wound) so each number has its own
        /// moment; otherwise one slash. Every beat carries the enemy of the exchange (DamageDealt.Unit).
        /// </summary>
        private void ApplyDamage(DepictionEvent ev, DamageDealt damage)
        {
            UnitSide source = CoreText.Side(damage.Actor);
            UnitSide target = CoreText.Side(damage.Target);
            UnitModel unit = Unit(damage.Target, damage.Unit);
            unit.Guard = damage.TargetGuardAfter;
            unit.Hp = damage.TargetHpAfter;

            // #248: the held card's number was its whole power before Guard; the played card shows the same sum.
            if (ev.Kind == DepictionEventKind.PlayCard && damage.Actor == Actor.Player && damage.Unit == _playedUnit)
            {
                _playedPower = Math.Max(0, _playedPower) + damage.Raw;
                ev.PreviewText = _playedPower.ToString();
            }

            bool splits = damage.Absorbed > 0 || damage.Actor == Actor.Enemy;
            if (!splits)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.Slash, Source = source, Target = target, System = _strike,
                    Fx = EffectGrammar.FromStrike(_strike), TraitAccent = _traitLit,
                    Amount = damage.Raw, Intensity = EffectStrength.Uniform, HpAfter = damage.TargetHpAfter, Unit = damage.Unit,
                });
                return;
            }

            ev.Cues.Add(new Cue
            {
                Kind = CueKind.Slash, Source = source, Target = target, System = _strike,
                Fx = EffectGrammar.FromStrike(_strike), TraitAccent = _traitLit,
                Amount = damage.Raw, Intensity = EffectStrength.Uniform, Unit = damage.Unit,
            });
            if (damage.Absorbed > 0)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.GuardBlock, Target = target,
                    Amount = damage.Absorbed, GuardAfter = damage.TargetGuardAfter, Unit = damage.Unit,
                });
            }
            if (damage.Damage > 0)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.Hit, Target = target,
                    Amount = damage.Damage, Intensity = EffectStrength.Uniform, HpAfter = damage.TargetHpAfter, Unit = damage.Unit,
                });
            }
        }

        /// <summary>js: CoreScriptWriter.js:766 — §7.3 壁のダメージ: the shield and the wound, like an enemy blow without its swing.</summary>
        private void ApplyWallHit(DepictionEvent ev, WallHit wall)
        {
            UnitSide target = CoreText.Side(wall.Actor);
            UnitModel unit = Unit(wall.Actor, wall.Unit);
            unit.Guard = wall.GuardAfter;
            unit.Hp = wall.HpAfter;
            // The wall a card's push drives the enemy into is part of the card's sum (TurnLoop.Preview).
            if (ev.Kind == DepictionEventKind.PlayCard && wall.Actor == Actor.Enemy && wall.Unit == _playedUnit && _playedPower >= 0)
            {
                _playedPower += wall.Raw;
                ev.PreviewText = _playedPower.ToString();
            }
            if (wall.Absorbed > 0)
            {
                ev.Cues.Add(new Cue { Kind = CueKind.GuardBlock, Target = target, Amount = wall.Absorbed, GuardAfter = wall.GuardAfter, Unit = wall.Unit });
            }
            if (wall.Damage > 0)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.Hit, Target = target,
                    Amount = wall.Damage, Intensity = EffectStrength.Uniform, HpAfter = wall.HpAfter, Unit = wall.Unit,
                });
            }
        }

        // js: CoreScriptWriter.js:788
        private void ApplyReserve(DepictionEvent ev, ReserveChecked reserve)
        {
            if (reserve.Actor == Actor.Player)
            {
                _player.Guard = reserve.GuardAfter;
                if (reserve.GuardGained > 0)
                {
                    ev.Cues.Add(new Cue
                    {
                        Kind = CueKind.StanceCue, Target = UnitSide.Player,
                        Amount = reserve.GuardGained, Text = "+" + reserve.GuardGained, GuardAfter = reserve.GuardAfter,
                    });
                }
                return;
            }

            // The enemy's 構え would push its own action past the 2.0 s an event may take (a shove is
            // already windup + swing + shield + wound + the push). It plays with the next omen instead,
            // and the Guard number on screen waits with it.
            if (reserve.GuardGained > 0) _tracks[reserve.Unit].ReserveHeld = reserve;
        }

        /// <summary>js: CoreScriptWriter.js:807 — every enemy's held 構え at the head of <paramref name="ev"/>, the first enemy's first.</summary>
        private void FlushHeldReserves(DepictionEvent ev)
        {
            for (int k = _tracks.Count - 1; k >= 0; k--) FlushHeldReserve(ev, k);
        }

        // js: CoreScriptWriter.js:812
        private void FlushHeldReserve(DepictionEvent ev, int unit)
        {
            if (unit < 0 || unit >= _tracks.Count) return;
            EnemyTrack track = _tracks[unit];
            if (track.ReserveHeld == null) return;
            ReserveChecked reserve = track.ReserveHeld;
            track.ReserveHeld = null;
            track.Model.Guard = reserve.GuardAfter;
            // First in the event: the shield goes up, then the omen it stands behind appears.
            Cue cue = GuardGain(Actor.Enemy, reserve.GuardGained, reserve.GuardAfter);
            cue.Unit = unit;
            ev.Cues.Insert(0, cue);
        }

        /// <summary>js: CoreScriptWriter.js:825 — writes the pips' cue and hands it back; null when nothing is shown (the enemy's stamina, no change).</summary>
        private Cue SetStamina(DepictionEvent ev, Actor actor, int unitIndex, int amount, int staminaAfter)
        {
            UnitModel unit = Unit(actor, unitIndex);
            unit.Stamina = staminaAfter;
            // Only the player's pips play a cue. The enemy's stamina is on its number panel since #455,
            // but it settles with the event's frame: battle_ui_ux_v2 §5.5 順 0 (its pips going by the cost
            // before the blow) is not played yet.
            if (actor != Actor.Player || amount == 0) return null;
            // #588: a held trait gain the core settled before this change plays first, so the pips
            // never step back (息継ぎ's own +1 lands after the trait's).
            ReleaseTraitStamina(ev);
            var cue = new Cue
            {
                Kind = CueKind.StaminaChange, Target = UnitSide.Player,
                Amount = amount, StaminaAfter = staminaAfter, StaminaMax = unit.StaminaMax,
            };
            ev.Cues.Add(cue);
            return cue;
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.9 (#588): plays the held trait stamina beat at the end of the cues written so
        /// far. Called when the card's event closes, and before the next beat that has to come after it:
        /// a draw (the later group's next row) or another stamina change (the pips keep the core's order).
        /// </summary>
        private void ReleaseTraitStamina(DepictionEvent ev)
        {
            if (_traitStaminaHeld == null || ev == null) return;
            ev.Cues.Add(_traitStaminaHeld);
            _traitStaminaHeld = null;
        }

        // js: CoreScriptWriter.js:841
        private Cue SetStatus(DepictionEvent ev, Actor holder, int unitIndex, StatusKind kind, int change, int stacksAfter)
        {
            UnitModel unit = Unit(holder, unitIndex);
            if (stacksAfter > 0) unit.Statuses[kind] = stacksAfter;
            else unit.Statuses.Remove(kind);
            var cue = new Cue
            {
                Kind = CueKind.StatusChange, Target = CoreText.Side(holder),
                Amount = change, Text = kind.ToLabel(), StacksAfter = stacksAfter, Unit = unitIndex,
            };
            ev.Cues.Add(cue);
            return cue;
        }

        /// <summary>js: CoreScriptWriter.js:854 — HP moved without a blow: 出血 / 再生, a heal, a 見切り return. The bar follows the settled value.</summary>
        private Cue SetHp(DepictionEvent ev, Actor actor, int unitIndex, int amount, int hpAfter, string cause)
        {
            Unit(actor, unitIndex).Hp = hpAfter;
            if (amount == 0) return null;
            var cue = new Cue
            {
                Kind = CueKind.HpChange, Target = CoreText.Side(actor),
                Amount = amount, HpAfter = hpAfter, Text = cause, Unit = unitIndex,
            };
            ev.Cues.Add(cue);
            return cue;
        }

        /// <summary>
        /// js: CoreScriptWriter.js:871 — §5.3 (#57): marks a beat that a card or an enemy action plays
        /// with its system, at the one strength every beat has (#30). A beat outside a play (a turn
        /// start's tick, the reserve's Guard) and a loss (a spent pip, a lost stack) keep none. Hands the
        /// cue back, null stays null.
        /// </summary>
        private Cue Grammar(DepictionEvent ev, Cue cue, FxSystem system)
        {
            if (cue == null || !InPlay(ev)) return cue;
            bool counted = cue.Kind == CueKind.StaminaChange || cue.Kind == CueKind.HpChange || cue.Kind == CueKind.StatusChange;
            if (counted && cue.Amount <= 0) return cue;
            cue.Fx = system;
            cue.Intensity = EffectStrength.Uniform;
            return cue;
        }

        /// <summary>The event is a card being played, or the enemy carrying out the action it chose.</summary>
        // js: CoreScriptWriter.js:881
        private bool InPlay(DepictionEvent ev)
        {
            return ev.Kind == DepictionEventKind.PlayCard || (ev.Kind == DepictionEventKind.EnemyAction && _enemyActed);
        }

        /// <summary>js: CoreScriptWriter.js:886 — the display name of a stance's source: a card for the player, an action of the enemy's.</summary>
        private static string StanceName(string sourceId, EnemyDef enemy)
        {
            if (string.IsNullOrEmpty(sourceId)) return "";
            EnemyActionDef action;
            if (enemy != null && enemy.Actions.TryGetValue(sourceId, out action)) return action.Name;
            foreach (CardDef def in CardCatalog.All)
            {
                if (def.Id == sourceId) return def.Name;
            }
            return sourceId;
        }

        // js: CoreScriptWriter.js:896
        private static Cue GuardGain(Actor actor, int amount, int guardAfter)
        {
            return new Cue
            {
                Kind = CueKind.GuardGain, Target = CoreText.Side(actor),
                Amount = amount, Intensity = EffectStrength.Uniform, GuardAfter = guardAfter,
            };
        }

        // ---- the enemy a card goes to (several enemies) -----------------------------------------

        /// <summary>
        /// js: CoreScriptWriter.js:927 — the enemy a card aimed at one enemy goes to when the screen names
        /// none: CoreBattleSource.TargetFor, and the target every number on the card's face is read
        /// against. The core takes the target from whoever releases the card (TurnLoop.PlayCard's
        /// <c>target</c>, §7.4 受け皿で 1 体); this picks the receiver the way a player dropping on "the
        /// enemy" means it: among the standing enemies the card can be played on now (TurnLoop.CanPlay),
        /// the one it costs least on, the nearest the player (lowest cell) of those; else, among the ones
        /// inside its reach (TurnLoop.Preview's InReach), the one it costs least on, the nearest of those;
        /// else the nearest standing one (BattleState.Nearest, the enemy the core itself reads when the
        /// one named does not stand); 0 when none stands. The cost comes first so that the face, whose
        /// cost is read on this enemy, shows the lowest cost the card has on any enemy in reach — the same
        /// "any enemy" reading as its lamp (battle-visual-v1 §6.3 決定 6): a lit コスト −1 lamp never sits
        /// over an undiscounted cost circle. The cost differs between enemies only through a trait read on
        /// the gap (石礫), so in every other case this is the nearest. A card aimed at nobody or at every
        /// enemy in reach ignores the target and gets the same fallback. With one enemy this is always 0,
        /// the target the one-enemy source passed.
        /// </summary>
        public static int TargetOf(BattleState state, string instanceId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            int nearest = state.Nearest;
            int fallback = nearest < 0 ? 0 : nearest;
            CardInstance card = null;
            foreach (CardInstance c in state.Hand)
            {
                if (c.InstanceId == instanceId)
                {
                    card = c;
                    break;
                }
            }
            if (card == null || !AimsAtOne(card.Def)) return fallback;
            List<int> line = LineOf(state);
            int playable = Cheapest(state, instanceId, line.Where(unit => TurnLoop.CanPlay(state, instanceId, unit) == PlayRefusal.None).ToList());
            if (playable >= 0) return playable;
            int inReach = Cheapest(state, instanceId, line.Where(unit =>
            {
                PlayPreview preview = TurnLoop.Preview(state, instanceId, unit);
                return preview != null && preview.InReach;
            }).ToList());
            return inReach >= 0 ? inReach : fallback;
        }

        /// <summary>
        /// js: CoreScriptWriter.js:953 — of <paramref name="units"/> (nearest first), the one card
        /// <paramref name="instanceId"/> costs least on (TurnLoop.Preview's Cost), the first of them on a
        /// tie; −1 when <paramref name="units"/> is empty.
        /// </summary>
        private static int Cheapest(BattleState state, string instanceId, IReadOnlyList<int> units)
        {
            int best = -1;
            int bestCost = 0;
            foreach (int unit in units)
            {
                PlayPreview preview = TurnLoop.Preview(state, instanceId, unit);
                int cost = preview != null ? preview.Cost : int.MaxValue;
                if (best < 0 || cost < bestCost)
                {
                    best = unit;
                    bestCost = cost;
                }
            }
            return best;
        }

        /// <summary>
        /// js: CoreScriptWriter.js:973 — battle-visual-v1 §5.2 スタミナ / §6.3 予測値もその敵で, for each
        /// enemy slot the cost the core charges for <paramref name="card"/> played on that enemy now
        /// (TurnLoop.Preview's Cost on it). A fallen slot, or a card the state no longer holds, gets
        /// <paramref name="shown"/> (the face's Cost). Empty for a card that does not aim at one enemy:
        /// its cost does not depend on a target.
        /// </summary>
        private static List<int> CostOn(BattleState state, CardInstance card, int shown)
        {
            var on = new List<int>();
            if (!AimsAtOne(card.Def)) return on;
            var living = new HashSet<int>(state.Living);
            for (int k = 0; k < state.Enemies.Count; k++)
            {
                PlayPreview preview = living.Contains(k) ? TurnLoop.Preview(state, card.InstanceId, k) : null;
                on.Add(preview != null ? preview.Cost : shown);
            }
            return on;
        }

        /// <summary>js: CoreScriptWriter.js:985 — a card the player releases on one enemy (targets one, aimed at the opponent).</summary>
        private static bool AimsAtOne(CardDef def)
        {
            return def.Targets == TargetKind.One && EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets);
        }

        /// <summary>js: CoreScriptWriter.js:990 — the standing enemies, nearest the player first (living enemies never share a cell).</summary>
        private static List<int> LineOf(BattleState state)
        {
            return state.Living.OrderBy(unit => state.Enemies[unit].Body.Cell).ToList();
        }

        /// <summary>
        /// js: CoreScriptWriter.js:1000 — battle-visual-v1 §5.2, for each enemy slot whether
        /// <paramref name="def"/> can be aimed at that enemy now: it stands and its N is inside the card's
        /// reach, the core's own test (Reach.Contains on BattleState.GapTo, as TurnLoop's aim check and
        /// Reached read it). Empty for a card aimed at nobody.
        /// </summary>
        private static List<bool> ReachOn(BattleState state, CardDef def)
        {
            var on = new List<bool>();
            if (!EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets)) return on;
            Reach reach = def.Face.ReachOrDefault;
            for (int k = 0; k < state.Enemies.Count; k++) on.Add(state.Enemies[k].Alive && reach.Contains(state.GapTo(k)));
            return on;
        }

        // ---- frames -------------------------------------------------------------------------

        // js: CoreScriptWriter.js:1011
        internal DepictionEvent NewEvent(DepictionEventKind kind, string title)
        {
            _order += 1;
            _whiffed = false;
            _rebuildPending = false;
            return new DepictionEvent { Order = _order, Kind = kind, Title = title };
        }

        // js: CoreScriptWriter.js:1019
        internal void Close(DepictionEvent ev, BattleState after)
        {
            if (ev == null) return;
            ev.After = Snapshot(after);
        }

        // js: CoreScriptWriter.js:1025
        private DepictionFrame Snapshot(BattleState after)
        {
            var frame = new DepictionFrame
            {
                Outcome = after.Result == GameResult.Won ? BattleOutcome.Won
                    : after.Result == GameResult.Lost ? BattleOutcome.Lost
                    : BattleOutcome.Ongoing,
                // Floor and miasma belong to the exploration layer (#99); the chain position is the demo's (#191).
                Corner = new CornerFrame { Turn = _turn, Floor = 1, ChainIndex = ChainIndex, ChainTotal = ChainTotal, MiasmaPercent = 0 },
            };
            int focus = FocusUnit;
            frame.Player = UnitOf(_player, showStamina: true, gap: GapTo(focus));
            // #589: every enemy's share — its number panel, omen, cells and stripes. Frame.Enemy, Frame.Omen
            // and the floor's enemy cells are the focus enemy's share, the same objects (FloorOf).
            BattleState shown = null;
            for (int k = 0; k < _tracks.Count; k++)
            {
                EnemyTrack track = _tracks[k];
                // battle-visual-v1 §4.5 (#455): the enemy's stamina is on its number panel (a dot and the number).
                UnitFrame unit = UnitOf(track.Model, showStamina: true, gap: null);
                // #288: the figure wears the art filed under the enemy's id; the View only looks it up.
                unit.ArtId = track.Def.Id;
                // §4.5 構えの盾: solid while the stamina it holds meets 構え's threshold (残り 3 以上), the
                // rule's own Combat.ReserveGuard; an elite's panel takes the phosphor frame.
                unit.ReserveLikely = !track.Model.Down && Combat.ReserveGuard(track.Model.Stamina) > 0;
                unit.Elite = track.Def.Rank == EnemyRank.Elite;
                var enemy = new EnemyFrame
                {
                    Unit = unit,
                    Omen = WithPlan(k, track.OmenVisible ? OmenFrameOf(after, k) : new OmenFrame { Visible = false }),
                    // Where it stands and its own N.
                    Cell = track.Model.Cell,
                    Size = Math.Max(1, track.Model.Size),
                    Gap = Fallen(k) ? -1 : GapTo(k),
                    // battle_ui_ux_v2 §1.4: 45°, −45°, upright, and the swatch once there are two or more.
                    StripeDirection = EnemyFrame.DirectionOf(k),
                    Swatch = _tracks.Count >= 2,
                };
                if (track.OmenVisible && after.Enemies.Count > k)
                {
                    enemy.AimCells = FloorCells.AimOf(track.Omen, after.Enemies[k].Body, track.Model.Cell);
                    // #455: the thick stripes and the move's frame are the core's, read on the board as shown.
                    if (shown == null) shown = AsShown(after);
                    enemy.DenseCells = FloorCells.DenseOf(shown, k);
                    enemy.MoveCells = FloorCells.MoveOf(shown, k);
                }
                frame.Enemies.Add(enemy);
            }
            frame.Enemy = frame.Enemies[focus].Unit;
            frame.Omen = frame.Enemies[focus].Omen;
            // battle-visual-v1 §4.7 (#455): the dashed dots, the core's estimate of the next recovery.
            frame.Player.NextRecovery = TurnLoop.PreviewRecovery(AsShown(after));
            frame.Floor = FloorOf(after, frame.Enemies[focus], focus);
            // battle-visual-v1 §4.7 (#455): the piles' counts, as the move left them.
            frame.Piles = new PileFrame { Shown = true, Draw = after.DrawPile.Count, Discard = after.DiscardPile.Count, Exile = after.Exiled.Count };
            // battle_ui_ux_v2 §1.1 C4 (#455): what each pile holds, for the list a click on it opens.
            PileCards.Fill(frame.Piles, after);
            // battle-visual-v1 §4.6 (#455): both sides' stance lists, from the stances the models hold.
            frame.StanceListsShown = true;
            for (int k = 0; k < frame.Enemies.Count; k++)
            {
                OmenFrame omen = frame.Enemies[k].Omen;
                if (omen.Visible) omen.Hit = HitMark(k, frame.Enemies[k].AimCells);
                OmenDisclosure.Apply(omen, Disclosure);
            }
            bool waiting = _playerActs && after.Result == GameResult.Ongoing;
            // battle-visual-v1 §4.9 (#455): the attributes the player has played this turn, as the core
            // folds and keeps them (CombatantState.PlayedThisTurn, cleared when the turn ends).
            frame.Played = PlayedOf(after.Player.PlayedThisTurn);
            // battle_ui_ux_v2 §1.1 B6 (#455): a 追撃 waiting for the next attack face, as the core keeps it.
            frame.Played.FollowUp = after.Player.FollowUp;
            foreach (CardInstance card in _hand)
            {
                // Every number on the face is read against the enemy the card would go to (TargetOf);
                // with one enemy that is 0, the target the one-enemy writer read.
                int target = TargetOf(after, card.InstanceId);
                PlayPreview preview = TurnLoop.Preview(after, card.InstanceId, target);
                CardFace face = CoreText.Face(card, preview, CoreText.ReachesNobody(after, card.InstanceId));
                face.ReachCells = FloorCells.ReachOf(card.Def, _player.Cell, _player.Size, after.FieldCells);
                // battle-visual-v1 §6.3 (#242): lit when any enemy in reach satisfies the trait, with each
                // enemy's own verdict for the one a held card frames. A card the state no longer holds
                // (the hand mirrored here runs ahead of it mid-event) keeps the preview's lamp.
                List<bool> perEnemy;
                bool lit = CardFaceText.Lamps(after, card.InstanceId, out perEnemy);
                if (preview != null)
                {
                    face.TraitLit = lit;
                    face.LitOn = perEnemy;
                }
                // battle-visual-v1 §4.5 (#507): the HP the card would take off each enemy, wall included.
                face.HpLossOn = CardFaceText.HpLoss(after, card.InstanceId);
                // §4.9 / §5.3 (#455): the 構えの予告 once this card's cost is paid, the rule's own
                // Combat.ReserveGuard on what would be left.
                face.StanceHintAfterPay = waiting ? ReserveHint(_player.Stamina - face.Cost) : "";
                // §5.2's frames per enemy, and the receiver the card goes to unnamed.
                face.ReachOn = ReachOn(after, card.Def);
                face.TargetUnit = AimsAtOne(card.Def) ? target : -1;
                // §5.2 / §6.3: the cost and the 構えの予告 while the card frames each enemy.
                face.CostOn = CostOn(after, card, face.Cost);
                face.StanceHintAfterPayOn = face.CostOn.Select(cost => waiting ? ReserveHint(_player.Stamina - cost) : "").ToList();
                frame.Hand.Add(face);
            }
            frame.StanceHint = waiting ? ReserveHint(_player.Stamina) : "";
            // §5.5 (#455): the screen sinks below the throw line while the enemy has the turn.
            frame.EnemyTurn = _enemyTurn && after.Result == GameResult.Ongoing;
            frame.StanceDivider = Constants.ReserveThreshold; // §4.7: the stamina tag's divider
            return frame;
        }

        /// <summary>js: CoreScriptWriter.js:1123 — 構えの予告 for <paramref name="stamina"/> left at the turn end: "+3" when 構え would give Guard, "" when not.</summary>
        private static string ReserveHint(int stamina)
        {
            int reserve = Combat.ReserveGuard(Math.Max(0, stamina));
            return reserve > 0 ? "+" + reserve : "";
        }

        /// <summary>js: CoreScriptWriter.js:1129 — §4.9 (#455): the played attributes counted per kind, the last one marked. A card of movement only counts as none.</summary>
        private static PlayedFrame PlayedOf(IReadOnlyList<BattleAttribute> played)
        {
            var frame = new PlayedFrame { Shown = true };
            foreach (BattleAttribute attribute in played)
            {
                if (attribute == BattleAttribute.None) continue;
                frame.Add(CoreText.KindOf(attribute));
            }
            return frame;
        }

        /// <summary>
        /// js: CoreScriptWriter.js:1144 — battle-visual-v1 §4.2 (#242): the floor as the models stand —
        /// the line's width, both figures' cells, N, and the cells the omen on screen aims at (none while
        /// it is hidden). The enemy members are <paramref name="focus"/>'s share (#589: the same lists);
        /// every enemy's own cells are its EnemyFrame.
        /// </summary>
        private FloorFrame FloorOf(BattleState after, EnemyFrame focus, int unit)
        {
            return new FloorFrame
            {
                Cells = after.FieldCells,
                PlayerCell = _player.Cell,
                PlayerSize = Math.Max(1, _player.Size),
                EnemyCell = focus.Cell,
                EnemySize = focus.Size,
                Gap = GapTo(unit),
                AimCells = focus.AimCells,
                DenseCells = focus.DenseCells,
                MoveCells = focus.MoveCells,
            };
        }

        /// <summary>
        /// js: CoreScriptWriter.js:1185 — <paramref name="after"/> as the frame being written shows it
        /// (#455): the models' cells, the player's stamina and the omens on screen. A move's frames are
        /// all read against the state the whole move left, which can be past them — an EndTurn ends on
        /// the next omen, after the enemy has moved — so what the core reads for a frame is read on this.
        /// </summary>
        private BattleState AsShown(BattleState after)
        {
            if (after.Enemies.Count == 0) return after;
            BattleState shown = after with { Player = after.Player with { Cell = _player.Cell, Stamina = _player.Stamina } };
            for (int k = 0; k < after.Enemies.Count && k < _tracks.Count; k++)
            {
                EnemyTrack track = _tracks[k];
                EnemyUnit unit = after.Enemies[k];
                shown = shown.WithUnit(k, unit with { Body = unit.Body with { Cell = track.Model.Cell }, Omen = track.Omen ?? unit.Omen });
            }
            return shown;
        }

        /// <summary>
        /// js: CoreScriptWriter.js:1203 — battle-visual-v1 §4.4 当たり外れの印 (#242): the core's
        /// OmenPreview.Lands whenever the preview read belongs to the omen on screen and is not a rest —
        /// it plays the enemy's turn start first (a stage's Sway moves it), so it can differ from the cells
        /// drawn now. Otherwise the aimed cells against the player's near edge, as the core counts N
        /// (FloorCells.HitOf). None for an omen that aims at nobody. Per enemy.
        /// </summary>
        private OmenHit HitMark(int unit, IReadOnlyList<int> aim)
        {
            EnemyTrack track = _tracks[unit];
            if (track.Omen == null || track.Omen.Label.Reach == null) return OmenHit.None;
            if (track.OmenPreview != null && Equals(track.OmenPreviewFor, track.Omen) && !track.OmenPreview.Rests)
            {
                return track.OmenPreview.Lands ? OmenHit.Lands : OmenHit.Misses;
            }
            return FloorCells.HitOf(track.Omen, aim, _player.Cell, _player.Size);
        }

        /// <summary>
        /// js: CoreScriptWriter.js:1218 — the omen badge. Its number is the core's (TurnLoop.PreviewOmen),
        /// taken whenever the state read is the player's turn with the shown omen standing; otherwise the
        /// last number it was shown with stays, or the face alone before there is one. Per enemy, each
        /// read against its own enemy's actions.
        /// </summary>
        private OmenFrame OmenFrameOf(BattleState after, int unit)
        {
            EnemyTrack track = _tracks[unit];
            if (track.Omen != null && after.Phase == BattlePhase.PlayerAction && after.Enemies.Count > unit
                && Equals(after.Enemies[unit].Omen, track.Omen))
            {
                track.OmenPreview = TurnLoop.PreviewOmen(after, unit);
                track.OmenPreviewFor = track.Omen;
            }
            return CoreText.OmenOf(track.Omen, track.Def, Equals(track.OmenPreviewFor, track.Omen) ? track.OmenPreview : null);
        }

        /// <summary>
        /// js: CoreScriptWriter.js:1233 — #50: the 予定 beside the omen: its kind (icon and word), its
        /// cells, and for attack and guard the face number (#349). The second action's predicted value
        /// comes after this (#242). It outlives the spent first omen. Per enemy.
        /// </summary>
        private OmenFrame WithPlan(int unit, OmenFrame frame)
        {
            EnemyTrack track = _tracks[unit];
            if (!track.PlanVisible || track.Plan == null) return frame;
            OmenKind kind = track.Plan.Label.Kind;
            EnemyActionDef planned;
            track.Def.Actions.TryGetValue(track.Plan.ActionId, out planned);
            frame.PlanVisible = true;
            frame.PlanIcon = CoreText.IconOf(kind, planned);
            frame.PlanKindLabel = CoreText.OmenKindWord(kind);
            frame.PlanValueText = CoreText.OmenValueOf(kind, planned, null);
            frame.PlanStatuses = CoreText.OmenStatusesOf(kind, planned);
            frame.PlanSideGlyph = track.Plan.Label.Reach != null ? track.Plan.Label.Reach.ToText() : "";
            frame.PlanChanged = track.PlanChanged;
            return frame;
        }

        /// <summary>js: CoreScriptWriter.js:1249 — what a whiff strikes off: the number the omen badge was showing, or the face alone. Per enemy.</summary>
        private int ShownOmenPower(string actionId, int unit)
        {
            EnemyTrack track = _tracks[unit];
            if (track.OmenPreview != null && track.OmenPreviewFor != null && track.OmenPreviewFor.ActionId == actionId) return track.OmenPreview.RawPower;
            return CoreText.FacePower(track.Def.Actions[actionId].Face);
        }

        // js: CoreScriptWriter.js:1256
        private static UnitFrame UnitOf(UnitModel unit, bool showStamina, int? gap)
        {
            var frame = new UnitFrame
            {
                Hp = unit.Hp, HpMax = unit.HpMax, Guard = unit.Guard,
                ShowStamina = showStamina, Stamina = unit.Stamina, StaminaMax = unit.StaminaMax,
                HasRange = gap.HasValue, Down = unit.Down,
            };
            if (gap.HasValue) frame.RangeGlyph = CoreText.GapGlyph(gap.Value);
            // battle-visual-v1 §4.6 (#455, #605): the stances held are the list at the unit's edge, each
            // row with its hover panel text; none of them is a status chip.
            frame.Stances = StatusChipText.StanceRows(unit.Stances);
            foreach (KeyValuePair<StatusKind, int> pair in unit.Statuses)
            {
                frame.Statuses.Add(StatusChipText.Chip(pair.Key, pair.Value));
            }
            return frame;
        }

        /// <summary>js: CoreScriptWriter.js:1278 — the player, or enemy <paramref name="unit"/>.</summary>
        private UnitModel Unit(Actor actor, int unit)
        {
            return actor == Actor.Player ? _player : _tracks[unit].Model;
        }

        /// <summary>js: CoreScriptWriter.js:1283 — enemy <paramref name="unit"/>'s def (a stance's name is looked up in it), the first enemy's for a unit the writer does not know.</summary>
        private EnemyDef DefOf(int unit)
        {
            return unit >= 0 && unit < _tracks.Count ? _tracks[unit].Def : _tracks[0].Def;
        }
    }
}
