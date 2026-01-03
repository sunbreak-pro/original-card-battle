import { useState } from "react";
import type { Depth } from "../../cards/type/cardType";
import { useBattleLogic } from "../logic/useBattleLogic";
import { selectRandomEnemy } from "../logic/enemyAI";
import StatusEffectDisplay from "../../components/StatusEffect";
import { CardComponent } from "../../cards/cardUI/CardComponent";
import { BattlingCardPileModal } from "../../cards/cardUI/CardModalDisplay";
import EnemyDisplay from "./EnemyDisplay";
import { EnemyActionPreview } from "./EnemyActionPreview";
import { TurnOrderIndicator } from "./TurnOrderIndicator";
import VictoryScreen from "./VictoryScreen";
import DefeatScreen from "./DefeatScreen";
import "./UIcss/BattleScreen.css";
const depthThemes = {
  1: {
    primary: "#1a3326",
    secondary: "#2d5f3f",
    accent: "#4a9d6d",
    bg: "linear-gradient(135deg, #050a08 0%, #0a1410 100%)",
    glow: "rgba(74, 157, 109, 0.25)",
    hover: "#96fabfff",
  },
  2: {
    primary: "#1a2640",
    secondary: "#2e4a7c",
    accent: "#4a7fd9",
    bg: "linear-gradient(135deg, #030509 0%, #060e18 100%)",
    glow: "rgba(74, 127, 217, 0.25)",
    hover: "#5086e2ff",
  },
  3: {
    primary: "#401a1a",
    secondary: "#7c2e2e",
    accent: "#d94a4a",
    bg: "linear-gradient(135deg, #0a0303 0%, #180808 100%)",
    glow: "rgba(217, 74, 74, 0.25)",
    hover: "#e44e4eff",
  },
  4: {
    primary: "#2d1a40",
    secondary: "#5a2e7c",
    accent: "#9a4ad9",
    bg: "linear-gradient(135deg, #050308 0%, #0d0618 100%)",
    glow: "rgba(154, 74, 217, 0.25)",
    hover: "#a34fe3ff",
  },
  5: {
    primary: "#1a0a0f",
    secondary: "#331419",
    accent: "#8f1f3d",
    bg: "linear-gradient(135deg, #000000 0%, #0a0305 100%)",
    glow: "rgba(143, 31, 61, 0.3)",
    hover: "#bc3d5fff",
  },
};

const BattleScreen = ({
  depth,
  onDepthChange,
  onReturnToCamp,
}: {
  depth: Depth;
  onDepthChange: (d: Depth) => void;
  onReturnToCamp?: () => void;
}) => {
  const theme = depthThemes[depth];

  // 遭遇カウント管理
  const [encounterCount, setEncounterCount] = useState(0);

  const {
    playerRef,
    aliveEnemies,
    playerName,
    playerClassName,
    playerHp,
    playerMaxHp,
    playerAp,
    playerMaxAp,
    playerGuard,
    playerBuffs,
    cardEnergy,
    maxEnergy,
    swordEnergy,
    enemyEnergy,
    nextEnemyActions,
    turn,
    turnMessage,
    showTurnMessage,
    hand,
    drawPile,
    discardPile,
    isNewCard,
    getDiscardingCards,
    handleCardPlay,
    handleEndTurn,
    resetForNextEnemy,
    openedPileType,
    openDrawPile,
    openDiscardPile,
    closePileModal,
    battleResult,
    battleStats,
    playerNowSpeed,
    enemyNowSpeed,
    turnOrder,
    speedBonusPlayer,
    speedBonusEnemy,
  } = useBattleLogic(depth);

  const handleContinueToNextBattle = () => {
    const nextEncounter = encounterCount + 1;
    setEncounterCount(nextEncounter);

    let encounterType: "normal" | "group" | "boss" = "normal";
    if (nextEncounter === 7) {
      encounterType = "boss";
    } else if (nextEncounter % 3 === 0) {
      encounterType = "group";
    }
    const { enemies: nextEnemies } = selectRandomEnemy(depth, encounterType);
    resetForNextEnemy(nextEnemies);
  };
  if (battleResult === "victory") {
    return (
      <VictoryScreen
        onContinue={handleContinueToNextBattle}
        rewards={{
          gold: 100 + turn * 10,
          experience: 50 + turn * 5,
          cards: [],
        }}
        battleStats={{
          turnCount: turn,
          damageDealt: battleStats.damageDealt,
          damageTaken: battleStats.damageTaken,
        }}
      />
    );
  }
  if (battleResult === "defeat") {
    return (
      <DefeatScreen
        onRetry={() => window.location.reload()}
        onReturnToCamp={() => {
          if (onReturnToCamp) onReturnToCamp();
        }}
        battleStats={{
          turnCount: turn,
          damageDealt: battleStats.damageDealt,
          damageTaken: battleStats.damageTaken,
        }}
      />
    );
  }
  const containerStyle = {
    "--theme-primary": theme.primary,
    "--theme-secondary": theme.secondary,
    "--theme-accent": theme.accent,
    "--theme-bg": theme.bg,
    "--theme-glow": theme.glow,
    "--theme-hover": theme.accent,
  } as React.CSSProperties;

  return (
    <div className="battle-screen" style={containerStyle}>
      {showTurnMessage && (
        <div className="turn-message-slide">
          <div className="turn-message-text">{turnMessage}</div>
        </div>
      )}
      <div className="battle-header">
        <div className="depth-info">
          {depth}-{encounterCount === 6 ? "BOSS" : encounterCount + 1} | Turn{" "}
          {turn}
        </div>
        <div className="depth-controls">
          {[1, 2, 3, 4, 5].map((d) => (
            <button
              key={d}
              className={`depth-btn ${depth === d ? "active" : ""}`}
              onClick={() => onDepthChange(d as Depth)}
            >
              D{d}
            </button>
          ))}
        </div>
      </div>
      <TurnOrderIndicator
        playerSpeed={playerNowSpeed}
        enemySpeed={enemyNowSpeed}
        turnOrder={turnOrder}
        playerBonus={speedBonusPlayer}
        enemyBonus={speedBonusEnemy}
      />
      <EnemyActionPreview
        actions={nextEnemyActions}
        enemyEnergy={enemyEnergy}
      />
      <div className="battle-field">
        <EnemyDisplay
          enemies={aliveEnemies.map((e) => ({
            enemy: e.enemy,
            hp: e.hp,
            maxHp: e.enemy.maxHp,
            ap: e.ap,
            maxAp: e.enemy.maxAp,
            guard: e.guard,
            actEnergy: e.energy,
            buffs: e.buffs,
            turnCount: turn,
          }))}
          enemyRefs={aliveEnemies.map((e) => e.ref)}
          theme={theme}
        />
        <div className="player-section">
          <div className="player-field">
            <div className="character-name">
              {playerName} [{playerClassName}]
            </div>
            <div className="character-visual player" ref={playerRef}>
              ⚔️
            </div>
            <div className="status-container">
              {playerGuard > 0 && (
                <div className="status-row">
                  <span className="status-label guard-num">
                    Guard: {playerGuard}
                  </span>
                  <span className="bar-frame">
                    <div
                      className="bar-gauge guard"
                      style={{
                        width: `${Math.min(100, (playerGuard / 30) * 100)}%`,
                      }}
                    />
                  </span>
                </div>
              )}
              {/* APの行 */}
              <div className="status-row">
                <span className="status-label ap-num">
                  AP: {playerAp}/{playerMaxAp}
                </span>
                <span className="bar-frame">
                  <div
                    className="bar-gauge ap"
                    style={{ width: `${(playerAp / playerMaxAp) * 100}%` }}
                  />
                </span>
              </div>
              <div className="status-row">
                <span className="status-label hp-num">
                  HP: {playerHp}/{playerMaxHp}
                </span>
                <span className="bar-frame">
                  <div
                    className="bar-gauge hp"
                    style={{ width: `${(playerHp / playerMaxHp) * 100}%` }}
                  />
                </span>
              </div>
              <StatusEffectDisplay buffsDebuffs={playerBuffs} theme={theme} />
            </div>
          </div>
          <div className="energy-and-ability">
            <div className="energy-display">
              <div>ENERGY</div>
              <div className="energy-orbs">
                {Array.from({ length: maxEnergy }).map((_, i) => (
                  <div
                    key={i}
                    className={`orb ${i < cardEnergy ? "filled" : ""}`}
                  />
                ))}
              </div>
            </div>
            <div className="sword-energy-display">
              <div className="sword-energy-label">剣気:</div>

              <div className="sword-energy-bar-container">
                <div className="sword-energy-bar">
                  <div
                    className={`sword-energy-fill ${
                      swordEnergy.current >= 10
                        ? "level-max"
                        : swordEnergy.current >= 8
                        ? "level-high"
                        : swordEnergy.current >= 5
                        ? "level-mid"
                        : ""
                    }`}
                    style={{
                      width: `${
                        (swordEnergy.current / swordEnergy.max) * 100
                      }%`,
                    }}
                  />
                  <span className="sword-energy-text">
                    {swordEnergy.current}/{swordEnergy.max}
                  </span>
                </div>
              </div>
              <div className="sword-energy-effects">
                <span
                  className={`effect-badge crit ${
                    swordEnergy.current >= 5 ? "active" : "inactive"
                  }`}
                >
                  {swordEnergy.current >= 5 ? "✓" : "○"} Crit+20%
                </span>
                <span
                  className={`effect-badge pierce ${
                    swordEnergy.current >= 8 ? "active" : "inactive"
                  }`}
                >
                  {swordEnergy.current >= 8 ? "✓" : "○"} 貫通+30%
                </span>
                <span
                  className={`effect-badge max ${
                    swordEnergy.current >= 10 ? "active" : "inactive"
                  }`}
                >
                  {swordEnergy.current >= 10 ? "✓" : "○"} MAX
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
      <div className="pile-icon draw" title="Draw Pile" onClick={openDrawPile}>
        <div className="pile-visual">🎴</div>
        <div className="pile-count">山札: {drawPile.length}</div>
      </div>
      <div
        className="pile-icon discard"
        title="Discard Pile"
        onClick={openDiscardPile}
      >
        <div className="pile-visual">🗑️</div>
        <div className="pile-count">捨て札: {discardPile.length}</div>
      </div>

      <button className="end-turn-btn" onClick={handleEndTurn}>
        End Turn
      </button>
      <div className="hand-container">
        {hand.map((card, index) => {
          const isDrawing = isNewCard(card.id);
          const isDiscarding = getDiscardingCards().some(
            (c) => c.id === card.id
          );

          const totalCards = hand.length;
          const offset = index - (totalCards - 1) / 2;
          const translateY = Math.abs(offset) * 1.5 - 1.8;
          const rotation = offset * 4.2;

          return (
            <div
              key={card.id}
              className={`card-wrapper ${isDrawing ? "drawing" : ""} ${
                isDiscarding ? "discarding" : ""
              }`}
              style={
                {
                  "--rot": `${rotation}deg`,
                  "--y": `${translateY * 0.5}vh`,
                  animationDelay: isDrawing
                    ? `${index * 0.1}s`
                    : isDiscarding
                    ? `${index * 0.05}s`
                    : "0s",
                } as React.CSSProperties
              }
              onClick={(e) => handleCardPlay(card, e.currentTarget)}
            >
              <CardComponent
                card={card}
                depth={depth}
                isPlayable={card.cost <= cardEnergy && !isDiscarding}
              />
            </div>
          );
        })}
      </div>
      <BattlingCardPileModal
        isOpen={openedPileType !== null}
        onClose={closePileModal}
        title={openedPileType === "draw" ? "山札" : "捨て札"}
        cards={openedPileType === "draw" ? drawPile : discardPile}
        depth={depth}
      />
    </div>
  );
};

export default BattleScreen;
