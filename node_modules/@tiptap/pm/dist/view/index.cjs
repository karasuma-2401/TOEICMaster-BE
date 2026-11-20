var prosemirror_view = require("prosemirror-view");
Object.keys(prosemirror_view).forEach(function(k) {
	if (k !== "default" && !Object.prototype.hasOwnProperty.call(exports, k)) Object.defineProperty(exports, k, {
		enumerable: true,
		get: function() {
			return prosemirror_view[k];
		}
	});
});
