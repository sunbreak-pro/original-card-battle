/**
 * CardDerivationTree Component
 *
 * Shows the derivation chain for a selected card.
 * Displays ancestors and descendants in a horizontal tree layout.
 */

import React, { useMemo } from "react";
import type { Card } from "@/types/cardTypes";
import { getDerivationChain } from "@/domain/cards/logic/cardDerivation";
import { SWORDSMAN_CARDS } from "@/constants/data/cards/swordmanCards";
import { MAGE_CARDS } from "@/constants/data/cards/mageCards";
import { SUMMONER_CARDS } from "@/constants/data/cards/summonerCards";
import { ELEMENT_LABEL_MAP, ELEMENT_COLOR_MAP } from "@/constants/cardConstants";

interface CardDerivationTreeProps {
  card: Card;
  onClose: () => void;
  unlockedCardTypeIds?: Set<string>;
}

/** Get all cards from all classes combined */
function getAllCardRecords(): Record<string, Card> {
  return {
    ...SWORDSMAN_CARDS,
    ...MAGE_CARDS,
    ...SUMMONER_CARDS,
  };
}

export const CardDerivationTree: React.FC<CardDerivationTreeProps> = ({
  card,
  onClose,
  unlockedCardTypeIds,
}) => {
  const allCards = useMemo(() => getAllCardRecords(), []);

  const chain = useMemo(
    () => getDerivationChain(card.cardTypeId, allCards),
    [card.cardTypeId, allCards],
  );

  const chainCards = useMemo(
    () => chain.map((id) => allCards[id]).filter(Boolean),
    [chain, allCards],
  );

  const hasDerivation = chainCards.length > 1;

  return (
    <div className="library-derivation-tree">
      <div className="derivation-header">
        <h3 className="derivation-title">{card.name}</h3>
        <button className="derivation-close" onClick={onClose}>
          x
        </button>
      </div>

      <div className="derivation-info">
        <span className="derivation-class">{card.characterClass}</span>
        <span className="derivation-element">
          {card.element.map((e) => (
            <span
              key={e}
              style={{ color: ELEMENT_COLOR_MAP[e], marginRight: "0.5vw" }}
            >
              {ELEMENT_LABEL_MAP[e]}
            </span>
          ))}
        </span>
      </div>

      <p className="derivation-description">{card.description}</p>

      {card.baseDamage !== undefined && card.baseDamage > 0 && (
        <div className="derivation-stats">
          <span>Cost: {card.cost}</span>
          <span>Damage: {card.baseDamage}</span>
          {card.hitCount && <span>Hits: {card.hitCount}</span>}
          {card.penetration && (
            <span>Penetration: {Math.round(card.penetration * 100)}%</span>
          )}
        </div>
      )}

      {hasDerivation ? (
        <div className="derivation-chain">
          <h4 className="derivation-chain-title">Derivation Chain</h4>
          <div className="derivation-chain-list">
            {chainCards.map((chainCard, index) => {
              const isUnlocked =
                !unlockedCardTypeIds ||
                unlockedCardTypeIds.has(chainCard.cardTypeId);
              const isActive = chainCard.cardTypeId === card.cardTypeId;

              return (
                <React.Fragment key={chainCard.cardTypeId}>
                  <div
                    className={`derivation-chain-node${isActive ? " active" : ""}${!isUnlocked ? " locked" : ""}`}
                    style={{
                      borderColor: isUnlocked
                        ? ELEMENT_COLOR_MAP[chainCard.element[0]] ?? "#6b7280"
                        : "#4b5563",
                      opacity: isUnlocked ? 1 : 0.6,
                    }}
                  >
                    {!isUnlocked && (
                      <span className="chain-node-lock">&#x1f512;</span>
                    )}
                    <span
                      className="chain-node-name"
                      style={{
                        color: isUnlocked ? undefined : "#6b7280",
                      }}
                    >
                      {isUnlocked ? chainCard.name : "???"}
                    </span>
                    <span className="chain-node-element">
                      {chainCard.element
                        .map((e) => ELEMENT_LABEL_MAP[e])
                        .join(" / ")}
                    </span>
                    {chainCard.unlockMasteryLevel !== undefined && (
                      <span
                        className="chain-node-mastery"
                        style={{
                          color: isUnlocked ? "#fbbf24" : "#9ca3af",
                        }}
                      >
                        {isUnlocked
                          ? "Unlocked"
                          : `Mastery Lv.${chainCard.unlockMasteryLevel} required`}
                      </span>
                    )}
                  </div>
                  {index < chainCards.length - 1 && (
                    <span className="derivation-arrow">&rarr;</span>
                  )}
                </React.Fragment>
              );
            })}
          </div>
        </div>
      ) : (
        <div className="derivation-no-chain">
          <span>No derivation chain for this card</span>
        </div>
      )}
    </div>
  );
};
