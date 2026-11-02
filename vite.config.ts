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
      // The main app is the only entry (the web prototypes were retired in #533; tag archive/web-v1-prototypes).
      input: {
        main: path.resolve(__dirname, "index.html"),
      },
    },
  },
});
