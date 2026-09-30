#!/usr/bin/env node
import { ControlError, createReleaseRecord, importEvidence, initializeVault, verifyVault } from "./evidence-control.js";

function parse(argv) {
  const [command, ...tokens] = argv;
  const values = { command };
  for (let index = 0; index < tokens.length; index += 2) {
    if (!tokens[index]?.startsWith("--") || tokens[index + 1] === undefined) throw new ControlError(`Expected --name value, found: ${tokens[index] ?? "<end>"}`);
    values[tokens[index].slice(2)] = tokens[index + 1];
  }
  return values;
}

function required(values, names) {
  for (const name of names) if (!values[name]) throw new ControlError(`Missing --${name}`);
}

async function main() {
  const values = parse(process.argv.slice(2));
  if (values.command === "init") {
    required(values, ["vault"]);
    console.log(`INITIALIZED vault=${await initializeVault(values.vault)}`);
    return;
  }
  if (values.command === "snapshot") {
    required(values, ["repo", "output", "version"]);
    const executedArtifacts = [];
    if (values.binary) executedArtifacts.push({ role: values["binary-role"] ?? "executed-probe", path: values.binary });
    if (values["source-artifact"]) executedArtifacts.push({ role: values["source-artifact-role"] ?? "probe-source", path: values["source-artifact"] });
    const record = await createReleaseRecord({ repoRoot: values.repo, outputPath: values.output, softwareVersion: values.version, requestedClassification: values.classification ?? "ENGINEERING_SNAPSHOT", executedArtifacts, sbomPath: values.sbom ?? null, sbomOfficiallyValidated: values["sbom-officially-validated"] === "true", sbomOfficialValidator: values["sbom-validator"] ?? "not-run" });
    console.log(`CREATED release_id=${record.release_id} classification=${record.classification} regulatory_use_permitted=${record.regulatory_use_permitted}`);
    return;
  }
  if (values.command === "import") {
    required(values, ["source", "vault", "source-run-id", "test-case-id", "release-record", "disposition", "data-classification", "limitation"]);
    const result = await importEvidence({ sourceDirectory: values.source, vaultRoot: values.vault, sourceRunId: values["source-run-id"], testCaseId: values["test-case-id"], releaseRecordPath: values["release-record"], disposition: values.disposition, dataClassification: values["data-classification"], limitations: [values.limitation] });
    console.log(`${result.imported ? "IMPORTED" : "EXISTS"} evidence_id=${result.receipt.evidence_id} directory=${result.directory}`);
    return;
  }
  if (values.command === "verify") {
    required(values, ["vault"]);
    const result = await verifyVault(values.vault);
    console.log(`VERIFIED vault_id=${result.vault_id} runs=${result.verified_runs}`);
    return;
  }
  throw new ControlError("Commands: init, snapshot, import, verify");
}

main().catch((error) => {
  console.error(`ERROR: ${error.message}`);
  process.exitCode = error instanceof ControlError ? 2 : 1;
});
