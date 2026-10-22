// Port of unity-project-kit/Assets/View/Depiction/Script/DepictionTypes.cs
// Display data for the v4.2 battle depiction. Pure C#: no UnityEngine, no BattleCore.
// The screen never computes a rule — every number, label and "did the condition hold"
// comes from these types. Today a hand-written script fills them (TurnSliceScript);
// later the v4.2 core's output is mapped into the same types.
//
// Port notes:
// - The C# classes here are mutable classes (not records). Each JS class takes no positional
//   arguments; `new X()` gives the C# field initializers (enum fields default to their first member,
//   as C#'s default(enum) is 0; an uninitialized string is null). An optional object of initial
//   members may be passed (`new Cue({ Kind: ..., Amount: 3 })`), which is C#'s object initializer.
// - Computed C# properties (CostDrop, PlanTag, RowText, WaitsForDrag, the runner's state) are getters.
// - `out` parameters follow the port's convention (see CoreBattleSource): TryPlay returns
//   `{ ok: verdict, value: played, verdict, played }`.
// - The modules imported below are read inside functions / constructors only (import cycles with
//   EffectGrammar, OmenDisclosure and PileLayout are fine).
import { ArgumentNullException, ArgumentOutOfRangeException, InvalidOperationException, defineEnum } from '../../shared/cs.js';
import { EffectGrammar } from './EffectGrammar.js';
import { OmenDisclosure } from './OmenDisclosure.js';
import { PileKind } from './PileLayout.js';

/** C# string concatenation writes null as "". */
function str(s) {
  return s == null ? '' : s;
}

/** new ArgumentNullException(nameof(x)) with .NET's message. */
function argNull(name) {
  const e = new ArgumentNullException(`Value cannot be null. (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

/** new ArgumentOutOfRangeException(paramName, actualValue, null) with .NET's message. */
function argRangeValue(name, value) {
  const e = new ArgumentOutOfRangeException(
    `Specified argument was out of the range of valid values. (Parameter '${name}')\nActual value was ${value}.`,
  );
  e.ParamName = name;
  e.ActualValue = value;
  return e;
}

/** Applies a C# object initializer's members, if any. */
function init(target, members) {
  if (members != null) Object.assign(target, members);
  return target;
}

export const RangeSide = defineEnum(['Near', 'Far']);

export const UnitSide = defineEnum(['Player', 'Enemy']);

/** Attribute of a card face; the View borrows the role colours (battle_ui_ux_v2 §11.4). */
export const CardKind = defineEnum(['Attack', 'Guard', 'Stance', 'Skill', 'Move']);

/**
 * The icon on the omen badge (#349, battle-visual-v1 4.4). The script picks it; the View only
 * draws it. Status is the star of a 技 that puts no status on the opponent (a buff on itself, a
 * push or pull); Seal is the stamp of a 技 that does (状態付与, 決定 19, #455).
 */
export const OmenIcon = defineEnum(['None', 'Attack', 'Guard', 'Status', 'Move', 'Stance', 'Rest', 'Seal']);

/**
 * The icon of a status a 状態付与 omen puts on the player (battle-visual-v1 §4.4 / 付録 A, #455).
 * None is a word with no icon in 付録 A (a boss's word, 鉤爪...): the badge writes its name in the
 * boss colour's double frame instead.
 */
export const StatusGlyph = defineEnum(['None', 'Bleed', 'Fragile', 'Slow', 'Intimidate', 'Fatigue']);

/** One status a 状態付与 omen puts on: its name as printed and its icon. */
export class OmenStatus {
  constructor(members) {
    this.Label = '';
    this.Glyph = StatusGlyph.None;
    init(this, members);
  }
}

/** battle-visual-v1 §4.4 当たり外れの印: none, lands (phosphor, solid), or misses (steel, dashed, struck). */
export const OmenHit = defineEnum(['None', 'Lands', 'Misses']);

/** How the card is released: on the target's receiver, or above the throw line. */
export const CardAim = defineEnum(['Single', 'Self']);

export const DropZone = defineEnum(['None', 'Receiver', 'AboveThrowLine']);

export class CardFace {
  constructor(members) {
    this.Id = null;
    this.Name = null;
    this.Cost = 0;
    this.Kind = CardKind.Attack;
    this.Aim = CardAim.Single;
    /**
     * Throw-line cards only: whose figures the effect lands on, so the View can mark them
     * while the card is held. Required when Aim is Self; a receiver card already shows its
     * target with the dish. (UnitSide?)
     */
    this.Affects = null;
    /** Kind and target as printed under the name ("攻撃・敵単体", "防御・自分"). */
    this.TypeLabel = '';
    /**
     * What the card does, as printed on its face: up to three sentences of at most nine full-width
     * characters each. The card prints one sentence per line.
     */
    this.Description = '';
    /** The face value as printed ("6", "13", "遠 +4"). Empty when the card has none. */
    this.ValueText = '';
    /** Trait line as printed ("初手 +3"). Empty when the card has no trait. */
    this.TraitText = '';
    /** True when the trait's condition holds right now (lamp lit). */
    this.TraitLit = false;
    /**
     * The range side the card may be played from, or null when either side works. The fixed
     * script prints it and never looks at it; a core battle refuses by reach instead
     * (PlayVerdict.OutOfRange). (RangeSide?)
     */
    this.RequiredRange = null;
    /** The glyph of RequiredRange as printed ("近"). Empty when the card has none. */
    this.RequiredRangeGlyph = '';
    /**
     * The line the hover panel beside the card prints when the card aims at the opponent and no
     * standing enemy is inside its reach right now ("相手との間合いが 1〜2 のとき使用可能", #261).
     * Empty when the card reaches someone, aims at nobody, or is only short of stamina.
     */
    this.ReachHint = '';

    // ---- battle-visual-v1 §6 / §7 / §9 (#242). The script writes every line; the View prints. ----

    /**
     * §6.1 値: the value columns, at most two (power, Guard, a move, a draw...). Statuses are never
     * a column; they lead TextLines. A stance card has none. (List<CardValue>)
     */
    this.Values = [];
    /**
     * §6.1 説明: the rows under the values, at most three. A card that puts a status writes it on
     * the first row ("敵に 鈍足 2・疲労 2"). A stance card writes one lasting effect per row and
     * sets Lasting, so the View puts ∞ at the head of each row. (List<string>)
     */
    this.TextLines = [];
    /** §6.1 スタンスの札: the rows of TextLines are lasting effects (∞ at their head). */
    this.Lasting = false;
    /** §6.1 素直な札: no trait, every value +2. The trait box is dashed and has no lamp. */
    this.Plain = false;
    /**
     * §6.1 特性の箱: the two rows printed in the box. One trait: the condition, then "→ effect".
     * 背水の陣's two traits: one trait per row ("間合い2以上→威力+5"). A plain card: 素直な札 /
     * 特性なし（+2 込み）. Empty for a card with no trait box at all.
     */
    this.TraitTop = '';
    this.TraitBottom = '';
    /** §6.1 コスト円: the printed cost before a discount; Cost below it means the circle is filled and "▼n" shows. */
    this.PrintedCost = 0;
    /**
     * §6.3: the lamp for each enemy slot (the index in the core's enemy list), so the View can
     * re-light the lamp for the enemy a held card frames without judging the condition itself.
     * Empty when the card aims at nobody: TraitLit is its lamp everywhere. (List<bool>)
     */
    this.LitOn = [];
    /**
     * battle-visual-v1 §4.5 HP の減る分 (#507): for each enemy slot (the index in the core's enemy
     * list), the HP this card would take off that enemy if it were played on it now — every blow
     * past its Guard, plus the wall damage of a push it cannot take (decision A, 2026-10-17), never
     * more than the HP it has. The View draws it as the wick stripe over that enemy's HP bar and
     * works nothing out. 0 for a slot the card cannot reach or that has fallen. Empty when the
     * card aims at nobody, or the source writes none. (List<int>)
     */
    this.HpLossOn = [];
    /** §7.2: the one line above a hovered card, with the values of now ("間合い 2 以上なら、この札の威力 13 → 18"). */
    this.HoverLine = '';
    /**
     * battle-visual-v1 §4.2 届くマスの光 (#242): the floor cells (1-based, as FloorFrame counts them)
     * this card reaches from where the player stands now, lit while it is hovered or held. Empty
     * for a card that aims at nobody, or when the source draws no floor. (List<int>)
     */
    this.ReachCells = [];
    /** §7.4: the right-click detail. Null when the source writes none (the v4.2 script). (CardDetail) */
    this.Detail = null;
    /**
     * battle-visual-v1 §4.9 / §5.3 構えの予告 (#455): the hint by the end-turn plate while this card
     * is held — DepictionFrame.StanceHint as it would read once the card's cost is paid ("+3"), or
     * "" when the stamina left would fall under 構え's threshold. Null: the source writes none, and
     * the hint stays as the frame has it.
     */
    this.StanceHintAfterPay = null;
    /**
     * battle-visual-v1 §7.1 / §6.1 (#455): the glossary words printed in the trait box, where they
     * stand in their row, and what they mean. The View draws a dotted underline under each and
     * shows the meaning on hover. Empty when the box holds no term, or the source writes none.
     * (List<TermMark>)
     */
    this.Terms = [];

    // ---- JS extension: several enemies (docs/port-notes/depiction-multi-enemy.md). The C# has none of these. ----

    /**
     * battle-visual-v1 §5.2 届く敵の枠 / 届かない敵の枠: for each enemy slot (the index in the core's
     * enemy list), whether this card can be aimed at that enemy now — it stands and its N is inside
     * the card's reach (the core's Reach.Contains on BattleState.GapTo, as TurnLoop's aim check reads
     * it). The View draws the amber corners on a true slot and the steel ones on a false one. Empty
     * for a card aimed at nobody, and for the sources that write none. (List<bool>)
     */
    this.ReachOn = [];
    /**
     * The enemy slot the source sends this card to when it is released on the receiver without the
     * screen naming one (CoreBattleSource.TargetFor): of the standing enemies it can be played on, the
     * one it costs least on (the nearest on a tie); else the same among the ones in its reach; else the
     * nearest standing one. Cost, CostDrop, StanceHintAfterPay and the preview on this face are read
     * against it, so at rest the face shows the lowest cost the card has on any enemy in reach (the
     * "any enemy" reading of its lamp, §6.3). −1 for a card that does not aim at one enemy.
     */
    this.TargetUnit = -1;
    /**
     * battle-visual-v1 §5.2 スタミナ「▸ n」/ §6.3 予測値もその敵で: for each enemy slot (the index in the
     * core's enemy list), the cost of this card played on that enemy now — the core's (TurnLoop.Preview's
     * Cost), which a trait read on the gap (石礫: 間合い 2 以上 → コスト −1) makes differ between enemies.
     * A fallen slot holds Cost. Empty for a card that does not aim at one enemy (its cost is Cost
     * whatever it frames), and for the sources that write none. CostFor(k) reads it. (List<int>)
     */
    this.CostOn = [];
    /**
     * §4.9 / §5.3 構えの予告 per enemy slot: StanceHintAfterPay as it reads once CostOn[k] is paid.
     * Same length as CostOn. StanceHintAfterPayFor(k) reads it. (List<string>)
     */
    this.StanceHintAfterPayOn = [];
    init(this, members);
  }

  /** JS extension: §5.2, whether the card can be aimed at enemy slot `enemy` now (ReachOn); false when the source writes none. */
  ReachFor(enemy) {
    return enemy >= 0 && enemy < this.ReachOn.length ? this.ReachOn[enemy] : false;
  }

  /** JS extension: §5.2 / §6.3, the cost while the card frames enemy slot `enemy` (CostOn); Cost when the source writes none for it. */
  CostFor(enemy) {
    return enemy >= 0 && enemy < this.CostOn.length ? this.CostOn[enemy] : this.Cost;
  }

  /** JS extension: §6.1 / §6.3, CostDrop while the card frames enemy slot `enemy`: "▼n" when CostFor(enemy) is below the printed cost. */
  CostDropFor(enemy) {
    const cost = this.CostFor(enemy);
    return this.PrintedCost > cost ? this.PrintedCost - cost : 0;
  }

  /** JS extension: §4.9 / §5.3, the 構えの予告 while the card frames enemy slot `enemy` (StanceHintAfterPayOn); StanceHintAfterPay when the source writes none for it. */
  StanceHintAfterPayFor(enemy) {
    return enemy >= 0 && enemy < this.StanceHintAfterPayOn.length ? this.StanceHintAfterPayOn[enemy] : this.StanceHintAfterPay;
  }

  /**
   * §6.3: the lamp while the card frames enemy `enemy`. A card with no lamp per enemy (one aimed
   * at nobody, or a source that writes none) keeps TraitLit.
   */
  LitFor(enemy) {
    return enemy >= 0 && enemy < this.LitOn.length ? this.LitOn[enemy] : this.TraitLit;
  }

  /** §4.5 (#507): the wick stripe on enemy `enemy`'s HP bar while this card is held. 0 when the source writes none. */
  HpLossFor(enemy) {
    return enemy >= 0 && enemy < this.HpLossOn.length ? this.HpLossOn[enemy] : 0;
  }

  /** §6.1: the cost circle is filled and marked "▼n" when the cost is below the printed one. */
  get CostDrop() {
    return this.PrintedCost > this.Cost ? this.PrintedCost - this.Cost : 0;
  }
}

/** The kind of one value column on a card face (battle-visual-v1 §6.1 / §8): its icon. */
export const CardValueKind = defineEnum([
  'Power',
  'Guard',
  'Heal',
  /** 詰める: the one playing moves toward the opponent (→). */
  'Close',
  /** 離れる: the one playing moves away (←). */
  'Back',
  /** 押す: the opponent is moved away (→). */
  'Push',
  /** 引く: the opponent is moved closer (←). */
  'Pull',
  'Draw',
]);

/** One value column: the icon's kind, the 36 px number and the 16 px word under it ("威力"). */
export class CardValue {
  constructor(members) {
    this.Kind = CardValueKind.Power;
    this.Number = '';
    this.Word = '';
    init(this, members);
  }
}

/** A word with a dotted underline and what it means (battle-visual-v1 §7.1 / §7.5). */
export class TermDefinition {
  constructor(members) {
    this.Word = '';
    this.Meaning = '';
    init(this, members);
  }
}

/** The row of the trait box a term is printed in (battle-visual-v1 §6.1 特性の箱). */
export const TermRow = defineEnum(['TraitTop', 'TraitBottom']);

/**
 * One underlined term on a card face (battle-visual-v1 §7.1, #455): the characters
 * [Start, Start + Length) of its row as printed, the glossary word they stand for (§7.5) and its
 * meaning.
 */
export class TermMark {
  constructor(members) {
    this.Row = TermRow.TraitTop;
    this.Start = 0;
    this.Length = 0;
    /** The glossary's word ("連動 X"), the heading the hover shows. */
    this.Word = '';
    this.Meaning = '';
    init(this, members);
  }
}

/** The right-click detail of a card (battle-visual-v1 §7.4): name, kind line, full text, terms. */
export class CardDetail {
  constructor(members) {
    this.Name = '';
    /** "攻撃 ・ 敵 1 体 ・ 間合い 0〜1", or "防御 ・ 自分". */
    this.KindLine = '';
    /** The full text, one sentence after another. */
    this.Body = '';
    /** (List<TermDefinition>) */
    this.Terms = [];
    init(this, members);
  }
}

/**
 * Which family a status chip belongs to (battle-visual-v1 §7.3). The View draws the corner from it:
 * a square chip for the words put on the opponent, a round one for the words put on oneself, ∞ for
 * a lasting stance, the boss colour's double frame for a boss's word.
 */
export const ChipKind = defineEnum(['OnFoe', 'OnSelf', 'Lasting', 'Boss']);

export class StatusChip {
  constructor(members) {
    this.Label = null;
    /** The stacks; for a Lasting chip the times the same stance is held (1 prints no "×1"). */
    this.Stacks = 0;
    this.Kind = ChipKind.OnFoe;
    /** §7.3 説明パネルの見出し: "鈍足 2", "岩の構え ×2". */
    this.Title = '';
    /** §7.3: the family on the panel's right, "相手に付ける系" / "自分に付ける系" / "永続" / "ボス専用". */
    this.Group = '';
    /** §7.3 効き方, written from the status table's values. */
    this.Effect = '';
    /** §7.3 減り方. */
    this.Decay = '';
    init(this, members);
  }
}

export class UnitFrame {
  constructor(members) {
    this.Hp = 0;
    this.HpMax = 0;
    this.Guard = 0;
    this.ShowStamina = false;
    this.Stamina = 0;
    this.StaminaMax = 0;
    /**
     * battle-visual-v1 §4.7 次のターンに回復する分 (#455): the stamina that comes back at the next turn
     * start if the turn ended now (the core's estimate, already capped at the maximum). The stamina
     * tag draws that many dashed dots after the ones held. 0 draws none.
     */
    this.NextRecovery = 0;
    /**
     * battle-visual-v1 §4.5 構えの盾 (#455), an enemy's only: whether 構え is expected to give it
     * Guard at the end of its phase, read off the stamina it holds now against the rule's
     * threshold (残り 3 以上). A solid shield when set, a dashed one when not. It is an estimate:
     * 構え is judged after the enemy acts.
     */
    this.ReserveLikely = false;
    /** battle-visual-v1 §4.5 (#455): an elite's number panel has a phosphor frame. */
    this.Elite = false;
    /** Only bosses, elites and the player hold a range side (battle_core_v4 §17.3). */
    this.HasRange = false;
    this.Range = RangeSide.Near;
    /** The one glyph on the tag at the unit's feet ("近" / "遠"). Required when HasRange. */
    this.RangeGlyph = '';
    /** (List<StatusChip>) */
    this.Statuses = [];
    /**
     * Whose art the figure wears (#288): the enemy's id, as the art folder is named
     * (CharacterArt). Empty, or an id with no art, keeps the placeholder silhouette.
     */
    this.ArtId = '';
    /** The unit has fallen and its figure is gone (battle_ui_ux_v2 §2.2 の 6). Set from the Defeat beat on. */
    this.Down = false;
    /**
     * battle-visual-v1 §4.6 / battle_core_v4 §4 v4.4 (#455): the stances the unit holds, in the
     * order its list shows them (StanceList.Rows: the newest last, a repeat counted on one row).
     * Drawn at the unit's edge when DepictionFrame.StanceListsShown. (List<StanceRow>)
     */
    this.Stances = [];

    // ---- JS extension: several enemies (docs/port-notes/depiction-multi-enemy.md). The C# has none of these. ----

    /**
     * Which enemy this is: its index in the core's enemy list (BattleState.Enemies, the Unit every
     * BattleEvent and Cue carries), fixed for the whole battle. −1 on the player's frame.
     */
    this.Unit = -1;
    /** Where the enemy stands: its lowest cell (FloorFrame counts) and how many cells it takes. 0 on the player's frame. */
    this.Cell = 0;
    this.Size = 0;
    /**
     * battle_core_v4 §7.2: this enemy's own N to the player (the empty cells between them). −1 once
     * it has fallen in the core (also while its Defeat beat is still to play: the core has taken its
     * cells, and the player may already stand past them), and on the player's frame. The screen
     * prints one N (Player.RangeGlyph, the nearest enemy's); this is the one it shows while a held
     * card frames this enemy.
     */
    this.Gap = -1;
    /** Gap as the floor's number prints it ("2"); empty when Gap is −1. */
    this.GapGlyph = '';
    /**
     * battle-visual-v1 §4.2: the enemy's hatch — the direction of its red stripes on the floor and of
     * its swatch on the HP row, the omen plate and the stance frame (EnemyHatch.AngleOf: 0 = 45°,
     * 1 = −45°, 2 = 90°). The enemy's Unit. −1 on the player's frame.
     */
    this.Hatch = -1;
    init(this, members);
  }
}

/**
 * JS extension (docs/port-notes/depiction-multi-enemy.md): battle-visual-v1 §3.2 / §4.2 斜線 — the
 * red stripes of each enemy's aimed cells and swatch turn a different way, 45° / −45° / 90°, so two
 * or three enemies' aims are told apart by direction alone and cross where they overlap.
 */
export const EnemyHatch = Object.freeze({
  /** One hatch per enemy: battle_core_v4 §7.4 ENEMIES_MAX. */
  Count: 3,
  /** The angle of hatch `hatch` in degrees, as §3.2 lists them; 45 for one outside 0..2. */
  AngleOf(hatch) {
    switch (hatch) {
      case 1: return -45;
      case 2: return 90;
      default: return 45;
    }
  },
});

/** One stance on a side's list (#455): its name and how many times it is held (1 prints no "×1"). */
export class StanceRow {
  constructor(members) {
    this.Name = '';
    this.Count = 1;
    init(this, members);
  }
}

export class OmenFrame {
  constructor(members) {
    this.Visible = false;
    /** The icon of the omen's kind (#349). None draws no icon. */
    this.Icon = OmenIcon.None;
    /** The kind as a word: "攻撃", "防御", "移動", "構え", "技", "休み". */
    this.KindLabel = '';
    /**
     * The reach the omen aims at ("1〜2"), kept until the floor shows the aimed cells (#242).
     * The fixed script (TurnSliceScript), written for v4.2, still writes the side glyph. Empty when none.
     */
    this.SideGlyph = '';
    /** The number beside the icon: attack (the core preview) and guard only (#349). Empty for any other kind. */
    this.ValueText = '';
    /**
     * battle-visual-v1 §4.4 当たり外れの印 (#242): whether the omen reaches the player where they
     * stand now. The core's OmenPreview.Lands when the bridge holds one for this omen that is not
     * a rest; otherwise the aimed cells against the player's near edge. None for an omen that aims
     * at nobody, and for the v4.2 sources, which keep SideGlyph in its place.
     */
    this.Hit = OmenHit.None;

    /**
     * battle-visual-v1 §4.4 状態付与 (#455): for a Seal omen, the statuses it puts on the player, at
     * most two, drawn at 28 px where the number would stand. Empty for any other omen, and below
     * 開示度 2 (OmenDisclosure). (List<OmenStatus>)
     */
    this.Statuses = [];

    /**
     * battle-visual-v1 §4.4 / battle_ui_ux_v2 §7 (#455): how far the journal has read this enemy,
     * 0〜2. The writer has already blanked what the level does not show (OmenDisclosure), so the
     * View draws the frame as it is. Every source writes OmenDisclosure.Full until the journal
     * hands a level to the battle.
     */
    this.Disclosure = OmenDisclosure.Full;

    /**
     * #50, §17.6 F11: the 予定 of an elite or a boss — the second action it plans, shown beside the
     * omen. It is not a commitment (the tree is read again after the first action). It stays up
     * after the first omen is spent, until the second action acts. Independent of Visible.
     */
    this.PlanVisible = false;
    /** The icon of the plan's kind (#349), as Icon picks it. */
    this.PlanIcon = OmenIcon.None;
    /** The plan's kind: "攻撃", "防御", "休み"... */
    this.PlanKindLabel = '';
    /**
     * The plan's number, attack and guard only (#349): the face alone, no trait, stance or wall,
     * since the plan is not a commitment. Empty for any other kind.
     */
    this.PlanValueText = '';
    /** §4.4: the plan's statuses as Statuses writes them, drawn at 20 px. (List<OmenStatus>) */
    this.PlanStatuses = [];
    /**
     * The cells the plan aims at, as SideGlyph writes them. Empty when none. The plan badge does
     * not draw it (battle-visual-v1 4.4 gives the plan no reach); it stays in the script for the
     * floor cells of #242.
     */
    this.PlanSideGlyph = '';
    /** True on the frames after a 予定変更, so the badge can mark the plan as changed. */
    this.PlanChanged = false;

    // ---- JS extension: several enemies (docs/port-notes/depiction-multi-enemy.md). ----

    /** Whose omen this is: the enemy's index in the core's enemy list (UnitFrame.Unit). */
    this.Unit = 0;
    /** battle-visual-v1 §4.4: the swatch at the right end of the plate's first row — the enemy's hatch (UnitFrame.Hatch). */
    this.Hatch = 0;
    init(this, members);
  }

  /** The tag on the plan badge: 予定, or 予定変更 once the plan was replaced (#50). */
  get PlanTag() {
    return this.PlanChanged ? DepictionText.PlanChangedTag : DepictionText.PlanTag;
  }
}

export class CornerFrame {
  constructor(members) {
    this.Turn = 0;
    this.Floor = 0;
    this.ChainIndex = 0;
    this.ChainTotal = 0;
    this.MiasmaPercent = 0;
    init(this, members);
  }
}

/** How the battle stands: still going, or over one way or the other (the one card, #77). */
export const BattleOutcome = defineEnum(['Ongoing', 'Won', 'Lost']);

/** One settled screen. The View can rebuild everything it shows from a frame alone. */
export class DepictionFrame {
  constructor(members) {
    /** Won or Lost once the battle is over; the View shows the one card for it. */
    this.Outcome = BattleOutcome.Ongoing;
    this.Corner = new CornerFrame();
    this.Player = new UnitFrame();
    this.Enemy = new UnitFrame();
    this.Omen = new OmenFrame();
    /** (List<CardFace>) */
    this.Hand = [];
    /** Shield hint beside the end-turn button ("+3"); empty when the stance would not trigger. */
    this.StanceHint = '';
    /**
     * battle-visual-v1 §4.7 (#242): the stamina 構え needs left at the turn end; the stamina tag
     * puts its divider after that many dots. 0 draws no divider.
     */
    this.StanceDivider = 0;
    /** battle-visual-v1 §4.2 (#242): the floor. FloorFrame.Cells 0 draws none (the v4.2 sources). */
    this.Floor = new FloorFrame();
    /** battle-visual-v1 §4.7 (#455): the draw, discard and exile piles. PileFrame.Shown false draws none. */
    this.Piles = new PileFrame();
    /**
     * battle-visual-v1 §4.6 (#455): the source keeps the stance lists (UnitFrame.Stances), so each
     * side's edge shows its list, or the dashed empty frame. False draws neither (the v4.2 sources
     * and the filming script).
     */
    this.StanceListsShown = false;
    /** battle-visual-v1 §4.9 (#455): the attributes played this turn. PlayedFrame.Shown false draws no frames. */
    this.Played = new PlayedFrame();
    /**
     * battle-visual-v1 §5.5 (#455): the enemy's turn — from the player's turn end until the next
     * turn start. The screen sinks below y 790 and the end-turn plate dims while it holds. The
     * v4.2 sources leave it false.
     */
    this.EnemyTurn = false;

    // ---- JS extension: several enemies (docs/port-notes/depiction-multi-enemy.md). The C# has none of these. ----
    //
    // Enemy, Omen, Floor's enemy members and Player's range describe one enemy: the first one standing
    // in the line (the lowest cell; once none stands, the one that fell last) — in a one-enemy battle,
    // the enemy, as before. Enemy is the very object Enemies holds for it, and Omen the one Omens holds.

    /**
     * Every enemy of the battle, one frame each, in the core's enemy order (BattleState.Enemies:
     * definition order, which is the phase order and the line front to back as the battle starts,
     * battle_core_v4 §7.4). Enemies[k].Unit is k for the whole battle; a fallen enemy stays with
     * Down set. Enemies may pass each other later (§7.1), so a figure stands at its Cell, not at its
     * place in this list. (List<UnitFrame>)
     */
    this.Enemies = [];
    /** Each enemy's omen badge, in the order of Enemies (Omens[k].Unit is k), read against that enemy's own actions. (List<OmenFrame>) */
    this.Omens = [];
    /**
     * battle-visual-v1 §4.2 / §4.4 / §4.5 / §4.6: the battle has two or more enemies, so each one's
     * HP row, omen plate and stance frame carry its hatch swatch (UnitFrame.Hatch). False in a
     * one-enemy battle. It holds for the whole battle, also once one enemy is left standing.
     */
    this.SwatchesShown = false;
    init(this, members);
  }
}

/**
 * The three piles at the bottom left (battle-visual-v1 §4.7, #455): how many cards each holds.
 * The View stacks that many sheets' worth of thickness (PileLayout) and writes no number
 * (battle_ui_ux_v2 §1.1 B9: 束は厚みだけ).
 */
export class PileFrame {
  constructor(members) {
    /** Whether the source keeps piles at all. The fixed filming script does not. */
    this.Shown = false;
    this.Draw = 0;
    this.Discard = 0;
    this.Exile = 0;

    /**
     * battle_ui_ux_v2 §1.1 C4 / §3.13 (#455): what each pile holds, for the list a click on the pile
     * opens. One row per kind of card, never in draw order (the order of the draw pile stays
     * hidden). Empty when the source keeps no lists. (List<PileCard>)
     */
    this.DrawCards = [];
    this.DiscardCards = [];
    this.ExileCards = [];
    init(this, members);
  }

  /** The rows of `pile`'s list. */
  CardsOf(pile) {
    switch (pile) {
      case PileKind.Draw: return this.DrawCards;
      case PileKind.Discard: return this.DiscardCards;
      case PileKind.Exile: return this.ExileCards;
      default: throw argRangeValue('pile', pile);
    }
  }
}

/**
 * One row of a pile's list (battle_ui_ux_v2 §1.1 C4 同種の残り「突き 2 / 3」): a kind of card, how
 * many of it the pile holds, and how many the battle's deck holds in all.
 */
export class PileCard {
  constructor(members) {
    this.Name = '';
    this.InPile = 0;
    this.InDeck = 0;
    init(this, members);
  }

  /** "突き 2 / 3". */
  get RowText() {
    return str(this.Name) + ' ' + this.InPile + ' / ' + this.InDeck;
  }
}

/**
 * The attributes played this turn (battle-visual-v1 §4.9 / battle_ui_ux_v2 §1.1 B6, #455): how many
 * cards of each, and which attribute the last one counted as. The core folds a card into one
 * attribute (battle_core_v4 §2.1); a card of movement only counts as none.
 */
export class PlayedFrame {
  constructor(members) {
    /** Whether the source keeps this tally. The v4.2 sources do not, and draw no frames. */
    this.Shown = false;
    this.Attack = 0;
    this.Guard = 0;
    this.Skill = 0;
    this.Stance = 0;
    /** B6 直前のカードに下線: the attribute of the card played last this turn, or null. (CardKind?) */
    this.Last = null;
    /**
     * battle_ui_ux_v2 §1.1 B6 / §3.6 (f) (#455): the power a 追撃 adds to the player's next attack face
     * this turn, as the core holds it (CombatantState.FollowUp). The row shows "+5" at its left end
     * while it is above 0; the core clears it when the next attack face spends it or the turn ends.
     */
    this.FollowUp = 0;
    init(this, members);
  }

  CountOf(kind) {
    switch (kind) {
      case CardKind.Attack: return this.Attack;
      case CardKind.Guard: return this.Guard;
      case CardKind.Skill: return this.Skill;
      case CardKind.Stance: return this.Stance;
      default: return 0;
    }
  }

  /** Counts one more card of `kind` and makes it the last one. */
  Add(kind) {
    switch (kind) {
      case CardKind.Attack: this.Attack++; break;
      case CardKind.Guard: this.Guard++; break;
      case CardKind.Skill: this.Skill++; break;
      case CardKind.Stance: this.Stance++; break;
      default: return;
    }
    this.Last = kind;
  }
}

/**
 * The line of floor cells under the figures (battle-visual-v1 §4.2, #242). Cells count from 1 at
 * the player's end, as BattleCore does. Every cell here is the core's; the View only draws them.
 */
export class FloorFrame {
  constructor(members) {
    /** How many cells the line has (5〜8). 0: the source draws no floor. */
    this.Cells = 0;
    /** The player's lowest cell and how many cells it takes. */
    this.PlayerCell = 0;
    this.PlayerSize = 1;
    /** The enemy's lowest cell and how many cells it takes (2 for a large elite). */
    this.EnemyCell = 0;
    this.EnemySize = 1;
    /** N: the empty cells between the player and the enemy. The one number on the floor. */
    this.Gap = 0;
    /** The cells the shown omen reaches (赤の斜線). Empty when it aims at nobody or is hidden. (List<int>) */
    this.AimCells = [];
    /**
     * battle-visual-v1 §4.2 境目を満たすマス (#455): of AimCells, the ones whose gap meets the
     * boundary the omen's action reads (間合い n 以上 / n 以下). Their stripes are drawn 6 px apart
     * instead of 11. Empty when the action reads no boundary or the omen is hidden. (List<int>)
     */
    this.DenseCells = [];
    /**
     * battle_ui_ux_v2 §5.8 / battle-visual-v1 §4.2 敵が動く予兆の移る先 (#455): the cells the enemy
     * would stand on after the move its shown omen makes (菫の破線の枠, no stripes). Empty when the
     * omen does not move it, or is hidden. (List<int>)
     */
    this.MoveCells = [];

    // ---- JS extension: several enemies (docs/port-notes/depiction-multi-enemy.md). ----

    /**
     * battle-visual-v1 §4.2: every enemy's cells and the cells its own shown omen aims at, in the
     * order of DepictionFrame.Enemies. The single-valued members above (EnemyCell, Gap, AimCells …)
     * are the one enemy DepictionFrame.Enemy describes; these are all of them. (List<FloorEnemy>)
     */
    this.Enemies = [];
    init(this, members);
  }
}

/**
 * JS extension (docs/port-notes/depiction-multi-enemy.md): one enemy on the floor (battle-visual-v1
 * §4.2) — where it stands and what its shown omen aims at. Every cell is the core's, read as for the
 * one enemy of FloorFrame. The View hatches AimCells in the direction of Hatch; where two enemies'
 * AimCells share a cell the two directions cross.
 */
export class FloorEnemy {
  constructor(members) {
    /** The enemy's index in the core's enemy list (UnitFrame.Unit). */
    this.Unit = 0;
    /** The direction of its stripes (UnitFrame.Hatch, EnemyHatch.AngleOf). */
    this.Hatch = 0;
    /** Its lowest cell and how many cells it takes. */
    this.Cell = 0;
    this.Size = 1;
    /** Its N to the player; −1 once it has fallen in the core (UnitFrame.Gap). */
    this.Gap = -1;
    /** It has fallen (UnitFrame.Down): its figure and its cells are gone, and it aims at nothing. */
    this.Down = false;
    /** The cells its shown omen reaches (赤の斜線). Empty when that aims at nobody or is hidden. (List<int>) */
    this.AimCells = [];
    /** Of AimCells, the ones meeting the boundary its action reads (stripes 6 px apart). (List<int>) */
    this.DenseCells = [];
    /** The cells it would stand on after the move its shown omen makes (菫の破線の枠). (List<int>) */
    this.MoveCells = [];
    init(this, members);
  }
}

/**
 * battle_ui_ux_v2 §5.3 の系統, as far as the slice plays them: the shape a blow draws and the way
 * an enemy moves into it. The writer of the script picks it (StrikeSystems.Of); the View only
 * draws what it is told.
 */
export const StrikeSystem = defineEnum([
  /** 斬: a diagonal arc across the target. The default. */
  'Slash',
  /** 突: one straight line into the target. */
  'Thrust',
  /** 払: a wide horizontal band. */
  'Sweep',
  /** 打: a ripple where it lands. Pushes and pulls play as this. */
  'Strike',
  /** 盾: no blow; the figure raises its guard. */
  'Shield',
  /** A move without a blow: the figure steps. */
  'Step',
]);

/**
 * §5.3: the blow's shape and the enemy's move from the technique's name and faces. The words and
 * their priority (突 → 打 → 払 → 斬) are the grammar's (EffectGrammar.BlowOf).
 */
export const StrikeSystems = {
  Of(name, attacks, guards, moves, pushes) {
    // A push or pull is what the action is for, even when its name holds 突 or 払, so it plays as 打.
    if (attacks) return EffectGrammar.ToStrike(EffectGrammar.BlowOf(name, pushes));
    if (moves) return StrikeSystem.Step;
    return guards ? StrikeSystem.Shield : StrikeSystem.Step;
  },
};

export const DepictionEventKind = defineEnum([
  'TurnStart',
  'PlayCard',
  'TurnEnd',
  'EnemyAction',
  'NextOmen',
  /** A unit falls (#288): the beat of its own that plays the 800 ms fall, after the blow's event. */
  'Defeat',
]);

export const CueKind = defineEnum([
  'GuardReset',
  'StaminaChange',
  'DrawHand',
  'OmenShow',
  'TraitFire',
  'Slash',
  'GuardGain',
  'RangeSwitch',
  'StanceCue',
  'DiscardHand',
  'EnemyWindup',
  'SideBonusMiss',
  'GuardBlock',
  'Hit',
  /**
   * A status word gained or lost stacks (battle_core_v4 §5). Added for the core-driven turn
   * (issue #73); how it plays is issue #77's, and until then the chips simply settle with the frame.
   */
  'StatusChange',
  /**
   * HP moved without a blow (#188): 出血 / 再生 at turn start, a heal face, a 見切り return.
   * Cue.Amount is signed (−4, +6), Cue.HpAfter settled and Cue.Text names the cause ("出血").
   * Unlike Hit it spends no omen.
   */
  'HpChange',
  /**
   * The target falls (#288, battle_ui_ux_v2 §2.2 の 6): it tilts, sinks, darkens and is gone, and its
   * cell is empty. The frame after it carries UnitFrame.Down.
   */
  'Defeat',
]);

/**
 * One beat inside an event. Values are settled results, never inputs to a formula:
 * the View shows Amount and then sets the gauges to the *After values.
 */
export class Cue {
  static Unchanged = -1;

  constructor(members) {
    this.Kind = CueKind.GuardReset;
    this.Target = UnitSide.Player;
    /** Slash only: who attacks. Required there, so the View never guesses it from the target. (UnitSide?) */
    this.Source = null;
    this.Amount = 0;
    this.Text = '';
    /** Strength of the effect, 1..4, decided by whoever writes the script (for now always EffectStrength.Uniform, §5.3 / #30). */
    this.Intensity = 1;
    this.HpAfter = Cue.Unchanged;
    this.GuardAfter = Cue.Unchanged;
    this.StaminaAfter = Cue.Unchanged;
    /** StaminaChange only: how many pips the unit has, so the View never looks outside the cue. */
    this.StaminaMax = Cue.Unchanged;
    this.RangeAfter = RangeSide.Near;
    /** RangeSwitch only: the glyph the tag flips to ("遠"). */
    this.RangeGlyphAfter = '';
    /** StatusChange only: the stacks left of the word named in Text (0 = gone). */
    this.StacksAfter = Cue.Unchanged;
    /**
     * DrawHand only (battle_ui_ux_v2 §3.14 山札切れ, #455): the draw pile ran out while this cue's
     * cards were drawn and the core made it again from the discard pile (DeckReshuffled) after this
     * many of them. Unchanged when the pile was not made again.
     */
    this.RebuildAfter = Cue.Unchanged;
    /** Slash and EnemyWindup: the shape of the blow and of the enemy's move (§5.3). */
    this.System = StrikeSystem.Slash;
    /**
     * §5.3 (#57): the system this beat draws, written by the script from the card or action that
     * plays it: the blow's on a Slash, 盾 on a guard gained, 型 on a stance set, 気 on a status put
     * on, 息 on a heal, a draw or a stamina point. Null: the beat draws no shape of the grammar
     * (a turn start's tick, the reserve's Guard). The View draws EffectGrammar.LookId. (FxSystem?)
     */
    this.Fx = null;
    /**
     * Slash only (§5.9 順 1, J2): a trait of the card or action fired, so the View layers a short
     * cue on the blow. It never changes Intensity.
     */
    this.TraitAccent = false;
    /** RangeSwitch only: the other side moved the player (a push or a pull), not the player itself. */
    this.Pushed = false;
    /**
     * RangeSwitch only (battle-visual-v1 §4.2, #242): the lowest cell of the player and of the enemy
     * after the move, and N after it, for a source that draws the floor. 0 when it draws none.
     */
    this.PlayerCellAfter = 0;
    this.EnemyCellAfter = 0;
    this.GapAfter = 0;
    /**
     * RangeSwitch on the floor only (battle_ui_ux_v2 §5.7 順 1, #586): how many cells the side that
     * moved went, so the walk takes 160 ms a cell. 0 when the source draws no floor.
     */
    this.CellsMoved = 0;

    // ---- JS extension: several enemies (docs/port-notes/depiction-multi-enemy.md). ----

    /**
     * Which enemy the cue is about (its index in the core's enemy list), or −1 when it concerns the
     * player alone. Slash, GuardBlock and Hit — a blow, a wall, a 見切り return — carry the enemy of
     * the exchange, the one struck or the one striking. GuardReset, GuardGain, StatusChange,
     * HpChange and TraitFire carry their holder when it is an enemy; OmenShow, EnemyWindup,
     * SideBonusMiss and Defeat their enemy. RangeSwitch carries the enemy that moved (by itself or
     * pushed), −1 when the player moved. A one-enemy battle writes 0 or −1.
     */
    this.Unit = -1;
    /**
     * RangeSwitch with Unit ≥ 0 only: that enemy's lowest cell and its N after the move. The
     * PlayerCellAfter / EnemyCellAfter / GapAfter above describe the enemy DepictionFrame.Enemy does.
     */
    this.UnitCellAfter = 0;
    this.UnitGapAfter = 0;
    init(this, members);
  }
}

export class DepictionEvent {
  constructor(members) {
    this.Order = 0;
    this.Kind = DepictionEventKind.TurnStart;
    this.Title = '';
    /** PlayCard only: the card the script expects and where it must be released. */
    this.CardId = '';
    this.Aim = CardAim.Single;
    /** PlayCard only: the single predicted value shown on the receiver while dragging. */
    this.PreviewText = '';
    /**
     * PlayCard only (battle-visual-v1 §4.7 / battle_ui_ux_v2 §5.11 順 4, #455): the pile the played
     * card goes to, as the core put it — the exile pile for a stance card (CardExiled), the discard
     * pile for any other. Its ghost flies there.
     */
    this.PlayedTo = PileKind.Discard;
    /**
     * PlayCard and the enemy's action (#57): the systems of the card or action, in the order its
     * faces resolve (§5.4). Empty for any other event, and for a movement-only card. (List<FxSystem>)
     */
    this.Systems = [];
    /** (List<Cue>) */
    this.Cues = [];
    /** The settled screen once every cue has played. (DepictionFrame) */
    this.After = null;
    /**
     * JS extension (docs/port-notes/depiction-multi-enemy.md): which enemy the event is about — the
     * one whose phase an EnemyAction beat belongs to, whose next omen a NextOmen shows, who falls in
     * a Defeat, and the one a PlayCard aimed at one enemy was played on. −1 for a turn start, a turn
     * end and a card aimed at nobody or at every enemy in reach.
     */
    this.Unit = -1;
    init(this, members);
  }

  get WaitsForDrag() {
    return this.Kind === DepictionEventKind.PlayCard;
  }
}

export class DepictionScript {
  constructor(members) {
    /** (DepictionFrame) */
    this.Opening = null;
    /** (List<DepictionEvent>) */
    this.Events = [];
    init(this, members);
  }
}

export const PlayVerdict = defineEnum([
  'Accepted',
  /** Nothing waits for the player: an event is still playing. */
  'NotWaiting',
  /** The card is no longer in the hand. */
  'NotInHand',
  /** The card costs more stamina than is left. */
  'NotEnoughStamina',
  /** The card only works from the other range side. */
  'OutOfRange',
  /** Released somewhere this card cannot be released. */
  'WrongZone',
  /**
   * Fixed-script playback only: the script writes one order and this is not its next card.
   * A played battle never answers this — there the hand is free (issue #36).
   */
  'OffScript',
  /**
   * The player is bound (呪縛, enemy_roster_v4 §4.1) and the card is one of movement only (#335).
   * Only the core-driven source answers this; the View treats it as any other refusal.
   */
  'Bound',
]);

/** Zones and strings the screen shows as given. Shared by every source. */
export const DepictionText = {
  RequiredZone(aim) {
    return aim === CardAim.Single ? DropZone.Receiver : DropZone.AboveThrowLine;
  },

  /** The turn banners and the one card at the end (battle_ui_ux_v2 §5.1 順 1 / §5.13 順 5, #77). */
  YourTurn: 'あなたの番',
  EnemyTurn: '敵の番',
  Won: '勝ち',
  Lost: '負け',

  /** The muted label on a figure the other side pushed (battle_ui_ux_v2 §5.7 順 6). */
  Pushed: '押し出し',

  /** The tag on the plan badge (battle-visual-v1 4.4) and its form after a plan change (#50). */
  PlanTag: '予定',
  PlanChangedTag: '予定変更',

  OutcomeText(outcome) {
    return outcome === BattleOutcome.Won ? DepictionText.Won : outcome === BattleOutcome.Lost ? DepictionText.Lost : '';
  },

  ZoneName(aim) {
    return aim === CardAim.Single ? '敵の受け皿の上' : '投げ上げ線より上';
  },

  /** Why a card released in the wrong zone went back to the hand. Every source says it the same way. */
  WrongZoneText(face) {
    return '「' + str(face.Name) + '」は' + DepictionText.ZoneName(face.Aim) + 'で離すと出せます';
  },

  Find(hand, cardId) {
    for (const face of hand) {
      if (face.Id === cardId) return face;
    }
    return null;
  },

  NameOf(hand, cardId) {
    const face = DepictionText.Find(hand, cardId);
    return face == null ? '' : face.Name;
  },
};

/**
 * What the screen plays, and what it may refuse. Two sources answer it: the fixed script kept
 * for filming (DepictionRunner) and the fight the player actually plays, which the bridge runs on
 * the BattleCore rules (CoreBattleSource, #571). The View holds one of them and never asks which
 * it is. Every line of text it shows is written here, so the View still prints rather than computes.
 *
 * A C# interface: nothing to check at runtime. Its members, for reference:
 * @typedef {object} IDepictionSourceShape
 * @property {DepictionFrame} Frame The settled screen right now.
 * @property {boolean} Finished True when nothing is left to play.
 * @property {boolean} WaitingForPlayer True while the screen waits for the player to release a card or end the turn.
 * @property {boolean} CanEndTurn True when the player may end the turn right now.
 * @property {() => DepictionEvent} AdvanceAuto Takes the next event that runs on its own. Throws while the source waits for the player.
 * @property {() => DepictionEvent} EndTurn Ends the turn. Throws when CanEndTurn is false.
 * @property {(cardId: string) => string} Inspect Why the card cannot be released at all right now; Accepted when it can.
 * @property {(cardId: string, zone: string) => { ok: string, value: DepictionEvent|null, verdict: string, played: DepictionEvent|null }} TryPlay
 * @property {(cardId: string) => string} PreviewFor The one predicted value shown on the receiver while the card is dragged; "" when none.
 * @property {string} SuggestedCardId The card the source would drag by itself for a capture, or "" when it has no opinion.
 * @property {string} GuideText The line above the hand while the source waits, or "".
 * @property {(cardId: string, verdict: string) => string} RefusalText Why a released card went back to the hand, or "" when there is nothing to say.
 *
 * JS extension (docs/port-notes/depiction-multi-enemy.md): CoreBattleSource's Inspect, TryPlay,
 * PreviewFor and RefusalText take the enemy a card is released on (its index in the core's enemy
 * list, DepictionFrame.Enemies[k].Unit) as an optional last argument, and TargetFor(cardId) names the
 * one it goes to when none is given. DepictionRunner ignores an extra argument.
 */
export const IDepictionSource = Object.freeze({
  $members: Object.freeze([
    'Frame', 'Finished', 'WaitingForPlayer', 'CanEndTurn', 'AdvanceAuto', 'EndTurn', 'Inspect',
    'TryPlay', 'PreviewFor', 'SuggestedCardId', 'GuideText', 'RefusalText',
  ]),
});

/**
 * Walks a fixed script in order, for captures and for filming a known slice. Holds no rules:
 * it only knows which event is next, whether that event waits for a drag, and whether a
 * released card is the one the script wrote. A player who wants a free hand fights through the
 * bridge's CoreBattleSource instead.
 * @implements {IDepictionSource}
 */
export class DepictionRunner {
  constructor(script) {
    if (script == null) throw argNull('script');
    /** @private readonly */
    this._script = script;
    /** C#: `public int Index { get; private set; }` */
    this.Index = 0;
    /** C#: `public DepictionFrame Frame { get; private set; }` */
    this.Frame = script.Opening;
  }

  get Finished() {
    return this.Index >= this._script.Events.length;
  }

  get Next() {
    return this.Finished ? null : this._script.Events[this.Index];
  }

  get WaitingForPlayer() {
    return !this.Finished && this.Next.WaitsForDrag;
  }

  /** A fixed script ends its own turns, so the player never does. */
  get CanEndTurn() {
    return false;
  }

  get SuggestedCardId() {
    return this.WaitingForPlayer ? this.Next.CardId : '';
  }

  get GuideText() {
    if (!this.WaitingForPlayer) return '';
    const next = this.Next;
    return '台本の次の一手：「' + str(DepictionText.NameOf(this.Frame.Hand, next.CardId)) + '」を'
      + DepictionText.ZoneName(next.Aim) + 'へ';
  }

  /** Takes the next automatic event. Throws when the script is waiting for a drag. */
  AdvanceAuto() {
    if (this.Finished) throw new InvalidOperationException('The script has already finished.');
    if (this.WaitingForPlayer) throw new InvalidOperationException('Event ' + this.Next.Order + ' waits for a drag.');
    return this.Take();
  }

  EndTurn() {
    throw new InvalidOperationException('A fixed script ends its own turn; the player cannot.');
  }

  Inspect(cardId) {
    if (!this.WaitingForPlayer) return PlayVerdict.NotWaiting;
    return this.Next.CardId === cardId ? PlayVerdict.Accepted : PlayVerdict.OffScript;
  }

  /**
   * C#: `PlayVerdict TryPlay(string cardId, DropZone zone, out DepictionEvent played)`.
   * Returns `{ ok, value }` (the PlayVerdict and the played event, null unless Accepted), also given
   * as `verdict` and `played`, as CoreBattleSource does.
   * @returns {{ ok: string, value: DepictionEvent|null, verdict: string, played: DepictionEvent|null }}
   */
  TryPlay(cardId, zone) {
    let played = null;
    const result = (verdict) => ({ ok: verdict, value: played, verdict, played });
    const verdict = this.Inspect(cardId);
    if (verdict !== PlayVerdict.Accepted) return result(verdict);
    if (zone !== DepictionText.RequiredZone(this.Next.Aim)) return result(PlayVerdict.WrongZone);
    played = this.Take();
    return result(PlayVerdict.Accepted);
  }

  PreviewFor(cardId) {
    const next = this.Next;
    return next != null && next.CardId === cardId ? next.PreviewText : '';
  }

  RefusalText(cardId, verdict) {
    const face = DepictionText.Find(this.Frame.Hand, cardId);
    if (face == null || this.Next == null) return '';
    if (verdict === PlayVerdict.OffScript) {
      return '「' + str(face.Name) + '」はまだ出せません。台本の次は「'
        + str(DepictionText.NameOf(this.Frame.Hand, this.Next.CardId)) + '」です';
    }
    if (verdict === PlayVerdict.WrongZone) {
      return DepictionText.WrongZoneText(face);
    }
    return '';
  }

  /** (C# private) */
  Take() {
    const ev = this._script.Events[this.Index];
    this.Index += 1;
    this.Frame = ev.After;
    return ev;
  }
}
