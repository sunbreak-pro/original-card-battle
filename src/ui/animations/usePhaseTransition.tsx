import { useState } from "react";

export const useTurnTransition = () => {
  const [turnMessage, setTurnMessage] = useState<string>("");
  const [showTurnMessage, setShowTurnMessage] = useState(false);

  const showMessage = (
    message: string,
    duration: number = 5000,
    onComplete?: () => void
  ) => {
    setTurnMessage(message);
    setShowTurnMessage(true);

    setTimeout(() => {
      setShowTurnMessage(false);
      if (onComplete) {
        onComplete();
      }
    }, duration);
  };

  const hideMessage = () => {
    setShowTurnMessage(false);
    setTurnMessage("");
  };

  return {
    turnMessage,
    showTurnMessage,
    showMessage,
    hideMessage,
  };
};
