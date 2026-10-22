// Port of unity-project-kit/Assets/View/Exploration/Script/EnemyIntel.cs
// The socket the enemy data plugs into (dungeon_exploration_v4.md §6.3). Which enemy stands behind a
// combat node is the spawn tables' (#102) and what the journal knows about it is #107's; until they
// land, NoEnemyIntel answers "nothing known" and the screen prints 「？」. Swapping the source in
// changes nothing on the screen's side.

/** What the screen may say about the enemy behind one combat node. */
export class NodeIntel {
  constructor(enemyName, disclosure, lines) {
    this.EnemyName = enemyName ?? '';
    /** The journal's disclosure for this enemy: 1 after an encounter, 2 after observation. */
    this.Disclosure = disclosure;
    /** Extra lines the disclosure allows, such as the tendency at level 2. */
    this.Lines = lines ?? [];
  }

  toJSON() {
    return { $type: 'NodeIntel', EnemyName: this.EnemyName, Disclosure: this.Disclosure, Lines: this.Lines };
  }
}

/**
 * interface IEnemyIntel — nothing to check at runtime; an implementation has these two methods.
 *
 * - ForNode(node: MapNode, layer: int, surveyed: bool): NodeIntel | null
 *   The enemy behind a 通常戦闘 / 精鋭 / 階層ボス node, or null when nothing is known yet.
 *   `surveyed` is true once this layer's 情報収集 node is resolved; the source then reports its
 *   surveyed enemy at disclosure 2.
 * - Pages(layer: int, surveyed: bool): JournalPage[]
 *   The journal's enemy pages for this layer. Empty when there are none yet.
 */
export const IEnemyIntel = Object.freeze({});

/** The stand-in until #102 and #107: no enemy is known, no enemy page exists. */
export class NoEnemyIntel {
  // eslint-disable-next-line no-unused-vars
  ForNode(node, layer, surveyed) { return null; }

  // eslint-disable-next-line no-unused-vars
  Pages(layer, surveyed) { return []; }
}
