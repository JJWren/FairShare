// Bundles the design system to dist/index.js (ESM, react external). Types come from
// tsc --emitDeclarationOnly (see package.json build script); CSS ships as static files
// under src/styles/ and is referenced by cssEntry/tokensGlob in the design-sync config.
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

console.log("built dist/index.js");
