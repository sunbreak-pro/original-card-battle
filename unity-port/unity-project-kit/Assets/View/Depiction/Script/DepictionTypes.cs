// Display data for the v4.2 battle depiction. Pure C#: no UnityEngine, no BattleCore.
// The screen never computes a rule — every number, label and "did the condition hold"
// comes from these types. Today a hand-written script fills them (TurnSliceScript);
// later the v4.2 core's output is mapped into the same types.
using System;
using System.Collections.Generic;

namespace Depiction
{
    public enum RangeSide
    {
        Near,
        Far,
    }

    public enum UnitSide
    {
        Player,
        Enemy,
    }

    /// <summary>Attribute of a card face; the View borrows the role colours (battle_ui_ux_v2 §11.4).</summary>
    public enum CardKind
    {
        Attack,
        Guard,
        Stance,
        Skill,
        Move,
    }

    /// <summary>How the card is released: on the target's receiver, or above the throw line.</summary>
    public enum CardAim
    {
        Single,
        Self,
    }

    public enum DropZone
    {
        None,
        Receiver,
        AboveThrowLine,
    }

    public sealed class CardFace
    {
        public string Id;
        public string Name;
        public int Cost;
        public CardKind Kind;
        public CardAim Aim;
        /// <summary>
        /// Throw-line cards only: whose figures the effect lands on, so the View can mark them
        /// while the card is held. Required when <see cref="Aim"/> is Self; a receiver card
        /// already shows its target with the dish.
        /// </summary>
        public UnitSide? Affects;
        /// <summary>Kind and target as printed under the name ("攻撃・敵単体", "防御・自分").</summary>
        public string TypeLabel = "";
        /// <summary>
        /// What the card does, as printed on its face: up to three sentences of at most nine full-width
        /// characters each. The card prints one sentence per line.
        /// </summary>
        public string Description = "";
        /// <summary>The face value as printed ("6", "13", "遠 +4"). Empty when the card has none.</summary>
        public string ValueText = "";
        /// <summary>Trait line as printed ("初手 +3"). Empty when the card has no trait.</summary>
        public string TraitText = "";
        /// <summary>True when the trait's condition holds right now (lamp lit).</summary>
        public bool TraitLit;
        /// <summary>
        /// The range side the card may be played from, or null when either side works. A live turn
        /// refuses a card held on the other side (<see cref="PlayVerdict.OutOfRange"/>); the fixed
        /// script never looks at it.
        /// </summary>
        public RangeSide? RequiredRange;
        /// <summary>The glyph of <see cref="RequiredRange"/> as printed ("近"). Empty when the card has none.</summary>
        public string RequiredRangeGlyph = "";
    }

    public sealed class StatusChip
    {
        public string Label;
        public int Stacks;
    }

    public sealed class UnitFrame
    {
        public int Hp;
        public int HpMax;
        public int Guard;
        public bool ShowStamina;
        public int Stamina;
        public int StaminaMax;
        /// <summary>Only bosses, elites and the player hold a range side (battle_core_v4 §17.3).</summary>
        public bool HasRange;
        public RangeSide Range;
        /// <summary>The one glyph on the tag at the unit's feet ("近" / "遠"). Required when <see cref="HasRange"/>.</summary>
        public string RangeGlyph = "";
        public List<StatusChip> Statuses = new List<StatusChip>();
    }

    public sealed class OmenFrame
    {
        public bool Visible;
        /// <summary>Kind only at disclosure 1: "攻撃", "防御".</summary>
        public string KindLabel = "";
        /// <summary>The side the omen punishes, one glyph ("近" / "遠"). Empty when none.</summary>
        public string SideGlyph = "";
        public string ValueText = "";
    }

    public sealed class CornerFrame
    {
        public int Turn;
        public int Floor;
        public int ChainIndex;
        public int ChainTotal;
        public int MiasmaPercent;
    }

    /// <summary>How the battle stands: still going, or over one way or the other (the one card, #77).</summary>
    public enum BattleOutcome
    {
        Ongoing,
        Won,
        Lost,
    }

    /// <summary>One settled screen. The View can rebuild everything it shows from a frame alone.</summary>
    public sealed class DepictionFrame
    {
        /// <summary>Won or Lost once the battle is over; the View shows the one card for it.</summary>
        public BattleOutcome Outcome = BattleOutcome.Ongoing;
        public CornerFrame Corner = new CornerFrame();
        public UnitFrame Player = new UnitFrame();
        public UnitFrame Enemy = new UnitFrame();
        public OmenFrame Omen = new OmenFrame();
        public List<CardFace> Hand = new List<CardFace>();
        /// <summary>Shield hint beside the end-turn button ("+3"); empty when the stance would not trigger.</summary>
        public string StanceHint = "";
    }

    /// <summary>
    /// battle_ui_ux_v2 §5.3 の系統, as far as the slice plays them: the shape a blow draws and the way
    /// an enemy moves into it. The writer of the script picks it (<see cref="StrikeSystems.Of"/>); the
    /// View only draws what it is told.
    /// </summary>
    public enum StrikeSystem
    {
        /// <summary>斬: a diagonal arc across the target. The default.</summary>
        Slash,

        /// <summary>突: one straight line into the target.</summary>
        Thrust,

        /// <summary>払: a wide horizontal band.</summary>
        Sweep,

        /// <summary>打: a ripple where it lands. Pushes and pulls play as this.</summary>
        Strike,

        /// <summary>盾: no blow; the figure raises its guard.</summary>
        Shield,

        /// <summary>A move without a blow: the figure steps.</summary>
        Step,
    }

    /// <summary>§5.3: the system from the technique's name and faces. The canon's priority is 突 → 打 → 払 → 斬.</summary>
    public static class StrikeSystems
    {
        public static StrikeSystem Of(string name, bool attacks, bool guards, bool moves, bool pushes)
        {
            name = name ?? "";
            if (attacks)
            {
                // A push or pull is what the action is for (石突きの押し込み, whose name holds 突), so it plays as 打.
                if (pushes) return StrikeSystem.Strike;
                if (HasAny(name, "突", "貫", "矢", "弩", "撃", "投")) return StrikeSystem.Thrust;
                if (HasAny(name, "打", "当", "礫", "圧殺", "押し込み")) return StrikeSystem.Strike;
                if (HasAny(name, "薙", "払", "牽制")) return StrikeSystem.Sweep;
                return StrikeSystem.Slash;
            }
            if (moves) return StrikeSystem.Step;
            return guards ? StrikeSystem.Shield : StrikeSystem.Step;
        }

        private static bool HasAny(string name, params string[] words)
        {
            foreach (string word in words)
            {
                if (name.Contains(word)) return true;
            }
            return false;
        }
    }

    public enum DepictionEventKind
    {
        TurnStart,
        PlayCard,
        TurnEnd,
        EnemyAction,
        NextOmen,
    }

    public enum CueKind
    {
        GuardReset,
        StaminaChange,
        DrawHand,
        OmenShow,
        TraitFire,
        Slash,
        GuardGain,
        RangeSwitch,
        StanceCue,
        DiscardHand,
        EnemyWindup,
        SideBonusMiss,
        GuardBlock,
        Hit,
        /// <summary>
        /// A status word gained or lost stacks (battle_core_v4 §5). Added for the core-driven turn
        /// (issue #73); how it plays is issue #77's, and until then the chips simply settle with the frame.
        /// </summary>
        StatusChange,
        /// <summary>
        /// HP moved without a blow (#188): 出血 / 再生 at turn start, a heal face, a 見切り return.
        /// <see cref="Cue.Amount"/> is signed (−4, +6), <see cref="Cue.HpAfter"/> settled and
        /// <see cref="Cue.Text"/> names the cause ("出血"). Unlike <see cref="Hit"/> it spends no omen.
        /// </summary>
        HpChange,
    }

    /// <summary>
    /// One beat inside an event. Values are settled results, never inputs to a formula:
    /// the View shows <see cref="Amount"/> and then sets the gauges to the *After values.
    /// </summary>
    public sealed class Cue
    {
        public const int Unchanged = -1;

        public CueKind Kind;
        public UnitSide Target;
        /// <summary>Slash only: who attacks. Required there, so the View never guesses it from the target.</summary>
        public UnitSide? Source;
        public int Amount;
        public string Text = "";
        /// <summary>Strength of the effect, 1..4, decided from the settled value by whoever writes the script.</summary>
        public int Intensity = 1;
        public int HpAfter = Unchanged;
        public int GuardAfter = Unchanged;
        public int StaminaAfter = Unchanged;
        /// <summary>StaminaChange only: how many pips the unit has, so the View never looks outside the cue.</summary>
        public int StaminaMax = Unchanged;
        public RangeSide RangeAfter;
        /// <summary>RangeSwitch only: the glyph the tag flips to ("遠").</summary>
        public string RangeGlyphAfter = "";
        /// <summary>StatusChange only: the stacks left of the word named in <see cref="Text"/> (0 = gone).</summary>
        public int StacksAfter = Unchanged;
        /// <summary>Slash and EnemyWindup: the shape of the blow and of the enemy's move (§5.3).</summary>
        public StrikeSystem System = StrikeSystem.Slash;
        /// <summary>RangeSwitch only: the other side moved the player (a push or a pull), not the player itself.</summary>
        public bool Pushed;
    }

    public sealed class DepictionEvent
    {
        public int Order;
        public DepictionEventKind Kind;
        public string Title = "";
        /// <summary>PlayCard only: the card the script expects and where it must be released.</summary>
        public string CardId = "";
        public CardAim Aim;
        /// <summary>PlayCard only: the single predicted value shown on the receiver while dragging.</summary>
        public string PreviewText = "";
        public List<Cue> Cues = new List<Cue>();
        /// <summary>The settled screen once every cue has played.</summary>
        public DepictionFrame After;

        public bool WaitsForDrag => Kind == DepictionEventKind.PlayCard;
    }

    public sealed class DepictionScript
    {
        public DepictionFrame Opening;
        public List<DepictionEvent> Events = new List<DepictionEvent>();
    }

    public enum PlayVerdict
    {
        Accepted,
        /// <summary>Nothing waits for the player: an event is still playing.</summary>
        NotWaiting,
        /// <summary>The card is no longer in the hand.</summary>
        NotInHand,
        /// <summary>The card costs more stamina than is left.</summary>
        NotEnoughStamina,
        /// <summary>The card only works from the other range side.</summary>
        OutOfRange,
        /// <summary>Released somewhere this card cannot be released.</summary>
        WrongZone,
        /// <summary>
        /// Fixed-script playback only: the script writes one order and this is not its next card.
        /// A live turn never answers this — there the hand is free (issue #36).
        /// </summary>
        OffScript,
    }

    /// <summary>Zones and strings the screen shows as given. Shared by every source.</summary>
    public static class DepictionText
    {
        public static DropZone RequiredZone(CardAim aim)
        {
            return aim == CardAim.Single ? DropZone.Receiver : DropZone.AboveThrowLine;
        }

        /// <summary>The turn banners and the one card at the end (battle_ui_ux_v2 §5.1 順 1 / §5.13 順 5, #77).</summary>
        public const string YourTurn = "あなたの番";
        public const string EnemyTurn = "敵の番";
        public const string Won = "勝ち";
        public const string Lost = "負け";

        /// <summary>The muted label on a figure the other side pushed (battle_ui_ux_v2 §5.7 順 6).</summary>
        public const string Pushed = "押し出し";

        public static string OutcomeText(BattleOutcome outcome)
        {
            return outcome == BattleOutcome.Won ? Won : outcome == BattleOutcome.Lost ? Lost : "";
        }

        public static string ZoneName(CardAim aim)
        {
            return aim == CardAim.Single ? "敵の受け皿の上" : "投げ上げ線より上";
        }

        public static CardFace Find(List<CardFace> hand, string cardId)
        {
            foreach (CardFace face in hand)
            {
                if (face.Id == cardId) return face;
            }
            return null;
        }

        public static string NameOf(List<CardFace> hand, string cardId)
        {
            CardFace face = Find(hand, cardId);
            return face == null ? "" : face.Name;
        }
    }

    /// <summary>
    /// What the screen plays, and what it may refuse. Two sources answer it: the fixed script kept
    /// for filming (<see cref="DepictionRunner"/>) and the turn the player actually fights
    /// (<see cref="LiveTurn"/>). The View holds one of them and never asks which it is.
    /// Every line of text it shows is written here, so the View still prints rather than computes.
    /// </summary>
    public interface IDepictionSource
    {
        /// <summary>The settled screen right now.</summary>
        DepictionFrame Frame { get; }

        /// <summary>True when nothing is left to play.</summary>
        bool Finished { get; }

        /// <summary>True while the screen waits for the player to release a card or end the turn.</summary>
        bool WaitingForPlayer { get; }

        /// <summary>True when the player may end the turn right now.</summary>
        bool CanEndTurn { get; }

        /// <summary>Takes the next event that runs on its own. Throws while the source waits for the player.</summary>
        DepictionEvent AdvanceAuto();

        /// <summary>Ends the turn. Throws when <see cref="CanEndTurn"/> is false.</summary>
        DepictionEvent EndTurn();

        /// <summary>Why the card cannot be released at all right now; Accepted when it can.</summary>
        PlayVerdict Inspect(string cardId);

        PlayVerdict TryPlay(string cardId, DropZone zone, out DepictionEvent played);

        /// <summary>The one predicted value shown on the receiver while the card is dragged; "" when none.</summary>
        string PreviewFor(string cardId);

        /// <summary>The card the source would drag by itself for a capture, or "" when it has no opinion.</summary>
        string SuggestedCardId { get; }

        /// <summary>The line above the hand while the source waits, or "".</summary>
        string GuideText { get; }

        /// <summary>Why a released card went back to the hand, or "" when there is nothing to say.</summary>
        string RefusalText(string cardId, PlayVerdict verdict);
    }

    /// <summary>
    /// Walks a fixed script in order, for captures and for filming a known slice. Holds no rules:
    /// it only knows which event is next, whether that event waits for a drag, and whether a
    /// released card is the one the script wrote. A player who wants a free hand takes
    /// <see cref="LiveTurn"/> instead.
    /// </summary>
    public sealed class DepictionRunner : IDepictionSource
    {
        private readonly DepictionScript _script;

        public DepictionRunner(DepictionScript script)
        {
            _script = script ?? throw new ArgumentNullException(nameof(script));
            Frame = script.Opening;
        }

        public int Index { get; private set; }
        public DepictionFrame Frame { get; private set; }
        public bool Finished => Index >= _script.Events.Count;
        public DepictionEvent Next => Finished ? null : _script.Events[Index];
        public bool WaitingForPlayer => !Finished && Next.WaitsForDrag;

        /// <summary>A fixed script ends its own turns, so the player never does.</summary>
        public bool CanEndTurn => false;

        public string SuggestedCardId => WaitingForPlayer ? Next.CardId : "";

        public string GuideText
        {
            get
            {
                if (!WaitingForPlayer) return "";
                DepictionEvent next = Next;
                return "台本の次の一手：「" + DepictionText.NameOf(Frame.Hand, next.CardId) + "」を"
                    + DepictionText.ZoneName(next.Aim) + "へ";
            }
        }

        /// <summary>Takes the next automatic event. Throws when the script is waiting for a drag.</summary>
        public DepictionEvent AdvanceAuto()
        {
            if (Finished) throw new InvalidOperationException("The script has already finished.");
            if (WaitingForPlayer) throw new InvalidOperationException("Event " + Next.Order + " waits for a drag.");
            return Take();
        }

        public DepictionEvent EndTurn()
        {
            throw new InvalidOperationException("A fixed script ends its own turn; the player cannot.");
        }

        public PlayVerdict Inspect(string cardId)
        {
            if (!WaitingForPlayer) return PlayVerdict.NotWaiting;
            return Next.CardId == cardId ? PlayVerdict.Accepted : PlayVerdict.OffScript;
        }

        public PlayVerdict TryPlay(string cardId, DropZone zone, out DepictionEvent played)
        {
            played = null;
            PlayVerdict verdict = Inspect(cardId);
            if (verdict != PlayVerdict.Accepted) return verdict;
            if (zone != DepictionText.RequiredZone(Next.Aim)) return PlayVerdict.WrongZone;
            played = Take();
            return PlayVerdict.Accepted;
        }

        public string PreviewFor(string cardId)
        {
            DepictionEvent next = Next;
            return next != null && next.CardId == cardId ? next.PreviewText : "";
        }

        public string RefusalText(string cardId, PlayVerdict verdict)
        {
            CardFace face = DepictionText.Find(Frame.Hand, cardId);
            if (face == null || Next == null) return "";
            if (verdict == PlayVerdict.OffScript)
            {
                return "「" + face.Name + "」はまだ出せません。台本の次は「"
                    + DepictionText.NameOf(Frame.Hand, Next.CardId) + "」です";
            }
            if (verdict == PlayVerdict.WrongZone)
            {
                return "「" + face.Name + "」は" + DepictionText.ZoneName(face.Aim) + "で離すと出せます";
            }
            return "";
        }

        public static DropZone RequiredZone(CardAim aim)
        {
            return DepictionText.RequiredZone(aim);
        }

        private DepictionEvent Take()
        {
            DepictionEvent ev = _script.Events[Index];
            Index += 1;
            Frame = ev.After;
            return ev;
        }
    }
}
