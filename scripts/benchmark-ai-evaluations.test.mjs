import { test } from "node:test";
import assert from "node:assert/strict";
import { analyze } from "./benchmark-ai-evaluations.mjs";

const row = (update = {}) => ({ sampleId: "one", skill: "writing", part: 1, run: 1,
  model: "synthetic-test", rubricVersion: "test-only", maximum: 3,
  score: 2, referenceScore: 2, latencyMs: 100, ...update });

test("perfect agreement and degenerate kappa", () => {
  const result = analyze([row(), row({ sampleId: "two", score: 3, referenceScore: 3 })])[0];
  assert.equal(result.maeAcrossRuns, 0);
  assert.equal(result.qwkFirstRunPerSample, 1);
  assert.equal(analyze([row()])[0].qwkFirstRunPerSample, null);
});
test("repeated runs do not inflate reference sample count", () => {
  const result = analyze([1, 2, 3].map((run) => row({ run, score: run })))[0];
  assert.equal(result.referenceSamples, 1);
  assert.equal(result.samplesWithThreeRuns, 1);
  assert.equal(result.maximumRepeatRange, 2);
  assert.equal(result.stableSampleRate, 0);
});
test("missing teacher scores leave accuracy unknown", () => {
  const result = analyze([row({ referenceScore: null })])[0];
  assert.equal(result.maeAcrossRuns, null);
  assert.equal(result.qwkFirstRunPerSample, null);
  assert.equal(result.latencyP50Ms, 100);
});
test("failed runs and versions remain separate", () => {
  const result = analyze([row(), row({ sampleId: "two", score: null, failed: true })])[0];
  assert.equal(result.failedRuns, 1);
  assert.equal(result.referenceSamples, 1);
  assert.equal(analyze([row(), row({ rubricVersion: "other" })]).length, 2);
});
test("invalid scores, duplicate runs and changing references are rejected", () => {
  for (const rows of [[row(), row()], [row({score: 4})], [row({latencyMs: -1})], [row(), row({run: 2, referenceScore: 1})]])
    assert.throws(() => analyze(rows));
});
