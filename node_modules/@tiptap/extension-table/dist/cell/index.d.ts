import { Node, ParentConfig } from "@tiptap/core";
//#region src/types.d.ts
declare module '@tiptap/core' {
  interface NodeConfig<Options, Storage> {
    /**
     * A string or function to determine the role of the table.
     * @default 'table'
     * @example () => 'table'
     */
    tableRole?: string | ((this: {
      name: string;
      options: Options;
      storage: Storage;
      parent: ParentConfig<NodeConfig<Options>>['tableRole'];
    }) => string);
  }
}
//#endregion
//#region src/cell/table-cell.d.ts
export interface TableCellOptions {
  /**
   * The HTML attributes for a table cell node.
   * @default {}
   * @example { class: 'foo' }
   */
  HTMLAttributes: Record<string, any>;
}
/**
 * This extension allows you to create table cells.
 * @see https://www.tiptap.dev/api/nodes/table-cell
 */
export declare const TableCell: Node<TableCellOptions, any>;
//#endregion
//# sourceMappingURL=index.d.ts.map