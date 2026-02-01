/**
 * FacilityHeader - Unified header component for all facility screens
 * Displays resources (Gold, Magic Stones by type)
 *
 * Variants:
 * - "default": Standard header with gold and magic stones + facility nav dropdown
 * - "basecamp": Adds lives display (hearts), no nav dropdown
 */

import React, { useState, useEffect, useRef, useCallback } from "react";
import { usePlayer } from "@/contexts/PlayerContext";
import { useResources } from "@/contexts/ResourceContext";
import { useGameState } from "@/contexts/GameStateContext";
import { HEADER_ICONS } from "@/constants/uiConstants";
import { FACILITY_NAV_ITEMS } from "@/constants/campConstants";
import "../../css/components/FacilityHeader.css";

interface FacilityHeaderProps {
  title: string;
  variant?: "default" | "basecamp";
}

export const FacilityHeader: React.FC<FacilityHeaderProps> = ({
  title,
  variant = "default",
}) => {
  const { runtimeState } = usePlayer();
  const { getTotalGold, resources } = useResources();
  const { gameState, navigateTo } = useGameState();
  const [navOpen, setNavOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const handleCloseNav = useCallback((e: MouseEvent) => {
    if (
      dropdownRef.current &&
      !dropdownRef.current.contains(e.target as Node)
    ) {
      setNavOpen(false);
    }
  }, []);

  useEffect(() => {
    if (navOpen) {
      document.addEventListener("click", handleCloseNav);
      return () => document.removeEventListener("click", handleCloseNav);
    }
  }, [navOpen, handleCloseNav]);

  // Get magic stones (both baseCamp and exploration combined for display)
  const magicStones = {
    small:
      resources.magicStones.baseCamp.small +
      resources.magicStones.exploration.small,
    medium:
      resources.magicStones.baseCamp.medium +
      resources.magicStones.exploration.medium,
    large:
      resources.magicStones.baseCamp.large +
      resources.magicStones.exploration.large,
    huge:
      resources.magicStones.baseCamp.huge +
      resources.magicStones.exploration.huge,
  };

  // Basecamp variant - horizontal layout with Pencil images
  const currentLives = runtimeState.lives.currentLives;

  return (
    <header className="facility-header basecamp-variant">
      {/* Left section: Title + Lives + Nav */}
      <div className="page-title">
        <span className="basecamp-title-text">{title}</span>
        {variant !== "basecamp" && (
          <div className="facility-nav-wrapper" ref={dropdownRef}>
            <button
              className={`facility-nav-toggle${navOpen ? " open" : ""}`}
              onClick={() => setNavOpen((prev) => !prev)}
              aria-label="施設メニュー"
            >
              <img
                src={HEADER_ICONS.dropdown}
                className="dropdown-icon"
                alt="▼"
              />
            </button>
            {navOpen && (
              <div className="facility-nav-dropdown">
                {FACILITY_NAV_ITEMS.filter(item => item.showInNav).map((item) => (
                  <button
                    key={item.screen}
                    className={`facility-nav-item${gameState.currentScreen === item.screen ? " active" : ""}`}
                    onClick={() => {
                      navigateTo(item.screen);
                      setNavOpen(false);
                    }}
                    disabled={gameState.currentScreen === item.screen}
                  >
                    <span className="facility-nav-icon">{item.icon}</span>
                    <span className="facility-nav-label">{item.label}</span>
                  </button>
                ))}
                <div className="facility-nav-divider" />
                <button
                  className="facility-nav-item camp-link"
                  onClick={() => {
                    navigateTo("camp");
                    setNavOpen(false);
                  }}
                >
                  <span className="facility-nav-icon">🏕️</span>
                  <span className="facility-nav-label">拠点に戻る</span>
                </button>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Right section: Resources */}
      <div className="resource-frame">
        {variant === "basecamp" && (
          <div className="remaining-life">
            {Array.from({ length: currentLives }).map((_, i) => (
              <img
                key={i}
                src={HEADER_ICONS.heart}
                alt="life"
                className="heart-icon"
              />
            ))}
          </div>
        )}

        <div className="gold-frame">
          <img src={HEADER_ICONS.gold} alt="gold" className="gold-icon" />
          <span className="gold-amount">{getTotalGold()} G</span>
        </div>
        {/* Magic stone frame */}
        <div className="magic-stone-frame">
          <div className="stone-item" title="小魔石 (30G)">
            <img
              src={HEADER_ICONS.stoneSmall}
              alt="small stone"
              className="stone-icon small"
            />
            <span className="stone-count-value">{magicStones.small}</span>
          </div>
          <div className="stone-item" title="中魔石 (100G)">
            <img
              src={HEADER_ICONS.stoneMedium}
              alt="medium stone"
              className="stone-icon medium"
            />
            <span className="stone-count-value">{magicStones.medium}</span>
          </div>
          <div className="stone-item" title="大魔石 (350G)">
            <img
              src={HEADER_ICONS.stoneLarge}
              alt="large stone"
              className="stone-icon large"
            />
            <span className="stone-count-value">{magicStones.large}</span>
          </div>
          <div className="stone-item" title="極大魔石 (1000G)">
            <img
              src={HEADER_ICONS.stoneHuge}
              alt="huge stone"
              className="stone-icon huge"
            />
            <span className="stone-count-value">{magicStones.huge}</span>
          </div>
        </div>
      </div>
    </header>
  );
};

export default FacilityHeader;
