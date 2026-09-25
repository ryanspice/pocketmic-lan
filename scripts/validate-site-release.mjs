import assert from "node:assert/strict";
import { readdirSync, readFileSync } from "node:fs";

const read = (path) => readFileSync(new URL(`../${path}`, import.meta.url), "utf8");
const release = JSON.parse(read("dev/v3/release.json"));
const routeManifest = JSON.parse(read("dev/v3/routes.json"));
const siteLocaleCatalog = JSON.parse(read("localization/site/catalog-status.json"));
const targetLocales = JSON.parse(read("localization/target-locales.json"));

const siteRoot = new URL("../dev/v3/", import.meta.url);
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
for (const page of htmlPages) {
  assert.match(readFileSync(page.url, "utf8"), /<html\b[^>]*\blang="en-CA"/, `${page.path} must declare the Canadian English source locale`);
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
for (const page of htmlPages.filter((candidate) => !candidate.path.startsWith("marketing/"))) {
  const depth = page.path.split("/").length - 1;
  const localeScriptPath = `${"../".repeat(depth)}assets/site-locale.js`;
  const html = readFileSync(page.url, "utf8");
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
]) {
  assert.ok(!roadmapPage.includes(staleClaim), `roadmap contains stale or unsupported claim: ${staleClaim}`);
}

const releaseRoute = routeManifest.find((route) => route.key === "release");
assert.ok(releaseRoute, "routes manifest is missing the release page");
assert.ok(releaseRoute.title.includes(release.version), "release route title does not match the manifest version");

console.log(`Release-site metadata and ${release.version} download pages are consistent.`);
