// Explicit opt-in policy. The legacy timing helper is unchanged.
export function evaluateScientificConditionsV1_1(conditions) {
  for (const condition of conditions) {
    if (!["REQUIRED", "PREFERRED", "INFORMATIONAL"].includes(condition.level)
      || !["PASS", "FAIL", "NOT_ASSESSED"].includes(condition.outcome)) throw new Error("Unsupported scientific condition.");
  }
  if (conditions.some(c => c.level === "REQUIRED" && c.outcome === "FAIL")) return "REJECTED";
  if (conditions.some(c => c.level === "REQUIRED" && c.outcome !== "PASS")
    || !conditions.some(c => c.level === "REQUIRED" || c.level === "PREFERRED")) return "REVIEW_REQUIRED";
  if (conditions.some(c => c.level === "PREFERRED" && c.outcome !== "PASS")) return "DEGRADED_ACCEPTABLE";
  return "CONFORMANT";
}
