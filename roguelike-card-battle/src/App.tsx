import BattleScreen from "./ui/battleUI/BattleScreen.tsx";
import GuildBattleScreen from "./ui/campsUI/Guild/GuildBattleScreen.tsx";
import BaseCamp from "./ui/campsUI/BaseCamp.tsx";
import { Guild } from "./ui/campsUI/Guild/Guild.tsx";
import { Shop } from "./ui/campsUI/Shop/Shop.tsx";
import Storage from "./ui/campsUI/Storage/Storage.tsx";
import { Blacksmith } from "./ui/campsUI/Blacksmith/Blacksmith.tsx";
import { Sanctuary } from "./ui/campsUI/Sanctuary/Sanctuary.tsx";
import { DungeonGate } from "./ui/dungeonUI/DungeonGate.tsx";
import { NodeMap } from "./ui/dungeonUI/NodeMap.tsx";
import { DungeonRunProvider } from "./ui/dungeonUI/DungeonRunContext.tsx";
import {
  GameStateProvider,
  useGameState,
} from "./domain/camps/contexts/GameStateContext.tsx";
import { ResourceProvider } from "./domain/camps/contexts/ResourceContext.tsx";
import { PlayerProvider } from "./domain/camps/contexts/PlayerContext.tsx";
import { InventoryProvider } from "./domain/camps/contexts/InventoryContext.tsx";
import { getGuildEnemy } from "./domain/camps/data/GuildEnemyData.ts";
import "./App.css";

/**
 * AppContent Component
 * Handles screen routing based on GameState
 */
function AppContent() {
  const { gameState, setDepth, returnToCamp } = useGameState();
  const { currentScreen, depth, battleMode, battleConfig } = gameState;

  return (
    <div className="app-container">
      {/* BaseCamp Screen */}
      {currentScreen === "camp" && <BaseCamp />}

      {/* Battle Screen */}
      {currentScreen === "battle" && (
        <>
          {battleMode === "exam" && battleConfig ? (
            // Exam battle - use GuildBattleScreen
            (() => {
              const examEnemy = getGuildEnemy(battleConfig.enemyIds[0]);
              if (!examEnemy) {
                console.error(
                  "Exam enemy not found:",
                  battleConfig.enemyIds[0]
                );
                return <div>Error: Exam enemy not found</div>;
              }
              return (
                <GuildBattleScreen
                  examEnemy={examEnemy}
                  onWin={battleConfig.onWin || (() => {})}
                  onLose={battleConfig.onLose || (() => {})}
                />
              );
            })()
          ) : (
            // Normal dungeon battle - use regular BattleScreen
            // Pass battleConfig callbacks for dungeon integration
            <BattleScreen
              depth={depth}
              onDepthChange={setDepth}
              onReturnToCamp={returnToCamp}
              onWin={battleConfig?.onWin}
              onLose={battleConfig?.onLose}
            />
          )}
        </>
      )}

      {/* Guild Screen */}
      {currentScreen === "guild" && <Guild />}
      {/* Shop Screen */}
      {currentScreen === "shop" && <Shop />}
      {/* Blacksmith Screen */}
      {currentScreen === "blacksmith" && <Blacksmith />}
      {/* Sanctuary Screen */}
      {currentScreen === "sanctuary" && <Sanctuary />}
      {currentScreen === "library" && (
        <div className="facility-placeholder">
          <button className="back-button" onClick={returnToCamp}>
            ← Back to Camp
          </button>
          <h1>Library (Coming Soon)</h1>
        </div>
      )}
      {/* Storage Screen */}
      {currentScreen === "storage" && <Storage />}
      {/* Dungeon Gate Screen */}
      {currentScreen === "dungeon" && <DungeonGate />}

      {/* Dungeon Map Screen */}
      {currentScreen === "dungeon_map" && <NodeMap />}
    </div>
  );
}

/**
 * Main App Component
 * Wraps AppContent with all necessary Context Providers
 * DungeonRunProvider is at app level to persist state across battle transitions
 */
function App() {
  return (
    <GameStateProvider>
      <ResourceProvider>
        <PlayerProvider>
          <InventoryProvider>
            <DungeonRunProvider>
              <AppContent />
            </DungeonRunProvider>
          </InventoryProvider>
        </PlayerProvider>
      </ResourceProvider>
    </GameStateProvider>
  );
}

export default App;
