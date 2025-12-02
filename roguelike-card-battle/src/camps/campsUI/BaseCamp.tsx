import { useState } from "react";
import "./BaseCamp.css";

type FacilityType =
  | "shop"
  | "blacksmith"
  | "dungeon"
  | "church"
  | "training"
  | "tavern";

interface FacilityCardProps {
  type: FacilityType;
  name: string;
  description: string;
  icon: string;
  isUnlocked: boolean;
  onEnter: () => void;
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
  const [selectedFacility, setSelectedFacility] = useState<FacilityType | null>(
    null
  );

  const facilities: FacilityCardProps[] = [
    {
      type: "dungeon",
      name: "深淵の入り口",
      description: "Descend into the depths and face your destiny",
      icon: "🌀",
      isUnlocked: true,
      onEnter: () => setSelectedFacility("dungeon"),
    },
    {
      type: "shop",
      name: "取引所",
      description: "Buy and sell cards, items, and relics",
      icon: "🏪",
      isUnlocked: true,
      onEnter: () => setSelectedFacility("shop"),
    },
    {
      type: "blacksmith",
      name: "鍛冶屋",
      description: "Forge and upgrade your equipment",
      icon: "⚒️",
      isUnlocked: true,
      onEnter: () => setSelectedFacility("blacksmith"),
    },
    {
      type: "church",
      name: "古代の時計台",
      description: "Remove curses and purify your deck",
      icon: "⛪",
      isUnlocked: false,
      onEnter: () => setSelectedFacility("church"),
    },
    {
      type: "training",
      name: "啓示の間",
      description: "Practice and master your cards",
      icon: "🎯",
      isUnlocked: false,
      onEnter: () => setSelectedFacility("training"),
    },
    {
      type: "tavern",
      name: "酒場",
      description: "Rest, recruit companions, and hear rumors",
      icon: "🍺",
      isUnlocked: false,
      onEnter: () => setSelectedFacility("tavern"),
    },
  ];

  // 施設が選択された場合、その施設の詳細画面を表示（今は簡易実装）
  if (selectedFacility) {
    return (
      <div className="facility-detail">
        <button
          className="back-button"
          onClick={() => setSelectedFacility(null)}
        >
          ← Back to Camp
        </button>
        <div className="facility-content">
          <h2>{facilities.find((f) => f.type === selectedFacility)?.name}</h2>
          <p className="coming-soon">Coming Soon...</p>
        </div>
      </div>
    );
  }

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
            <span className="stat-value">1,250 Gold</span>
          </div>
          <div className="stat-item">
            <span className="stat-icon">📊</span>
            <span className="stat-value">Level 5</span>
          </div>
          <div className="stat-item">
            <span className="stat-icon">🃏</span>
            <span className="stat-value">20 Cards</span>
          </div>
        </div>
      </div>
    </div>
  );
};

export default BaseCamp;
