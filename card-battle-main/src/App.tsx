import BattleScreen from "./ui/battleHtml/BattleScreen.tsx";
import GuildBattleScreen from "./ui/campsHtml/Guild/GuildBattleScreen.tsx";
import BaseCamp from "./ui/campsHtml/BaseCamp.tsx";
import { Guild } from "./ui/campsHtml/Guild/Guild.tsx";
import { Shop } from "./ui/campsHtml/Shop/Shop.tsx";
import Storage from "./ui/campsHtml/Storage/Storage.tsx";
import { Blacksmith } from "./ui/campsHtml/Blacksmith/Blacksmith.tsx";
import { Sanctuary } from "./ui/campsHtml/Sanctuary/Sanctuary.tsx";
import { Library } from "./ui/campsHtml/Library/Library.tsx";
import { DungeonGate } from "./ui/dungeonUI/DungeonGate.tsx";
import { NodeMap } from "./ui/dungeonUI/NodeMap.tsx";
import { DungeonRunProvider } from "./ui/dungeonUI/DungeonRunContext.tsx";
import { CharacterSelect } from "./ui/characterSelectHtml/CharacterSelect.tsx";
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
      {/* Character Selection Screen */}
      {currentScreen === "character_select" && <CharacterSelect />}

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
                  battleConfig.enemyIds[0],
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
      {/* Library Screen */}
      {currentScreen === "library" && <Library />}
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
