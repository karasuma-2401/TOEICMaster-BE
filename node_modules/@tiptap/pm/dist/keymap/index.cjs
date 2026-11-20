var prosemirror_keymap = require("prosemirror-keymap");
Object.keys(prosemirror_keymap).forEach(function(k) {
	if (k !== "default" && !Object.prototype.hasOwnProperty.call(exports, k)) Object.defineProperty(exports, k, {
		enumerable: true,
		get: function() {
			return prosemirror_keymap[k];
		}
	});
});
