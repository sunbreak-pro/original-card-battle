/**
 * Elemental Resonance Display Component
 *
 * Visual UI for the Mage's elemental resonance system.
 * Shows current element, resonance gauge (3 segments), damage multiplier,
 * and resonance effect badges.
 */

import type { ElementalState, ElementType, ResonanceLevel } from "@/types/characterTypes";
import { RESONANCE_MULTIPLIER } from "@/constants";

// ============================================================================
// Constants
// ============================================================================

const ELEMENT_COLOR_MAP: Record<ElementType, string> = {
  fire: "#ff4500",
  ice: "#00bfff",
  lightning: "#ffd700",
  dark: "#6a0dad",
  light: "#fffacd",
  physics: "#888888",
  guard: "#4488cc",
  summon: "#66cc66",
  enhance: "#cc66cc",
  sacrifice: "#cc3333",
  buff: "#66cccc",
  debuff: "#996633",
  heal: "#66ff66",
  attack: "#cc0000",
  classAbility: "#cc8800",
  chain: "#8888cc",
};

const ELEMENT_LABEL_MAP: Record<ElementType, string> = {
  fire: "火",
  ice: "氷",
  lightning: "雷",
  dark: "闇",
  light: "光",
  physics: "物理",
  guard: "防御",
  summon: "召喚",
  enhance: "強化",
  sacrifice: "犠牲",
  buff: "強化",
  debuff: "弱体",
  heal: "回復",
  attack: "攻撃",
  classAbility: "技能",
  chain: "連鎖",
};

const MAGIC_ELEMENT_SET = new Set<ElementType>(["fire", "ice", "lightning", "dark", "light"]);

/** Effect descriptions for each magic element at resonance levels 1 and 2 */
const EFFECT_TEXT_MAP: Record<string, Record<1 | 2, string>> = {
  fire: { 1: "燃焼 1重/2T", 2: "燃焼 2重/3T + 火フィールド" },
  ice: { 1: "凍結/2T", 2: "凍結/3T + 氷フィールド" },
  lightning: { 1: "-", 2: "スタン/1T + 電フィールド" },
  dark: { 1: "吸命30%", 2: "弱体/3T + 吸命40% + 闇フィールド" },
  light: { 1: "浄化1", 2: "浄化2 + 回復10 + 光フィールド" },
};

const LEVEL_LABELS: Record<ResonanceLevel, string> = {
  0: "共鳴なし",
  1: "共鳴",
  2: "大共鳴",
};

// ============================================================================
// Component
// ============================================================================

interface ElementalResonanceDisplayProps {
  elementalState: ElementalState;
}

export function ElementalResonanceDisplay({ elementalState }: ElementalResonanceDisplayProps) {
  const { lastElement, resonanceLevel } = elementalState;
  const hasElement = lastElement !== null && MAGIC_ELEMENT_SET.has(lastElement);
  const color = hasElement ? ELEMENT_COLOR_MAP[lastElement] : "#666666";
  const multiplier = RESONANCE_MULTIPLIER[resonanceLevel];

  const effectText = hasElement && resonanceLevel > 0
    ? EFFECT_TEXT_MAP[lastElement]?.[resonanceLevel as 1 | 2] ?? ""
    : resonanceLevel === 0 && hasElement
      ? "待機中"
      : "共鳴なし";

  const isLightElement = lastElement === "light";

  return (
    <div
      className={`elemental-resonance-display resonance-level-${resonanceLevel}`}
      style={{ "--resonance-color": color } as React.CSSProperties}
    >
      {/* Element indicator */}
      <div className="resonance-element-indicator">
        {hasElement ? (
          <>
            <img
              className={`resonance-element-icon ${resonanceLevel >= 1 ? "pulsing" : ""} ${resonanceLevel >= 2 ? "max-glow" : ""}`}
              src={`/assets/images/elements/element-${lastElement}.png`}
              alt={ELEMENT_LABEL_MAP[lastElement]}
            />
            <span
              className={`resonance-element-name ${isLightElement ? "light-element-text" : ""}`}
              style={{ color }}
            >
              {ELEMENT_LABEL_MAP[lastElement]}属性
            </span>
          </>
        ) : (
          <>
            <div className="resonance-element-icon idle" />
            <span className="resonance-element-name idle-text">---</span>
          </>
        )}
      </div>

      {/* Resonance gauge - 3 segments */}
      <div className="resonance-gauge">
        {([0, 1, 2] as const).map((seg) => (
          <div
            key={seg}
            className={`resonance-segment ${seg <= resonanceLevel && hasElement ? "lit" : ""} ${seg === 2 && resonanceLevel === 2 ? "max-pulse" : ""} ${seg === 0 && hasElement ? "base-lit" : ""}`}
          />
        ))}
      </div>

      {/* Info row: multiplier + level label */}
      <div className="resonance-info-row">
        {hasElement && (
          <span className="resonance-multiplier">
            x{multiplier.toFixed(2)}
            {resonanceLevel === 2 && <span className="crit-bonus"> +Crit10%</span>}
          </span>
        )}
        <span className={`resonance-level-text ${isLightElement ? "light-element-text" : ""}`}>
          {LEVEL_LABELS[resonanceLevel]}
        </span>
      </div>

      {/* Effect badge */}
      <div className={`resonance-effect-badge ${resonanceLevel >= 1 ? "active" : ""}`}>
        {effectText}
      </div>
    </div>
  );
}
