import React, { useState, useEffect } from "react";
import type { EnemyDefinition, EnemyAction } from "@/types/characterTypes";
import type { BuffDebuffMap } from "@/types/battleTypes";
import StatusEffectDisplay from "../componentsHtml/BuffEffect";
import { determineEnemyAction } from "@/domain/characters/enemy/logic/enemyAI";
import { GUARD_BAR_DISPLAY_MAX, ENERGY_ANIMATION } from "@/constants";

// Action type determination for icon display
type ActionType = "attack" | "guard" | "debuff" | "special";

const getActionType = (action: EnemyAction): ActionType => {
  if (action.baseDamage > 0) return "attack";
  if (action.guardGain && action.guardGain > 0) return "guard";
  if (action.applyDebuffs && action.applyDebuffs.length > 0) return "debuff";
  return "special";
};

// Placeholder icons - replace with SVG imports when ready
const ActionIcon: React.FC<{ type: ActionType }> = ({ type }) => {
  const iconMap: Record<ActionType, string> = {
    attack: "⚔",
    guard: "🛡",
    debuff: "💀",
    special: "✨",
  };
  return (
    <span className={`action-icon action-icon-${type}`}>
      {/* TODO: Replace with <img src={actionIconSvg} /> */}
      {iconMap[type]}
    </span>
  );
};

interface EnemyState {
  definition: EnemyDefinition;
  hp: number;
  maxHp: number;
  ap: number;
  maxAp: number;
  guard: number;
  buffDebuffs: BuffDebuffMap;
  actEnergy: number;
  turnCount: number;
}

interface EnemyLocateProps {
  enemies: EnemyState[];
  enemyRefs: React.RefObject<HTMLDivElement | null>[];
  theme: {
    primary: string;
    secondary: string;
    accent: string;
    bg: string;
    glow: string;
    hover: string;
  };
  selectedTargetIndex?: number;
  onSelectTarget?: (index: number) => void;
  isPlayerPhase?: boolean;
}

// Individual Enemy Card Component
const EnemyLocate: React.FC<{
  state: EnemyState;
  enemyRef: React.RefObject<HTMLDivElement | null>;
  theme: EnemyLocateProps["theme"];
  size?: "normal" | "small";
  isTargeted?: boolean;
  onClick?: () => void;
  isClickable?: boolean;
}> = ({
  state,
  enemyRef,
  theme,
  size = "normal",
  isTargeted = false,
  onClick,
  isClickable = false,
}) => {
  const [isHovered, setIsHovered] = useState(false);
  const [isConsuming, setIsConsuming] = useState(false);
  const [prevEnergy, setPrevEnergy] = useState(state.actEnergy);

  // Detect energy consumption during render (React recommended pattern)
  if (state.actEnergy !== prevEnergy) {
    setPrevEnergy(state.actEnergy);
    if (state.actEnergy < prevEnergy) {
      setIsConsuming(true);
    }
  }

  // Timer to end animation (reacting to isConsuming change is OK in useEffect)
  useEffect(() => {
    if (isConsuming) {
      const timer = setTimeout(
        () => setIsConsuming(false),
        ENERGY_ANIMATION.CONSUMPTION_TIMEOUT,
      );
      return () => clearTimeout(timer);
    }
  }, [isConsuming]);

  // Preview next action (Ver 4.0)
  const nextAction: EnemyAction = determineEnemyAction(
    state.definition,
    state.hp,
    state.maxHp,
    state.turnCount + 1,
  );

  const actionType = getActionType(nextAction);

  const targetedClass = isTargeted ? "enemy-targeted" : "";
  const clickableClass = isClickable ? "enemy-clickable" : "";

  // Use displayWidth from definition, with fallback based on size
  const widthVw = state.definition.displayWidth ?? (size === "small" ? 18 : 28);

  return (
    <div
      className={`enemy-locate ${targetedClass} ${clickableClass}`}
      style={{ width: `${widthVw}vw` }}
      onClick={isClickable ? onClick : undefined}
    >
      {/* Enemy name with action icon */}
      <div className="enemy-name-row">
        <span className="enemy-name">{state.definition.nameJa}</span>
        <div
          className="action-icon-wrapper"
          onMouseEnter={() => setIsHovered(true)}
          onMouseLeave={() => setIsHovered(false)}
        >
          <ActionIcon type={actionType} />
          {/* Action tooltip on icon hover */}
          {isHovered && (
            <div className="next-action-tooltip">
              <div className="tooltip-header">次の行動</div>
              <div className="tooltip-action-name">{nextAction.name}</div>
              <span className="tooltip-energy">{state.actEnergy}エナジー</span>
              {nextAction.baseDamage > 0 && (
                <div className="tooltip-damage">
                  <span className="damage-icon">⚔️</span>
                  <span className="damage-value">{nextAction.baseDamage}</span>
                  {nextAction.hitCount && nextAction.hitCount > 1 && (
                    <span className="hit-count">×{nextAction.hitCount}</span>
                  )}
                </div>
              )}
              {nextAction.guardGain && nextAction.guardGain > 0 && (
                <div className="tooltip-guard">
                  <span className="guard-icon">🛡️</span>
                  <span className="guard-value">+{nextAction.guardGain}</span>
                </div>
              )}
              {nextAction.applyDebuffs &&
                nextAction.applyDebuffs.length > 0 && (
                  <div className="tooltip-debuffs">
                    {nextAction.applyDebuffs.map((d, i) => (
                      <span key={i} className="debuff-tag">
                        {d.name}
                      </span>
                    ))}
                  </div>
                )}
            </div>
          )}
        </div>
      </div>

      {/* Enemy visual */}
      <div className="enemy-visual" ref={enemyRef}>
        {state.definition.imagePath ? (
          <img
            className="enemy-img"
            src={state.definition.imagePath}
            alt={state.definition.nameJa}
          />
        ) : (
          <img
            className="enemy-img"
            alt="enemy-img"
            src="/assets/images/enemies/depth_1_schelton.png"
          />
        )}
      </div>

      <div className="status-container enemy-status-container">
        {/* HP/AP combined bar - AP overlays on HP */}
        {/* Guard bar - value badge on left */}
        {state.guard > 0 && (
          <div className="status-bar-row guard-row">
            <div className="value-badge guard-badge">{state.guard}</div>
            <div className="unified-bar-container guard-bar">
              <div
                className="bar-fill guard-fill"
                style={{
                  width: `${Math.min(100, (state.guard / GUARD_BAR_DISPLAY_MAX) * 100)}%`,
                }}
              />
            </div>
          </div>
        )}

        <div className="status-bar-row hp-row">
          <div
            {...(state.ap === 0
              ? { className: "break-badge" }
              : { className: "value-badge ap-badge" })}
          >
            {state.ap > 0 && `${state.ap}/${state.maxAp}`}
            {state.ap === 0 && `break!`}
          </div>
          <div
            {...(state.ap > 0
              ? { className: "Armor-border" }
              : { className: "unified-bar-container hp-bar" })}
          >
            {/* AP overlay */}
            {state.ap > 0 && (
              <div
                className="bar-fill ap-overlay"
                style={{ width: `${(state.ap / state.maxAp) * 100}%` }}
              />
            )}
            {/* HP bar */}
            <div
              className="bar-fill hp-fill"
              style={{ width: `${(state.hp / state.maxHp) * 100}%` }}
            />
            <span className="hp-value">
              {state.hp}/{state.maxHp}
            </span>
          </div>
        </div>

        {/* Energy bar - value badge on left */}
        <div className="status-bar-row energy-row">
          <div className="value-badge energy-badge">
            {state.actEnergy}/{state.definition.actEnergy}
          </div>
          <div
            className={`unified-bar-container energy-bar ${isConsuming ? "energy-consuming" : ""}`}
          >
            <div
              className="bar-fill energy-fill"
              style={{
                width: `${(state.actEnergy / state.definition.actEnergy) * 100}%`,
              }}
            />
          </div>
        </div>
        <StatusEffectDisplay buffsDebuffs={state.buffDebuffs} theme={theme} />
      </div>
    </div>
  );
};
const EnemyFrame: React.FC<EnemyLocateProps> = ({
  enemies,
  enemyRefs,
  theme,
  selectedTargetIndex = 0,
  onSelectTarget,
  isPlayerPhase = false,
}) => {
  const enemyCount = enemies.length;
  const hasMultipleEnemies = enemyCount > 1;
  const isClickable = hasMultipleEnemies && isPlayerPhase;

  // determine layout class
  const getLayoutClass = () => {
    switch (enemyCount) {
      case 1:
        return "enemy-layout-single";
      case 2:
        return "enemy-layout-double";
      case 3:
        return "enemy-layout-triple";
      default:
        return "enemy-layout-single";
    }
  };

  return (
    <div className={`enemy-section ${getLayoutClass()}`}>
      {enemyCount === 1 && (
        <EnemyLocate
          state={enemies[0]}
          enemyRef={enemyRefs[0]}
          theme={theme}
          size="normal"
          isTargeted={true}
        />
      )}

      {enemyCount === 2 && (
        <div className="enemy-row">
          {enemies.map((enemy, index) => (
            <EnemyLocate
              key={index}
              state={enemy}
              enemyRef={enemyRefs[index]}
              theme={theme}
              size="small"
              isTargeted={index === selectedTargetIndex}
              isClickable={isClickable}
              onClick={() => onSelectTarget?.(index)}
            />
          ))}
        </div>
      )}

      {enemyCount === 3 && (
        <div className="enemy-row front-row">
          {enemies.map((enemy, index) => (
            <EnemyLocate
              key={index}
              state={enemy}
              enemyRef={enemyRefs[index]}
              theme={theme}
              size="small"
              isTargeted={index === selectedTargetIndex}
              isClickable={isClickable}
              onClick={() => onSelectTarget?.(index)}
            />
          ))}
        </div>
      )}
    </div>
  );
};

export default EnemyFrame;
