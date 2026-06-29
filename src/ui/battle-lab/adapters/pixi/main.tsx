import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { PixiBattle } from "./PixiBattle";

const rootEl = document.getElementById("root");
if (rootEl) {
  createRoot(rootEl).render(
    <StrictMode>
      <PixiBattle />
    </StrictMode>,
  );
}
