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
//#region src/header/table-header.d.ts
export interface TableHeaderOptions {
  /**
   * The HTML attributes for a table header node.
   * @default {}
   * @example { class: 'foo' }
   */
  HTMLAttributes: Record<string, any>;
}
/**
 * This extension allows you to create table headers.
 * @see https://www.tiptap.dev/api/nodes/table-header
 */
export declare const TableHeader: Node<TableHeaderOptions, any>;
//#endregion
//# sourceMappingURL=index.d.ts.map