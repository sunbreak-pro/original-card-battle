// The battle the v4.3 core runs, offered to the screen as an IDepictionSource. Pure C#:
// BattleCore + Depiction.Script, no UnityEngine.
//
// This is the source of every played battle; the only other one is DepictionRunner (the fixed
// script kept for filming). The source that wrote the rules by hand is gone (#571), so the battle
// rules live in BattleCore alone. This class holds no rule of its own either: TurnLoop moves
// the battle, CoreScriptWriter turns what happened into screen events, and this class only decides
// which of the two the screen is waiting for. It does not read DemoDeck.
//
// Several enemies (port of the browser trial's JS extension, docs/port-notes/depiction-multi-enemy.md,
// js: src/bridge/depiction/CoreBattleSource.js). The source used to name no target, so TurnLoop read its
// default 0: the first enemy, also once it had fallen (and then every card aimed at one enemy was
// refused). Inspect, TryPlay, PreviewFor and RefusalText now also take the enemy the card is released on
// (its index in the core's enemy list, as DepictionFrame.Enemies[k].Unit), and without one the card goes
// where TargetFor sends it — the target every number on its face was read against. With one enemy both
// are 0, as before.
//
// 俊敏's free cell (battle_core_v4.md §5 #48, battle_ui_ux_v2 §3.8 / §5.1 順 7 / §5.7 順 10). The core
// opens the step at the turn start (TurnLoop.CanTakeFreeStep / TakeFreeStep), and the source used to offer
// the screen no way to take it, so Unity let it go. CanTakeFreeStep(dir) (read-only) and TakeFreeStep(dir)
// now do: the step's core events go through the same writer as every other player move (its Apply and
// Close, in one screen event of their own), and TakeFreeStep returns that event as EndTurn returns its
// first. The event's Kind is DepictionEventKind.FreeStep. A source that never calls them behaves as before.
using System;
using System.Collections.Generic;
using BattleCore;

namespace Depiction.Bridge
{
    /// <summary>
    /// What the trial's battle scene asks of its source beyond <see cref="IDepictionSource"/>
    /// (battle-flow §5.2): the target a card goes to, the verdicts and numbers on a named enemy, the
    /// unattended choice, and 俊敏's free cell. <see cref="CoreBattleSource"/> answers it; a recorder that
    /// wraps a source (the trial's save by replay) implements it by forwarding every call.
    /// <see cref="IDepictionSource"/> stays as it was for <see cref="DepictionRunner"/>.
    /// </summary>
    public interface ITrialSource : IDepictionSource
    {
        /// <summary>The core state behind the screen. For tests and tools; the View never reads it.</summary>
        BattleState State { get; }

        /// <summary>Every core event of the battle so far, from TurnLoop.Start on.</summary>
        IReadOnlyList<BattleEvent> History { get; }

        /// <summary>The unattended choice, whether or not the source was built to suggest (js: CoreBattleSource.js:142).</summary>
        string AutoCardId { get; }

        /// <summary>The enemy an unnamed release of the card goes to (js: CoreBattleSource.js:165).</summary>
        int TargetFor(string cardId);

        /// <summary><see cref="IDepictionSource.Inspect"/> on enemy <paramref name="target"/>; a negative one is no enemy named.</summary>
        PlayVerdict Inspect(string cardId, int target);

        /// <summary><see cref="IDepictionSource.TryPlay"/> released on enemy <paramref name="target"/>; a negative one is no enemy named.</summary>
        PlayVerdict TryPlay(string cardId, DropZone zone, int target, out DepictionEvent played);

        /// <summary><see cref="IDepictionSource.PreviewFor"/> read on enemy <paramref name="target"/>; a negative one is no enemy named.</summary>
        string PreviewFor(string cardId, int target);

        /// <summary><see cref="IDepictionSource.RefusalText"/> with the numbers of enemy <paramref name="target"/>; a negative one is no enemy named.</summary>
        string RefusalText(string cardId, PlayVerdict verdict, int target);

        /// <summary>Whether 俊敏's free cell may be taken now; with <paramref name="dir"/> (+1 / −1), also whether the line has a cell that way.</summary>
        bool CanTakeFreeStep(int? dir = null);

        /// <summary>Takes 俊敏's free cell, <paramref name="dir"/> +1 forward or −1 back; the screen event it plays as.</summary>
        DepictionEvent TakeFreeStep(int dir);
    }

    public sealed class CoreBattleSource : ITrialSource
    {
        private readonly IRng _rng;
        private readonly CoreScriptWriter _writer;
        private readonly Queue<DepictionEvent> _pending = new Queue<DepictionEvent>();
        private readonly List<BattleEvent> _history = new List<BattleEvent>();
        private readonly bool _suggestCards;
        private readonly int _stopAfterTurns;
        private BattleState _state;

        /// <summary>The gap from which the unattended run stops stepping back (<see cref="SuggestedCardId"/>).</summary>
        public const int FleeGap = 2;

        /// <summary>js: CoreBattleSource.js:58 — DepictionEvent.Kind of the free step's event.</summary>
        public const DepictionEventKind FreeStepKind = DepictionEventKind.FreeStep;

        /// <summary>js: CoreBattleSource.js:61 — the free step event's title (the status's name, battle_core_v4.md §5).</summary>
        public const string FreeStepTitle = "俊敏";

        /// <param name="setup">Who fights whom, with which deck.</param>
        /// <param name="seed">Fixes every shuffle, so the same seed replays the same battle here and under `dotnet test`.</param>
        /// <param name="suggestCards">
        /// Unattended runs only (captures, the PlayMode test): suggest the leftmost card that can be
        /// paid for, and nothing once none can. A played battle leaves the choice to the player.
        /// </param>
        /// <param name="stopAfterTurns">Unattended runs only: report Finished once this many turns have closed on their next omen. 0 = fight to the end.</param>
        /// <param name="chainIndex">§12 連戦 (#191): which battle of the chain this is (1-based), for the corner of the screen.</param>
        /// <param name="chainTotal">How many battles the chain holds; 1 for a battle on its own.</param>
        /// <param name="disclosure">
        /// battle-visual-v1 §4.4 (#455): the journal's 開示度 for this enemy (0〜2); below 2 the omen shows
        /// no numbers. Read once here, as the battle starts (#500): BattleLaunch and DemoSession hand it in.
        /// </param>
        public CoreBattleSource(BattleSetup setup, int seed, bool suggestCards = false, int stopAfterTurns = 0, int chainIndex = 1, int chainTotal = 1,
            int disclosure = OmenDisclosure.Full)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (stopAfterTurns < 0) throw new ArgumentOutOfRangeException(nameof(stopAfterTurns), stopAfterTurns, "0 or more.");
            _suggestCards = suggestCards;
            _stopAfterTurns = stopAfterTurns;
            _rng = new SeededRng(seed);
            _writer = new CoreScriptWriter(setup.Enemy) { ChainIndex = chainIndex, ChainTotal = chainTotal, Disclosure = disclosure };
            StepResult start = TurnLoop.Start(setup, _rng);
            _state = start.State;
            _history.AddRange(start.Events);
            Frame = _writer.Opening(_state);
        }

        /// <summary>The slice: the polearm against the ten-kind prototype deck.</summary>
        public static CoreBattleSource Slice(int seed)
        {
            return new CoreBattleSource(BattleSetup.Slice(), seed);
        }

        /// <summary>The core state behind the screen. For tests and tools; the View never reads it.</summary>
        public BattleState State => _state;

        /// <summary>Every core event of the battle so far, from TurnLoop.Start on. The end screen counts from it (Chain.Tally, #191).</summary>
        public IReadOnlyList<BattleEvent> History => _history;

        public DepictionFrame Frame { get; private set; }

        public bool Finished => _pending.Count == 0 && (_state.Result != GameResult.Ongoing || ReachedTurnLimit);

        public bool WaitingForPlayer =>
            _pending.Count == 0 && _state.Result == GameResult.Ongoing && _state.Phase == BattlePhase.PlayerAction;

        /// <summary>The turn limit is only looked at between turns, so a limited run still ends on a next omen.</summary>
        private bool ReachedTurnLimit =>
            _stopAfterTurns > 0 && _state.Phase == BattlePhase.AwaitingTurnStart && _state.Turn >= _stopAfterTurns;

        public bool CanEndTurn => WaitingForPlayer;

        /// <summary>
        /// js: CoreBattleSource.js:131 — a played battle has no opinion about which card to drag; an
        /// unattended run takes the leftmost payable one — except a card that steps back while the gap is
        /// already 2 or more (#192). Backing off to the end of the line puts the player out of every reach
        /// of 大黒蛇 セルク, which never moves and binds the feet every phase, and the run would never end
        /// (#196). The gap is the nearest standing enemy's (the one-enemy source read the first enemy's, also
        /// once it had fallen), and a card is payable on the enemy <see cref="TargetFor"/> sends it to.
        /// </summary>
        public string SuggestedCardId
        {
            get
            {
                if (!_suggestCards || !WaitingForPlayer) return "";
                return AutoCardId;
            }
        }

        /// <summary>
        /// js: CoreBattleSource.js:142 — <see cref="SuggestedCardId"/>'s choice whether or not this source
        /// was built to suggest: for the game's ?autobattle test switch, which also fights a fight a reload
        /// brings back (that source is replayed as the player's page built it, suggestCards false, and with
        /// SuggestedCardId alone the run only ended turns). Read-only: the save recorder logs no read.
        /// </summary>
        public string AutoCardId
        {
            get
            {
                if (!WaitingForPlayer) return "";
                foreach (CardInstance card in _state.Hand)
                {
                    if (card.Def.Face.Move < 0 && NearestGap >= FleeGap) continue;
                    if (TurnLoop.CanPlay(_state, card.InstanceId, TargetFor(card.InstanceId)) == PlayRefusal.None) return card.InstanceId;
                }
                return "";
            }
        }

        /// <summary>js: CoreBattleSource.js:152 — N to the nearest standing enemy (BattleState.Gap, the first enemy's, when none stands).</summary>
        private int NearestGap
        {
            get
            {
                int nearest = _state.Nearest;
                return nearest < 0 ? _state.Gap : _state.GapTo(nearest);
            }
        }

        /// <summary>
        /// js: CoreBattleSource.js:165 — the enemy (its index in the core's enemy list) a card released on
        /// the receiver goes to when the screen names none, and the one its face's cost and preview are
        /// read against (<see cref="CoreScriptWriter.TargetOf"/>): of the standing enemies it can be played
        /// on now, the one it costs least on (the nearest on a tie); else the same among the ones inside
        /// its reach; else the nearest standing one. A card aimed at nobody or at every enemy in reach
        /// ignores it. Always 0 in a one-enemy battle.
        /// </summary>
        public int TargetFor(string cardId)
        {
            return CoreScriptWriter.TargetOf(_state, cardId);
        }

        public string GuideText
        {
            get
            {
                if (_pending.Count > 0) return "";
                // The outcome is the end screen's to show (#191); the guide line says nothing once the battle is over.
                if (_state.Result != GameResult.Ongoing) return "";
                if (ReachedTurnLimit) return "";
                if (!WaitingForPlayer) return "";
                return HasPlayableCard()
                    ? "出したい札を離してください。手が済んだら「ターン終了」"
                    : "出せる札がありません。「ターン終了」を押してください";
            }
        }

        public DepictionEvent AdvanceAuto()
        {
            if (_pending.Count == 0)
            {
                if (Finished) throw new InvalidOperationException("The battle is over.");
                if (_state.Phase != BattlePhase.AwaitingTurnStart) throw new InvalidOperationException("The turn waits for the player.");
                Take(TurnLoop.BeginPlayerTurn(_state, _rng));
            }
            return Next();
        }

        public DepictionEvent EndTurn()
        {
            if (!CanEndTurn) throw new InvalidOperationException("The turn cannot be ended right now.");
            Take(TurnLoop.EndTurn(_state, _rng));
            return Next();
        }

        /// <summary>
        /// js: CoreBattleSource.js:204 — whether the player may take 俊敏's free cell now: the battle waits
        /// for the player and the core holds the step open (it opens at the turn start and goes with the
        /// first card played or the turn's end). With <paramref name="dir"/> (+1 forward, −1 back), also
        /// whether the line has a cell that way: the move the core would make (Field.Move) changes the
        /// player's cell. Only the line closes a way (the end of the line, the nearest standing enemy):
        /// 呪縛, 鈍足 and 鉤爪 do not, the core takes the step and answers with MoveBlocked (battle_ui_ux_v2
        /// §3.8: the arrow is shown under 呪縛 too). Read-only; any other <paramref name="dir"/> is false.
        /// </summary>
        public bool CanTakeFreeStep(int? dir = null)
        {
            if (!WaitingForPlayer || !TurnLoop.CanTakeFreeStep(_state)) return false;
            if (dir == null) return true;
            if (dir.Value != 1 && dir.Value != -1) return false;
            Shift shift = Field.Move(_state, Actor.Player, dir.Value);
            return shift.To != shift.From;
        }

        /// <summary>
        /// js: CoreBattleSource.js:219 — the player takes 俊敏's free cell, <paramref name="dir"/> +1 forward
        /// or −1 back. Returns the screen event the step plays as: the walk of one cell (a RangeSwitch
        /// cue), or what stopped it (a MoveBlocked line: 呪縛 / 鉤爪), or nothing to show (鈍足, the line).
        /// Staying needs no call: the first card or the turn's end lets the step go. Throws when the step
        /// cannot be taken now (<see cref="CanTakeFreeStep"/>() is false) or <paramref name="dir"/> is not ±1.
        /// </summary>
        public DepictionEvent TakeFreeStep(int dir)
        {
            if (!CanTakeFreeStep()) throw new InvalidOperationException("The free step cannot be taken right now.");
            if (dir != 1 && dir != -1) throw new ArgumentOutOfRangeException(nameof(dir), dir, "The free step is one cell: +1 (forward) or -1 (back).");
            StepResult step = TurnLoop.TakeFreeStep(_state, dir);
            _state = step.State;
            _history.AddRange(step.Events);
            _pending.Enqueue(WriteFreeStep(step.Events));
            return Next();
        }

        /// <summary>
        /// js: CoreBattleSource.js:234 — the free step's core events as one screen event, written by the
        /// writer as it writes any move of the player's turn (its Apply per core event, its Close for the
        /// settled frame). The writer's own Write opens a screen event only at a turn start, a card or an
        /// enemy beat, and would drop these.
        /// </summary>
        private DepictionEvent WriteFreeStep(IReadOnlyList<BattleEvent> events)
        {
            _writer.EnsureTracks(_state);
            DepictionEvent ev = _writer.NewEvent(FreeStepKind, FreeStepTitle);
            foreach (BattleEvent e in events) _writer.Apply(e, ev);
            _writer.Close(ev, _state);
            return ev;
        }

        public PlayVerdict Inspect(string cardId)
        {
            return Inspect(cardId, -1);
        }

        /// <summary>
        /// js: CoreBattleSource.js:248 — <paramref name="target"/> is the enemy the card would be released on
        /// (its index in the core's enemy list); a negative one names none (the JS's omitted argument), and
        /// the card is judged on <see cref="TargetFor"/>'s. A target that is not a standing enemy answers
        /// NotWaiting, as any refusal the screen has no word for (the screen offers no receiver on a fallen enemy).
        /// </summary>
        public PlayVerdict Inspect(string cardId, int target)
        {
            if (!WaitingForPlayer) return PlayVerdict.NotWaiting;
            switch (TurnLoop.CanPlay(_state, cardId, Aimed(cardId, target)))
            {
                case PlayRefusal.None: return PlayVerdict.Accepted;
                case PlayRefusal.NotInHand: return PlayVerdict.NotInHand;
                case PlayRefusal.NotEnoughStamina: return PlayVerdict.NotEnoughStamina;
                case PlayRefusal.OutOfReach: return PlayVerdict.OutOfRange;
                // #335: 呪縛 has a reason of its own to give, not the silent "not now".
                case PlayRefusal.Bound: return PlayVerdict.Bound;
                default: return PlayVerdict.NotWaiting;
            }
        }

        public PlayVerdict TryPlay(string cardId, DropZone zone, out DepictionEvent played)
        {
            return TryPlay(cardId, zone, -1, out played);
        }

        /// <summary>
        /// js: CoreBattleSource.js:270 — <paramref name="target"/> is the enemy the card is released on (its
        /// index in the core's enemy list); a negative one names none, and the card goes to <see cref="TargetFor"/>'s.
        /// </summary>
        public PlayVerdict TryPlay(string cardId, DropZone zone, int target, out DepictionEvent played)
        {
            played = null;
            int aimed = Aimed(cardId, target);
            PlayVerdict verdict = Inspect(cardId, aimed);
            if (verdict != PlayVerdict.Accepted) return verdict;

            CardFace face = DepictionText.Find(Frame.Hand, cardId);
            if (face == null) return PlayVerdict.NotInHand;
            if (zone != DepictionText.RequiredZone(face.Aim)) return PlayVerdict.WrongZone;

            Take(TurnLoop.PlayCard(_state, cardId, _rng, aimed));
            played = Next();
            return PlayVerdict.Accepted;
        }

        public string PreviewFor(string cardId)
        {
            return PreviewFor(cardId, -1);
        }

        /// <summary>
        /// js: CoreBattleSource.js:294 — the one number shown while a card is held: for an attack, the
        /// whole card's power before the enemy's Guard (#248: every blow and the wall, as one sum); else
        /// the Guard it gives. The number sits beside the power icon, and what gets past the Guard is the
        /// HP bar's hatching, not this (battle-visual-v1.md §5.2, §8). <paramref name="target"/> is the
        /// enemy a held card frames, the one the number is read on; a negative one names none, and the
        /// number is read on <see cref="TargetFor"/>'s — the target TryPlay would use.
        /// </summary>
        public string PreviewFor(string cardId, int target)
        {
            int aimed = Aimed(cardId, target);
            if (Inspect(cardId, aimed) != PlayVerdict.Accepted) return "";
            PlayPreview preview = TurnLoop.Preview(_state, cardId, aimed);
            if (preview == null) return "";
            if (preview.Attacks) return preview.RawPower.ToString();
            return preview.GuardGain > 0 ? preview.GuardGain.ToString() : "";
        }

        public string RefusalText(string cardId, PlayVerdict verdict)
        {
            return RefusalText(cardId, verdict, -1);
        }

        /// <summary>
        /// js: CoreBattleSource.js:314 — the battle-visual-v1 §5.4 出せなかった理由 line: why a released card
        /// went back to the hand. <paramref name="target"/> is the enemy the card was released on (its
        /// index in the core's enemy list); a negative one names none, and the line is about the one TryPlay
        /// sends it to (CardFace.TargetUnit). Both numbers the line quotes are that enemy's, as the frame
        /// gives them: the cost of the card on it (CardFace.CostFor — 石礫 costs 1 less on an enemy at N 2
        /// or more) and its N (DepictionFrame.Enemies[k].GapGlyph; Player.RangeGlyph for a card aimed at
        /// nobody or an enemy that does not stand). OutOfRange is written whenever it is asked for, also
        /// while another enemy is inside the card's reach: §5.1's "どの敵にも届かないときだけ" is the hover
        /// panel's rule (CardFace.ReachHint), not this line's. A one-enemy battle answers as before.
        /// </summary>
        public string RefusalText(string cardId, PlayVerdict verdict, int target)
        {
            CardFace face = DepictionText.Find(Frame.Hand, cardId);
            if (face == null) return "";
            bool named = target >= 0;
            switch (verdict)
            {
                case PlayVerdict.NotEnoughStamina:
                    return "「" + face.Name + "」はスタミナ " + (named ? face.CostFor(target) : face.Cost) + " が要ります。残りは " + Frame.Player.Stamina + " です";
                case PlayVerdict.OutOfRange:
                    return "「" + face.Name + "」は間合い " + face.RequiredRangeGlyph + " の相手にしか出せません。いまは " + ShownGapGlyph(named ? target : face.TargetUnit) + " です";
                case PlayVerdict.WrongZone:
                    return DepictionText.WrongZoneText(face);
                case PlayVerdict.Bound:
                    return CoreText.BoundRefusal(face.Name);
                default:
                    return "";
            }
        }

        /// <summary>
        /// js: CoreBattleSource.js:335 — the N of enemy <paramref name="unit"/> as the frame shows it (its
        /// GapGlyph), or Player.RangeGlyph (the focus enemy's, as before) when <paramref name="unit"/> is −1
        /// or does not stand.
        /// </summary>
        private string ShownGapGlyph(int unit)
        {
            UnitFrame enemy = null;
            if (unit >= 0)
            {
                foreach (UnitFrame u in Frame.Enemies)
                {
                    if (u.Unit == unit)
                    {
                        enemy = u;
                        break;
                    }
                }
            }
            return enemy != null && enemy.Gap >= 0 ? enemy.GapGlyph : Frame.Player.RangeGlyph;
        }

        /// <summary>The enemy a call is about: <paramref name="target"/>, or <see cref="TargetFor"/>'s when it names none (negative; the JS's `target ?? this.TargetFor(cardId)`).</summary>
        private int Aimed(string cardId, int target)
        {
            return target >= 0 ? target : TargetFor(cardId);
        }

        private void Take(StepResult step)
        {
            _state = step.State;
            _history.AddRange(step.Events);
            foreach (DepictionEvent ev in _writer.Write(step.Events, _state)) _pending.Enqueue(ev);
        }

        private DepictionEvent Next()
        {
            DepictionEvent ev = _pending.Dequeue();
            Frame = ev.After;
            return ev;
        }

        /// <summary>js: CoreBattleSource.js:356 — each card on the enemy <see cref="TargetFor"/> sends it to.</summary>
        private bool HasPlayableCard()
        {
            foreach (CardInstance card in _state.Hand)
            {
                if (TurnLoop.CanPlay(_state, card.InstanceId, TargetFor(card.InstanceId)) == PlayRefusal.None) return true;
            }
            return false;
        }
    }
}
