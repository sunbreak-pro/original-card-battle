import { useState } from "react";
import { useGameState } from "../../domain/camps/contexts/GameStateContext";
import type {
  FacilityType,
  GameScreen,
} from "../../domain/camps/types/CampTypes";
import "./BaseCamp.css";
import { Swordman_Status } from "../../domain/characters/player/data/PlayerData";
interface FacilityCardProps {
  type: FacilityType;
  name: string;
  description: string;
  icon: string;
  isUnlocked: boolean;
  onEnter: () => GameScreen | void;
}

const FacilityCard = ({
  type,
  name,
  description,
  icon,
  isUnlocked,
  onEnter,
}: FacilityCardProps) => {
  const [isHovered, setIsHovered] = useState(false);

  return (
    <div
      className={`facility-card ${type} ${isUnlocked ? "unlocked" : "locked"} ${
        isHovered ? "hovered" : ""
      }`}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      onClick={isUnlocked ? onEnter : undefined}
    >
      {/* 背景装飾 */}
      <div className="facility-bg-pattern" />
      <div className="facility-glow" />

      {/* アイコン */}
      <div className="facility-icon">{icon}</div>

      {/* 施設名 */}
      <div className="facility-name">{name}</div>

      {/* 説明文 */}
      <div className="facility-description">{description}</div>

      {/* ロック表示 */}
      {!isUnlocked && (
        <div className="facility-lock">
          <div className="lock-icon">🔒</div>
          <div className="lock-text">Locked</div>
        </div>
      )}

      {/* ホバーエフェクト */}
      {isHovered && isUnlocked && (
        <div className="facility-hover-effect">
          <div
            className="hover-text"
            style={{ color: "rgba(124, 220, 77, 1)" }}
          >
            Enter →
          </div>
        </div>
      )}
    </div>
  );
};

const BaseCamp = () => {
  const { navigateTo } = useGameState();

  const facilities: FacilityCardProps[] = [
    {
      type: "dungeon",
      name: "ダンジョンゲート",
      description: "Descend into the depths and face your destiny",
      icon: "🌀",
      isUnlocked: true,
      onEnter: () => navigateTo("dungeon"),
    },
    {
      type: "shop",
      name: "取引所",
      description: "Buy and sell cards, items, and relics",
      icon: "🏪",
      isUnlocked: true,
      onEnter: () => navigateTo("shop"),
    },
    {
      type: "blacksmith",
      name: "鍛冶屋",
      description: "Forge and upgrade your equipment",
      icon: "⚒️",
      isUnlocked: true,
      onEnter: () => navigateTo("blacksmith"),
    },
    {
      type: "sanctuary",
      name: "聖域",
      description: "Strengthen your soul with permanent upgrades",
      icon: "⛪",
      isUnlocked: true,
      onEnter: () => navigateTo("sanctuary"),
    },
    {
      type: "library",
      name: "図書館",
      description: "Build your deck and browse the encyclopedia",
      icon: "📚",
      isUnlocked: true,
      onEnter: () => navigateTo("library"),
    },
    {
      type: "guild",
      name: "酒場",
      description: "Rest, recruit companions, and hear rumors",
      icon: "🍺",
      isUnlocked: true,
      onEnter: () => navigateTo("guild"),
    },
    {
      type: "storage",
      name: "倉庫",
      description: "Store and manage your items safely",
      icon: "📦",
      isUnlocked: true,
      onEnter: () => navigateTo("storage"),
    },
  ];

  return (
    <div className="base-camp">
      {/* ヘッダー */}
      <div className="camp-header">
        <h1 className="camp-title">Base Camp</h1>
        <div className="camp-subtitle">
          A sanctuary amidst the darkness. Choose your path wisely.
        </div>
      </div>

      {/* 背景装飾 */}
      <div className="camp-background">
        <div className="bg-stars" />
        {/* <div className="bg-fog" /> */}
        <div className="bg-ground" />
      </div>

      {/* 施設グリッド */}
      <div className="facilities-grid">
        {facilities.map((facility) => (
          <FacilityCard key={facility.type} {...facility} />
        ))}
      </div>

      {/* フッター情報 */}
      <div className="camp-footer">
        <div className="player-stats">
          <div className="stat-item">
            <span className="stat-icon">💰</span>
            <span className="stat-value">Gold: {Swordman_Status.gold} </span>
          </div>
          <div className="stat-item">
            <span className="stat-icon">📊</span>
            <span className="stat-value">Level:{Swordman_Status.level} </span>
          </div>
          <div className="stat-item">
            <span className="stat-icon">🃏</span>
            <span className="stat-value">
              Cards: {Swordman_Status.deck.length}
            </span>
          </div>
        </div>
      </div>
    </div>
  );
};

export default BaseCamp;
