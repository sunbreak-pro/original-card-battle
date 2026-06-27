import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { PrototypeBattle } from "./PrototypeBattle";
import "./prototype-battle.css";

const rootEl = document.getElementById("root");
if (rootEl) {
  createRoot(rootEl).render(
    <StrictMode>
      <PrototypeBattle />
    </StrictMode>,
  );
}
