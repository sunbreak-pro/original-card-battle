/**
 * EnemyEncyclopediaTab Component
 *
 * Displays all enemies in the bestiary with filtering options.
 * Supports depth filtering for enemies from all 5 dungeon levels.
 */

import React, { useState, useMemo } from "react";
import {
  createEnemyEncyclopediaEntries,
  isBossEnemy,
  getEnemyStats,
  getEnemiesByDepth,
} from "../../../constants/data/camps/EnemyEncyclopediaData";
import type { EnemyFilterOptions } from "@/types/campTypes";
import type { Depth } from "@/types/cardTypes";

const DEPTHS: Depth[] = [1, 2, 3, 4, 5];

export const EnemyEncyclopediaTab: React.FC = () => {
  const [filters, setFilters] = useState<EnemyFilterOptions>({
    depth: null,
    isBoss: null,
    searchText: "",
  });

  const allEntries = useMemo(() => createEnemyEncyclopediaEntries(), []);
  const stats = useMemo(() => getEnemyStats(), []);

  const filteredEntries = useMemo(() => {
    // If depth filter is active, get depth-specific enemies
    const depthEnemyIds =
      filters.depth !== null
        ? new Set(getEnemiesByDepth(filters.depth as Depth).map((e) => e.id))
        : null;

    return allEntries.filter((entry) => {
      const { enemy } = entry;

      // Depth filter
      if (depthEnemyIds !== null && !depthEnemyIds.has(enemy.id)) {
        return false;
      }

      // Boss filter
      if (filters.isBoss !== null) {
        const enemyIsBoss = isBossEnemy(enemy);
        if (enemyIsBoss !== filters.isBoss) {
          return false;
        }
      }

      // Search filter
      if (filters.searchText) {
        const searchLower = filters.searchText.toLowerCase();
        if (
          !enemy.name.toLowerCase().includes(searchLower) &&
          !(enemy.nameJa?.toLowerCase().includes(searchLower) ?? false) &&
          !enemy.description.toLowerCase().includes(searchLower)
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
          <span className="stat-label">Total Enemies</span>
        </div>
        <div className="stat-item">
          <span className="stat-value">{stats.normalCount}</span>
          <span className="stat-label">Normal</span>
        </div>
        <div className="stat-item">
          <span className="stat-value">{stats.bossCount}</span>
          <span className="stat-label">Boss</span>
        </div>
        <div className="stat-item">
          <span className="stat-value">{stats.avgHp}</span>
          <span className="stat-label">Avg HP</span>
        </div>
      </div>

      {/* Filters */}
      <div className="library-filters">
        <div className="filter-group">
          <span className="filter-label">Depth:</span>
          <div className="depth-filter-buttons">
            <button
              className={`depth-filter-btn ${filters.depth === null ? "active" : ""}`}
              onClick={() => setFilters({ ...filters, depth: null })}
            >
              All
            </button>
            {DEPTHS.map((d) => (
              <button
                key={d}
                className={`depth-filter-btn ${filters.depth === d ? "active" : ""}`}
                onClick={() => setFilters({ ...filters, depth: d })}
              >
                Depth {d}
              </button>
            ))}
          </div>
        </div>

        <div className="filter-group">
          <span className="filter-label">Type:</span>
          <select
            className="filter-select"
            value={
              filters.isBoss === null ? "" : filters.isBoss ? "boss" : "normal"
            }
            onChange={(e) => {
              const value = e.target.value;
              setFilters({
                ...filters,
                isBoss: value === "" ? null : value === "boss",
              });
            }}
          >
            <option value="">All</option>
            <option value="normal">Normal</option>
            <option value="boss">Boss</option>
          </select>
        </div>

        <div className="filter-group">
          <input
            type="text"
            className="search-input"
            placeholder="Search enemies..."
            value={filters.searchText}
            onChange={(e) =>
              setFilters({ ...filters, searchText: e.target.value })
            }
          />
        </div>
      </div>

      {/* Enemy Grid */}
      {filteredEntries.length === 0 ? (
        <div className="empty-state">
          <span className="empty-state-icon">{"\uD83D\uDC79"}</span>
          <span className="empty-state-text">No enemies found</span>
        </div>
      ) : (
        <div className="enemy-grid">
          {filteredEntries.map((entry) => {
            const { enemy } = entry;
            const boss = isBossEnemy(enemy);

            return (
              <div
                key={enemy.id}
                className={`enemy-card ${boss ? "boss" : ""}`}
              >
                <div className="enemy-card-header">
                  <div>
                    <h3 className="enemy-name">{enemy.name}</h3>
                    {enemy.nameJa && (
                      <p className="enemy-name-ja">{enemy.nameJa}</p>
                    )}
                  </div>
                  <span className={`enemy-badge ${boss ? "boss" : "normal"}`}>
                    {boss ? "BOSS" : "Normal"}
                  </span>
                </div>

                <div className="enemy-stats">
                  <div className="enemy-stat">
                    <span className="enemy-stat-label">HP</span>
                    <span className="enemy-stat-value">{enemy.baseMaxHp}</span>
                  </div>
                  <div className="enemy-stat">
                    <span className="enemy-stat-label">Speed</span>
                    <span className="enemy-stat-value">{enemy.baseSpeed}</span>
                  </div>
                  <div className="enemy-stat">
                    <span className="enemy-stat-label">Guard</span>
                    <span className="enemy-stat-value">
                      {enemy.startingGuard
                        ? Math.floor(enemy.baseMaxAp * 0.5)
                        : 0}
                    </span>
                  </div>
                </div>

                <p className="enemy-description">{enemy.description}</p>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
