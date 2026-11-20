var prosemirror_history = require("prosemirror-history");
Object.keys(prosemirror_history).forEach(function(k) {
	if (k !== "default" && !Object.prototype.hasOwnProperty.call(exports, k)) Object.defineProperty(exports, k, {
		enumerable: true,
		get: function() {
			return prosemirror_history[k];
		}
	});
});
