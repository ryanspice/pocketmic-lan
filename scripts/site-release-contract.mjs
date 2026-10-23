import assert from "node:assert/strict";

const repository = "https://github.com/ryanspice/pocketmic-lan";
const assetNames = { android: "app-debug.apk", windows: "PocketMicReceiver-win-x64.zip" };
const labels = { stable: "Stable release", prerelease: "Development prerelease" };
export const releasePagePaths = [
  "index.html", "download/index.html", "download/android/index.html",
  "download/windows/index.html", "release-notes/index.html", "roadmap/index.html",
];

// Channel sections deliberately contain no nested section elements. Keeping the
// contract in static HTML also makes both download paths usable without scripts.
function channelContent(html, channel) {
  const blocks = [...html.matchAll(/<section\b[^>]*\bdata-release-channel="([^"]+)"[^>]*>[\s\S]*?<\/section>/gi)];
  for (const block of blocks) {
    assert.ok(Object.hasOwn(labels, block[1]), `unknown download channel ${block[1]}`);
    assert.ok(!/<section\b/i.test(block[0].slice(block[0].indexOf(">") + 1)), "release channel sections must not contain nested sections");
  }
  if (channel === "prerelease") return blocks.filter((block) => block[1] === channel).map((block) => block[0]).join("\n");
  return blocks.filter((block) => block[1] === "prerelease").reduce((content, block) => content.replace(block[0], ""), html);
}

function validateAsset(content, asset, platform, context) {
  for (const [field, expected] of Object.entries({
    url: asset.url,
    sha256: asset.sha256,
    size: `${(asset.bytes / 1_000_000).toFixed(1)} MB`,
    bytes: `${asset.bytes.toLocaleString("en-CA")} bytes`,
    signing: asset.signing,
  })) {
    assert.ok(content.includes(expected), `${context} ${platform} is missing its manifest ${field}: ${expected}`);
  }
}

function validatePrereleaseOptIn(content, version, pagePath) {
  const disclosures = [...content.matchAll(/<details\b([^>]*)>([\s\S]*?)<\/details>/gi)];
  assert.equal(disclosures.length, 1, `${pagePath} prerelease downloads must have one opt-in details disclosure`);
  const [disclosure, attributes, body] = disclosures[0];
  const names = [...attributes.matchAll(/\s+([^\s=/>]+)(?:\s*=\s*(?:"[^"]*"|'[^']*'|[^\s>]+))?/g)].map((match) => match[1].toLowerCase());
  assert.ok(!names.includes("open"), `${pagePath} prerelease opt-in disclosure must be closed by default`);
  const summary = body.match(/^\s*<summary\b[^>]*>([\s\S]*?)<\/summary>/i)?.[1];
  assert.ok(summary, `${pagePath} prerelease opt-in disclosure must start with a summary`);
  assert.ok(summary.replace(/<[^>]+>/g, "").includes(`${labels.prerelease} · ${version}`), `${pagePath} prerelease summary must explicitly label its development channel and version`);
  assert.ok(!/<a\b/i.test(summary), `${pagePath} prerelease links must require expanding the opt-in disclosure`);
  assert.ok(!/<a\b/i.test(content.replace(disclosure, "")), `${pagePath} prerelease links must stay inside the opt-in disclosure`);
}

export function validateReleaseChannels(manifest, pages) {
  assert.equal(manifest.defaultChannel, "stable", "default downloads must use the stable channel");
  assert.match(manifest.checkedOn, /^\d{4}-\d{2}-\d{2}$/, "checkedOn must be an ISO date");
  assert.deepEqual(Object.keys(manifest.channels).sort(), ["prerelease", "stable"], "manifest must distinguish both release channels");
  for (const [channel, release] of Object.entries(manifest.channels)) {
    assert.equal(release.prerelease, channel === "prerelease", `${channel} prerelease flag disagrees with its channel`);
    assert.match(release.version, /^v\d+\.\d+\.\d+$/, `${channel} version must be SemVer with a v prefix`);
    assert.equal(release.releaseUrl, `${repository}/releases/tag/${release.version}`, `${channel} release URL must pin its version`);
    for (const [platform, filename] of Object.entries(assetNames)) {
      const asset = release[platform];
      assert.ok(asset, `${channel} manifest is missing ${platform}`);
      assert.equal(asset.url, `${repository}/releases/download/${release.version}/${filename}`, `${channel} ${platform} asset URL must pin its channel version`);
      assert.match(asset.sha256, /^[a-f0-9]{64}$/, `${channel} ${platform} SHA-256 must be lowercase hex`);
      assert.ok(Number.isSafeInteger(asset.bytes) && asset.bytes > 0, `${channel} ${platform} bytes must be a positive integer`);
      assert.equal(asset.signing, platform === "android" ? "debug-signed" : "unsigned", `${channel} ${platform} signing must describe the current distribution`);
      if (platform === "windows") assert.equal(asset.packaging, "self-contained ZIP");
    }
  }
  assert.notEqual(manifest.channels.stable.version, manifest.channels.prerelease.version, "channels must identify distinct releases");
  assert.match(manifest.sourceBaseline.version, /^v\d+\.\d+\.\d+$/);
  assert.equal(manifest.sourceBaseline.status, "historical source baseline");
  assert.match(manifest.nextMilestone.version, /^v\d+\.\d+\.\d+$/);
  assert.equal(manifest.nextMilestone.status, "next Apple development milestone");

  for (const pagePath of releasePagePaths) {
    const html = pages.get(pagePath);
    assert.ok(html, `missing release page ${pagePath}`);
    assert.ok(!/releases\/latest(?:[\/"?#]|$)/i.test(html), `${pagePath} must use version-pinned release links`);
    for (const [channel, release] of Object.entries(manifest.channels)) {
      const content = channelContent(html, channel);
      assert.ok(content.includes(release.version), `${pagePath} ${channel} must show its manifest version`);
      assert.ok(content.includes(labels[channel]), `${pagePath} ${channel} must state its channel label`);
      assert.ok(content.includes(`${labels[channel]} · ${release.version}`), `${pagePath} ${channel} displayed channel version must match its manifest`);
      if (channel === "prerelease" && pagePath !== "roadmap/index.html") validatePrereleaseOptIn(content, release.version, pagePath);
      assert.ok(!content.includes(manifest.channels[channel === "stable" ? "prerelease" : "stable"].android.url), `${pagePath} ${channel} contains the other channel's Android download`);
      assert.ok(!content.includes(manifest.channels[channel === "stable" ? "prerelease" : "stable"].windows.url), `${pagePath} ${channel} contains the other channel's Windows download`);
      const allowedLinks = new Set([release.releaseUrl, release.android.url, release.windows.url, `${repository}/releases/download/${release.version}/SHA256SUMS.txt`]);
      for (const [, href] of content.matchAll(/\bhref="(https:\/\/github\.com\/ryanspice\/pocketmic-lan\/releases\/(?:download|tag)\/[^"#]+)"/g)) {
        assert.ok(allowedLinks.has(href), `${pagePath} ${channel} has an unexpected channel/version link: ${href}`);
      }
      if (pagePath.includes("download/android/")) validateAsset(content, release.android, "android", `${pagePath} ${channel}`);
      if (pagePath.includes("download/windows/")) validateAsset(content, release.windows, "windows", `${pagePath} ${channel}`);
      if (pagePath === "release-notes/index.html" || (pagePath === "download/index.html" && channel === "prerelease")) {
        for (const platform of Object.keys(assetNames)) validateAsset(content, release[platform], platform, `${pagePath} ${channel}`);
      }
    }
    const stable = channelContent(html, "stable");
    assert.ok(!stable.includes(manifest.channels.prerelease.version), `${pagePath} must keep prerelease version text inside its labelled section`);
  }
  const homepage = channelContent(pages.get("index.html"), "stable");
  assert.ok(homepage.includes(`data-stable-version="${manifest.channels.stable.version}"`), "homepage runtime labels must use the manifest stable version");
  const jsonLd = homepage.match(/<script type="application\/ld\+json">([\s\S]*?)<\/script>/i)?.[1];
  assert.equal(JSON.parse(jsonLd).softwareVersion, manifest.channels.stable.version.slice(1), "homepage structured data must describe the default stable release");
  const roadmap = pages.get("roadmap/index.html");
  for (const [key, attribute] of [["sourceBaseline", "data-source-baseline"], ["nextMilestone", "data-next-milestone"]]) {
    assert.ok(roadmap.includes(`${attribute}="${manifest[key].version}"`), `roadmap must identify the manifest ${key} version`);
    assert.ok(roadmap.includes(manifest[key].status), `roadmap must identify the ${manifest[key].status}`);
  }
}
