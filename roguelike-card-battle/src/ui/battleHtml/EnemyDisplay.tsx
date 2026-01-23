import React, { useState, useEffect, useRef } from "react";
import type {
  EnemyDefinition,
  EnemyAction,
} from "../../domain/characters/type/enemyType";
import type { BuffDebuffMap } from "../../domain/battles/type/baffType";
import StatusEffectDisplay from "../commonHtml/BuffEffect";
import { determineEnemyAction } from "../../domain/characters/enemy/logic/enemyAI";

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

interface EnemyDisplayProps {
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
}

// Individual Enemy Card Component
const EnemyCard: React.FC<{
  state: EnemyState;
  enemyRef: React.RefObject<HTMLDivElement | null>;
  theme: EnemyDisplayProps["theme"];
  size?: "normal" | "small";
}> = ({ state, enemyRef, theme, size = "normal" }) => {
  const [isHovered, setIsHovered] = useState(false);
  const [_isConsuming, setIsConsuming] = useState(false);
  const prevEnergyRef = useRef(state.actEnergy);

  // Detect energy consumption and trigger animation
  useEffect(() => {
    if (state.actEnergy < prevEnergyRef.current) {
      setIsConsuming(true);
      const timer = setTimeout(() => setIsConsuming(false), 600);
      prevEnergyRef.current = state.actEnergy;
      return () => clearTimeout(timer);
    }
    prevEnergyRef.current = state.actEnergy;
  }, [state.actEnergy]);

  // Preview next action (Ver 4.0)
  const nextAction: EnemyAction = determineEnemyAction(
    state.definition,
    state.hp,
    state.maxHp,
    state.turnCount + 1,
    state.definition.actEnergy, // enemy energy
  );

  const sizeClass = size === "small" ? "enemy-card-small" : "";

  const actionType = getActionType(nextAction);

  return (
    <div className={`enemy-card ${sizeClass}`}>
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
            className="enemy-image"
            src={state.definition.imagePath}
            alt={state.definition.nameJa}
          />
        ) : (
          <div className="enemy-emoji">👹</div>
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
                  width: `${Math.min(100, (state.guard / 30) * 100)}%`,
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
          <div className="unified-bar-container energy-bar">
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
const EnemyDisplay: React.FC<EnemyDisplayProps> = ({
  enemies,
  enemyRefs,
  theme,
}) => {
  const enemyCount = enemies.length;

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
        <EnemyCard
          state={enemies[0]}
          enemyRef={enemyRefs[0]}
          theme={theme}
          size="normal"
        />
      )}

      {enemyCount === 2 && (
        <div className="enemy-row">
          {enemies.map((enemy, index) => (
            <EnemyCard
              key={index}
              state={enemy}
              enemyRef={enemyRefs[index]}
              theme={theme}
              size="small"
            />
          ))}
        </div>
      )}

      {enemyCount === 3 && (
        <>
          <div className="enemy-row-top">
            <EnemyCard
              state={enemies[0]}
              enemyRef={enemyRefs[0]}
              theme={theme}
              size="small"
            />
          </div>
          <div className="enemy-row-bottom">
            <EnemyCard
              state={enemies[1]}
              enemyRef={enemyRefs[1]}
              theme={theme}
              size="small"
            />
            <EnemyCard
              state={enemies[2]}
              enemyRef={enemyRefs[2]}
              theme={theme}
              size="small"
            />
          </div>
        </>
      )}
    </div>
  );
};

export default EnemyDisplay;
