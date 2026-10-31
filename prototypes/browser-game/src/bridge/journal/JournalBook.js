// Port of unity-project-kit/Assets/View/Journal/Script/JournalBook.cs
// The journal drawer's content, shared by every screen that opens the drawer: the exploration
// screen now, and the battle screen once its drawer lands (battle_ui_ux_v2.md, 手記ドロワー).
// Pure: which rows a disclosure level hides and which tabs open where are decided here, so the
// drawer View only lays the text out. The pages' real contents are #107's.
import { defineEnum, firstOrDefault, ArgumentException, ArgumentOutOfRangeException } from '../../shared/cs.js';

/**
 * The drawer's tabs: one per enemy, the dungeon's page, the player's memo, and the page of terms
 * and statuses (#234). Pages are handed over in this order, so the tabs read left to right in it.
 * Terms: the rules' words and the statuses: no disclosure, never left at a death (JournalTerms).
 */
export const JournalTab = defineEnum(['Enemy', 'Dungeon', 'Memo', 'Terms']);

/**
 * Where the journal is read. In battle only the enemy pages and the page of terms open; while
 * exploring, all of them.
 */
export const JournalContext = defineEnum(['Exploration', 'Battle']);

// .NET's formatting of ArgumentOutOfRangeException(paramName, actualValue, message).
function outOfRange(param, value, message) {
  return new ArgumentOutOfRangeException(`${message} (Parameter '${param}')\nActual value was ${value}.`);
}

/**
 * One line of a page. A row the reader has not learned yet reads as 「？」. A row with a label and
 * no text is a heading.
 */
export class JournalRow {
  static Hidden = '？';

  constructor(label, text, needsDisclosure = 0) {
    if (needsDisclosure < 0 || needsDisclosure > JournalPage.MaxDisclosure)
      throw outOfRange('needsDisclosure', needsDisclosure, 'disclosure runs 0..2');

    this.Label = label ?? '';
    this.Text = text ?? '';
    /** The disclosure the page must have reached before this row is readable. */
    this.NeedsDisclosure = needsDisclosure;
  }

  toJSON() {
    return { $type: 'JournalRow', Label: this.Label, Text: this.Text, NeedsDisclosure: this.NeedsDisclosure };
  }
}

/** One page of the journal: an enemy, the dungeon, the memo, or the terms. */
export class JournalPage {
  /** Encounter gives 1, observation gives 2 (concept-v3.md §14). */
  static MaxDisclosure = 2;

  constructor(id, tab, title, rows, disclosure = JournalPage.MaxDisclosure) {
    if (id == null || id.length === 0) throw new ArgumentException("a page needs an id (Parameter 'id')");
    if (disclosure < 0 || disclosure > JournalPage.MaxDisclosure)
      throw outOfRange('disclosure', disclosure, 'disclosure runs 0..2');

    this.Id = id;
    this.Tab = tab;
    this.Title = title ?? '';
    this.Rows = rows ?? [];
    this.Disclosure = disclosure;
  }

  /**
   * 「開示度 1 / 2」 beside an enemy page's title. The other pages carry none, and neither does an
   * enemy page at 0: a real page exists only after an encounter, so 0 is a stand-in.
   */
  get DisclosureLabel() {
    return this.Tab === JournalTab.Enemy && this.Disclosure > 0 ? `開示度 ${this.Disclosure} / ${JournalPage.MaxDisclosure}` : '';
  }

  IsHidden(row) {
    return row != null && row.NeedsDisclosure > this.Disclosure;
  }

  /** What the drawer prints for a row: its text, or 「？」 when it is not learned yet. */
  TextOf(row) {
    return row == null ? '' : this.IsHidden(row) ? JournalRow.Hidden : row.Text;
  }

  /**
   * The page's text as the drawer prints it: each row's label over its text, a blank line between
   * rows. A heading prints its label alone.
   */
  get Body() {
    return this.Rows.map((row) =>
      row.Label.length === 0 ? this.TextOf(row)
        : row.Text.length === 0 ? row.Label
          : row.Label + '\n' + this.TextOf(row)).join('\n\n');
  }

  toJSON() {
    return {
      $type: 'JournalPage',
      Id: this.Id,
      Tab: this.Tab,
      Title: this.Title,
      Rows: this.Rows,
      Disclosure: this.Disclosure,
      DisclosureLabel: this.DisclosureLabel,
      Body: this.Body,
    };
  }
}

/** The pages one screen hands the drawer, and which of them may be opened there. */
export class JournalBook {
  /** The drawer's heading. */
  static Heading = '手記';

  /** The close button. J opens and closes the drawer wherever it is used. */
  static CloseLabel = '閉じる（J）';

  constructor(pages, context) {
    this.Pages = pages ?? [];
    this.Context = context;
  }

  /** A line beside the close button. Only the battle has one: the hand waits while the drawer is open. */
  get ClosingHint() {
    return this.Context === JournalContext.Battle ? '閉じるとプレイできます' : '';
  }

  /** In battle the dungeon page and the memo stay shut (dungeon_exploration_v4.md §6.7). */
  CanOpen(page) {
    return page != null && (this.Context === JournalContext.Exploration || page.Tab === JournalTab.Enemy || page.Tab === JournalTab.Terms);
  }

  /** The page the drawer shows first: the first that may be opened here, or null. */
  get First() {
    return firstOrDefault(this.Pages, (p) => this.CanOpen(p));
  }

  /** The page with this id if it may be opened here, else the first one that may. */
  Find(id) {
    return firstOrDefault(this.Pages, (p) => p.Id === id && this.CanOpen(p)) ?? this.First;
  }

  /**
   * What a page's tab says: an enemy's tab carries its name, the others their kind. Every page
   * gets a tab; one that may not be opened here is shown but not pressable (battle_ui_ux_v2.md).
   */
  static TabText(page) {
    return page.Tab === JournalTab.Enemy ? page.Title : JournalBook.TabLabel(page.Tab);
  }

  static TabLabel(tab) {
    switch (tab) {
      case JournalTab.Enemy: return '敵';
      case JournalTab.Dungeon: return 'ダンジョン';
      case JournalTab.Memo: return 'メモ';
      case JournalTab.Terms: return '用語';
      default: throw new ArgumentOutOfRangeException(`Specified argument was out of the range of valid values. (Parameter 'tab')\nActual value was ${tab}.`);
    }
  }

  toJSON() {
    return {
      $type: 'JournalBook',
      Pages: this.Pages,
      Context: this.Context,
      ClosingHint: this.ClosingHint,
      First: this.First,
    };
  }
}
