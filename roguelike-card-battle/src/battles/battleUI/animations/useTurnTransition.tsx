import { useState } from "react";

/**
 * ターン遷移メッセージアニメーション用のカスタムフック
 */
export const useTurnTransition = () => {
  // ターン遷移メッセージ
  const [turnMessage, setTurnMessage] = useState<string>("");
  // メッセージ表示フラグ
  const [showTurnMessage, setShowTurnMessage] = useState(false);

  /**
   * ターン遷移メッセージを表示
   * @param message 表示するメッセージ
   * @param duration 表示時間（ms）
   * @param onComplete 完了時のコールバック
   */
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

  /**
   * メッセージを即座に非表示
   */
  const hideMessage = () => {
    setShowTurnMessage(false);
    setTurnMessage("");
  };

  return {
    // 状態
    turnMessage,
    showTurnMessage,
    // 関数
    showMessage,
    hideMessage,
  };
};
