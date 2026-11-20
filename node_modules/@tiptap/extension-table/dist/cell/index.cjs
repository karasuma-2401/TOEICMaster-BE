Object.defineProperty(exports, Symbol.toStringTag, { value: "Module" });
let _tiptap_core = require("@tiptap/core");
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
exports.TableCell = TableCell;

//# sourceMappingURL=index.cjs.map