/**
 * GameTipsTab Component
 *
 * Displays game tips organized by category.
 */

import React, { useState, useMemo } from "react";
import {
  getAllTips,
  getTipStats,
  CATEGORY_NAMES,
} from "@/constants/data/camps/GameTipsData";
import type { TipCategory } from '@/types/campTypes';

/**
 * Category icons
 */
const CATEGORY_ICONS: Record<TipCategory, string> = {
  battle: "\u2694\uFE0F",
  cards: "\uD83C\uDFB4",
  exploration: "\uD83D\uDDFA\uFE0F",
  class: "\uD83D\uDC64",
  general: "\uD83D\uDCA1",
};

export const GameTipsTab: React.FC = () => {
  const [expandedTip, setExpandedTip] = useState<string | null>(null);
  const [expandedCategories, setExpandedCategories] = useState<
    Set<TipCategory>
  >(new Set(["battle", "cards", "exploration", "class", "general"]));

  const allTips = useMemo(() => getAllTips(), []);
  const stats = useMemo(() => getTipStats(), []);

  // Group tips by category
  const tipsByCategory = useMemo(() => {
    const grouped: Record<TipCategory, typeof allTips> = {
      battle: [],
      cards: [],
      exploration: [],
      class: [],
      general: [],
    };

    allTips.forEach((tip) => {
      grouped[tip.category].push(tip);
    });

    return grouped;
  }, [allTips]);

  const toggleCategory = (category: TipCategory) => {
    const newSet = new Set(expandedCategories);
    if (newSet.has(category)) {
      newSet.delete(category);
    } else {
      newSet.add(category);
    }
    setExpandedCategories(newSet);
  };

  const toggleTip = (tipId: string) => {
    setExpandedTip(expandedTip === tipId ? null : tipId);
  };

  const categories: TipCategory[] = [
    "battle",
    "cards",
    "exploration",
    "class",
    "general",
  ];

  return (
    <div className="library-content">
      {/* Stats Bar */}
      <div className="library-stats">
        <div className="stat-item">
          <span className="stat-value">{stats.total}</span>
          <span className="stat-label">Total Tips</span>
        </div>
        {categories.map((category) => (
          <div key={category} className="stat-item">
            <span className="stat-value">
              {stats.byCategory[category] || 0}
            </span>
            <span className="stat-label">{CATEGORY_NAMES[category]}</span>
          </div>
        ))}
      </div>

      {/* Tips by Category */}
      <div className="tips-container">
        {categories.map((category) => {
          const tips = tipsByCategory[category];
          const isExpanded = expandedCategories.has(category);

          if (tips.length === 0) return null;

          return (
            <div key={category} className="tips-category">
              <div
                className="tips-category-header"
                onClick={() => toggleCategory(category)}
              >
                <span className="tips-category-icon">
                  {CATEGORY_ICONS[category]}
                </span>
                <span className="tips-category-title">
                  {CATEGORY_NAMES[category]}
                </span>
                <span className="tips-category-count">{tips.length} tips</span>
                <span>{isExpanded ? "\u25BC" : "\u25B6"}</span>
              </div>

              {isExpanded && (
                <div className="tips-list">
                  {tips.map((tip) => (
                    <div
                      key={tip.id}
                      className={`tip-card ${
                        expandedTip === tip.id ? "expanded" : ""
                      }`}
                      onClick={() => toggleTip(tip.id)}
                    >
                      <div className="tip-title">{tip.title}</div>
                      <div className="tip-content">{tip.content}</div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
};
