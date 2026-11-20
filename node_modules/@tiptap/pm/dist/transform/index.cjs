var prosemirror_transform = require("prosemirror-transform");
Object.keys(prosemirror_transform).forEach(function(k) {
	if (k !== "default" && !Object.prototype.hasOwnProperty.call(exports, k)) Object.defineProperty(exports, k, {
		enumerable: true,
		get: function() {
			return prosemirror_transform[k];
		}
	});
});
