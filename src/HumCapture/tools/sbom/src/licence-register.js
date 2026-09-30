import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

export const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../..");
export const registerPath = path.join(repoRoot, "sbom/dependency-licences.json");
export const documentPath = path.join(repoRoot, "docs/governance/DEPENDENCY_LICENSE_REGISTER.md");

const text = value => typeof value === "string" && value.trim().length > 0;
const cell = value => String(value).replaceAll("|", "\\|").replace(/\r?\n/g, " ");
const anchor = value => value.toLowerCase().replace(/[^a-z0-9-]/g, "");

export function validateRegister(bom, register) {
  if (register.schema_version !== "1.0.0" || !/^\d{4}-\d{2}-\d{2}$/.test(register.reviewed_on)
    || !text(register.owner) || !register.profiles || !Array.isArray(register.entries)
    || !Array.isArray(register.candidates) || !Array.isArray(register.supplemental)) throw new Error("Invalid licence register structure.");
  const components = [bom.metadata.component, ...bom.components];
  const refs = new Set(components.map(c => c["bom-ref"]));
  if (refs.size !== components.length) throw new Error("Duplicate SBOM identity.");
  const seen = new Set();
  for (const [id, profile] of Object.entries(register.profiles)) {
    if (![profile.licence, profile.cost, profile.obligations].every(text) || !Array.isArray(profile.sources)
      || !profile.sources.length || !profile.sources.every(text)) throw new Error(`Incomplete licence profile: ${id}`);
  }
  for (const item of [...register.entries, ...register.candidates, ...register.supplemental]) {
    if (!Object.hasOwn(register.profiles, item.profile) || ![item.use, item.review_status, item.next_action].every(text)) {
      throw new Error("Incomplete licence review entry.");
    }
  }
  for (const item of register.entries) {
    if (!refs.has(item.ref)) throw new Error(`Stale/unknown licence identity: ${item.ref}`);
    if (seen.has(item.ref)) throw new Error(`Duplicate licence identity: ${item.ref}`);
    if (!Array.isArray(item.evidence) || !item.evidence.length || !item.evidence.every(text)) throw new Error(`Missing licence evidence: ${item.ref}`);
    seen.add(item.ref);
  }
  for (const ref of refs) { if (!seen.has(ref)) throw new Error(`Missing/version-changed licence entry: ${ref}`); }
  for (const item of [...register.candidates, ...register.supplemental]) {
    if (![item.name, item.version].every(text)) throw new Error("Unidentified candidate/tooling item.");
  }
  for (const candidate of register.candidates) {
    if (components.some(c => c.name === candidate.name)) throw new Error(`Candidate now inventoried: reconcile status for ${candidate.name}`);
  }
  return components;
}

export function renderRegister(bom, register) {
  const components = validateRegister(bom, register);
  const lines = [
    "# HumCapture Dependency, Licence and Cost Register", "",
    "HC-GOV-LIC-001 | Living engineering register | NOT legal/distribution approval", "",
    `Publisher/metadata review date: ${register.reviewed_on}. Owner: ${register.owner}.`, "",
    `Coverage: ${bom.components.length} SBOM components plus HumCapture product; ${register.candidates.length} selected-not-installed candidate(s); ${register.supplemental.length} supplemental tooling/composition items.`,
    `SBOM baseline: ${bom.metadata.component.version}, generated ${bom.metadata.timestamp}.`, "",
    "Scope is src/HumCapture, not the wider HumTrack repository or all software installed on this machine. Direct and transitive package identities follow the SBOM dependency graph. This is source/lockfile coverage, NOT a complete binary-composition audit.", "",
    "## Read this first", "",
    ...register.candidates.map(c => `- ${c.name}: ${c.use}. ${c.next_action}`),
    "- Gyan FFmpeg 9.0.1 is engineering-only, runtime disabled, redistribution unapproved. GPL/build notices and codec patent questions remain open.",
    "- The pinned Sonar analyzer declares LGPL-3.0-only. It is build tooling, not an intended application runtime component.",
    "- No mandatory licence fee identified is NOT no legal obligations, no total cost, or clearance to distribute. Fees exclude tax/FX and optional support unless stated.",
    "- No total project cost is calculated: organization size/revenue, Windows/tool entitlements, hosting allowances and patent applicability are unresolved. No CDSCO, certification or legal approval follows.", "",
    "## Licence and charge profiles", "",
    "Entries below link to these shared profiles. Summaries describe publisher terms and review tasks, not counsel's conclusions. All engineering declarations still need appropriate release review.", ""
  ];
  for (const [id, profile] of Object.entries(register.profiles)) {
    lines.push(`### ${id}`, "", `Licence: ${profile.licence}.`, "", `Potential charges: ${profile.cost}`, "",
      `Obligations / implications: ${profile.obligations}`, "",
      `Sources: ${profile.sources.map((url, i) => `[${i + 1}](${url})`).join(", ")}.`, "");
  }
  lines.push("## Current SBOM inventory", "",
    "Review states are recorded per row; a declaration is not legal approval. Versions and commit identities are exact SBOM identities, except explicitly host-resolved components. Node package scope means required by development tooling, not necessarily shipped in the Coordinator.", "",
    "| Component / version | Use | Licence / cost profile | Evidence | Outstanding action |",
    "|---|---|---|---|---|");
  for (const component of components) {
    const item = register.entries.find(e => e.ref === component["bom-ref"]);
    lines.push(`| ${cell(component.group ? component.group + "/" : "")}${cell(component.name)} **${cell(component.version)}** | ${cell(item.use)} | [${item.profile}](#${anchor(item.profile)}) | ${item.evidence.map((url, i) => `[${i + 1}](${url})`).join(" ")} | ${cell(item.review_status)}: ${cell(item.next_action)} |`);
  }
  for (const [title, items] of [["Selected but not installed", register.candidates], ["Supplemental tools and unresolved composition", register.supplemental]]) {
    lines.push("", `## ${title}`, "", "| Item / version | Use | Licence / costs | Status and next action |", "|---|---|---|---|");
    for (const item of items) {
      lines.push(`| ${cell(item.name)} — ${cell(item.version)} | ${cell(item.use)} | [${item.profile}](#${anchor(item.profile)}) | ${cell(item.review_status)}: ${cell(item.next_action)} |`);
    }
  }
  lines.push("", "## Maintenance and approval", "",
    "1. For every added, removed, upgraded, re-scoped or redistributed dependency: regenerate the SBOM, review exact package/version terms and transitive dependencies, then update sbom/dependency-licences.json in the same change.",
    "2. Refresh prices/applicability sources when selecting/upgrading a package and before release. Preserve review dates and decisions in version control. This document does not poll prices or accept terms automatically.",
    "3. Regenerate this document with `node tools/sbom/src/licence-register.js --write`. Do not hand-edit generated tables. Run `npm.cmd test --prefix tools/sbom`.",
    "4. Existing CI runs these tests: missing/version-changed entries, changed manifests against retained SBOM, and stale rendered documentation fail. First-party version changes also require reconciliation. Machine checks establish inventory consistency, not legal correctness or completeness of embedded binaries.",
    "5. Package owners propose updates; Release/SBOM owner maintains coverage; commercial owner confirms fees/entitlements; qualified counsel reviews unresolved legal/distribution questions. Record approval separately with reviewer, date, scope and evidence. AI does not approve its own licence analysis.",
    "6. Before shipping: complete third-party notice/licence bundle, exact binary composition, applicable corresponding-source obligations, paid entitlement evidence where needed, and a release-specific review. Internal tool use and application redistribution remain separate.", "",
    "Not yet complete: embedded FFmpeg libraries, native SQLite revision, runner/SDK bundled tools, historical PATH binaries, exact local tool versions and producer licensing. Android/iOS/NDI SDK packages are not represented as installed; new manifests must enter the SBOM and this register when introduced.", "",
    "Authoritative inputs: [SBOM](../../sbom/humcapture.cdx.json), [review data](../../sbom/dependency-licences.json), [SBOM policy](SBOM_POLICY.md).", ""
  );
  return lines.join("\n");
}

export async function readInputs() {
  return { bom: JSON.parse(await readFile(path.join(repoRoot, "sbom/humcapture.cdx.json"), "utf8")),
    register: JSON.parse(await readFile(registerPath, "utf8")) };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const mode = process.argv[2];
    if (process.argv.length !== 3 || !["--write", "--check"].includes(mode)) throw new Error("Use --write or --check.");
    const { bom, register } = await readInputs();
    const rendered = renderRegister(bom, register);
    if (mode === "--write") await writeFile(documentPath, rendered, "utf8");
    else if ((await readFile(documentPath, "utf8")).replaceAll("\r\n", "\n") !== rendered) throw new Error("Licence document stale; run --write after reviewing data.");
    console.log(`LICENCE_REGISTER_OK components=${bom.components.length + 1} candidates=${register.candidates.length}`);
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}
