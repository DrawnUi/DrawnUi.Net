import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [react()],
  build: {
    // main.tsx awaits the engine startup at the top level
    target: "esnext",
    // the drawing engine alone is ~750 kB minified (225 kB gzipped); Vite's default warns at 500 kB
    chunkSizeWarningLimit: 1000,
  },
});
