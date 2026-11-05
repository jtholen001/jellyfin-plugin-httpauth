#!/usr/bin/env node
// Helpers used by .github/workflows/publish.yml.
//   stamp    - write the release version and changelog into build.yaml / Directory.Build.props
//   manifest - add a release to a Jellyfin plugin manifest.json (newest version first)
import { readFileSync, writeFileSync, existsSync } from "node:fs";
import { createHash } from "node:crypto";
import { basename } from "node:path";
import { pathToFileURL } from "node:url";

const TAG_RE = /^v?(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?(?:-[0-9A-Za-z.-]+)?$/;

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i += 2) {
    if (!argv[i].startsWith("--") || i + 1 >= argv.length) {
      throw new Error(`Invalid arguments near "${argv[i]}"`);
    }
    args[argv[i].slice(2)] = argv[i + 1];
  }
  return args;
}

function requireArgs(args, ...names) {
  for (const name of names) {
    if (!args[name]) throw new Error(`Missing --${name}`);
  }
}

export function tagToVersion(tag) {
  const m = TAG_RE.exec(tag);
  if (!m) throw new Error(`Tag "${tag}" is not a version like v1.2.3 or v1.2.3-rc.1`);
  return `${m[1]}.${m[2]}.${m[3]}.${m[4] ?? 0}`;
}

export function compareVersions(a, b) {
  const pa = a.split(".").map(Number);
  const pb = b.split(".").map(Number);
  for (let i = 0; i < 4; i++) {
    const diff = (pa[i] ?? 0) - (pb[i] ?? 0);
    if (diff !== 0) return diff;
  }
  return 0;
}

export function stampBuildYaml(text, version, changelog) {
  const notes = changelog.replace(/\r/g, "").trim() || "No changelog provided.";
  // "|2-" pins the indentation so notes starting with spaces still parse.
  const block = "changelog: |2-\n" + notes.split("\n").map((l) => (l ? `  ${l}` : "")).join("\n") + "\n";

  if (!/^version:.*$/m.test(text)) throw new Error("build.yaml has no version key");
  const out = text.replace(/^version:.*$/m, `version: "${version}"`);

  // The changelog key owns every following indented or blank line.
  const changelogRe = /^changelog:[^\n]*(?:\n(?:[ \t][^\n]*|))*\n?/m;
  return changelogRe.test(out) ? out.replace(changelogRe, () => block) : out.replace(/\n*$/, "\n") + block;
}

export function stampBuildProps(text, version) {
  let out = text;
  for (const tag of ["Version", "AssemblyVersion", "FileVersion"]) {
    const re = new RegExp(`<${tag}>[^<]*</${tag}>`);
    if (!re.test(out)) throw new Error(`Directory.Build.props has no <${tag}>`);
    out = out.replace(re, `<${tag}>${version}</${tag}>`);
  }
  return out;
}

export function updateManifest(existing, meta, sourceUrl, checksum) {
  const entry = {
    version: meta.version,
    changelog: meta.changelog,
    targetAbi: meta.targetAbi,
    sourceUrl,
    checksum,
    timestamp: meta.timestamp,
  };

  const manifest = Array.isArray(existing) ? existing : [];
  let plugin = manifest.find((p) => p.guid === meta.guid);
  if (!plugin) {
    plugin = { versions: [] };
    manifest.push(plugin);
  }

  Object.assign(plugin, {
    guid: meta.guid,
    name: meta.name,
    description: meta.description,
    overview: meta.overview,
    owner: meta.owner,
    category: meta.category,
  });
  if (meta.imageUrl) plugin.imageUrl = meta.imageUrl;

  plugin.versions = [entry, ...(plugin.versions ?? []).filter((v) => v.version !== entry.version)].sort((a, b) =>
    compareVersions(b.version, a.version),
  );
  return manifest;
}

function main() {
  const [command, ...rest] = process.argv.slice(2);
  const args = parseArgs(rest);

  if (command === "stamp") {
    requireArgs(args, "tag", "changelog");
    const version = tagToVersion(args.tag);
    const changelog = existsSync(args.changelog) ? readFileSync(args.changelog, "utf8") : "";
    writeFileSync("build.yaml", stampBuildYaml(readFileSync("build.yaml", "utf8"), version, changelog));
    writeFileSync("Directory.Build.props", stampBuildProps(readFileSync("Directory.Build.props", "utf8"), version));
    console.log(`Stamped version ${version}`);
  } else if (command === "manifest") {
    requireArgs(args, "meta", "zip", "url", "existing", "out");
    const meta = JSON.parse(readFileSync(args.meta, "utf8"));
    const existing = existsSync(args.existing) ? JSON.parse(readFileSync(args.existing, "utf8")) : [];
    const checksum = createHash("md5").update(readFileSync(args.zip)).digest("hex");
    const manifest = updateManifest(existing, meta, args.url, checksum);
    writeFileSync(args.out, JSON.stringify(manifest, null, 2) + "\n");
    console.log(`Wrote ${args.out} with ${manifest[0].versions.length} version(s) for ${basename(args.zip)}`);
  } else {
    throw new Error(`Unknown command "${command}" (expected "stamp" or "manifest")`);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    main();
  } catch (e) {
    console.error(e.message);
    process.exit(1);
  }
}
