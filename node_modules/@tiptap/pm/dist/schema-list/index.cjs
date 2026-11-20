var prosemirror_schema_list = require("prosemirror-schema-list");
Object.keys(prosemirror_schema_list).forEach(function(k) {
	if (k !== "default" && !Object.prototype.hasOwnProperty.call(exports, k)) Object.defineProperty(exports, k, {
		enumerable: true,
		get: function() {
			return prosemirror_schema_list[k];
		}
	});
});
