import { useGameState } from "@/contexts/GameStateContext";
import "../../css/components/BackToCampButton.css";

export const BackToCampButton = () => {
  const { returnToCamp } = useGameState();

  return (
    <button className="back-to-camp-button" onClick={returnToCamp}>
      ← キャンプに戻る
    </button>
  );
};

export default BackToCampButton;
