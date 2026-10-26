import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const outputDir = path.resolve(scriptDir, "../outputs/admin-lr-exam-import-template");
const outputPath = path.join(outputDir, "toeic-lr-exam-import-template.xlsx");
const previewDir = path.join(os.tmpdir(), "toeic-lr-exam-import-template-preview");
const placeholderScoreProfileId = "00000000-0000-4000-8000-000000000001";
const placeholderImageAssetId = "00000000-0000-4000-8000-000000000002";
const colors = { ink: "#1F2937", navy: "#1E3A5F", blue: "#EAF2F8", input: "#FFF7D6", border: "#CBD5E1", note: "#475569" };

const blocks = [];
const contexts = [];
const questions = [];

function addQuestion(blockKey, part, number) {
  const suffix = String(number).padStart(3, "0");
  questions.push([
    blockKey,
    number,
    `LR-DEMO-${suffix}`,
    `Sample Part ${part} question ${number}. Replace with approved exam content before import.`,
    "A. Sample option A",
    "B. Sample option B",
    "C. Sample option C",
    part === 2 ? "" : "D. Sample option D",
    "A",
    "Bản dịch mẫu. Thay bằng nội dung được biên soạn.",
    "Giải thích mẫu. Thay bằng nội dung được biên soạn.",
    part === 1 ? placeholderImageAssetId : "",
  ]);
}

function addSingles(part, firstQuestionNumber, count) {
  for (let number = firstQuestionNumber; number < firstQuestionNumber + count; number += 1) {
    const blockKey = `P${part}-Q${number}`;
    blocks.push([blockKey, part, "SINGLE", number, 1]);
    addQuestion(blockKey, part, number);
  }
}

function addGroups(part, firstQuestionNumber, groupSizes) {
  let nextQuestionNumber = firstQuestionNumber;
  for (const count of groupSizes) {
    const blockKey = `P${part}-G${nextQuestionNumber}`;
    blocks.push([blockKey, part, "GROUP", nextQuestionNumber, count]);
    contexts.push([
      blockKey,
      1,
      "TEXT",
      `Shared context for Part ${part}, questions ${nextQuestionNumber}-${nextQuestionNumber + count - 1}. Replace with the approved transcript, reading passage, or add IMAGE rows with uploaded ImageAssetId values.`,
      "",
      "",
    ]);
    for (let number = nextQuestionNumber; number < nextQuestionNumber + count; number += 1)
      addQuestion(blockKey, part, number);
    nextQuestionNumber += count;
  }
}

addSingles(1, 1, 6);
addSingles(2, 7, 25);
addGroups(3, 32, Array(13).fill(3));
addGroups(4, 71, Array(10).fill(3));
addSingles(5, 101, 30);
addGroups(6, 131, Array(4).fill(4));
addGroups(7, 147, [2, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 5, 5, 5]);

function styleTitle(sheet, rangeAddress, title) {
  const titleCell = sheet.getRange(rangeAddress.split(":")[0]);
  titleCell.values = [[title]];
  titleCell.format = { font: { name: "Arial", size: 14, bold: true, color: colors.ink }, horizontalAlignment: "left", verticalAlignment: "center" };
  titleCell.format.rowHeight = 26;
}

function styleHeader(sheet, rangeAddress) {
  const range = sheet.getRange(rangeAddress);
  range.format = { fill: colors.navy, font: { name: "Arial", size: 10, bold: true, color: "#FFFFFF" }, horizontalAlignment: "center", verticalAlignment: "center", wrapText: true, borders: { preset: "all", style: "thin", color: "#FFFFFF" } };
  range.format.rowHeight = 30;
}

function styleInputs(sheet, rangeAddress) {
  const range = sheet.getRange(rangeAddress);
  range.format = { fill: colors.input, font: { name: "Arial", size: 10, color: colors.ink }, verticalAlignment: "top", wrapText: true, borders: { preset: "inside", style: "thin", color: colors.border } };
  range.format.borders = { preset: "outside", style: "thin", color: colors.border };
}

function setWidths(sheet, widths) {
  for (const [column, width] of Object.entries(widths))
    sheet.getRange(`${column}:${column}`).format.columnWidth = width;
}

const workbook = Workbook.create();
const readMe = workbook.worksheets.add("ReadMe");
const manifest = workbook.worksheets.add("Manifest");
const blocksSheet = workbook.worksheets.add("Blocks");
const contextSheet = workbook.worksheets.add("ContextBlocks");
const questionsSheet = workbook.worksheets.add("Questions");

for (const sheet of [readMe, manifest, blocksSheet, contextSheet, questionsSheet]) {
  sheet.showGridLines = false;
  sheet.tabColor = colors.navy;
}
readMe.tabColor = "#64748B";

styleTitle(readMe, "A1:H1", "TOEIC Listening & Reading import template");
readMe.getRange("A3:B11").values = [
  ["Mục", "Hướng dẫn"],
  ["Phạm vi", "Một workbook tạo đúng một đề L&R gồm đủ 200 câu, cấu trúc ETS cố định từ Part 1 đến Part 7."],
  ["Master Audio", "Đính kèm riêng một file .mp3 hoặc .wav trong cùng request import. Không nhúng audio vào Excel. Giới hạn 100 MB."],
  ["Barem", "Tạo Score Profile trước, rồi thay ScoreProfileId trong Manifest bằng UUID profile do API trả về. Draft có thể dùng profile chưa đủ mapping; Publish sẽ chặn cho đến khi đủ 101 mốc Listening và 101 mốc Reading."],
  ["Ảnh Part 1", "Upload ảnh trước qua API media. Thay ImageAssetId placeholder ở các câu Part 1 bằng UUID asset trả về. Chỉ dùng PNG, JPG/JPEG hoặc WEBP, tối đa 5 MB."],
  ["Ảnh ngữ cảnh", "Muốn thêm ảnh cho nhóm Part 3, 4, 6 hoặc 7: thêm dòng ContextBlocks, Type=IMAGE, ImageAssetId là UUID đã upload; giữ Ordinal liên tiếp từ 1."],
  ["Không sửa cấu trúc", "Không thêm, xóa, đổi BlockKey, Part, QuestionNumber hay QuestionCount. Importer đối chiếu toàn bộ 103 block và 200 slot; chỉ cần một lỗi là từ chối toàn bộ."],
  ["Nội dung mẫu", "Toàn bộ câu/context trong template là placeholder để thay thế. Không dùng để xuất bản."],
  ["Nguồn yêu cầu", "2. Tài liệu phân tích.md, AC-02.1 đến AC-02.11; quyết định nghiệp vụ đã chốt: một Master Audio tổng, import atomic."],
];
styleHeader(readMe, "A3:B3");
styleInputs(readMe, "A4:B11");
readMe.getRange("A4:A11").format.font = { name: "Arial", size: 10, bold: true, color: colors.ink };
readMe.getRange("A4:B11").format.rowHeight = 44;
setWidths(readMe, { A: 22, B: 116 });

styleTitle(manifest, "A1:D1", "Manifest import");
manifest.getRange("A3:B8").values = [
  ["Field", "Value"],
  ["SchemaVersion", "1"],
  ["TestCode", "LR-YYYY-001"],
  ["Title", "TOEIC Listening & Reading practice test"],
  ["Scope", "LISTENING_READING"],
  ["ScoreProfileId", placeholderScoreProfileId],
];
styleHeader(manifest, "A3:B3");
styleInputs(manifest, "A4:B8");
setWidths(manifest, { A: 24, B: 58 });

styleTitle(blocksSheet, "A1:G1", "Blocks: fixed ETS structure, do not alter");
blocksSheet.getRange("A3:E3").values = [["BlockKey", "Part", "BlockType", "FirstQuestionNumber", "QuestionCount"]];
blocksSheet.getRange(`A4:E${blocks.length + 3}`).values = blocks;
styleHeader(blocksSheet, "A3:E3");
styleInputs(blocksSheet, `A4:E${blocks.length + 3}`);
blocksSheet.freezePanes.freezeRows(3);
blocksSheet.dataValidations.add({ range: `C4:C${blocks.length + 3}`, rule: { type: "list", values: ["SINGLE", "GROUP"] } });
setWidths(blocksSheet, { A: 18, B: 9, C: 14, D: 21, E: 15 });

styleTitle(contextSheet, "A1:H1", "ContextBlocks: ordered text or image blocks for every GROUP");
contextSheet.getRange("A3:F3").values = [["BlockKey", "Ordinal", "Type", "Text", "ImageAssetId", "AltText"]];
contextSheet.getRange(`A4:F${contexts.length + 3}`).values = contexts;
styleHeader(contextSheet, "A3:F3");
styleInputs(contextSheet, `A4:F${contexts.length + 3}`);
contextSheet.freezePanes.freezeRows(3);
contextSheet.dataValidations.add({ range: `C4:C${contexts.length + 103}`, rule: { type: "list", values: ["TEXT", "IMAGE"] } });
setWidths(contextSheet, { A: 18, B: 10, C: 12, D: 100, E: 40, F: 32 });

styleTitle(questionsSheet, "A1:L1", "Questions: one row per fixed question slot");
questionsSheet.getRange("A3:L3").values = [["BlockKey", "QuestionNumber", "QuestionCode", "Stem", "OptionA", "OptionB", "OptionC", "OptionD", "CorrectKey", "Translation", "Explanation", "ImageAssetId"]];
questionsSheet.getRange(`A4:L${questions.length + 3}`).values = questions;
styleHeader(questionsSheet, "A3:L3");
styleInputs(questionsSheet, `A4:L${questions.length + 3}`);
questionsSheet.freezePanes.freezeRows(3);
questionsSheet.freezePanes.freezeColumns(3);
questionsSheet.dataValidations.add({ range: `I4:I${questions.length + 3}`, rule: { type: "list", values: ["A", "B", "C", "D"] } });
setWidths(questionsSheet, { A: 18, B: 16, C: 20, D: 60, E: 28, F: 28, G: 28, H: 28, I: 12, J: 38, K: 52, L: 40 });

workbook.recalculate();
await fs.mkdir(outputDir, { recursive: true });
await fs.mkdir(previewDir, { recursive: true });
for (const [sheetName, range, fileName] of [
  ["ReadMe", "A1:B11", "readme.png"],
  ["Manifest", "A1:B8", "manifest.png"],
  ["Blocks", "A1:E16", "blocks.png"],
  ["ContextBlocks", "A1:F16", "contexts.png"],
  ["Questions", "A1:L15", "questions.png"],
]) {
  const preview = await workbook.render({ sheetName, range, scale: 1.25, format: "png" });
  await fs.writeFile(path.join(previewDir, fileName), new Uint8Array(await preview.arrayBuffer()));
}
const exported = await SpreadsheetFile.exportXlsx(workbook);
await exported.save(outputPath);
const check = await workbook.inspect({ kind: "table", range: "Questions!A3:L8", include: "values,formulas", tableMaxRows: 8, tableMaxCols: 12 });
const errors = await workbook.inspect({ kind: "match", searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!", options: { useRegex: true, maxResults: 50 }, summary: "formula error scan" });
console.log(check.ndjson);
console.log(errors.ndjson);
console.log(JSON.stringify({ outputPath, previewDir, blocks: blocks.length, contexts: contexts.length, questions: questions.length }));
