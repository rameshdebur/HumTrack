#!/usr/bin/env node
import { EvidenceError, generateHashes, validateCampaignIndex, validateEvidencePackage } from "./evidence.js";

function usage() {
  return [
    "Usage:",
    "  node src/cli.js validate <evidence-directory>",
    "  node src/cli.js hash <evidence-directory>",
    "  node src/cli.js validate-campaign <campaign-index.json>"
  ].join("\n");
}

async function main() {
  const [command, target, ...extra] = process.argv.slice(2);
  if (!command || !target || extra.length > 0) throw new EvidenceError(usage());
  if (command === "validate") {
    const result = await validateEvidencePackage(target);
    console.log(`VALID run_id=${result.runId} procedure_id=${result.procedureId} hardware_qualification=${result.hardwareQualification} artifacts=${result.artifactCount} disposition=${result.disposition}`);
    return;
  }
  if (command === "hash") {
    const artifacts = await generateHashes(target);
    console.log(`HASHED artifacts=${artifacts.length}`);
    return;
  }
  if (command === "validate-campaign") {
    const result = await validateCampaignIndex(target);
    console.log(`VALID campaign_id=${result.campaignId} runs=${result.runCount}`);
    return;
  }
  throw new EvidenceError(`Unknown command: ${command}`, [usage()]);
}

main().catch((error) => {
  if (error instanceof EvidenceError) {
    console.error(`ERROR: ${error.message}`);
    error.details.forEach((detail) => console.error(`  - ${detail}`));
    process.exitCode = 2;
    return;
  }
  console.error(error);
  process.exitCode = 1;
});
