import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { releasePagePaths, validateReleaseChannels } from "./site-release-contract.mjs";

const read = (relative) => readFileSync(new URL(`../dev/v3/${relative}`, import.meta.url), "utf8");
const manifest = JSON.parse(read("release.json"));
const pages = new Map(releasePagePaths.map((relative) => [relative, read(relative)]));
const stable = manifest.channels.stable;
const prerelease = manifest.channels.prerelease;

function rejectPageChange(page, before, after, expected) {
  const changed = new Map(pages);
  assert.ok(changed.get(page).includes(before), "regression fixture must change existing public content");
  changed.set(page, changed.get(page).replace(before, after));
  assert.throws(() => validateReleaseChannels(manifest, changed), expected);
}

test("checked-in channels agree with all canonical release pages", () => {
  validateReleaseChannels(manifest, pages);
});

test("a stable primary download cannot silently become a prerelease", () => {
  rejectPageChange("download/android/index.html", stable.android.url, prerelease.android.url, /stable contains the other channel/);
});

test("a prerelease download cannot point back to stable", () => {
  rejectPageChange("download/windows/index.html", prerelease.windows.url, stable.windows.url, /prerelease contains the other channel/);
});

test("a development download cannot follow GitHub latest", () => {
  rejectPageChange("download/index.html", prerelease.android.url, "https://github.com/ryanspice/pocketmic-lan/releases/latest/download/app-debug.apk", /version-pinned release links/);
});

test("a syntactically valid but wrong tag is rejected", () => {
  rejectPageChange("download/android/index.html", stable.android.url, stable.android.url.replace(stable.version, "v9.9.9"), /unexpected channel\/version link/);
});

test("a wrong displayed development version fails even when download URLs are correct", () => {
  rejectPageChange("download/index.html", `Development prerelease · ${prerelease.version}`, "Development prerelease · v9.9.9", /displayed channel version must match/);
});

test("the stable checksum is checked separately from the prerelease checksum", () => {
  rejectPageChange("download/android/index.html", stable.android.sha256, prerelease.android.sha256, /stable android is missing its manifest sha256/);
});

test("a plausible but incorrect development checksum is rejected", () => {
  rejectPageChange("download/windows/index.html", prerelease.windows.sha256, "a".repeat(64), /prerelease windows is missing its manifest sha256/);
});

test("exact bytes are checked even if rounded megabytes look right", () => {
  rejectPageChange("download/windows/index.html", `${stable.windows.bytes.toLocaleString("en-CA")} bytes`, `${(stable.windows.bytes + 1).toLocaleString("en-CA")} bytes`, /stable windows is missing its manifest bytes/);
});

test("rounded display size must also agree with metadata", () => {
  rejectPageChange("download/index.html", `${(prerelease.windows.bytes / 1_000_000).toFixed(1)} MB`, "1.0 MB", /prerelease windows is missing its manifest size/);
});

test("signing disclosure is part of each artifact contract", () => {
  rejectPageChange("download/windows/index.html", "<dd>unsigned</dd>", "<dd>signed</dd>", /stable windows is missing its manifest signing/);
});

test("a prerelease must be visibly labelled as development", () => {
  const changed = new Map(pages);
  changed.set("download/index.html", pages.get("download/index.html").replaceAll("Development prerelease", "Preview"));
  assert.throws(() => validateReleaseChannels(manifest, changed), /prerelease must state its channel label/);
});

test("removing the optional development section cannot hide the channel contract", () => {
  const changed = new Map(pages);
  changed.set("index.html", pages.get("index.html").replace(/<section\b[^>]*data-release-channel="prerelease"[\s\S]*?<\/section>/, ""));
  assert.throws(() => validateReleaseChannels(manifest, changed), /prerelease must show its manifest version/);
});

for (const pagePath of releasePagePaths.filter((relative) => relative !== "roadmap/index.html")) {
  test(`${pagePath} development downloads must stay closed by default`, () => {
    for (const attribute of ["open", 'open="false"']) {
      const changed = new Map(pages);
      const html = pages.get(pagePath);
      // Scope the mutation to the development section: homepage has other details.
      const offset = html.indexOf('data-release-channel="prerelease"');
      changed.set(pagePath, html.slice(0, offset) + html.slice(offset).replace("<details", `<details ${attribute}`));
      assert.throws(() => validateReleaseChannels(manifest, changed), /must be closed by default/);
    }
  });

  test(`${pagePath} development downloads require an opt-in disclosure`, () => {
    const changed = new Map(pages);
    changed.set(pagePath, pages.get(pagePath).replace(/(<section\b[^>]*data-release-channel="prerelease"[\s\S]*?)<details\b[^>]*>([\s\S]*?)<\/details>/, "$1$2"));
    assert.throws(() => validateReleaseChannels(manifest, changed), /must have one opt-in details disclosure/);
  });

  test(`${pagePath} development summary must disclose its channel before expanding`, () => {
    const changed = new Map(pages);
    const label = `Development prerelease · ${prerelease.version}`;
    const html = pages.get(pagePath);
    const offset = html.indexOf('data-release-channel="prerelease"');
    changed.set(pagePath, html.slice(0, offset) + html.slice(offset).replace(label, "More downloads").replace("</summary>", `</summary><p>${label}</p>`));
    assert.throws(() => validateReleaseChannels(manifest, changed), /summary must explicitly label its development channel and version/);
  });
}

test("development download links cannot escape a closed disclosure", () => {
  const changed = new Map(pages);
  changed.set("download/index.html", pages.get("download/index.html").replace("</details>", `</details><a href="${prerelease.android.url}">Development APK</a>`));
  assert.throws(() => validateReleaseChannels(manifest, changed), /links must stay inside the opt-in disclosure/);
});

test("manifest channel flags and URLs cannot disagree", () => {
  const changed = structuredClone(manifest);
  changed.channels.prerelease.prerelease = false;
  assert.throws(() => validateReleaseChannels(changed, pages), /prerelease flag disagrees/);
  changed.channels.prerelease.prerelease = true;
  changed.channels.prerelease.android.url = stable.android.url;
  assert.throws(() => validateReleaseChannels(changed, pages), /asset URL must pin its channel version/);
});

test("homepage structured data remains on stable", () => {
  rejectPageChange("index.html", `"softwareVersion": "${stable.version.slice(1)}"`, `"softwareVersion": "${prerelease.version.slice(1)}"`, /structured data must describe the default stable/);
});

test("homepage runtime labels cannot show a different version from the default download", () => {
  rejectPageChange("index.html", `data-stable-version="${stable.version}"`, 'data-stable-version="v9.9.9"', /runtime labels must use the manifest stable version/);
});

test("roadmap milestone and source baseline follow metadata", () => {
  rejectPageChange("roadmap/index.html", `data-next-milestone="${manifest.nextMilestone.version}"`, 'data-next-milestone="v9.9.9"', /manifest nextMilestone version/);
  rejectPageChange("roadmap/index.html", `data-source-baseline="${manifest.sourceBaseline.version}"`, 'data-source-baseline="v9.9.9"', /manifest sourceBaseline version/);
});

test("a coordinated metadata and page update needs no validator version edits", () => {
  const changed = structuredClone(manifest);
  const substitutions = new Map();
  const versions = { stable: "v4.2.0", prerelease: "v4.2.1" };
  let serial = 0;
  for (const channel of ["stable", "prerelease"]) {
    const before = manifest.channels[channel];
    const after = changed.channels[channel];
    substitutions.set(before.version, versions[channel]);
    after.version = versions[channel];
    after.releaseUrl = before.releaseUrl.replace(before.version, after.version);
    for (const platform of ["android", "windows"]) {
      const old = before[platform];
      const asset = after[platform];
      asset.url = old.url.replace(before.version, after.version);
      asset.sha256 = String(++serial).repeat(64);
      asset.bytes = 40_000_001 + serial * 20_000_000;
      substitutions.set(old.sha256, asset.sha256);
      substitutions.set(`${old.bytes.toLocaleString("en-CA")} bytes`, `${asset.bytes.toLocaleString("en-CA")} bytes`);
      substitutions.set(`${(old.bytes / 1_000_000).toFixed(1)} MB`, `${(asset.bytes / 1_000_000).toFixed(1)} MB`);
    }
  }
  for (const [key, version] of [["sourceBaseline", "v4.1.0"], ["nextMilestone", "v4.3.0"]]) {
    substitutions.set(changed[key].version, version);
    changed[key].version = version;
  }
  substitutions.set(`"softwareVersion": "${stable.version.slice(1)}"`, `"softwareVersion": "${changed.channels.stable.version.slice(1)}"`);
  const pattern = new RegExp([...substitutions.keys()].map((value) => value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")).join("|"), "g");
  const changedPages = new Map([...pages].map(([relative, html]) => [relative, html.replace(pattern, (value) => substitutions.get(value))]));
  validateReleaseChannels(changed, changedPages);
});
