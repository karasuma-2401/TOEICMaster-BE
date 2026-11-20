Object.defineProperty(exports, Symbol.toStringTag, { value: "Module" });
let _tiptap_core = require("@tiptap/core");
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
exports.TableRow = TableRow;

//# sourceMappingURL=index.cjs.map