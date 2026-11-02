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
//
// The class is one partial class over several files by section (#580): this one holds the state, Opening,
// Write and the falls; .UnitModel (the numbers kept per unit and per enemy), .Boundary (where a screen
// event begins), .Apply (one core event → its beats), .Cues (the beats those cases share), .Target (the
// enemy a card goes to) and .Frames (the frame written after each event).
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;

namespace Depiction.Bridge
{
    public sealed partial class CoreScriptWriter
    {
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
        /// <summary>The power the trait of the card or action in play added to each blow, and that trait (#455); 0 and null when none did.</summary>
        private int _traitPower;
        private Trait _traitOfPower;

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
        /// enemy of the battle unless <see cref="Disclosures"/> says otherwise (EncounterBattle hands in the
        /// lowest of them).
        /// </summary>
        public int Disclosure { get; set; } = OmenDisclosure.Full;

        /// <summary>
        /// #589: the journal's 開示度 per enemy, in the core's enemy order, so each enemy's omen shows what the
        /// journal has read of that enemy. Null, or an enemy past its end, reads <see cref="Disclosure"/>.
        /// </summary>
        public IReadOnlyList<int> Disclosures { get; set; }

        /// <summary>#589: how far the journal has read enemy <paramref name="unit"/>.</summary>
        private int DisclosureOf(int unit)
        {
            return Disclosures != null && unit >= 0 && unit < Disclosures.Count ? Disclosures[unit] : Disclosure;
        }

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
    }
}
