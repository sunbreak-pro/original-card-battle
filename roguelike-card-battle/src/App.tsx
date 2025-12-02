import { useState } from "react";
import BattleScreen from "./battles/battleUI/BattleScreen";
import BaseCamp from "./camps/campsUI/BaseCamp";

type GameScreen = "camp" | "battle";

function App() {
  const [currentScreen, setCurrentScreen] = useState<GameScreen>("camp");
  const [depth, setDepth] = useState<1 | 2 | 3 | 4 | 5>(1);

  return (
    <div style={{ width: "100%", height: "100%" }}>
      {currentScreen === "camp" && <BaseCamp />}
      {currentScreen === "battle" && (
        <BattleScreen depth={depth} onDepthChange={setDepth} />
      )}

      {/* Debug: Screen switcher */}
      <div
        style={{
          position: "fixed",
          top: "10px",
          right: "10px",
          zIndex: 9999,
          display: "flex",
          gap: "10px",
        }}
      >
        <button
          onClick={() => setCurrentScreen("camp")}
          style={{
            padding: "8px 16px",
            background: currentScreen === "camp" ? "#8a629e" : "#333",
            color: "#fff",
            border: "2px solid #8a629e",
            borderRadius: "4px",
            cursor: "pointer",
            fontWeight: "bold",
          }}
        >
          Camp
        </button>
        <button
          onClick={() => setCurrentScreen("battle")}
          style={{
            padding: "8px 16px",
            background: currentScreen === "battle" ? "#8a629e" : "#333",
            color: "#fff",
            border: "2px solid #8a629e",
            borderRadius: "4px",
            cursor: "pointer",
            fontWeight: "bold",
          }}
        >
          Battle
        </button>
      </div>
    </div>
  );
}

export default App;
