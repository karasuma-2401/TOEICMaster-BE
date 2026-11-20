var prosemirror_model = require("prosemirror-model");
Object.keys(prosemirror_model).forEach(function(k) {
	if (k !== "default" && !Object.prototype.hasOwnProperty.call(exports, k)) Object.defineProperty(exports, k, {
		enumerable: true,
		get: function() {
			return prosemirror_model[k];
		}
	});
});
