import assert from 'node:assert/strict';
import { execFile, spawn } from 'node:child_process';
import { once } from 'node:events';
import { cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { createServer as createTcpServer } from 'node:net';
import { tmpdir } from 'node:os';
import { basename, dirname, join, resolve } from 'node:path';
import { DatabaseSync } from 'node:sqlite';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';
import { chromium } from 'playwright';

// Test a disposable copy of published output; never use a user's database.
const published = resolve(process.argv[2] ?? '');
assert(existsSync(join(published, 'Feedster.Web.dll')), 'Pass a published Feedster.Web directory.');
const temporaryRoot = mkdtempSync(join(tmpdir(), 'feedster-ui-'));
const appRoot = join(temporaryRoot, 'app');
const artifactRoot = resolve(process.env.UI_ARTIFACTS ?? join(temporaryRoot, 'screenshots'));
cpSync(published, appRoot, { recursive: true });
mkdirSync(join(appRoot, 'data'), { recursive: true });
mkdirSync(join(appRoot, 'images'), { recursive: true });
mkdirSync(artifactRoot, { recursive: true });
const fixture = createServer((request, response) => {
    response.setHeader('Content-Type', 'application/rss+xml');
    response.end('<rss version="2.0"><channel><title>Fixture feed</title><link>https://example.test</link><description>Local test feed</description><item><title>Fresh fixture story</title><link>https://example.test/fresh</link><description>A newly fetched article.</description><pubDate>Mon, 05 Oct 2026 10:00:00 GMT</pubDate></item></channel></rss>');
});
await new Promise(resolve => fixture.listen(0, '127.0.0.1', resolve));
const fixtureUrl = 'http://127.0.0.1:' + fixture.address().port + '/feed.xml';
const portReservation = createTcpServer();
await new Promise(resolve => portReservation.listen(0, '127.0.0.1', resolve));
const port = portReservation.address().port;
await new Promise(resolve => portReservation.close(resolve));
const url = 'http://127.0.0.1:' + port;
let app;
let browser;
let db;
let output = '';
const browserErrors = [];
async function startApp() {
    output = '';
    app = spawn(process.env.DOTNET_EXE ?? 'dotnet', ['Feedster.Web.dll'], {
        cwd: appRoot,
        env: { ...process.env, PORT: String(port), ASPNETCORE_ENVIRONMENT: 'Production',
            ConnectionStrings__DefaultConnection: 'Data Source=./data/database.db;Cache=Shared',
            Logging__LogLevel__Default: 'Warning' },
        stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true
    });
    app.stdout.on('data', data => output += data);
    app.stderr.on('data', data => output += data);
    for (let attempt = 0; attempt < 100; attempt++) {
        if (app.exitCode !== null) throw new Error('Application exited:\n' + output);
        try { if ((await fetch(url)).ok) return; } catch {}
        await new Promise(resolve => setTimeout(resolve, 100));
    }
    throw new Error('Application did not become ready:\n' + output);
}
async function stopApp() {
    if (!app || app.exitCode !== null) return;
    const stopped = once(app, 'exit');
    app.kill();
    const forced = setTimeout(() => app.kill('SIGKILL'), 5000);
    await stopped;
    clearTimeout(forced);
    app = undefined;
}
function seed() {
    db?.close();
    db = new DatabaseSync(join(appRoot, 'data', 'database.db'));
    db.exec('PRAGMA foreign_keys=ON; DELETE FROM FeedFolder; DELETE FROM Articles; DELETE FROM Folders; DELETE FROM Feeds;');
    db.prepare('UPDATE UserSettings SET ArticleRefreshAfterMinutes=0, ArticleExpirationAfterDays=0, ArticleCountOnPage=12, IsDarkMode=1, ShowImages=1, DownloadImages=0').run();
    const insertFeed = db.prepare('INSERT INTO Feeds(FeedId,Name,RssUrl) VALUES(?,?,?)');
    insertFeed.run(1, 'The Daily Perspective', fixtureUrl);
    insertFeed.run(2, 'A very long source name for checking narrow reading screens', fixtureUrl + '?other=1');
    const insertFolder = db.prepare('INSERT INTO Folders(FolderId,Name) VALUES(?,?)');
    insertFolder.run(1, 'Ideas & culture');
    insertFolder.run(2, 'A longer folder name to check mobile navigation and wrapping');
    db.exec('INSERT INTO FeedFolder(FeedsFeedId,FoldersFolderId) VALUES(1,1),(2,1)');
    const insert = db.prepare('INSERT INTO Articles(ArticleId,FeedId,Title,Description,ArticleLink,PublicationDate,ImagePath) VALUES(?,?,?,?,?,?,?)');
    for (let index = 1; index <= 24; index++) {
        const number = String(index).padStart(2, '0');
        insert.run(index, index % 2 ? 2 : 1, 'Article ' + number + ' — Finding a little more room for curiosity',
            index === 24 ? 'A thoughtful look at the small ideas that shape everyday life. ' + 'longword'.repeat(50) : 'Stories, ideas, and a fresh perspective. A quieter way to keep up with the world, one source at a time.',
            index === 23 ? 'javascript:alert(1)' : 'https://example.test/article/' + index,
            '2026-10-' + number + ' 10:00:00', index === 24 ? 'fixture.svg' : null);
    }
    writeFileSync(join(appRoot, 'images', 'fixture.svg'), '<svg xmlns="http://www.w3.org/2000/svg" width="256" height="216"><rect width="256" height="216" fill="#a796de"/><circle cx="160" cy="74" r="42" fill="#f6e8c9"/><path d="M0 180 90 70 180 180Z" fill="#574675"/><path d="M90 216 190 110 256 216Z" fill="#78609d"/></svg>');
}
async function navigate(page, path, heading) {
    await page.goto(url + path);
    await page.getByRole('heading', { name: heading, exact: true }).waitFor();
    await page.locator('main[aria-busy="false"]').waitFor();
}
async function assertNoOverflow(page, label) {
    const geometry = await page.evaluate(() => ({
        viewport: innerWidth, document: document.documentElement.scrollWidth,
        dialog: Array.from(document.querySelectorAll('dialog[open]')).map(dialog => {
            const rect = dialog.getBoundingClientRect();
            return { left: rect.left, right: rect.right, top: rect.top, bottom: rect.bottom };
        }), height: innerHeight
    }));
    assert(geometry.document <= geometry.viewport + 1, label + ': horizontal overflow ' + JSON.stringify(geometry));
    for (const dialog of geometry.dialog) {
        assert(dialog.left >= 0 && dialog.right <= geometry.viewport + 1 && dialog.top >= 0 && dialog.bottom <= geometry.height + 1, label + ': dialog escapes viewport');
    }
    assert.equal(await page.locator('#blazor-error-ui').isVisible(), false, label + ': Blazor error UI');
}
async function waitForDb(query, expected) {
    for (let attempt = 0; attempt < 50; attempt++) {
        if (db.prepare(query).get().value === expected) return;
        await new Promise(resolve => setTimeout(resolve, 100));
    }
    assert.equal(db.prepare(query).get().value, expected, query);
}
try {
    const initializer = resolve(dirname(fileURLToPath(import.meta.url)), '../../Feedster.ContainerSmoke/bin/Release/net10.0/Feedster.ContainerSmoke.dll');
    await promisify(execFile)(process.env.DOTNET_EXE ?? 'dotnet', [initializer, '--initialize-ui-database', join(appRoot, 'data', 'database.db')], { windowsHide: true, timeout: 30000 });
    seed();
    await startApp();
    browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' });
    const page = await context.newPage();
    page.on('pageerror', error => browserErrors.push(error.message));
    page.on('console', message => { if (message.type() === 'error') browserErrors.push(message.text()); });
    await navigate(page, '/', 'All articles');
    const search = page.getByRole('searchbox', { name: 'Search articles' });
    await search.fill('Article 24');
    await page.getByText('1 article', { exact: false }).waitFor();
    assert.equal(await page.locator('.article-card').count(), 1, 'Search responds to input');
    await search.fill('');
    await page.getByText('24 articles', { exact: false }).waitFor();
    assert.equal(await page.locator('.article-card').count(), 12);
    assert.match(await page.locator('.article-card h2').first().innerText(), /Article 24/);
    assert.equal(await page.locator('a[href^="javascript:"]').count(), 0);
    await page.getByRole('button', { name: 'Next', exact: false }).click();
    await page.getByText('Page 2 of 2').waitFor();
    assert.equal(await page.getByRole('button', { name: 'Next', exact: false }).isDisabled(), true, 'Exact multiple has no empty third page');
    assert.match(await page.locator('.article-card h2').first().innerText(), /Article 12/);
    await page.getByRole('button', { name: 'Previous', exact: false }).click();
    await page.getByText('Page 1 of 2').waitFor();
    await search.fill('no matching text');
    await page.getByRole('heading', { name: 'No matching articles' }).waitFor();
    await page.getByRole('button', { name: 'Clear search' }).click();
    await page.getByText('24 articles', { exact: false }).waitFor();
    await page.getByRole('button', { name: 'Feeds in Ideas & culture', exact: true }).click();
    const feedLinks = page.locator('#folder-feeds-1');
    await feedLinks.getByRole('link', { name: 'The Daily Perspective', exact: true }).click();
    await page.getByRole('heading', { name: 'The Daily Perspective', exact: true }).waitFor();
    assert.equal(await page.locator('.article-card').count(), 12);
    await feedLinks.getByRole('link', { name: 'A very long source name for checking narrow reading screens', exact: true }).click();
    await page.getByRole('heading', { name: 'A very long source name for checking narrow reading screens', exact: true }).waitFor();
    assert.match(await page.locator('.article-card h2').first().innerText(), /Article 23/, 'Changing feed route refreshes articles');

    await navigate(page, '/folders/manage', 'Manage folders');
    await page.getByRole('button', { name: 'Edit Ideas & culture', exact: true }).click();
    const editor = page.getByRole('dialog');
    await editor.waitFor();
    await page.getByLabel('Folder name', { exact: true }).fill('Discarded draft');
    await editor.getByLabel('The Daily Perspective', { exact: true }).uncheck();
    await page.getByRole('button', { name: 'Cancel', exact: true }).click();
    await editor.waitFor({ state: 'hidden' });
    assert.equal(db.prepare('SELECT Name AS value FROM Folders WHERE FolderId=1').get().value, 'Ideas & culture');
    assert.equal(db.prepare('SELECT COUNT(*) AS value FROM FeedFolder WHERE FoldersFolderId=1').get().value, 2);
    await page.getByRole('button', { name: 'Edit Ideas & culture', exact: true }).click();
    await editor.waitFor();
    assert.equal(await page.getByLabel('Folder name', { exact: true }).inputValue(), 'Ideas & culture');
    assert.equal(await editor.getByLabel('The Daily Perspective', { exact: true }).isChecked(), true);
    await page.getByLabel('Folder name', { exact: true }).fill('Saved reading list');
    await page.getByRole('button', { name: 'Save folder', exact: true }).click();
    await editor.waitFor({ state: 'hidden' });
    await page.locator('nav').getByRole('link', { name: /Saved reading list/ }).waitFor();
    await waitForDb('SELECT Name AS value FROM Folders WHERE FolderId=1', 'Saved reading list');
    await page.getByRole('button', { name: 'Delete Saved reading list', exact: true }).click();
    await page.getByRole('dialog', { name: 'Delete folder?' }).waitFor();
    assert.equal(await page.getByRole('button', { name: 'Cancel', exact: true }).evaluate(element => element === document.activeElement), true);
    await page.keyboard.press('Escape');
    await page.getByRole('dialog').waitFor({ state: 'hidden' });
    assert.equal(db.prepare('SELECT COUNT(*) AS value FROM Folders WHERE FolderId=1').get().value, 1);
    assert.equal(await page.getByRole('button', { name: 'Delete Saved reading list', exact: true }).evaluate(element => element === document.activeElement), true, 'Dialog restores opener focus');

    await navigate(page, '/feeds/manage', 'Manage feeds');
    await page.getByRole('button', { name: 'Edit The Daily Perspective', exact: true }).click();
    await editor.waitFor();
    await page.getByLabel('Name', { exact: true }).fill('Cancelled feed draft');
    await page.keyboard.press('Escape');
    await editor.waitFor({ state: 'hidden' });
    assert.equal(db.prepare('SELECT Name AS value FROM Feeds WHERE FeedId=1').get().value, 'The Daily Perspective');
    await page.getByRole('button', { name: 'Add feed', exact: true }).click();
    await editor.waitFor();
    await page.getByRole('button', { name: 'Save feed', exact: true }).click();
    await editor.locator('.validation-message').first().waitFor();
    await page.getByLabel('Name', { exact: true }).fill('New local source');
    await page.getByLabel('RSS or Atom URL', { exact: true }).fill(fixtureUrl);
    await page.getByRole('button', { name: 'Save feed', exact: true }).click();
    await editor.waitFor({ state: 'hidden' });
    await page.getByRole('button', { name: 'Update New local source', exact: true }).waitFor({ state: 'visible' });
    await page.waitForFunction(() => !document.querySelector('button[aria-label="Update New local source"]').disabled);
    await waitForDb("SELECT COUNT(*) AS value FROM Articles a JOIN Feeds f ON f.FeedId=a.FeedId WHERE f.Name='New local source'", 1);
    await page.getByRole('button', { name: 'Dismiss notification', exact: true }).click();
    await page.getByRole('button', { name: 'Update New local source', exact: true }).click();
    await page.getByText("You're up to date", { exact: true }).waitFor();
    await page.getByRole('button', { name: 'Delete New local source', exact: true }).click();
    await page.getByRole('dialog', { name: 'Delete feed?' }).waitFor();
    await page.getByRole('button', { name: 'Confirm', exact: true }).click();
    await waitForDb("SELECT COUNT(*) AS value FROM Feeds WHERE Name='New local source'", 0);

    await navigate(page, '/settings', 'Settings');
    const save = page.getByRole('button', { name: 'Save changes', exact: true });
    assert.equal(await save.isDisabled(), true);
    await page.getByLabel('Check for new articles', { exact: true }).selectOption('15');
    await page.getByLabel('Articles per page', { exact: true }).fill('0');
    await page.getByLabel('Dark mode', { exact: false }).uncheck();
    await page.locator('.app-shell:not(.dark)').waitFor();
    await save.click();
    await page.getByText('Preferences saved', { exact: true }).waitFor();
    assert.equal(await save.isDisabled(), true);
    await waitForDb('SELECT ArticleCountOnPage AS value FROM UserSettings', 0);
    await waitForDb('SELECT IsDarkMode AS value FROM UserSettings', 0);
    await page.getByRole('button', { name: 'Dismiss notification', exact: true }).click();
    await page.getByLabel('Articles per page', { exact: true }).fill('-1');
    await page.getByLabel('Articles per page', { exact: true }).press('Tab');
    await page.locator('.validation-message').first().waitFor();
    await waitForDb('SELECT ArticleCountOnPage AS value FROM UserSettings', 0);
    await page.getByLabel('Articles per page', { exact: true }).fill('12');
    await page.getByLabel('Articles per page', { exact: true }).press('Tab');
    await save.click();
    await page.getByText('Preferences saved', { exact: true }).waitFor();
    await page.getByLabel('Dark mode', { exact: false }).check();
    await page.locator('.app-shell.dark').waitFor();
    await page.getByRole('link', { name: 'All articles', exact: true }).click();
    await page.getByRole('heading', { name: 'All articles', exact: true }).waitFor();
    await page.locator('.app-shell:not(.dark)').waitFor();
    await waitForDb('SELECT IsDarkMode AS value FROM UserSettings', 0);

    // Geometry and screenshots cover all main routes, both themes, and short dialogs.
    for (const dark of [false, true]) {
        db.prepare('UPDATE UserSettings SET IsDarkMode=?').run(Number(dark));
        for (const [width, height] of [[1440,1000], [820,1000], [390,844], [320,568]]) {
            await page.setViewportSize({ width, height });
            for (const [path, heading] of [['/', 'All articles'], ['/feeds/manage', 'Manage feeds'], ['/folders/manage', 'Manage folders'], ['/settings', 'Settings'], ['/folder/1', 'Saved reading list'], ['/feed/1', 'The Daily Perspective']]) {
                await navigate(page, path, heading);
                await assertNoOverflow(page, path + ' ' + width + ' ' + dark);
                if ((path === '/' || path === '/settings') && width !== 320) {
                    await page.screenshot({ path: join(artifactRoot, (dark ? 'dark' : 'light') + '-' + width + '-' + (path === '/' ? 'articles' : 'settings') + '.png') });
                }
            }
            await navigate(page, '/folders/manage', 'Manage folders');
            await page.getByRole('button', { name: 'Add folder', exact: true }).click();
            await editor.waitFor();
            await assertNoOverflow(page, 'Editor ' + width + ' ' + dark);
            assert.equal(await page.locator('body').evaluate(element => element.classList.contains('dialog-open')), true);
            await page.keyboard.press('Tab');
            assert.equal(await editor.evaluate(element => element.contains(document.activeElement)), true, 'Focus remains in dialog');
            await page.screenshot({ path: join(artifactRoot, (dark ? 'dark' : 'light') + '-' + width + '-editor.png') });
            await page.keyboard.press('Escape');
            await editor.waitFor({ state: 'hidden' });
            await page.waitForFunction(() => !document.body.classList.contains('dialog-open'));
            if (width <= 700) {
                await page.getByRole('button', { name: 'Open navigation', exact: true }).click();
                await page.locator('#sidebar-navigation.is-open').waitFor();
                await assertNoOverflow(page, 'Mobile navigation ' + width);
                await page.getByRole('link', { name: 'All articles', exact: true }).click();
                await page.getByRole('heading', { name: 'All articles', exact: true }).waitFor();
                assert.equal(await page.getByRole('button', { name: 'Open navigation', exact: true }).getAttribute('aria-expanded'), 'false');
            }
        }
    }
    await page.setViewportSize({ width: 1440, height: 1000 });
    await navigate(page, '/settings', 'Settings');
    const exported = await page.request.get(url + '/api/Opml/export');
    assert.equal(exported.status(), 200);
    assert.match(await exported.text(), /The Daily Perspective/);
    const opml = join(temporaryRoot, 'subscriptions.opml');
    writeFileSync(opml, '<?xml version="1.0"?><opml version="2.0"><head><title>Test</title></head><body><outline text="Imported source" xmlUrl="' + fixtureUrl + '?import=1" type="rss"/></body></opml>');
    await page.getByLabel('Import subscriptions', { exact: true }).setInputFiles(opml);
    await page.getByText('Subscriptions imported', { exact: true }).waitFor();
    await page.getByRole('button', { name: 'Clear articles', exact: true }).click();
    await page.getByRole('dialog', { name: 'Clear all articles?' }).waitFor();
    await page.getByRole('button', { name: 'Cancel', exact: true }).click();
    assert.equal(db.prepare('SELECT COUNT(*) AS value FROM Articles').get().value, 24, 'Cancel preserves articles');
    assert.equal(await save.isDisabled(), true, 'Import and cache actions do not submit settings');
    assert.deepEqual(browserErrors, [], 'No browser errors');
    if (process.env.README_SCREENSHOTS) {
        const screenshots = resolve(process.env.README_SCREENSHOTS);
        mkdirSync(screenshots, { recursive: true });
        seed();
        db.exec("UPDATE Feeds SET Name='Hacker News' WHERE FeedId=1; UPDATE Feeds SET Name='Developer Notes' WHERE FeedId=2; UPDATE Folders SET Name='Technology' WHERE FolderId=1; UPDATE Folders SET Name='Reading list' WHERE FolderId=2;");
        const stories = [
            ['Building software that works without a network', 'Local-first applications keep your data on your device and sync when a connection becomes available. A look at the design choices behind them.'],
            ['A field guide to the modern web platform', 'An overview of browser capabilities, from native dialogs and container queries to the tools that make responsive interfaces simpler.'],
            ['What makes a good personal website?', 'A collection of small, independent sites and the people behind them. Practical ideas for publishing your own work on the web.'],
            ['The tools we keep coming back to', 'Developers share the editors, utilities, and simple workflows they rely on to get their work done.'],
            ['Understanding how SQLite stores your data', 'A tour through pages, indexes, and transactions in a database that fits into a single file.'],
            ['Notes from a weekend of building', 'A small project, a few useful discoveries, and the lessons that only show up when you put an idea into practice.']
        ];
        const update = db.prepare('UPDATE Articles SET Title=?, Description=?, ArticleLink=?, PublicationDate=? WHERE ArticleId=?');
        for (let index = 1; index <= 24; index++) {
            const story = stories[(24 - index) % stories.length];
            update.run(story[0], story[1], 'https://example.test/articles/' + index, '2026-10-05 ' + String(Math.floor(index / 2)).padStart(2, '0') + ':00:00', index);
        }
        for (const dark of [false, true]) {
            db.prepare('UPDATE UserSettings SET IsDarkMode=?').run(Number(dark));
            const theme = dark ? 'dark' : 'light';
            await page.setViewportSize({ width: 1440, height: 1000 });
            await navigate(page, '/', 'All articles');
            await page.screenshot({ path: join(screenshots, 'articles-' + theme + '.png') });
            await navigate(page, '/settings', 'Settings');
            await page.screenshot({ path: join(screenshots, 'settings-' + theme + '.png') });
            await page.setViewportSize({ width: 390, height: 844 });
            await navigate(page, '/', 'All articles');
            await page.screenshot({ path: join(screenshots, 'mobile-' + theme + '.png') });
        }
        console.log('README screenshots: ' + screenshots);
    }
    console.log('UI smoke passed: pagination, search, routing, draft cancel, folder save, feed fetch/delete, settings, OPML, focus, and 48 responsive/theme route checks.');
    console.log('Screenshots: ' + artifactRoot);
} catch (error) {
    console.error('Browser errors: ' + JSON.stringify(browserErrors));
    if (browser) {
        const pages = browser.contexts().flatMap(context => context.pages());
        if (pages[0]) console.error('Main aria-busy: ' + await pages[0].locator('main').getAttribute('aria-busy'));
        if (pages[0]) await pages[0].screenshot({ path: join(artifactRoot, 'failure.png'), fullPage: true }).catch(() => {});
    }
    console.error(output.slice(-6000));
    throw error;
} finally {
    await browser?.close();
    await stopApp();
    db?.close();
    await new Promise(resolve => fixture.close(resolve));
    // Only remove the private test workspace created by this invocation.
    assert.equal(dirname(resolve(temporaryRoot)), resolve(tmpdir()));
    assert(basename(temporaryRoot).startsWith('feedster-ui-'));
    if (!artifactRoot.startsWith(resolve(temporaryRoot))) rmSync(temporaryRoot, { recursive: true, force: true });
}
