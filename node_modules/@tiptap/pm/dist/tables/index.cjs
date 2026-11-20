var prosemirror_tables = require("prosemirror-tables");
Object.keys(prosemirror_tables).forEach(function(k) {
	if (k !== "default" && !Object.prototype.hasOwnProperty.call(exports, k)) Object.defineProperty(exports, k, {
		enumerable: true,
		get: function() {
			return prosemirror_tables[k];
		}
	});
});
