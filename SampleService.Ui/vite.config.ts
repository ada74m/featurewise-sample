import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

// Builds the module as one ES module, dist/index.js, with React inside it, so the shell loads it with
// a plain import() and nothing else on the page needs React.
export default defineConfig({
  plugins: [react()],
  define: { "process.env.NODE_ENV": JSON.stringify("production") },
  build: {
    lib: { entry: "src/index.tsx", formats: ["es"], fileName: () => "index.js" },
    outDir: "dist",
    minify: true,
    emptyOutDir: true
  },
  test: { environment: "happy-dom", include: ["test/**/*.test.tsx"] }
});
