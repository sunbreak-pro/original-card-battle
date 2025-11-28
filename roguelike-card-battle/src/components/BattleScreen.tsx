import { useState } from "react";
import type { Card, Depth } from "../types/cardType";
import {
  calculateEffectivePower,
  getDepthEfficiency,
  MASTERY_THRESHOLDS,
} from "../types/cardType";
import { SAMPLE_CARDS } from "../data/Cards";

// 深度別のカラーテーマ定義
const depthThemes = {
  1: {
    // 上層：緑
    primary: "#1a3326",
    secondary: "#2d5f3f",
    accent: "#4a9d6d",
    bg: "linear-gradient(135deg, #050a08 0%, #0a1410 100%)",
    glow: "rgba(74, 157, 109, 0.25)",
  },
  2: {
    // 中層：青
    primary: "#1a2640",
    secondary: "#2e4a7c",
    accent: "#4a7fd9",
    bg: "linear-gradient(135deg, #030509 0%, #060e18 100%)",
    glow: "rgba(74, 127, 217, 0.25)",
  },
  3: {
    // 下層：赤
    primary: "#401a1a",
    secondary: "#7c2e2e",
    accent: "#d94a4a",
    bg: "linear-gradient(135deg, #0a0303 0%, #180808 100%)",
    glow: "rgba(217, 74, 74, 0.25)",
  },
  4: {
    // 深層：紫
    primary: "#2d1a40",
    secondary: "#5a2e7c",
    accent: "#9a4ad9",
    bg: "linear-gradient(135deg, #050308 0%, #0d0618 100%)",
    glow: "rgba(154, 74, 217, 0.25)",
  },
  5: {
    // 深淵：漆黒
    primary: "#1a0a0f",
    secondary: "#331419",
    accent: "#8f1f3d",
    bg: "linear-gradient(135deg, #000000 0%, #0a0305 100%)",
    glow: "rgba(143, 31, 61, 0.3)",
  },
} as const;
const hoverDepthThemes = {
  1: {
    // 上層：明るい緑
    primary: "#3d7a5c",
    secondary: "#5fbf8d",
    accent: "#7de0a8",
    bg: "linear-gradient(135deg, #1a3326 0%, #2d5f3f 100%)",
    glow: "rgba(125, 224, 168, 0.4)",
  },
  2: {
    // 中層：明るい青
    primary: "#3d5f8f",
    secondary: "#5c91d9",
    accent: "#7ab8ff",
    bg: "linear-gradient(135deg, #1a2640 0%, #2e4a7c 100%)",
    glow: "rgba(122, 184, 255, 0.4)",
  },
  3: {
    // 下層：明るい赤
    primary: "#d96060",
    secondary: "#d96060",
    accent: "#8f3d3d",
    bg: "linear-gradient(135deg, #401a1a 0%, #7c2e2e 100%)",
    glow: "rgba(255, 128, 128, 0.4)",
  },
  4: {
    // 深層：明るい紫
    primary: "#6b3d8f",
    secondary: "#9a60d9",
    accent: "#c280ff",
    bg: "linear-gradient(135deg, #2d1a40 0%, #5a2e7c 100%)",
    glow: "rgba(194, 128, 255, 0.4)",
  },
  5: {
    // 深淵：明るい深紅
    primary: "#8f2d40",
    secondary: "#d94060",
    accent: "#ff6080",
    bg: "linear-gradient(135deg, #1a0a0f 0%, #331419 100%)",
    glow: "rgba(255, 96, 128, 0.45)",
  },
} as const;

// 将来の実装のための型定義（現在は未使用）
// type StatusEffectType =
//   | "strength"
//   | "defense"
//   | "vulnerable"
//   | "weak"
//   | "poison"
//   | "regen";

// type EquipmentSlot =
//   | "weapon"
//   | "armor"
//   | "helmet"
//   | "boots"
//   | "accessory1"
//   | "accessory2";

// type DamageType = "physical" | "magical" | "heal" | "shield";

// interface StatusEffect {
//   id: string;
//   type: StatusEffectType;
//   name: string;
//   icon: string;
//   value: number;
//   duration: number;
//   description: string;
//   isDebuff: boolean;
// }

// interface Equipment {
//   id: string;
//   name: string;
//   slot: EquipmentSlot;
//   icon: string;
//   durability: number;
//   maxDurability: number;
//   effects: string[];
// }

// interface DamageNumber {
//   id: string;
//   value: number;
//   type: DamageType;
//   x: number;
//   y: number;
//   timestamp: number;
// }

// Card型は types/card.ts からインポート

interface BattleScreenProps {
  depth: Depth;
  onDepthChange: (depth: Depth) => void;
}

// カードコンポーネント
const CardComponent = ({
  card,
  theme,
  isPlayable,
  currentDepth,
}: {
  card: Card;
  theme: (typeof depthThemes)[Depth];
  isPlayable: boolean;
  currentDepth: Depth;
}) => {
  const getCategoryColor = (category: Card["category"]) => {
    switch (category) {
      case "physical":
        return "#d94a4a";
      case "defense":
        return "#4a8ed9";
      case "magic":
        return "#9a4ad9";
      case "heal":
        return "#4ade80";
    }
  };

  const categoryColor = getCategoryColor(card.category);
  const effectivePower = card.basePower
    ? calculateEffectivePower(card, currentDepth)
    : undefined;

  return (
    <div
      style={{
        width: "150px",
        height: "220px",
        background: `linear-gradient(145deg, ${theme.primary} 0%, ${theme.secondary} 50%, ${theme.primary} 100%)`,
        border: `3px solid ${
          isPlayable ? theme.accent : "rgba(60, 60, 60, 0.5)"
        }`,
        borderRadius: "12px",
        padding: "14px",
        position: "relative",
        cursor: isPlayable ? "pointer" : "not-allowed",
        opacity: isPlayable ? 1 : 0.5,
        transition: "all 0.35s cubic-bezier(0.34, 1.56, 0.64, 1)",
        boxShadow: isPlayable
          ? `0 6px 16px rgba(0, 0, 0, 0.6), 0 0 25px ${theme.glow}, inset 0 2px 4px rgba(255, 255, 255, 0.1)`
          : "0 2px 8px rgba(0, 0, 0, 0.8), inset 0 2px 4px rgba(0, 0, 0, 0.3)",
        display: "flex",
        flexDirection: "column",
        gap: "10px",
        transform: "translateY(0) scale(1) rotate(0deg)",
      }}
      onMouseEnter={(e) => {
        if (isPlayable) {
          e.currentTarget.style.transform =
            "translateY(-30px) scale(1.15) rotate(0deg)";
          e.currentTarget.style.zIndex = "100";
          e.currentTarget.style.boxShadow = `0 12px 32px rgba(0, 0, 0, 0.8), 0 0 50px ${theme.glow}, inset 0 2px 4px rgba(255, 255, 255, 0.2)`;
        }
      }}
      onMouseLeave={(e) => {
        e.currentTarget.style.transform = "translateY(0) scale(1) rotate(0deg)";
        e.currentTarget.style.zIndex = "1";
        e.currentTarget.style.boxShadow = isPlayable
          ? `0 6px 16px rgba(0, 0, 0, 0.6), 0 0 25px ${theme.glow}, inset 0 2px 4px rgba(255, 255, 255, 0.1)`
          : "0 2px 8px rgba(0, 0, 0, 0.8), inset 0 2px 4px rgba(0, 0, 0, 0.3)";
      }}
    >
      {/* コスト表示 */}
      <div
        style={{
          position: "absolute",
          top: "-12px",
          left: "-12px",
          width: "44px",
          height: "44px",
          background: `radial-gradient(circle, ${theme.accent} 0%, ${categoryColor} 100%)`,
          borderRadius: "50%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontWeight: "bold",
          fontSize: "22px",
          color: "#fff",
          border: "3px solid #000",
          boxShadow: `0 0 15px ${theme.glow}, 0 4px 8px rgba(0, 0, 0, 0.6), inset 0 2px 4px rgba(255, 255, 255, 0.3)`,
          textShadow: "0 2px 4px rgba(0, 0, 0, 0.8)",
        }}
      >
        {card.cost}
      </div>

      {/* カテゴリバッジ */}
      <div
        style={{
          position: "absolute",
          top: "10px",
          right: "10px",
          background: `linear-gradient(135deg, ${categoryColor} 0%, ${categoryColor}dd 100%)`,
          padding: "4px 8px",
          borderRadius: "6px",
          fontSize: "10px",
          fontWeight: "bold",
          textTransform: "uppercase",
          boxShadow: `0 2px 6px rgba(0, 0, 0, 0.6), 0 0 10px ${categoryColor}66`,
          border: "1px solid rgba(255, 255, 255, 0.2)",
          letterSpacing: "0.5px",
        }}
      >
        {card.category}
      </div>

      {/* カード名 */}
      <div
        style={{
          fontSize: "16px",
          fontWeight: "bold",
          color: "#fff",
          marginTop: "24px",
          textAlign: "center",
          textShadow: `0 0 8px ${theme.glow}, 0 2px 4px rgba(0, 0, 0, 0.8)`,
          letterSpacing: "0.5px",
        }}
      >
        {card.name}
      </div>

      {/* 説明文エリア */}
      <div
        style={{
          flex: 1,
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          background: "rgba(0, 0, 0, 0.4)",
          borderRadius: "8px",
          padding: "10px",
          border: "1px solid rgba(255, 255, 255, 0.1)",
          gap: "6px",
        }}
      >
        <div
          style={{
            fontSize: "12px",
            color: "rgba(255, 255, 255, 0.9)",
            textAlign: "center",
            lineHeight: "1.4",
          }}
        >
          {card.description}
        </div>
        {effectivePower !== undefined && (
          <div
            style={{
              fontSize: "14px",
              fontWeight: "bold",
              color: theme.accent,
              textShadow: `0 0 8px ${theme.glow}`,
            }}
          >
            Power: {effectivePower}
          </div>
        )}
      </div>

      {/* 熟練度バー */}
      <div style={{ marginTop: "auto" }}>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            fontSize: "10px",
            color: "rgba(255, 255, 255, 0.7)",
            marginBottom: "4px",
          }}
        >
          <span style={{ fontWeight: "bold" }}>
            Mastery Lv.{card.masteryLevel}
          </span>
          <span style={{ color: theme.accent, fontWeight: "bold" }}>
            {card.useCount}/
            {
              MASTERY_THRESHOLDS[
                Math.min(3, card.masteryLevel + 1) as 0 | 1 | 2 | 3
              ]
            }
          </span>
        </div>
        <div
          style={{
            width: "100%",
            height: "6px",
            background: "rgba(0, 0, 0, 0.6)",
            borderRadius: "3px",
            overflow: "hidden",
            border: "1px solid rgba(255, 255, 255, 0.1)",
          }}
        >
          <div
            style={{
              width: `${
                card.masteryLevel === 3 ? 100 : ((card.useCount % 8) / 8) * 100
              }%`,
              height: "100%",
              background: `linear-gradient(90deg, ${categoryColor} 0%, ${theme.accent} 100%)`,
              boxShadow: `0 0 10px ${theme.glow}`,
              transition: "width 0.5s ease",
            }}
          />
        </div>
      </div>

      {/* 深度適正表示 */}
      <div
        style={{
          display: "flex",
          gap: "4px",
          justifyContent: "center",
          alignItems: "center",
        }}
      >
        {[1, 2, 3, 4, 5].map((d) => {
          const efficiency = getDepthEfficiency(
            card.depthCurveType,
            d as Depth
          );
          const isSuitable = efficiency >= 1.0;
          const isCurrentDepth = d === currentDepth;
          return (
            <div
              key={d}
              style={{
                width: "12px",
                height: "12px",
                borderRadius: "50%",
                background: isSuitable
                  ? `radial-gradient(circle, ${theme.accent} 0%, ${categoryColor} 100%)`
                  : "rgba(30, 30, 30, 0.6)",
                border: `2px solid ${
                  isCurrentDepth
                    ? "#fff"
                    : isSuitable
                    ? theme.accent
                    : "rgba(60, 60, 60, 0.4)"
                }`,
                boxShadow: isSuitable
                  ? `0 0 8px ${theme.glow}`
                  : "inset 0 1px 2px rgba(0, 0, 0, 0.5)",
                transition: "all 0.3s ease",
                opacity: efficiency >= 1.0 ? 1.0 : 0.3 + efficiency * 0.5,
              }}
            />
          );
        })}
      </div>
    </div>
  );
};

const BattleScreen = ({ depth, onDepthChange }: BattleScreenProps) => {
  const theme = depthThemes[depth];
  const hoverTheme = hoverDepthThemes[depth];

  // 戦闘状態
  const [playerHp, setPlayerHp] = useState(80);
  const [playerMaxHp] = useState(100);
  const [playerShield, setPlayerShield] = useState(15);
  const [enemyHp] = useState(45); // setEnemyHpは将来使用予定
  const [enemyMaxHp] = useState(60);
  const [enemyShield] = useState(10); // 敵のシールド（デモ用）
  const [energy, setEnergy] = useState(3);
  const [maxEnergy] = useState(3);
  const [turn, setTurn] = useState(1);
  const [drawPileCount] = useState(23); // setDrawPileCountは将来使用予定
  const [discardPileCount] = useState(7); // setDiscardPileCountは将来使用予定

  // サンプルカード - SAMPLE_CARDSから最初の5枚を使用
  const [hand] = useState<Card[]>(SAMPLE_CARDS.slice(0, 5));

  const handleEndTurn = () => {
    setEnergy(maxEnergy);
    setTurn(turn + 1);
    // 敵の行動をシミュレート
    const damage = Math.floor(Math.random() * 10) + 5;
    if (playerShield > 0) {
      const remainingDamage = Math.max(0, damage - playerShield);
      setPlayerShield(Math.max(0, playerShield - damage));
      setPlayerHp(Math.max(0, playerHp - remainingDamage));
    } else {
      setPlayerHp(Math.max(0, playerHp - damage));
    }
  };

  const depthNames = {
    1: "Upper Depths",
    2: "Middle Depths",
    3: "Lower Depths",
    4: "Deep Abyss",
    5: "Crimson Void",
  };

  return (
    <div
      style={{
        width: "100%",
        maxWidth: "1600px",
        height: "900px",
        aspectRatio: "16 / 9",
        background: theme.bg,
        border: `3px solid ${theme.accent}`,
        borderRadius: "12px",
        boxShadow: `0 0 40px ${theme.glow}, inset 0 0 60px rgba(0, 0, 0, 0.5)`,
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "20px",
        gap: "20px",
      }}
    >
      {/* 深度表示とテスト用ボタン */}
      <div
        style={{
          position: "absolute",
          top: "20px",
          left: "15%",
          transform: "translateX(-50%)",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          gap: "8px",
          zIndex: 10,
        }}
      >
        <div
          style={{
            fontSize: "24px",
            fontWeight: "bold",
            color: theme.accent,
            textShadow: `0 0 10px ${theme.glow}`,
            textTransform: "uppercase",
            letterSpacing: "2px",
          }}
        >
          {depthNames[depth]} - Turn {turn}
        </div>
        <div style={{ display: "flex", gap: "4px" }}>
          {[1, 2, 3, 4, 5].map((d) => (
            <button
              key={d}
              onClick={() => onDepthChange(d as Depth)}
              style={{
                padding: "4px 12px",
                background: depth === d ? theme.accent : "rgba(0, 0, 0, 0.5)",
                border: `1px solid ${theme.accent}`,
                borderRadius: "4px",
                color: "#fff",
                fontSize: "12px",
                fontWeight: "bold",
              }}
            >
              D{d}
            </button>
          ))}
        </div>
      </div>

      {/* 戦闘エリア - 敵とプレイヤー横並び */}
      <div
        style={{
          flex: 1,
          display: "flex",
          flexDirection: "row",
          justifyContent: "space-around",
          alignItems: "center",
          gap: "40px",
          padding: "20px",
        }}
      >
        {/* 敵エリア */}
        <div
          style={{
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            gap: "16px",
          }}
        >
          {/* 敵名 */}
          <div
            style={{
              fontSize: "24px",
              fontWeight: "bold",
              color: "#fff",
              textShadow: `0 0 10px ${theme.glow}`,
            }}
          >
            Shadow Beast
          </div>

          {/* 敵の画像エリア */}
          <div
            style={{
              width: "200px",
              height: "200px",
              background: `radial-gradient(circle, ${theme.secondary} 0%, rgba(0, 0, 0, 0.9) 100%)`,
              border: `3px solid ${theme.accent}`,
              borderRadius: "12px",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              boxShadow: `0 0 30px ${theme.glow}, inset 0 2px 8px rgba(255, 255, 255, 0.1)`,
              fontSize: "120px",
              transition: "all 0.3s ease",
            }}
          >
            👹
          </div>

          {/* HP & SHIELD & 次の行動（1つのボーダーで囲む） */}
          <div
            style={{
              background: `linear-gradient(135deg, ${theme.primary} 0%, rgba(0, 0, 0, 0.8) 100%)`,
              border: `2px solid ${theme.accent}`,
              borderRadius: "12px",
              padding: "16px",
              minWidth: "280px",
              boxShadow: `0 0 20px ${theme.glow}`,
            }}
          >
            {/* 敵SHIELD */}
            {enemyShield > 0 && (
              <div style={{ marginBottom: "12px" }}>
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    fontSize: "14px",
                    color: "rgba(74, 142, 217, 0.9)",
                    marginBottom: "6px",
                  }}
                >
                  <span>SHIELD</span>
                  <span>{enemyShield}</span>
                </div>
                <div
                  style={{
                    width: "100%",
                    height: "12px",
                    background: "rgba(0, 0, 0, 0.5)",
                    borderRadius: "6px",
                    overflow: "hidden",
                  }}
                >
                  <div
                    style={{
                      width: `${Math.min(
                        (enemyShield / enemyMaxHp) * 100,
                        100
                      )}%`,
                      height: "100%",
                      background: `linear-gradient(90deg, #2e5c9d 0%, #4a8ed9 100%)`,
                      transition: "width 0.3s ease",
                      boxShadow: "0 0 10px rgba(74, 142, 217, 0.6)",
                    }}
                  />
                </div>
              </div>
            )}

            {/* 敵HP */}
            <div style={{ marginBottom: "16px" }}>
              <div
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  fontSize: "14px",
                  color: "rgba(255, 255, 255, 0.8)",
                  marginBottom: "6px",
                }}
              >
                <span>HP</span>
                <span>
                  {enemyHp} / {enemyMaxHp}
                </span>
              </div>
              <div
                style={{
                  width: "100%",
                  height: "20px",
                  background: "rgba(0, 0, 0, 0.5)",
                  borderRadius: "10px",
                  overflow: "hidden",
                }}
              >
                <div
                  style={{
                    width: `${(enemyHp / enemyMaxHp) * 100}%`,
                    height: "100%",
                    background: `linear-gradient(90deg, #d94a4a 0%, #ff6b6b 100%)`,
                    transition: "width 0.3s ease",
                    boxShadow: "0 0 10px rgba(217, 74, 74, 0.5)",
                  }}
                />
              </div>
            </div>

            {/* 次の行動 */}
            <div>
              <div
                style={{
                  fontSize: "12px",
                  color: "rgba(255, 255, 255, 0.6)",
                  marginBottom: "4px",
                }}
              >
                Next Action:
              </div>
              <div
                style={{
                  fontSize: "16px",
                  color: "#d94a4a",
                  fontWeight: "bold",
                }}
              >
                Attack (8-12 dmg)
              </div>
            </div>
          </div>
        </div>

        {/* プレイヤーステータスバー */}
        <div
          style={{
            display: "block",
            justifyContent: "center",
            alignItems: "center",
            padding: "10px 30px",
            background: `linear-gradient(90deg, ${theme.primary} 0%, rgba(0, 0, 0, 0.8) 100%)`,
            border: `2px solid ${theme.accent}`,
            borderRadius: "12px",
            boxShadow: `0 0 20px ${theme.glow}`,
            gap: "1.5rem",
          }}
        >
          <div
            style={{
              display: "flex",
              justifyContent: "center",
              alignItems: "center",
              fontSize: "18px",
              fontWeight: "bold",
              color: "#fff",
              marginBottom: "10px",
            }}
          >
            Player
          </div>
          {/* プレイヤー画像エリア（プレースホルダー） */}
          <div
            style={{
              width: "120px",
              height: "140px",
              border: `3px solid ${theme.accent}`,
              borderRadius: "12px",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              boxShadow: `0 0 20px ${theme.glow}, inset 0 2px 8px rgba(255, 255, 255, 0.1)`,
              fontSize: "64px",
              flexShrink: 0,
            }}
          >
            ⚔️
          </div>

          {/* プレイヤーHP & シールド */}
          <div style={{ flex: 1 }}>
            {/* シールドバー（HPバーの上） */}
            {playerShield > 0 && (
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "12px",
                  marginBottom: "10px",
                }}
              >
                <div
                  style={{
                    width: "100%",
                    maxWidth: "350px",
                    height: "10px",
                    background: "rgba(0, 0, 0, 0.7)",
                    borderRadius: "9px",
                    overflow: "hidden",
                    border: "2px solid rgba(74, 142, 217, 0.4)",
                  }}
                >
                  <div
                    style={{
                      width: `${Math.min(
                        (playerShield / playerMaxHp) * 100,
                        100
                      )}%`,
                      height: "100%",
                      background: `linear-gradient(90deg, #2e5c9d 0%, #4a8ed9 100%)`,
                      transition: "width 0.3s ease",
                      boxShadow:
                        "0 0 10px rgba(74, 142, 217, 0.6), inset 0 2px 4px rgba(255, 255, 255, 0.3)",
                    }}
                  />
                </div>
                <div
                  style={{
                    fontSize: "1rem",
                    fontWeight: "bold",
                    color: "rgba(74, 142, 217, 0.9)",
                    minWidth: "60px",
                  }}
                >
                  {playerShield}
                </div>
              </div>
            )}

            {/* HPバー */}
            <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
              <div
                style={{
                  width: "100%",
                  maxWidth: "350px",
                  height: "10px",
                  background: "rgba(0, 0, 0, 0.7)",
                  borderRadius: "10px",
                  overflow: "hidden",
                  border: "2px solid rgba(255, 255, 255, 0.1)",
                }}
              >
                <div
                  style={{
                    width: `${(playerHp / playerMaxHp) * 100}%`,
                    height: "100%",
                    background: `linear-gradient(90deg, #b34646ff 0%, #ff0000ff 100%)`,
                    transition: "width 0.3s ease",
                    boxShadow: "inset 0 2px 4px rgba(255, 255, 255, 0.2)",
                  }}
                />
              </div>
              <div
                style={{
                  fontSize: "1rem",
                  fontWeight: "bold",
                  color: "rgba(255, 255, 255, 0.9)",
                  width: "80px",
                  textAlign: "right",
                }}
              >
                {playerHp} / {playerMaxHp}
              </div>
            </div>
          </div>

          {/* エナジー表示 */}
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: "16px",
            }}
          >
            <div
              style={{
                position: "relative",
                display: "flex",
                gap: "8px",
              }}
            >
              {Array.from({ length: maxEnergy }).map((_, i) => (
                <div
                  key={i}
                  style={{
                    position: "relative",
                    width: "48px",
                    height: "48px",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                  }}
                >
                  {/* エナジーの背景円 */}
                  <div
                    style={{
                      position: "absolute",
                      width: "100%",
                      height: "100%",
                      borderRadius: "50%",
                      background:
                        i < energy
                          ? `radial-gradient(circle, ${theme.accent} 0%, ${theme.secondary} 100%)`
                          : "rgba(20, 20, 20, 0.8)",
                      border: `3px solid ${
                        i < energy ? theme.accent : "rgba(60, 60, 60, 0.5)"
                      }`,
                      boxShadow:
                        i < energy
                          ? `0 0 20px ${theme.glow}, inset 0 2px 4px rgba(255, 255, 255, 0.3)`
                          : "inset 0 2px 4px rgba(0, 0, 0, 0.5)",
                      transition:
                        "all 0.4s cubic-bezier(0.68, -0.55, 0.265, 1.55)",
                      transform: i < energy ? "scale(1)" : "scale(0.85)",
                    }}
                  />
                </div>
              ))}
            </div>
            <div
              style={{
                fontSize: "18px",
                fontWeight: "bold",
                color: theme.accent,
                textShadow: `0 0 10px ${theme.glow}`,
                display: "flex",
                margin: "0 1rem 0 0",
              }}
            >
              {energy}/{maxEnergy}
            </div>
          </div>

          {/* ターン終了ボタン */}
          <button
            onClick={handleEndTurn}
            style={{
              padding: "12px 32px",
              background: `linear-gradient(135deg, ${theme.secondary} 0%, ${theme.accent} 100%)`,
              border: `2px solid ${theme.accent}`,
              borderRadius: "8px",
              color: "#fff",
              fontSize: "16px",
              fontWeight: "bold",
              textTransform: "uppercase",
              letterSpacing: "1px",
              boxShadow: `0 4px 12px ${theme.glow}`,
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.background = `linear-gradient(135deg, ${hoverTheme.secondary} 0%, ${hoverTheme.accent} 100%)`;
              e.currentTarget.style.transform = "translateY(-2px)";
              e.currentTarget.style.boxShadow = `0 6px 16px ${hoverTheme.glow}`;
              e.currentTarget.style.borderColor = hoverTheme.accent;
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.background = `linear-gradient(135deg, ${theme.secondary} 0%, ${theme.accent} 100%)`;
              e.currentTarget.style.transform = "translateY(0)";
              e.currentTarget.style.boxShadow = `0 4px 12px ${theme.glow}`;
              e.currentTarget.style.borderColor = theme.accent;
            }}
          >
            End Turn
          </button>
        </div>
      </div>

      {/* 手札エリア */}
      <div
        style={{
          display: "flex",
          justifyContent: "center",
          alignItems: "flex-end",
          gap: "8px",
          padding: "24px",
          background: "rgba(0, 0, 0, 0.5)",
          borderRadius: "16px",
          border: `2px solid ${theme.primary}`,
          boxShadow: `inset 0 4px 12px rgba(0, 0, 0, 0.6), 0 0 20px ${theme.glow}`,
          position: "relative",
        }}
      >
        {hand.map((card, index) => {
          const totalCards = hand.length;
          const middleIndex = (totalCards - 1) / 2;
          const offset = index - middleIndex;
          const rotation = offset * 3; // 各カードを少し回転
          const verticalOffset = Math.abs(offset) * 20; // 中央のカードを少し上げる

          return (
            <div
              key={card.id}
              style={{
                transform: `translateY(${verticalOffset}px) rotate(${rotation}deg)`,
                transition: "transform 0.3s ease",
              }}
            >
              <CardComponent
                card={card}
                theme={theme}
                isPlayable={card.cost <= energy}
                currentDepth={depth}
              />
            </div>
          );
        })}

        {/* 捨て札アイコン（左下） */}
        <div
          style={{
            position: "absolute",
            bottom: "20px",
            left: "20px",
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            gap: "8px",
          }}
        >
          <div
            style={{
              width: "70px",
              height: "95px",
              background: `linear-gradient(135deg, ${theme.primary} 0%, rgba(0, 0, 0, 0.9) 100%)`,
              border: `2px solid ${theme.accent}`,
              borderRadius: "8px",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: "48px",
              boxShadow: `0 4px 12px rgba(0, 0, 0, 0.6), 0 0 15px ${theme.glow}`,
              cursor: "pointer",
              transition: "all 0.3s ease",
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.transform = "translateY(-4px) scale(1.05)";
              e.currentTarget.style.boxShadow = `0 6px 16px rgba(0, 0, 0, 0.8), 0 0 25px ${theme.glow}`;
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.transform = "translateY(0) scale(1)";
              e.currentTarget.style.boxShadow = `0 4px 12px rgba(0, 0, 0, 0.6), 0 0 15px ${theme.glow}`;
            }}
          >
            🗑️
          </div>
          <div
            style={{
              fontSize: "18px",
              fontWeight: "bold",
              color: theme.accent,
              textShadow: `0 0 8px ${theme.glow}`,
            }}
          >
            {discardPileCount}
          </div>
        </div>

        {/* 山札アイコン（右下） */}
        <div
          style={{
            position: "absolute",
            bottom: "20px",
            right: "20px",
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            gap: "8px",
          }}
        >
          <div
            style={{
              width: "70px",
              height: "95px",
              background: `linear-gradient(135deg, ${theme.primary} 0%, ${theme.secondary} 100%)`,
              border: `2px solid ${theme.accent}`,
              borderRadius: "8px",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: "48px",
              boxShadow: `0 4px 12px rgba(0, 0, 0, 0.6), 0 0 15px ${theme.glow}`,
              cursor: "pointer",
              transition: "all 0.3s ease",
              position: "relative",
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.transform = "translateY(-4px) scale(1.05)";
              e.currentTarget.style.boxShadow = `0 6px 16px rgba(0, 0, 0, 0.8), 0 0 25px ${theme.glow}`;
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.transform = "translateY(0) scale(1)";
              e.currentTarget.style.boxShadow = `0 4px 12px rgba(0, 0, 0, 0.6), 0 0 15px ${theme.glow}`;
            }}
          >
            🎴
          </div>
          <div
            style={{
              fontSize: "18px",
              fontWeight: "bold",
              color: theme.accent,
              textShadow: `0 0 8px ${theme.glow}`,
            }}
          >
            {drawPileCount}
          </div>
        </div>
      </div>
    </div>
  );
};

export default BattleScreen;
