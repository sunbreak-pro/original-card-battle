/**
 * RumorBubble - Displays rumors/tips when clicked
 *
 * A floating speech bubble icon that shows a random rumor or tip
 * when clicked. Adds atmosphere and provides occasional useful info.
 */

import { useState, useCallback } from "react";
import { INN_RUMORS } from "@/constants/data/camps/InnData";

const RumorBubble = () => {
  const [showRumor, setShowRumor] = useState(false);
  const [currentRumor, setCurrentRumor] = useState<string | null>(null);

  const handleClick = useCallback(() => {
    // Select a random rumor
    const randomIndex = Math.floor(Math.random() * INN_RUMORS.length);
    setCurrentRumor(INN_RUMORS[randomIndex]);
    setShowRumor(true);

    // Auto-hide after 5 seconds
    setTimeout(() => {
      setShowRumor(false);
    }, 5000);
  }, []);

  const handleClose = useCallback(() => {
    setShowRumor(false);
  }, []);

  return (
    <div className="rumor-bubble-container">
      <button
        className="rumor-bubble-trigger"
        onClick={handleClick}
        title="噂を聞く"
        aria-label="噂を聞く"
      >
        <span className="bubble-icon">
          <span className="bubble-icon-chat" aria-hidden="true" />
        </span>
      </button>

      {showRumor && currentRumor && (
        <div className="rumor-popup" onClick={handleClose}>
          <div className="rumor-content">
            <span className="rumor-quote">"</span>
            <p className="rumor-text">{currentRumor}</p>
            <span className="rumor-quote">"</span>
          </div>
          <span className="rumor-source">- 宿の客より</span>
        </div>
      )}
    </div>
  );
};

export default RumorBubble;
