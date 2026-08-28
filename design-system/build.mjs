// Bundles the design system to dist/index.js (ESM, react external) and flattens the
// stylesheet set to dist/warm-counsel.css (local @imports inlined, the Google-Fonts
// remote @import preserved). Types come from tsc --emitDeclarationOnly (see
// package.json build script); design-sync's cssEntry points at the flattened css.
import { build } from "esbuild";

await build({
  entryPoints: ["src/index.ts"],
  outfile: "dist/index.js",
  bundle: true,
  format: "esm",
  target: "es2020",
  external: ["react", "react-dom", "react/jsx-runtime"],
  jsx: "automatic",
});

await build({
  entryPoints: ["src/styles/index.css"],
  outfile: "dist/warm-counsel.css",
  bundle: true,
  external: ["https://*"],
});

console.log("built dist/index.js + dist/warm-counsel.css");
