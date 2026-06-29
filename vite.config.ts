import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "path";

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  optimizeDeps: {
    include: ["pixi.js", "@pixi/react"],
  },
  build: {
    rollupOptions: {
      // Multi-page build: the main app plus the engine bake-off entries.
      // Without an explicit input map Vite would only build index.html.
      input: {
        main: path.resolve(__dirname, "index.html"),
        prototype: path.resolve(__dirname, "prototype.html"),
        pixiBakeoff: path.resolve(__dirname, "pixi-bakeoff.html"),
        phaserBakeoff: path.resolve(__dirname, "phaser-bakeoff.html"),
      },
    },
  },
});
