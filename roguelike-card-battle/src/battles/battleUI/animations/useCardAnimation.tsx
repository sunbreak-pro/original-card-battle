import { useState } from "react";
import type { Card } from "../../../cards/type/cardType";
import {
  animateAsync,
  Easing,
  createParticles,
  showDamageText,
  shakeElement,
} from "./animationEngine";

/**
 * カードアニメーション用のカスタムフック（強化版）
 * 新しいアニメーションエンジンを使用した滑らかなアニメーション
 */
export const useCardAnimation = () => {
  // 破棄中のカード（破棄アニメーション用）
  const [discardingCards, setDiscardingCards] = useState<Card[]>([]);
  // 新しくドローされたカードのIDセット（ドローアニメーション用）
  const [newCardIds, setNewCardIds] = useState<Set<string>>(new Set());

  /**
   * カードを1枚ずつドローするアニメーション（強化版）
   * 1枚ずつフェードインさせる
   */
  const drawCardsWithAnimation = async (
    cards: Card[],
    onAllCardsDrawn: (cards: Card[]) => void,
    interval: number = 150 // カード間の出現間隔
  ): Promise<void> => {
    // 1. まず、アニメーション対象の全カードIDを「ドロー中」としてマークする
    //    これにより、BattleScreen.tsx で .drawing クラスが付与されます。
    const newIds = new Set(cards.map((c) => c.id));
    setNewCardIds((prev) => new Set([...prev, ...newIds]));

    onAllCardsDrawn(cards);

    const animationDuration = 800; // CSSの0.8s
    const totalDuration = animationDuration + (cards.length - 1) * interval;

    await new Promise((resolve) => setTimeout(resolve, totalDuration));

    // 4. アニメーション完了後、「ドロー中」のマークを解除する
    setNewCardIds((prev) => {
      const next = new Set(prev);
      cards.forEach((c) => next.delete(c.id));
      return next;
    });
  };

  /**
   * カード要素に対してドローアニメーションを適用
   */
  const applyDrawAnimation = async (element: HTMLElement): Promise<void> => {
    // 山札の位置を取得（右下）
    const container = element.closest(".battle-screen") as HTMLElement;
    if (!container) return;

    const containerRect = container.getBoundingClientRect();
    const elementRect = element.getBoundingClientRect();

    // 右下の山札位置から開始
    const startX = containerRect.width - 100;
    const startY = containerRect.height - 150;
    const endX = elementRect.left - containerRect.left;
    const endY = elementRect.top - containerRect.top;

    // 初期状態を設定
    element.style.position = "absolute";
    element.style.left = `${startX}px`;
    element.style.top = `${startY}px`;
    element.style.opacity = "0";
    element.style.transform = "scale(0.1) rotate(20deg)";
    element.style.zIndex = "100";

    // アニメーション実行
    await animateAsync({
      element,
      duration: 600,
      easing: Easing.easeOutBack,
      to: {
        left: `${endX}px`,
        top: `${endY}px`,
        opacity: "1",
        transform: "scale(1) rotate(0deg)",
      } as Partial<CSSStyleDeclaration>,
    });

    // アニメーション後、positionをリセット
    element.style.position = "";
    element.style.left = "";
    element.style.top = "";
    element.style.zIndex = "";
  };

  /**
   * カードを破棄するアニメーション（右側から徐々に）
   */
  const discardCardsWithAnimation = async (
    cards: Card[],
    interval: number = 100,
    onComplete?: () => void // ★ 修正: コールバックを追加
  ): Promise<void> => {
    if (cards.length === 0) {
      if (onComplete) onComplete(); // カードがない場合は即時完了
      return;
    }

    // 右側のカードから順番に
    const reversedCards = [...cards].reverse();

    for (let i = 0; i < reversedCards.length; i++) {
      const card = reversedCards[i];

      // 破棄リストに追加
      setDiscardingCards((prev) => [...prev, card]);

      // 最後のカード以外は間隔を待機
      if (i < reversedCards.length - 1) {
        await new Promise((resolve) => setTimeout(resolve, interval));
      }
    }

    // 最後のカードのアニメーション完了を待つ
    await new Promise((resolve) => setTimeout(resolve, 500));
    // 全てクリア
    setDiscardingCards([]);
    if (onComplete) {
      onComplete();
    }
  };

  /**
   * カード要素に対して破棄アニメーションを適用
   */
  const applyDiscardAnimation = async (element: HTMLElement): Promise<void> => {
    const container = element.closest(".battle-screen") as HTMLElement;
    if (!container) return;

    const containerRect = container.getBoundingClientRect();
    const elementRect = element.getBoundingClientRect();

    // 現在位置
    const startX = elementRect.left - containerRect.left;
    const startY = elementRect.top - containerRect.top;

    // 捨て札の位置（左下）
    const endX = 100;
    const endY = containerRect.height - 150;

    // 初期位置を設定
    element.style.position = "absolute";
    element.style.left = `${startX}px`;
    element.style.top = `${startY}px`;
    element.style.zIndex = "100";

    // パーティクルエフェクト
    createParticles({
      container,
      x: startX + 80,
      y: startY + 100,
      count: 10,
      color: "rgba(100, 100, 255, 0.6)",
      size: 3,
      spread: 50,
    });

    // アニメーション実行
    await animateAsync({
      element,
      duration: 500,
      easing: Easing.easeInCubic,
      to: {
        left: `${endX}px`,
        top: `${endY}px`,
        opacity: "0",
        transform: "scale(0.1) rotate(-25deg)",
      } as Partial<CSSStyleDeclaration>,
    });
  };

  /**
   * カードプレイアニメーション（敵に向かって飛んでいく）
   */
  const playCardWithAnimation = async (
    element: HTMLElement,
    targetElement: HTMLElement,
    onComplete: () => void
  ): Promise<void> => {
    const container = element.closest(".battle-screen") as HTMLElement;
    if (!container) {
      onComplete();
      return;
    }

    const containerRect = container.getBoundingClientRect();
    const elementRect = element.getBoundingClientRect();
    const targetRect = targetElement.getBoundingClientRect();

    // 開始位置
    const startX = elementRect.left - containerRect.left;
    const startY = elementRect.top - containerRect.top;

    // ターゲット位置（敵の中心）
    const endX = targetRect.left - containerRect.left + targetRect.width / 2;
    const endY = targetRect.top - containerRect.top + targetRect.height / 2;

    // カードをクローン
    const clone = element.cloneNode(true) as HTMLElement;
    clone.style.position = "absolute";
    clone.style.left = `${startX}px`;
    clone.style.top = `${startY}px`;
    clone.style.zIndex = "200";
    clone.style.pointerEvents = "none";
    container.appendChild(clone);

    // 元のカードを非表示
    element.style.opacity = "0";

    // トレイルエフェクト（軌跡）
    const createTrail = () => {
      const trail = clone.cloneNode(true) as HTMLElement;
      trail.style.opacity = "0.3";
      trail.style.filter = "blur(2px)";
      trail.style.zIndex = "199";
      container.appendChild(trail);

      setTimeout(() => {
        trail.remove();
      }, 200);
    };

    const trailInterval = setInterval(createTrail, 50);

    // アニメーション実行
    await animateAsync({
      element: clone,
      duration: 400,
      easing: Easing.easeInQuad,
      to: {
        left: `${endX}px`,
        top: `${endY}px`,
        transform: "scale(0.5) rotate(360deg)",
        opacity: "0.8",
      } as Partial<CSSStyleDeclaration>,
    });

    clearInterval(trailInterval);

    // 衝撃エフェクト
    createParticles({
      container,
      x: endX,
      y: endY,
      count: 30,
      color: "rgba(255, 200, 100, 0.8)",
      size: 5,
      spread: 150,
      gravity: 1,
    });

    // 敵をシェイク
    shakeElement(targetElement, 15, 300);

    // クリーンアップ
    clone.remove();
    element.style.opacity = "";

    onComplete();
  };

  /**
   * ダメージ表示エフェクト
   */
  const showDamageEffect = (
    targetElement: HTMLElement,
    damage: number,
    isCritical: boolean = false
  ): void => {
    const container = targetElement.closest(".battle-screen") as HTMLElement;
    if (!container) return;

    const containerRect = container.getBoundingClientRect();
    const targetRect = targetElement.getBoundingClientRect();

    const x = targetRect.left - containerRect.left + targetRect.width / 2;
    const y = targetRect.top - containerRect.top + targetRect.height / 2;

    showDamageText({
      container,
      x,
      y,
      value: damage,
      color: isCritical ? "#ff6600" : "#ff4444",
      isCritical,
    });

    // シェイク
    shakeElement(targetElement, isCritical ? 20 : 10, 300);

    // パーティクル
    createParticles({
      container,
      x,
      y,
      count: isCritical ? 40 : 20,
      color: isCritical ? "#ff6600" : "#ff4444",
      size: isCritical ? 6 : 4,
      spread: isCritical ? 200 : 100,
    });
  };

  /**
   * 回復表示エフェクト
   */
  const showHealEffect = (targetElement: HTMLElement, heal: number): void => {
    const container = targetElement.closest(".battle-screen") as HTMLElement;
    if (!container) return;

    const containerRect = container.getBoundingClientRect();
    const targetRect = targetElement.getBoundingClientRect();

    const x = targetRect.left - containerRect.left + targetRect.width / 2;
    const y = targetRect.top - containerRect.top + targetRect.height / 2;

    showDamageText({
      container,
      x,
      y,
      value: heal,
      color: "#44ff44",
    });

    // 回復パーティクル（上昇）
    for (let i = 0; i < 20; i++) {
      setTimeout(() => {
        const particle = document.createElement("div");
        particle.style.position = "absolute";
        particle.style.left = `${x + (Math.random() - 0.5) * 100}px`;
        particle.style.top = `${y + 50}px`;
        particle.style.width = "8px";
        particle.style.height = "8px";
        particle.style.backgroundColor = "#44ff44";
        particle.style.borderRadius = "50%";
        particle.style.pointerEvents = "none";
        particle.style.zIndex = "9999";
        particle.style.boxShadow = "0 0 10px #44ff44";
        container.appendChild(particle);

        animateAsync({
          element: particle,
          duration: 1000,
          easing: Easing.easeOutQuad,
          to: {
            top: `${y - 100}px`,
            opacity: "0",
          } as Partial<CSSStyleDeclaration>,
        }).then(() => {
          particle.remove();
        });
      }, i * 30);
    }
  };

  /**
   * シールド表示エフェクト
   */
  const showShieldEffect = (
    targetElement: HTMLElement,
    shield: number
  ): void => {
    const container = targetElement.closest(".battle-screen") as HTMLElement;
    if (!container) return;

    const containerRect = container.getBoundingClientRect();
    const targetRect = targetElement.getBoundingClientRect();

    const x = targetRect.left - containerRect.left + targetRect.width / 2;
    const y = targetRect.top - containerRect.top + targetRect.height / 2;

    showDamageText({
      container,
      x,
      y,
      value: shield,
      color: "#4488ff",
      fontSize: 28,
    });

    // シールドのリング効果
    const ring = document.createElement("div");
    ring.style.position = "absolute";
    ring.style.left = `${x}px`;
    ring.style.top = `${y}px`;
    ring.style.width = "20px";
    ring.style.height = "20px";
    ring.style.border = "3px solid #4488ff";
    ring.style.borderRadius = "50%";
    ring.style.pointerEvents = "none";
    ring.style.zIndex = "9999";
    ring.style.transform = "translate(-50%, -50%)";
    ring.style.boxShadow = "0 0 20px #4488ff";
    container.appendChild(ring);

    animateAsync({
      element: ring,
      duration: 800,
      easing: Easing.easeOutQuad,
      to: {
        width: "200px",
        height: "200px",
        opacity: "0",
      } as Partial<CSSStyleDeclaration>,
    }).then(() => {
      ring.remove();
    });
  };

  /**
   * カードが新しくドローされたものかどうかを判定
   */
  const isNewCard = (cardId: string): boolean => {
    return newCardIds.has(cardId);
  };

  /**
   * 破棄中のカードを取得
   */
  const getDiscardingCards = (): Card[] => {
    return discardingCards;
  };

  return {
    // アニメーション関数
    drawCardsWithAnimation,
    discardCardsWithAnimation,
    applyDrawAnimation,
    applyDiscardAnimation,
    playCardWithAnimation,
    // エフェクト関数
    showDamageEffect,
    showHealEffect,
    showShieldEffect,
    // 状態判定関数
    isNewCard,
    getDiscardingCards,
  };
};
