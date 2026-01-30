/**
 * FacilityHeader - Unified header component for all facility screens
 * Displays resources (Gold, Magic Stones by type)
 *
 * Variants:
 * - "default": Standard header with gold and magic stones
 * - "basecamp": Adds lives display (hearts)
 */

import React from "react";
import { usePlayer } from "../../contexts/PlayerContext";
import { useResources } from "../../contexts/ResourceContext";
import { HEADER_ICONS } from "@/constants/uiConstants";
import "../css/components/FacilityHeader.css";

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
      {/* Left section: Title + Lives */}
      <div className="page-title">
        <span className="basecamp-title-text">{title}</span>
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
