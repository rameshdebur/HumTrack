import { createHash } from "node:crypto";
import { lstat, readFile, readdir, writeFile } from "node:fs/promises";
import path from "node:path";

export class SbomError extends Error {}

const discoveryExclusions = new Set([".git", "node_modules", "bin", "obj", "x64", "coverage"]);

function isDependencyManifest(name) {
  return name === "package-lock.json"
    || name === "packages.lock.json"
    || name.endsWith(".csproj")
    || name.endsWith(".vcxproj")
    || ["build.gradle", "build.gradle.kts", "gradle.lockfile", "libs.versions.toml", "Cargo.lock", "go.sum", "pyproject.toml", "Podfile.lock"].includes(name)
    || /^requirements.*\.txt$/i.test(name);
}

function nugetPurl(name, version) {
  return `pkg:nuget/${encodeURIComponent(name)}@${encodeURIComponent(version)}`;
}

async function nugetSurface(repoRoot, relativeLockPath, scope = "required") {
  const text = await readFile(path.join(repoRoot, ...relativeLockPath.split("/")), "utf8");
  const lock = JSON.parse(text);
  const frameworks = Object.entries(lock.dependencies ?? {});
  if (frameworks.length !== 1) throw new SbomError(`NuGet lock must contain exactly one target framework: ${relativeLockPath}`);
  const [framework, entries] = frameworks[0];
  const packages = [];
  const dependencies = [];
  const direct = [];
  for (const [name, item] of Object.entries(entries)) {
    if (item.type === "Project") continue;
    if (!item.resolved || !item.contentHash) throw new SbomError(`NuGet package lacks resolved version/hash: ${relativeLockPath}:${name}`);
    const ref = nugetPurl(name, item.resolved);
    const packageScope = /Analyzer/i.test(name) ? "excluded" : scope;
    packages.push({
      type: "library",
      name,
      version: item.resolved,
      "bom-ref": ref,
      purl: ref,
      scope: packageScope,
      hashes: integrityHash(`sha512-${item.contentHash}`),
      properties: [
        { name: "humcapture:component-origin", value: "third-party" },
        { name: "humcapture:resolution-source", value: relativeLockPath },
        { name: "humcapture:target-framework", value: framework },
        { name: "humcapture:license-status", value: "NuGet-package-licence-review-required" }
      ]
    });
    dependencies.push({
      ref,
      dependsOn: Object.entries(item.dependencies ?? {}).map(([dependencyName, dependencyVersion]) => {
        const resolved = entries[dependencyName]?.resolved ?? String(dependencyVersion).replace(/^\[|[, ].*$/g, "");
        return nugetPurl(dependencyName, resolved);
      }).sort()
    });
    if (item.type === "Direct") direct.push(ref);
  }
  return { packages, dependencies, direct: direct.sort(), manifestHash: sha256(text) };
}

export async function discoverDependencyManifests(root, current = root) {
  const found = [];
  for (const entry of await readdir(current, { withFileTypes: true })) {
    if (entry.isDirectory() && discoveryExclusions.has(entry.name)) continue;
    const absolute = path.join(current, entry.name);
    const item = await lstat(absolute);
    if (item.isSymbolicLink()) throw new SbomError(`Symbolic link is not allowed during manifest discovery: ${absolute}`);
    if (entry.isDirectory()) found.push(...await discoverDependencyManifests(root, absolute));
    else if (entry.isFile() && isDependencyManifest(entry.name)) found.push(path.relative(root, absolute).split(path.sep).join("/"));
  }
  return found.sort();
}

function sha256(content) {
  return createHash("sha256").update(content).digest("hex");
}

function uuidFromHex(hex) {
  const bytes = Buffer.from(hex.slice(0, 32), "hex");
  bytes[6] = (bytes[6] & 0x0f) | 0x50;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const value = bytes.toString("hex");
  return `${value.slice(0, 8)}-${value.slice(8, 12)}-${value.slice(12, 16)}-${value.slice(16, 20)}-${value.slice(20)}`;
}

function npmPurl(name, version) {
  const encoded = name.startsWith("@") ? `%40${name.slice(1).replace("/", "/")}` : encodeURIComponent(name);
  return `pkg:npm/${encoded}@${encodeURIComponent(version)}`;
}

function integrityHash(integrity) {
  if (!integrity) return [];
  const [algorithm, base64] = integrity.split("-", 2);
  const names = { sha256: "SHA-256", sha384: "SHA-384", sha512: "SHA-512" };
  if (!names[algorithm] || !base64) return [];
  return [{ alg: names[algorithm], content: Buffer.from(base64, "base64").toString("hex") }];
}

function componentHash(content) {
  return [{ alg: "SHA-256", content: sha256(content) }];
}

export function parsePinnedGitHubActions(workflowText, sourceManifest = ".github/workflows/humcapture-ci.yml") {
  const actions = [];
  for (const [index, line] of workflowText.split(/\r?\n/).entries()) {
    const uses = /^\s*-?\s*uses:\s*(\S+)(?:\s+#\s*(\S+))?\s*$/.exec(line);
    if (!uses) continue;
    const reference = /^([A-Za-z0-9_.-]+)\/([A-Za-z0-9_.-]+)@([0-9a-f]{40})$/.exec(uses[1]);
    if (!reference) throw new SbomError(`GitHub Action must be pinned to a 40-character commit SHA at ${sourceManifest}:${index + 1}: ${uses[1]}`);
    const [, owner, repository, commit] = reference;
    const version = uses[2] ?? commit;
    const purl = `pkg:github/${owner}/${repository}@${commit}`;
    actions.push({
      type: "application",
      group: owner,
      name: repository,
      version,
      "bom-ref": purl,
      purl,
      scope: "excluded",
      properties: [
        { name: "humcapture:component-origin", value: "third-party-build-action" },
        { name: "humcapture:source-manifest", value: sourceManifest },
        { name: "humcapture:commit-pin", value: commit },
        { name: "humcapture:hash-status", value: "git-commit-pin-recorded; action-package-not-hashed" },
        { name: "humcapture:license-status", value: "build-action-licence-review-required" },
        { name: "humcapture:not-distributed", value: "true" }
      ]
    });
  }
  if (actions.length === 0) throw new SbomError(`No GitHub Actions were found in ${sourceManifest}.`);
  const refs = new Set(actions.map((action) => action["bom-ref"]));
  if (refs.size !== actions.length) throw new SbomError(`Duplicate GitHub Action commit references found in ${sourceManifest}.`);
  return actions;
}

export function parsePinnedChocolateyPackages(workflowText, sourceManifest = ".github/workflows/humcapture-ci.yml") {
  const packages = [];
  for (const [index, line] of workflowText.split(/\r?\n/).entries()) {
    const install = /(?:^|\s)choco(?:\.exe)?\s+install\s+(\S+)(.*)$/i.exec(line);
    if (!install) continue;
    const version = /(?:^|\s)--version(?:=|\s+)([^\s]+)/i.exec(install[2])?.[1];
    if (!version) throw new SbomError(`Chocolatey package must use an exact --version at ${sourceManifest}:${index + 1}: ${install[1]}`);
    const name = install[1].toLowerCase();
    const purl = `pkg:chocolatey/${name}@${encodeURIComponent(version)}`;
    packages.push({
      type: "framework",
      name,
      version,
      "bom-ref": purl,
      purl,
      scope: "excluded",
      properties: [
        { name: "humcapture:component-origin", value: "third-party-build-package" },
        { name: "humcapture:source-manifest", value: sourceManifest },
        { name: "humcapture:hash-status", value: "package-manager-verification; package-artifact-hash-not-collected" },
        { name: "humcapture:license-status", value: "build-package-licence-review-required" },
        { name: "humcapture:not-distributed", value: "true" }
      ]
    });
  }
  return packages;
}

function licenseChoice(license) {
  return license ? [{ license: { id: license } }] : [];
}

function dependencyMapEntries(lock, packageByName) {
  const dependencies = [];
  for (const [packagePath, item] of Object.entries(lock.packages ?? {})) {
    if (packagePath === "") continue;
    const name = packagePath.replace(/^.*node_modules\//, "");
    const current = packageByName.get(`${name}@${item.version}`);
    if (!current) continue;
    const dependsOn = Object.keys(item.dependencies ?? {}).map((dependencyName) => {
      const candidate = [...packageByName.entries()].find(([key]) => key.startsWith(`${dependencyName}@`));
      return candidate?.[1]["bom-ref"];
    }).filter(Boolean).sort();
    dependencies.push({ ref: current["bom-ref"], dependsOn });
  }
  return dependencies;
}

async function npmSurface(repoRoot, relativeLockPath) {
  const absolute = path.join(repoRoot, ...relativeLockPath.split("/"));
  const text = await readFile(absolute, "utf8");
  const lock = JSON.parse(text);
  const root = lock.packages?.[""];
  if (!root?.name || !root?.version) throw new SbomError(`Lockfile lacks root package identity: ${relativeLockPath}`);
  const rootRef = npmPurl(root.name, root.version);
  const firstParty = {
    type: "application",
    group: "HumCapture",
    name: root.name,
    version: root.version,
    "bom-ref": rootRef,
    purl: rootRef,
    hashes: componentHash(text),
    properties: [
      { name: "humcapture:source-manifest", value: relativeLockPath },
      { name: "humcapture:component-origin", value: "first-party" },
      { name: "humcapture:license-status", value: "first-party-licence-not-yet-baselined" },
      { name: "humcapture:distribution-status", value: "engineering-tool-not-released" }
    ]
  };
  const packages = new Map();
  for (const [packagePath, item] of Object.entries(lock.packages ?? {})) {
    if (packagePath === "") continue;
    const name = packagePath.replace(/^.*node_modules\//, "");
    const purl = npmPurl(name, item.version);
    packages.set(`${name}@${item.version}`, {
      type: "library",
      name,
      version: item.version,
      "bom-ref": purl,
      purl,
      scope: "required",
      ...(integrityHash(item.integrity).length ? { hashes: integrityHash(item.integrity) } : {}),
      ...(licenseChoice(item.license).length ? { licenses: licenseChoice(item.license) } : {}),
      properties: [
        { name: "humcapture:component-origin", value: "third-party" },
        { name: "humcapture:resolution-source", value: relativeLockPath }
      ]
    });
  }
  const direct = Object.keys(root.dependencies ?? {}).map((name) => {
    const candidate = [...packages.entries()].find(([key]) => key.startsWith(`${name}@`));
    return candidate?.[1]["bom-ref"];
  }).filter(Boolean).sort();
  return { firstParty, packages: [...packages.values()], dependencies: [{ ref: rootRef, dependsOn: direct }, ...dependencyMapEntries(lock, packages)], manifestHash: sha256(text) };
}

async function projectComponent(repoRoot, relativePath, name, type, version, properties = []) {
  const text = await readFile(path.join(repoRoot, ...relativePath.split("/")), "utf8");
  const ref = `pkg:generic/humcapture/${encodeURIComponent(name)}@${encodeURIComponent(version)}`;
  return {
    component: {
      type,
      group: "HumCapture",
      name,
      version,
      "bom-ref": ref,
      purl: ref,
      hashes: componentHash(text),
      properties: [
        { name: "humcapture:source-manifest", value: relativePath },
        { name: "humcapture:component-origin", value: "first-party" },
        { name: "humcapture:license-status", value: "first-party-licence-not-yet-baselined" },
        ...properties
      ]
    },
    manifestHash: sha256(text)
  };
}

function mergeDependencies(dependencies) {
  const merged = new Map();
  for (const item of dependencies) {
    if (!merged.has(item.ref)) merged.set(item.ref, new Set());
    for (const dependency of item.dependsOn ?? []) merged.get(item.ref).add(dependency);
  }
  return [...merged.entries()].map(([ref, values]) => ({ ref, dependsOn: [...values].sort() })).sort((a, b) => a.ref.localeCompare(b.ref));
}

export async function generateSbom({ repoRoot, outputPath, productVersion, timestamp }) {
  const absoluteRoot = path.resolve(repoRoot);
  const generatedAt = new Date(timestamp);
  if (!Number.isFinite(generatedAt.getTime())) throw new SbomError(`Invalid timestamp: ${timestamp}`);
  const npmLocks = [
    "tools/capability-probes/shared/package-lock.json",
    "tools/evidence-control/package-lock.json"
  ];
  const coordinatorProjectPath = "apps/windows-coordinator/src/HumCapture.Coordinator.Repository/HumCapture.Coordinator.Repository.csproj";
  const coordinatorLockPath = "apps/windows-coordinator/src/HumCapture.Coordinator.Repository/packages.lock.json";
  const coordinatorSelfTestProjectPath = "apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest/HumCapture.Coordinator.Repository.SelfTest.csproj";
  const coordinatorSelfTestLockPath = "apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest/packages.lock.json";
  const handledDependencyManifests = [
    ...npmLocks,
    coordinatorProjectPath,
    coordinatorLockPath,
    coordinatorSelfTestProjectPath,
    coordinatorSelfTestLockPath,
    "tools/capability-probes/windows/managed/HumCapture.ManagedCameraProbe.csproj",
    "tools/capability-probes/windows/native-mf/HumCapture.MfCapture.vcxproj",
    "tools/capability-probes/windows/native-mf/HumCapture.MfEnumerator.vcxproj"
  ];
  const discoveredDependencyManifests = await discoverDependencyManifests(absoluteRoot);
  const unhandled = discoveredDependencyManifests.filter((item) => !handledDependencyManifests.includes(item));
  const missing = handledDependencyManifests.filter((item) => !discoveredDependencyManifests.includes(item));
  if (unhandled.length || missing.length) throw new SbomError(`SBOM manifest coverage changed. Unhandled: ${unhandled.join(", ") || "none"}. Missing: ${missing.join(", ") || "none"}.`);
  const npmSurfaces = [];
  for (const lock of npmLocks) npmSurfaces.push(await npmSurface(absoluteRoot, lock));
  const coordinatorNuget = await nugetSurface(absoluteRoot, coordinatorLockPath);
  const coordinatorSelfTestNuget = await nugetSurface(absoluteRoot, coordinatorSelfTestLockPath, "excluded");

  const managedProjectPath = "tools/capability-probes/windows/managed/HumCapture.ManagedCameraProbe.csproj";
  const nativeCaptureProjectPath = "tools/capability-probes/windows/native-mf/HumCapture.MfCapture.vcxproj";
  const nativeEnumeratorProjectPath = "tools/capability-probes/windows/native-mf/HumCapture.MfEnumerator.vcxproj";
  const managedProjectText = await readFile(path.join(absoluteRoot, ...managedProjectPath.split("/")), "utf8");
  const nativeCaptureProjectText = await readFile(path.join(absoluteRoot, ...nativeCaptureProjectPath.split("/")), "utf8");
  const nativeEnumeratorProjectText = await readFile(path.join(absoluteRoot, ...nativeEnumeratorProjectPath.split("/")), "utf8");
  const targetFramework = /<TargetFramework>([^<]+)<\/TargetFramework>/.exec(managedProjectText)?.[1];
  const windowsTarget = /<WindowsTargetPlatformVersion>([^<]+)<\/WindowsTargetPlatformVersion>/.exec(nativeCaptureProjectText)?.[1];
  const platformToolset = /<PlatformToolset>([^<]+)<\/PlatformToolset>/.exec(nativeCaptureProjectText)?.[1];
  const linkedLibraries = [...new Set([...nativeCaptureProjectText.matchAll(/<AdditionalDependencies>([^<]+)<\/AdditionalDependencies>/g), ...nativeEnumeratorProjectText.matchAll(/<AdditionalDependencies>([^<]+)<\/AdditionalDependencies>/g)].flatMap((match) => match[1].split(";")).filter((name) => name && !name.startsWith("%(")))].sort();
  if (!targetFramework || !windowsTarget || !platformToolset || linkedLibraries.length === 0) throw new SbomError("Cannot derive managed/native target and linker dependency metadata from project files.");
  const managed = await projectComponent(absoluteRoot, managedProjectPath, "HumCapture.ManagedCameraProbe", "application", "0.1.0-p0", [
    { name: "humcapture:target-framework", value: targetFramework },
    { name: "humcapture:version-provenance", value: "project-version-not-declared; engineering-version-assigned" }
  ]);
  const nativeCapture = await projectComponent(absoluteRoot, nativeCaptureProjectPath, "HumCapture.MfCapture", "application", "0.1.0-p0", [
    { name: "humcapture:platform-toolset", value: platformToolset },
    { name: "humcapture:windows-target-platform", value: windowsTarget }
  ]);
  const nativeEnumerator = await projectComponent(absoluteRoot, nativeEnumeratorProjectPath, "HumCapture.MfEnumerator", "application", "0.1.0-p0", [
    { name: "humcapture:platform-toolset", value: /<PlatformToolset>([^<]+)<\/PlatformToolset>/.exec(nativeEnumeratorProjectText)?.[1] ?? platformToolset },
    { name: "humcapture:windows-target-platform", value: /<WindowsTargetPlatformVersion>([^<]+)<\/WindowsTargetPlatformVersion>/.exec(nativeEnumeratorProjectText)?.[1] ?? windowsTarget }
  ]);
  const coordinator = await projectComponent(absoluteRoot, coordinatorProjectPath, "HumCapture.Coordinator.Repository", "library", "0.1.0", [
    { name: "humcapture:target-framework", value: "net10.0-windows10.0.19041.0" },
    { name: "humcapture:distribution-status", value: "engineering-application-component-not-released" }
  ]);
  const coordinatorSelfTest = await projectComponent(absoluteRoot, coordinatorSelfTestProjectPath, "HumCapture.Coordinator.Repository.SelfTest", "application", "0.1.0", [
    { name: "humcapture:target-framework", value: "net10.0-windows10.0.19041.0" },
    { name: "humcapture:execution-context", value: "test" },
    { name: "humcapture:not-distributed", value: "true" }
  ]);
  const sbomTool = await projectComponent(absoluteRoot, "tools/sbom/package.json", "HumCapture SBOM Tool", "application", "0.1.0", [
    { name: "humcapture:execution-context", value: "build" }
  ]);
  const workflowManifestPath = ".github/workflows/humcapture-ci.yml";
  const workflowAbsolutePath = path.resolve(absoluteRoot, "..", "..", ...workflowManifestPath.split("/"));
  const workflowText = await readFile(workflowAbsolutePath, "utf8");
  const githubActions = parsePinnedGitHubActions(workflowText, workflowManifestPath);
  const chocolateyPackages = parsePinnedChocolateyPackages(workflowText, workflowManifestPath);
  const workflowRef = `pkg:generic/humcapture/HumCapture-CI@${encodeURIComponent(productVersion)}`;
  const workflowComponent = {
    type: "application",
    group: "HumCapture",
    name: "HumCapture CI",
    version: productVersion,
    "bom-ref": workflowRef,
    purl: workflowRef,
    scope: "excluded",
    hashes: componentHash(workflowText),
    properties: [
      { name: "humcapture:source-manifest", value: workflowManifestPath },
      { name: "humcapture:component-origin", value: "first-party-build-workflow" },
      { name: "humcapture:license-status", value: "first-party-licence-not-yet-baselined" },
      { name: "humcapture:not-distributed", value: "true" }
    ]
  };

  const netRuntimeRef = "pkg:generic/microsoft/Microsoft.NETCore.App@8.0.0";
  const net10RuntimeRef = "pkg:generic/microsoft/Microsoft.NETCore.App@10.0.0";
  const windowsRuntimeRef = "pkg:generic/microsoft/Windows-Media-Foundation-COM-Shell@host-provided";
  const windowsSdkRef = "pkg:generic/microsoft/Windows-SDK@10.0.19041.0";
  const platformComponents = [
    {
      type: "framework", name: "Microsoft.NETCore.App", version: "8.0.0", "bom-ref": netRuntimeRef, purl: netRuntimeRef, scope: "required",
      properties: [
        { name: "humcapture:component-origin", value: "third-party-platform" },
        { name: "humcapture:license-status", value: "host-framework-licence-review-required" },
        { name: "humcapture:hash-status", value: "host-resolved-framework-not-hashed" },
        { name: "humcapture:resolution", value: "minimum framework from runtimeconfig; installed patch resolves at runtime" }
      ]
    },
    {
      type: "framework", name: "Microsoft.NETCore.App", version: "10.0.0", "bom-ref": net10RuntimeRef, purl: net10RuntimeRef, scope: "required",
      properties: [
        { name: "humcapture:component-origin", value: "third-party-platform" },
        { name: "humcapture:license-status", value: "host-framework-licence-review-required" },
        { name: "humcapture:hash-status", value: "host-resolved-framework-not-hashed" },
        { name: "humcapture:resolution", value: "minimum framework from runtimeconfig; installed patch resolves at runtime" }
      ]
    },
    {
      type: "framework", name: "Microsoft Windows Media Foundation, COM/OLE and Shell runtime APIs", version: "host-provided", "bom-ref": windowsRuntimeRef, purl: windowsRuntimeRef, scope: "required",
      properties: [
        { name: "humcapture:component-origin", value: "operating-system" },
        { name: "humcapture:license-status", value: "operating-system-licence-review-required" },
        { name: "humcapture:hash-status", value: "host-provided-runtime-not-hashed" },
        { name: "humcapture:known-unknown", value: "runtime patch version follows the host Windows servicing level" }
      ]
    },
    {
      type: "framework", name: "Microsoft Windows SDK", version: "10.0.19041.0", "bom-ref": windowsSdkRef, purl: windowsSdkRef, scope: "excluded",
      properties: [
        { name: "humcapture:component-origin", value: "build-toolchain" },
        { name: "humcapture:license-status", value: "build-toolchain-licence-review-required" },
        { name: "humcapture:hash-status", value: "sdk-component-hash-not-collected" },
        { name: "humcapture:linked-import-libraries", value: linkedLibraries.join(";") },
        { name: "humcapture:not-distributed", value: "true" }
      ]
    }
  ];

  const firstParty = [...npmSurfaces.map((surface) => surface.firstParty), managed.component, nativeCapture.component, nativeEnumerator.component, coordinator.component, coordinatorSelfTest.component, sbomTool.component, workflowComponent];
  const thirdPartyByRef = new Map();
  for (const component of [...npmSurfaces.flatMap((surface) => surface.packages), ...coordinatorSelfTestNuget.packages, ...coordinatorNuget.packages, ...platformComponents, ...githubActions, ...chocolateyPackages]) thirdPartyByRef.set(component["bom-ref"], component);
  const rootRef = `pkg:generic/humcapture/HumCapture@${encodeURIComponent(productVersion)}`;
  const rootComponent = {
    type: "application",
    group: "HumTrack",
    name: "HumCapture",
    version: productVersion,
    "bom-ref": rootRef,
    purl: rootRef,
    supplier: { name: "HumCapture project; legal producer identity pending governance baseline" },
    properties: [
      { name: "humcapture:release-status", value: "engineering-unreleased" },
      { name: "humcapture:license-status", value: "first-party-licence-not-yet-baselined" },
      { name: "humcapture:hash-status", value: "primary-source-package-not-released" },
      { name: "humcapture:scope", value: "src/HumCapture only" }
    ]
  };
  const dependencies = [
    { ref: rootRef, dependsOn: firstParty.map((item) => item["bom-ref"]).sort() },
    ...npmSurfaces.flatMap((surface) => surface.dependencies),
    ...coordinatorNuget.dependencies,
    ...coordinatorSelfTestNuget.dependencies,
    { ref: managed.component["bom-ref"], dependsOn: [netRuntimeRef] },
    { ref: nativeCapture.component["bom-ref"], dependsOn: [windowsRuntimeRef, windowsSdkRef] },
    { ref: nativeEnumerator.component["bom-ref"], dependsOn: [windowsRuntimeRef, windowsSdkRef] },
    { ref: coordinator.component["bom-ref"], dependsOn: [...coordinatorNuget.direct, net10RuntimeRef] },
    { ref: coordinatorSelfTest.component["bom-ref"], dependsOn: [coordinator.component["bom-ref"], ...coordinatorSelfTestNuget.direct, net10RuntimeRef] },
    { ref: sbomTool.component["bom-ref"], dependsOn: [] },
    { ref: workflowRef, dependsOn: [...githubActions, ...chocolateyPackages].map((component) => component["bom-ref"]).sort() }
  ];
  for (const component of thirdPartyByRef.values()) if (!dependencies.some((item) => item.ref === component["bom-ref"])) dependencies.push({ ref: component["bom-ref"], dependsOn: [] });
  const manifestDigest = sha256([ ...npmSurfaces.map((item) => item.manifestHash), coordinatorNuget.manifestHash, coordinatorSelfTestNuget.manifestHash, managed.manifestHash, nativeCapture.manifestHash, nativeEnumerator.manifestHash, coordinator.manifestHash, coordinatorSelfTest.manifestHash, sbomTool.manifestHash, sha256(workflowText), productVersion, generatedAt.toISOString() ].join("\n"));
  const bom = {
    "$schema": "http://cyclonedx.org/schema/bom-1.7.schema.json",
    bomFormat: "CycloneDX",
    specVersion: "1.7",
    serialNumber: `urn:uuid:${uuidFromHex(manifestDigest)}`,
    version: 1,
    metadata: {
      timestamp: generatedAt.toISOString(),
      lifecycles: [{ phase: "build" }],
      authors: [{ name: "HumCapture project" }],
      tools: {
        components: [{ type: "application", name: "HumCapture SBOM Tool", version: "0.1.0", "bom-ref": sbomTool.component["bom-ref"] }]
      },
      component: rootComponent,
      properties: [
        { name: "humcapture:generation-context", value: "source-manifest and build-context inventory" },
        { name: "humcapture:coverage", value: "all package locks and project files present under src/HumCapture plus the scoped repository CI workflow at generation" },
        { name: "humcapture:known-unknowns", value: "host runtime patches; dynamic modules; future Android dependencies; legal producer identity; binary composition" }
      ]
    },
    components: [...firstParty, ...thirdPartyByRef.values()].sort((a, b) => a["bom-ref"].localeCompare(b["bom-ref"])),
    dependencies: mergeDependencies(dependencies),
    properties: [
      { name: "humcapture:sbom-policy", value: "HC-GOV-SBOM-001 revision 1.0" },
      { name: "humcapture:declared-manifests", value: [...handledDependencyManifests, "tools/sbom/package.json", workflowManifestPath].join(";") }
    ]
  };
  await validateSbom(bom);
  const text = `${JSON.stringify(bom, null, 2)}\n`;
  await writeFile(path.resolve(outputPath), text, "utf8");
  await writeFile(`${path.resolve(outputPath)}.sha256`, `${sha256(text)}  ${path.basename(outputPath)}\n`, "utf8");
  return { bom, sha256: sha256(text), componentCount: bom.components.length };
}

export async function validateSbomFile(filePath) {
  const text = await readFile(path.resolve(filePath), "utf8");
  const bom = JSON.parse(text);
  const result = await validateSbom(bom);
  return { ...result, sha256: sha256(text) };
}

export async function validateSbom(bom) {
  const errors = [];
  if (bom.bomFormat !== "CycloneDX" || bom.specVersion !== "1.7") errors.push("SBOM must be CycloneDX 1.7.");
  if (!/^urn:uuid:[0-9a-f-]{36}$/i.test(bom.serialNumber ?? "")) errors.push("SBOM serialNumber must be a UUID URN.");
  if (!bom.metadata?.timestamp || !bom.metadata?.component?.["bom-ref"]) errors.push("SBOM metadata timestamp and primary component are required.");
  if (!bom.metadata?.authors?.[0]?.name || !bom.metadata?.component?.supplier?.name) errors.push("SBOM author and software producer/supplier are required.");
  const components = bom.components ?? [];
  const refs = new Set([bom.metadata?.component?.["bom-ref"], ...components.map((item) => item["bom-ref"])]);
  if (refs.has(undefined) || refs.size !== components.length + 1) errors.push("Every component must have a unique bom-ref.");
  for (const component of components) {
    if (!component.type || !component.name || !component.version) errors.push(`Component lacks type/name/version: ${component["bom-ref"] ?? "unknown"}`);
    if (!component.hashes?.length && !component.properties?.some((item) => item.name === "humcapture:hash-status")) errors.push(`Component lacks hash or explicit hash status: ${component["bom-ref"]}`);
    if (!component.licenses?.length && !component.properties?.some((item) => item.name === "humcapture:license-status")) errors.push(`Component lacks licence or explicit licence status: ${component["bom-ref"]}`);
  }
  const dependencyRefs = new Set((bom.dependencies ?? []).map((item) => item.ref));
  for (const ref of refs) if (!dependencyRefs.has(ref)) errors.push(`Missing dependency graph node: ${ref}`);
  for (const item of bom.dependencies ?? []) {
    if (!refs.has(item.ref)) errors.push(`Dependency graph contains unknown ref: ${item.ref}`);
    for (const dependency of item.dependsOn ?? []) if (!refs.has(dependency)) errors.push(`Dependency graph points to unknown ref: ${dependency}`);
  }
  const declared = bom.properties?.find((item) => item.name === "humcapture:declared-manifests")?.value ?? "";
  for (const required of ["tools/capability-probes/shared/package-lock.json", "tools/evidence-control/package-lock.json", "HumCapture.ManagedCameraProbe.csproj", "HumCapture.MfCapture.vcxproj", "HumCapture.MfEnumerator.vcxproj", "HumCapture.Coordinator.Repository.csproj", "HumCapture.Coordinator.Repository.SelfTest.csproj", "packages.lock.json", "tools/sbom/package.json", ".github/workflows/humcapture-ci.yml"]) if (!declared.includes(required)) errors.push(`Declared manifest coverage missing: ${required}`);
  if (errors.length) throw new SbomError(`SBOM validation failed:\n${errors.join("\n")}`);
  return { componentCount: components.length, dependencyNodeCount: bom.dependencies.length };
}
