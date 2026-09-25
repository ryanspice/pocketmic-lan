import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const read = (relativePath) => readFileSync(new URL(`../${relativePath}`, import.meta.url), "utf8").replace(/^\uFEFF/, "");
const readJson = (relativePath) => JSON.parse(read(relativePath));
const release = readJson("dev/v3/release.json");
const routeManifest = readJson("dev/v3/routes.json");
const siteLocaleCatalog = readJson("localization/site/catalog-status.json");
const targetLocales = readJson("localization/target-locales.json");
const siteRoot = new URL("../dev/v3/", import.meta.url);
const siteRootPath = fileURLToPath(siteRoot);
const htmlPages = [];

function collectHtmlPages(directory, prefix = "") {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const relativePath = prefix ? `${prefix}/${entry.name}` : entry.name;
    if (entry.isDirectory()) {
      collectHtmlPages(new URL(`${entry.name}/`, directory), relativePath);
    } else if (entry.isFile() && entry.name.endsWith(".html")) {
      htmlPages.push({ path: relativePath, url: new URL(entry.name, directory) });
    }
  }
}

collectHtmlPages(siteRoot);
const pageByPath = new Map(htmlPages.map((page) => [page.path, page]));
const pageContent = new Map();
for (const page of htmlPages) {
  const html = readFileSync(page.url, "utf8").replace(/^\uFEFF/, "");
  pageContent.set(page.path, html);
  assert.match(html, /<html\b[^>]*\blang="en-CA"/i, `${page.path} must declare the Canadian English source locale`);
}

assert.equal(siteLocaleCatalog.defaultLocale, "en-CA", "the site fallback must remain Canadian English");
assert.deepEqual(
  Object.keys(siteLocaleCatalog.locales).sort(),
  targetLocales.locales.map((locale) => locale.tag).sort(),
  "site locale status must account for every product locale target",
);
assert.equal(siteLocaleCatalog.locales["en-US"].status, "draft", "US English is a spelling preview until its regional copy is reviewed");
assert.equal(siteLocaleCatalog.locales["en-US"].reviewed, false, "the US English preview must not be reported as reviewed");

const siteLocaleScript = read("dev/v3/assets/site-locale.js");
assert.ok(siteLocaleScript.includes('var DEFAULT_LOCALE = "en-CA"'), "site locale control must use en-CA as fallback");
assert.ok(siteLocaleScript.includes('var US_LOCALE = "en-US"'), "site locale control must expose the separate en-US preview");
assert.ok(siteLocaleScript.includes("return DEFAULT_LOCALE;"), "browser language must not override the Canadian English default");
assert.ok(!siteLocaleScript.includes("navigator.language"), "automatic browser-language selection must not change the site default");
for (const page of htmlPages.filter((candidate) => !candidate.path.startsWith("marketing/"))) {
  const depth = page.path.split("/").length - 1;
  const localeScriptPath = `${"../".repeat(depth)}assets/site-locale.js`;
  const html = pageContent.get(page.path);
  assert.ok(html.includes(`<script src="${localeScriptPath}" defer></script>`), `${page.path} must load the shared locale preference control`);
}

assert.match(release.version, /^v\d+\.\d+\.\d+$/, "release version must be SemVer with a v prefix");
assert.match(release.checkedOn, /^\d{4}-\d{2}-\d{2}$/, "checkedOn must be an ISO date");
assert.equal(release.releaseUrl, `https://github.com/ryanspice/pocketmic-lan/releases/tag/${release.version}`);

for (const [platform, assetName] of [
  ["android", "app-debug.apk"],
  ["windows", "PocketMicReceiver-win-x64.zip"],
]) {
  const asset = release[platform];
  assert.ok(asset, `release manifest is missing ${platform}`);
  assert.match(asset.url, new RegExp(`/releases/download/${release.version}/${assetName.replaceAll(".", "\\.")}$`));
  assert.match(asset.sha256, /^[a-f0-9]{64}$/, `${platform} SHA-256 must contain 64 lowercase hex characters`);
  assert.ok(Number.isSafeInteger(asset.bytes) && asset.bytes > 0, `${platform} byte size must be a positive integer`);
}

assert.equal(new Set(routeManifest.map((route) => route.key)).size, routeManifest.length, "route keys must be unique");
for (const route of routeManifest) {
  assert.ok(existsSync(new URL(route.path, siteRoot)), `route ${route.key} points to missing file ${route.path}`);
}
const sitemapUrls = [...read("dev/v3/sitemap.xml").matchAll(/<loc>([^<]+)<\/loc>/g)].map((match) => match[1]);
for (const route of routeManifest) {
  assert.equal(sitemapUrls.includes(route.url), !route.noindex, `sitemap inclusion must match the indexability of route ${route.key}`);
}
assert.ok(sitemapUrls.every((url) => routeManifest.some((route) => route.url === url)), "sitemap must not contain unregistered routes");

const homepage = pageContent.get("index.html");
assert.ok(homepage, "canonical marketing homepage is missing");
assert.equal((homepage.match(/<h1\b/gi) || []).length, 1, "homepage must have exactly one h1");
assert.ok(homepage.includes('<link rel="canonical" href="https://canopydigital.ca/sites/pocketmic-lan/">'), "homepage canonical URL must match the site root");
assert.ok(homepage.includes('property="og:title"') && homepage.includes('property="og:description"'), "homepage must include Open Graph title and description");
assert.ok(homepage.includes('name="twitter:card"'), "homepage must include Twitter card metadata");
assert.ok(homepage.includes("application/ld+json"), "homepage must include JSON-LD");
const title = homepage.match(/<title>([^<]+)<\/title>/i)?.[1];
assert.equal(routeManifest.find((route) => route.key === "home")?.title, title, "home route title must match the document title");
const jsonLd = homepage.match(/<script type="application\/ld\+json">([\s\S]*?)<\/script>/i)?.[1];
assert.ok(jsonLd, "homepage JSON-LD block must be parseable");
assert.equal(JSON.parse(jsonLd).softwareVersion, release.version.slice(1), "homepage structured-data version must match the latest published release");

for (const [selector, target, href] of [
  ['id="headDl"', "downloads", "download/index.html"],
  ['id="heroCta"', "downloads", "download/index.html"],
  ['id="dlBig"', "downloads", "download/index.html"],
  ['id="androidDl"', "android", "download/android/index.html"],
  ['id="windowsDl"', "windows", "download/windows/index.html"],
]) {
  const start = homepage.indexOf(selector);
  assert.notEqual(start, -1, `homepage CTA ${selector} is missing`);
  const openingTag = homepage.slice(homepage.lastIndexOf("<a", start), homepage.indexOf(">", start) + 1);
  assert.ok(openingTag.includes('data-event="cta_click"'), `homepage CTA ${selector} is missing its conversion event hook`);
  assert.ok(openingTag.includes(`data-target="${target}"`), `homepage CTA ${selector} has the wrong conversion target`);
  assert.ok(openingTag.includes(`href="${href}"`), `homepage CTA ${selector} must point to the checked-in ${href} detail page`);
}

const homeScript = read("dev/v3/assets/marketing-home.js");
const subsiteScript = read("dev/v3/assets/subsite.js");
assert.ok(homepage.includes('<script src="assets/subsite.js" defer></script>'), "homepage must load shared navigation and conversion hooks");
assert.ok(subsiteScript.includes("pocketmic:conversion"), "shared site controls must emit the local conversion event");
assert.ok(!/\bfetch\s*\(|XMLHttpRequest|sendBeacon|api\.github\.com/i.test(`${homeScript}\n${subsiteScript}`), "site scripts must not fetch live GitHub stats or send analytics to a remote service");
assert.ok(!/100\s*[–-]\s*250\s*ms|historical PCM baseline|published PCM baseline/i.test(`${homepage}\n${homeScript}`), "homepage must not publish unsupported end-to-end latency measurements");
assert.ok(homepage.includes("No dated, reproducible end-to-end latency result"), "homepage must state the current latency evidence boundary");
for (const staleClaim of [
  "A working miniature of the PocketMic pipeline",
  "network figures are typical values",
  "good first issues are labeled as such",
  "GOOD FIRST ISSUES",
  "BUY ME A COFFEE — $4",
]) {
  assert.ok(!`${homepage}\n${homeScript}`.includes(staleClaim), `homepage contains an unsupported or stale claim: ${staleClaim}`);
}
assert.ok(homepage.includes("packet-rate readout are illustrative"), "no-JavaScript Signal Lab text must label its simulated network display");
assert.ok(homeScript.includes("packet-rate readout are illustrative"), "Signal Lab copy must remain accurate after JavaScript initializes");
assert.ok(homeScript.includes('noteMuted:"LOCAL BROWSER DEMO MUTED — NO AUDIO IS SENT TO POCKETMIC."'), "muting the local demo must not imply a live receiver connection");
assert.ok(!homeScript.includes("THE RECEIVER SEES SILENCE"), "Signal Lab must not describe a receiver connection it does not create");
assert.ok(homepage.includes('data-i18n="stage.live">PREVIEW</span>'), "the illustrative device mockup must not present a live device connection");
assert.ok(homepage.includes("EXAMPLE PC : 9014") && !homepage.includes("192.168.1.8"), "the device mockup must use an example endpoint, not a plausible private address");
assert.ok(homepage.includes("PCM / OPUS · UDP · LAN") && homeScript.includes("PCM / OPUS · UDP · LAN"), "the architecture diagram must name both supported Android packet formats");
assert.ok(homepage.includes("localization catalog tracks 21 targets"), "homepage must describe the locale count as targets, not supported translations");
assert.ok(homepage.includes("full site is currently Canadian English"), "homepage must state that site translation coverage is limited");
for (const protocolFact of ["48 <span>kHz</span>", "10 <span>ms</span>", "768 <span>kbit/s</span>", "1,000 bytes", "220 ms", "200 ms"]) {
  assert.ok(homepage.includes(protocolFact), `homepage protocol section is missing ${protocolFact}`);
}

function idsIn(html) {
  return new Set([...html.matchAll(/\b(?:id|name)=["']([^"']+)["']/gi)].map((match) => match[1]));
}

function resolveLocalReference(reference, page, context) {
  if (!reference || /^(?:#|data:|mailto:|tel:|javascript:)/i.test(reference)) {
    if (reference?.startsWith("#") && reference.length > 1) {
      const id = decodeURIComponent(reference.slice(1));
      assert.ok(idsIn(pageContent.get(page.path)).has(id), `${page.path} has a link to missing anchor #${id}`);
    }
    return;
  }
  const url = new URL(reference, page.url);
  if (url.protocol !== "file:") return;

  let targetPath = fileURLToPath(url);
  const relative = path.relative(siteRootPath, targetPath);
  assert.ok(relative && relative !== ".." && !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative), `${context} escapes the dev/v3 site root`);
  assert.ok(existsSync(targetPath), `${context} points to missing file ${relative}`);
  if (statSync(targetPath).isDirectory()) targetPath = path.join(targetPath, "index.html");
  assert.ok(existsSync(targetPath), `${context} points to a directory without index.html`);

  if (url.hash && targetPath.toLowerCase().endsWith(".html")) {
    const targetRelative = path.relative(siteRootPath, targetPath).split(path.sep).join("/");
    const targetHtml = targetRelative === page.path ? pageContent.get(page.path) : readFileSync(targetPath, "utf8").replace(/^\uFEFF/, "");
    const id = decodeURIComponent(url.hash.slice(1));
    assert.ok(idsIn(targetHtml).has(id), `${context} points to missing anchor #${id} in ${targetRelative}`);
  }
}

const publicRoutes = routeManifest.map((route) => pageByPath.get(route.path)).filter(Boolean);
assert.equal(publicRoutes.length, routeManifest.length, "every public route must resolve to a collected HTML page");
const brokenReferences = [];
for (const page of publicRoutes) {
  const html = pageContent.get(page.path).replace(/<!--[\s\S]*?-->/g, "");
  for (const match of html.matchAll(/\b(?:href|src|poster)=["']([^"']+)["']/gi)) {
    try {
      resolveLocalReference(match[1], page, `${page.path} ${match[0]}`);
    } catch (error) {
      brokenReferences.push(error.message);
    }
  }
}
assert.equal(brokenReferences.length, 0, `public routes contain broken local references:\n${brokenReferences.join("\n")}`);

const androidPage = read("dev/v3/download/android/index.html");
const windowsPage = read("dev/v3/download/windows/index.html");
const releasePage = read("dev/v3/release-notes/index.html");
const downloadPage = read("dev/v3/download/index.html");
const roadmapPage = read("dev/v3/roadmap/index.html");
for (const [label, page, asset] of [
  ["Android download page", androidPage, release.android],
  ["Windows download page", windowsPage, release.windows],
]) {
  assert.ok(page.includes(release.version), `${label} does not show ${release.version}`);
  assert.ok(page.includes(asset.url), `${label} does not link to its manifest asset`);
  assert.ok(page.includes(asset.sha256), `${label} does not show the manifest checksum`);
  assert.ok(page.includes(`${(asset.bytes / 1_000_000).toFixed(1)} MB`), `${label} does not show the manifest size`);
}

assert.ok(downloadPage.includes(release.version), "downloads index does not show the current release version");
assert.ok(releasePage.includes(`Published ${release.version} artifacts`), "release notes do not identify the manifest version");
assert.ok(releasePage.includes(release.android.url) && releasePage.includes(release.windows.url), "release notes do not link both published assets");
assert.ok(roadmapPage.includes("v0.1.5 is the current published release"), "roadmap does not identify the current published release");
assert.ok(roadmapPage.includes("v0.1.6") && roadmapPage.includes("not released"), "roadmap does not label v0.1.6 as unreleased");
for (const staleClaim of [
  "crash on CI",
  "Current latency is 100–250 ms",
  "~10–20 ms latency",
  "replaces VB-CABLE",
  "Planned for v0.2",
  "VB-CABLE / PM-LAN",
  "Measured latency",
  "An iOS app is possible but requires Swift development",
]) {
  assert.ok(!roadmapPage.includes(staleClaim), `roadmap contains stale or unsupported claim: ${staleClaim}`);
}

const privacyPage = read("dev/v3/privacy/index.html");
for (const disclosure of ["Google Fonts", "fonts.googleapis.com", "microphone demo", "not recorded, uploaded, or sent", "localStorage", "pocketmic-site-locale"]) {
  assert.ok(privacyPage.includes(disclosure), `privacy page is missing the ${disclosure} disclosure`);
}

console.log(`Canonical dev/v3 site: ${publicRoutes.length} routes, local links/hooks, locale defaults, and ${release.version} downloads are consistent.`);
