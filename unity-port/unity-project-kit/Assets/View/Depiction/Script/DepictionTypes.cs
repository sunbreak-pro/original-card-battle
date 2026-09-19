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
        /// <summary>The face value as printed ("6", "13", "遠 +4"). Empty when the card has none.</summary>
        public string ValueText = "";
        /// <summary>Trait line as printed ("初手 +3"). Empty when the card has no trait.</summary>
        public string TraitText = "";
        /// <summary>True when the trait's condition holds right now (lamp lit).</summary>
        public bool TraitLit;
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

    /// <summary>One settled screen. The View can rebuild everything it shows from a frame alone.</summary>
    public sealed class DepictionFrame
    {
        public CornerFrame Corner = new CornerFrame();
        public UnitFrame Player = new UnitFrame();
        public UnitFrame Enemy = new UnitFrame();
        public OmenFrame Omen = new OmenFrame();
        public List<CardFace> Hand = new List<CardFace>();
        /// <summary>Shield hint beside the end-turn button ("+3"); empty when the stance would not trigger.</summary>
        public string StanceHint = "";
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
        NotWaiting,
        WrongCard,
        WrongZone,
    }

    /// <summary>
    /// Walks a script in order. Holds no rules: it only knows which event is next, whether
    /// that event waits for a drag, and whether a released card matches what the script wrote.
    /// </summary>
    public sealed class DepictionRunner
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
        public bool WaitingForDrag => !Finished && Next.WaitsForDrag;

        /// <summary>Takes the next automatic event. Throws when the script is waiting for a drag.</summary>
        public DepictionEvent AdvanceAuto()
        {
            if (Finished) throw new InvalidOperationException("The script has already finished.");
            if (WaitingForDrag) throw new InvalidOperationException("Event " + Next.Order + " waits for a drag.");
            return Take();
        }

        public PlayVerdict TryPlay(string cardId, DropZone zone, out DepictionEvent played)
        {
            played = null;
            if (!WaitingForDrag) return PlayVerdict.NotWaiting;
            DepictionEvent next = Next;
            if (next.CardId != cardId) return PlayVerdict.WrongCard;
            if (zone != RequiredZone(next.Aim)) return PlayVerdict.WrongZone;
            played = Take();
            return PlayVerdict.Accepted;
        }

        public static DropZone RequiredZone(CardAim aim)
        {
            return aim == CardAim.Single ? DropZone.Receiver : DropZone.AboveThrowLine;
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
