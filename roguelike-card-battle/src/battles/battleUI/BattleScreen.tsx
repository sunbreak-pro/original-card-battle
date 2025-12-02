// import { useState } from "react";
import type { Depth } from "../../cards/type/cardType";
import { useBattleLogic } from "../logic/useBattleLogic";
import StatusEffectDisplay from "../../components/StatusEffect";
import { CardComponent } from "../../cards/component/CardComponent"; // ★ 変更: 別ファイルからインポート
import { BattlingCardPileModal } from "../../cards/cardUI/CardModalDisplay"; // ★ 追加
import VictoryScreen from "./VictoryScreen";
import DefeatScreen from "./DefeatScreen";
import "./BattleScreen.css";

// 深度ごとのテーマカラー定義 (JSオブジェクト)
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

  const {
    playerRef,
    enemyRef,
    playerHp,
    playerMaxHp,
    playerShield,
    playerBuffs,
    enemyHp,
    enemyMaxHp,
    enemyShield,
    enemyBuffs,
    energy,
    maxEnergy,
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
    openedPileType,
    openDrawPile,
    openDiscardPile,
    closePileModal,
    battleResult,
    battleStats,
  } = useBattleLogic(depth);

  // 勝利画面の処理
  if (battleResult === "victory") {
    return (
      <VictoryScreen
        onContinue={() => {
          if (onReturnToCamp) onReturnToCamp();
        }}
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

  // 敗北画面の処理
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

  // CSS変数としてテーマカラーを注入
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
      {/* ターンメッセージ */}
      {showTurnMessage && (
        <div className="turn-message-slide">
          <div className="turn-message-text">{turnMessage}</div>
        </div>
      )}

      {/* ヘッダー */}
      <div className="battle-header">
        <div className="depth-info">
          Depth {depth} - Turn {turn}
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

      {/* フィールド */}
      <div className="battle-field">
        {/* 敵 */}
        <div className="character-section">
          <div className="character-name">Shadow Beast</div>
          <div className="character-visual" ref={enemyRef}>
            <img
              className="enemy-image"
              src="/Gemini_Generated_Image_ixo5jrixo5jrixo5.png"
              alt="Shadow Beast"
            />
          </div>
          <div className="status-container">
            {/* シールドがある場合のみ表示 (Playerと同じロジックにする場合) */}
            {enemyShield > 0 && (
              <div className="status-row">
                <span className="status-label shield-num">
                  Shield: {enemyShield}
                </span>
                <span className="bar-frame">
                  <div
                    className="bar-gauge shield"
                    style={{ width: `${(enemyShield / 20) * 100}%` }}
                  ></div>
                </span>
              </div>
            )}
            {/* HPの行 */}
            <div className="status-row">
              <span className="status-label hp-num">HP : {enemyHp}</span>
              <span className=" bar-frame">
                <div
                  className="bar-gauge hp"
                  style={{ width: `${(enemyHp / enemyMaxHp) * 100}%` }}
                ></div>
              </span>
            </div>

            <StatusEffectDisplay buffsDebuffs={enemyBuffs} theme={theme} />
          </div>
        </div>

        {/* プレイヤー */}
        <div className="character-section">
          <div className="character-name">Player</div>
          <div className="character-visual player" ref={playerRef}>
            ⚔️
          </div>
          <div className="status-container">
            {playerShield > 0 && (
              <div className="status-row">
                <span className="status-label shield-num">
                  Shield: {playerShield}
                </span>
                <span className="bar-frame">
                  <div
                    className="bar-gauge shield"
                    style={{ width: `${(playerShield / 20) * 100}%` }}
                  />
                </span>
              </div>
            )}
            <div className="status-row">
              <span className="status-label hp-num">HP: {playerHp}</span>
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
      </div>

      {/* コントロール & デッキ */}
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

      <div className="energy-display">
        <div>ENERGY</div>
        <div className="energy-orbs">
          {Array.from({ length: maxEnergy }).map((_, i) => (
            <div key={i} className={`orb ${i < energy ? "filled" : ""}`} />
          ))}
        </div>
      </div>
      <button className="end-turn-btn" onClick={handleEndTurn}>
        End Turn
      </button>

      {/* 手札 */}
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
                  // CSS変数として値を渡す (単位: deg, vh)
                  "--rot": `${rotation}deg`,
                  "--y": `${translateY * 0.5}vh`,

                  // アニメーション遅延
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
                isPlayable={card.cost <= energy && !isDiscarding}
              />
            </div>
          );
        })}
      </div>

      {/* カードモーダル（山札・捨て札一覧表示） */}
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
