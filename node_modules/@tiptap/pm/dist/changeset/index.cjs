var prosemirror_changeset = require("prosemirror-changeset");
Object.keys(prosemirror_changeset).forEach(function(k) {
	if (k !== "default" && !Object.prototype.hasOwnProperty.call(exports, k)) Object.defineProperty(exports, k, {
		enumerable: true,
		get: function() {
			return prosemirror_changeset[k];
		}
	});
});
