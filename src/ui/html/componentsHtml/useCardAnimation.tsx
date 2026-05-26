import { useState } from "react";
import type { Card } from "@/types/cardTypes";
import {
  animateAsync,
  Easing,
  createParticles,
  showDamageText,
  shakeElement,
} from "../../animations/animationEngine";
import {
  CARD_ANIMATION,
  DAMAGE_ANIMATION,
  HEAL_ANIMATION,
  SHIELD_ANIMATION,
} from "@/constants";

export const useCardAnimation = () => {
  const [discardingCards, setDiscardingCards] = useState<Card[]>([]);
  const [newCardIds, setNewCardIds] = useState<Set<string>>(new Set());
  const drawCardsWithAnimation = async (
    cards: Card[],
    onAllCardsDrawn: (cards: Card[]) => void,
    interval: number = CARD_ANIMATION.DRAW_INTERVAL,
  ): Promise<void> => {
    const newIds = new Set(cards.map((c) => c.id));
    setNewCardIds((prev) => new Set([...prev, ...newIds]));

    onAllCardsDrawn(cards);

    const totalDuration =
      CARD_ANIMATION.DRAW_DURATION + (cards.length - 1) * interval;

    await new Promise((resolve) => setTimeout(resolve, totalDuration));

    setNewCardIds((prev) => {
      const next = new Set(prev);
      cards.forEach((c) => next.delete(c.id));
      return next;
    });
  };

  const applyDrawAnimation = async (element: HTMLElement): Promise<void> => {
    const container = element.closest(".battle-screen") as HTMLElement;
    if (!container) return;
    const containerRect = container.getBoundingClientRect();
    const elementRect = element.getBoundingClientRect();
    const startX = containerRect.width - CARD_ANIMATION.DRAW_START_OFFSET_X;
    const startY = containerRect.height - CARD_ANIMATION.DRAW_START_OFFSET_Y;
    const endX = elementRect.left - containerRect.left;
    const endY = elementRect.top - containerRect.top;

    element.style.position = "absolute";
    element.style.left = `${startX}px`;
    element.style.top = `${startY}px`;
    element.style.opacity = "0";
    element.style.transform = `scale(${CARD_ANIMATION.DRAW_INITIAL_SCALE}) rotate(${CARD_ANIMATION.DRAW_INITIAL_ROTATION}deg)`;
    element.style.zIndex = String(CARD_ANIMATION.DRAW_Z_INDEX);

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
    element.style.position = "";
    element.style.left = "";
    element.style.top = "";
    element.style.zIndex = "";
  };
  const discardCardsWithAnimation = async (
    cards: Card[],
    interval: number = CARD_ANIMATION.DISCARD_INTERVAL,
    onComplete?: () => void,
  ): Promise<void> => {
    if (cards.length === 0) {
      if (onComplete) onComplete();
      return;
    }

    const reversedCards = [...cards].reverse();

    for (let i = 0; i < reversedCards.length; i++) {
      const card = reversedCards[i];

      setDiscardingCards((prev) => [...prev, card]);

      if (i < reversedCards.length - 1) {
        await new Promise((resolve) => setTimeout(resolve, interval));
      }
    }

    await new Promise((resolve) =>
      setTimeout(resolve, CARD_ANIMATION.DISCARD_DURATION),
    );
    setDiscardingCards([]);
    if (onComplete) {
      onComplete();
    }
  };

  const applyDiscardAnimation = async (element: HTMLElement): Promise<void> => {
    const container = element.closest(".battle-screen") as HTMLElement;
    if (!container) return;

    const containerRect = container.getBoundingClientRect();
    const elementRect = element.getBoundingClientRect();
    const startX = elementRect.left - containerRect.left;
    const startY = elementRect.top - containerRect.top;
    const endX = CARD_ANIMATION.DISCARD_END_OFFSET_X;
    const endY = containerRect.height - CARD_ANIMATION.DISCARD_END_OFFSET_Y;

    element.style.position = "absolute";
    element.style.left = `${startX}px`;
    element.style.top = `${startY}px`;
    element.style.zIndex = String(CARD_ANIMATION.DRAW_Z_INDEX);

    createParticles({
      container,
      x: startX + 80,
      y: startY + 100,
      count: CARD_ANIMATION.DISCARD_PARTICLE_COUNT,
      color: `rgba(100, 100, 255, ${CARD_ANIMATION.DISCARD_PARTICLE_OPACITY})`,
      size: CARD_ANIMATION.DISCARD_PARTICLE_SIZE,
      spread: CARD_ANIMATION.DISCARD_PARTICLE_SPREAD,
    });

    await animateAsync({
      element,
      duration: CARD_ANIMATION.DISCARD_DURATION,
      easing: Easing.easeInCubic,
      to: {
        left: `${endX}px`,
        top: `${endY}px`,
        opacity: "0",
        transform: `scale(${CARD_ANIMATION.DRAW_INITIAL_SCALE}) rotate(-${CARD_ANIMATION.DISCARD_ROTATION}deg)`,
      } as Partial<CSSStyleDeclaration>,
    });
  };

  const playCardWithAnimation = async (
    element: HTMLElement,
    targetElement: HTMLElement,
    onComplete: () => void,
    preComputedStart?: { x: number; y: number },
  ): Promise<void> => {
    const container = element.closest(".battle-screen") as HTMLElement;
    if (!container) {
      onComplete();
      return;
    }

    const containerRect = container.getBoundingClientRect();
    const elementRect = element.getBoundingClientRect();
    const targetRect = targetElement.getBoundingClientRect();
    // Use pre-computed coordinates if available (captured before state update removes card)
    const startX = preComputedStart
      ? preComputedStart.x
      : elementRect.left - containerRect.left;
    const startY = preComputedStart
      ? preComputedStart.y
      : elementRect.top - containerRect.top;
    const endX = targetRect.left - containerRect.left + targetRect.width / 2;
    const endY = targetRect.top - containerRect.top + targetRect.height / 2;
    const clone = element.cloneNode(true) as HTMLElement;

    clone.style.position = "absolute";
    clone.style.left = `${startX}px`;
    clone.style.top = `${startY}px`;
    clone.style.zIndex = String(CARD_ANIMATION.PLAY_Z_INDEX);
    clone.style.pointerEvents = "none";
    container.appendChild(clone);

    element.style.opacity = "0";

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

    const trailInterval = setInterval(
      createTrail,
      CARD_ANIMATION.PLAY_TRAIL_INTERVAL,
    );

    await animateAsync({
      element: clone,
      duration: CARD_ANIMATION.PLAY_DURATION,
      easing: Easing.easeInQuad,
      to: {
        left: `${endX}px`,
        top: `${endY}px`,
        transform: `scale(${CARD_ANIMATION.PLAY_FINAL_SCALE}) rotate(${CARD_ANIMATION.PLAY_FINAL_ROTATION}deg)`,
        opacity: String(CARD_ANIMATION.PLAY_PARTICLE_OPACITY),
      } as Partial<CSSStyleDeclaration>,
    });

    clearInterval(trailInterval);

    createParticles({
      container,
      x: endX,
      y: endY,
      count: CARD_ANIMATION.PLAY_PARTICLE_COUNT,
      color: `rgba(255, 200, 100, ${CARD_ANIMATION.PLAY_PARTICLE_OPACITY})`,
      size: CARD_ANIMATION.PLAY_PARTICLE_SIZE,
      spread: CARD_ANIMATION.PLAY_PARTICLE_SPREAD,
      gravity: 1,
    });

    shakeElement(targetElement, 15, 300);

    clone.remove();
    element.style.opacity = "";

    onComplete();
  };

  const showDamageEffect = (
    targetElement: HTMLElement,
    damage: number,
    isCritical: boolean = false,
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
      color: isCritical
        ? DAMAGE_ANIMATION.CRIT_COLOR
        : DAMAGE_ANIMATION.NORMAL_COLOR,
      duration: DAMAGE_ANIMATION.TEXT_DURATION,
      isCritical,
    });

    shakeElement(
      targetElement,
      isCritical
        ? DAMAGE_ANIMATION.CRIT_SHAKE_AMPLITUDE
        : DAMAGE_ANIMATION.NORMAL_SHAKE_AMPLITUDE,
      DAMAGE_ANIMATION.SHAKE_DURATION,
    );

    createParticles({
      container,
      x,
      y,
      count: isCritical
        ? DAMAGE_ANIMATION.CRIT_PARTICLE_COUNT
        : DAMAGE_ANIMATION.NORMAL_PARTICLE_COUNT,
      color: isCritical
        ? DAMAGE_ANIMATION.CRIT_COLOR
        : DAMAGE_ANIMATION.NORMAL_COLOR,
      size: isCritical
        ? DAMAGE_ANIMATION.CRIT_PARTICLE_SIZE
        : DAMAGE_ANIMATION.NORMAL_PARTICLE_SIZE,
      spread: isCritical
        ? DAMAGE_ANIMATION.CRIT_PARTICLE_SPREAD
        : DAMAGE_ANIMATION.NORMAL_PARTICLE_SPREAD,
    });
  };

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

    for (let i = 0; i < HEAL_ANIMATION.PARTICLE_COUNT; i++) {
      setTimeout(() => {
        const particle = document.createElement("div");
        particle.style.position = "absolute";
        particle.style.left = `${x + (Math.random() - 0.5) * 100}px`;
        particle.style.top = `${y + HEAL_ANIMATION.PARTICLE_Y_OFFSET}px`;
        particle.style.width = `${HEAL_ANIMATION.PARTICLE_SIZE}px`;
        particle.style.height = `${HEAL_ANIMATION.PARTICLE_SIZE}px`;
        particle.style.backgroundColor = "#44ff44";
        particle.style.borderRadius = "50%";
        particle.style.pointerEvents = "none";
        particle.style.zIndex = "9999";
        particle.style.boxShadow = `0 0 ${HEAL_ANIMATION.GLOW_BLUR}px #44ff44`;
        container.appendChild(particle);

        animateAsync({
          element: particle,
          duration: HEAL_ANIMATION.PARTICLE_DURATION,
          easing: Easing.easeOutQuad,
          to: {
            top: `${y - HEAL_ANIMATION.PARTICLE_FINAL_Y}px`,
            opacity: "0",
          } as Partial<CSSStyleDeclaration>,
        }).then(() => {
          particle.remove();
        });
      }, i * HEAL_ANIMATION.PARTICLE_INTERVAL);
    }
  };

  const showShieldEffect = (
    targetElement: HTMLElement,
    shield: number,
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
      fontSize: SHIELD_ANIMATION.TEXT_FONT_SIZE,
    });

    const ring = document.createElement("div");
    ring.style.position = "absolute";
    ring.style.left = `${x}px`;
    ring.style.top = `${y}px`;
    ring.style.width = `${SHIELD_ANIMATION.RING_INITIAL_SIZE}px`;
    ring.style.height = `${SHIELD_ANIMATION.RING_INITIAL_SIZE}px`;
    ring.style.border = `${SHIELD_ANIMATION.RING_BORDER}px solid #4488ff`;
    ring.style.borderRadius = "50%";
    ring.style.pointerEvents = "none";
    ring.style.zIndex = "9999";
    ring.style.transform = "translate(-50%, -50%)";
    ring.style.boxShadow = `0 0 ${SHIELD_ANIMATION.RING_GLOW_BLUR}px #4488ff`;
    container.appendChild(ring);

    animateAsync({
      element: ring,
      duration: SHIELD_ANIMATION.RING_DURATION,
      easing: Easing.easeOutQuad,
      to: {
        width: `${SHIELD_ANIMATION.RING_FINAL_SIZE}px`,
        height: `${SHIELD_ANIMATION.RING_FINAL_SIZE}px`,
        opacity: "0",
      } as Partial<CSSStyleDeclaration>,
    }).then(() => {
      ring.remove();
    });
  };

  const isNewCard = (cardId: string): boolean => {
    return newCardIds.has(cardId);
  };

  const getDiscardingCards = (): Card[] => {
    return discardingCards;
  };

  return {
    drawCardsWithAnimation,
    discardCardsWithAnimation,
    applyDrawAnimation,
    applyDiscardAnimation,
    playCardWithAnimation,
    showDamageEffect,
    showHealEffect,
    showShieldEffect,
    isNewCard,
    getDiscardingCards,
  };
};
