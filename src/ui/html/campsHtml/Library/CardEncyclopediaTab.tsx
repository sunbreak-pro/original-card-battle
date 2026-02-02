/**
 * CardEncyclopediaTab Component
 *
 * Displays all cards in the encyclopedia with filtering options.
 * Supports tag, element, class, cost range, search, and unknown toggle filters.
 * Unknown (locked) cards display as "???" silhouettes.
 */

import React, { useState, useMemo } from "react";
import {
  createCardEncyclopediaEntries,
  getCardStats,
} from "@/constants/data/camps/CardEncyclopediaData";
import type { CardFilterOptions } from "@/types/campTypes";
import type { ElementType } from "@/types/characterTypes";
import type { Card, CardTag } from "@/types/cardTypes";
import {
  ELEMENT_LABEL_MAP,
  CARD_TAG_LABEL_MAP,
  CARD_TAG_COLOR_MAP,
} from "@/constants/cardConstants";
import { CardComponent } from "../../cardHtml/CardComponent";
import { CardDerivationTree } from "./CardDerivationTree";

export const CardEncyclopediaTab: React.FC = () => {
  const [filters, setFilters] = useState<CardFilterOptions>({
    element: null,
    characterClass: null,
    searchText: "",
    tag: null,
    costMin: null,
    costMax: null,
    showUnknown: true,
  });
  const [selectedCard, setSelectedCard] = useState<Card | null>(null);

  // For now, all cards are unlocked (no persistent unlock tracking yet)
  // When unlock tracking is added, pass the set here
  const unlockedCardTypeIds: Set<string> | undefined = undefined;

  const allEntries = useMemo(
    () => createCardEncyclopediaEntries(unlockedCardTypeIds),
    [unlockedCardTypeIds],
  );
  const stats = useMemo(
    () => getCardStats(unlockedCardTypeIds),
    [unlockedCardTypeIds],
  );

  const filteredEntries = useMemo(() => {
    return allEntries.filter((entry) => {
      const { card, isUnlocked } = entry;

      // Unknown toggle: hide unknown cards if showUnknown is false
      if (!filters.showUnknown && !isUnlocked) {
        return false;
      }

      // Tag filter
      if (filters.tag && !card.tags.includes(filters.tag)) {
        return false;
      }

      // Element filter
      if (filters.element && !card.element.includes(filters.element)) {
        return false;
      }

      // Class filter
      if (
        filters.characterClass &&
        card.characterClass !== filters.characterClass &&
        card.characterClass !== "common"
      ) {
        return false;
      }

      // Cost range filter
      if (filters.costMin !== null && card.cost < filters.costMin) {
        return false;
      }
      if (filters.costMax !== null && card.cost > filters.costMax) {
        return false;
      }

      // Search filter (only for unlocked cards)
      if (filters.searchText && isUnlocked) {
        const searchLower = filters.searchText.toLowerCase();
        if (
          !card.name.toLowerCase().includes(searchLower) &&
          !card.description.toLowerCase().includes(searchLower)
        ) {
          return false;
        }
      }
      // Hide unknown cards that don't match search (can't search unknown)
      if (filters.searchText && !isUnlocked) {
        return false;
      }

      return true;
    });
  }, [allEntries, filters]);

  const tagOptions: CardTag[] = ["attack", "guard", "skill", "stance"];

  return (
    <div className="library-content">
      {/* Stats Bar with unlock ratio */}
      <div className="library-stats">
        <div className="stat-item">
          <span className="stat-value">
            {stats.unlocked}/{stats.total}
          </span>
          <span className="stat-label">解放済み</span>
        </div>
        <div className="stat-item">
          <span className="stat-value">{stats.byClass?.swordsman || 0}</span>
          <span className="stat-label">剣士</span>
        </div>
        <div className="stat-item">
          <span className="stat-value">{stats.byClass?.mage || 0}</span>
          <span className="stat-label">魔術師</span>
        </div>
        <div className="stat-item">
          <span className="stat-value">{stats.byClass?.summoner || 0}</span>
          <span className="stat-label">召喚師</span>
        </div>
        {tagOptions.map((tag) => (
          <div key={tag} className="stat-item">
            <span
              className="stat-value"
              style={{ color: CARD_TAG_COLOR_MAP[tag] }}
            >
              {stats.byTag?.[tag] || 0}
            </span>
            <span className="stat-label">{CARD_TAG_LABEL_MAP[tag]}</span>
          </div>
        ))}
      </div>

      {/* Filters */}
      <div className="library-filters">
        {/* Tag filter */}
        <div className="filter-group">
          <span className="filter-label">タグ:</span>
          <select
            className="filter-select"
            value={filters.tag || ""}
            onChange={(e) =>
              setFilters({
                ...filters,
                tag: (e.target.value || null) as CardTag | null,
              })
            }
          >
            <option value="">全て</option>
            {tagOptions.map((tag) => (
              <option key={tag} value={tag}>
                {CARD_TAG_LABEL_MAP[tag]}
              </option>
            ))}
          </select>
        </div>

        {/* Class filter */}
        <div className="filter-group">
          <span className="filter-label">クラス:</span>
          <select
            className="filter-select"
            value={filters.characterClass || ""}
            onChange={(e) =>
              setFilters({ ...filters, characterClass: e.target.value || null })
            }
          >
            <option value="">全て</option>
            <option value="swordsman">剣士</option>
            <option value="mage">魔術師</option>
            <option value="summoner">召喚師</option>
          </select>
        </div>

        {/* Element filter */}
        <div className="filter-group">
          <span className="filter-label">属性:</span>
          <select
            className="filter-select"
            value={filters.element || ""}
            onChange={(e) =>
              setFilters({
                ...filters,
                element: (e.target.value || null) as ElementType | null,
              })
            }
          >
            <option value="">全て</option>
            <option value="physics">{ELEMENT_LABEL_MAP.physics}</option>
            <option value="fire">{ELEMENT_LABEL_MAP.fire}</option>
            <option value="ice">{ELEMENT_LABEL_MAP.ice}</option>
            <option value="lightning">{ELEMENT_LABEL_MAP.lightning}</option>
            <option value="dark">{ELEMENT_LABEL_MAP.dark}</option>
            <option value="light">{ELEMENT_LABEL_MAP.light}</option>
            <option value="guard">{ELEMENT_LABEL_MAP.guard}</option>
            <option value="summon">{ELEMENT_LABEL_MAP.summon}</option>
            <option value="enhance">{ELEMENT_LABEL_MAP.enhance}</option>
            <option value="sacrifice">{ELEMENT_LABEL_MAP.sacrifice}</option>
            <option value="buff">{ELEMENT_LABEL_MAP.buff}</option>
            <option value="debuff">{ELEMENT_LABEL_MAP.debuff}</option>
            <option value="heal">{ELEMENT_LABEL_MAP.heal}</option>
          </select>
        </div>

        {/* Cost range */}
        <div className="filter-group cost-range-group">
          <span className="filter-label">コスト:</span>
          <input
            type="number"
            className="cost-range-input"
            placeholder="最小"
            min={0}
            max={99}
            value={filters.costMin ?? ""}
            onChange={(e) =>
              setFilters({
                ...filters,
                costMin: e.target.value ? Number(e.target.value) : null,
              })
            }
          />
          <span className="filter-label">~</span>
          <input
            type="number"
            className="cost-range-input"
            placeholder="最大"
            min={0}
            max={99}
            value={filters.costMax ?? ""}
            onChange={(e) =>
              setFilters({
                ...filters,
                costMax: e.target.value ? Number(e.target.value) : null,
              })
            }
          />
        </div>

        {/* Search */}
        <div className="filter-group">
          <input
            type="text"
            className="search-input"
            placeholder="カード名で検索..."
            value={filters.searchText}
            onChange={(e) =>
              setFilters({ ...filters, searchText: e.target.value })
            }
          />
        </div>

        {/* Unknown toggle */}
        <div className="filter-group unknown-toggle">
          <label className="toggle-label">
            <input
              type="checkbox"
              checked={filters.showUnknown}
              onChange={(e) =>
                setFilters({ ...filters, showUnknown: e.target.checked })
              }
            />
            <span>未解放を表示</span>
          </label>
        </div>
      </div>

      {/* Derivation Tree (shown when card selected) */}
      {selectedCard && (
        <CardDerivationTree
          card={selectedCard}
          onClose={() => setSelectedCard(null)}
          unlockedCardTypeIds={unlockedCardTypeIds}
        />
      )}

      {/* Card Grid */}
      {filteredEntries.length === 0 ? (
        <div className="empty-state">
          <span className="empty-state-icon">{"\uD83C\uDFB4"}</span>
          <span className="empty-state-text">カードが見つかりません</span>
        </div>
      ) : (
        <div className="card-grid">
          {filteredEntries.map((entry) => (
            <div
              key={entry.card.id}
              className={`encyclopedia-card-wrapper${!entry.isUnlocked ? " card-unknown" : ""}`}
              onClick={() => entry.isUnlocked && setSelectedCard(entry.card)}
            >
              {entry.isUnlocked ? (
                <CardComponent
                  card={entry.card}
                  depth={1}
                  isPlayable={true}
                />
              ) : (
                <div className="unknown-card-placeholder">
                  <div className="unknown-card-icon">?</div>
                  <div className="unknown-card-text">未解放</div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
