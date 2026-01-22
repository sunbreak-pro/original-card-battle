import { useState, useEffect, useRef } from "react";
import type { Depth } from "../../domain/cards/type/cardType";
import { useBattle } from "../../domain/battles/managements/battleFlowManage";
import { selectRandomEnemy } from "../../domain/characters/enemy/logic/enemyAI";
import { CardComponent } from "../cardUI/CardComponent";
import { BattlingCardPileModal } from "../cardUI/CardModalDisplay";
import { TurnOrderIndicator } from "./TurnOrderIndicator";
import StatusEffectDisplay from "../commonUI/BuffEffect";
import EnemyDisplay from "../enemyUI/EnemyDisplay";
import VictoryScreen from "./VictoryScreen";
import DefeatScreen from "./DefeatScreen";
import "../css/BattleScreen.css";
import { depthThemes } from "../../domain/dungeon/depth/deptManager";
import { usePlayer } from "../../domain/camps/contexts/PlayerContext";
import { handlePlayerDeath } from "../../domain/battles/logic/deathHandler";

const BattleScreen = ({
  depth,
  onDepthChange,
  onReturnToCamp,
  onWin,
  onLose,
}: {
  depth: Depth;
  onDepthChange: (d: Depth) => void;
  onReturnToCamp?: () => void;
  onWin?: () => void;
  onLose?: () => void;
}) => {
  const theme = depthThemes[depth];
  const { player, updatePlayer } = usePlayer();

  // 遭遇カウント管理
  const [encounterCount, setEncounterCount] = useState(0);
  const deathHandledRef = useRef(false);

  const {
    playerRef,
    aliveEnemies,
    playerName,
    playerClass,
    playerHp,
    playerMaxHp,
    playerAp,
    playerMaxAp,
    playerGuard,
    playerBuffs,
    cardEnergy,
    maxEnergy,
    swordEnergy,
    phaseCount,
    turnMessage,
    showTurnMessage,
    hand,
    drawPile,
    discardPile,
    isNewCard,
    getDiscardingCards,
    handleCardPlay,
    handleEndPhase,
    resetForNextEnemy,
    openedPileType,
    openDrawPile,
    openDiscardPile,
    closePileModal,
    battleResult,
    battleStats,
    phaseQueue,
    currentPhaseIndex,
  } = useBattle(depth);

  // Handle player death penalty when defeated
  // IMPORTANT: This useEffect must be BEFORE any early returns to follow React's Rules of Hooks
  useEffect(() => {
    if (battleResult === "defeat" && !deathHandledRef.current) {
      // Apply death penalty
      const updatedPlayer = handlePlayerDeath(player);
      updatePlayer(updatedPlayer);
      deathHandledRef.current = true;
    }
  }, [battleResult, player, updatePlayer]);

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
    // If onWin callback is provided (dungeon mode), use it instead of continuing to next battle
    const handleVictoryContinue = onWin || handleContinueToNextBattle;

    return (
      <VictoryScreen
        onContinue={handleVictoryContinue}
        rewards={{
          gold: 100 + phaseCount * 10,
          experience: 50 + phaseCount * 5,
          cards: [],
        }}
        battleStats={{
          turnCount: phaseCount,
          damageDealt: battleStats.damageDealt,
          damageTaken: battleStats.damageTaken,
        }}
      />
    );
  }

  if (battleResult === "defeat") {
    // If onLose callback is provided (dungeon mode), use it for return to camp
    const handleDefeatReturnToCamp = () => {
      if (onLose) {
        onLose();
      } else if (onReturnToCamp) {
        onReturnToCamp();
      }
    };

    return (
      <DefeatScreen
        onRetry={() => window.location.reload()}
        onReturnToCamp={handleDefeatReturnToCamp}
        battleStats={{
          turnCount: phaseCount,
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
    backgroundImage: `linear-gradient(${theme.bg}), url('/assets/images/depth_${depth}-background.png')`,
    backgroundSize: 'cover',
    backgroundPosition: 'center',
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
          {depth}-{encounterCount === 6 ? "BOSS" : encounterCount + 1} | Phase{" "}
          {phaseCount}
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
      {/* Phase system - TurnOrderIndicator v5.0 horizontal */}
      <TurnOrderIndicator
        phaseQueue={phaseQueue}
        currentPhaseIndex={currentPhaseIndex}
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
            turnCount: phaseCount,
          }))}
          enemyRefs={aliveEnemies.map((e) => e.ref)}
          theme={theme}
        />
        <div className="player-section">
          <div className="player-field">
            <div className="character-name">
              {playerName} [{playerClass}]
            </div>
            <div className="character-visual player" ref={playerRef}>
              ⚔️
            </div>
            <div className="status-container player-status-container">
              {/* HP/AP combined bar - AP overlays on HP */}
              {/* Guard bar - value badge on left */}
              {playerGuard > 0 && (
                <div className="status-bar-row guard-row">
                  <div className="value-badge guard-badge">{playerGuard}</div>
                  <div className="unified-bar-container guard-bar">
                    <div
                      className="bar-fill guard-fill"
                      style={{
                        width: `${Math.min(100, (playerGuard / 30) * 100)}%`,
                      }}
                    />
                  </div>
                </div>
              )}

              <div className="status-bar-row hp-row">
                <div
                  {...(playerAp === 0
                    ? { className: "break-badge" }
                    : { className: "value-badge ap-badge" })}
                >
                  {" "}
                  {playerAp > 0 && `${playerAp}/${playerMaxAp}`}
                  {playerAp === 0 && `break!`}
                </div>

                <div
                  {...(playerAp > 0
                    ? { className: "Armor-border" }
                    : { className: "unified-bar-container hp-bar" })}
                >
                  {/* AP overlay */}
                  {playerAp > 0 && (
                    <div
                      className="bar-fill ap-overlay"
                      style={{ width: `${(playerAp / playerMaxAp) * 100}%` }}
                    />
                  )}
                  {/* HP bar */}
                  <div
                    className="bar-fill hp-fill"
                    style={{ width: `${(playerHp / playerMaxHp) * 100}%` }}
                  />
                  <span className="hp-value">
                    {playerHp}/{playerMaxHp}
                  </span>
                </div>
              </div>

              {/* Energy bar - value badge on left */}
              <div className="status-bar-row energy-row">
                <div className="value-badge energy-badge">
                  {cardEnergy}/{maxEnergy}
                </div>
                <div className="unified-bar-container energy-bar">
                  <div
                    className="bar-fill energy-fill"
                    style={{ width: `${(cardEnergy / maxEnergy) * 100}%` }}
                  />
                </div>
              </div>
              <StatusEffectDisplay buffsDebuffs={playerBuffs} theme={theme} />
            </div>
          </div>
          <div className="energy-and-ability">
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

      <button className="end-turn-btn" onClick={handleEndPhase}>
        End Phase
      </button>
      <div className="hand-container">
        {hand.map((card, index) => {
          const isDrawing = isNewCard(card.id);
          const isDiscarding = getDiscardingCards().some(
            (c) => c.id === card.id,
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
