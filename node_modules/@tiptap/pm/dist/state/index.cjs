var prosemirror_state = require("prosemirror-state");
Object.keys(prosemirror_state).forEach(function(k) {
	if (k !== "default" && !Object.prototype.hasOwnProperty.call(exports, k)) Object.defineProperty(exports, k, {
		enumerable: true,
		get: function() {
			return prosemirror_state[k];
		}
	});
});
