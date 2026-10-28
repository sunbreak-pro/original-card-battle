/**
 * Event Encyclopedia Data for Journal
 *
 * Provides event/discovery tracking for the encyclopedia.
 * Events represent significant game moments and discoveries.
 */

/**
 * Event category types
 */
export type EventCategory =
  | "story"
  | "achievement"
  | "discovery"
  | "milestone"
  | "secret";

/**
 * Event encyclopedia entry
 */
export interface EventEntry {
  id: string;
  category: EventCategory;
  title: string;
  titleJa: string;
  description: string;
  descriptionJa: string;
  icon: string;
  isUnlocked: boolean;
  unlockedAt?: string;
}

/**
 * All predefined events in the game
 */
export const GAME_EVENTS: EventEntry[] = [
  // Story events
  {
    id: "event_first_battle",
    category: "story",
    title: "First Battle",
    titleJa: "初陣",
    description: "Won your first battle in the dungeon.",
    descriptionJa: "ダンジョンでの最初の戦闘に勝利した。",
    icon: "⚔️",
    isUnlocked: true,
  },
  {
    id: "event_first_depth_clear",
    category: "story",
    title: "Depth 1 Clear",
    titleJa: "深度1クリア",
    description: "Cleared the first depth of the dungeon.",
    descriptionJa: "ダンジョンの深度1をクリアした。",
    icon: "🏆",
    isUnlocked: true,
  },
  {
    id: "event_guild_promotion",
    category: "story",
    title: "Guild Promotion",
    titleJa: "ギルド昇格",
    description: "Received your first guild promotion.",
    descriptionJa: "初めてのギルド昇格を達成した。",
    icon: "📜",
    isUnlocked: true,
  },

  // Achievement events
  {
    id: "event_100_battles",
    category: "achievement",
    title: "Seasoned Warrior",
    titleJa: "歴戦の戦士",
    description: "Completed 100 battles.",
    descriptionJa: "100回の戦闘を完了した。",
    icon: "🎖️",
    isUnlocked: false,
  },
  {
    id: "event_no_damage_boss",
    category: "achievement",
    title: "Perfect Victory",
    titleJa: "完全勝利",
    description: "Defeated a boss without taking damage.",
    descriptionJa: "ボスをノーダメージで撃破した。",
    icon: "⭐",
    isUnlocked: false,
  },
  {
    id: "event_full_mastery",
    category: "achievement",
    title: "Card Master",
    titleJa: "カードマスター",
    description: "Mastered a card to maximum level.",
    descriptionJa: "カードを最大レベルまで熟練させた。",
    icon: "🃏",
    isUnlocked: false,
  },

  // Discovery events
  {
    id: "event_rare_card_drop",
    category: "discovery",
    title: "Rare Find",
    titleJa: "レアな発見",
    description: "Found a rare card in the dungeon.",
    descriptionJa: "ダンジョンでレアカードを発見した。",
    icon: "💎",
    isUnlocked: false,
  },
  {
    id: "event_legendary_equipment",
    category: "discovery",
    title: "Legendary Equipment",
    titleJa: "伝説の装備",
    description: "Obtained a legendary piece of equipment.",
    descriptionJa: "伝説級の装備を入手した。",
    icon: "👑",
    isUnlocked: false,
  },
  {
    id: "event_hidden_room",
    category: "discovery",
    title: "Hidden Room",
    titleJa: "隠し部屋",
    description: "Discovered a hidden room in the dungeon.",
    descriptionJa: "ダンジョンで隠し部屋を発見した。",
    icon: "🚪",
    isUnlocked: false,
  },

  // Milestone events
  {
    id: "event_1000_gold",
    category: "milestone",
    title: "Wealthy Adventurer",
    titleJa: "裕福な冒険者",
    description: "Accumulated 1000 gold.",
    descriptionJa: "1000ゴールドを蓄えた。",
    icon: "💰",
    isUnlocked: false,
  },
  {
    id: "event_depth_5_reach",
    category: "milestone",
    title: "Deep Explorer",
    titleJa: "深淵探検家",
    description: "Reached depth 5 of the dungeon.",
    descriptionJa: "ダンジョンの深度5に到達した。",
    icon: "🕳️",
    isUnlocked: false,
  },
  {
    id: "event_full_sanctuary",
    category: "milestone",
    title: "Soul Collector",
    titleJa: "魂の収集者",
    description: "Unlocked all nodes in the Sanctuary.",
    descriptionJa: "聖域の全ノードを解放した。",
    icon: "🔮",
    isUnlocked: false,
  },

  // Secret events
  {
    id: "event_secret_boss",
    category: "secret",
    title: "???",
    titleJa: "？？？",
    description: "Discovered a hidden secret.",
    descriptionJa: "隠された秘密を発見した。",
    icon: "❓",
    isUnlocked: false,
  },
];

/**
 * Get all events
 */
export function getAllEvents(): EventEntry[] {
  return GAME_EVENTS;
}

/**
 * Get event statistics
 */
export function getEventStats(discoveredIds?: Set<string>): {
  total: number;
  discovered: number;
  byCategory: Record<string, { total: number; discovered: number }>;
} {
  const byCategory: Record<string, { total: number; discovered: number }> = {};

  let totalDiscovered = 0;

  GAME_EVENTS.forEach((event) => {
    if (!byCategory[event.category]) {
      byCategory[event.category] = { total: 0, discovered: 0 };
    }
    byCategory[event.category].total++;

    const isDiscovered = !discoveredIds || discoveredIds.has(event.id);
    if (isDiscovered) {
      byCategory[event.category].discovered++;
      totalDiscovered++;
    }
  });

  return {
    total: GAME_EVENTS.length,
    discovered: totalDiscovered,
    byCategory,
  };
}

/**
 * Event category display names (Japanese)
 */
export const EVENT_CATEGORY_NAMES: Record<EventCategory, string> = {
  story: "ストーリー",
  achievement: "実績",
  discovery: "発見",
  milestone: "マイルストーン",
  secret: "秘密",
};
