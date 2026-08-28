// Build-time SEO snapshots (#173): renders the public routes of a RUNNING FairShare
// stack with headless Chromium and writes the post-boot DOM to snapshots/<route>.html.
// nginx serves these to the first request of each route (try_files, see nginx.conf) and
// the WASM app hydrates over them - so crawlers finally see per-route titles, canonicals,
// and the guide's actual text instead of the identical shell.
//
// Run from the repo root with the compose stack up:
//   BASE_URL=http://localhost:5858 node scripts/render-seo-snapshots.mjs
// CI wires this between "compose up" and the final docker build (ci.yml / release.yml).
//
// Every snapshot is asserted to carry its own <title> and a route-unique marker string -
// a silent shell-capture (boot too slow, API down) must fail the build, never ship.
import { chromium } from 'playwright';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';

const BASE = process.env.BASE_URL ?? 'http://localhost:5858';
const OUT = process.env.OUT_DIR ?? 'snapshots';

// path -> [expected <title> fragment, expected body marker]. Auth-gated and admin routes
// are deliberately absent; /States/AL and /States/OR capture their post-redirect first
// form (their canonicals point at the form URL, which is correct consolidation).
const routes = [
  ['/', 'FairShare - Select State', 'Choose your state'],
  ['/States/AL', 'Calculator', 'Number of Children'],
  ['/States/AL/CS42', 'CS-42', 'Number of Children'],
  ['/States/AL/CS42S', 'CS-42-S', 'Number of Children'],
  ['/States/OR', 'Calculator', 'Oregon worksheet'],
  ['/States/OR/Worksheet', 'Worksheet', 'Joint minor children'],
  ['/guides/oregon-worksheet', 'Understanding the Oregon worksheet', 'self-support reserve'],
  ['/privacy', 'Privacy', 'privacy'],
  ['/terms', 'Terms', 'Estimates, not legal advice'],
  ['/support', 'Support', 'FairShare'],
];

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1280, height: 900 } });
let failures = 0;

for (const [route, titlePart, marker] of routes) {
  const page = await ctx.newPage();
  try {
    await page.goto(BASE + route, { waitUntil: 'networkidle', timeout: 60000 });
    // Blazor boot is done when the shell's boot-progress is gone and an h1 exists.
    await page.waitForSelector('h1', { timeout: 45000 });
    await page.waitForFunction(() => !document.querySelector('.fs-boot-progress'), null, { timeout: 45000 });
    await page.waitForTimeout(800);

    const title = await page.title();
    const body = await page.evaluate(() => document.body.innerText);
    const html = '<!DOCTYPE html>\n' + await page.evaluate(() => document.documentElement.outerHTML);

    if (!title.includes(titlePart)) {
      console.error(`FAIL ${route}: title "${title}" lacks "${titlePart}"`);
      failures++;
    } else if (!body.toLowerCase().includes(marker.toLowerCase())) {
      console.error(`FAIL ${route}: body lacks marker "${marker}"`);
      failures++;
    } else {
      const file = route === '/' ? join(OUT, 'index.html') : join(OUT, route.replace(/^\//, '') + '.html');
      mkdirSync(dirname(file), { recursive: true });
      writeFileSync(file, html);
      console.log(`ok ${route} -> ${file} (${(html.length / 1024).toFixed(0)}KB, title "${title}")`);
    }
  } catch (err) {
    console.error(`FAIL ${route}: ${err.message.split('\n')[0]}`);
    failures++;
  } finally {
    await page.close();
  }
}

await browser.close();
if (failures > 0) {
  console.error(`${failures} route(s) failed to snapshot - refusing a partial set.`);
  process.exit(1);
}
console.log('all snapshots rendered');
