// Smoke test of the Web app through a real browser. Needs the app running (dotnet run --project HitlSendReply.AppHost).
// Sends an email from the inbox, waits for the reply to wait for a person (kept in email_replies),
// approves it, then sends a second one and edits it. Saves screenshots at 1440 and 390 px, light and
// dark, to docs/images/.
// Usage: node tests/e2e/smoke.mjs [baseUrl]
// Playwright, from the experiment folder: npm install --no-save playwright && npx playwright install chromium
import { createRequire } from 'node:module';
import { mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');

const baseUrl = process.argv[2] ?? 'http://localhost:5090';
const out = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', '..', 'docs', 'images');
mkdirSync(out, { recursive: true });

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
const byId = (id) => page.locator(`[data-testid="${id}"]`);
// The short wait lets MudBlazor's color transitions finish before the picture.
const shot = async (name) => { await page.waitForTimeout(500); await page.screenshot({ path: path.join(out, `${name}.png`), fullPage: true }); };
const fail = (message) => { console.error(`FAIL ${message}`); process.exitCode = 1; };

async function setDark(dark) {
  const isDark = await page.locator('.mud-theme-dark, body.mud-theme-dark').count() > 0
    || await page.evaluate(() => getComputedStyle(document.body).backgroundColor !== 'rgb(255, 255, 255)');
  if (isDark !== dark) {
    await byId('theme').click();
    await page.waitForTimeout(300);
  }
}

// Inbox, laid out like a mail client: an email from a customer becomes a run, its reply waits
// for a person in email_replies, and the person approves it (or edits it) under the email.
async function composeEmail(subject) {
  await byId('compose').click();
  await page.locator('[data-testid="compose-from"] input, input[data-testid="compose-from"]').first().fill('li.wang@example.com');
  await page.keyboard.press('Tab');
  await page.locator('[data-testid="compose-subject"] input, input[data-testid="compose-subject"]').first().fill(subject);
  await page.locator('[data-testid="compose-body"] textarea, textarea[data-testid="compose-body"]').first().fill('The package arrived damaged. I want my money back.');
}

const rowOf = (subject) => page.locator(`[data-testid="mail-row"][data-subject="${subject}"]`);
const subject = `Damaged package ${Date.now()}`;
await page.goto(`${baseUrl}/inbox`);
await byId('inbox').waitFor();
await composeEmail(subject);
await shot('inbox-compose-1440-light');
await byId('compose-send').click();
const row = rowOf(subject);
await row.locator('[data-testid="email-status"][data-status="AwaitingReview"]').waitFor({ timeout: 90000 });
await row.locator('[data-testid="reply-status"][data-status="Pending"]').waitFor({ timeout: 10000 })
  .catch(() => fail('the row should show the reply waiting for a person'));
await byId('approve').waitFor({ timeout: 30000 });
await page.locator('.mud-snackbar').waitFor({ state: 'detached', timeout: 10000 }).catch(() => {});
if (!/approval/.test(await byId('summary').innerText())) { fail('the reply should say on top that it needs approval'); }
await shot('inbox-awaiting-1440-light');
await setDark(true);
await shot('inbox-awaiting-1440-dark');
await page.setViewportSize({ width: 390, height: 844 });
await shot('inbox-awaiting-390-dark');
await setDark(false);
await shot('inbox-awaiting-390-light');
await page.setViewportSize({ width: 1440, height: 1000 });

await byId('approve').click();
await byId('result').waitFor({ state: 'attached' });
await row.locator('[data-testid="reply-status"][data-status="Approved"]').waitFor({ timeout: 15000 })
  .catch(() => fail('the row should show the reply as approved'));
await row.locator('[data-testid="email-status"][data-status="Sent"]').waitFor({ timeout: 15000 })
  .catch(() => fail('the row should show Sent after the approval'));
await shot('inbox-sent-1440-light');

// Edit: the person sends their own version; it passes the rules again and is marked as edited.
const edited = `Edited ${Date.now()}`;
await composeEmail(edited);
await byId('compose-send').click();
const editedRow = rowOf(edited);
await editedRow.locator('[data-testid="reply-status"][data-status="Pending"]').waitFor({ timeout: 90000 });
await byId('edit').waitFor({ timeout: 30000 });
await byId('edit').click();
const mine = 'Your order #A-1077 was delivered on November 7. A teammate will review the damage report and write back.';
await page.locator('[data-testid="edit-input"] textarea, textarea[data-testid="edit-input"]').first().fill(mine);
await shot('inbox-edit-1440-light');
await byId('send-edit').click();
await byId('result').waitFor({ state: 'attached' });
if (!(await byId('draft').innerText()).includes(mine)) { fail('the edited text should be what was sent'); }
await editedRow.locator('[data-testid="reply-status"][data-status="Approved"][data-edited="true"]').waitFor({ timeout: 15000 })
  .catch(() => fail('the row should mark the reply as edited'));
await shot('inbox-edited-1440-light');

await browser.close();
console.log(process.exitCode ? 'smoke: FAILED' : `smoke: OK (screenshots in ${path.relative(process.cwd(), out)})`);
