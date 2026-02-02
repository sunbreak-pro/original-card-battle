/**
 * CardEncyclopediaTab Component
 *
 * Displays all cards in the encyclopedia with filtering options.
 * Supports Swordsman, Mage, and Summoner cards with class filtering.
 */

import React, { useState, useMemo } from "react";
import {
  createCardEncyclopediaEntries,
  getCardStats,
} from "@/constants/data/camps/CardEncyclopediaData";
import type { CardFilterOptions } from "@/types/campTypes";
import type { ElementType } from "@/types/characterTypes";
import { ELEMENT_LABEL_MAP } from "@/constants/cardConstants";
import { CardComponent } from "../../cardHtml/CardComponent";
import { CardDerivationTree } from "./CardDerivationTree";
import type { Card } from "@/types/cardTypes";

export const CardEncyclopediaTab: React.FC = () => {
  const [filters, setFilters] = useState<CardFilterOptions>({
    element: null,
    characterClass: null,
    searchText: "",
  });
  const [selectedCard, setSelectedCard] = useState<Card | null>(null);

  const allEntries = useMemo(() => createCardEncyclopediaEntries(), []);
  const stats = useMemo(() => getCardStats(), []);

  const filteredEntries = useMemo(() => {
    return allEntries.filter((entry) => {
      const { card } = entry;

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

      // Search filter
      if (filters.searchText) {
        const searchLower = filters.searchText.toLowerCase();
        if (
          !card.name.toLowerCase().includes(searchLower) &&
          !card.description.toLowerCase().includes(searchLower)
        ) {
          return false;
        }
      }

      return true;
    });
  }, [allEntries, filters]);

  return (
    <div className="library-content">
      {/* Stats Bar */}
      <div className="library-stats">
        <div className="stat-item">
          <span className="stat-value">{stats.total}</span>
          <span className="stat-label">Total Cards</span>
        </div>
        <div className="stat-item">
          <span className="stat-value">{stats.byClass?.swordsman || 0}</span>
          <span className="stat-label">Swordsman</span>
        </div>
        <div className="stat-item">
          <span className="stat-value">{stats.byClass?.mage || 0}</span>
          <span className="stat-label">Mage</span>
        </div>
        <div className="stat-item">
          <span className="stat-value">{stats.byClass?.summoner || 0}</span>
          <span className="stat-label">Summoner</span>
        </div>
      </div>

      {/* Filters */}
      <div className="library-filters">
        <div className="filter-group">
          <span className="filter-label">Class:</span>
          <select
            className="filter-select"
            value={filters.characterClass || ""}
            onChange={(e) =>
              setFilters({ ...filters, characterClass: e.target.value || null })
            }
          >
            <option value="">All Classes</option>
            <option value="swordsman">Swordsman</option>
            <option value="mage">Mage</option>
            <option value="summoner">Summoner</option>
          </select>
        </div>

        <div className="filter-group">
          <span className="filter-label">Element:</span>
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
            <option value="">All</option>
            <option value="attack">{ELEMENT_LABEL_MAP.attack}</option>
            <option value="guard">{ELEMENT_LABEL_MAP.guard}</option>
            <option value="buff">{ELEMENT_LABEL_MAP.buff}</option>
            <option value="debuff">{ELEMENT_LABEL_MAP.debuff}</option>
            <option value="heal">{ELEMENT_LABEL_MAP.heal}</option>
            <option value="classAbility">{ELEMENT_LABEL_MAP.classAbility}</option>
            <option value="physics">{ELEMENT_LABEL_MAP.physics}</option>
            <option value="fire">{ELEMENT_LABEL_MAP.fire}</option>
            <option value="ice">{ELEMENT_LABEL_MAP.ice}</option>
            <option value="lightning">{ELEMENT_LABEL_MAP.lightning}</option>
            <option value="dark">{ELEMENT_LABEL_MAP.dark}</option>
            <option value="light">{ELEMENT_LABEL_MAP.light}</option>
            <option value="summon">{ELEMENT_LABEL_MAP.summon}</option>
            <option value="enhance">{ELEMENT_LABEL_MAP.enhance}</option>
            <option value="sacrifice">{ELEMENT_LABEL_MAP.sacrifice}</option>
          </select>
        </div>

        <div className="filter-group">
          <input
            type="text"
            className="search-input"
            placeholder="Search cards..."
            value={filters.searchText}
            onChange={(e) =>
              setFilters({ ...filters, searchText: e.target.value })
            }
          />
        </div>
      </div>

      {/* Derivation Tree (shown when card selected) */}
      {selectedCard && (
        <CardDerivationTree
          card={selectedCard}
          onClose={() => setSelectedCard(null)}
        />
      )}

      {/* Card Grid */}
      {filteredEntries.length === 0 ? (
        <div className="empty-state">
          <span className="empty-state-icon">{"\uD83C\uDFB4"}</span>
          <span className="empty-state-text">No cards found</span>
        </div>
      ) : (
        <div className="card-grid">
          {filteredEntries.map((entry) => (
            <div
              key={entry.card.id}
              className="encyclopedia-card-wrapper"
              onClick={() => setSelectedCard(entry.card)}
            >
              <CardComponent card={entry.card} depth={1} isPlayable={true} />
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
