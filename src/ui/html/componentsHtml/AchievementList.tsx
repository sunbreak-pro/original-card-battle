/**
 * AchievementList
 *
 * Displays all achievements with unlock status.
 */

import React, { useState, useMemo } from 'react';
import { ACHIEVEMENTS } from '@/constants/data/achievements/AchievementData';
import type { Achievement, AchievementCategory, AchievementRarity } from '@/types/achievementTypes';
import { usePlayer } from '@/contexts/PlayerContext';

// ============================================================================
// Types
// ============================================================================

type FilterType = 'all' | 'unlocked' | 'locked';

// ============================================================================
// Helper Functions
// ============================================================================

const CATEGORY_LABELS: Record<AchievementCategory, string> = {
  battle: '戦闘',
  exploration: '探索',
  collection: '収集',
  progression: '進行',
  special: '特殊',
};

const RARITY_LABELS: Record<AchievementRarity, string> = {
  common: '一般',
  uncommon: '珍しい',
  rare: 'レア',
  epic: 'エピック',
  legendary: '伝説',
};

const RARITY_COLORS: Record<AchievementRarity, string> = {
  common: '#9ca3af',
  uncommon: '#22c55e',
  rare: '#3b82f6',
  epic: '#a855f7',
  legendary: '#f59e0b',
};

// ============================================================================
// Achievement Card Component
// ============================================================================

interface AchievementCardProps {
  achievement: Achievement;
  isUnlocked: boolean;
  unlockDate?: number;
}

const AchievementCard: React.FC<AchievementCardProps> = ({
  achievement,
  isUnlocked,
  unlockDate,
}) => {
  const rarityColor = RARITY_COLORS[achievement.rarity];

  return (
    <div className={`achievement-card ${isUnlocked ? 'unlocked' : 'locked'}`}>
      <div className="achievement-icon" style={{ opacity: isUnlocked ? 1 : 0.4 }}>
        {achievement.icon}
      </div>
      <div className="achievement-info">
        <div className="achievement-header">
          <span className="achievement-name">{achievement.name}</span>
          <span
            className="achievement-rarity"
            style={{ color: rarityColor, borderColor: rarityColor }}
          >
            {RARITY_LABELS[achievement.rarity]}
          </span>
        </div>
        <p className="achievement-description">
          {isUnlocked || !achievement.hidden
            ? achievement.description
            : '???'}
        </p>
        {isUnlocked && unlockDate && (
          <p className="achievement-unlock-date">
            {new Date(unlockDate).toLocaleDateString('ja-JP')} に解除
          </p>
        )}
        {!isUnlocked && achievement.condition && !achievement.hidden && (
          <p className="achievement-condition">
            条件: {achievement.condition}
          </p>
        )}
      </div>
      {isUnlocked && <div className="achievement-check">✓</div>}
    </div>
  );
};

// ============================================================================
// Achievement List Component
// ============================================================================

export const AchievementList: React.FC = () => {
  const { playerData } = usePlayer();
  const [filter, setFilter] = useState<FilterType>('all');
  const [categoryFilter, setCategoryFilter] = useState<AchievementCategory | 'all'>('all');

  // Get unlocked achievement IDs from player data
  const unlockedIds = useMemo(() => {
    return new Set(playerData.progression.completedAchievements || []);
  }, [playerData.progression.completedAchievements]);

  // Filter achievements
  const filteredAchievements = useMemo(() => {
    return ACHIEVEMENTS.filter((achievement) => {
      const isUnlocked = unlockedIds.has(achievement.id);

      // Status filter
      if (filter === 'unlocked' && !isUnlocked) return false;
      if (filter === 'locked' && isUnlocked) return false;

      // Category filter
      if (categoryFilter !== 'all' && achievement.category !== categoryFilter) return false;

      return true;
    });
  }, [filter, categoryFilter, unlockedIds]);

  // Calculate stats
  const totalCount = ACHIEVEMENTS.length;
  const unlockedCount = unlockedIds.size;
  const progressPercent = Math.round((unlockedCount / totalCount) * 100);

  return (
    <div className="achievement-list">
      {/* Progress Bar */}
      <div className="achievement-progress">
        <div className="achievement-progress-text">
          <span>実績進捗</span>
          <span>{unlockedCount} / {totalCount} ({progressPercent}%)</span>
        </div>
        <div className="achievement-progress-bar">
          <div
            className="achievement-progress-fill"
            style={{ width: `${progressPercent}%` }}
          />
        </div>
      </div>

      {/* Filters */}
      <div className="achievement-filters">
        <div className="filter-group">
          <label>状態:</label>
          <select
            value={filter}
            onChange={(e) => setFilter(e.target.value as FilterType)}
            className="filter-select"
          >
            <option value="all">すべて</option>
            <option value="unlocked">解除済み</option>
            <option value="locked">未解除</option>
          </select>
        </div>
        <div className="filter-group">
          <label>カテゴリ:</label>
          <select
            value={categoryFilter}
            onChange={(e) => setCategoryFilter(e.target.value as AchievementCategory | 'all')}
            className="filter-select"
          >
            <option value="all">すべて</option>
            {Object.entries(CATEGORY_LABELS).map(([key, label]) => (
              <option key={key} value={key}>{label}</option>
            ))}
          </select>
        </div>
      </div>

      {/* Achievement Grid */}
      <div className="achievement-grid">
        {filteredAchievements.map((achievement) => (
          <AchievementCard
            key={achievement.id}
            achievement={achievement}
            isUnlocked={unlockedIds.has(achievement.id)}
            // TODO: Get actual unlock date from player data
            unlockDate={unlockedIds.has(achievement.id) ? Date.now() : undefined}
          />
        ))}
      </div>

      {filteredAchievements.length === 0 && (
        <div className="achievement-empty">
          該当する実績がありません
        </div>
      )}
    </div>
  );
};

export default AchievementList;
