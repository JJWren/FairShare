// Every token pair the components put together must clear WCAG AA - the same
// assertion set that gated the approved Warm Counsel mocks (issue #183).
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const css = readFileSync(join(dirname(fileURLToPath(import.meta.url)), "..", "src", "styles", "tokens.css"), "utf8");

function block(marker) {
  const start = css.indexOf(marker);
  assert.ok(start >= 0, `${marker} block present`);
  const open = css.indexOf("{", start);
  const close = css.indexOf("}", open);
  return css.slice(open, close);
}

function tokens(marker, fallback = {}) {
  const body = block(marker);
  const map = { ...fallback };
  for (const m of body.matchAll(/(--fs-[a-z-]+):\s*(#[0-9a-fA-F]{6})/g)) map[m[1]] = m[2];
  return map;
}

function lum(hex) {
  const n = hex.replace("#", "");
  const [r, g, b] = [0, 2, 4]
    .map((i) => parseInt(n.slice(i, i + 2), 16) / 255)
    .map((c) => (c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4)));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function ratio(f, g) {
  const [a, b] = [lum(f), lum(g)].sort((x, y) => y - x);
  return (a + 0.05) / (b + 0.05);
}

// The pairs the components actually compose (class -> fg on bg), with the
// AA threshold each must clear: 4.5 for text, 3.0 for UI components.
const PAIRS = [
  ["ink on page", "--fs-ink", "--fs-page", 4.5],
  ["ink on card", "--fs-ink", "--fs-card", 4.5],
  ["ink on input", "--fs-ink", "--fs-input-bg", 4.5],
  ["muted on card", "--fs-muted", "--fs-card", 4.5],
  ["muted on card-alt", "--fs-muted", "--fs-card-alt", 4.5],
  ["muted on page", "--fs-muted", "--fs-page", 4.5],
  ["faint on card", "--fs-faint", "--fs-card", 4.5],
  ["button text", "--fs-btn-ink", "--fs-btn-bg", 4.5],
  ["accent on page", "--fs-accent", "--fs-page", 4.5],
  ["accent on card", "--fs-accent", "--fs-card", 4.5],
  ["accent chip", "--fs-accent", "--fs-accent-bg", 4.5],
  ["danger outline text", "--fs-danger", "--fs-input-bg", 4.5],
  ["plaintiff badge", "--fs-plaintiff-ink", "--fs-plaintiff-bg", 4.5],
  ["defendant badge", "--fs-defendant-ink", "--fs-defendant-bg", 4.5],
  ["gold row", "--fs-gold-ink", "--fs-gold-bg", 4.5],
  ["input border", "--fs-border-strong", "--fs-input-bg", 3.0],
  ["toggle on card-alt", "--fs-toggle", "--fs-card-alt", 3.0],
  ["plaintiff edge on card", "--fs-plaintiff-edge", "--fs-card", 3.0],
  ["defendant edge on card", "--fs-defendant-edge", "--fs-card", 3.0],
];

const light = tokens(":root");
const dark = tokens('[data-fs-theme="dark"]', light);

for (const [themeName, map] of [["light", light], ["dark", dark]]) {
  test(`${themeName} theme pairs clear WCAG AA`, () => {
    for (const [name, fg, bg, need] of PAIRS) {
      assert.ok(map[fg], `${fg} defined`);
      assert.ok(map[bg], `${bg} defined`);
      const r = ratio(map[fg], map[bg]);
      assert.ok(r >= need, `${themeName} ${name}: ${map[fg]} on ${map[bg]} = ${r.toFixed(2)}, needs ${need}`);
    }
  });
}
