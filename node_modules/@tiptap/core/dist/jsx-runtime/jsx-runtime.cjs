Object.defineProperty(exports, Symbol.toStringTag, { value: "Module" });
//#region src/jsx-runtime.ts
const jsxElements = /* @__PURE__ */ new WeakSet();
const jsxFragments = /* @__PURE__ */ new WeakSet();
/** Create a new JSX element from the given spec */
function createJSXElement(spec) {
	const element = spec;
	jsxElements.add(element);
	return element;
}
/** Check if a spec is a JSX element */
function isJSXElement(value) {
	return Array.isArray(value) && jsxElements.has(value);
}
function flattenFragmentChildren(children) {
	return children.flatMap((child) => {
		if (child == null) return [];
		if (Array.isArray(child) && jsxFragments.has(child) && !isJSXElement(child)) return flattenFragmentChildren(child);
		return [child];
	});
}
function Fragment(props) {
	jsxFragments.add(props.children);
	return props.children;
}
function render(tag, attributes) {
	if (tag === "slot") return 0;
	if (tag instanceof Function) {
		const result = tag(attributes);
		if (Array.isArray(result) && !isJSXElement(result) && !jsxFragments.has(result)) return createJSXElement(result);
		return result;
	}
	const { children, ...rest } = attributes !== null && attributes !== void 0 ? attributes : {};
	if (tag === "svg") throw new Error("SVG elements are not supported in the JSX syntax, use the array syntax instead");
	if (Array.isArray(children)) {
		if (isJSXElement(children)) return createJSXElement([
			tag,
			rest,
			children
		]);
		if (children.length === 0) return createJSXElement([tag, rest]);
		const flattenedChildren = flattenFragmentChildren(children);
		if (flattenedChildren.length === 0) return createJSXElement([tag, rest]);
		return createJSXElement([
			tag,
			rest,
			...flattenedChildren
		]);
	}
	if (children !== void 0 && children !== null) return createJSXElement([
		tag,
		rest,
		children
	]);
	return createJSXElement([tag, rest]);
}
const h = (tag, attributes) => render(tag, attributes);
const jsxs = (tag, attributes) => render(tag, attributes);
const jsxDEV = (tag, attributes, _key, _isStaticChildren) => {
	return render(tag, attributes);
};
//#endregion
exports.Fragment = Fragment;
exports.createElement = h;
exports.h = h;
exports.jsx = h;
exports.jsxDEV = jsxDEV;
exports.jsxs = jsxs;

//# sourceMappingURL=jsx-runtime.cjs.map