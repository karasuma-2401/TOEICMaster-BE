var prosemirror_gapcursor = require("prosemirror-gapcursor");
Object.keys(prosemirror_gapcursor).forEach(function(k) {
	if (k !== "default" && !Object.prototype.hasOwnProperty.call(exports, k)) Object.defineProperty(exports, k, {
		enumerable: true,
		get: function() {
			return prosemirror_gapcursor[k];
		}
	});
});
