/**
 * Achievement Data
 *
 * All achievement definitions for the game.
 */

import type { Achievement } from '@/types/achievementTypes';

// ============================================================================
// Achievement Definitions
// ============================================================================

export const ACHIEVEMENTS: Achievement[] = [
  // -------------------------------------------------------------------------
  // Battle Achievements
  // -------------------------------------------------------------------------
  {
    id: 'first_victory',
    name: '初陣',
    description: '初めての戦闘に勝利した',
    icon: '⚔️',
    category: 'battle',
    rarity: 'common',
    condition: '戦闘に1回勝利する',
  },
  {
    id: 'battle_10',
    name: '戦士の道',
    description: '10回の戦闘に勝利した',
    icon: '🗡️',
    category: 'battle',
    rarity: 'common',
    condition: '戦闘に10回勝利する',
  },
  {
    id: 'battle_50',
    name: '熟練の戦士',
    description: '50回の戦闘に勝利した',
    icon: '🛡️',
    category: 'battle',
    rarity: 'uncommon',
    condition: '戦闘に50回勝利する',
  },
  {
    id: 'battle_100',
    name: '百戦錬磨',
    description: '100回の戦闘に勝利した',
    icon: '🏆',
    category: 'battle',
    rarity: 'rare',
    condition: '戦闘に100回勝利する',
  },
  {
    id: 'no_damage_victory',
    name: '完璧な勝利',
    description: 'ダメージを受けずに戦闘に勝利した',
    icon: '✨',
    category: 'battle',
    rarity: 'rare',
    condition: 'ノーダメージで勝利する',
  },
  {
    id: 'boss_slayer',
    name: 'ボススレイヤー',
    description: '初めてのボスを討伐した',
    icon: '👹',
    category: 'battle',
    rarity: 'uncommon',
    condition: 'ボスを倒す',
  },

  // -------------------------------------------------------------------------
  // Exploration Achievements
  // -------------------------------------------------------------------------
  {
    id: 'first_exploration',
    name: '冒険の始まり',
    description: '初めてダンジョンに挑んだ',
    icon: '🚪',
    category: 'exploration',
    rarity: 'common',
    condition: 'ダンジョンに入る',
  },
  {
    id: 'depth_2',
    name: '深淵への一歩',
    description: '深度2に到達した',
    icon: '⬇️',
    category: 'exploration',
    rarity: 'common',
    condition: '深度2に到達する',
  },
  {
    id: 'depth_3',
    name: '闇の探索者',
    description: '深度3に到達した',
    icon: '🌑',
    category: 'exploration',
    rarity: 'uncommon',
    condition: '深度3に到達する',
  },
  {
    id: 'depth_5',
    name: '深淵の征服者',
    description: '深度5に到達した',
    icon: '🕳️',
    category: 'exploration',
    rarity: 'rare',
    condition: '深度5に到達する',
  },
  {
    id: 'safe_return',
    name: '生還者',
    description: '探索から無事に帰還した',
    icon: '🏠',
    category: 'exploration',
    rarity: 'common',
    condition: '探索から帰還する',
  },

  // -------------------------------------------------------------------------
  // Collection Achievements
  // -------------------------------------------------------------------------
  {
    id: 'card_10',
    name: 'カードコレクター',
    description: '10種類のカードを入手した',
    icon: '🃏',
    category: 'collection',
    rarity: 'common',
    condition: '10種類のカードを獲得する',
  },
  {
    id: 'card_30',
    name: 'デッキビルダー',
    description: '30種類のカードを入手した',
    icon: '📚',
    category: 'collection',
    rarity: 'uncommon',
    condition: '30種類のカードを獲得する',
  },
  {
    id: 'gold_1000',
    name: '金持ち',
    description: '累計1000ゴールドを獲得した',
    icon: '💰',
    category: 'collection',
    rarity: 'common',
    condition: '累計1000ゴールドを獲得する',
  },
  {
    id: 'gold_10000',
    name: '富豪',
    description: '累計10000ゴールドを獲得した',
    icon: '💎',
    category: 'collection',
    rarity: 'rare',
    condition: '累計10000ゴールドを獲得する',
  },

  // -------------------------------------------------------------------------
  // Progression Achievements
  // -------------------------------------------------------------------------
  {
    id: 'class_d',
    name: 'Dランク冒険者',
    description: 'ギルドランクDに昇格した',
    icon: '🥉',
    category: 'progression',
    rarity: 'common',
    condition: 'ランクDに昇格する',
  },
  {
    id: 'class_c',
    name: 'Cランク冒険者',
    description: 'ギルドランクCに昇格した',
    icon: '🥈',
    category: 'progression',
    rarity: 'uncommon',
    condition: 'ランクCに昇格する',
  },
  {
    id: 'class_b',
    name: 'Bランク冒険者',
    description: 'ギルドランクBに昇格した',
    icon: '🥇',
    category: 'progression',
    rarity: 'rare',
    condition: 'ランクBに昇格する',
  },
  {
    id: 'class_a',
    name: 'Aランク冒険者',
    description: 'ギルドランクAに昇格した',
    icon: '🏅',
    category: 'progression',
    rarity: 'epic',
    condition: 'ランクAに昇格する',
  },
  {
    id: 'sanctuary_first',
    name: '魂の目覚め',
    description: '聖域で初めてノードを解放した',
    icon: '🌟',
    category: 'progression',
    rarity: 'common',
    condition: '聖域でノードを解放する',
  },

  // -------------------------------------------------------------------------
  // Special Achievements
  // -------------------------------------------------------------------------
  {
    id: 'first_death',
    name: '敗北を知る者',
    description: '初めて戦闘で倒れた',
    icon: '💀',
    category: 'special',
    rarity: 'common',
    condition: '戦闘で敗北する',
  },
  {
    id: 'comeback_victory',
    name: '逆転の勝利',
    description: 'HP10%以下から勝利した',
    icon: '🔥',
    category: 'special',
    rarity: 'rare',
    condition: 'HP10%以下で勝利する',
  },
  {
    id: 'one_turn_kill',
    name: '一撃必殺',
    description: '1ターンで敵を倒した',
    icon: '⚡',
    category: 'special',
    rarity: 'rare',
    condition: '1ターンで敵を倒す',
  },
];
