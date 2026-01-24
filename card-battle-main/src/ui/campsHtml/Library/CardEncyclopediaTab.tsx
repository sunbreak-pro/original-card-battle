/**
 * CardEncyclopediaTab Component
 *
 * Displays all cards in the encyclopedia with filtering options.
 */

import React, { useState, useMemo } from "react";
import {
  createCardEncyclopediaEntries,
  getCardStats,
} from "../../../domain/camps/data/CardEncyclopediaData";
import type { CardFilterOptions } from "../../../domain/camps/types/LibraryTypes";
import { CardComponent } from "../../cardHtml/CardComponent";

export const CardEncyclopediaTab: React.FC = () => {
  const [filters, setFilters] = useState<CardFilterOptions>({
    rarity: null,
    category: null,
    characterClass: null,
    searchText: "",
  });

  const allEntries = useMemo(() => createCardEncyclopediaEntries(), []);
  const stats = useMemo(() => getCardStats(), []);

  const filteredEntries = useMemo(() => {
    return allEntries.filter((entry) => {
      const { card } = entry;

      // Rarity filter
      if (filters.rarity && card.rarity !== filters.rarity) {
        return false;
      }

      // Category filter
      if (filters.category && card.category !== filters.category) {
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
          <span className="stat-value">{stats.byRarity.common || 0}</span>
          <span className="stat-label">Common</span>
        </div>
        <div className="stat-item">
          <span className="stat-value">{stats.byRarity.rare || 0}</span>
          <span className="stat-label">Rare</span>
        </div>
        <div className="stat-item">
          <span className="stat-value">{stats.byRarity.epic || 0}</span>
          <span className="stat-label">Epic</span>
        </div>
        <div className="stat-item">
          <span className="stat-value">{stats.byRarity.legend || 0}</span>
          <span className="stat-label">Legend</span>
        </div>
      </div>

      {/* Filters */}
      <div className="library-filters">
        <div className="filter-group">
          <span className="filter-label">Rarity:</span>
          <select
            className="filter-select"
            value={filters.rarity || ""}
            onChange={(e) =>
              setFilters({ ...filters, rarity: e.target.value || null })
            }
          >
            <option value="">All</option>
            <option value="common">Common</option>
            <option value="rare">Rare</option>
            <option value="epic">Epic</option>
            <option value="legend">Legend</option>
          </select>
        </div>

        <div className="filter-group">
          <span className="filter-label">Category:</span>
          <select
            className="filter-select"
            value={filters.category || ""}
            onChange={(e) =>
              setFilters({ ...filters, category: e.target.value || null })
            }
          >
            <option value="">All</option>
            <option value="atk">Attack</option>
            <option value="def">Defense</option>
            <option value="buff">Buff</option>
            <option value="debuff">Debuff</option>
            <option value="heal">Heal</option>
            <option value="swordEnergy">Sword Energy</option>
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

      {/* Card Grid */}
      {filteredEntries.length === 0 ? (
        <div className="empty-state">
          <span className="empty-state-icon">{"\uD83C\uDFB4"}</span>
          <span className="empty-state-text">No cards found</span>
        </div>
      ) : (
        <div className="card-grid">
          {filteredEntries.map((entry) => (
            <div key={entry.card.id} className="encyclopedia-card-wrapper">
              <CardComponent card={entry.card} depth={1} isPlayable={true} />
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
