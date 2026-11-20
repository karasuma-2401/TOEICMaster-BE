var prosemirror_dropcursor = require("prosemirror-dropcursor");
Object.keys(prosemirror_dropcursor).forEach(function(k) {
	if (k !== "default" && !Object.prototype.hasOwnProperty.call(exports, k)) Object.defineProperty(exports, k, {
		enumerable: true,
		get: function() {
			return prosemirror_dropcursor[k];
		}
	});
});
