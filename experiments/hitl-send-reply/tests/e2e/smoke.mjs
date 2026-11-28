// Smoke test of the Web app through a real browser. Needs the app running (dotnet run --project HitlSendReply.AppHost),
// which seeds the demo inbox when no model key is set. First the screenshots, on the demo emails, at 1440
// and 390 px, light and dark, to docs/images/. Then the flows on emails of its own: one is sent, waits
// for a person (kept in email_replies), is opened from the "To review" category and approved in the reply
// composer; a second one is edited, opened with the e key.
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
  // Dark when the page background is dark: its average channel is under half.
  const isDark = await page.evaluate(() => {
    const [r, g, b] = getComputedStyle(document.body).backgroundColor.match(/\d+/g).map(Number);
    return (r + g + b) / 3 < 128;
  });
  if (isDark !== dark) {
    await byId('theme').click();
    await page.waitForTimeout(300);
  }
}

// Inbox, laid out like a mail client: an email from a customer becomes a run, its reply waits
// for a person in email_replies, and the person approves it (or edits it) under the email.
async function composeEmail(subject, body = 'The package arrived damaged. I want my money back.') {
  await byId('compose').click();
  await page.locator('[data-testid="compose-from"] input, input[data-testid="compose-from"]').first().fill('li.wang@example.com');
  await page.keyboard.press('Tab');
  await page.locator('[data-testid="compose-subject"] input, input[data-testid="compose-subject"]').first().fill(subject);
  await page.locator('[data-testid="compose-body"] textarea, textarea[data-testid="compose-body"]').first().fill(body);
}

// Categories filter the list by where each email stands; the open email stays open across them.
async function openCategory(folder) {
  await byId('categories').locator('button').first().click();
  await page.locator(`[data-testid="category"][data-folder="${folder}"]`).first().click();
  await page.waitForURL(folder === 'All' ? /\/inbox$/ : new RegExp(`folder=${folder}`));
}

const rowOf = (subject) => page.locator(`[data-testid="mail-row"][data-subject="${subject}"]`);
const attached = { state: 'attached' };
const status = (row, kind, value, timeout) =>
  row.locator(`[data-testid="${kind}"][data-status="${value}"]`).waitFor({ ...attached, timeout });

// 1. Screenshots on the demo inbox, before this run adds emails of its own.
await page.goto(`${baseUrl}/inbox`);
await byId('inbox').waitFor();
const demo = rowOf('Package arrived damaged');
const seeded = await status(demo, 'reply-status', 'Pending', 60000).then(() => true, () => false);
if (!seeded) {
  console.log('smoke: no demo inbox (Mailbox:DemoSeed is off), screenshots skipped');
} else {
  await byId('compose').click();
  await page.locator('[data-testid="compose-from"] input, input[data-testid="compose-from"]').first().fill('ana.perez@example.com');
  await page.keyboard.press('Tab');
  await page.locator('[data-testid="compose-subject"] input, input[data-testid="compose-subject"]').first().fill('Change of delivery address');
  await page.locator('[data-testid="compose-body"] textarea, textarea[data-testid="compose-body"]').first()
    .fill('Hi,\n\nI moved last week. Could you send order A-1042 to my new address?\n\nThanks,\nAna');
  await shot('inbox-compose-1440-light');
  await page.keyboard.press('Escape');
  await byId('compose-dialog').waitFor({ state: 'detached' });

  await openCategory('Review');
  await demo.click();
  await byId('approve').waitFor({ timeout: 30000 });
  if ((await byId('composer-to').innerText()).trim() !== 'li.wang@example.com') { fail('the composer should show the ticket\'s address'); }
  if (!/approval/.test(await byId('summary').innerText())) { fail('the reply should say on top that it needs approval'); }
  await shot('inbox-awaiting-1440-light');
  await setDark(true);
  await shot('inbox-awaiting-1440-dark');
  await page.setViewportSize({ width: 390, height: 844 });
  await shot('inbox-awaiting-390-dark');
  await setDark(false);
  await shot('inbox-awaiting-390-light');
  await page.goto(`${baseUrl}/inbox`);
  await byId('inbox').waitFor();
  await demo.waitFor();
  await shot('inbox-list-390-light');
  await page.setViewportSize({ width: 1440, height: 1000 });

  await demo.click();
  await byId('edit').waitFor({ timeout: 30000 });
  await byId('why-toggle').click();
  await byId('why').waitFor();
  await shot('inbox-details-1440-light');
  await byId('why-toggle').click();
  await byId('edit').click();
  await shot('inbox-edit-1440-light');
  await byId('cancel-edit').click();

  await rowOf('Where is my order?').click();
  await byId('sent-message').waitFor({ timeout: 30000 });
  await shot('inbox-sent-1440-light');
}

// 2. Approve: an email of this run waits for a person and is approved from "To review".
const subject = `Damaged package ${Date.now()}`;
await page.goto(`${baseUrl}/inbox`);
await byId('inbox').waitFor();
await composeEmail(subject);
await byId('compose-send').click();
const row = rowOf(subject);
await status(row, 'email-status', 'AwaitingReview', 90000);
await status(row, 'reply-status', 'Pending', 10000).catch(() => fail('the row should show the reply waiting for a person'));
await openCategory('Review');
await row.click();
await byId('approve').waitFor({ timeout: 30000 });
await page.locator('.mud-snackbar').waitFor({ state: 'detached', timeout: 10000 }).catch(() => {});
if ((await byId('composer-to').innerText()).trim() !== 'li.wang@example.com') { fail('the composer should show the ticket\'s address'); }
await byId('approve').click();
await byId('result').waitFor(attached);
await openCategory('All');
await status(row, 'reply-status', 'Approved', 15000).catch(() => fail('the row should show the reply as approved'));
await status(row, 'email-status', 'Sent', 15000).catch(() => fail('the row should show Sent after the approval'));

// 3. Edit, opened with the e key: the person sends their own version; it passes the rules again and is marked as edited.
const edited = `Edited ${Date.now()}`;
await composeEmail(edited);
await byId('compose-send').click();
const editedRow = rowOf(edited);
await status(editedRow, 'reply-status', 'Pending', 90000);
await byId('approve').waitFor({ timeout: 30000 });
await page.locator('.mud-snackbar').waitFor({ state: 'detached', timeout: 10000 }).catch(() => {});
await page.locator('body').click({ position: { x: 5, y: 895 } });
await page.keyboard.press('e');
await byId('edit-input').waitFor({ timeout: 5000 }).catch(() => fail('the e key should open the edit'));
const mine = 'Your order #A-1077 was delivered on November 7. A teammate will review the damage report and write back.';
await page.locator('[data-testid="edit-input"] textarea, textarea[data-testid="edit-input"]').first().fill(mine);
await byId('send-edit').click();
await byId('result').waitFor(attached);
if (!(await byId('draft').innerText()).includes(mine)) { fail('the edited text should be what was sent'); }
await editedRow.locator('[data-testid="reply-status"][data-status="Approved"][data-edited="true"]').waitFor({ ...attached, timeout: 15000 })
  .catch(() => fail('the row should mark the reply as edited'));

await browser.close();
console.log(process.exitCode ? 'smoke: FAILED' : `smoke: OK (screenshots in ${path.relative(process.cwd(), out)})`);
