/**
 * CardDerivationTree Component
 *
 * Shows the derivation chain for a selected card.
 * Displays ancestors and descendants in a horizontal tree layout.
 */

import React, { useMemo } from "react";
import type { Card } from "@/types/cardTypes";
import { getDerivationChain } from "../../../domain/cards/logic/cardDerivation";
import { SWORDSMAN_CARDS } from "../../../constants/data/cards/SwordmanCards";
import { MAGE_CARDS } from "../../../constants/data/cards/mageCards";
import { SUMMONER_CARDS } from "../../../constants/data/cards/summonerCards";
import { ELEMENT_LABEL_MAP } from "../../../constants/cardConstants";

interface CardDerivationTreeProps {
  card: Card;
  onClose: () => void;
}

/** Get all cards from all classes combined */
function getAllCardRecords(): Record<string, Card> {
  return {
    ...SWORDSMAN_CARDS,
    ...MAGE_CARDS,
    ...SUMMONER_CARDS,
  };
}

const RARITY_COLORS: Record<string, string> = {
  common: "#9ca3af",
  rare: "#3b82f6",
  epic: "#a855f7",
  legend: "#f59e0b",
};

export const CardDerivationTree: React.FC<CardDerivationTreeProps> = ({
  card,
  onClose,
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
        <span
          className="derivation-rarity"
          style={{ color: RARITY_COLORS[card.rarity] }}
        >
          {card.rarity.toUpperCase()}
        </span>
        <span className="derivation-element">
          {card.element.map((e) => ELEMENT_LABEL_MAP[e]).join(" / ")}
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
            {chainCards.map((chainCard, index) => (
              <React.Fragment key={chainCard.cardTypeId}>
                <div
                  className={`derivation-chain-node ${
                    chainCard.cardTypeId === card.cardTypeId ? "active" : ""
                  }`}
                  style={{
                    borderColor: RARITY_COLORS[chainCard.rarity],
                  }}
                >
                  <span className="chain-node-name">{chainCard.name}</span>
                  <span
                    className="chain-node-rarity"
                    style={{ color: RARITY_COLORS[chainCard.rarity] }}
                  >
                    {chainCard.rarity}
                  </span>
                  {chainCard.unlockMasteryLevel !== undefined && (
                    <span className="chain-node-mastery">
                      Mastery Lv.{chainCard.unlockMasteryLevel}
                    </span>
                  )}
                </div>
                {index < chainCards.length - 1 && (
                  <span className="derivation-arrow">&rarr;</span>
                )}
              </React.Fragment>
            ))}
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
