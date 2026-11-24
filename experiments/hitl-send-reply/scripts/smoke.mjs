// Smoke test of the Web app through a real browser. Needs the app running (dotnet run --project HitlSendReply.AppHost).
// Runs scenario 1 (auto-sent) and scenario 2 (stopped for a human, then approved) and saves
// screenshots at 1440 and 390 px, light and dark, to evidence/screenshots/web/.
// Usage: node scripts/smoke.mjs [baseUrl]
// Playwright, from the experiment folder: npm install --no-save playwright && npx playwright install chromium
import { createRequire } from 'node:module';
import { mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');

const baseUrl = process.argv[2] ?? 'http://localhost:5090';
const out = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'evidence', 'screenshots', 'web');
mkdirSync(out, { recursive: true });

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
const byId = (id) => page.locator(`[data-testid="${id}"]`);
// The short wait lets MudBlazor's color transitions finish before the picture.
const shot = async (name) => { await page.waitForTimeout(500); await page.screenshot({ path: path.join(out, `${name}.png`), fullPage: true }); };
const fail = (message) => { console.error(`FAIL ${message}`); process.exitCode = 1; };

async function pickScenario(number) {
  await byId('scenario').locator('.mud-input-control').click();
  await page.locator('.mud-popover-open .mud-list-item', { hasText: `${number}. ` }).first().click();
}

async function setDark(dark) {
  const isDark = await page.locator('.mud-theme-dark, body.mud-theme-dark').count() > 0
    || await page.evaluate(() => getComputedStyle(document.body).backgroundColor !== 'rgb(255, 255, 255)');
  if (isDark !== dark) {
    await byId('theme').click();
    await page.waitForTimeout(300);
  }
}

await page.goto(baseUrl);
await byId('start').waitFor();

// Scenario 1: facts verified, judge clear, sent without a human.
await pickScenario(1);
await byId('start').click();
await byId('result').waitFor();
const result1 = await byId('result').innerText();
if (!/Sent/.test(result1)) { fail(`scenario 1 should auto-send, got: ${result1}`); }
if ((await byId('outcome').innerText()) !== 'AutoSend') { fail('scenario 1 should be AutoSend'); }
await shot('scenario1-autosend-1440-light');

// Scenario 2: the policy stops a refund promise; a person approves it.
await pickScenario(2);
await byId('start').click();
await byId('approve').waitFor();
if ((await byId('outcome').innerText()) !== 'NeedsHuman') { fail('scenario 2 should stop for a human'); }
await shot('scenario2-pending-1440-light');
await setDark(true);
await shot('scenario2-pending-1440-dark');
await page.setViewportSize({ width: 390, height: 844 });
await shot('scenario2-pending-390-dark');
await setDark(false);
await shot('scenario2-pending-390-light');
await page.setViewportSize({ width: 1440, height: 1000 });

await byId('approve').click();
await byId('result').waitFor();
const result2 = await byId('result').innerText();
if (!/Sent/.test(result2)) { fail(`scenario 2 should be sent after approval, got: ${result2}`); }
const audit = await byId('audit').innerText();
if (!/human-approved/.test(audit) || !/\bsent\b/.test(audit)) { fail('audit should show human-approved then sent'); }
await shot('scenario2-approved-1440-light');

await browser.close();
console.log(process.exitCode ? 'smoke: FAILED' : `smoke: OK (screenshots in ${path.relative(process.cwd(), out)})`);
