import React, { useState } from "react";
import type { Enemy, EnemyAction } from "../../Character/data/EnemyData";
import type { BuffDebuffMap } from "../../cards/type/baffType";
import StatusEffectDisplay from "../../components/StatusEffect";
import { determineEnemyAction } from "../logic/enemyAI";

interface EnemyState {
  enemy: Enemy;
  hp: number;
  maxHp: number;
  ap: number;
  maxAp: number;
  guard: number;
  buffs: BuffDebuffMap;
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

  // Preview next action (Ver 4.0)
  const nextAction: EnemyAction = determineEnemyAction(
    state.enemy,
    state.hp,
    state.maxHp,
    state.turnCount + 1,
    state.enemy.actEnergy // enemy energy
  );

  const sizeClass = size === "small" ? "enemy-card-small" : "";

  return (
    <div
      className={`enemy-card ${sizeClass}`}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
    >
      <div className="enemy-name">{state.enemy.nameJa}</div>
      <div className="enemy-visual" ref={enemyRef}>
        {state.enemy.imagePath ? (
          <img
            className="enemy-image"
            src={state.enemy.imagePath}
            alt={state.enemy.nameJa}
          />
        ) : (
          <div className="enemy-emoji">👹</div>
        )}

        {/* next action tooltip */}
        {isHovered && (
          <div className="next-action-tooltip">
            <div className="tooltip-header">次の行動</div>
            <div className="tooltip-action-name">{nextAction.name}</div>
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
            {nextAction.applyDebuffs && nextAction.applyDebuffs.length > 0 && (
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

      <div className="enemy-status">
        {/* Guard */}
        {state.guard > 0 && (
          <div className="status-row">
            <span className="status-label guard-num">Guard: {state.guard}</span>
            <span className="bar-frame">
              <div
                className="bar-gauge guard"
                style={{ width: `${Math.min(100, (state.guard / 30) * 100)}%` }}
              />
            </span>
          </div>
        )}
        {/* AP */}
        {state.ap > 0 && (
          <div className="status-row">
            <span className="status-label ap-num">
              AP: {state.ap}/{state.maxAp}
            </span>
            <span className="bar-frame">
              <div
                className="bar-gauge ap"
                style={{ width: `${(state.ap / state.maxAp) * 100}%` }}
              />
            </span>
          </div>
        )}
        {/* HP */}
        <div className="status-row">
          <span className="status-label hp-num">
            HP: {state.hp}/{state.maxHp}
          </span>
          <span className="bar-frame">
            <div
              className="bar-gauge hp"
              style={{ width: `${(state.hp / state.maxHp) * 100}%` }}
            />
          </span>
        </div>

        {/* Enemy Energy Orbs (Ver 4.0) */}
        <div className="enemy-energy-orbs">
          {Array.from({ length: state.enemy.actEnergy }).map((_, i) => (
            <div key={i} className="energy-orb filled" />
          ))}
        </div>

        <StatusEffectDisplay buffsDebuffs={state.buffs} theme={theme} />
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
