#!/usr/bin/env node
import { generateSbom, SbomError, validateSbomFile } from "./sbom.js";

function args(tokens) {
  const [command, ...rest] = tokens;
  const values = { command };
  for (let index = 0; index < rest.length; index += 2) {
    if (!rest[index]?.startsWith("--") || rest[index + 1] === undefined) throw new SbomError(`Expected --name value, found ${rest[index] ?? "<end>"}`);
    values[rest[index].slice(2)] = rest[index + 1];
  }
  return values;
}

function required(values, names) {
  for (const name of names) if (!values[name]) throw new SbomError(`Missing --${name}`);
}

async function main() {
  const values = args(process.argv.slice(2));
  if (values.command === "generate") {
    required(values, ["repo", "output", "version", "timestamp"]);
    const result = await generateSbom({ repoRoot: values.repo, outputPath: values.output, productVersion: values.version, timestamp: values.timestamp });
    console.log(`GENERATED format=CycloneDX-1.7 components=${result.componentCount} sha256=${result.sha256}`);
    return;
  }
  if (values.command === "validate") {
    required(values, ["input"]);
    const result = await validateSbomFile(values.input);
    console.log(`VALID format=CycloneDX-1.7 components=${result.componentCount} dependency_nodes=${result.dependencyNodeCount} sha256=${result.sha256}`);
    return;
  }
  throw new SbomError("Commands: generate, validate");
}

main().catch((error) => {
  console.error(`ERROR: ${error.message}`);
  process.exitCode = error instanceof SbomError ? 2 : 1;
});
