import { readFileSync, writeFileSync } from "node:fs";
import { parseArgs } from "node:util";
import { pathToFileURL } from "node:url";

// Analyze exported runs locally; this tool never calls a provider or reads credentials.
const round = (value) => Math.round(value * 10000) / 10000;
const mean = (values) => values.length ? values.reduce((sum, value) => sum + value, 0) / values.length : null;

function percentile(values, fraction) {
  if (!values.length) return null;
  const sorted = [...values].sort((left, right) => left - right);
  const position = (sorted.length - 1) * fraction;
  const lower = Math.floor(position);
  return round(sorted[lower] + (sorted[Math.ceil(position)] - sorted[lower]) * (position - lower));
}

function kappa(pairs) {
  if (!pairs.length) return null;
  const references = new Map();
  const scores = new Map();
  let observed = 0;
  for (const [reference, score] of pairs) {
    references.set(reference, (references.get(reference) ?? 0) + 1);
    scores.set(score, (scores.get(score) ?? 0) + 1);
    observed += (reference - score) ** 2;
  }
  let expected = 0;
  for (const [reference, referenceCount] of references)
    for (const [score, scoreCount] of scores)
      expected += referenceCount * scoreCount * (reference - score) ** 2 / pairs.length;
  // Constant, identical distributions make kappa undefined, rather than perfect by assumption.
  return expected ? round(1 - observed / expected) : null;
}

export function analyze(rows) {
  if (!Array.isArray(rows)) throw new Error("Input must be a JSON array.");
  const groups = new Map();
  const seen = new Set();
  const references = new Map();
  rows.forEach((row, index) => {
    const reject = (message) => { throw new Error(`Row ${index + 1}: ${message}`); };
    if (!row || typeof row !== "object" || Array.isArray(row)) reject("expected an object");
    for (const key of ["sampleId", "model", "rubricVersion"])
      if (typeof row[key] !== "string" || !row[key].trim()) reject(`${key} must be a nonempty string`);
    if (!["speaking", "writing"].includes(row.skill)) reject("invalid skill");
    if (!(row.part === "FULL" || Number.isInteger(row.part) && row.part >= 1 && row.part <= (row.skill === "writing" ? 3 : 5))) reject("invalid part");
    if (!Number.isInteger(row.run) || row.run < 1) reject("run must be a positive integer");
    if (!Number.isInteger(row.maximum) || row.maximum <= 0) reject("invalid maximum");
    if (!Number.isFinite(row.latencyMs) || row.latencyMs < 0) reject("invalid latencyMs");
    if (row.failed !== undefined && typeof row.failed !== "boolean") reject("failed must be boolean");
    for (const key of ["score", "referenceScore"])
      if (row[key] != null && (!Number.isInteger(row[key]) || row[key] < 0 || row[key] > row.maximum)) reject(`${key} is outside the rubric`);
    if (!row.failed && row.score == null) reject("a successful run requires a score");
    const key = JSON.stringify([row.skill, row.part, row.model, row.rubricVersion, row.maximum]);
    const identity = JSON.stringify([key, row.sampleId, row.run]);
    if (seen.has(identity)) reject("duplicate run");
    seen.add(identity);
    const sampleKey = JSON.stringify([key, row.sampleId]);
    if (row.referenceScore != null) {
      if (references.has(sampleKey) && references.get(sampleKey) !== row.referenceScore) reject("reference changed between runs");
      references.set(sampleKey, row.referenceScore);
    }
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(row);
  });
  return [...groups.entries()].map(([key, runs]) => {
    const [skill, part, model, rubricVersion, maximum] = JSON.parse(key);
    const valid = runs.filter((row) => !row.failed);
    const scored = valid.filter((row) => row.referenceScore != null);
    const errors = scored.map((row) => Math.abs(row.referenceScore - row.score));
    const bySample = new Map();
    for (const row of valid) {
      if (!bySample.has(row.sampleId)) bySample.set(row.sampleId, []);
      bySample.get(row.sampleId).push(row.score);
    }
    const repeated = [...bySample.values()].filter((scores) => scores.length >= 3);
    const ranges = repeated.map((scores) => Math.max(...scores) - Math.min(...scores));
    // One vote per sample for agreement; repeated runs only measure consistency.
    const firstBySample = new Map();
    for (const row of [...scored].sort((left, right) => left.run - right.run))
      if (!firstBySample.has(row.sampleId)) firstBySample.set(row.sampleId, [row.referenceScore, row.score]);
    const latencies = valid.map((row) => row.latencyMs);
    return {
      skill, part, model, rubricVersion, maximum, runs: runs.length,
      samples: new Set(runs.map((row) => row.sampleId)).size,
      failedRuns: runs.length - valid.length, referenceSamples: firstBySample.size,
      maeAcrossRuns: errors.length ? round(mean(errors)) : null,
      rmseAcrossRuns: errors.length ? round(Math.sqrt(mean(errors.map((error) => error ** 2)))) : null,
      exactAgreementAcrossRuns: errors.length ? round(mean(errors.map((error) => Number(error === 0)))) : null,
      qwkFirstRunPerSample: kappa([...firstBySample.values()]),
      samplesWithThreeRuns: repeated.length,
      stableSampleRate: ranges.length ? round(mean(ranges.map((range) => Number(range === 0)))) : null,
      maximumRepeatRange: ranges.length ? Math.max(...ranges) : null,
      latencyP50Ms: percentile(latencies, 0.5), latencyP95Ms: percentile(latencies, 0.95),
    };
  });
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const { positionals, values } = parseArgs({ allowPositionals: true, options: { output: { type: "string" } } });
    if (positionals.length !== 1) throw new Error("Usage: node scripts/benchmark-ai-evaluations.mjs runs.json [--output report.json]");
    const rows = JSON.parse(readFileSync(positionals[0], "utf8").replace(/^\uFEFF/, ""));
    const output = JSON.stringify({ groups: analyze(rows) }, null, 2) + "\n";
    if (values.output) writeFileSync(values.output, output);
    else process.stdout.write(output);
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  }
}
