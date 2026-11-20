var prosemirror_inputrules = require("prosemirror-inputrules");
Object.keys(prosemirror_inputrules).forEach(function(k) {
	if (k !== "default" && !Object.prototype.hasOwnProperty.call(exports, k)) Object.defineProperty(exports, k, {
		enumerable: true,
		get: function() {
			return prosemirror_inputrules[k];
		}
	});
});
