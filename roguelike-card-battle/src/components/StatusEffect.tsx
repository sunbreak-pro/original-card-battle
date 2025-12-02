import { useState } from "react";
import type { BuffDebuffMap, BuffDebuffType } from "../cards/type/baffType";
import { BuffDebuffEffects } from "../cards/data/BuffData";

interface StatusEffectDisplayProps {
  buffsDebuffs: BuffDebuffMap;
  theme: {
    primary: string;
    secondary: string;
    accent: string;
    bg: string;
    glow: string;
  };
}

// アイコンマッピング（簡易実装）
const BUFF_DEBUFF_ICONS: Record<BuffDebuffType, string> = {
  // デバフ - 持続ダメージ
  burn: "🔥",
  bleed: "🩸",
  poison: "☠️",
  curse: "👿",
  // デバフ - 状態異常
  slow: "🐌",
  freeze: "❄️",
  paralyze: "⚡",
  stun: "💫",
  weak: "💔",
  // デバフ - 能力減少
  defDown: "🛡️↓",
  atkDown: "⚔️↓",
  healingDown: "💚↓",
  // バフ - 能力上昇
  atkUp: "⚔️↑",
  defUp: "🛡️↑",
  magicUp: "✨↑",
  physicalUp: "💪↑",
  penetrationUp: "🎯↑",
  // バフ - 特殊
  regeneration: "💚",
  shieldRegen: "🛡️",
  reflect: "🔄",
  evasion: "💨",
  critical: "⭐",
  immunity: "✨",
};

const StatusEffectDisplay = ({
  buffsDebuffs,
  theme,
}: StatusEffectDisplayProps) => {
  const [hoveredEffect, setHoveredEffect] = useState<string | null>(null);

  if (buffsDebuffs.size === 0) return null;

  return (
    <div
      style={{
        display: "flex",
        gap: "8px",
        flexWrap: "wrap",
        marginTop: "12px",
      }}
    >
      {Array.from(buffsDebuffs.entries()).map(([type, buff]) => {
        const effectData = BuffDebuffEffects[type];
        const icon = BUFF_DEBUFF_ICONS[type];

        return (
          <div
            key={type}
            style={{
              position: "relative",
              width: "48px",
              height: "48px",
              background: effectData.isDebuff
                ? "linear-gradient(135deg, #5f1e1e, #9d2e2e)"
                : `linear-gradient(135deg, ${theme.primary}, ${theme.secondary})`,
              border: `2px solid ${
                effectData.isDebuff ? "#d94a4a" : theme.accent
              }`,
              borderRadius: "8px",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: "24px",
              cursor: "pointer",
              transition: "all 0.3s ease",
              boxShadow: effectData.isDebuff
                ? "0 4px 12px rgba(217, 74, 74, 0.5)"
                : `0 4px 12px ${theme.glow}`,
            }}
            onMouseEnter={() => setHoveredEffect(type)}
            onMouseLeave={() => setHoveredEffect(null)}
          >
            {icon}

            {/* スタック数（右上） */}
            {buff.stacks > 1 && (
              <div
                style={{
                  position: "absolute",
                  top: "-6px",
                  right: "-6px",
                  width: "20px",
                  height: "20px",
                  background: "#000",
                  border: `2px solid ${
                    effectData.isDebuff ? "#d94a4a" : theme.accent
                  }`,
                  borderRadius: "50%",
                  fontSize: "10px",
                  fontWeight: "bold",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  color: "#fff",
                  boxShadow: "0 2px 6px rgba(0, 0, 0, 0.8)",
                }}
              >
                {buff.stacks}
              </div>
            )}

            {/* 残りターン数（右下） */}
            {!buff.isPermanent && (
              <div
                style={{
                  position: "absolute",
                  bottom: "-6px",
                  right: "-6px",
                  width: "24px",
                  height: "24px",
                  background: "#000",
                  border: `2px solid ${
                    effectData.isDebuff ? "#d94a4a" : theme.accent
                  }`,
                  borderRadius: "50%",
                  fontSize: "12px",
                  fontWeight: "bold",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  color: "#fff",
                  boxShadow: "0 2px 6px rgba(0, 0, 0, 0.8)",
                }}
              >
                {buff.duration}
              </div>
            )}

            {/* ホバー時のツールチップ */}
            {hoveredEffect === type && (
              <div
                style={{
                  position: "absolute",
                  bottom: "60px",
                  left: "50%",
                  transform: "translateX(-50%)",
                  background: "rgba(0, 0, 0, 0.95)",
                  border: `2px solid ${
                    effectData.isDebuff ? "#d94a4a" : theme.accent
                  }`,
                  borderRadius: "8px",
                  padding: "10px 14px",
                  whiteSpace: "nowrap",
                  fontSize: "14px",
                  zIndex: 100,
                  boxShadow: `0 4px 16px rgba(0, 0, 0, 0.8), 0 0 20px ${
                    effectData.isDebuff ? "rgba(217, 74, 74, 0.5)" : theme.glow
                  }`,
                  pointerEvents: "none",
                }}
              >
                <div
                  style={{
                    fontWeight: "bold",
                    marginBottom: "6px",
                    color: effectData.isDebuff ? "#ff8080" : theme.accent,
                    fontSize: "15px",
                  }}
                >
                  {effectData.nameJa}
                  {buff.value > 0 && ` (${buff.value})`}
                  {buff.stacks > 1 && ` x${buff.stacks}`}
                </div>
                <div style={{ fontSize: "12px", color: "#ccc" }}>
                  {effectData.description}
                </div>
                <div
                  style={{
                    fontSize: "11px",
                    color: "rgba(255, 255, 255, 0.6)",
                    marginTop: "4px",
                    fontStyle: "italic",
                  }}
                >
                  {buff.isPermanent
                    ? "Permanent"
                    : `${buff.duration} turn${
                        buff.duration !== 1 ? "s" : ""
                      } remaining`}
                </div>

                {/* ツールチップの矢印 */}
                <div
                  style={{
                    position: "absolute",
                    bottom: "-8px",
                    left: "50%",
                    transform: "translateX(-50%)",
                    width: "0",
                    height: "0",
                    borderLeft: "8px solid transparent",
                    borderRight: "8px solid transparent",
                    borderTop: `8px solid ${
                      effectData.isDebuff ? "#d94a4a" : theme.accent
                    }`,
                  }}
                />
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
};

export default StatusEffectDisplay;
