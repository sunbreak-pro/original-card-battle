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

    /// <summary>
    /// The icon on the omen badge (#349, battle-visual-v1 4.4). The script picks it; the View only
    /// draws it. Status is the star of a 技 that puts no status on the opponent (a buff on itself, a
    /// push or pull); Seal is the stamp of a 技 that does (状態付与, 決定 19, #455).
    /// </summary>
    public enum OmenIcon
    {
        None,
        Attack,
        Guard,
        Status,
        Move,
        Stance,
        Rest,
        Seal,
    }

    /// <summary>
    /// The icon of a status a 状態付与 omen puts on the player (battle-visual-v1 §4.4 / 付録 A, #455).
    /// None is a word with no icon in 付録 A (a boss's word, 鉤爪...): the badge writes its name in the
    /// boss colour's double frame instead.
    /// </summary>
    public enum StatusGlyph
    {
        None,
        Bleed,
        Fragile,
        Slow,
        Intimidate,
        Fatigue,
    }

    /// <summary>One status a 状態付与 omen puts on: its name as printed and its icon.</summary>
    public sealed class OmenStatus
    {
        public string Label = "";
        public StatusGlyph Glyph = StatusGlyph.None;
    }

    /// <summary>battle-visual-v1 §4.4 当たり外れの印: none, lands (phosphor, solid), or misses (steel, dashed, struck).</summary>
    public enum OmenHit
    {
        None,
        Lands,
        Misses,
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
        /// The range side the card may be played from, or null when either side works. The fixed
        /// script prints it and never looks at it; a core battle refuses by reach instead
        /// (<see cref="PlayVerdict.OutOfRange"/>).
        /// </summary>
        public RangeSide? RequiredRange;
        /// <summary>The glyph of <see cref="RequiredRange"/> as printed ("近"). Empty when the card has none.</summary>
        public string RequiredRangeGlyph = "";
        /// <summary>
        /// The line the hover panel beside the card prints when the card aims at the opponent and no
        /// standing enemy is inside its reach right now ("相手との間合いが 1〜2 のとき使用可能", #261).
        /// Empty when the card reaches someone, aims at nobody, or is only short of stamina.
        /// </summary>
        public string ReachHint = "";

        // ---- battle-visual-v1 §6 / §7 / §9 (#242). The script writes every line; the View prints. ----

        /// <summary>
        /// §6.1 値: the value columns, at most two (power, Guard, a move, a draw...). Statuses are never
        /// a column; they lead <see cref="TextLines"/>. A stance card has none.
        /// </summary>
        public List<CardValue> Values = new List<CardValue>();
        /// <summary>
        /// §6.1 説明: the rows under the values, at most three. A card that puts a status writes it on
        /// the first row ("敵に 鈍足 2・疲労 2"). A stance card writes one lasting effect per row and
        /// sets <see cref="Lasting"/>, so the View puts ∞ at the head of each row.
        /// </summary>
        public List<string> TextLines = new List<string>();
        /// <summary>§6.1 スタンスの札: the rows of <see cref="TextLines"/> are lasting effects (∞ at their head).</summary>
        public bool Lasting;
        /// <summary>§6.1 素直な札: no trait, every value +2. The trait box is dashed and has no lamp.</summary>
        public bool Plain;
        /// <summary>
        /// §6.1 特性の箱: the two rows printed in the box. One trait: the condition, then "→ effect".
        /// 背水の陣's two traits: one trait per row ("間合い2以上→威力+5"). A plain card: 素直な札 /
        /// 特性なし（+2 込み）. Empty for a card with no trait box at all.
        /// </summary>
        public string TraitTop = "";
        public string TraitBottom = "";
        /// <summary>§6.1 コスト円: the printed cost before a discount; Cost below it means the circle is filled and "▼n" shows.</summary>
        public int PrintedCost;
        /// <summary>
        /// §6.3: the lamp for each enemy slot (the index in the core's enemy list), so the View can
        /// re-light the lamp for the enemy a held card frames without judging the condition itself.
        /// Empty when the card aims at nobody: <see cref="TraitLit"/> is its lamp everywhere.
        /// </summary>
        public List<bool> LitOn = new List<bool>();
        /// <summary>
        /// battle-visual-v1 §4.5 HP の減る分 (#507): for each enemy slot (the index in the core's enemy
        /// list), the HP this card would take off that enemy if it were played on it now — every blow
        /// past its Guard, plus the wall damage of a push it cannot take (decision A, 2026-10-17), never
        /// more than the HP it has. The View draws it as the wick stripe over that enemy's HP bar and
        /// works nothing out. 0 for a slot the card cannot reach or that has fallen. Empty when the
        /// card aims at nobody, or the source writes none.
        /// </summary>
        public List<int> HpLossOn = new List<int>();
        /// <summary>§7.2: the one line above a hovered card, with the values of now ("間合い 2 以上なら、この札の威力 13 → 18").</summary>
        public string HoverLine = "";
        /// <summary>
        /// battle-visual-v1 §4.2 届くマスの光 (#242): the floor cells (1-based, as <see cref="FloorFrame"/>
        /// counts them) this card reaches from where the player stands now, lit while it is hovered or
        /// held. Empty for a card that aims at nobody, or when the source draws no floor.
        /// </summary>
        public List<int> ReachCells = new List<int>();
        /// <summary>§7.4: the right-click detail. Null when the source writes none (the v4.2 script).</summary>
        public CardDetail Detail;
        /// <summary>
        /// battle-visual-v1 §4.9 / §5.3 構えの予告 (#455): the hint by the end-turn plate while this card
        /// is held — <see cref="DepictionFrame.StanceHint"/> as it would read once the card's cost is
        /// paid ("+3"), or "" when the stamina left would fall under 構え's threshold. Null: the source
        /// writes none, and the hint stays as the frame has it.
        /// </summary>
        public string StanceHintAfterPay;
        /// <summary>
        /// battle-visual-v1 §7.1 / §6.1 (#455): the glossary words printed in the trait box, where they
        /// stand in their row, and what they mean. The View draws a dotted underline under each and
        /// shows the meaning on hover. Empty when the box holds no term, or the source writes none.
        /// </summary>
        public List<TermMark> Terms = new List<TermMark>();

        /// <summary>
        /// §6.3: the lamp while the card frames enemy <paramref name="enemy"/>. A card with no lamp per
        /// enemy (one aimed at nobody, or a source that writes none) keeps <see cref="TraitLit"/>.
        /// </summary>
        public bool LitFor(int enemy)
        {
            return enemy >= 0 && enemy < LitOn.Count ? LitOn[enemy] : TraitLit;
        }

        /// <summary>§4.5 (#507): the wick stripe on enemy <paramref name="enemy"/>'s HP bar while this card is held. 0 when the source writes none.</summary>
        public int HpLossFor(int enemy)
        {
            return enemy >= 0 && enemy < HpLossOn.Count ? HpLossOn[enemy] : 0;
        }

        /// <summary>§6.1: the cost circle is filled and marked "▼n" when the cost is below the printed one.</summary>
        public int CostDrop => PrintedCost > Cost ? PrintedCost - Cost : 0;
    }

    /// <summary>The kind of one value column on a card face (battle-visual-v1 §6.1 / §8): its icon.</summary>
    public enum CardValueKind
    {
        Power,
        Guard,
        Heal,
        /// <summary>詰める: the one playing moves toward the opponent (→).</summary>
        Close,
        /// <summary>離れる: the one playing moves away (←).</summary>
        Back,
        /// <summary>押す: the opponent is moved away (→).</summary>
        Push,
        /// <summary>引く: the opponent is moved closer (←).</summary>
        Pull,
        Draw,
    }

    /// <summary>One value column: the icon's kind, the 36 px number and the 16 px word under it ("威力").</summary>
    public sealed class CardValue
    {
        public CardValueKind Kind;
        public string Number = "";
        public string Word = "";
    }

    /// <summary>A word with a dotted underline and what it means (battle-visual-v1 §7.1 / §7.5).</summary>
    public sealed class TermDefinition
    {
        public string Word = "";
        public string Meaning = "";
    }

    /// <summary>The row of the trait box a term is printed in (battle-visual-v1 §6.1 特性の箱).</summary>
    public enum TermRow
    {
        TraitTop,
        TraitBottom,
    }

    /// <summary>
    /// One underlined term on a card face (battle-visual-v1 §7.1, #455): the characters
    /// [<see cref="Start"/>, <see cref="Start"/> + <see cref="Length"/>) of its row as printed, the
    /// glossary word they stand for (§7.5) and its meaning.
    /// </summary>
    public sealed class TermMark
    {
        public TermRow Row;
        public int Start;
        public int Length;
        /// <summary>The glossary's word ("連動 X"), the heading the hover shows.</summary>
        public string Word = "";
        public string Meaning = "";
    }

    /// <summary>The right-click detail of a card (battle-visual-v1 §7.4): name, kind line, full text, terms.</summary>
    public sealed class CardDetail
    {
        public string Name = "";
        /// <summary>"攻撃 ・ 敵 1 体 ・ 間合い 0〜1", or "防御 ・ 自分".</summary>
        public string KindLine = "";
        /// <summary>The full text, one sentence after another.</summary>
        public string Body = "";
        public List<TermDefinition> Terms = new List<TermDefinition>();
    }

    /// <summary>
    /// Which family a status chip belongs to (battle-visual-v1 §7.3). The View draws the corner from it:
    /// a square chip for the words put on the opponent, a round one for the words put on oneself, ∞ for
    /// a lasting stance, the boss colour's double frame for a boss's word.
    /// </summary>
    public enum ChipKind
    {
        OnFoe,
        OnSelf,
        Lasting,
        Boss,
    }

    public sealed class StatusChip
    {
        public string Label;
        /// <summary>The stacks; for a <see cref="ChipKind.Lasting"/> chip the times the same stance is held (1 prints no "×1").</summary>
        public int Stacks;
        public ChipKind Kind = ChipKind.OnFoe;
        /// <summary>§7.3 説明パネルの見出し: "鈍足 2", "岩の構え ×2".</summary>
        public string Title = "";
        /// <summary>§7.3: the family on the panel's right, "相手に付ける系" / "自分に付ける系" / "永続" / "ボス専用".</summary>
        public string Group = "";
        /// <summary>§7.3 効き方, written from the status table's values.</summary>
        public string Effect = "";
        /// <summary>§7.3 減り方.</summary>
        public string Decay = "";
    }

    public sealed class UnitFrame
    {
        public int Hp;
        public int HpMax;
        public int Guard;
        public bool ShowStamina;
        public int Stamina;
        public int StaminaMax;
        /// <summary>
        /// battle-visual-v1 §4.7 次のターンに回復する分 (#455): the stamina that comes back at the next turn
        /// start if the turn ended now (the core's estimate, already capped at the maximum). The stamina
        /// tag draws that many dashed dots after the ones held. 0 draws none.
        /// </summary>
        public int NextRecovery;
        /// <summary>
        /// battle-visual-v1 §4.5 構えの盾 (#455), an enemy's only: whether 構え is expected to give it
        /// Guard at the end of its phase, read off the stamina it holds now against the rule's
        /// threshold (残り 3 以上). A solid shield when set, a dashed one when not. It is an estimate:
        /// 構え is judged after the enemy acts.
        /// </summary>
        public bool ReserveLikely;
        /// <summary>battle-visual-v1 §4.5 (#455): an elite's number panel has a phosphor frame.</summary>
        public bool Elite;
        /// <summary>Only bosses, elites and the player hold a range side (battle_core_v4 §17.3).</summary>
        public bool HasRange;
        public RangeSide Range;
        /// <summary>The one glyph on the tag at the unit's feet ("近" / "遠"). Required when <see cref="HasRange"/>.</summary>
        public string RangeGlyph = "";
        public List<StatusChip> Statuses = new List<StatusChip>();
        /// <summary>
        /// Whose art the figure wears (#288): the enemy's id, as the art folder is named
        /// (<see cref="CharacterArt"/>). Empty, or an id with no art, keeps the placeholder silhouette.
        /// </summary>
        public string ArtId = "";
        /// <summary>The unit has fallen and its figure is gone (battle_ui_ux_v2 §2.2 の 6). Set from the Defeat beat on.</summary>
        public bool Down;
        /// <summary>
        /// battle-visual-v1 §4.6 / battle_core_v4 §4 v4.4 (#455): the stances the unit holds, in the
        /// order its list shows them (<see cref="StanceList.Rows"/>: the newest last, a repeat counted
        /// on one row). Drawn at the unit's edge when <see cref="DepictionFrame.StanceListsShown"/>.
        /// </summary>
        public List<StanceRow> Stances = new List<StanceRow>();
    }

    /// <summary>One stance on a side's list (#455): its name and how many times it is held (1 prints no "×1").</summary>
    public sealed class StanceRow
    {
        public string Name = "";
        public int Count = 1;
    }

    public sealed class OmenFrame
    {
        public bool Visible;
        /// <summary>The icon of the omen's kind (#349). None draws no icon.</summary>
        public OmenIcon Icon = OmenIcon.None;
        /// <summary>The kind as a word: "攻撃", "防御", "移動", "構え", "技", "休み".</summary>
        public string KindLabel = "";
        /// <summary>
        /// The reach the omen aims at ("1〜2"), kept until the floor shows the aimed cells (#242).
        /// The fixed script (TurnSliceScript), written for v4.2, still writes the side glyph. Empty when none.
        /// </summary>
        public string SideGlyph = "";
        /// <summary>The number beside the icon: attack (the core preview) and guard only (#349). Empty for any other kind.</summary>
        public string ValueText = "";
        /// <summary>
        /// battle-visual-v1 §4.4 当たり外れの印 (#242): whether the omen reaches the player where they
        /// stand now. The core's OmenPreview.Lands when the bridge holds one for this omen that is not
        /// a rest; otherwise the aimed cells against the player's near edge. None for an omen that aims
        /// at nobody, and for the v4.2 sources, which keep <see cref="SideGlyph"/> in its place.
        /// </summary>
        public OmenHit Hit = OmenHit.None;

        /// <summary>
        /// battle-visual-v1 §4.4 状態付与 (#455): for a <see cref="OmenIcon.Seal"/> omen, the statuses it
        /// puts on the player, at most two, drawn at 28 px where the number would stand. Empty for any
        /// other omen, and below 開示度 2 (<see cref="OmenDisclosure"/>).
        /// </summary>
        public List<OmenStatus> Statuses = new List<OmenStatus>();

        /// <summary>
        /// battle-visual-v1 §4.4 / battle_ui_ux_v2 §7 (#455): how far the journal has read this enemy,
        /// 0〜2. The writer has already blanked what the level does not show (<see cref="OmenDisclosure"/>),
        /// so the View draws the frame as it is. Every source writes <see cref="OmenDisclosure.Full"/>
        /// until the journal hands a level to the battle.
        /// </summary>
        public int Disclosure = OmenDisclosure.Full;

        /// <summary>
        /// #50, §17.6 F11: the 予定 of an elite or a boss — the second action it plans, shown beside the
        /// omen. It is not a commitment (the tree is read again after the first action). It stays up
        /// after the first omen is spent, until the second action acts. Independent of
        /// <see cref="Visible"/>.
        /// </summary>
        public bool PlanVisible;
        /// <summary>The icon of the plan's kind (#349), as <see cref="Icon"/> picks it.</summary>
        public OmenIcon PlanIcon = OmenIcon.None;
        /// <summary>The plan's kind: "攻撃", "防御", "休み"...</summary>
        public string PlanKindLabel = "";
        /// <summary>
        /// The plan's number, attack and guard only (#349): the face alone, no trait, stance or wall,
        /// since the plan is not a commitment. Empty for any other kind.
        /// </summary>
        public string PlanValueText = "";
        /// <summary>§4.4: the plan's statuses as <see cref="Statuses"/> writes them, drawn at 20 px.</summary>
        public List<OmenStatus> PlanStatuses = new List<OmenStatus>();
        /// <summary>
        /// The cells the plan aims at, as <see cref="SideGlyph"/> writes them. Empty when none. The plan
        /// badge does not draw it (battle-visual-v1 4.4 gives the plan no reach); it stays in the
        /// script for the floor cells of #242.
        /// </summary>
        public string PlanSideGlyph = "";
        /// <summary>True on the frames after a 予定変更, so the badge can mark the plan as changed.</summary>
        public bool PlanChanged;

        /// <summary>The tag on the plan badge: 予定, or 予定変更 once the plan was replaced (#50).</summary>
        public string PlanTag => PlanChanged ? DepictionText.PlanChangedTag : DepictionText.PlanTag;
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
        /// <summary>
        /// battle-visual-v1 §4.7 (#242): the stamina 構え needs left at the turn end; the stamina tag
        /// puts its divider after that many dots. 0 draws no divider.
        /// </summary>
        public int StanceDivider;
        /// <summary>battle-visual-v1 §4.2 (#242): the floor. <see cref="FloorFrame.Cells"/> 0 draws none (the v4.2 sources).</summary>
        public FloorFrame Floor = new FloorFrame();
        /// <summary>battle-visual-v1 §4.7 (#455): the draw, discard and exile piles. <see cref="PileFrame.Shown"/> false draws none.</summary>
        public PileFrame Piles = new PileFrame();
        /// <summary>
        /// battle-visual-v1 §4.6 (#455): the source keeps the stance lists (<see cref="UnitFrame.Stances"/>),
        /// so each side's edge shows its list, or the dashed empty frame. False draws neither (the v4.2
        /// sources and the filming script).
        /// </summary>
        public bool StanceListsShown;
        /// <summary>battle-visual-v1 §4.9 (#455): the attributes played this turn. <see cref="PlayedFrame.Shown"/> false draws no frames.</summary>
        public PlayedFrame Played = new PlayedFrame();
        /// <summary>
        /// battle-visual-v1 §5.5 (#455): the enemy's turn — from the player's turn end until the next
        /// turn start. The screen sinks below y 790 and the end-turn plate dims while it holds. The
        /// v4.2 sources leave it false.
        /// </summary>
        public bool EnemyTurn;
        /// <summary>
        /// battle_ui_ux_v2 §2.2 / battle-visual-v1 §4.2 (#589): every enemy of the battle, in the core's
        /// order (the first acts first). The first is <see cref="Enemy"/>, <see cref="Omen"/> and the
        /// floor's enemy cells and aimed cells, the same objects, so a View that reads only those still
        /// shows it. Empty from a source that keeps one enemy (the filming script): the single-enemy
        /// fields stand alone.
        /// </summary>
        public List<EnemyFrame> Enemies = new List<EnemyFrame>();
    }

    /// <summary>
    /// One enemy's share of the screen when the battle has several (#589): its number panel and stance
    /// list (<see cref="Unit"/>), its omen badge, where it stands, the cells its shown omen aims at, and
    /// the stripes that tell its cells from the others'.
    /// </summary>
    public sealed class EnemyFrame
    {
        public UnitFrame Unit = new UnitFrame();
        public OmenFrame Omen = new OmenFrame();
        /// <summary>Its lowest cell and how many cells it takes (2 for a large elite), as the floor draws them.</summary>
        public int Cell;
        public int Size = 1;
        /// <summary>The cells its shown omen reaches, as <see cref="FloorFrame.AimCells"/> holds them for the first enemy.</summary>
        public List<int> AimCells = new List<int>();
        /// <summary>Of <see cref="AimCells"/>, the ones that meet the boundary its action reads (<see cref="FloorFrame.DenseCells"/>).</summary>
        public List<int> DenseCells = new List<int>();
        /// <summary>The cells it would stand on after the move its shown omen makes (<see cref="FloorFrame.MoveCells"/>).</summary>
        public List<int> MoveCells = new List<int>();
        /// <summary>
        /// battle_ui_ux_v2 §1.4 / battle-visual-v1 §4.2: the direction of its red stripes on the floor —
        /// 0 is 45° (the first enemy), 1 is −45° (the second), 2 is upright (the third). The View's
        /// stripe art takes the same numbers.
        /// </summary>
        public int StripeDirection;
        /// <summary>
        /// battle-visual-v1 §4.4 / §4.5 / §4.6: the swatch of <see cref="StripeDirection"/> beside its omen,
        /// on its HP row and on its stance frame. Only a battle of two or more enemies draws it; one enemy
        /// needs nothing to tell its cells apart.
        /// </summary>
        public bool Swatch;

        /// <summary>battle_ui_ux_v2 §1.4: the stripe direction of the enemy at <paramref name="index"/> (0, 1, 2; a fourth and on repeat upright).</summary>
        public static int DirectionOf(int index) => index <= 0 ? 0 : index >= 2 ? 2 : 1;
    }

    /// <summary>
    /// The three piles at the bottom left (battle-visual-v1 §4.7, #455): how many cards each holds.
    /// The View stacks that many sheets' worth of thickness (PileLayout) and writes no number
    /// (battle_ui_ux_v2 §1.1 B9: 束は厚みだけ).
    /// </summary>
    public sealed class PileFrame
    {
        /// <summary>Whether the source keeps piles at all. The fixed filming script does not.</summary>
        public bool Shown;
        public int Draw;
        public int Discard;
        public int Exile;

        /// <summary>
        /// battle_ui_ux_v2 §1.1 C4 / §3.13 (#455): what each pile holds, for the list a click on the pile
        /// opens. One row per kind of card, never in draw order (the order of the draw pile stays
        /// hidden). Empty when the source keeps no lists.
        /// </summary>
        public List<PileCard> DrawCards = new List<PileCard>();
        public List<PileCard> DiscardCards = new List<PileCard>();
        public List<PileCard> ExileCards = new List<PileCard>();

        /// <summary>The rows of <paramref name="pile"/>'s list.</summary>
        public List<PileCard> CardsOf(PileKind pile)
        {
            switch (pile)
            {
                case PileKind.Draw: return DrawCards;
                case PileKind.Discard: return DiscardCards;
                case PileKind.Exile: return ExileCards;
                default: throw new ArgumentOutOfRangeException(nameof(pile), pile, null);
            }
        }
    }

    /// <summary>
    /// One row of a pile's list (battle_ui_ux_v2 §1.1 C4 同種の残り「突き 2 / 3」): a kind of card, how
    /// many of it the pile holds, and how many the battle's deck holds in all.
    /// </summary>
    public sealed class PileCard
    {
        public string Name = "";
        public int InPile;
        public int InDeck;

        /// <summary>"突き 2 / 3".</summary>
        public string RowText => Name + " " + InPile + " / " + InDeck;
    }

    /// <summary>
    /// The attributes played this turn (battle-visual-v1 §4.9 / battle_ui_ux_v2 §1.1 B6, #455): how many
    /// cards of each, and which attribute the last one counted as. The core folds a card into one
    /// attribute (battle_core_v4 §2.1); a card of movement only counts as none.
    /// </summary>
    public sealed class PlayedFrame
    {
        /// <summary>Whether the source keeps this tally. The v4.2 sources do not, and draw no frames.</summary>
        public bool Shown;
        public int Attack;
        public int Guard;
        public int Skill;
        public int Stance;
        /// <summary>B6 直前のカードに下線: the attribute of the card played last this turn, or null.</summary>
        public CardKind? Last;
        /// <summary>
        /// battle_ui_ux_v2 §1.1 B6 / §3.6 (f) (#455): the power a 追撃 adds to the player's next attack face
        /// this turn, as the core holds it (CombatantState.FollowUp). The row shows "+5" at its left end
        /// while it is above 0; the core clears it when the next attack face spends it or the turn ends.
        /// </summary>
        public int FollowUp;

        public int CountOf(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Attack: return Attack;
                case CardKind.Guard: return Guard;
                case CardKind.Skill: return Skill;
                case CardKind.Stance: return Stance;
                default: return 0;
            }
        }

        /// <summary>Counts one more card of <paramref name="kind"/> and makes it the last one.</summary>
        public void Add(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Attack: Attack++; break;
                case CardKind.Guard: Guard++; break;
                case CardKind.Skill: Skill++; break;
                case CardKind.Stance: Stance++; break;
                default: return;
            }
            Last = kind;
        }
    }

    /// <summary>
    /// The line of floor cells under the figures (battle-visual-v1 §4.2, #242). Cells count from 1 at
    /// the player's end, as BattleCore does. Every cell here is the core's; the View only draws them.
    /// </summary>
    public sealed class FloorFrame
    {
        /// <summary>How many cells the line has (5〜8). 0: the source draws no floor.</summary>
        public int Cells;
        /// <summary>The player's lowest cell and how many cells it takes.</summary>
        public int PlayerCell;
        public int PlayerSize = 1;
        /// <summary>The enemy's lowest cell and how many cells it takes (2 for a large elite).</summary>
        public int EnemyCell;
        public int EnemySize = 1;
        /// <summary>N: the empty cells between the player and the enemy. The one number on the floor.</summary>
        public int Gap;
        /// <summary>The cells the shown omen reaches (赤の斜線). Empty when it aims at nobody or is hidden.</summary>
        public List<int> AimCells = new List<int>();
        /// <summary>
        /// battle-visual-v1 §4.2 境目を満たすマス (#455): of <see cref="AimCells"/>, the ones whose gap meets
        /// the boundary the omen's action reads (間合い n 以上 / n 以下). Their stripes are drawn 6 px
        /// apart instead of 11. Empty when the action reads no boundary or the omen is hidden.
        /// </summary>
        public List<int> DenseCells = new List<int>();
        /// <summary>
        /// battle_ui_ux_v2 §5.8 / battle-visual-v1 §4.2 敵が動く予兆の移る先 (#455): the cells the enemy
        /// would stand on after the move its shown omen makes (菫の破線の枠, no stripes). Empty when the
        /// omen does not move it, or is hidden.
        /// </summary>
        public List<int> MoveCells = new List<int>();
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

    /// <summary>
    /// §5.3: the blow's shape and the enemy's move from the technique's name and faces. The words and
    /// their priority (突 → 打 → 払 → 斬) are the grammar's (<see cref="EffectGrammar.BlowOf"/>).
    /// </summary>
    public static class StrikeSystems
    {
        public static StrikeSystem Of(string name, bool attacks, bool guards, bool moves, bool pushes)
        {
            // A push or pull is what the action is for, even when its name holds 突 or 払, so it plays as 打.
            if (attacks) return EffectGrammar.ToStrike(EffectGrammar.BlowOf(name, pushes));
            if (moves) return StrikeSystem.Step;
            return guards ? StrikeSystem.Shield : StrikeSystem.Step;
        }
    }

    public enum DepictionEventKind
    {
        TurnStart,
        PlayCard,
        TurnEnd,
        EnemyAction,
        NextOmen,
        /// <summary>A unit falls (#288): the beat of its own that plays the 800 ms fall, after the blow's event.</summary>
        Defeat,
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
        /// <summary>
        /// The target falls (#288, battle_ui_ux_v2 §2.2 の 6): it tilts, sinks, darkens and is gone, and its
        /// cell is empty. The frame after it carries <see cref="UnitFrame.Down"/>.
        /// </summary>
        Defeat,
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
        /// <summary>Strength of the effect, 1..4, decided by whoever writes the script (for now always <see cref="EffectStrength.Uniform"/>, §5.3 / #30).</summary>
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
        /// <summary>
        /// DrawHand only (battle_ui_ux_v2 §3.14 山札切れ, #455): the draw pile ran out while this cue's
        /// cards were drawn and the core made it again from the discard pile (DeckReshuffled) after this
        /// many of them. <see cref="Unchanged"/> when the pile was not made again.
        /// </summary>
        public int RebuildAfter = Unchanged;
        /// <summary>Slash and EnemyWindup: the shape of the blow and of the enemy's move (§5.3).</summary>
        public StrikeSystem System = StrikeSystem.Slash;
        /// <summary>
        /// §5.3 (#57): the system this beat draws, written by the script from the card or action that
        /// plays it: the blow's on a Slash, 盾 on a guard gained, 型 on a stance set, 気 on a status put
        /// on, 息 on a heal, a draw or a stamina point. Null: the beat draws no shape of the grammar
        /// (a turn start's tick, the reserve's Guard). The View draws <see cref="EffectGrammar.LookId"/>.
        /// </summary>
        public FxSystem? Fx;
        /// <summary>
        /// Slash only (§5.9 順 1, J2): a trait of the card or action fired, so the View layers a short
        /// cue on the blow. It never changes <see cref="Intensity"/>.
        /// </summary>
        public bool TraitAccent;
        /// <summary>RangeSwitch only: the other side moved the player (a push or a pull), not the player itself.</summary>
        public bool Pushed;
        /// <summary>
        /// RangeSwitch only (battle-visual-v1 §4.2, #242): the lowest cell of the player and of the enemy
        /// after the move, and N after it, for a source that draws the floor. 0 when it draws none.
        /// </summary>
        public int PlayerCellAfter;
        public int EnemyCellAfter;
        public int GapAfter;
        /// <summary>
        /// RangeSwitch on the floor only (battle_ui_ux_v2 §5.7 順 1, #586): how many cells the side that
        /// moved went, so the walk takes 160 ms a cell. 0 when the source draws no floor.
        /// </summary>
        public int CellsMoved;
        /// <summary>
        /// #589: which enemy (its index in <see cref="DepictionFrame.Enemies"/>) is on the enemy end of the
        /// beat — the enemy that acts, or the one a card struck. 0 in a one-enemy battle and for a beat
        /// about the player alone.
        /// </summary>
        public int Unit;
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
        /// <summary>
        /// PlayCard only (battle-visual-v1 §4.7 / battle_ui_ux_v2 §5.11 順 4, #455): the pile the played
        /// card goes to, as the core put it — the exile pile for a stance card (CardExiled), the discard
        /// pile for any other. Its ghost flies there.
        /// </summary>
        public PileKind PlayedTo = PileKind.Discard;
        /// <summary>
        /// PlayCard and the enemy's action (#57): the systems of the card or action, in the order its
        /// faces resolve (§5.4). Empty for any other event, and for a movement-only card.
        /// </summary>
        public List<FxSystem> Systems = new List<FxSystem>();
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
        /// A played battle never answers this — there the hand is free (issue #36).
        /// </summary>
        OffScript,
        /// <summary>
        /// The player is bound (呪縛, enemy_roster_v4 §4.1) and the card is one of movement only (#335).
        /// Only the core-driven source answers this; the View treats it as any other refusal.
        /// </summary>
        Bound,
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

        /// <summary>The tag on the plan badge (battle-visual-v1 4.4) and its form after a plan change (#50).</summary>
        public const string PlanTag = "予定";
        public const string PlanChangedTag = "予定変更";

        public static string OutcomeText(BattleOutcome outcome)
        {
            return outcome == BattleOutcome.Won ? Won : outcome == BattleOutcome.Lost ? Lost : "";
        }

        public static string ZoneName(CardAim aim)
        {
            return aim == CardAim.Single ? "敵の受け皿の上" : "投げ上げ線より上";
        }

        /// <summary>Why a card released in the wrong zone went back to the hand. Every source says it the same way.</summary>
        public static string WrongZoneText(CardFace face)
        {
            return "「" + face.Name + "」は" + ZoneName(face.Aim) + "で離すと出せます";
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
    /// for filming (<see cref="DepictionRunner"/>) and the fight the player actually plays, which the
    /// bridge runs on the BattleCore rules (CoreBattleSource, #571). The View holds one of them and
    /// never asks which it is.
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
    /// released card is the one the script wrote. A player who wants a free hand fights through the
    /// bridge's CoreBattleSource instead.
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
                return DepictionText.WrongZoneText(face);
            }
            return "";
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
