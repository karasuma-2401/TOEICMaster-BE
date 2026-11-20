//#region src/jsx-runtime.d.ts
export type Attributes = Record<string, any>;
export type DOMOutputSpecElement = 0 | Attributes | DOMOutputSpecArray;
/**
 * Better describes the output of a `renderHTML` function in prosemirror
 * @see https://prosemirror.net/docs/ref/#model.DOMOutputSpec
 */
export type DOMOutputSpecArray = [string] | [string, Attributes] | [string, 0] | [string, Attributes, 0] | [string, Attributes, DOMOutputSpecArray | 0] | [string, DOMOutputSpecArray];
export declare namespace JSX {
  type Element = DOMOutputSpecArray;
  interface IntrinsicElements {
    [key: string]: any;
  }
  interface ElementChildrenAttribute {
    children: unknown;
  }
}
type JSXChild = DOMOutputSpecElement | string | null | undefined | JSXChild[];
export type JSXRenderer = (tag: 'slot' | string | ((props?: Attributes) => DOMOutputSpecArray | DOMOutputSpecElement), props?: Attributes, ...children: JSXChild[]) => DOMOutputSpecArray | DOMOutputSpecElement;
export declare function Fragment(props: {
  children: JSXChild[];
}): JSXChild[];
export declare const h: JSXRenderer;
export declare const jsxs: JSXRenderer;
export declare const jsxDEV: (tag: Parameters<JSXRenderer>[0], attributes?: Attributes, _key?: unknown, _isStaticChildren?: boolean) => 0 | any[] | Attributes;
//#endregion
export { h as createElement, h as jsx };
//# sourceMappingURL=jsx-runtime.d.ts.map