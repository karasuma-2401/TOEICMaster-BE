Object.defineProperty(exports, Symbol.toStringTag, { value: "Module" });
let _tiptap_core = require("@tiptap/core");
let _tiptap_pm_state = require("@tiptap/pm/state");
let _tiptap_pm_tables = require("@tiptap/pm/tables");
//#region src/utils/parseAlign.ts
/**
* Normalize unknown input into a supported table alignment
*
* @param value - A potential alignment value
* @returns A valid TableCellAlign value or null
*/
function normalizeTableCellAlign(value) {
	if (value === "left" || value === "right" || value === "center") return value;
	return null;
}
/**
* Parse table cell alignment from an HTML element
*
* Prefers inline style (${"`"}text-align${"`"}) and falls back to the legacy
* ${"`"}align${"`"} attribute.
*
* @param element - The table cell/header DOM element
* @returns A valid TableCellAlign value or null
*/
function parseAlign(element) {
	const styleAlign = (element.style.textAlign || "").trim().toLowerCase();
	const attrAlign = (element.getAttribute("align") || "").trim().toLowerCase();
	return normalizeTableCellAlign(styleAlign || attrAlign);
}
/**
* Normalize alignment from a generic attrs object that may include an align field
*
* @param attributes - A node attrs-like object with an optional align field
* @returns A valid TableCellAlign value or null.
*/
function normalizeTableCellAlignFromAttributes(attributes) {
	return normalizeTableCellAlign(attributes === null || attributes === void 0 ? void 0 : attributes.align);
}
/**
* Create a reusable Tiptap attribute config for table alignment
*
* @returns A Tiptap Attribute definition that parses and renders table alignment
*/
function createAlignAttribute() {
	return {
		default: null,
		parseHTML: (element) => parseAlign(element),
		renderHTML: (attributes) => {
			const align = normalizeTableCellAlign(attributes.align);
			if (!(0, _tiptap_core.isValidCSSStyleValue)(align)) return {};
			return { style: `text-align: ${align}` };
		}
	};
}
//#endregion
//#region src/utils/parseColwidth.ts
/**
* reads the width of the `<col>` element matching a cell's column from the table's `<colgroup>`
*
* @param element - The table cell/header DOM element
* @returns - An array with the column width in pixels or null
*/
function parseColgroupWidth(element) {
	var _table$querySelectorA;
	const row = element.parentElement;
	const table = element.closest("table");
	if (!row || !table) return null;
	const cellIndex = Array.from(row.children).indexOf(element);
	const width = (_table$querySelectorA = table.querySelectorAll("colgroup > col")[cellIndex]) === null || _table$querySelectorA === void 0 ? void 0 : _table$querySelectorA.getAttribute("width");
	return width ? [parseInt(width, 10)] : null;
}
/**
* Parse the column width/s of a table cell/header from an HTML element.
* it prefers the `colwidth` attribute and if not-provided falls back to the `width` attribute
* of the matching `<col>` element in the table's `<colgroup>`.
*
* @param element - The table cell/header DOM element
* @returns - An array of column widths in pixels or null
*/
function parseColwidth(element) {
	const colwidth = element.getAttribute("colwidth");
	if (colwidth) return colwidth.split(",").map((width) => parseInt(width, 10));
	return parseColgroupWidth(element);
}
//#endregion
//#region src/utils/fillEmptyCellContent.ts
const COLLAPSIBLE_WHITESPACE = /[ \t\r\n\f]+/g;
/** Whether a cell/header element has no child elements and only collapsible whitespace. */
function isEmptyCellElement(element) {
	var _element$textContent;
	if (element.children.length > 0) return false;
	return ((_element$textContent = element.textContent) !== null && _element$textContent !== void 0 ? _element$textContent : "").replace(COLLAPSIBLE_WHITESPACE, "") === "";
}
/** Builds a cell/header's minimal `block+` content, derived from the node type. */
function fillEmptyCellContent(cellType) {
	const filled = cellType.createAndFill();
	if (!filled) throw new Error(`[tiptap error]: "${cellType.name}" has no default content to backfill.`);
	return filled.content;
}
//#endregion
//#region src/utils/omitDefaultSpanAttribute.ts
function omitDefaultSpanAttribute(attributes, renderedAttributes, name) {
	if (!(name in renderedAttributes)) return;
	const value = renderedAttributes[name];
	if (value === 1 || value === "1" || value === null || value === void 0) delete attributes[name];
}
//#endregion
//#region src/cell/table-cell.ts
/**
* This extension allows you to create table cells.
* @see https://www.tiptap.dev/api/nodes/table-cell
*/
const TableCell = _tiptap_core.Node.create({
	name: "tableCell",
	addOptions() {
		return { HTMLAttributes: {} };
	},
	content: "block+",
	addAttributes() {
		return {
			colspan: { default: 1 },
			rowspan: { default: 1 },
			colwidth: {
				default: null,
				parseHTML: parseColwidth
			},
			align: createAlignAttribute()
		};
	},
	tableRole: "cell",
	isolating: true,
	parseHTML() {
		return [{
			tag: "td",
			getAttrs: (node) => isEmptyCellElement(node) ? {} : false,
			getContent: (_node, schema) => fillEmptyCellContent(schema.nodes[this.name])
		}, { tag: "td" }];
	},
	renderHTML({ HTMLAttributes }) {
		const attributes = (0, _tiptap_core.mergeAttributes)(this.options.HTMLAttributes, HTMLAttributes);
		omitDefaultSpanAttribute(attributes, HTMLAttributes, "colspan");
		omitDefaultSpanAttribute(attributes, HTMLAttributes, "rowspan");
		return [
			"td",
			attributes,
			0
		];
	}
});
//#endregion
//#region src/header/table-header.ts
/**
* This extension allows you to create table headers.
* @see https://www.tiptap.dev/api/nodes/table-header
*/
const TableHeader = _tiptap_core.Node.create({
	name: "tableHeader",
	addOptions() {
		return { HTMLAttributes: {} };
	},
	content: "block+",
	addAttributes() {
		return {
			colspan: { default: 1 },
			rowspan: { default: 1 },
			colwidth: {
				default: null,
				parseHTML: parseColwidth
			},
			align: createAlignAttribute()
		};
	},
	tableRole: "header_cell",
	isolating: true,
	parseHTML() {
		return [{
			tag: "th",
			getAttrs: (node) => isEmptyCellElement(node) ? {} : false,
			getContent: (_node, schema) => fillEmptyCellContent(schema.nodes[this.name])
		}, { tag: "th" }];
	},
	renderHTML({ HTMLAttributes }) {
		const attributes = (0, _tiptap_core.mergeAttributes)(this.options.HTMLAttributes, HTMLAttributes);
		omitDefaultSpanAttribute(attributes, HTMLAttributes, "colspan");
		omitDefaultSpanAttribute(attributes, HTMLAttributes, "rowspan");
		return [
			"th",
			attributes,
			0
		];
	}
});
//#endregion
//#region src/row/table-row.ts
/**
* This extension allows you to create table rows.
* @see https://www.tiptap.dev/api/nodes/table-row
*/
const TableRow = _tiptap_core.Node.create({
	name: "tableRow",
	addOptions() {
		return { HTMLAttributes: {} };
	},
	content: "(tableCell | tableHeader)*",
	tableRole: "row",
	parseHTML() {
		return [{ tag: "tr" }];
	},
	renderHTML({ HTMLAttributes }) {
		return [
			"tr",
			(0, _tiptap_core.mergeAttributes)(this.options.HTMLAttributes, HTMLAttributes),
			0
		];
	}
});
//#endregion
//#region src/table/utilities/colStyle.ts
function getColStyleDeclaration(minWidth, width) {
	if (width) return ["width", `${Math.max(width, minWidth)}px`];
	return ["min-width", `${minWidth}px`];
}
//#endregion
//#region src/table/TableView.ts
function updateColumns(node, colgroup, table, cellMinWidth, overrideCol, overrideValue) {
	let totalWidth = 0;
	let fixedWidth = true;
	let nextDOM = colgroup.firstChild;
	const row = node.firstChild;
	if (row !== null) for (let i = 0, col = 0; i < row.childCount; i += 1) {
		const { colspan, colwidth } = row.child(i).attrs;
		for (let j = 0; j < colspan; j += 1, col += 1) {
			const hasWidth = overrideCol === col ? overrideValue : colwidth && colwidth[j];
			const cssWidth = hasWidth ? `${hasWidth}px` : "";
			totalWidth += hasWidth || cellMinWidth;
			if (!hasWidth) fixedWidth = false;
			if (!nextDOM) {
				const colElement = document.createElement("col");
				const [propertyKey, propertyValue] = getColStyleDeclaration(cellMinWidth, hasWidth);
				colElement.style.setProperty(propertyKey, propertyValue);
				colgroup.appendChild(colElement);
			} else {
				if (nextDOM.style.width !== cssWidth) {
					const [propertyKey, propertyValue] = getColStyleDeclaration(cellMinWidth, hasWidth);
					nextDOM.style.setProperty(propertyKey, propertyValue);
				}
				nextDOM = nextDOM.nextSibling;
			}
		}
	}
	while (nextDOM) {
		var _nextDOM$parentNode;
		const after = nextDOM.nextSibling;
		(_nextDOM$parentNode = nextDOM.parentNode) === null || _nextDOM$parentNode === void 0 || _nextDOM$parentNode.removeChild(nextDOM);
		nextDOM = after;
	}
	const hasUserWidth = node.attrs.style && typeof node.attrs.style === "string" && /\bwidth\s*:/i.test(node.attrs.style);
	if (fixedWidth && !hasUserWidth) {
		table.style.width = `${totalWidth}px`;
		table.style.minWidth = "";
	} else {
		table.style.width = "";
		table.style.minWidth = `${totalWidth}px`;
	}
}
var TableView = class {
	constructor(node, cellMinWidth, _view, HTMLAttributes = {}) {
		this.node = node;
		this.cellMinWidth = cellMinWidth;
		this.dom = document.createElement("div");
		this.dom.className = "tableWrapper";
		this.table = this.dom.appendChild(document.createElement("table"));
		for (const [key, value] of Object.entries(HTMLAttributes)) if (value !== void 0 && value !== null) {
			if (key === "style") this.table.style.cssText = String(value);
			else this.table.setAttribute(key, String(value));
		}
		if (node.attrs.style) this.table.style.cssText = node.attrs.style;
		this.colgroup = this.table.appendChild(document.createElement("colgroup"));
		updateColumns(node, this.colgroup, this.table, cellMinWidth);
		this.contentDOM = this.table.appendChild(document.createElement("tbody"));
	}
	update(node) {
		if (!node.sameMarkup(this.node)) return false;
		this.node = node;
		updateColumns(node, this.colgroup, this.table, this.cellMinWidth);
		return true;
	}
	ignoreMutation(mutation) {
		const target = mutation.target;
		const isInsideWrapper = this.dom.contains(target);
		const isInsideContent = this.contentDOM.contains(target);
		if (isInsideWrapper && !isInsideContent) {
			if (mutation.type === "attributes" || mutation.type === "childList" || mutation.type === "characterData") return true;
		}
		return false;
	}
};
//#endregion
//#region src/table/utilities/createColGroup.ts
function createColGroup(node, cellMinWidth, overrideCol, overrideValue) {
	let totalWidth = 0;
	let fixedWidth = true;
	const cols = [];
	const row = node.firstChild;
	if (!row) return {};
	for (let i = 0, col = 0; i < row.childCount; i += 1) {
		const { colspan, colwidth } = row.child(i).attrs;
		for (let j = 0; j < colspan; j += 1, col += 1) {
			const hasWidth = overrideCol === col ? overrideValue : colwidth && colwidth[j];
			totalWidth += hasWidth || cellMinWidth;
			if (!hasWidth) fixedWidth = false;
			const [property, value] = getColStyleDeclaration(cellMinWidth, hasWidth);
			cols.push(["col", { style: `${property}: ${value}` }]);
		}
	}
	const tableWidth = fixedWidth ? `${totalWidth}px` : "";
	const tableMinWidth = fixedWidth ? "" : `${totalWidth}px`;
	return {
		colgroup: [
			"colgroup",
			{},
			...cols
		],
		tableWidth,
		tableMinWidth
	};
}
//#endregion
//#region src/table/utilities/createCell.ts
function createCell(cellType, cellContent) {
	if (cellContent) return cellType.createChecked(null, cellContent);
	return cellType.createAndFill();
}
//#endregion
//#region src/table/utilities/getTableNodeTypes.ts
function getTableNodeTypes(schema) {
	if (schema.cached.tableNodeTypes) return schema.cached.tableNodeTypes;
	const roles = {};
	Object.keys(schema.nodes).forEach((type) => {
		const nodeType = schema.nodes[type];
		if (nodeType.spec.tableRole) roles[nodeType.spec.tableRole] = nodeType;
	});
	schema.cached.tableNodeTypes = roles;
	return roles;
}
//#endregion
//#region src/table/utilities/createTable.ts
function createTable(schema, rowsCount, colsCount, withHeaderRow, cellContent) {
	const types = getTableNodeTypes(schema);
	const headerCells = [];
	const cells = [];
	for (let index = 0; index < colsCount; index += 1) {
		const cell = createCell(types.cell, cellContent);
		if (cell) cells.push(cell);
		if (withHeaderRow) {
			const headerCell = createCell(types.header_cell, cellContent);
			if (headerCell) headerCells.push(headerCell);
		}
	}
	const rows = [];
	for (let index = 0; index < rowsCount; index += 1) rows.push(types.row.createChecked(null, withHeaderRow && index === 0 ? headerCells : cells));
	return types.table.createChecked(null, rows);
}
//#endregion
//#region src/table/utilities/isCellSelection.ts
function isCellSelection(value) {
	return value instanceof _tiptap_pm_tables.CellSelection;
}
//#endregion
//#region src/table/utilities/deleteTableWhenAllCellsSelected.ts
const deleteTableWhenAllCellsSelected = ({ editor }) => {
	const { selection } = editor.state;
	if (!isCellSelection(selection)) return false;
	let cellCount = 0;
	const table = (0, _tiptap_core.findParentNodeClosestToPos)(selection.ranges[0].$from, (node) => {
		return node.type.name === "table";
	});
	table === null || table === void 0 || table.node.descendants((node) => {
		if (node.type.name === "table") return false;
		if (["tableCell", "tableHeader"].includes(node.type.name)) cellCount += 1;
	});
	if (!(cellCount === selection.ranges.length)) return false;
	editor.commands.deleteTable();
	return true;
};
//#endregion
//#region src/table/utilities/keepCursorInTable.ts
function keepCursorInTable(tr, tablePos) {
	const mappedTablePos = tr.mapping.map(tablePos);
	const stillInTable = (0, _tiptap_core.findParentNodeClosestToPos)(tr.selection.$from, (node) => node.type.name === "table");
	if ((stillInTable === null || stillInTable === void 0 ? void 0 : stillInTable.pos) === mappedTablePos) return;
	const tableNode = tr.doc.nodeAt(mappedTablePos);
	if (!tableNode) return;
	const endOfTable = mappedTablePos + tableNode.nodeSize - 1;
	tr.setSelection(_tiptap_pm_state.TextSelection.near(tr.doc.resolve(endOfTable), -1));
}
//#endregion
//#region src/table/utilities/markdown.ts
const DEFAULT_CELL_LINE_SEPARATOR = "";
/**
* Walk a single table-row line and escape any `|` characters that appear
* inside backtick code spans so marked's cell splitter ignores them.
* Backslash escape sequences outside code spans are passed through untouched.
* If a backtick run has no matching closer on the same line it is emitted
* as-is — no over-escaping for malformed input.
*/
function escapeTableCellPipes(line) {
	let result = "";
	let i = 0;
	while (i < line.length) {
		if (line[i] === "\\" && i + 1 < line.length) {
			result += line[i] + line[i + 1];
			i += 2;
			continue;
		}
		if (line[i] !== "`") {
			result += line[i++];
			continue;
		}
		let runLen = 0;
		while (i + runLen < line.length && line[i + runLen] === "`") runLen += 1;
		let j = i + runLen;
		let found = false;
		while (j < line.length) {
			if (line[j] !== "`") {
				j += 1;
				continue;
			}
			let closeLen = 0;
			while (j + closeLen < line.length && line[j + closeLen] === "`") closeLen += 1;
			if (closeLen === runLen) {
				const spanContent = line.slice(i + runLen, j);
				result += line.slice(i, i + runLen) + spanContent.replace(/\\\||\|/g, (match) => match === "|" ? "\\|" : match) + line.slice(j, j + runLen);
				i = j + runLen;
				found = true;
				break;
			}
			j += closeLen;
		}
		if (!found) {
			result += line.slice(i, i + runLen);
			i += runLen;
		}
	}
	return result;
}
/**
* Escape pipe characters inside backtick code spans on table-row lines.
* marked's `splitCells` only recognises backslash-escaped pipes (`\|`) and
* splits on every other `|`, so \`a || b\` in a table cell would be treated
* as multiple column delimiters. Escaping them here lets `splitCells` skip
* them; it already converts `\|` → `|` after splitting, so the cell content
* is restored correctly.
*/
function preprocessTablePipes(src) {
	return src.split("\n").map((line) => {
		if (!line.includes("|") || !line.includes("`")) return line;
		return escapeTableCellPipes(line);
	}).join("\n");
}
function collapseWhitespace(s) {
	return (s || "").replaceAll("", " ").replace(/\s+/g, " ").trim();
}
function replaceCellLineSeparator(value, cellLineSeparator) {
	if (!cellLineSeparator) return value;
	return value.split(cellLineSeparator).join("\n");
}
function escapePipesInCellText(text) {
	return text.replace(/\\.|\|/g, (match) => match === "|" ? "\\|" : match);
}
function renderTableToMarkdown(node, h, options = {}) {
	if (!node || !node.content || node.content.length === 0) return "";
	const rows = [];
	node.content.forEach((rowNode) => {
		const cells = [];
		if (rowNode.content) rowNode.content.forEach((cellNode) => {
			let raw = "";
			if (cellNode.content && Array.isArray(cellNode.content) && cellNode.content.length > 1) raw = cellNode.content.map((child) => replaceCellLineSeparator(h.renderChildren(child), options.cellLineSeparator)).join("\n");
			else raw = replaceCellLineSeparator(cellNode.content ? h.renderChildren(cellNode.content) : "", options.cellLineSeparator);
			const text = escapePipesInCellText(collapseWhitespace(raw.replace(/[ \t]*\r?\n[ \t]*/g, "<br>")));
			const isHeader = cellNode.type === "tableHeader";
			const align = normalizeTableCellAlignFromAttributes(cellNode.attrs);
			cells.push({
				text,
				isHeader,
				align
			});
		});
		rows.push(cells);
	});
	const columnCount = rows.reduce((max, r) => Math.max(max, r.length), 0);
	if (columnCount === 0) return "";
	const colWidths = Array.from({ length: columnCount }).fill(0);
	rows.forEach((r) => {
		for (let i = 0; i < columnCount; i += 1) {
			var _r$i;
			const len = (((_r$i = r[i]) === null || _r$i === void 0 ? void 0 : _r$i.text) || "").length;
			if (len > colWidths[i]) colWidths[i] = len;
			if (colWidths[i] < 3) colWidths[i] = 3;
		}
	});
	const pad = (s, width) => s + " ".repeat(Math.max(0, width - s.length));
	const headerRow = rows[0];
	const hasHeader = headerRow.some((c) => c.isHeader);
	const colAlignments = Array.from({ length: columnCount }).fill(null);
	rows.forEach((r) => {
		for (let i = 0; i < columnCount; i += 1) {
			var _r$i2;
			if (!colAlignments[i] && ((_r$i2 = r[i]) === null || _r$i2 === void 0 ? void 0 : _r$i2.align)) colAlignments[i] = r[i].align;
		}
	});
	let out = "\n";
	const headerTexts = Array.from({ length: columnCount }).map((_, i) => hasHeader ? headerRow[i] && headerRow[i].text || "" : "");
	out += `| ${headerTexts.map((t, i) => pad(t, colWidths[i])).join(" | ")} |\n`;
	out += `| ${colWidths.map((w, index) => {
		const dashCount = Math.max(3, w);
		const alignment = colAlignments[index];
		if (alignment === "left") return `:${"-".repeat(dashCount)}`;
		if (alignment === "right") return `${"-".repeat(dashCount)}:`;
		if (alignment === "center") return `:${"-".repeat(dashCount)}:`;
		return "-".repeat(dashCount);
	}).join(" | ")} |\n`;
	(hasHeader ? rows.slice(1) : rows).forEach((r) => {
		out += `| ${Array.from({ length: columnCount }).fill(0).map((_, i) => pad(r[i] && r[i].text || "", colWidths[i])).join(" | ")} |\n`;
	});
	return out;
}
//#endregion
//#region src/table/utilities/preserveCellSelectionOnContextMenu.ts
const preserveCellSelectionOnContextMenuPluginKey = new _tiptap_pm_state.PluginKey("preserveCellSelectionOnContextMenu");
function resolveClickPos(view, event) {
	const coords = view.posAtCoords({
		left: event.clientX,
		top: event.clientY
	});
	if (coords) return coords.pos;
	if (!(event.target instanceof Node)) return null;
	try {
		return view.posAtDOM(event.target, 0);
	} catch {
		return null;
	}
}
function isPosInsideCellSelection(selection, pos) {
	let inside = false;
	selection.forEachCell((cell, cellPos) => {
		if (pos >= cellPos && pos < cellPos + cell.nodeSize) inside = true;
	});
	return inside;
}
function preserveCellSelectionOnContextMenu() {
	return new _tiptap_pm_state.Plugin({
		key: preserveCellSelectionOnContextMenuPluginKey,
		props: { handleDOMEvents: { mousedown(view, event) {
			if (event.button !== 2) return false;
			const { selection } = view.state;
			if (!isCellSelection(selection) || selection.ranges.length < 2) return false;
			const clickPos = resolveClickPos(view, event);
			if (clickPos == null || !isPosInsideCellSelection(selection, clickPos)) return false;
			event.preventDefault();
			return true;
		} } }
	});
}
//#endregion
//#region src/table/table.ts
/**
* This extension allows you to create tables.
* @see https://www.tiptap.dev/api/nodes/table
*/
const Table = _tiptap_core.Node.create({
	name: "table",
	addOptions() {
		return {
			HTMLAttributes: {},
			resizable: false,
			renderWrapper: false,
			handleWidth: 5,
			cellMinWidth: 25,
			View: TableView,
			lastColumnResizable: true,
			allowTableNodeSelection: false
		};
	},
	content: "tableRow+",
	tableRole: "table",
	isolating: true,
	group: "block",
	parseHTML() {
		return [{ tag: "table" }];
	},
	renderHTML({ node, HTMLAttributes }) {
		const { colgroup, tableWidth, tableMinWidth } = createColGroup(node, this.options.cellMinWidth);
		const userStyles = HTMLAttributes.style;
		function getTableStyle() {
			if (userStyles) return userStyles;
			return tableWidth ? `width: ${tableWidth}` : `min-width: ${tableMinWidth}`;
		}
		const table = [
			"table",
			(0, _tiptap_core.mergeAttributes)(this.options.HTMLAttributes, HTMLAttributes, { style: getTableStyle() }),
			colgroup,
			["tbody", 0]
		];
		return this.options.renderWrapper ? [
			"div",
			{ class: "tableWrapper" },
			table
		] : table;
	},
	parseMarkdown: (token, h) => {
		const rows = [];
		const alignments = Array.isArray(token.align) ? token.align : [];
		if (token.header) {
			const headerCells = [];
			token.header.forEach((cell, index) => {
				var _alignments$index;
				const align = normalizeTableCellAlign((_alignments$index = alignments[index]) !== null && _alignments$index !== void 0 ? _alignments$index : cell.align);
				const attrs = align ? { align } : {};
				headerCells.push(h.createNode("tableHeader", attrs, [{
					type: "paragraph",
					content: h.parseInline(cell.tokens)
				}]));
			});
			rows.push(h.createNode("tableRow", {}, headerCells));
		}
		if (token.rows) token.rows.forEach((row) => {
			const bodyCells = [];
			row.forEach((cell, index) => {
				var _alignments$index2;
				const align = normalizeTableCellAlign((_alignments$index2 = alignments[index]) !== null && _alignments$index2 !== void 0 ? _alignments$index2 : cell.align);
				const attrs = align ? { align } : {};
				bodyCells.push(h.createNode("tableCell", attrs, [{
					type: "paragraph",
					content: h.parseInline(cell.tokens)
				}]));
			});
			rows.push(h.createNode("tableRow", {}, bodyCells));
		});
		return h.createNode("table", void 0, rows);
	},
	renderMarkdown: (node, h) => {
		return renderTableToMarkdown(node, h);
	},
	markdownTokenizer: {
		name: "table",
		level: "block",
		start: (src) => {
			const lines = src.split("\n");
			if (lines.length < 2) return -1;
			const sep = lines[1];
			if (!/^[ \t|:]*-[ \t|:-]*$/.test(sep) || !sep.includes("|")) return -1;
			return lines[0].includes("|") ? 0 : -1;
		},
		tokenize(src, _tokens, helper) {
			const blankLineIndex = src.indexOf("\n\n");
			const candidate = blankLineIndex >= 0 ? src.slice(0, blankLineIndex) : src;
			const candidateLines = candidate.split("\n");
			if (candidateLines.length < 2) return void 0;
			const sep = candidateLines[1];
			if (!/^[ \t|:]*-[ \t|:-]*$/.test(sep) || !sep.includes("|")) return void 0;
			const preprocessed = preprocessTablePipes(candidate);
			if (preprocessed === candidate) return void 0;
			const tableToken = helper.blockTokens(preprocessed)[0];
			if ((tableToken === null || tableToken === void 0 ? void 0 : tableToken.type) !== "table" || !tableToken.raw) return void 0;
			const lineCount = tableToken.raw.split("\n").length;
			const raw = src.split("\n").slice(0, lineCount).join("\n");
			return {
				...tableToken,
				raw
			};
		}
	},
	addCommands() {
		return {
			insertTable: ({ rows = 3, cols = 3, withHeaderRow = true } = {}) => ({ tr, dispatch, editor }) => {
				const node = createTable(editor.schema, rows, cols, withHeaderRow);
				if (dispatch) {
					const offset = tr.selection.from + 1;
					tr.replaceSelectionWith(node).scrollIntoView().setSelection(_tiptap_pm_state.TextSelection.near(tr.doc.resolve(offset)));
				}
				return true;
			},
			addColumnBefore: () => ({ state, dispatch }) => {
				return (0, _tiptap_pm_tables.addColumnBefore)(state, dispatch);
			},
			addColumnAfter: () => ({ state, dispatch }) => {
				return (0, _tiptap_pm_tables.addColumnAfter)(state, dispatch);
			},
			deleteColumn: () => ({ state, dispatch }) => {
				const table = (0, _tiptap_core.findParentNodeClosestToPos)(state.selection.$from, (node) => node.type.name === "table");
				return (0, _tiptap_pm_tables.deleteColumn)(state, dispatch && ((tr) => {
					if (table) keepCursorInTable(tr, table.pos);
					dispatch(tr);
				}));
			},
			addRowBefore: () => ({ state, dispatch }) => {
				return (0, _tiptap_pm_tables.addRowBefore)(state, dispatch);
			},
			addRowAfter: () => ({ state, dispatch }) => {
				return (0, _tiptap_pm_tables.addRowAfter)(state, dispatch);
			},
			deleteRow: () => ({ state, dispatch }) => {
				const table = (0, _tiptap_core.findParentNodeClosestToPos)(state.selection.$from, (node) => node.type.name === "table");
				return (0, _tiptap_pm_tables.deleteRow)(state, dispatch && ((tr) => {
					if (table) keepCursorInTable(tr, table.pos);
					dispatch(tr);
				}));
			},
			deleteTable: () => ({ state, dispatch }) => {
				return (0, _tiptap_pm_tables.deleteTable)(state, dispatch);
			},
			mergeCells: () => ({ state, dispatch }) => {
				return (0, _tiptap_pm_tables.mergeCells)(state, dispatch);
			},
			splitCell: () => ({ state, dispatch }) => {
				return (0, _tiptap_pm_tables.splitCell)(state, dispatch);
			},
			toggleHeaderColumn: () => ({ state, dispatch }) => {
				return (0, _tiptap_pm_tables.toggleHeader)("column")(state, dispatch);
			},
			toggleHeaderRow: () => ({ state, dispatch }) => {
				return (0, _tiptap_pm_tables.toggleHeader)("row")(state, dispatch);
			},
			toggleHeaderCell: () => ({ state, dispatch }) => {
				return (0, _tiptap_pm_tables.toggleHeaderCell)(state, dispatch);
			},
			mergeOrSplit: () => ({ state, dispatch }) => {
				if ((0, _tiptap_pm_tables.mergeCells)(state, dispatch)) return true;
				return (0, _tiptap_pm_tables.splitCell)(state, dispatch);
			},
			setCellAttribute: (name, value) => ({ state, dispatch }) => {
				return (0, _tiptap_pm_tables.setCellAttr)(name, value)(state, dispatch);
			},
			goToNextCell: () => ({ state, dispatch }) => {
				return (0, _tiptap_pm_tables.goToNextCell)(1)(state, dispatch);
			},
			goToPreviousCell: () => ({ state, dispatch }) => {
				return (0, _tiptap_pm_tables.goToNextCell)(-1)(state, dispatch);
			},
			fixTables: () => ({ state, dispatch }) => {
				if (dispatch) (0, _tiptap_pm_tables.fixTables)(state);
				return true;
			},
			setCellSelection: (position) => ({ tr, dispatch }) => {
				if (dispatch) {
					const selection = _tiptap_pm_tables.CellSelection.create(tr.doc, position.anchorCell, position.headCell);
					tr.setSelection(selection);
				}
				return true;
			}
		};
	},
	addKeyboardShortcuts() {
		return {
			Tab: () => {
				if (this.editor.commands.goToNextCell()) return true;
				if (!this.editor.can().addRowAfter()) return false;
				return this.editor.chain().addRowAfter().goToNextCell().run();
			},
			"Shift-Tab": () => this.editor.commands.goToPreviousCell(),
			Backspace: deleteTableWhenAllCellsSelected,
			"Mod-Backspace": deleteTableWhenAllCellsSelected,
			Delete: deleteTableWhenAllCellsSelected,
			"Mod-Delete": deleteTableWhenAllCellsSelected
		};
	},
	addProseMirrorPlugins() {
		return [
			...this.options.resizable && this.editor.isEditable ? [(0, _tiptap_pm_tables.columnResizing)({
				handleWidth: this.options.handleWidth,
				cellMinWidth: this.options.cellMinWidth,
				defaultCellMinWidth: this.options.cellMinWidth,
				View: null,
				lastColumnResizable: this.options.lastColumnResizable
			})] : [],
			(0, _tiptap_pm_tables.tableEditing)({ allowTableNodeSelection: this.options.allowTableNodeSelection }),
			preserveCellSelectionOnContextMenu()
		];
	},
	addNodeView() {
		const View = this.options.View;
		if (!View) return null;
		return ({ node, view, HTMLAttributes }) => {
			const mergedAttributes = (0, _tiptap_core.mergeAttributes)(this.options.HTMLAttributes, HTMLAttributes);
			return new View(node, this.options.cellMinWidth, view, mergedAttributes);
		};
	},
	extendNodeSchema(extension) {
		const context = {
			name: extension.name,
			options: extension.options,
			storage: extension.storage
		};
		return { tableRole: (0, _tiptap_core.callOrReturn)((0, _tiptap_core.getExtensionField)(extension, "tableRole", context)) };
	}
});
//#endregion
//#region src/kit/index.ts
/**
* The table kit is a collection of table editor extensions.
*
* It’s a good starting point for building your own table in Tiptap.
*/
const TableKit = _tiptap_core.Extension.create({
	name: "tableKit",
	addExtensions() {
		const extensions = [];
		if (this.options.table !== false) extensions.push(Table.configure(this.options.table));
		if (this.options.tableCell !== false) extensions.push(TableCell.configure(this.options.tableCell));
		if (this.options.tableHeader !== false) extensions.push(TableHeader.configure(this.options.tableHeader));
		if (this.options.tableRow !== false) extensions.push(TableRow.configure(this.options.tableRow));
		return extensions;
	}
});
//#endregion
exports.DEFAULT_CELL_LINE_SEPARATOR = DEFAULT_CELL_LINE_SEPARATOR;
exports.Table = Table;
exports.TableCell = TableCell;
exports.TableHeader = TableHeader;
exports.TableKit = TableKit;
exports.TableRow = TableRow;
exports.TableView = TableView;
exports.createColGroup = createColGroup;
exports.createTable = createTable;
exports.escapeTableCellPipes = escapeTableCellPipes;
exports.preprocessTablePipes = preprocessTablePipes;
exports.renderTableToMarkdown = renderTableToMarkdown;
exports.updateColumns = updateColumns;

//# sourceMappingURL=index.cjs.map