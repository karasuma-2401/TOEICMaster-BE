var prosemirror_commands = require("prosemirror-commands");
Object.keys(prosemirror_commands).forEach(function(k) {
	if (k !== "default" && !Object.prototype.hasOwnProperty.call(exports, k)) Object.defineProperty(exports, k, {
		enumerable: true,
		get: function() {
			return prosemirror_commands[k];
		}
	});
});
