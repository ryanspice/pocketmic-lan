# Deployment and release checks

## Existing path

Target: `https://canopydigital.ca/sites/pocketmic-lan/`.

Publish the **contents of `site/`** to that directory, preserving nested folders. The server must serve `index.html` for directories. All runtime assets are local and route links use relative paths. No framework adapter, SPA rewrite, API key, Node server, database, form endpoint or server-side rendering is required.

Do not deploy the package root. The social studio, source generator, one-file preview and verification images are review / authoring tools, not consumer routes.

`policy.html` is retained with a canonical pointing to `/privacy/` and a noindex directive. A host-level permanent redirect is an optional later cleanup; the current file avoids breaking the old URL without requiring server configuration.

## Sitemap and host policy

The sitemap contains thirteen indexable routes. `/setup/ready/` and the compatibility policy page are not indexed. Submit the deployed sitemap through the property’s existing tooling.

The included `site/robots.txt` is a template/reference when deploying under a subdirectory: crawler robots rules are ordinarily read from the domain root. Add the sitemap location to the **existing domain-root robots.txt** rather than replacing the domain’s robots configuration with this package. Do not overwrite unrelated Canopy Digital routes or policies.

Review server/CDN logs, retention, any host-injected scripts and the privacy draft before publication. The site code has no third-party analytics, but the delivery environment is outside this package’s control. No invented consent banner or unconnected analytics toggle was added.

## Changing the public base

Regenerate canonical, Open Graph and sitemap URLs with:

```powershell
py .\src\build.py --base-url 'https://example.com/pocketmic/'
```

Also update `BASE` in `scripts/create_marketing.py` and the review-base constants in `scripts/build_preview.py` when moving the deployment. Rebuild marketing, artwork if changed, the preview and verification. The default production path has already been set consistently.

## Release updates

Pinned version values live in `src/build.py`; social copy lives in `scripts/create_marketing.py`. `site/release.json` is generated, not the source of truth to edit independently.

Before changing v0.1.4, inspect the actual release tag, artifact filenames, signatures, packaging, hashes, native requirements, routing requirements and feature scope. Do not make the APK filename versioned while the URL uses `latest/download`. Do not add the unsigned APK to the consumer install button.

Replace screenshots only with new real app captures, removing or masking credentials before capture. A screenshot should not be cosmetically edited to imply a different native interface or connection state. Rebuild cards and review all social drafts after release changes.

## Public smoke test

After deployment, open the homepage, each audience route, both platform routes, setup, ready, troubleshooting, technical and privacy. Check directory indexing, internal links, mobile navigation, images and release links from a clean browser profile.

Inspect response headers and the actual network panel. Verify page metadata, canonical paths and all five wide card-image URLs. Check crawler accessibility on the real host; the local rendering tests cannot establish live-host behavior. Confirm the intended app versions and file hashes on real downloaded files.

Then run the native pairing and routing test on real Android/Windows hardware. Social posts should be published only once their destination pages and relevant setup path work on the host. No deployment or social publication was performed in this delivery.
