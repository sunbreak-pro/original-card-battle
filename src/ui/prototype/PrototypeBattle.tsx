import type { JSX } from "react";
import { useReducer } from "react";
import { battleReducer, initState } from "./engine/battleReducer";
import { MAX_STAMINA, PLAYER_MAX_HP } from "./engine/constants";
import { ENEMY_DEF } from "./engine/enemy";
import { DistanceTrack } from "./components/DistanceTrack";
import { CombatantPanel } from "./components/CombatantPanel";
import { HandView } from "./components/HandView";
import { BattleLog } from "./components/BattleLog";
import { ResultOverlay } from "./components/ResultOverlay";

const ENEMY_HINT =
  "有効間合い: 中（薙ぎ払い）/ 遠（穂先の突き）。近に詰めると弱い押し戻ししかできない。";

export function PrototypeBattle(): JSX.Element {
  const [state, dispatch] = useReducer(battleReducer, undefined, initState);
  const isOver = state.result !== "ongoing";

  return (
    <div className="prototype-battle">
      <header className="pb-header">
        <h1 className="pb-title">間合い × スタミナ 検証台</h1>
        <span className="pb-turn">ターン {state.turn}</span>
      </header>

      <DistanceTrack distanceIndex={state.distanceIndex} />

      <div className="pb-combatants">
        <CombatantPanel
          side="player"
          name="あなた（剣士）"
          hp={state.playerHp}
          maxHp={PLAYER_MAX_HP}
          stamina={state.playerStamina}
          maxStamina={MAX_STAMINA}
          guard={state.playerGuard}
        />
        <CombatantPanel
          side="enemy"
          name={ENEMY_DEF.name}
          hp={state.enemyHp}
          maxHp={ENEMY_DEF.maxHp}
          stamina={state.enemyStamina}
          maxStamina={MAX_STAMINA}
          hint={ENEMY_HINT}
        />
      </div>

      <HandView
        hand={state.hand}
        playerStamina={state.playerStamina}
        distanceIndex={state.distanceIndex}
        disabled={isOver}
        onPlay={(instanceId) => dispatch({ type: "PLAY_CARD", instanceId })}
      />

      <div className="pb-controls">
        <button
          type="button"
          className="pb-end-turn"
          disabled={isOver}
          onClick={() => dispatch({ type: "END_TURN" })}
        >
          ターン終了
        </button>
      </div>

      <BattleLog log={state.log} />

      {isOver && (
        <ResultOverlay
          result={state.result}
          onRestart={() => dispatch({ type: "RESTART" })}
        />
      )}
    </div>
  );
}
