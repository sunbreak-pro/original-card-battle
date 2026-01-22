// GuildBattleScreen: Dedicated battle screen for promotion exams
// Simplified version of BattleScreen.tsx without depth/encounter progression

import { useEffect } from "react";
import type { Enemy } from "../../../domain/characters/type/enemyType";
import { useBattle } from "../../../domain/battles/managements/battleFlowManage";
import { CardComponent } from "../../cardHtml/CardComponent";
import { BattlingCardPileModal } from "../../cardHtml/CardModalDisplay";
import { TurnOrderIndicator } from "../../battleHtml/TurnOrderIndicator";
import StatusEffectDisplay from "../../commonHtml/BuffEffect";
import EnemyDisplay from "../../battleHtml/EnemyDisplay";
import "../../css/BattleScreen.css";

interface GuildBattleScreenProps {
  examEnemy: Enemy;
  onWin: () => void;
  onLose: () => void;
}

const GuildBattleScreen = ({
  examEnemy,
  onWin,
  onLose,
}: GuildBattleScreenProps) => {
  // Arena theme (similar to depth 1 but with guild colors)
  const theme = {
    primary: "#8B4513", // Brown/bronze for guild arena
    secondary: "#CD853F", // Peru
    accent: "#DAA520", // Goldenrod
    bg: "#2C1810", // Dark brown
    glow: "#FFD700", // Gold
    hover: "#FFEC8B", // Light gold
  };

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
    openedPileType,
    openDrawPile,
    openDiscardPile,
    closePileModal,
    battleResult,
    phaseQueue,
    currentPhaseIndex,
  } = useBattle(1, [examEnemy]); // Fixed depth=1, specific enemy

  // Handle battle result callbacks
  useEffect(() => {
    if (battleResult === "victory") {
      onWin();
    } else if (battleResult === "defeat") {
      onLose();
    }
  }, [battleResult, onWin, onLose]);

  // Don't render victory/defeat screens - let parent handle that
  if (battleResult === "victory" || battleResult === "defeat") {
    return null;
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

      {/* Header - simplified for exam */}
      <div className="battle-header">
        <div className="depth-info">Promotion Exam | Phase {phaseCount}</div>
        <div className="exam-enemy-name">
          vs {examEnemy.nameJa || examEnemy.name}
        </div>
      </div>

      {/* Phase system - TurnOrderIndicator */}
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
              {/* Guard bar */}
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

              {/* HP/AP bar */}
              <div className="status-bar-row hp-row">
                <div
                  {...(playerAp === 0
                    ? { className: "break-badge" }
                    : { className: "value-badge ap-badge" })}
                >
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

              {/* Energy bar */}
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

          {/* Sword Energy (if applicable) */}
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

      {/* Draw and Discard piles */}
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

      {/* End Phase button */}
      <button className="end-turn-btn" onClick={handleEndPhase}>
        End Phase
      </button>

      {/* Hand */}
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
                depth={1}
                isPlayable={card.cost <= cardEnergy && !isDiscarding}
              />
            </div>
          );
        })}
      </div>

      {/* Pile modal */}
      <BattlingCardPileModal
        isOpen={openedPileType !== null}
        onClose={closePileModal}
        title={openedPileType === "draw" ? "山札" : "捨て札"}
        cards={openedPileType === "draw" ? drawPile : discardPile}
        depth={1}
      />
    </div>
  );
};

export default GuildBattleScreen;
