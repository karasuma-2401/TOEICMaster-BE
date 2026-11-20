import { Node, mergeAttributes } from "@tiptap/core";
//#region src/row/table-row.ts
/**
* This extension allows you to create table rows.
* @see https://www.tiptap.dev/api/nodes/table-row
*/
const TableRow = Node.create({
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
			mergeAttributes(this.options.HTMLAttributes, HTMLAttributes),
			0
		];
	}
});
//#endregion
export { TableRow };

//# sourceMappingURL=index.js.map