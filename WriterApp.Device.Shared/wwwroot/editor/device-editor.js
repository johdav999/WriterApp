//#region \0rolldown/runtime.js
var e = Object.defineProperty, t = (t, n) => {
	let r = {};
	for (var i in t) e(r, i, {
		get: t[i],
		enumerable: !0
	});
	return n || e(r, Symbol.toStringTag, { value: "Module" }), r;
}, n = Object.defineProperty, r = (e, t) => {
	let r = {};
	for (var i in e) n(r, i, {
		get: e[i],
		enumerable: !0
	});
	return t || n(r, Symbol.toStringTag, { value: "Module" }), r;
};
//#endregion
//#region node_modules/orderedmap/dist/index.js
function i(e) {
	this.content = e;
}
i.prototype = {
	constructor: i,
	find: function(e) {
		for (var t = 0; t < this.content.length; t += 2) if (this.content[t] === e) return t;
		return -1;
	},
	get: function(e) {
		var t = this.find(e);
		return t == -1 ? void 0 : this.content[t + 1];
	},
	update: function(e, t, n) {
		var r = n && n != e ? this.remove(n) : this, a = r.find(e), o = r.content.slice();
		return a == -1 ? o.push(n || e, t) : (o[a + 1] = t, n && (o[a] = n)), new i(o);
	},
	remove: function(e) {
		var t = this.find(e);
		if (t == -1) return this;
		var n = this.content.slice();
		return n.splice(t, 2), new i(n);
	},
	addToStart: function(e, t) {
		return new i([e, t].concat(this.remove(e).content));
	},
	addToEnd: function(e, t) {
		var n = this.remove(e).content.slice();
		return n.push(e, t), new i(n);
	},
	addBefore: function(e, t, n) {
		var r = this.remove(t), a = r.content.slice(), o = r.find(e);
		return a.splice(o == -1 ? a.length : o, 0, t, n), new i(a);
	},
	forEach: function(e) {
		for (var t = 0; t < this.content.length; t += 2) e(this.content[t], this.content[t + 1]);
	},
	prepend: function(e) {
		return e = i.from(e), e.size ? new i(e.content.concat(this.subtract(e).content)) : this;
	},
	append: function(e) {
		return e = i.from(e), e.size ? new i(this.subtract(e).content.concat(e.content)) : this;
	},
	subtract: function(e) {
		var t = this;
		e = i.from(e);
		for (var n = 0; n < e.content.length; n += 2) t = t.remove(e.content[n]);
		return t;
	},
	toObject: function() {
		var e = {};
		return this.forEach(function(t, n) {
			e[t] = n;
		}), e;
	},
	get size() {
		return this.content.length >> 1;
	}
}, i.from = function(e) {
	if (e instanceof i) return e;
	var t = [];
	if (e) for (var n in e) t.push(n, e[n]);
	return new i(t);
};
//#endregion
//#region node_modules/prosemirror-model/dist/index.js
function a(e, t, n) {
	for (let r = 0;; r++) {
		if (r == e.childCount || r == t.childCount) return e.childCount == t.childCount ? null : n;
		let i = e.child(r), o = t.child(r);
		if (i == o) {
			n += i.nodeSize;
			continue;
		}
		if (!i.sameMarkup(o)) return n;
		if (i.isText && i.text != o.text) {
			let e = i.text, t = o.text, r = 0;
			for (; e[r] == t[r]; r++) n++;
			return r && r < e.length && r < t.length && c(e.charCodeAt(r - 1)) && s(e.charCodeAt(r)) && n--, n;
		}
		if (i.content.size || o.content.size) {
			let e = a(i.content, o.content, n + 1);
			if (e != null) return e;
		}
		n += i.nodeSize;
	}
}
function o(e, t, n, r) {
	for (let i = e.childCount, a = t.childCount;;) {
		if (i == 0 || a == 0) return i == a ? null : {
			a: n,
			b: r
		};
		let l = e.child(--i), u = t.child(--a), d = l.nodeSize;
		if (l == u) {
			n -= d, r -= d;
			continue;
		}
		if (!l.sameMarkup(u)) return {
			a: n,
			b: r
		};
		if (l.isText && l.text != u.text) {
			let e = l.text, t = u.text, i = e.length, a = t.length;
			for (; i > 0 && a > 0 && e[i - 1] == t[a - 1];) i--, a--, n--, r--;
			return i && a && i < e.length && c(e.charCodeAt(i - 1)) && s(e.charCodeAt(i)) && (n++, r++), {
				a: n,
				b: r
			};
		}
		if (l.content.size || u.content.size) {
			let e = o(l.content, u.content, n - 1, r - 1);
			if (e) return e;
		}
		n -= d, r -= d;
	}
}
function s(e) {
	return e >= 56320 && e < 57344;
}
function c(e) {
	return e >= 55296 && e < 56320;
}
var l = class e {
	constructor(e, t) {
		if (this.content = e, this.size = t || 0, t == null) for (let t = 0; t < e.length; t++) this.size += e[t].nodeSize;
	}
	nodesBetween(e, t, n, r = 0, i) {
		for (let a = 0, o = 0; o < t; a++) {
			let s = this.content[a], c = o + s.nodeSize;
			if (c > e && n(s, r + o, i || null, a) !== !1 && s.content.size) {
				let i = o + 1;
				s.nodesBetween(Math.max(0, e - i), Math.min(s.content.size, t - i), n, r + i);
			}
			o = c;
		}
	}
	descendants(e) {
		this.nodesBetween(0, this.size, e);
	}
	textBetween(e, t, n, r) {
		let i = "", a = !0;
		return this.nodesBetween(e, t, (o, s) => {
			let c = o.isText ? o.text.slice(Math.max(e, s) - s, t - s) : o.isLeaf ? r ? typeof r == "function" ? r(o) : r : o.type.spec.leafText ? o.type.spec.leafText(o) : "" : "";
			o.isBlock && (o.isLeaf && c || o.isTextblock) && n && (a ? a = !1 : i += n), i += c;
		}, 0), i;
	}
	append(t) {
		if (!t.size) return this;
		if (!this.size) return t;
		let n = this.lastChild, r = t.firstChild, i = this.content.slice(), a = 0;
		for (n.isText && n.sameMarkup(r) && (i[i.length - 1] = n.withText(n.text + r.text), a = 1); a < t.content.length; a++) i.push(t.content[a]);
		return new e(i, this.size + t.size);
	}
	cut(t, n = this.size) {
		if (t == 0 && n == this.size) return this;
		let r = [], i = 0;
		if (n > t) for (let e = 0, a = 0; a < n; e++) {
			let o = this.content[e], s = a + o.nodeSize;
			s > t && ((a < t || s > n) && (o = o.isText ? o.cut(Math.max(0, t - a), Math.min(o.text.length, n - a)) : o.cut(Math.max(0, t - a - 1), Math.min(o.content.size, n - a - 1))), r.push(o), i += o.nodeSize), a = s;
		}
		return new e(r, i);
	}
	cutByIndex(t, n) {
		return t == n ? e.empty : t == 0 && n == this.content.length ? this : new e(this.content.slice(t, n));
	}
	replaceChild(t, n) {
		let r = this.content[t];
		if (r == n) return this;
		let i = this.content.slice(), a = this.size + n.nodeSize - r.nodeSize;
		return i[t] = n, new e(i, a);
	}
	addToStart(t) {
		return new e([t].concat(this.content), this.size + t.nodeSize);
	}
	addToEnd(t) {
		return new e(this.content.concat(t), this.size + t.nodeSize);
	}
	eq(e) {
		if (this.content.length != e.content.length) return !1;
		for (let t = 0; t < this.content.length; t++) if (!this.content[t].eq(e.content[t])) return !1;
		return !0;
	}
	get firstChild() {
		return this.content.length ? this.content[0] : null;
	}
	get lastChild() {
		return this.content.length ? this.content[this.content.length - 1] : null;
	}
	get childCount() {
		return this.content.length;
	}
	child(e) {
		let t = this.content[e];
		if (!t) throw RangeError("Index " + e + " out of range for " + this);
		return t;
	}
	maybeChild(e) {
		return this.content[e] || null;
	}
	forEach(e) {
		for (let t = 0, n = 0; t < this.content.length; t++) {
			let r = this.content[t];
			e(r, n, t), n += r.nodeSize;
		}
	}
	findDiffStart(e, t = 0) {
		return a(this, e, t);
	}
	findDiffEnd(e, t = this.size, n = e.size) {
		return o(this, e, t, n);
	}
	findIndex(e) {
		if (e == 0) return d(0, e);
		if (e == this.size) return d(this.content.length, e);
		if (e > this.size || e < 0) throw RangeError(`Position ${e} outside of fragment (${this})`);
		for (let t = 0, n = 0;; t++) {
			let r = this.child(t), i = n + r.nodeSize;
			if (i >= e) return i == e ? d(t + 1, i) : d(t, n);
			n = i;
		}
	}
	toString() {
		return "<" + this.toStringInner() + ">";
	}
	toStringInner() {
		return this.content.join(", ");
	}
	toJSON() {
		return this.content.length ? this.content.map((e) => e.toJSON()) : null;
	}
	static fromJSON(t, n) {
		if (!n) return e.empty;
		if (!Array.isArray(n)) throw RangeError("Invalid input for Fragment.fromJSON");
		return e.fromArray(n.map(t.nodeFromJSON));
	}
	static fromArray(t) {
		if (!t.length) return e.empty;
		let n, r = 0;
		for (let e = 0; e < t.length; e++) {
			let i = t[e];
			r += i.nodeSize, e && i.isText && t[e - 1].sameMarkup(i) ? (n || (n = t.slice(0, e)), n[n.length - 1] = i.withText(n[n.length - 1].text + i.text)) : n && n.push(i);
		}
		return new e(n || t, r);
	}
	static from(t) {
		if (!t) return e.empty;
		if (t instanceof e) return t;
		if (Array.isArray(t)) return this.fromArray(t);
		if (t.attrs) return new e([t], t.nodeSize);
		throw RangeError("Can not convert " + t + " to a Fragment" + (t.nodesBetween ? " (looks like multiple versions of prosemirror-model were loaded)" : ""));
	}
};
l.empty = new l([], 0);
var u = {
	index: 0,
	offset: 0
};
function d(e, t) {
	return u.index = e, u.offset = t, u;
}
function f(e, t) {
	if (e === t) return !0;
	if (!(e && typeof e == "object") || !(t && typeof t == "object")) return !1;
	let n = Array.isArray(e);
	if (Array.isArray(t) != n) return !1;
	if (n) {
		if (e.length != t.length) return !1;
		for (let n = 0; n < e.length; n++) if (!f(e[n], t[n])) return !1;
	} else {
		for (let n in e) if (!(n in t) || !f(e[n], t[n])) return !1;
		for (let n in t) if (!(n in e)) return !1;
	}
	return !0;
}
var p = class e {
	constructor(e, t) {
		this.type = e, this.attrs = t;
	}
	addToSet(e) {
		let t, n = !1;
		for (let r = 0; r < e.length; r++) {
			let i = e[r];
			if (this.eq(i)) return e;
			if (this.type.excludes(i.type)) t || (t = e.slice(0, r));
			else if (i.type.excludes(this.type)) return e;
			else !n && i.type.rank > this.type.rank && (t || (t = e.slice(0, r)), t.push(this), n = !0), t && t.push(i);
		}
		return t || (t = e.slice()), n || t.push(this), t;
	}
	removeFromSet(e) {
		for (let t = 0; t < e.length; t++) if (this.eq(e[t])) return e.slice(0, t).concat(e.slice(t + 1));
		return e;
	}
	isInSet(e) {
		for (let t = 0; t < e.length; t++) if (this.eq(e[t])) return !0;
		return !1;
	}
	eq(e) {
		return this == e || this.type == e.type && f(this.attrs, e.attrs);
	}
	toJSON() {
		let e = { type: this.type.name };
		for (let t in this.attrs) {
			e.attrs = this.attrs;
			break;
		}
		return e;
	}
	static fromJSON(e, t) {
		if (!t) throw RangeError("Invalid input for Mark.fromJSON");
		let n = e.marks[t.type];
		if (!n) throw RangeError(`There is no mark type ${t.type} in this schema`);
		return n.create(t.attrs);
	}
	static sameSet(e, t) {
		if (e == t) return !0;
		if (e.length != t.length) return !1;
		for (let n = 0; n < e.length; n++) if (!e[n].eq(t[n])) return !1;
		return !0;
	}
	static setFrom(t) {
		if (!t || Array.isArray(t) && t.length == 0) return e.none;
		if (t instanceof e) return [t];
		let n = t.slice();
		return n.sort((e, t) => e.type.rank - t.type.rank), n;
	}
};
p.none = [];
var m = class extends Error {}, h = class e {
	constructor(e, t, n) {
		this.content = e, this.openStart = t, this.openEnd = n;
	}
	get size() {
		return this.content.size - this.openStart - this.openEnd;
	}
	insertAt(t, n) {
		let r = _(this.content, t + this.openStart, n, this.openStart + 1, this.openEnd + 1);
		return r && new e(r, this.openStart, this.openEnd);
	}
	removeBetween(t, n) {
		return new e(g(this.content, t + this.openStart, n + this.openStart), this.openStart, this.openEnd);
	}
	eq(e) {
		return this.content.eq(e.content) && this.openStart == e.openStart && this.openEnd == e.openEnd;
	}
	toString() {
		return this.content + "(" + this.openStart + "," + this.openEnd + ")";
	}
	toJSON() {
		if (!this.content.size) return null;
		let e = { content: this.content.toJSON() };
		return this.openStart > 0 && (e.openStart = this.openStart), this.openEnd > 0 && (e.openEnd = this.openEnd), e;
	}
	static fromJSON(t, n) {
		if (!n) return e.empty;
		let r = n.openStart || 0, i = n.openEnd || 0;
		if (typeof r != "number" || typeof i != "number") throw RangeError("Invalid input for Slice.fromJSON");
		return new e(l.fromJSON(t, n.content), r, i);
	}
	static maxOpen(t, n = !0) {
		let r = 0, i = 0;
		for (let e = t.firstChild; e && !e.isLeaf && (n || !e.type.spec.isolating); e = e.firstChild) r++;
		for (let e = t.lastChild; e && !e.isLeaf && (n || !e.type.spec.isolating); e = e.lastChild) i++;
		return new e(t, r, i);
	}
};
h.empty = new h(l.empty, 0, 0);
function g(e, t, n) {
	let { index: r, offset: i } = e.findIndex(t), a = e.maybeChild(r), { index: o, offset: s } = e.findIndex(n);
	if (i == t || a.isText) {
		if (s != n && !e.child(o).isText) throw RangeError("Removing non-flat range");
		return e.cut(0, t).append(e.cut(n));
	}
	if (r != o) throw RangeError("Removing non-flat range");
	return e.replaceChild(r, a.copy(g(a.content, t - i - 1, n - i - 1)));
}
function _(e, t, n, r, i, a) {
	let { index: o, offset: s } = e.findIndex(t), c = e.maybeChild(o);
	if (s == t || c.isText) return a && r <= 0 && i <= 0 && !a.canReplace(o, o, n) ? null : e.cut(0, t).append(n).append(e.cut(t));
	let l = _(c.content, t - s - 1, n, o == 0 ? r - 1 : 0, o == e.childCount - 1 ? i - 1 : 0, c);
	return l && e.replaceChild(o, c.copy(l));
}
function ee(e, t, n) {
	if (n.openStart > e.depth) throw new m("Inserted content deeper than insertion position");
	if (e.depth - n.openStart != t.depth - n.openEnd) throw new m("Inconsistent open depths");
	return v(e, t, n, 0);
}
function v(e, t, n, r) {
	let i = e.index(r), a = e.node(r);
	if (i == t.index(r) && r < e.depth - n.openStart) {
		let o = v(e, t, n, r + 1);
		return a.copy(a.content.replaceChild(i, o));
	}
	if (!n.content.size) return ne(a, ie(e, t, r));
	if (!n.openStart && !n.openEnd && e.depth == r && t.depth == r) {
		let r = e.parent, i = r.content;
		return ne(r, i.cut(0, e.parentOffset).append(n.content).append(i.cut(t.parentOffset)));
	}
	{
		let { start: i, end: o } = ae(n, e);
		return ne(a, re(e, i, o, t, r));
	}
}
function y(e, t) {
	if (!t.type.compatibleContent(e.type)) throw new m("Cannot join " + t.type.name + " onto " + e.type.name);
}
function b(e, t, n) {
	let r = e.node(n);
	return y(r, t.node(n)), r;
}
function x(e, t) {
	let n = t.length - 1;
	n >= 0 && e.isText && e.sameMarkup(t[n]) ? t[n] = e.withText(t[n].text + e.text) : t.push(e);
}
function te(e, t, n, r) {
	let i = (t || e).node(n), a = 0, o = t ? t.index(n) : i.childCount;
	e && (a = e.index(n), e.depth > n ? a++ : e.textOffset && (x(e.nodeAfter, r), a++));
	for (let e = a; e < o; e++) x(i.child(e), r);
	t && t.depth == n && t.textOffset && x(t.nodeBefore, r);
}
function ne(e, t) {
	if (!e.type.validContent(t)) throw new m("Invalid content for node " + e.type.name);
	return e.copy(t);
}
function re(e, t, n, r, i) {
	let a = e.depth > i && b(e, t, i + 1), o = r.depth > i && b(n, r, i + 1), s = [];
	return te(null, e, i, s), a && o && t.index(i) == n.index(i) ? (y(a, o), x(ne(a, re(e, t, n, r, i + 1)), s)) : (a && x(ne(a, ie(e, t, i + 1)), s), te(t, n, i, s), o && x(ne(o, ie(n, r, i + 1)), s)), te(r, null, i, s), new l(s);
}
function ie(e, t, n) {
	let r = [];
	return te(null, e, n, r), e.depth > n && x(ne(b(e, t, n + 1), ie(e, t, n + 1)), r), te(t, null, n, r), new l(r);
}
function ae(e, t) {
	let n = t.depth - e.openStart, r = t.node(n).copy(e.content);
	for (let e = n - 1; e >= 0; e--) r = t.node(e).copy(l.from(r));
	return {
		start: r.resolveNoCache(e.openStart + n),
		end: r.resolveNoCache(r.content.size - e.openEnd - n)
	};
}
var oe = class e {
	constructor(e, t, n) {
		this.pos = e, this.path = t, this.parentOffset = n, this.depth = t.length / 3 - 1;
	}
	resolveDepth(e) {
		return e == null ? this.depth : e < 0 ? this.depth + e : e;
	}
	get parent() {
		return this.node(this.depth);
	}
	get doc() {
		return this.node(0);
	}
	node(e) {
		return this.path[this.resolveDepth(e) * 3];
	}
	index(e) {
		return this.path[this.resolveDepth(e) * 3 + 1];
	}
	indexAfter(e) {
		return e = this.resolveDepth(e), this.index(e) + (e == this.depth && !this.textOffset ? 0 : 1);
	}
	start(e) {
		return e = this.resolveDepth(e), e == 0 ? 0 : this.path[e * 3 - 1] + 1;
	}
	end(e) {
		return e = this.resolveDepth(e), this.start(e) + this.node(e).content.size;
	}
	before(e) {
		if (e = this.resolveDepth(e), !e) throw RangeError("There is no position before the top-level node");
		return e == this.depth + 1 ? this.pos : this.path[e * 3 - 1];
	}
	after(e) {
		if (e = this.resolveDepth(e), !e) throw RangeError("There is no position after the top-level node");
		return e == this.depth + 1 ? this.pos : this.path[e * 3 - 1] + this.path[e * 3].nodeSize;
	}
	get textOffset() {
		return this.pos - this.path[this.path.length - 1];
	}
	get nodeAfter() {
		let e = this.parent, t = this.index(this.depth);
		if (t == e.childCount) return null;
		let n = this.pos - this.path[this.path.length - 1], r = e.child(t);
		return n ? e.child(t).cut(n) : r;
	}
	get nodeBefore() {
		let e = this.index(this.depth), t = this.pos - this.path[this.path.length - 1];
		return t ? this.parent.child(e).cut(0, t) : e == 0 ? null : this.parent.child(e - 1);
	}
	posAtIndex(e, t) {
		t = this.resolveDepth(t);
		let n = this.path[t * 3], r = t == 0 ? 0 : this.path[t * 3 - 1] + 1;
		for (let t = 0; t < e; t++) r += n.child(t).nodeSize;
		return r;
	}
	marks() {
		let e = this.parent, t = this.index();
		if (e.content.size == 0) return p.none;
		if (this.textOffset) return e.child(t).marks;
		let n = e.maybeChild(t - 1), r = e.maybeChild(t);
		if (!n) {
			let e = n;
			n = r, r = e;
		}
		let i = n.marks;
		for (var a = 0; a < i.length; a++) i[a].type.spec.inclusive === !1 && (!r || !i[a].isInSet(r.marks)) && (i = i[a--].removeFromSet(i));
		return i;
	}
	marksAcross(e) {
		let t = this.parent.maybeChild(this.index());
		if (!t || !t.isInline) return null;
		let n = t.marks, r = e.parent.maybeChild(e.index());
		for (var i = 0; i < n.length; i++) n[i].type.spec.inclusive === !1 && (!r || !n[i].isInSet(r.marks)) && (n = n[i--].removeFromSet(n));
		return n;
	}
	sharedDepth(e) {
		for (let t = this.depth; t > 0; t--) if (this.start(t) <= e && this.end(t) >= e) return t;
		return 0;
	}
	blockRange(e = this, t) {
		if (e.pos < this.pos) return e.blockRange(this);
		for (let n = this.depth - (this.parent.inlineContent || this.pos == e.pos ? 1 : 0); n >= 0; n--) if (e.pos <= this.end(n) && (!t || t(this.node(n)))) return new le(this, e, n);
		return null;
	}
	sameParent(e) {
		return this.pos - this.parentOffset == e.pos - e.parentOffset;
	}
	max(e) {
		return e.pos > this.pos ? e : this;
	}
	min(e) {
		return e.pos < this.pos ? e : this;
	}
	toString() {
		let e = "";
		for (let t = 1; t <= this.depth; t++) e += (e ? "/" : "") + this.node(t).type.name + "_" + this.index(t - 1);
		return e + ":" + this.parentOffset;
	}
	static resolve(t, n) {
		if (!(n >= 0 && n <= t.content.size)) throw RangeError("Position " + n + " out of range");
		let r = [], i = 0, a = n;
		for (let e = t;;) {
			let { index: t, offset: n } = e.content.findIndex(a), o = a - n;
			if (r.push(e, t, i + n), !o || (e = e.child(t), e.isText)) break;
			a = o - 1, i += n + 1;
		}
		return new e(n, r, a);
	}
	static resolveCached(t, n) {
		let r = S.get(t);
		if (r) for (let e = 0; e < r.elts.length; e++) {
			let t = r.elts[e];
			if (t.pos == n) return t;
		}
		else S.set(t, r = new se());
		let i = r.elts[r.i] = e.resolve(t, n);
		return r.i = (r.i + 1) % ce, i;
	}
}, se = class {
	constructor() {
		this.elts = [], this.i = 0;
	}
}, ce = 12, S = /* @__PURE__ */ new WeakMap(), le = class {
	constructor(e, t, n) {
		this.$from = e, this.$to = t, this.depth = n;
	}
	get start() {
		return this.$from.before(this.depth + 1);
	}
	get end() {
		return this.$to.after(this.depth + 1);
	}
	get parent() {
		return this.$from.node(this.depth);
	}
	get startIndex() {
		return this.$from.index(this.depth);
	}
	get endIndex() {
		return this.$to.indexAfter(this.depth);
	}
}, ue = Object.create(null), de = class e {
	constructor(e, t, n, r = p.none) {
		this.type = e, this.attrs = t, this.marks = r, this.content = n || l.empty;
	}
	get children() {
		return this.content.content;
	}
	get nodeSize() {
		return this.isLeaf ? 1 : 2 + this.content.size;
	}
	get childCount() {
		return this.content.childCount;
	}
	child(e) {
		return this.content.child(e);
	}
	maybeChild(e) {
		return this.content.maybeChild(e);
	}
	forEach(e) {
		this.content.forEach(e);
	}
	nodesBetween(e, t, n, r = 0) {
		this.content.nodesBetween(e, t, n, r, this);
	}
	descendants(e) {
		this.nodesBetween(0, this.content.size, e);
	}
	get textContent() {
		return this.isLeaf && this.type.spec.leafText ? this.type.spec.leafText(this) : this.textBetween(0, this.content.size, "");
	}
	textBetween(e, t, n, r) {
		return this.content.textBetween(e, t, n, r);
	}
	get firstChild() {
		return this.content.firstChild;
	}
	get lastChild() {
		return this.content.lastChild;
	}
	eq(e) {
		return this == e || this.sameMarkup(e) && this.content.eq(e.content);
	}
	sameMarkup(e) {
		return this.hasMarkup(e.type, e.attrs, e.marks);
	}
	hasMarkup(e, t, n) {
		return this.type == e && f(this.attrs, t || e.defaultAttrs || ue) && p.sameSet(this.marks, n || p.none);
	}
	copy(t = null) {
		return t == this.content ? this : new e(this.type, this.attrs, t, this.marks);
	}
	mark(t) {
		return t == this.marks ? this : new e(this.type, this.attrs, this.content, t);
	}
	cut(e, t = this.content.size) {
		return e == 0 && t == this.content.size ? this : this.copy(this.content.cut(e, t));
	}
	slice(e, t = this.content.size, n = !1) {
		if (e == t) return h.empty;
		let r = this.resolve(e), i = this.resolve(t), a = n ? 0 : r.sharedDepth(t), o = r.start(a);
		return new h(r.node(a).content.cut(r.pos - o, i.pos - o), r.depth - a, i.depth - a);
	}
	replace(e, t, n) {
		return ee(this.resolve(e), this.resolve(t), n);
	}
	nodeAt(e) {
		for (let t = this;;) {
			let { index: n, offset: r } = t.content.findIndex(e);
			if (t = t.maybeChild(n), !t) return null;
			if (r == e || t.isText) return t;
			e -= r + 1;
		}
	}
	childAfter(e) {
		let { index: t, offset: n } = this.content.findIndex(e);
		return {
			node: this.content.maybeChild(t),
			index: t,
			offset: n
		};
	}
	childBefore(e) {
		if (e == 0) return {
			node: null,
			index: 0,
			offset: 0
		};
		let { index: t, offset: n } = this.content.findIndex(e);
		if (n < e) return {
			node: this.content.child(t),
			index: t,
			offset: n
		};
		let r = this.content.child(t - 1);
		return {
			node: r,
			index: t - 1,
			offset: n - r.nodeSize
		};
	}
	resolve(e) {
		return oe.resolveCached(this, e);
	}
	resolveNoCache(e) {
		return oe.resolve(this, e);
	}
	rangeHasMark(e, t, n) {
		let r = !1;
		return t > e && this.nodesBetween(e, t, (e) => (n.isInSet(e.marks) && (r = !0), !r)), r;
	}
	get isBlock() {
		return this.type.isBlock;
	}
	get isTextblock() {
		return this.type.isTextblock;
	}
	get inlineContent() {
		return this.type.inlineContent;
	}
	get isInline() {
		return this.type.isInline;
	}
	get isText() {
		return this.type.isText;
	}
	get isLeaf() {
		return this.type.isLeaf;
	}
	get isAtom() {
		return this.type.isAtom;
	}
	toString() {
		if (this.type.spec.toDebugString) return this.type.spec.toDebugString(this);
		let e = this.type.name;
		return this.content.size && (e += "(" + this.content.toStringInner() + ")"), pe(this.marks, e);
	}
	contentMatchAt(e) {
		let t = this.type.contentMatch.matchFragment(this.content, 0, e);
		if (!t) throw Error("Called contentMatchAt on a node with invalid content");
		return t;
	}
	canReplace(e, t, n = l.empty, r = 0, i = n.childCount) {
		let a = this.contentMatchAt(e).matchFragment(n, r, i), o = a && a.matchFragment(this.content, t);
		if (!o || !o.validEnd) return !1;
		for (let e = r; e < i; e++) if (!this.type.allowsMarks(n.child(e).marks)) return !1;
		return !0;
	}
	canReplaceWith(e, t, n, r) {
		if (r && !this.type.allowsMarks(r)) return !1;
		let i = this.contentMatchAt(e).matchType(n), a = i && i.matchFragment(this.content, t);
		return a ? a.validEnd : !1;
	}
	canAppend(e) {
		return e.content.size ? this.canReplace(this.childCount, this.childCount, e.content) : this.type.compatibleContent(e.type);
	}
	check() {
		this.type.checkContent(this.content), me(this.type.attrs, this.attrs, "node", this.type.name);
		let e = p.none;
		for (let t = 0; t < this.marks.length; t++) {
			let n = this.marks[t];
			me(n.type.attrs, n.attrs, "mark", n.type.name), e = n.addToSet(e);
		}
		if (!p.sameSet(e, this.marks)) throw RangeError(`Invalid collection of marks for node ${this.type.name}: ${this.marks.map((e) => e.type.name)}`);
		this.content.forEach((e) => e.check());
	}
	toJSON() {
		let e = { type: this.type.name };
		for (let t in this.attrs) {
			e.attrs = this.attrs;
			break;
		}
		return this.content.size && (e.content = this.content.toJSON()), this.marks.length && (e.marks = this.marks.map((e) => e.toJSON())), e;
	}
	static fromJSON(e, t) {
		if (!t) throw RangeError("Invalid input for Node.fromJSON");
		let n;
		if (t.marks) {
			if (!Array.isArray(t.marks)) throw RangeError("Invalid mark data for Node.fromJSON");
			n = t.marks.map(e.markFromJSON);
		}
		if (t.type == "text") {
			if (typeof t.text != "string") throw RangeError("Invalid text node in JSON");
			return e.text(t.text, n);
		}
		let r = l.fromJSON(e, t.content);
		return e.nodeType(t.type).create(t.attrs, r, n);
	}
};
de.prototype.text = void 0;
var fe = class e extends de {
	constructor(e, t, n, r) {
		if (super(e, t, null, r), !n) throw RangeError("Empty text nodes are not allowed");
		this.text = n;
	}
	toString() {
		return this.type.spec.toDebugString ? this.type.spec.toDebugString(this) : pe(this.marks, JSON.stringify(this.text));
	}
	get textContent() {
		return this.text;
	}
	textBetween(e, t) {
		return this.text.slice(e, t);
	}
	get nodeSize() {
		return this.text.length;
	}
	mark(t) {
		return t == this.marks ? this : new e(this.type, this.attrs, this.text, t);
	}
	withText(t) {
		return t == this.text ? this : new e(this.type, this.attrs, t, this.marks);
	}
	cut(e = 0, t = this.text.length) {
		return e == 0 && t == this.text.length ? this : this.withText(this.text.slice(e, t));
	}
	eq(e) {
		return this.sameMarkup(e) && this.text == e.text;
	}
	toJSON() {
		let e = super.toJSON();
		return e.text = this.text, e;
	}
};
function pe(e, t) {
	for (let n = e.length - 1; n >= 0; n--) t = e[n].type.name + "(" + t + ")";
	return t;
}
function me(e, t, n, r) {
	for (let i in t) if (!(i in e)) throw RangeError(`Unsupported attribute ${i} for ${n} of type ${r}`);
	for (let n in e) e[n].validate && e[n].validate(t[n]);
}
var he = class e {
	constructor(e) {
		this.validEnd = e, this.next = [], this.wrapCache = [];
	}
	static parse(t, n) {
		let r = new ge(t, n);
		if (r.next == null) return e.empty;
		let i = _e(r);
		r.next && r.err("Unexpected trailing text");
		let a = De(we(i));
		return Oe(a, r), a;
	}
	matchType(e) {
		for (let t = 0; t < this.next.length; t++) if (this.next[t].type == e) return this.next[t].next;
		return null;
	}
	matchFragment(e, t = 0, n = e.childCount) {
		let r = this;
		for (let i = t; r && i < n; i++) r = r.matchType(e.child(i).type);
		return r;
	}
	get inlineContent() {
		return this.next.length != 0 && this.next[0].type.isInline;
	}
	get defaultType() {
		for (let e = 0; e < this.next.length; e++) {
			let { type: t } = this.next[e];
			if (!(t.isText || t.hasRequiredAttrs())) return t;
		}
		return null;
	}
	compatible(e) {
		for (let t = 0; t < this.next.length; t++) for (let n = 0; n < e.next.length; n++) if (this.next[t].type == e.next[n].type) return !0;
		return !1;
	}
	fillBefore(e, t = !1, n = 0) {
		let r = [this];
		function i(a, o) {
			let s = a.matchFragment(e, n);
			if (s && (!t || s.validEnd)) return l.from(o.map((e) => e.createAndFill()));
			for (let e = 0; e < a.next.length; e++) {
				let { type: t, next: n } = a.next[e];
				if (!(t.isText || t.hasRequiredAttrs()) && r.indexOf(n) == -1) {
					r.push(n);
					let e = i(n, o.concat(t));
					if (e) return e;
				}
			}
			return null;
		}
		return i(this, []);
	}
	findWrapping(e) {
		for (let t = 0; t < this.wrapCache.length; t += 2) if (this.wrapCache[t] == e) return this.wrapCache[t + 1];
		let t = this.computeWrapping(e);
		return this.wrapCache.push(e, t), t;
	}
	computeWrapping(e) {
		let t = Object.create(null), n = [{
			match: this,
			type: null,
			via: null
		}];
		for (; n.length;) {
			let r = n.shift(), i = r.match;
			if (i.matchType(e)) {
				let e = [];
				for (let t = r; t.type; t = t.via) e.push(t.type);
				return e.reverse();
			}
			for (let e = 0; e < i.next.length; e++) {
				let { type: a, next: o } = i.next[e];
				!a.isLeaf && !a.hasRequiredAttrs() && !(a.name in t) && (!r.type || o.validEnd) && (n.push({
					match: a.contentMatch,
					type: a,
					via: r
				}), t[a.name] = !0);
			}
		}
		return null;
	}
	get edgeCount() {
		return this.next.length;
	}
	edge(e) {
		if (e >= this.next.length) throw RangeError(`There's no ${e}th edge in this content match`);
		return this.next[e];
	}
	toString() {
		let e = [];
		function t(n) {
			e.push(n);
			for (let r = 0; r < n.next.length; r++) e.indexOf(n.next[r].next) == -1 && t(n.next[r].next);
		}
		return t(this), e.map((t, n) => {
			let r = n + (t.validEnd ? "*" : " ") + " ";
			for (let n = 0; n < t.next.length; n++) r += (n ? ", " : "") + t.next[n].type.name + "->" + e.indexOf(t.next[n].next);
			return r;
		}).join("\n");
	}
};
he.empty = new he(!0);
var ge = class {
	constructor(e, t) {
		this.string = e, this.nodeTypes = t, this.inline = null, this.pos = 0, this.tokens = e.split(/\s*(?=\b|\W|$)/), this.tokens[this.tokens.length - 1] == "" && this.tokens.pop(), this.tokens[0] == "" && this.tokens.shift();
	}
	get next() {
		return this.tokens[this.pos];
	}
	eat(e) {
		return this.next == e && (this.pos++ || !0);
	}
	err(e) {
		throw SyntaxError(e + " (in content expression '" + this.string + "')");
	}
};
function _e(e) {
	let t = [];
	do
		t.push(ve(e));
	while (e.eat("|"));
	return t.length == 1 ? t[0] : {
		type: "choice",
		exprs: t
	};
}
function ve(e) {
	let t = [];
	do
		t.push(ye(e));
	while (e.next && e.next != ")" && e.next != "|");
	return t.length == 1 ? t[0] : {
		type: "seq",
		exprs: t
	};
}
function ye(e) {
	let t = Ce(e);
	for (;;) if (e.eat("+")) t = {
		type: "plus",
		expr: t
	};
	else if (e.eat("*")) t = {
		type: "star",
		expr: t
	};
	else if (e.eat("?")) t = {
		type: "opt",
		expr: t
	};
	else if (e.eat("{")) t = xe(e, t);
	else break;
	return t;
}
function be(e) {
	/\D/.test(e.next) && e.err("Expected number, got '" + e.next + "'");
	let t = Number(e.next);
	return e.pos++, t;
}
function xe(e, t) {
	let n = be(e), r = n;
	return e.eat(",") && (r = e.next == "}" ? -1 : be(e)), e.eat("}") || e.err("Unclosed braced range"), {
		type: "range",
		min: n,
		max: r,
		expr: t
	};
}
function Se(e, t) {
	let n = e.nodeTypes, r = n[t];
	if (r) return [r];
	let i = [];
	for (let e in n) {
		let r = n[e];
		r.isInGroup(t) && i.push(r);
	}
	return i.length == 0 && e.err("No node type or group '" + t + "' found"), i;
}
function Ce(e) {
	if (e.eat("(")) {
		let t = _e(e);
		return e.eat(")") || e.err("Missing closing paren"), t;
	}
	if (/\W/.test(e.next)) e.err("Unexpected token '" + e.next + "'");
	else {
		let t = Se(e, e.next).map((t) => (e.inline == null ? e.inline = t.isInline : e.inline != t.isInline && e.err("Mixing inline and block content"), {
			type: "name",
			value: t
		}));
		return e.pos++, t.length == 1 ? t[0] : {
			type: "choice",
			exprs: t
		};
	}
}
function we(e) {
	let t = [[]];
	return i(a(e, 0), n()), t;
	function n() {
		return t.push([]) - 1;
	}
	function r(e, n, r) {
		let i = {
			term: r,
			to: n
		};
		return t[e].push(i), i;
	}
	function i(e, t) {
		e.forEach((e) => e.to = t);
	}
	function a(e, t) {
		if (e.type == "choice") return e.exprs.reduce((e, n) => e.concat(a(n, t)), []);
		if (e.type == "seq") for (let r = 0;; r++) {
			let o = a(e.exprs[r], t);
			if (r == e.exprs.length - 1) return o;
			i(o, t = n());
		}
		else if (e.type == "star") {
			let o = n();
			return r(t, o), i(a(e.expr, o), o), [r(o)];
		} else if (e.type == "plus") {
			let o = n();
			return i(a(e.expr, t), o), i(a(e.expr, o), o), [r(o)];
		} else if (e.type == "opt") return [r(t)].concat(a(e.expr, t));
		else if (e.type == "range") {
			let o = t;
			for (let t = 0; t < e.min; t++) {
				let t = n();
				i(a(e.expr, o), t), o = t;
			}
			if (e.max == -1) i(a(e.expr, o), o);
			else for (let t = e.min; t < e.max; t++) {
				let t = n();
				r(o, t), i(a(e.expr, o), t), o = t;
			}
			return [r(o)];
		} else if (e.type == "name") return [r(t, void 0, e.value)];
		else throw Error("Unknown expr type");
	}
}
function Te(e, t) {
	return t - e;
}
function Ee(e, t) {
	let n = [];
	return r(t), n.sort(Te);
	function r(t) {
		let i = e[t];
		if (i.length == 1 && !i[0].term) return r(i[0].to);
		n.push(t);
		for (let e = 0; e < i.length; e++) {
			let { term: t, to: a } = i[e];
			!t && n.indexOf(a) == -1 && r(a);
		}
	}
}
function De(e) {
	let t = Object.create(null);
	return n(Ee(e, 0));
	function n(r) {
		let i = [];
		r.forEach((t) => {
			e[t].forEach(({ term: t, to: n }) => {
				if (!t) return;
				let r;
				for (let e = 0; e < i.length; e++) i[e][0] == t && (r = i[e][1]);
				Ee(e, n).forEach((e) => {
					r || i.push([t, r = []]), r.indexOf(e) == -1 && r.push(e);
				});
			});
		});
		let a = t[r.join(",")] = new he(r.indexOf(e.length - 1) > -1);
		for (let e = 0; e < i.length; e++) {
			let r = i[e][1].sort(Te);
			a.next.push({
				type: i[e][0],
				next: t[r.join(",")] || n(r)
			});
		}
		return a;
	}
}
function Oe(e, t) {
	for (let n = 0, r = [e]; n < r.length; n++) {
		let e = r[n], i = !e.validEnd, a = [];
		for (let t = 0; t < e.next.length; t++) {
			let { type: n, next: o } = e.next[t];
			a.push(n.name), i && !(n.isText || n.hasRequiredAttrs()) && (i = !1), r.indexOf(o) == -1 && r.push(o);
		}
		i && t.err("Only non-generatable nodes (" + a.join(", ") + ") in a required position (see https://prosemirror.net/docs/guide/#generatable)");
	}
}
function ke(e) {
	let t = Object.create(null);
	for (let n in e) {
		let r = e[n];
		if (!r.hasDefault) return null;
		t[n] = r.default;
	}
	return t;
}
function Ae(e, t) {
	let n = Object.create(null);
	for (let r in e) {
		let i = e[r], a = t && t[r];
		if (a === void 0) {
			if (i.hasDefault) a = i.default;
			else throw RangeError("No value supplied for attribute " + r);
		} else i.validate && i.validate(a);
		n[r] = a;
	}
	return n;
}
function je(e, t) {
	let n = Object.create(null);
	if (t) for (let r in t) n[r] = new Pe(e, r, t[r]);
	return n;
}
var Me = class e {
	constructor(e, t, n) {
		this.name = e, this.schema = t, this.spec = n, this.markSet = null, this.groups = n.group ? n.group.split(" ") : [], this.attrs = je(e, n.attrs), this.defaultAttrs = ke(this.attrs), this.contentMatch = null, this.inlineContent = null, this.isBlock = !(n.inline || e == "text"), this.isText = e == "text";
	}
	get isInline() {
		return !this.isBlock;
	}
	get isTextblock() {
		return this.isBlock && this.inlineContent;
	}
	get isLeaf() {
		return this.contentMatch == he.empty;
	}
	get isAtom() {
		return this.isLeaf || !!this.spec.atom;
	}
	isInGroup(e) {
		return this.groups.indexOf(e) > -1;
	}
	get whitespace() {
		return this.spec.whitespace || (this.spec.code ? "pre" : "normal");
	}
	hasRequiredAttrs() {
		for (let e in this.attrs) if (this.attrs[e].isRequired) return !0;
		return !1;
	}
	compatibleContent(e) {
		return this == e || this.contentMatch.compatible(e.contentMatch);
	}
	computeAttrs(e) {
		return !e && this.defaultAttrs ? this.defaultAttrs : Ae(this.attrs, e);
	}
	create(e = null, t, n) {
		if (this.isText) throw Error("NodeType.create can't construct text nodes");
		return new de(this, this.computeAttrs(e), l.from(t), p.setFrom(n));
	}
	createChecked(e = null, t, n) {
		return t = l.from(t), this.checkContent(t), new de(this, this.computeAttrs(e), t, p.setFrom(n));
	}
	createAndFill(e = null, t, n) {
		if (e = this.computeAttrs(e), t = l.from(t), t.size) {
			let e = this.contentMatch.fillBefore(t);
			if (!e) return null;
			t = e.append(t);
		}
		let r = this.contentMatch.matchFragment(t), i = r && r.fillBefore(l.empty, !0);
		return i ? new de(this, e, t.append(i), p.setFrom(n)) : null;
	}
	validContent(e) {
		let t = this.contentMatch.matchFragment(e);
		if (!t || !t.validEnd) return !1;
		for (let t = 0; t < e.childCount; t++) if (!this.allowsMarks(e.child(t).marks)) return !1;
		return !0;
	}
	checkContent(e) {
		if (!this.validContent(e)) throw RangeError(`Invalid content for node ${this.name}: ${e.toString().slice(0, 50)}`);
	}
	checkAttrs(e) {}
	allowsMarkType(e) {
		return this.markSet == null || this.markSet.indexOf(e) > -1;
	}
	allowsMarks(e) {
		if (this.markSet == null) return !0;
		for (let t = 0; t < e.length; t++) if (!this.allowsMarkType(e[t].type)) return !1;
		return !0;
	}
	allowedMarks(e) {
		if (this.markSet == null) return e;
		let t;
		for (let n = 0; n < e.length; n++) this.allowsMarkType(e[n].type) ? t && t.push(e[n]) : t || (t = e.slice(0, n));
		return t ? t.length ? t : p.none : e;
	}
	static compile(t, n) {
		let r = Object.create(null);
		t.forEach((t, i) => r[t] = new e(t, n, i));
		let i = n.spec.topNode || "doc";
		if (!r[i]) throw RangeError("Schema is missing its top node type ('" + i + "')");
		if (!r.text) throw RangeError("Every schema needs a 'text' type");
		for (let e in r.text.attrs) throw RangeError("The text node type should not have attributes");
		return r;
	}
};
function Ne(e, t, n) {
	let r = n.split("|");
	return (n) => {
		let i = n === null ? "null" : typeof n;
		if (r.indexOf(i) < 0) throw RangeError(`Expected value of type ${r} for attribute ${t} on type ${e}, got ${i}`);
	};
}
var Pe = class {
	constructor(e, t, n) {
		this.hasDefault = Object.prototype.hasOwnProperty.call(n, "default"), this.default = n.default, this.validate = typeof n.validate == "string" ? Ne(e, t, n.validate) : n.validate;
	}
	get isRequired() {
		return !this.hasDefault;
	}
}, Fe = class e {
	constructor(e, t, n, r) {
		this.name = e, this.rank = t, this.schema = n, this.spec = r, this.attrs = je(e, r.attrs), this.excluded = null;
		let i = ke(this.attrs);
		this.instance = i ? new p(this, i) : null;
	}
	create(e = null) {
		return !e && this.instance ? this.instance : new p(this, Ae(this.attrs, e));
	}
	static compile(t, n) {
		let r = Object.create(null), i = 0;
		return t.forEach((t, a) => r[t] = new e(t, i++, n, a)), r;
	}
	removeFromSet(e) {
		for (var t = 0; t < e.length; t++) e[t].type == this && (e = e.slice(0, t).concat(e.slice(t + 1)), t--);
		return e;
	}
	isInSet(e) {
		for (let t = 0; t < e.length; t++) if (e[t].type == this) return e[t];
	}
	excludes(e) {
		return this.excluded.indexOf(e) > -1;
	}
}, Ie = class {
	constructor(e) {
		this.linebreakReplacement = null, this.cached = Object.create(null);
		let t = this.spec = {};
		for (let n in e) t[n] = e[n];
		t.nodes = i.from(e.nodes), t.marks = i.from(e.marks || {}), this.nodes = Me.compile(this.spec.nodes, this), this.marks = Fe.compile(this.spec.marks, this);
		let n = Object.create(null);
		for (let e in this.nodes) {
			if (e in this.marks) throw RangeError(e + " can not be both a node and a mark");
			let t = this.nodes[e], r = t.spec.content || "", i = t.spec.marks;
			if (t.contentMatch = n[r] || (n[r] = he.parse(r, this.nodes)), t.inlineContent = t.contentMatch.inlineContent, t.spec.linebreakReplacement) {
				if (this.linebreakReplacement) throw RangeError("Multiple linebreak nodes defined");
				if (!t.isInline || !t.isLeaf) throw RangeError("Linebreak replacement nodes must be inline leaf nodes");
				this.linebreakReplacement = t;
			}
			t.markSet = i == "_" ? null : i ? Le(this, i.split(" ")) : i == "" || !t.inlineContent ? [] : null;
		}
		for (let e in this.marks) {
			let t = this.marks[e], n = t.spec.excludes;
			t.excluded = n == null ? [t] : n == "" ? [] : Le(this, n.split(" "));
		}
		this.nodeFromJSON = (e) => de.fromJSON(this, e), this.markFromJSON = (e) => p.fromJSON(this, e), this.topNodeType = this.nodes[this.spec.topNode || "doc"], this.cached.wrappings = Object.create(null);
	}
	node(e, t = null, n, r) {
		if (typeof e == "string") e = this.nodeType(e);
		else if (!(e instanceof Me)) throw RangeError("Invalid node type: " + e);
		else if (e.schema != this) throw RangeError("Node type from different schema used (" + e.name + ")");
		return e.createChecked(t, n, r);
	}
	text(e, t) {
		let n = this.nodes.text;
		return new fe(n, n.defaultAttrs, e, p.setFrom(t));
	}
	mark(e, t) {
		return typeof e == "string" && (e = this.marks[e]), e.create(t);
	}
	nodeType(e) {
		let t = this.nodes[e];
		if (!t) throw RangeError("Unknown node type: " + e);
		return t;
	}
};
function Le(e, t) {
	let n = [];
	for (let r = 0; r < t.length; r++) {
		let i = t[r], a = e.marks[i], o = a;
		if (a) n.push(a);
		else for (let t in e.marks) {
			let r = e.marks[t];
			(i == "_" || r.spec.group && r.spec.group.split(" ").indexOf(i) > -1) && n.push(o = r);
		}
		if (!o) throw SyntaxError("Unknown mark type: '" + t[r] + "'");
	}
	return n;
}
function Re(e) {
	return e.tag != null;
}
function ze(e) {
	return e.style != null;
}
var Be = class e {
	constructor(e, t) {
		this.schema = e, this.rules = t, this.tags = [], this.styles = [];
		let n = this.matchedStyles = [];
		t.forEach((e) => {
			if (Re(e)) this.tags.push(e);
			else if (ze(e)) {
				let t = /[^=]*/.exec(e.style)[0];
				n.indexOf(t) < 0 && n.push(t), this.styles.push(e);
			}
		}), this.normalizeLists = !this.tags.some((t) => {
			if (!/^(ul|ol)\b/.test(t.tag) || !t.node) return !1;
			let n = e.nodes[t.node];
			return n.contentMatch.matchType(n);
		});
	}
	parse(e, t = {}) {
		let n = new Ye(this, t, !1);
		return n.addAll(e, p.none, t.from, t.to), n.finish();
	}
	parseSlice(e, t = {}) {
		let n = new Ye(this, t, !0);
		return n.addAll(e, p.none, t.from, t.to), h.maxOpen(n.finish());
	}
	matchTag(e, t, n) {
		for (let r = n ? this.tags.indexOf(n) + 1 : 0; r < this.tags.length; r++) {
			let n = this.tags[r];
			if (Ze(e, n.tag) && (n.namespace === void 0 || e.namespaceURI == n.namespace) && (!n.context || t.matchesContext(n.context))) {
				if (n.getAttrs) {
					let t = n.getAttrs(e);
					if (t === !1) continue;
					n.attrs = t || void 0;
				}
				return n;
			}
		}
	}
	matchStyle(e, t, n, r) {
		for (let i = r ? this.styles.indexOf(r) + 1 : 0; i < this.styles.length; i++) {
			let r = this.styles[i], a = r.style;
			if (!(a.indexOf(e) != 0 || r.context && !n.matchesContext(r.context) || a.length > e.length && (a.charCodeAt(e.length) != 61 || a.slice(e.length + 1) != t))) {
				if (r.getAttrs) {
					let e = r.getAttrs(t);
					if (e === !1) continue;
					r.attrs = e || void 0;
				}
				return r;
			}
		}
	}
	static schemaRules(e) {
		let t = [];
		function n(e) {
			let n = e.priority == null ? 50 : e.priority, r = 0;
			for (; r < t.length; r++) {
				let e = t[r];
				if ((e.priority == null ? 50 : e.priority) < n) break;
			}
			t.splice(r, 0, e);
		}
		for (let t in e.marks) {
			let r = e.marks[t].spec.parseDOM;
			r && r.forEach((e) => {
				n(e = Qe(e)), e.mark || e.ignore || e.clearMark || (e.mark = t);
			});
		}
		for (let t in e.nodes) {
			let r = e.nodes[t].spec.parseDOM;
			r && r.forEach((e) => {
				n(e = Qe(e)), e.node || e.ignore || e.mark || (e.node = t);
			});
		}
		return t;
	}
	static fromSchema(t) {
		return t.cached.domParser || (t.cached.domParser = new e(t, e.schemaRules(t)));
	}
}, Ve = {
	address: !0,
	article: !0,
	aside: !0,
	blockquote: !0,
	body: !0,
	canvas: !0,
	dd: !0,
	div: !0,
	dl: !0,
	fieldset: !0,
	figcaption: !0,
	figure: !0,
	footer: !0,
	form: !0,
	h1: !0,
	h2: !0,
	h3: !0,
	h4: !0,
	h5: !0,
	h6: !0,
	header: !0,
	hgroup: !0,
	hr: !0,
	li: !0,
	noscript: !0,
	ol: !0,
	output: !0,
	p: !0,
	pre: !0,
	section: !0,
	table: !0,
	tfoot: !0,
	ul: !0
}, He = {
	head: !0,
	noscript: !0,
	object: !0,
	script: !0,
	style: !0,
	title: !0
}, Ue = {
	ol: !0,
	ul: !0
}, We = 1, Ge = 2, Ke = 4;
function qe(e, t, n) {
	return t == null ? e && e.whitespace == "pre" ? 3 : n & -5 : (t ? We : 0) | (t === "full" ? Ge : 0);
}
var Je = class {
	constructor(e, t, n, r, i, a) {
		this.type = e, this.attrs = t, this.marks = n, this.solid = r, this.options = a, this.content = [], this.activeMarks = p.none, this.match = i || (a & Ke ? null : e.contentMatch);
	}
	findWrapping(e) {
		if (!this.match) {
			if (!this.type) return [];
			let t = this.type.contentMatch.fillBefore(l.from(e));
			if (t) this.match = this.type.contentMatch.matchFragment(t);
			else {
				let t = this.type.contentMatch, n;
				return (n = t.findWrapping(e.type)) ? (this.match = t, n) : null;
			}
		}
		return this.match.findWrapping(e.type);
	}
	finish(e) {
		if (!(this.options & We)) {
			let e = this.content[this.content.length - 1], t;
			if (e && e.isText && (t = /[ \t\r\n\u000c]+$/.exec(e.text))) {
				let n = e;
				e.text.length == t[0].length ? this.content.pop() : this.content[this.content.length - 1] = n.withText(n.text.slice(0, n.text.length - t[0].length));
			}
		}
		let t = l.from(this.content);
		return !e && this.match && (t = t.append(this.match.fillBefore(l.empty, !0))), this.type ? this.type.create(this.attrs, t, this.marks) : t;
	}
	inlineContext(e) {
		return this.type ? this.type.inlineContent : this.content.length ? this.content[0].isInline : e.parentNode && !Ve.hasOwnProperty(e.parentNode.nodeName.toLowerCase());
	}
}, Ye = class {
	constructor(e, t, n) {
		this.parser = e, this.options = t, this.isOpen = n, this.open = 0, this.localPreserveWS = !1;
		let r = t.topNode, i, a = qe(null, t.preserveWhitespace, 0) | (n ? Ke : 0);
		i = r ? new Je(r.type, r.attrs, p.none, !0, t.topMatch || r.type.contentMatch, a) : n ? new Je(null, null, p.none, !0, null, a) : new Je(e.schema.topNodeType, null, p.none, !0, null, a), this.nodes = [i], this.find = t.findPositions, this.needsBlock = !1;
	}
	get top() {
		return this.nodes[this.open];
	}
	addDOM(e, t) {
		e.nodeType == 3 ? this.addTextNode(e, t) : e.nodeType == 1 && this.addElement(e, t);
	}
	addTextNode(e, t) {
		let n = e.nodeValue, r = this.top, i = r.options & Ge ? "full" : this.localPreserveWS || (r.options & We) > 0, { schema: a } = this.parser;
		if (i === "full" || r.inlineContext(e) || /[^ \t\r\n\u000c]/.test(n)) {
			if (!i) {
				if (n = n.replace(/[ \t\r\n\u000c]+/g, " "), /^[ \t\r\n\u000c]/.test(n) && this.open == this.nodes.length - 1) {
					let t = r.content[r.content.length - 1], i = e.previousSibling;
					(!t || i && i.nodeName == "BR" || t.isText && /[ \t\r\n\u000c]$/.test(t.text)) && (n = n.slice(1));
				}
			} else if (i === "full") n = n.replace(/\r\n?/g, "\n");
			else if (a.linebreakReplacement && /[\r\n]/.test(n) && this.top.findWrapping(a.linebreakReplacement.create())) {
				let e = n.split(/\r?\n|\r/);
				for (let n = 0; n < e.length; n++) n && this.insertNode(a.linebreakReplacement.create(), t, !0), e[n] && this.insertNode(a.text(e[n]), t, !/\S/.test(e[n]));
				n = "";
			} else n = n.replace(/\r?\n|\r/g, " ");
			n && this.insertNode(a.text(n), t, !/\S/.test(n)), this.findInText(e);
		} else this.findInside(e);
	}
	addElement(e, t, n) {
		let r = this.localPreserveWS, i = this.top;
		(e.tagName == "PRE" || /pre/.test(e.style && e.style.whiteSpace)) && (this.localPreserveWS = !0);
		let a = e.nodeName.toLowerCase(), o;
		Ue.hasOwnProperty(a) && this.parser.normalizeLists && Xe(e);
		let s = this.options.ruleFromNode && this.options.ruleFromNode(e) || (o = this.parser.matchTag(e, this, n));
		out: if (s ? s.ignore : He.hasOwnProperty(a)) this.findInside(e), this.ignoreFallback(e, t);
		else if (!s || s.skip || s.closeParent) {
			s && s.closeParent ? this.open = Math.max(0, this.open - 1) : s && s.skip.nodeType && (e = s.skip);
			let n, r = this.needsBlock;
			if (Ve.hasOwnProperty(a)) i.content.length && i.content[0].isInline && this.open && (this.open--, i = this.top), n = !0, i.type || (this.needsBlock = !0);
			else if (!e.firstChild) {
				this.leafFallback(e, t);
				break out;
			}
			let o = s && s.skip ? t : this.readStyles(e, t);
			o && this.addAll(e, o), n && this.sync(i), this.needsBlock = r;
		} else {
			let n = this.readStyles(e, t);
			n && this.addElementByRule(e, s, n, s.consuming === !1 ? o : void 0);
		}
		this.localPreserveWS = r;
	}
	leafFallback(e, t) {
		e.nodeName == "BR" && this.top.type && this.top.type.inlineContent && this.addTextNode(e.ownerDocument.createTextNode("\n"), t);
	}
	ignoreFallback(e, t) {
		e.nodeName == "BR" && (!this.top.type || !this.top.type.inlineContent) && this.findPlace(this.parser.schema.text("-"), t, !0);
	}
	readStyles(e, t) {
		let n = e.style;
		if (n && n.length) for (let e = 0; e < this.parser.matchedStyles.length; e++) {
			let r = this.parser.matchedStyles[e], i = n.getPropertyValue(r);
			if (i) for (let e;;) {
				let n = this.parser.matchStyle(r, i, this, e);
				if (!n) break;
				if (n.ignore) return null;
				if (t = n.clearMark ? t.filter((e) => !n.clearMark(e)) : t.concat(this.parser.schema.marks[n.mark].create(n.attrs)), n.consuming === !1) e = n;
				else break;
			}
		}
		return t;
	}
	addElementByRule(e, t, n, r) {
		let i, a;
		if (t.node) {
			if (a = this.parser.schema.nodes[t.node], a.isLeaf) this.insertNode(a.create(t.attrs), n, e.nodeName == "BR") || this.leafFallback(e, n);
			else {
				let e = this.enter(a, t.attrs || null, n, t.preserveWhitespace);
				e && (i = !0, n = e);
			}
		} else {
			let e = this.parser.schema.marks[t.mark];
			n = n.concat(e.create(t.attrs));
		}
		let o = this.top;
		if (a && a.isLeaf) this.findInside(e);
		else if (r) this.addElement(e, n, r);
		else if (t.getContent) this.findInside(e), t.getContent(e, this.parser.schema).forEach((e) => this.insertNode(e, n, !1));
		else {
			let r = e;
			typeof t.contentElement == "string" ? r = e.querySelector(t.contentElement) : typeof t.contentElement == "function" ? r = t.contentElement(e) : t.contentElement && (r = t.contentElement), this.findAround(e, r, !0), this.addAll(r, n), this.findAround(e, r, !1);
		}
		i && this.sync(o) && this.open--;
	}
	addAll(e, t, n, r) {
		let i = n || 0;
		for (let a = n ? e.childNodes[n] : e.firstChild, o = r == null ? null : e.childNodes[r]; a != o; a = a.nextSibling, ++i) this.findAtPoint(e, i), this.addDOM(a, t);
		this.findAtPoint(e, i);
	}
	findPlace(e, t, n) {
		let r, i;
		for (let t = this.open, a = 0; t >= 0; t--) {
			let o = this.nodes[t], s = o.findWrapping(e);
			if (s && (!r || r.length > s.length + a) && (r = s, i = o, !s.length)) break;
			if (o.solid) {
				if (n) break;
				a += 2;
			}
		}
		if (!r) return null;
		this.sync(i);
		for (let e = 0; e < r.length; e++) t = this.enterInner(r[e], null, t, !1);
		return t;
	}
	insertNode(e, t, n) {
		if (e.isInline && this.needsBlock && !this.top.type) {
			let e = this.textblockFromContext();
			e && (t = this.enterInner(e, null, t));
		}
		let r = this.findPlace(e, t, n);
		if (r) {
			this.closeExtra();
			let t = this.top;
			t.match && (t.match = t.match.matchType(e.type));
			let n = p.none;
			for (let i of r.concat(e.marks)) (t.type ? t.type.allowsMarkType(i.type) : $e(i.type, e.type)) && (n = i.addToSet(n));
			return t.content.push(e.mark(n)), !0;
		}
		return !1;
	}
	enter(e, t, n, r) {
		let i = this.findPlace(e.create(t), n, !1);
		return i && (i = this.enterInner(e, t, n, !0, r)), i;
	}
	enterInner(e, t, n, r = !1, i) {
		this.closeExtra();
		let a = this.top;
		a.match = a.match && a.match.matchType(e);
		let o = qe(e, i, a.options);
		a.options & Ke && a.content.length == 0 && (o |= Ke);
		let s = p.none;
		return n = n.filter((t) => !(a.type ? a.type.allowsMarkType(t.type) : $e(t.type, e)) || (s = t.addToSet(s), !1)), this.nodes.push(new Je(e, t, s, r, null, o)), this.open++, n;
	}
	closeExtra(e = !1) {
		let t = this.nodes.length - 1;
		if (t > this.open) {
			for (; t > this.open; t--) this.nodes[t - 1].content.push(this.nodes[t].finish(e));
			this.nodes.length = this.open + 1;
		}
	}
	finish() {
		return this.open = 0, this.closeExtra(this.isOpen), this.nodes[0].finish(!!(this.isOpen || this.options.topOpen));
	}
	sync(e) {
		for (let t = this.open; t >= 0; t--) if (this.nodes[t] == e) return this.open = t, !0;
		else this.localPreserveWS && (this.nodes[t].options |= We);
		return !1;
	}
	get currentPos() {
		this.closeExtra();
		let e = 0;
		for (let t = this.open; t >= 0; t--) {
			let n = this.nodes[t].content;
			for (let t = n.length - 1; t >= 0; t--) e += n[t].nodeSize;
			t && e++;
		}
		return e;
	}
	findAtPoint(e, t) {
		if (this.find) for (let n = 0; n < this.find.length; n++) this.find[n].node == e && this.find[n].offset == t && (this.find[n].pos = this.currentPos);
	}
	findInside(e) {
		if (this.find) for (let t = 0; t < this.find.length; t++) this.find[t].pos == null && e.nodeType == 1 && e.contains(this.find[t].node) && (this.find[t].pos = this.currentPos);
	}
	findAround(e, t, n) {
		if (e != t && this.find) for (let r = 0; r < this.find.length; r++) this.find[r].pos == null && e.nodeType == 1 && e.contains(this.find[r].node) && t.compareDocumentPosition(this.find[r].node) & (n ? 2 : 4) && (this.find[r].pos = this.currentPos);
	}
	findInText(e) {
		if (this.find) for (let t = 0; t < this.find.length; t++) this.find[t].node == e && (this.find[t].pos = this.currentPos - (e.nodeValue.length - this.find[t].offset));
	}
	matchesContext(e) {
		if (e.indexOf("|") > -1) return e.split(/\s*\|\s*/).some(this.matchesContext, this);
		let t = e.split("/"), n = this.options.context, r = !this.isOpen && (!n || n.parent.type == this.nodes[0].type), i = -(n ? n.depth + 1 : 0) + +!r, a = (e, o) => {
			for (; e >= 0; e--) {
				let s = t[e];
				if (s == "") {
					if (e == t.length - 1 || e == 0) continue;
					for (; o >= i; o--) if (a(e - 1, o)) return !0;
					return !1;
				}
				{
					let e = o > 0 || o == 0 && r ? this.nodes[o].type : n && o >= i ? n.node(o - i).type : null;
					if (!e || e.name != s && !e.isInGroup(s)) return !1;
					o--;
				}
			}
			return !0;
		};
		return a(t.length - 1, this.open);
	}
	textblockFromContext() {
		let e = this.options.context;
		if (e) for (let t = e.depth; t >= 0; t--) {
			let n = e.node(t).contentMatchAt(e.indexAfter(t)).defaultType;
			if (n && n.isTextblock && n.defaultAttrs) return n;
		}
		for (let e in this.parser.schema.nodes) {
			let t = this.parser.schema.nodes[e];
			if (t.isTextblock && t.defaultAttrs) return t;
		}
	}
};
function Xe(e) {
	for (let t = e.firstChild, n = null; t; t = t.nextSibling) {
		let e = t.nodeType == 1 ? t.nodeName.toLowerCase() : null;
		e && Ue.hasOwnProperty(e) && n ? (n.appendChild(t), t = n) : e == "li" ? n = t : e && (n = null);
	}
}
function Ze(e, t) {
	return (e.matches || e.msMatchesSelector || e.webkitMatchesSelector || e.mozMatchesSelector).call(e, t);
}
function Qe(e) {
	let t = {};
	for (let n in e) t[n] = e[n];
	return t;
}
function $e(e, t) {
	let n = t.schema.nodes;
	for (let r in n) {
		let i = n[r];
		if (!i.allowsMarkType(e)) continue;
		let a = [], o = (e) => {
			a.push(e);
			for (let n = 0; n < e.edgeCount; n++) {
				let { type: r, next: i } = e.edge(n);
				if (r == t || a.indexOf(i) < 0 && o(i)) return !0;
			}
		};
		if (o(i.contentMatch)) return !0;
	}
}
var et = class e {
	constructor(e, t) {
		this.nodes = e, this.marks = t;
	}
	serializeFragment(e, t = {}, n) {
		n || (n = nt(t).createDocumentFragment());
		let r = n, i = [];
		return e.forEach((e) => {
			if (i.length || e.marks.length) {
				let n = 0, a = 0;
				for (; n < i.length && a < e.marks.length;) {
					let t = e.marks[a];
					if (!this.marks[t.type.name]) {
						a++;
						continue;
					}
					if (!t.eq(i[n][0]) || t.type.spec.spanning === !1) break;
					n++, a++;
				}
				for (; n < i.length;) r = i.pop()[1];
				for (; a < e.marks.length;) {
					let n = e.marks[a++], o = this.serializeMark(n, e.isInline, t);
					o && (i.push([n, r]), r.appendChild(o.dom), r = o.contentDOM || o.dom);
				}
			}
			r.appendChild(this.serializeNodeInner(e, t));
		}), n;
	}
	serializeNodeInner(e, t) {
		if (e.isText) return nt(t).createTextNode(e.text);
		let { dom: n, contentDOM: r } = ot(nt(t), this.nodes[e.type.name](e), null, e.attrs);
		if (r) {
			if (e.isLeaf) throw RangeError("Content hole not allowed in a leaf node spec");
			this.serializeFragment(e.content, t, r);
		}
		return n;
	}
	serializeNode(e, t = {}) {
		let n = this.serializeNodeInner(e, t);
		for (let r = e.marks.length - 1; r >= 0; r--) {
			let i = this.serializeMark(e.marks[r], e.isInline, t);
			i && ((i.contentDOM || i.dom).appendChild(n), n = i.dom);
		}
		return n;
	}
	serializeMark(e, t, n = {}) {
		let r = this.marks[e.type.name];
		return r && ot(nt(n), r(e, t), null, e.attrs);
	}
	static renderSpec(e, t, n = null, r) {
		return typeof t == "string" ? { dom: e.createTextNode(t) } : ot(e, t, n, r);
	}
	static fromSchema(t) {
		return t.cached.domSerializer || (t.cached.domSerializer = new e(this.nodesFromSchema(t), this.marksFromSchema(t)));
	}
	static nodesFromSchema(e) {
		let t = tt(e.nodes);
		return t.text || (t.text = (e) => e.text), t;
	}
	static marksFromSchema(e) {
		return tt(e.marks);
	}
};
function tt(e) {
	let t = {};
	for (let n in e) {
		let r = e[n].spec.toDOM;
		r && (t[n] = r);
	}
	return t;
}
function nt(e) {
	return e.document || window.document;
}
var rt = /* @__PURE__ */ new WeakMap();
function it(e) {
	let t = rt.get(e);
	return t === void 0 && rt.set(e, t = at(e)), t;
}
function at(e) {
	let t = null;
	function n(e) {
		if (e && typeof e == "object") {
			if (Array.isArray(e)) {
				if (typeof e[0] == "string") t || (t = []), t.push(e);
				else for (let t = 0; t < e.length; t++) n(e[t]);
			} else for (let t in e) n(e[t]);
		}
	}
	return n(e), t;
}
function ot(e, t, n, r) {
	if (t.nodeType == 1) return { dom: t };
	if (t.dom && t.dom.nodeType == 1) return t;
	let i = t[0], a;
	if (typeof i != "string") throw RangeError("Invalid array passed to renderSpec");
	if (r && (a = it(r)) && a.indexOf(t) > -1) throw RangeError("Using an array from an attribute object as a DOM spec. This may be an attempted cross site scripting attack.");
	let o = i.indexOf(" ");
	o > 0 && (n = i.slice(0, o), i = i.slice(o + 1));
	let s, c = n ? e.createElementNS(n, i) : e.createElement(i), l = t[1], u = 1;
	if (l && typeof l == "object" && l.nodeType == null && !Array.isArray(l)) {
		u = 2;
		for (let e in l) if (l[e] != null) {
			let t = e.indexOf(" ");
			t > 0 ? c.setAttributeNS(e.slice(0, t), e.slice(t + 1), l[e]) : e == "style" && c.style ? c.style.cssText = l[e] : c.setAttribute(e, l[e]);
		}
	}
	for (let i = u; i < t.length; i++) {
		let a = t[i];
		if (a === 0) {
			if (i < t.length - 1 || i > u) throw RangeError("Content hole must be the only child of its parent node");
			return {
				dom: c,
				contentDOM: c
			};
		}
		if (typeof a == "string") c.appendChild(e.createTextNode(a));
		else {
			let { dom: t, contentDOM: i } = ot(e, a, n, r);
			if (c.appendChild(t), i) {
				if (s) throw RangeError("Multiple content holes");
				s = i;
			}
		}
	}
	return {
		dom: c,
		contentDOM: s
	};
}
//#endregion
//#region node_modules/prosemirror-transform/dist/index.js
var st = 65535, ct = 2 ** 16;
function lt(e, t) {
	return e + t * ct;
}
function ut(e) {
	return e & st;
}
function dt(e) {
	return (e - (e & st)) / ct;
}
var ft = 1, pt = 2, mt = 4, ht = 8, gt = class {
	constructor(e, t, n) {
		this.pos = e, this.delInfo = t, this.recover = n;
	}
	get deleted() {
		return (this.delInfo & ht) > 0;
	}
	get deletedBefore() {
		return (this.delInfo & 5) > 0;
	}
	get deletedAfter() {
		return (this.delInfo & 6) > 0;
	}
	get deletedAcross() {
		return (this.delInfo & mt) > 0;
	}
}, _t = class e {
	constructor(t, n = !1) {
		if (this.ranges = t, this.inverted = n, !t.length && e.empty) return e.empty;
	}
	recover(e) {
		let t = 0, n = ut(e);
		if (!this.inverted) for (let e = 0; e < n; e++) t += this.ranges[e * 3 + 2] - this.ranges[e * 3 + 1];
		return this.ranges[n * 3] + t + dt(e);
	}
	mapResult(e, t = 1) {
		return this._map(e, t, !1);
	}
	map(e, t = 1) {
		return this._map(e, t, !0);
	}
	_map(e, t, n) {
		let r = 0, i = this.inverted ? 2 : 1, a = this.inverted ? 1 : 2;
		for (let o = 0; o < this.ranges.length; o += 3) {
			let s = this.ranges[o] - (this.inverted ? r : 0);
			if (s > e) break;
			let c = this.ranges[o + i], l = this.ranges[o + a], u = s + c;
			if (e <= u) {
				let i = c ? e == s ? -1 : e == u ? 1 : t : t, a = s + r + (i < 0 ? 0 : l);
				if (n) return a;
				let d = e == (t < 0 ? s : u) ? null : lt(o / 3, e - s), f = 0;
				return c && (f |= e == s ? pt : e == u ? ft : mt, (t < 0 ? e != s : e != u) && (f |= ht)), new gt(a, f, d);
			}
			r += l - c;
		}
		return n ? e + r : new gt(e + r, 0, null);
	}
	touches(e, t) {
		let n = 0, r = ut(t), i = this.inverted ? 2 : 1, a = this.inverted ? 1 : 2;
		for (let t = 0; t < this.ranges.length; t += 3) {
			let o = this.ranges[t] - (this.inverted ? n : 0);
			if (o > e) break;
			let s = this.ranges[t + i];
			if (e <= o + s && t == r * 3) return !0;
			n += this.ranges[t + a] - s;
		}
		return !1;
	}
	forEach(e) {
		let t = this.inverted ? 2 : 1, n = this.inverted ? 1 : 2;
		for (let r = 0, i = 0; r < this.ranges.length; r += 3) {
			let a = this.ranges[r], o = a - (this.inverted ? i : 0), s = a + (this.inverted ? 0 : i), c = this.ranges[r + t], l = this.ranges[r + n];
			e(o, o + c, s, s + l), i += l - c;
		}
	}
	invert() {
		return new e(this.ranges, !this.inverted);
	}
	toString() {
		return (this.inverted ? "-" : "") + JSON.stringify(this.ranges);
	}
	static offset(t) {
		return t == 0 ? e.empty : new e(t < 0 ? [
			0,
			-t,
			0
		] : [
			0,
			0,
			t
		]);
	}
};
_t.empty = new _t([]);
var vt = class e {
	constructor(e, t, n = 0, r = e ? e.length : 0) {
		this.mirror = t, this.from = n, this.to = r, this._maps = e || [], this.ownData = !(e || t);
	}
	get maps() {
		return this._maps;
	}
	slice(t = 0, n = this.maps.length) {
		return new e(this._maps, this.mirror, t, n);
	}
	appendMap(e, t) {
		this.ownData || (this._maps = this._maps.slice(), this.mirror = this.mirror && this.mirror.slice(), this.ownData = !0), this.to = this._maps.push(e), t != null && this.setMirror(this._maps.length - 1, t);
	}
	appendMapping(e) {
		for (let t = 0, n = this._maps.length; t < e._maps.length; t++) {
			let r = e.getMirror(t);
			this.appendMap(e._maps[t], r != null && r < t ? n + r : void 0);
		}
	}
	getMirror(e) {
		if (this.mirror) {
			for (let t = 0; t < this.mirror.length; t++) if (this.mirror[t] == e) return this.mirror[t + (t % 2 ? -1 : 1)];
		}
	}
	setMirror(e, t) {
		this.mirror || (this.mirror = []), this.mirror.push(e, t);
	}
	appendMappingInverted(e) {
		for (let t = e.maps.length - 1, n = this._maps.length + e._maps.length; t >= 0; t--) {
			let r = e.getMirror(t);
			this.appendMap(e._maps[t].invert(), r != null && r > t ? n - r - 1 : void 0);
		}
	}
	invert() {
		let t = new e();
		return t.appendMappingInverted(this), t;
	}
	map(e, t = 1) {
		if (this.mirror) return this._map(e, t, !0);
		for (let n = this.from; n < this.to; n++) e = this._maps[n].map(e, t);
		return e;
	}
	mapResult(e, t = 1) {
		return this._map(e, t, !1);
	}
	_map(e, t, n) {
		let r = 0;
		for (let n = this.from; n < this.to; n++) {
			let i = this._maps[n].mapResult(e, t);
			if (i.recover != null) {
				let t = this.getMirror(n);
				if (t != null && t > n && t < this.to) {
					n = t, e = this._maps[t].recover(i.recover);
					continue;
				}
			}
			r |= i.delInfo, e = i.pos;
		}
		return n ? e : new gt(e, r, null);
	}
}, yt = Object.create(null), bt = class {
	getMap() {
		return _t.empty;
	}
	merge(e) {
		return null;
	}
	static fromJSON(e, t) {
		if (!t || !t.stepType) throw RangeError("Invalid input for Step.fromJSON");
		let n = yt[t.stepType];
		if (!n) throw RangeError(`No step type ${t.stepType} defined`);
		return n.fromJSON(e, t);
	}
	static jsonID(e, t) {
		if (e in yt) throw RangeError("Duplicate use of step JSON ID " + e);
		return yt[e] = t, t.prototype.jsonID = e, t;
	}
}, xt = class e {
	constructor(e, t) {
		this.doc = e, this.failed = t;
	}
	static ok(t) {
		return new e(t, null);
	}
	static fail(t) {
		return new e(null, t);
	}
	static fromReplace(t, n, r, i) {
		try {
			return e.ok(t.replace(n, r, i));
		} catch (t) {
			if (t instanceof m) return e.fail(t.message);
			throw t;
		}
	}
};
function St(e, t, n) {
	let r = [];
	for (let i = 0; i < e.childCount; i++) {
		let a = e.child(i);
		a.content.size && (a = a.copy(St(a.content, t, a))), a.isInline && (a = t(a, n, i)), r.push(a);
	}
	return l.fromArray(r);
}
var Ct = class e extends bt {
	constructor(e, t, n) {
		super(), this.from = e, this.to = t, this.mark = n;
	}
	apply(e) {
		let t = e.slice(this.from, this.to), n = e.resolve(this.from), r = n.node(n.sharedDepth(this.to)), i = new h(St(t.content, (e, t) => !e.isAtom || !t.type.allowsMarkType(this.mark.type) ? e : e.mark(this.mark.addToSet(e.marks)), r), t.openStart, t.openEnd);
		return xt.fromReplace(e, this.from, this.to, i);
	}
	invert() {
		return new wt(this.from, this.to, this.mark);
	}
	map(t) {
		let n = t.mapResult(this.from, 1), r = t.mapResult(this.to, -1);
		return n.deleted && r.deleted || n.pos >= r.pos ? null : new e(n.pos, r.pos, this.mark);
	}
	merge(t) {
		return t instanceof e && t.mark.eq(this.mark) && this.from <= t.to && this.to >= t.from ? new e(Math.min(this.from, t.from), Math.max(this.to, t.to), this.mark) : null;
	}
	toJSON() {
		return {
			stepType: "addMark",
			mark: this.mark.toJSON(),
			from: this.from,
			to: this.to
		};
	}
	static fromJSON(t, n) {
		if (typeof n.from != "number" || typeof n.to != "number") throw RangeError("Invalid input for AddMarkStep.fromJSON");
		return new e(n.from, n.to, t.markFromJSON(n.mark));
	}
};
bt.jsonID("addMark", Ct);
var wt = class e extends bt {
	constructor(e, t, n) {
		super(), this.from = e, this.to = t, this.mark = n;
	}
	apply(e) {
		let t = e.slice(this.from, this.to), n = new h(St(t.content, (e) => e.mark(this.mark.removeFromSet(e.marks)), e), t.openStart, t.openEnd);
		return xt.fromReplace(e, this.from, this.to, n);
	}
	invert() {
		return new Ct(this.from, this.to, this.mark);
	}
	map(t) {
		let n = t.mapResult(this.from, 1), r = t.mapResult(this.to, -1);
		return n.deleted && r.deleted || n.pos >= r.pos ? null : new e(n.pos, r.pos, this.mark);
	}
	merge(t) {
		return t instanceof e && t.mark.eq(this.mark) && this.from <= t.to && this.to >= t.from ? new e(Math.min(this.from, t.from), Math.max(this.to, t.to), this.mark) : null;
	}
	toJSON() {
		return {
			stepType: "removeMark",
			mark: this.mark.toJSON(),
			from: this.from,
			to: this.to
		};
	}
	static fromJSON(t, n) {
		if (typeof n.from != "number" || typeof n.to != "number") throw RangeError("Invalid input for RemoveMarkStep.fromJSON");
		return new e(n.from, n.to, t.markFromJSON(n.mark));
	}
};
bt.jsonID("removeMark", wt);
var Tt = class e extends bt {
	constructor(e, t) {
		super(), this.pos = e, this.mark = t;
	}
	apply(e) {
		let t = e.nodeAt(this.pos);
		if (!t) return xt.fail("No node at mark step's position");
		let n = t.type.create(t.attrs, null, this.mark.addToSet(t.marks));
		return xt.fromReplace(e, this.pos, this.pos + 1, new h(l.from(n), 0, +!t.isLeaf));
	}
	invert(t) {
		let n = t.nodeAt(this.pos);
		if (n) {
			let t = this.mark.addToSet(n.marks);
			if (t.length == n.marks.length) {
				for (let r = 0; r < n.marks.length; r++) if (!n.marks[r].isInSet(t)) return new e(this.pos, n.marks[r]);
				return new e(this.pos, this.mark);
			}
		}
		return new Et(this.pos, this.mark);
	}
	map(t) {
		let n = t.mapResult(this.pos, 1);
		return n.deletedAfter ? null : new e(n.pos, this.mark);
	}
	toJSON() {
		return {
			stepType: "addNodeMark",
			pos: this.pos,
			mark: this.mark.toJSON()
		};
	}
	static fromJSON(t, n) {
		if (typeof n.pos != "number") throw RangeError("Invalid input for AddNodeMarkStep.fromJSON");
		return new e(n.pos, t.markFromJSON(n.mark));
	}
};
bt.jsonID("addNodeMark", Tt);
var Et = class e extends bt {
	constructor(e, t) {
		super(), this.pos = e, this.mark = t;
	}
	apply(e) {
		let t = e.nodeAt(this.pos);
		if (!t) return xt.fail("No node at mark step's position");
		let n = t.type.create(t.attrs, null, this.mark.removeFromSet(t.marks));
		return xt.fromReplace(e, this.pos, this.pos + 1, new h(l.from(n), 0, +!t.isLeaf));
	}
	invert(e) {
		let t = e.nodeAt(this.pos);
		return !t || !this.mark.isInSet(t.marks) ? this : new Tt(this.pos, this.mark);
	}
	map(t) {
		let n = t.mapResult(this.pos, 1);
		return n.deletedAfter ? null : new e(n.pos, this.mark);
	}
	toJSON() {
		return {
			stepType: "removeNodeMark",
			pos: this.pos,
			mark: this.mark.toJSON()
		};
	}
	static fromJSON(t, n) {
		if (typeof n.pos != "number") throw RangeError("Invalid input for RemoveNodeMarkStep.fromJSON");
		return new e(n.pos, t.markFromJSON(n.mark));
	}
};
bt.jsonID("removeNodeMark", Et);
var Dt = class e extends bt {
	constructor(e, t, n, r = !1) {
		super(), this.from = e, this.to = t, this.slice = n, this.structure = r;
	}
	apply(e) {
		return this.structure && kt(e, this.from, this.to) ? xt.fail("Structure replace would overwrite content") : xt.fromReplace(e, this.from, this.to, this.slice);
	}
	getMap() {
		return new _t([
			this.from,
			this.to - this.from,
			this.slice.size
		]);
	}
	invert(t) {
		return new e(this.from, this.from + this.slice.size, t.slice(this.from, this.to));
	}
	map(t) {
		let n = t.mapResult(this.to, -1), r = this.from == this.to && e.MAP_BIAS < 0 ? n : t.mapResult(this.from, 1);
		return r.deletedAcross && n.deletedAcross ? null : new e(r.pos, Math.max(r.pos, n.pos), this.slice, this.structure);
	}
	merge(t) {
		if (!(t instanceof e) || t.structure || this.structure) return null;
		if (this.from + this.slice.size == t.from && !this.slice.openEnd && !t.slice.openStart) {
			let n = this.slice.size + t.slice.size == 0 ? h.empty : new h(this.slice.content.append(t.slice.content), this.slice.openStart, t.slice.openEnd);
			return new e(this.from, this.to + (t.to - t.from), n, this.structure);
		}
		if (t.to == this.from && !this.slice.openStart && !t.slice.openEnd) {
			let n = this.slice.size + t.slice.size == 0 ? h.empty : new h(t.slice.content.append(this.slice.content), t.slice.openStart, this.slice.openEnd);
			return new e(t.from, this.to, n, this.structure);
		}
		return null;
	}
	toJSON() {
		let e = {
			stepType: "replace",
			from: this.from,
			to: this.to
		};
		return this.slice.size && (e.slice = this.slice.toJSON()), this.structure && (e.structure = !0), e;
	}
	static fromJSON(t, n) {
		if (typeof n.from != "number" || typeof n.to != "number") throw RangeError("Invalid input for ReplaceStep.fromJSON");
		return new e(n.from, n.to, h.fromJSON(t, n.slice), !!n.structure);
	}
};
Dt.MAP_BIAS = 1, bt.jsonID("replace", Dt);
var Ot = class e extends bt {
	constructor(e, t, n, r, i, a, o = !1) {
		super(), this.from = e, this.to = t, this.gapFrom = n, this.gapTo = r, this.slice = i, this.insert = a, this.structure = o;
	}
	apply(e) {
		if (this.structure && (kt(e, this.from, this.gapFrom) || kt(e, this.gapTo, this.to))) return xt.fail("Structure gap-replace would overwrite content");
		let t = e.slice(this.gapFrom, this.gapTo);
		if (t.openStart || t.openEnd) return xt.fail("Gap is not a flat range");
		let n = this.slice.insertAt(this.insert, t.content);
		return n ? xt.fromReplace(e, this.from, this.to, n) : xt.fail("Content does not fit in gap");
	}
	getMap() {
		return new _t([
			this.from,
			this.gapFrom - this.from,
			this.insert,
			this.gapTo,
			this.to - this.gapTo,
			this.slice.size - this.insert
		]);
	}
	invert(t) {
		let n = this.gapTo - this.gapFrom;
		return new e(this.from, this.from + this.slice.size + n, this.from + this.insert, this.from + this.insert + n, t.slice(this.from, this.to).removeBetween(this.gapFrom - this.from, this.gapTo - this.from), this.gapFrom - this.from, this.structure);
	}
	map(t) {
		let n = t.mapResult(this.from, 1), r = t.mapResult(this.to, -1), i = this.from == this.gapFrom ? n.pos : t.map(this.gapFrom, -1), a = this.to == this.gapTo ? r.pos : t.map(this.gapTo, 1);
		return n.deletedAcross && r.deletedAcross || i < n.pos || a > r.pos ? null : new e(n.pos, r.pos, i, a, this.slice, this.insert, this.structure);
	}
	toJSON() {
		let e = {
			stepType: "replaceAround",
			from: this.from,
			to: this.to,
			gapFrom: this.gapFrom,
			gapTo: this.gapTo,
			insert: this.insert
		};
		return this.slice.size && (e.slice = this.slice.toJSON()), this.structure && (e.structure = !0), e;
	}
	static fromJSON(t, n) {
		if (typeof n.from != "number" || typeof n.to != "number" || typeof n.gapFrom != "number" || typeof n.gapTo != "number" || typeof n.insert != "number") throw RangeError("Invalid input for ReplaceAroundStep.fromJSON");
		return new e(n.from, n.to, n.gapFrom, n.gapTo, h.fromJSON(t, n.slice), n.insert, !!n.structure);
	}
};
bt.jsonID("replaceAround", Ot);
function kt(e, t, n) {
	let r = e.resolve(t), i = n - t, a = r.depth;
	for (; i > 0 && a > 0 && r.indexAfter(a) == r.node(a).childCount;) a--, i--;
	if (i > 0) {
		let e = r.node(a).maybeChild(r.indexAfter(a));
		for (; i > 0;) {
			if (!e || e.isLeaf) return !0;
			e = e.firstChild, i--;
		}
	}
	return !1;
}
function At(e, t, n, r) {
	let i = [], a = [], o, s;
	e.doc.nodesBetween(t, n, (e, c, l) => {
		if (!e.isInline) return;
		let u = e.marks;
		if (!r.isInSet(u) && l.type.allowsMarkType(r.type)) {
			let l = Math.max(c, t), d = Math.min(c + e.nodeSize, n), f = r.addToSet(u);
			for (let e = 0; e < u.length; e++) u[e].isInSet(f) || (o && o.to == l && o.mark.eq(u[e]) ? o.to = d : i.push(o = new wt(l, d, u[e])));
			s && s.to == l ? s.to = d : a.push(s = new Ct(l, d, r));
		}
	}), i.forEach((t) => e.step(t)), a.forEach((t) => e.step(t));
}
function jt(e, t, n, r) {
	let i = [], a = 0;
	e.doc.nodesBetween(t, n, (e, o) => {
		if (!e.isInline) return;
		a++;
		let s = null;
		if (r instanceof Fe) {
			let t = e.marks, n;
			for (; n = r.isInSet(t);) (s || (s = [])).push(n), t = n.removeFromSet(t);
		} else r ? r.isInSet(e.marks) && (s = [r]) : s = e.marks;
		if (s && s.length) {
			let r = Math.min(o + e.nodeSize, n);
			for (let e = 0; e < s.length; e++) {
				let n = s[e], c;
				for (let e = 0; e < i.length; e++) {
					let t = i[e];
					t.step == a - 1 && n.eq(i[e].style) && (c = t);
				}
				c ? (c.to = r, c.step = a) : i.push({
					style: n,
					from: Math.max(o, t),
					to: r,
					step: a
				});
			}
		}
	}), i.forEach((t) => e.step(new wt(t.from, t.to, t.style)));
}
function Mt(e, t, n, r = n.contentMatch, i = !0) {
	let a = e.doc.nodeAt(t), o = [], s = t + 1;
	for (let t = 0; t < a.childCount; t++) {
		let c = a.child(t), u = s + c.nodeSize, d = r.matchType(c.type);
		if (!d) o.push(new Dt(s, u, h.empty));
		else {
			r = d;
			for (let t = 0; t < c.marks.length; t++) n.allowsMarkType(c.marks[t].type) || e.step(new wt(s, u, c.marks[t]));
			if (i && c.isText && n.whitespace != "pre") {
				let e, t = /\r?\n|\r/g, r;
				for (; e = t.exec(c.text);) r || (r = new h(l.from(n.schema.text(" ", n.allowedMarks(c.marks))), 0, 0)), o.push(new Dt(s + e.index, s + e.index + e[0].length, r));
			}
		}
		s = u;
	}
	if (!r.validEnd) {
		let t = r.fillBefore(l.empty, !0);
		e.replace(s, s, new h(t, 0, 0));
	}
	for (let t = o.length - 1; t >= 0; t--) e.step(o[t]);
}
function Nt(e, t, n) {
	return (t == 0 || e.canReplace(t, e.childCount)) && (n == e.childCount || e.canReplace(0, n));
}
function Pt(e) {
	let t = e.parent.content.cutByIndex(e.startIndex, e.endIndex);
	for (let n = e.depth, r = 0, i = 0;; --n) {
		let a = e.$from.node(n), o = e.$from.index(n) + r, s = e.$to.indexAfter(n) - i;
		if (n < e.depth && a.canReplace(o, s, t)) return n;
		if (n == 0 || a.type.spec.isolating || !Nt(a, o, s)) break;
		o && (r = 1), s < a.childCount && (i = 1);
	}
	return null;
}
function Ft(e, t, n) {
	let { $from: r, $to: i, depth: a } = t, o = r.before(a + 1), s = i.after(a + 1), c = o, u = s, d = l.empty, f = 0;
	for (let e = a, t = !1; e > n; e--) t || r.index(e) > 0 ? (t = !0, d = l.from(r.node(e).copy(d)), f++) : c--;
	let p = l.empty, m = 0;
	for (let e = a, t = !1; e > n; e--) t || i.after(e + 1) < i.end(e) ? (t = !0, p = l.from(i.node(e).copy(p)), m++) : u++;
	e.step(new Ot(c, u, o, s, new h(d.append(p), f, m), d.size - f, !0));
}
function It(e, t, n = null, r = e) {
	let i = Rt(e, t), a = i && zt(r, t);
	return a ? i.map(Lt).concat({
		type: t,
		attrs: n
	}).concat(a.map(Lt)) : null;
}
function Lt(e) {
	return {
		type: e,
		attrs: null
	};
}
function Rt(e, t) {
	let { parent: n, startIndex: r, endIndex: i } = e, a = n.contentMatchAt(r).findWrapping(t);
	if (!a) return null;
	let o = a.length ? a[0] : t;
	return n.canReplaceWith(r, i, o) ? a : null;
}
function zt(e, t) {
	let { parent: n, startIndex: r, endIndex: i } = e, a = n.child(r), o = t.contentMatch.findWrapping(a.type);
	if (!o) return null;
	let s = (o.length ? o[o.length - 1] : t).contentMatch;
	for (let e = r; s && e < i; e++) s = s.matchType(n.child(e).type);
	return !s || !s.validEnd ? null : o;
}
function Bt(e, t, n) {
	let r = l.empty;
	for (let e = n.length - 1; e >= 0; e--) {
		if (r.size) {
			let t = n[e].type.contentMatch.matchFragment(r);
			if (!t || !t.validEnd) throw RangeError("Wrapper type given to Transform.wrap does not form valid content of its parent wrapper");
		}
		r = l.from(n[e].type.create(n[e].attrs, r));
	}
	let i = t.start, a = t.end;
	e.step(new Ot(i, a, i, a, new h(r, 0, 0), n.length, !0));
}
function Vt(e, t, n, r, i) {
	if (!r.isTextblock) throw RangeError("Type given to setBlockType should be a textblock");
	let a = e.steps.length;
	e.doc.nodesBetween(t, n, (t, n) => {
		let o = typeof i == "function" ? i(t) : i;
		if (t.isTextblock && !t.hasMarkup(r, o) && Wt(e.doc, e.mapping.slice(a).map(n), r)) {
			let i = null;
			if (r.schema.linebreakReplacement) {
				let e = r.whitespace == "pre", t = !!r.contentMatch.matchType(r.schema.linebreakReplacement);
				e && !t ? i = !1 : !e && t && (i = !0);
			}
			i === !1 && Ut(e, t, n, a), Mt(e, e.mapping.slice(a).map(n, 1), r, void 0, i === null);
			let s = e.mapping.slice(a), c = s.map(n, 1), u = s.map(n + t.nodeSize, 1);
			return e.step(new Ot(c, u, c + 1, u - 1, new h(l.from(r.create(o, null, t.marks)), 0, 0), 1, !0)), i === !0 && Ht(e, t, n, a), !1;
		}
	});
}
function Ht(e, t, n, r) {
	t.forEach((i, a) => {
		if (i.isText) {
			let o, s = /\r?\n|\r/g;
			for (; o = s.exec(i.text);) {
				let i = e.mapping.slice(r).map(n + 1 + a + o.index);
				e.replaceWith(i, i + 1, t.type.schema.linebreakReplacement.create());
			}
		}
	});
}
function Ut(e, t, n, r) {
	t.forEach((i, a) => {
		if (i.type == i.type.schema.linebreakReplacement) {
			let i = e.mapping.slice(r).map(n + 1 + a);
			e.replaceWith(i, i + 1, t.type.schema.text("\n"));
		}
	});
}
function Wt(e, t, n) {
	let r = e.resolve(t), i = r.index();
	return r.parent.canReplaceWith(i, i + 1, n);
}
function Gt(e, t, n, r, i) {
	let a = e.doc.nodeAt(t);
	if (!a) throw RangeError("No node at given position");
	n || (n = a.type);
	let o = n.create(r, null, i || a.marks);
	if (a.isLeaf) return e.replaceWith(t, t + a.nodeSize, o);
	if (!n.validContent(a.content)) throw RangeError("Invalid content for node type " + n.name);
	e.step(new Ot(t, t + a.nodeSize, t + 1, t + a.nodeSize - 1, new h(l.from(o), 0, 0), 1, !0));
}
function Kt(e, t, n = 1, r) {
	let i = e.resolve(t), a = i.depth - n, o = r && r[r.length - 1] || i.parent;
	if (a < 0 || i.parent.type.spec.isolating || !i.parent.canReplace(i.index(), i.parent.childCount) || !o.type.validContent(i.parent.content.cutByIndex(i.index(), i.parent.childCount))) return !1;
	for (let e = i.depth - 1, t = n - 2; e > a; e--, t--) {
		let n = i.node(e), a = i.index(e);
		if (n.type.spec.isolating) return !1;
		let o = n.content.cutByIndex(a, n.childCount), s = r && r[t + 1];
		s && (o = o.replaceChild(0, s.type.create(s.attrs)));
		let c = r && r[t] || n;
		if (!n.canReplace(a + 1, n.childCount) || !c.type.validContent(o)) return !1;
	}
	let s = i.indexAfter(a), c = r && r[0];
	return i.node(a).canReplaceWith(s, s, c ? c.type : i.node(a + 1).type);
}
function qt(e, t, n = 1, r) {
	let i = e.doc.resolve(t), a = l.empty, o = l.empty;
	for (let e = i.depth, t = i.depth - n, s = n - 1; e > t; e--, s--) {
		a = l.from(i.node(e).copy(a));
		let t = r && r[s];
		o = l.from(t ? t.type.create(t.attrs, o) : i.node(e).copy(o));
	}
	e.step(new Dt(t, t, new h(a.append(o), n, n), !0));
}
function Jt(e, t) {
	let n = e.resolve(t), r = n.index();
	return Xt(n.nodeBefore, n.nodeAfter) && n.parent.canReplace(r, r + 1);
}
function Yt(e, t) {
	t.content.size || e.type.compatibleContent(t.type);
	let n = e.contentMatchAt(e.childCount), { linebreakReplacement: r } = e.type.schema;
	for (let i = 0; i < t.childCount; i++) {
		let a = t.child(i), o = a.type == r ? e.type.schema.nodes.text : a.type;
		if (n = n.matchType(o), !n || !e.type.allowsMarks(a.marks)) return !1;
	}
	return n.validEnd;
}
function Xt(e, t) {
	return !!(e && t && !e.isLeaf && Yt(e, t));
}
function Zt(e, t, n = -1) {
	let r = e.resolve(t);
	for (let e = r.depth;; e--) {
		let i, a, o = r.index(e);
		if (e == r.depth ? (i = r.nodeBefore, a = r.nodeAfter) : n > 0 ? (i = r.node(e + 1), o++, a = r.node(e).maybeChild(o)) : (i = r.node(e).maybeChild(o - 1), a = r.node(e + 1)), i && !i.isTextblock && Xt(i, a) && r.node(e).canReplace(o, o + 1)) return t;
		if (e == 0) break;
		t = n < 0 ? r.before(e) : r.after(e);
	}
}
function Qt(e, t, n) {
	let r = null, { linebreakReplacement: i } = e.doc.type.schema, a = e.doc.resolve(t - n), o = a.node().type;
	if (i && o.inlineContent) {
		let e = o.whitespace == "pre", t = !!o.contentMatch.matchType(i);
		e && !t ? r = !1 : !e && t && (r = !0);
	}
	let s = e.steps.length;
	if (r === !1) {
		let r = e.doc.resolve(t + n);
		Ut(e, r.node(), r.before(), s);
	}
	o.inlineContent && Mt(e, t + n - 1, o, a.node().contentMatchAt(a.index()), r == null);
	let c = e.mapping.slice(s), l = c.map(t - n);
	if (e.step(new Dt(l, c.map(t + n, -1), h.empty, !0)), r === !0) {
		let t = e.doc.resolve(l);
		Ht(e, t.node(), t.before(), e.steps.length);
	}
	return e;
}
function $t(e, t, n) {
	let r = e.resolve(t);
	if (r.parent.canReplaceWith(r.index(), r.index(), n)) return t;
	if (r.parentOffset == 0) for (let e = r.depth - 1; e >= 0; e--) {
		let t = r.index(e);
		if (r.node(e).canReplaceWith(t, t, n)) return r.before(e + 1);
		if (t > 0) return null;
	}
	if (r.parentOffset == r.parent.content.size) for (let e = r.depth - 1; e >= 0; e--) {
		let t = r.indexAfter(e);
		if (r.node(e).canReplaceWith(t, t, n)) return r.after(e + 1);
		if (t < r.node(e).childCount) return null;
	}
	return null;
}
function en(e, t, n) {
	let r = e.resolve(t);
	if (!n.content.size) return t;
	let i = n.content;
	for (let e = 0; e < n.openStart; e++) i = i.firstChild.content;
	for (let e = 1; e <= (n.openStart == 0 && n.size ? 2 : 1); e++) for (let t = r.depth; t >= 0; t--) {
		let n = t == r.depth ? 0 : r.pos <= (r.start(t + 1) + r.end(t + 1)) / 2 ? -1 : 1, a = r.index(t) + +(n > 0), o = r.node(t), s = !1;
		if (e == 1) s = o.canReplace(a, a, i);
		else {
			let e = o.contentMatchAt(a).findWrapping(i.firstChild.type);
			s = e && o.canReplaceWith(a, a, e[0]);
		}
		if (s) return n == 0 ? r.pos : n < 0 ? r.before(t + 1) : r.after(t + 1);
	}
	return null;
}
function tn(e, t, n = t, r = h.empty) {
	if (t == n && !r.size) return null;
	let i = e.resolve(t), a = e.resolve(n);
	return nn(i, a, r) ? new Dt(t, n, r) : new rn(i, a, r).fit();
}
function nn(e, t, n) {
	return !n.openStart && !n.openEnd && e.start() == t.start() && e.parent.canReplace(e.index(), t.index(), n.content);
}
var rn = class {
	constructor(e, t, n) {
		this.$from = e, this.$to = t, this.unplaced = n, this.frontier = [], this.placed = l.empty;
		for (let t = 0; t <= e.depth; t++) {
			let n = e.node(t);
			this.frontier.push({
				type: n.type,
				match: n.contentMatchAt(e.indexAfter(t))
			});
		}
		for (let t = e.depth; t > 0; t--) this.placed = l.from(e.node(t).copy(this.placed));
	}
	get depth() {
		return this.frontier.length - 1;
	}
	fit() {
		for (; this.unplaced.size;) {
			let e = this.findFittable();
			e ? this.placeNodes(e) : this.openMore() || this.dropNode();
		}
		let e = this.mustMoveInline(), t = this.placed.size - this.depth - this.$from.depth, n = this.$from, r = this.close(e < 0 ? this.$to : n.doc.resolve(e));
		if (!r) return null;
		let i = this.placed, a = n.depth, o = r.depth;
		for (; a && o && i.childCount == 1;) i = i.firstChild.content, a--, o--;
		let s = new h(i, a, o);
		return e > -1 ? new Ot(n.pos, e, this.$to.pos, this.$to.end(), s, t) : s.size || n.pos != this.$to.pos ? new Dt(n.pos, r.pos, s) : null;
	}
	findFittable() {
		let e = this.unplaced.openStart;
		for (let t = this.unplaced.content, n = 0, r = this.unplaced.openEnd; n < e; n++) {
			let i = t.firstChild;
			if (t.childCount > 1 && (r = 0), i.type.spec.isolating && r <= n) {
				e = n;
				break;
			}
			t = i.content;
		}
		for (let t = 1; t <= 2; t++) for (let n = t == 1 ? e : this.unplaced.openStart; n >= 0; n--) {
			let e, r = null;
			n ? (r = sn(this.unplaced.content, n - 1).firstChild, e = r.content) : e = this.unplaced.content;
			let i = e.firstChild;
			for (let e = this.depth; e >= 0; e--) {
				let { type: a, match: o } = this.frontier[e], s, c = null;
				if (t == 1 && (i ? o.matchType(i.type) || (c = o.fillBefore(l.from(i), !1)) : r && a.compatibleContent(r.type))) return {
					sliceDepth: n,
					frontierDepth: e,
					parent: r,
					inject: c
				};
				if (t == 2 && i && (s = o.findWrapping(i.type))) return {
					sliceDepth: n,
					frontierDepth: e,
					parent: r,
					wrap: s
				};
				if (r && o.matchType(r.type)) break;
			}
		}
	}
	openMore() {
		let { content: e, openStart: t, openEnd: n } = this.unplaced;
		return fn(e, -1) <= t ? !1 : (this.unplaced.size > 1 && fn(e, 1) > n && n++, this.unplaced = new h(e, t + 1, n), !0);
	}
	dropNode() {
		let { content: e, openStart: t, openEnd: n } = this.unplaced, r = sn(e, t);
		if (r.childCount <= 1 && t > 0) {
			let i = e.size - t <= t + r.size;
			this.unplaced = new h(an(e, t - 1, 1), t - 1, i ? t - 1 : n);
		} else this.unplaced = new h(an(e, t, 1), t, n);
	}
	placeNodes({ sliceDepth: e, frontierDepth: t, parent: n, inject: r, wrap: i }) {
		for (; this.depth > t;) this.closeFrontierNode();
		if (i) for (let e = 0; e < i.length; e++) this.openFrontierNode(i[e]);
		let a = this.unplaced, o = n ? n.content : a.content, s = a.openStart - e, c = 0, u = [], { match: d, type: f } = this.frontier[t];
		if (r) {
			for (let e = 0; e < r.childCount; e++) u.push(r.child(e));
			d = d.matchFragment(r);
		}
		let p = o.size + e - (a.content.size - a.openEnd);
		for (; c < o.childCount;) {
			let e = o.child(c), t = d.matchType(e.type);
			if (!t) break;
			c++, (c > 1 || s == 0 || e.content.size) && (d = t, u.push(cn(e.mark(f.allowedMarks(e.marks)), c == 1 ? s : 0, c == o.childCount ? p : -1)));
		}
		let m = c == o.childCount;
		m || (p = -1), this.placed = on(this.placed, t, l.from(u)), this.frontier[t].match = d, m && p < 0 && n && n.type == this.frontier[this.depth].type && this.frontier.length > 1 && this.closeFrontierNode();
		for (let e = 0, t = o; e < p; e++) {
			let e = t.lastChild;
			this.frontier.push({
				type: e.type,
				match: e.contentMatchAt(e.childCount)
			}), t = e.content;
		}
		this.unplaced = m ? e == 0 ? h.empty : new h(an(a.content, e - 1, 1), e - 1, p < 0 ? a.openEnd : e - 1) : new h(an(a.content, e, c), a.openStart, a.openEnd);
	}
	mustMoveInline() {
		if (!this.$to.parent.isTextblock) return -1;
		let e = this.frontier[this.depth], t;
		if (!e.type.isTextblock || !ln(this.$to, this.$to.depth, e.type, e.match, !1) || this.$to.depth == this.depth && (t = this.findCloseLevel(this.$to)) && t.depth == this.depth) return -1;
		let { depth: n } = this.$to, r = this.$to.after(n);
		for (; n > 1 && r == this.$to.end(--n);) ++r;
		return r;
	}
	findCloseLevel(e) {
		scan: for (let t = Math.min(this.depth, e.depth); t >= 0; t--) {
			let { match: n, type: r } = this.frontier[t], i = t < e.depth && e.end(t + 1) == e.pos + (e.depth - (t + 1)), a = ln(e, t, r, n, i);
			if (a) {
				for (let n = t - 1; n >= 0; n--) {
					let { match: t, type: r } = this.frontier[n], i = ln(e, n, r, t, !0);
					if (!i || i.childCount) continue scan;
				}
				return {
					depth: t,
					fit: a,
					move: i ? e.doc.resolve(e.after(t + 1)) : e
				};
			}
		}
	}
	close(e) {
		let t = this.findCloseLevel(e);
		if (!t) return null;
		for (; this.depth > t.depth;) this.closeFrontierNode();
		t.fit.childCount && (this.placed = on(this.placed, t.depth, t.fit)), e = t.move;
		for (let n = t.depth + 1; n <= e.depth; n++) {
			let t = e.node(n), r = t.type.contentMatch.fillBefore(t.content, !0, e.index(n));
			this.openFrontierNode(t.type, t.attrs, r);
		}
		return e;
	}
	openFrontierNode(e, t = null, n) {
		let r = this.frontier[this.depth];
		r.match = r.match.matchType(e), this.placed = on(this.placed, this.depth, l.from(e.create(t, n))), this.frontier.push({
			type: e,
			match: e.contentMatch
		});
	}
	closeFrontierNode() {
		let e = this.frontier.pop().match.fillBefore(l.empty, !0);
		e.childCount && (this.placed = on(this.placed, this.frontier.length, e));
	}
};
function an(e, t, n) {
	return t == 0 ? e.cutByIndex(n, e.childCount) : e.replaceChild(0, e.firstChild.copy(an(e.firstChild.content, t - 1, n)));
}
function on(e, t, n) {
	return t == 0 ? e.append(n) : e.replaceChild(e.childCount - 1, e.lastChild.copy(on(e.lastChild.content, t - 1, n)));
}
function sn(e, t) {
	for (let n = 0; n < t; n++) e = e.firstChild.content;
	return e;
}
function cn(e, t, n) {
	if (t <= 0) return e;
	let r = e.content;
	return t > 1 && (r = r.replaceChild(0, cn(r.firstChild, t - 1, r.childCount == 1 ? n - 1 : 0))), t > 0 && (r = e.type.contentMatch.fillBefore(r).append(r), n <= 0 && (r = r.append(e.type.contentMatch.matchFragment(r).fillBefore(l.empty, !0)))), e.copy(r);
}
function ln(e, t, n, r, i) {
	let a = e.node(t), o = i ? e.indexAfter(t) : e.index(t);
	if (o == a.childCount && !n.compatibleContent(a.type)) return null;
	let s = r.fillBefore(a.content, !0, o);
	return s && !un(n, a.content, o) ? s : null;
}
function un(e, t, n) {
	for (let r = n; r < t.childCount; r++) if (!e.allowsMarks(t.child(r).marks)) return !0;
	return !1;
}
function dn(e) {
	return e.spec.defining || e.spec.definingForContent;
}
function fn(e, t) {
	for (let n = 0;; n++) {
		let r = t < 0 ? e.firstChild : e.lastChild;
		if (!r || r.isAtom) return n;
		e = r.content;
	}
}
function pn(e, t, n, r) {
	if (!r.size) return e.deleteRange(t, n);
	let i = e.doc.resolve(t), a = e.doc.resolve(n);
	if (nn(i, a, r)) return e.step(new Dt(t, n, r));
	let o = _n(i, a);
	o[o.length - 1] == 0 && o.pop();
	let s = -(i.depth + 1);
	o.unshift(s);
	for (let e = i.depth, t = i.pos - 1; e > 0; e--, t--) {
		let n = i.node(e).type.spec;
		if (n.defining || n.definingAsContext || n.isolating) break;
		o.indexOf(e) > -1 ? s = e : i.before(e) == t && o.splice(1, 0, -e);
	}
	let c = o.indexOf(s), l = [], u = r.openStart;
	for (let e = r.content, t = 0;; t++) {
		let n = e.firstChild;
		if (l.push(n), t == r.openStart) break;
		e = n.content;
	}
	for (let e = u - 1; e >= 0; e--) {
		let t = l[e], n = dn(t.type);
		if (n && !t.sameMarkup(i.node(Math.abs(s) - 1))) u = e;
		else if (n || !t.type.isTextblock) break;
	}
	for (let t = r.openStart; t >= 0; t--) {
		let s = (t + u + 1) % (r.openStart + 1), d = l[s];
		if (d) for (let t = 0; t < o.length; t++) {
			let l = o[(t + c) % o.length], u = !0;
			l < 0 && (u = !1, l = -l);
			let f = i.node(l - 1), p = i.index(l - 1);
			if (f.canReplaceWith(p, p, d.type, d.marks)) return e.replace(i.before(l), u ? a.after(l) : n, new h(mn(r.content, 0, r.openStart, s), s, r.openEnd));
		}
	}
	let d = e.steps.length;
	for (let s = o.length - 1; s >= 0 && (e.replace(t, n, r), !(e.steps.length > d)); s--) {
		let e = o[s];
		e < 0 || (t = i.before(e), n = a.after(e));
	}
}
function mn(e, t, n, r, i) {
	if (t < n) {
		let i = e.firstChild;
		e = e.replaceChild(0, i.copy(mn(i.content, t + 1, n, r, i)));
	}
	if (t > r) {
		let t = i.contentMatchAt(0), n = t.fillBefore(e).append(e);
		e = n.append(t.matchFragment(n).fillBefore(l.empty, !0));
	}
	return e;
}
function hn(e, t, n, r) {
	if (!r.isInline && t == n && e.doc.resolve(t).parent.content.size) {
		let i = $t(e.doc, t, r.type);
		i != null && (t = n = i);
	}
	e.replaceRange(t, n, new h(l.from(r), 0, 0));
}
function gn(e, t, n) {
	let r = e.doc.resolve(t), i = e.doc.resolve(n);
	if (r.parent.isTextblock && i.parent.isTextblock && r.start() != i.start() && r.parentOffset == 0 && i.parentOffset == 0) {
		let a = r.sharedDepth(n), o = !1;
		for (let e = r.depth; e > a; e--) r.node(e).type.spec.isolating && (o = !0);
		for (let e = i.depth; e > a; e--) i.node(e).type.spec.isolating && (o = !0);
		if (!o) {
			for (let e = r.depth; e > 0 && t == r.start(e); e--) t = r.before(e);
			for (let e = i.depth; e > 0 && n == i.start(e); e--) n = i.before(e);
			r = e.doc.resolve(t), i = e.doc.resolve(n);
		}
	}
	let a = _n(r, i);
	for (let t = 0; t < a.length; t++) {
		let n = a[t], o = t == a.length - 1;
		if (o && n == 0 || r.node(n).type.contentMatch.validEnd) return e.delete(r.start(n), i.end(n));
		if (n > 0 && (o || r.node(n - 1).canReplace(r.index(n - 1), i.indexAfter(n - 1)))) return e.delete(r.before(n), i.after(n));
	}
	for (let a = 1; a <= r.depth && a <= i.depth; a++) if (t - r.start(a) == r.depth - a && n > r.end(a) && i.end(a) - n != i.depth - a && r.start(a - 1) == i.start(a - 1) && r.node(a - 1).canReplace(r.index(a - 1), i.index(a - 1))) return e.delete(r.before(a), n);
	e.delete(t, n);
}
function _n(e, t) {
	let n = [], r = Math.min(e.depth, t.depth);
	for (let i = r; i >= 0; i--) {
		let r = e.start(i);
		if (r < e.pos - (e.depth - i) || t.end(i) > t.pos + (t.depth - i) || e.node(i).type.spec.isolating || t.node(i).type.spec.isolating) break;
		(r == t.start(i) || i == e.depth && i == t.depth && e.parent.inlineContent && t.parent.inlineContent && i && t.start(i - 1) == r - 1) && n.push(i);
	}
	return n;
}
var vn = class e extends bt {
	constructor(e, t, n) {
		super(), this.pos = e, this.attr = t, this.value = n;
	}
	apply(e) {
		let t = e.nodeAt(this.pos);
		if (!t) return xt.fail("No node at attribute step's position");
		let n = Object.create(null);
		for (let e in t.attrs) n[e] = t.attrs[e];
		n[this.attr] = this.value;
		let r = t.type.create(n, null, t.marks);
		return xt.fromReplace(e, this.pos, this.pos + 1, new h(l.from(r), 0, +!t.isLeaf));
	}
	getMap() {
		return _t.empty;
	}
	invert(t) {
		return new e(this.pos, this.attr, t.nodeAt(this.pos).attrs[this.attr]);
	}
	map(t) {
		let n = t.mapResult(this.pos, 1);
		return n.deletedAfter ? null : new e(n.pos, this.attr, this.value);
	}
	toJSON() {
		return {
			stepType: "attr",
			pos: this.pos,
			attr: this.attr,
			value: this.value
		};
	}
	static fromJSON(t, n) {
		if (typeof n.pos != "number" || typeof n.attr != "string") throw RangeError("Invalid input for AttrStep.fromJSON");
		return new e(n.pos, n.attr, n.value);
	}
};
bt.jsonID("attr", vn);
var yn = class e extends bt {
	constructor(e, t) {
		super(), this.attr = e, this.value = t;
	}
	apply(e) {
		let t = Object.create(null);
		for (let n in e.attrs) t[n] = e.attrs[n];
		t[this.attr] = this.value;
		let n = e.type.create(t, e.content, e.marks);
		return xt.ok(n);
	}
	getMap() {
		return _t.empty;
	}
	invert(t) {
		return new e(this.attr, t.attrs[this.attr]);
	}
	map(e) {
		return this;
	}
	toJSON() {
		return {
			stepType: "docAttr",
			attr: this.attr,
			value: this.value
		};
	}
	static fromJSON(t, n) {
		if (typeof n.attr != "string") throw RangeError("Invalid input for DocAttrStep.fromJSON");
		return new e(n.attr, n.value);
	}
};
bt.jsonID("docAttr", yn);
var bn = class extends Error {};
bn = function e(t) {
	let n = Error.call(this, t);
	return n.__proto__ = e.prototype, n;
}, bn.prototype = Object.create(Error.prototype), bn.prototype.constructor = bn, bn.prototype.name = "TransformError";
var xn = class {
	constructor(e) {
		this.doc = e, this.steps = [], this.docs = [], this.mapping = new vt();
	}
	get before() {
		return this.docs.length ? this.docs[0] : this.doc;
	}
	step(e) {
		let t = this.maybeStep(e);
		if (t.failed) throw new bn(t.failed);
		return this;
	}
	maybeStep(e) {
		let t = e.apply(this.doc);
		return t.failed || this.addStep(e, t.doc), t;
	}
	get docChanged() {
		return this.steps.length > 0;
	}
	changedRange() {
		let e = 1e9, t = -1e9;
		for (let n = 0; n < this.mapping.maps.length; n++) {
			let r = this.mapping.maps[n];
			n && (e = r.map(e, 1), t = r.map(t, -1)), r.forEach((n, r, i, a) => {
				e = Math.min(e, i), t = Math.max(t, a);
			});
		}
		return e == 1e9 ? null : {
			from: e,
			to: t
		};
	}
	addStep(e, t) {
		this.docs.push(this.doc), this.steps.push(e), this.mapping.appendMap(e.getMap()), this.doc = t;
	}
	replace(e, t = e, n = h.empty) {
		let r = tn(this.doc, e, t, n);
		return r && this.step(r), this;
	}
	replaceWith(e, t, n) {
		return this.replace(e, t, new h(l.from(n), 0, 0));
	}
	delete(e, t) {
		return this.replace(e, t, h.empty);
	}
	insert(e, t) {
		return this.replaceWith(e, e, t);
	}
	replaceRange(e, t, n) {
		return pn(this, e, t, n), this;
	}
	replaceRangeWith(e, t, n) {
		return hn(this, e, t, n), this;
	}
	deleteRange(e, t) {
		return gn(this, e, t), this;
	}
	lift(e, t) {
		return Ft(this, e, t), this;
	}
	join(e, t = 1) {
		return Qt(this, e, t), this;
	}
	wrap(e, t) {
		return Bt(this, e, t), this;
	}
	setBlockType(e, t = e, n, r = null) {
		return Vt(this, e, t, n, r), this;
	}
	setNodeMarkup(e, t, n = null, r) {
		return Gt(this, e, t, n, r), this;
	}
	setNodeAttribute(e, t, n) {
		return this.step(new vn(e, t, n)), this;
	}
	setDocAttribute(e, t) {
		return this.step(new yn(e, t)), this;
	}
	addNodeMark(e, t) {
		return this.step(new Tt(e, t)), this;
	}
	removeNodeMark(e, t) {
		let n = this.doc.nodeAt(e);
		if (!n) throw RangeError("No node at position " + e);
		if (t instanceof p) t.isInSet(n.marks) && this.step(new Et(e, t));
		else {
			let r = n.marks, i, a = [];
			for (; i = t.isInSet(r);) a.push(new Et(e, i)), r = i.removeFromSet(r);
			for (let e = a.length - 1; e >= 0; e--) this.step(a[e]);
		}
		return this;
	}
	split(e, t = 1, n) {
		return qt(this, e, t, n), this;
	}
	addMark(e, t, n) {
		return At(this, e, t, n), this;
	}
	removeMark(e, t, n) {
		return jt(this, e, t, n), this;
	}
	clearIncompatible(e, t, n) {
		return Mt(this, e, t, n), this;
	}
}, Sn = Object.create(null), C = class {
	constructor(e, t, n) {
		this.$anchor = e, this.$head = t, this.ranges = n || [new Cn(e.min(t), e.max(t))];
	}
	get anchor() {
		return this.$anchor.pos;
	}
	get head() {
		return this.$head.pos;
	}
	get from() {
		return this.$from.pos;
	}
	get to() {
		return this.$to.pos;
	}
	get $from() {
		return this.ranges[0].$from;
	}
	get $to() {
		return this.ranges[0].$to;
	}
	get empty() {
		let e = this.ranges;
		for (let t = 0; t < e.length; t++) if (e[t].$from.pos != e[t].$to.pos) return !1;
		return !0;
	}
	content() {
		return this.$from.doc.slice(this.from, this.to, !0);
	}
	replace(e, t = h.empty) {
		let n = t.content.lastChild, r = null;
		for (let e = 0; e < t.openEnd; e++) r = n, n = n.lastChild;
		let i = e.steps.length, a = this.ranges;
		for (let o = 0; o < a.length; o++) {
			let { $from: s, $to: c } = a[o], l = e.mapping.slice(i);
			e.replaceRange(l.map(s.pos), l.map(c.pos), o ? h.empty : t), o == 0 && jn(e, i, (n ? n.isInline : r && r.isTextblock) ? -1 : 1);
		}
	}
	replaceWith(e, t) {
		let n = e.steps.length, r = this.ranges;
		for (let i = 0; i < r.length; i++) {
			let { $from: a, $to: o } = r[i], s = e.mapping.slice(n), c = s.map(a.pos), l = s.map(o.pos);
			i ? e.deleteRange(c, l) : (e.replaceRangeWith(c, l, t), jn(e, n, t.isInline ? -1 : 1));
		}
	}
	static findFrom(e, t, n = !1) {
		let r = e.parent.inlineContent ? new w(e) : An(e.node(0), e.parent, e.pos, e.index(), t, n);
		if (r) return r;
		for (let r = e.depth - 1; r >= 0; r--) {
			let i = t < 0 ? An(e.node(0), e.node(r), e.before(r + 1), e.index(r), t, n) : An(e.node(0), e.node(r), e.after(r + 1), e.index(r) + 1, t, n);
			if (i) return i;
		}
		return null;
	}
	static near(e, t = 1) {
		return this.findFrom(e, t) || this.findFrom(e, -t) || new On(e.node(0));
	}
	static atStart(e) {
		return An(e, e, 0, 0, 1) || new On(e);
	}
	static atEnd(e) {
		return An(e, e, e.content.size, e.childCount, -1) || new On(e);
	}
	static fromJSON(e, t) {
		if (!t || !t.type) throw RangeError("Invalid input for Selection.fromJSON");
		let n = Sn[t.type];
		if (!n) throw RangeError(`No selection type ${t.type} defined`);
		return n.fromJSON(e, t);
	}
	static jsonID(e, t) {
		if (e in Sn) throw RangeError("Duplicate use of selection JSON ID " + e);
		return Sn[e] = t, t.prototype.jsonID = e, t;
	}
	getBookmark() {
		return w.between(this.$anchor, this.$head).getBookmark();
	}
};
C.prototype.visible = !0;
var Cn = class {
	constructor(e, t) {
		this.$from = e, this.$to = t;
	}
}, wn = !1;
function Tn(e) {
	!wn && !e.parent.inlineContent && (wn = !0, console.warn("TextSelection endpoint not pointing into a node with inline content (" + e.parent.type.name + ")"));
}
var w = class e extends C {
	constructor(e, t = e) {
		Tn(e), Tn(t), super(e, t);
	}
	get $cursor() {
		return this.$anchor.pos == this.$head.pos ? this.$head : null;
	}
	map(t, n) {
		let r = t.resolve(n.map(this.head));
		if (!r.parent.inlineContent) return C.near(r);
		let i = t.resolve(n.map(this.anchor));
		return new e(i.parent.inlineContent ? i : r, r);
	}
	replace(e, t = h.empty) {
		if (super.replace(e, t), t == h.empty) {
			let t = this.$from.marksAcross(this.$to);
			t && e.ensureMarks(t);
		}
	}
	eq(t) {
		return t instanceof e && t.anchor == this.anchor && t.head == this.head;
	}
	getBookmark() {
		return new En(this.anchor, this.head);
	}
	toJSON() {
		return {
			type: "text",
			anchor: this.anchor,
			head: this.head
		};
	}
	static fromJSON(t, n) {
		if (typeof n.anchor != "number" || typeof n.head != "number") throw RangeError("Invalid input for TextSelection.fromJSON");
		return new e(t.resolve(n.anchor), t.resolve(n.head));
	}
	static create(e, t, n = t) {
		let r = e.resolve(t);
		return new this(r, n == t ? r : e.resolve(n));
	}
	static between(t, n, r) {
		let i = t.pos - n.pos;
		if ((!r || i) && (r = i >= 0 ? 1 : -1), !n.parent.inlineContent) {
			let e = C.findFrom(n, r, !0) || C.findFrom(n, -r, !0);
			if (e) n = e.$head;
			else return C.near(n, r);
		}
		return t.parent.inlineContent || (i == 0 ? t = n : (t = (C.findFrom(t, -r, !0) || C.findFrom(t, r, !0)).$anchor, t.pos < n.pos != i < 0 && (t = n))), new e(t, n);
	}
};
C.jsonID("text", w);
var En = class e {
	constructor(e, t) {
		this.anchor = e, this.head = t;
	}
	map(t) {
		return new e(t.map(this.anchor), t.map(this.head));
	}
	resolve(e) {
		return w.between(e.resolve(this.anchor), e.resolve(this.head));
	}
}, T = class e extends C {
	constructor(e) {
		let t = e.nodeAfter, n = e.node(0).resolve(e.pos + t.nodeSize);
		super(e, n), this.node = t;
	}
	map(t, n) {
		let { deleted: r, pos: i } = n.mapResult(this.anchor), a = t.resolve(i);
		return r ? C.near(a) : new e(a);
	}
	content() {
		return new h(l.from(this.node), 0, 0);
	}
	eq(t) {
		return t instanceof e && t.anchor == this.anchor;
	}
	toJSON() {
		return {
			type: "node",
			anchor: this.anchor
		};
	}
	getBookmark() {
		return new Dn(this.anchor);
	}
	static fromJSON(t, n) {
		if (typeof n.anchor != "number") throw RangeError("Invalid input for NodeSelection.fromJSON");
		return new e(t.resolve(n.anchor));
	}
	static create(t, n) {
		return new e(t.resolve(n));
	}
	static isSelectable(e) {
		return !e.isText && e.type.spec.selectable !== !1;
	}
};
T.prototype.visible = !1, C.jsonID("node", T);
var Dn = class e {
	constructor(e) {
		this.anchor = e;
	}
	map(t) {
		let { deleted: n, pos: r } = t.mapResult(this.anchor);
		return n ? new En(r, r) : new e(r);
	}
	resolve(e) {
		let t = e.resolve(this.anchor), n = t.nodeAfter;
		return n && T.isSelectable(n) ? new T(t) : C.near(t);
	}
}, On = class e extends C {
	constructor(e) {
		super(e.resolve(0), e.resolve(e.content.size));
	}
	replace(e, t = h.empty) {
		if (t == h.empty) {
			e.delete(0, e.doc.content.size);
			let t = C.atStart(e.doc);
			t.eq(e.selection) || e.setSelection(t);
		} else super.replace(e, t);
	}
	toJSON() {
		return { type: "all" };
	}
	static fromJSON(t) {
		return new e(t);
	}
	map(t) {
		return new e(t);
	}
	eq(t) {
		return t instanceof e;
	}
	getBookmark() {
		return kn;
	}
};
C.jsonID("all", On);
var kn = {
	map() {
		return this;
	},
	resolve(e) {
		return new On(e);
	}
};
function An(e, t, n, r, i, a = !1) {
	if (t.inlineContent) return w.create(e, n);
	for (let o = r - (i > 0 ? 0 : 1); i > 0 ? o < t.childCount : o >= 0; o += i) {
		let r = t.child(o);
		if (!r.isAtom) {
			let t = An(e, r, n + i, i < 0 ? r.childCount : 0, i, a);
			if (t) return t;
		} else if (!a && T.isSelectable(r)) return T.create(e, n - (i < 0 ? r.nodeSize : 0));
		n += r.nodeSize * i;
	}
	return null;
}
function jn(e, t, n) {
	let r = e.steps.length - 1;
	if (r < t) return;
	let i = e.steps[r];
	if (!(i instanceof Dt || i instanceof Ot)) return;
	let a = e.mapping.maps[r], o;
	a.forEach((e, t, n, r) => {
		o ?? (o = r);
	}), e.setSelection(C.near(e.doc.resolve(o), n));
}
var Mn = 1, Nn = 2, Pn = 4, Fn = class extends xn {
	constructor(e) {
		super(e.doc), this.curSelectionFor = 0, this.updated = 0, this.meta = Object.create(null), this.time = Date.now(), this.curSelection = e.selection, this.storedMarks = e.storedMarks;
	}
	get selection() {
		return this.curSelectionFor < this.steps.length && (this.curSelection = this.curSelection.map(this.doc, this.mapping.slice(this.curSelectionFor)), this.curSelectionFor = this.steps.length), this.curSelection;
	}
	setSelection(e) {
		if (e.$from.doc != this.doc) throw RangeError("Selection passed to setSelection must point at the current document");
		return this.curSelection = e, this.curSelectionFor = this.steps.length, this.updated = (this.updated | Mn) & -3, this.storedMarks = null, this;
	}
	get selectionSet() {
		return (this.updated & Mn) > 0;
	}
	setStoredMarks(e) {
		return this.storedMarks = e, this.updated |= Nn, this;
	}
	ensureMarks(e) {
		return p.sameSet(this.storedMarks || this.selection.$from.marks(), e) || this.setStoredMarks(e), this;
	}
	addStoredMark(e) {
		return this.ensureMarks(e.addToSet(this.storedMarks || this.selection.$head.marks()));
	}
	removeStoredMark(e) {
		return this.ensureMarks(e.removeFromSet(this.storedMarks || this.selection.$head.marks()));
	}
	get storedMarksSet() {
		return (this.updated & Nn) > 0;
	}
	addStep(e, t) {
		super.addStep(e, t), this.updated &= -3, this.storedMarks = null;
	}
	setTime(e) {
		return this.time = e, this;
	}
	replaceSelection(e) {
		return this.selection.replace(this, e), this;
	}
	replaceSelectionWith(e, t = !0) {
		let n = this.selection;
		return t && (e = e.mark(this.storedMarks || (n.empty ? n.$from.marks() : n.$from.marksAcross(n.$to) || p.none))), n.replaceWith(this, e), this;
	}
	deleteSelection() {
		return this.selection.replace(this), this;
	}
	insertText(e, t, n) {
		let r = this.doc.type.schema;
		if (t == null) return e ? this.replaceSelectionWith(r.text(e), !0) : this.deleteSelection();
		{
			if (n ?? (n = t), !e) return this.deleteRange(t, n);
			let i = this.storedMarks;
			if (!i) {
				let e = this.doc.resolve(t);
				i = n == t ? e.marks() : e.marksAcross(this.doc.resolve(n));
			}
			return this.replaceRangeWith(t, n, r.text(e, i)), !this.selection.empty && this.selection.to == t + e.length && this.setSelection(C.near(this.selection.$to)), this;
		}
	}
	setMeta(e, t) {
		return this.meta[typeof e == "string" ? e : e.key] = t, this;
	}
	getMeta(e) {
		return this.meta[typeof e == "string" ? e : e.key];
	}
	get isGeneric() {
		for (let e in this.meta) return !1;
		return !0;
	}
	scrollIntoView() {
		return this.updated |= Pn, this;
	}
	get scrolledIntoView() {
		return (this.updated & Pn) > 0;
	}
};
function In(e, t) {
	return !t || !e ? e : e.bind(t);
}
var Ln = class {
	constructor(e, t, n) {
		this.name = e, this.init = In(t.init, n), this.apply = In(t.apply, n);
	}
}, Rn = [
	new Ln("doc", {
		init(e) {
			return e.doc || e.schema.topNodeType.createAndFill();
		},
		apply(e) {
			return e.doc;
		}
	}),
	new Ln("selection", {
		init(e, t) {
			return e.selection || C.atStart(t.doc);
		},
		apply(e) {
			return e.selection;
		}
	}),
	new Ln("storedMarks", {
		init(e) {
			return e.storedMarks || null;
		},
		apply(e, t, n, r) {
			return r.selection.$cursor ? e.storedMarks : null;
		}
	}),
	new Ln("scrollToSelection", {
		init() {
			return 0;
		},
		apply(e, t) {
			return e.scrolledIntoView ? t + 1 : t;
		}
	})
], zn = class {
	constructor(e, t) {
		this.schema = e, this.plugins = [], this.pluginsByKey = Object.create(null), this.fields = Rn.slice(), t && t.forEach((e) => {
			if (this.pluginsByKey[e.key]) throw RangeError("Adding different instances of a keyed plugin (" + e.key + ")");
			this.plugins.push(e), this.pluginsByKey[e.key] = e, e.spec.state && this.fields.push(new Ln(e.key, e.spec.state, e));
		});
	}
}, Bn = class e {
	constructor(e) {
		this.config = e;
	}
	get schema() {
		return this.config.schema;
	}
	get plugins() {
		return this.config.plugins;
	}
	apply(e) {
		return this.applyTransaction(e).state;
	}
	filterTransaction(e, t = -1) {
		for (let n = 0; n < this.config.plugins.length; n++) if (n != t) {
			let t = this.config.plugins[n];
			if (t.spec.filterTransaction && !t.spec.filterTransaction.call(t, e, this)) return !1;
		}
		return !0;
	}
	applyTransaction(e) {
		if (!this.filterTransaction(e)) return {
			state: this,
			transactions: []
		};
		let t = [e], n = this.applyInner(e), r = null;
		for (;;) {
			let i = !1;
			for (let a = 0; a < this.config.plugins.length; a++) {
				let o = this.config.plugins[a];
				if (o.spec.appendTransaction) {
					let s = r ? r[a].n : 0, c = r ? r[a].state : this, l = s < t.length && o.spec.appendTransaction.call(o, s ? t.slice(s) : t, c, n);
					if (l && n.filterTransaction(l, a)) {
						if (l.setMeta("appendedTransaction", e), !r) {
							r = [];
							for (let e = 0; e < this.config.plugins.length; e++) r.push(e < a ? {
								state: n,
								n: t.length
							} : {
								state: this,
								n: 0
							});
						}
						t.push(l), n = n.applyInner(l), i = !0;
					}
					r && (r[a] = {
						state: n,
						n: t.length
					});
				}
			}
			if (!i) return {
				state: n,
				transactions: t
			};
		}
	}
	applyInner(t) {
		if (!t.before.eq(this.doc)) throw RangeError("Applying a mismatched transaction");
		let n = new e(this.config), r = this.config.fields;
		for (let e = 0; e < r.length; e++) {
			let i = r[e];
			n[i.name] = i.apply(t, this[i.name], this, n);
		}
		return n;
	}
	get tr() {
		return new Fn(this);
	}
	static create(t) {
		let n = new zn(t.doc ? t.doc.type.schema : t.schema, t.plugins), r = new e(n);
		for (let e = 0; e < n.fields.length; e++) r[n.fields[e].name] = n.fields[e].init(t, r);
		return r;
	}
	reconfigure(t) {
		let n = new zn(this.schema, t.plugins), r = n.fields, i = new e(n);
		for (let e = 0; e < r.length; e++) {
			let n = r[e].name;
			i[n] = this.hasOwnProperty(n) ? this[n] : r[e].init(t, i);
		}
		return i;
	}
	toJSON(e) {
		let t = {
			doc: this.doc.toJSON(),
			selection: this.selection.toJSON()
		};
		if (this.storedMarks && (t.storedMarks = this.storedMarks.map((e) => e.toJSON())), e && typeof e == "object") for (let n in e) {
			if (n == "doc" || n == "selection") throw RangeError("The JSON fields `doc` and `selection` are reserved");
			let r = e[n], i = r.spec.state;
			i && i.toJSON && (t[n] = i.toJSON.call(r, this[r.key]));
		}
		return t;
	}
	static fromJSON(t, n, r) {
		if (!n) throw RangeError("Invalid input for EditorState.fromJSON");
		if (!t.schema) throw RangeError("Required config field 'schema' missing");
		let i = new zn(t.schema, t.plugins), a = new e(i);
		return i.fields.forEach((e) => {
			if (e.name == "doc") a.doc = de.fromJSON(t.schema, n.doc);
			else if (e.name == "selection") a.selection = C.fromJSON(a.doc, n.selection);
			else if (e.name == "storedMarks") n.storedMarks && (a.storedMarks = n.storedMarks.map(t.schema.markFromJSON));
			else {
				if (r) for (let i in r) {
					let o = r[i], s = o.spec.state;
					if (o.key == e.name && s && s.fromJSON && Object.prototype.hasOwnProperty.call(n, i)) {
						a[e.name] = s.fromJSON.call(o, t, n[i], a);
						return;
					}
				}
				a[e.name] = e.init(t, a);
			}
		}), a;
	}
};
function Vn(e, t, n) {
	for (let r in e) {
		let i = e[r];
		i instanceof Function ? i = i.bind(t) : r == "handleDOMEvents" && (i = Vn(i, t, {})), n[r] = i;
	}
	return n;
}
var E = class {
	constructor(e) {
		this.spec = e, this.props = {}, e.props && Vn(e.props, this, this.props), this.key = e.key ? e.key.key : Un("plugin");
	}
	getState(e) {
		return e[this.key];
	}
}, Hn = Object.create(null);
function Un(e) {
	return e in Hn ? e + "$" + ++Hn[e] : (Hn[e] = 0, e + "$");
}
var D = class {
	constructor(e = "key") {
		this.key = Un(e);
	}
	get(e) {
		return e.config.pluginsByKey[this.key];
	}
	getState(e) {
		return e[this.key];
	}
}, Wn = (e, t) => !e.selection.empty && (t && t(e.tr.deleteSelection().scrollIntoView()), !0);
function Gn(e, t) {
	let { $cursor: n } = e.selection;
	return !n || (t ? !t.endOfTextblock("backward", e) : n.parentOffset > 0) ? null : n;
}
var Kn = (e, t, n) => {
	let r = Gn(e, n);
	if (!r) return !1;
	let i = Qn(r);
	if (!i) {
		let n = r.blockRange(), i = n && Pt(n);
		return i != null && (t && t(e.tr.lift(n, i).scrollIntoView()), !0);
	}
	let a = i.nodeBefore;
	if (gr(e, i, t, -1)) return !0;
	if (r.parent.content.size == 0 && (Xn(a, "end") || T.isSelectable(a))) for (let n = r.depth;; n--) {
		let o = tn(e.doc, r.before(n), r.after(n), h.empty);
		if (o && o.slice.size < o.to - o.from) {
			if (t) {
				let n = e.tr.step(o);
				n.setSelection(Xn(a, "end") ? C.findFrom(n.doc.resolve(n.mapping.map(i.pos, -1)), -1) : T.create(n.doc, i.pos - a.nodeSize)), t(n.scrollIntoView());
			}
			return !0;
		}
		if (n == 1 || r.node(n - 1).childCount > 1) break;
	}
	return a.isAtom && i.depth == r.depth - 1 ? (t && t(e.tr.delete(i.pos - a.nodeSize, i.pos).scrollIntoView()), !0) : !1;
}, qn = (e, t, n) => {
	let r = Gn(e, n);
	if (!r) return !1;
	let i = Qn(r);
	return i ? Yn(e, i, t) : !1;
}, Jn = (e, t, n) => {
	let r = $n(e, n);
	if (!r) return !1;
	let i = nr(r);
	return i ? Yn(e, i, t) : !1;
};
function Yn(e, t, n) {
	let r = t.nodeBefore, i = t.pos - 1;
	for (; !r.isTextblock; i--) {
		if (r.type.spec.isolating) return !1;
		let e = r.lastChild;
		if (!e) return !1;
		r = e;
	}
	let a = t.nodeAfter, o = t.pos + 1;
	for (; !a.isTextblock; o++) {
		if (a.type.spec.isolating) return !1;
		let e = a.firstChild;
		if (!e) return !1;
		a = e;
	}
	let s = tn(e.doc, i, o, h.empty);
	if (!s || s.from != i || s instanceof Dt && s.slice.size >= o - i) return !1;
	if (n) {
		let t = e.tr.step(s);
		t.setSelection(w.create(t.doc, i)), n(t.scrollIntoView());
	}
	return !0;
}
function Xn(e, t, n = !1) {
	for (let r = e; r; r = t == "start" ? r.firstChild : r.lastChild) {
		if (r.isTextblock) return !0;
		if (n && r.childCount != 1) return !1;
	}
	return !1;
}
var Zn = (e, t, n) => {
	let { $head: r, empty: i } = e.selection, a = r;
	if (!i) return !1;
	if (r.parent.isTextblock) {
		if (n ? !n.endOfTextblock("backward", e) : r.parentOffset > 0) return !1;
		a = Qn(r);
	}
	let o = a && a.nodeBefore;
	return !o || !T.isSelectable(o) ? !1 : (t && t(e.tr.setSelection(T.create(e.doc, a.pos - o.nodeSize)).scrollIntoView()), !0);
};
function Qn(e) {
	if (!e.parent.type.spec.isolating) for (let t = e.depth - 1; t >= 0; t--) {
		if (e.index(t) > 0) return e.doc.resolve(e.before(t + 1));
		if (e.node(t).type.spec.isolating) break;
	}
	return null;
}
function $n(e, t) {
	let { $cursor: n } = e.selection;
	return !n || (t ? !t.endOfTextblock("forward", e) : n.parentOffset < n.parent.content.size) ? null : n;
}
var er = (e, t, n) => {
	let r = $n(e, n);
	if (!r) return !1;
	let i = nr(r);
	if (!i) return !1;
	let a = i.nodeAfter;
	if (gr(e, i, t, 1)) return !0;
	if (r.parent.content.size == 0 && (Xn(a, "start") || T.isSelectable(a))) {
		let n = tn(e.doc, r.before(), r.after(), h.empty);
		if (n && n.slice.size < n.to - n.from) {
			if (t) {
				let r = e.tr.step(n);
				r.setSelection(Xn(a, "start") ? C.findFrom(r.doc.resolve(r.mapping.map(i.pos)), 1) : T.create(r.doc, r.mapping.map(i.pos))), t(r.scrollIntoView());
			}
			return !0;
		}
	}
	return a.isAtom && i.depth == r.depth - 1 ? (t && t(e.tr.delete(i.pos, i.pos + a.nodeSize).scrollIntoView()), !0) : !1;
}, tr = (e, t, n) => {
	let { $head: r, empty: i } = e.selection, a = r;
	if (!i) return !1;
	if (r.parent.isTextblock) {
		if (n ? !n.endOfTextblock("forward", e) : r.parentOffset < r.parent.content.size) return !1;
		a = nr(r);
	}
	let o = a && a.nodeAfter;
	return !o || !T.isSelectable(o) ? !1 : (t && t(e.tr.setSelection(T.create(e.doc, a.pos)).scrollIntoView()), !0);
};
function nr(e) {
	if (!e.parent.type.spec.isolating) for (let t = e.depth - 1; t >= 0; t--) {
		let n = e.node(t);
		if (e.index(t) + 1 < n.childCount) return e.doc.resolve(e.after(t + 1));
		if (n.type.spec.isolating) break;
	}
	return null;
}
var rr = (e, t) => {
	let n = e.selection, r = n instanceof T, i;
	if (r) {
		if (n.node.isTextblock || !Jt(e.doc, n.from)) return !1;
		i = n.from;
	} else if (i = Zt(e.doc, n.from, -1), i == null) return !1;
	if (t) {
		let n = e.tr.join(i);
		r && n.setSelection(T.create(n.doc, i - e.doc.resolve(i).nodeBefore.nodeSize)), t(n.scrollIntoView());
	}
	return !0;
}, ir = (e, t) => {
	let n = e.selection, r;
	if (n instanceof T) {
		if (n.node.isTextblock || !Jt(e.doc, n.to)) return !1;
		r = n.to;
	} else if (r = Zt(e.doc, n.to, 1), r == null) return !1;
	return t && t(e.tr.join(r).scrollIntoView()), !0;
}, ar = (e, t) => {
	let { $from: n, $to: r } = e.selection, i = n.blockRange(r), a = i && Pt(i);
	return a != null && (t && t(e.tr.lift(i, a).scrollIntoView()), !0);
}, or = (e, t) => {
	let { $head: n, $anchor: r } = e.selection;
	return !n.parent.type.spec.code || !n.sameParent(r) ? !1 : (t && t(e.tr.insertText("\n").scrollIntoView()), !0);
};
function sr(e) {
	for (let t = 0; t < e.edgeCount; t++) {
		let { type: n } = e.edge(t);
		if (n.isTextblock && !n.hasRequiredAttrs()) return n;
	}
	return null;
}
var cr = (e, t) => {
	let { $head: n, $anchor: r } = e.selection;
	if (!n.parent.type.spec.code || !n.sameParent(r)) return !1;
	let i = n.node(-1), a = n.indexAfter(-1), o = sr(i.contentMatchAt(a));
	if (!o || !i.canReplaceWith(a, a, o)) return !1;
	if (t) {
		let r = n.after(), i = e.tr.replaceWith(r, r, o.createAndFill());
		i.setSelection(C.near(i.doc.resolve(r), 1)), t(i.scrollIntoView());
	}
	return !0;
}, lr = (e, t) => {
	let n = e.selection, { $from: r, $to: i } = n;
	if (n instanceof On || r.parent.inlineContent || i.parent.inlineContent) return !1;
	let a = sr(i.parent.contentMatchAt(i.indexAfter()));
	if (!a || !a.isTextblock) return !1;
	if (t) {
		let n = (!r.parentOffset && i.index() < i.parent.childCount ? r : i).pos, o = e.tr.insert(n, a.createAndFill());
		o.setSelection(w.create(o.doc, n + 1)), t(o.scrollIntoView());
	}
	return !0;
}, ur = (e, t) => {
	let { $cursor: n } = e.selection;
	if (!n || n.parent.content.size) return !1;
	if (n.depth > 1 && n.after() != n.end(-1)) {
		let r = n.before();
		if (Kt(e.doc, r)) return t && t(e.tr.split(r).scrollIntoView()), !0;
	}
	let r = n.blockRange(), i = r && Pt(r);
	return i != null && (t && t(e.tr.lift(r, i).scrollIntoView()), !0);
};
function dr(e) {
	return (t, n) => {
		let { $from: r, $to: i } = t.selection;
		if (t.selection instanceof T && t.selection.node.isBlock) return !r.parentOffset || !Kt(t.doc, r.pos) ? !1 : (n && n(t.tr.split(r.pos).scrollIntoView()), !0);
		if (!r.depth) return !1;
		let a = [], o, s, c = !1, l = !1;
		for (let t = r.depth;; t--) if (r.node(t).isBlock) {
			c = r.end(t) == r.pos + (r.depth - t), l = r.start(t) == r.pos - (r.depth - t), s = sr(r.node(t - 1).contentMatchAt(r.indexAfter(t - 1)));
			let n = e && e(i.parent, c, r);
			a.unshift(n || (c && s ? { type: s } : null)), o = t;
			break;
		} else {
			if (t == 1) return !1;
			a.unshift(null);
		}
		let u = t.tr;
		(t.selection instanceof w || t.selection instanceof On) && u.deleteSelection();
		let d = u.mapping.map(r.pos), f = Kt(u.doc, d, a.length, a);
		if (f || (a[0] = s ? { type: s } : null, f = Kt(u.doc, d, a.length, a)), !f) return !1;
		if (u.split(d, a.length, a), !c && l && r.node(o).type != s) {
			let e = u.mapping.map(r.before(o)), t = u.doc.resolve(e);
			s && r.node(o - 1).canReplaceWith(t.index(), t.index() + 1, s) && u.setNodeMarkup(u.mapping.map(r.before(o)), s);
		}
		return n && n(u.scrollIntoView()), !0;
	};
}
var fr = dr(), pr = (e, t) => {
	let { $from: n, to: r } = e.selection, i, a = n.sharedDepth(r);
	return a != 0 && (i = n.before(a), t && t(e.tr.setSelection(T.create(e.doc, i))), !0);
}, mr = (e, t) => (t && t(e.tr.setSelection(new On(e.doc))), !0);
function hr(e, t, n) {
	let r = t.nodeBefore, i = t.nodeAfter, a = t.index();
	return !r || !i || !r.type.compatibleContent(i.type) ? !1 : !r.content.size && t.parent.canReplace(a - 1, a) ? (n && n(e.tr.delete(t.pos - r.nodeSize, t.pos).scrollIntoView()), !0) : !t.parent.canReplace(a, a + 1) || !(i.isTextblock || Jt(e.doc, t.pos)) ? !1 : (n && n(e.tr.join(t.pos).scrollIntoView()), !0);
}
function gr(e, t, n, r) {
	let i = t.nodeBefore, a = t.nodeAfter, o, s, c = i.type.spec.isolating || a.type.spec.isolating;
	if (!c && hr(e, t, n)) return !0;
	let u = !c && t.parent.canReplace(t.index(), t.index() + 1);
	if (u && (o = (s = i.contentMatchAt(i.childCount)).findWrapping(a.type)) && s.matchType(o[0] || a.type).validEnd) {
		if (n) {
			let r = t.pos + a.nodeSize, s = l.empty;
			for (let e = o.length - 1; e >= 0; e--) s = l.from(o[e].create(null, s));
			s = l.from(i.copy(s));
			let c = e.tr.step(new Ot(t.pos - 1, r, t.pos, r, new h(s, 1, 0), o.length, !0)), u = c.doc.resolve(r + 2 * o.length);
			u.nodeAfter && u.nodeAfter.type == i.type && Jt(c.doc, u.pos) && c.join(u.pos), n(c.scrollIntoView());
		}
		return !0;
	}
	let d = a.type.spec.isolating || r > 0 && c ? null : C.findFrom(t, 1), f = d && d.$from.blockRange(d.$to), p = f && Pt(f);
	if (p != null && p >= t.depth) return n && n(e.tr.lift(f, p).scrollIntoView()), !0;
	if (u && Xn(a, "start", !0) && Xn(i, "end")) {
		let r = i, o = [];
		for (; o.push(r), !r.isTextblock;) r = r.lastChild;
		let s = a, c = 1;
		for (; !s.isTextblock; s = s.firstChild) c++;
		if (r.canReplace(r.childCount, r.childCount, s.content)) {
			if (n) {
				let r = l.empty;
				for (let e = o.length - 1; e >= 0; e--) r = l.from(o[e].copy(r));
				n(e.tr.step(new Ot(t.pos - o.length, t.pos + a.nodeSize, t.pos + c, t.pos + a.nodeSize - c, new h(r, o.length, 0), 0, !0)).scrollIntoView());
			}
			return !0;
		}
	}
	return !1;
}
function _r(e) {
	return function(t, n) {
		let r = t.selection, i = e < 0 ? r.$from : r.$to, a = i.depth;
		for (; i.node(a).isInline;) {
			if (!a) return !1;
			a--;
		}
		return i.node(a).isTextblock ? (n && n(t.tr.setSelection(w.create(t.doc, e < 0 ? i.start(a) : i.end(a)))), !0) : !1;
	};
}
var vr = _r(-1), yr = _r(1);
function br(e, t = null) {
	return function(n, r) {
		let { $from: i, $to: a } = n.selection, o = i.blockRange(a), s = o && It(o, e, t);
		return s ? (r && r(n.tr.wrap(o, s).scrollIntoView()), !0) : !1;
	};
}
function xr(e, t = null) {
	return function(n, r) {
		let i = !1;
		for (let r = 0; r < n.selection.ranges.length && !i; r++) {
			let { $from: { pos: a }, $to: { pos: o } } = n.selection.ranges[r];
			n.doc.nodesBetween(a, o, (r, a) => {
				if (i) return !1;
				if (r.isTextblock && !r.hasMarkup(e, t)) {
					if (r.type == e) i = !0;
					else {
						let t = n.doc.resolve(a), r = t.index();
						i = t.parent.canReplaceWith(r, r + 1, e);
					}
				}
			});
		}
		if (!i) return !1;
		if (r) {
			let i = n.tr;
			for (let r = 0; r < n.selection.ranges.length; r++) {
				let { $from: { pos: a }, $to: { pos: o } } = n.selection.ranges[r];
				i.setBlockType(a, o, e, t);
			}
			r(i.scrollIntoView());
		}
		return !0;
	};
}
function Sr(...e) {
	return function(t, n, r) {
		for (let i = 0; i < e.length; i++) if (e[i](t, n, r)) return !0;
		return !1;
	};
}
var Cr = Sr(Wn, Kn, Zn), wr = Sr(Wn, er, tr), Tr = {
	Enter: Sr(or, lr, ur, fr),
	"Mod-Enter": cr,
	Backspace: Cr,
	"Mod-Backspace": Cr,
	"Shift-Backspace": Cr,
	Delete: wr,
	"Mod-Delete": wr,
	"Mod-a": mr
}, Er = {
	"Ctrl-h": Tr.Backspace,
	"Alt-Backspace": Tr["Mod-Backspace"],
	"Ctrl-d": Tr.Delete,
	"Ctrl-Alt-Backspace": Tr["Mod-Delete"],
	"Alt-Delete": Tr["Mod-Delete"],
	"Alt-d": Tr["Mod-Delete"],
	"Ctrl-a": vr,
	"Ctrl-e": yr
};
for (let e in Tr) Er[e] = Tr[e];
typeof navigator < "u" ? /Mac|iP(hone|[oa]d)/.test(navigator.platform) : typeof os < "u" && os.platform && os.platform();
//#endregion
//#region node_modules/@tiptap/pm/node_modules/prosemirror-schema-list/dist/index.js
function Dr(e, t = null) {
	return function(n, r) {
		let { $from: i, $to: a } = n.selection, o = i.blockRange(a);
		if (!o) return !1;
		let s = r ? n.tr : null;
		return Or(s, o, e, t) ? (r && r(s.scrollIntoView()), !0) : !1;
	};
}
function Or(e, t, n, r = null) {
	let i = !1, a = t, o = t.$from.doc;
	if (t.depth >= 2 && t.$from.node(t.depth - 1).type.compatibleContent(n) && t.startIndex == 0) {
		if (t.$from.index(t.depth - 1) == 0) return !1;
		let e = o.resolve(t.start - 2);
		a = new le(e, e, t.depth), t.endIndex < t.parent.childCount && (t = new le(t.$from, o.resolve(t.$to.end(t.depth)), t.depth)), i = !0;
	}
	let s = It(a, n, r, t);
	return s ? (e && kr(e, t, s, i, n), !0) : !1;
}
function kr(e, t, n, r, i) {
	let a = l.empty;
	for (let e = n.length - 1; e >= 0; e--) a = l.from(n[e].type.create(n[e].attrs, a));
	e.step(new Ot(t.start - (r ? 2 : 0), t.end, t.start, t.end, new h(a, 0, 0), n.length, !0));
	let o = 0;
	for (let e = 0; e < n.length; e++) n[e].type == i && (o = e + 1);
	let s = n.length - o, c = t.start + n.length - (r ? 2 : 0), u = t.parent;
	for (let n = t.startIndex, r = t.endIndex, i = !0; n < r; n++, i = !1) !i && Kt(e.doc, c, s) && (e.split(c, s), c += 2 * s), c += u.child(n).nodeSize;
	return e;
}
function Ar(e) {
	return function(t, n) {
		let { $from: r, $to: i } = t.selection, a = r.blockRange(i, (t) => t.childCount > 0 && t.firstChild.type == e);
		return a ? n ? r.node(a.depth - 1).type == e ? jr(t, n, e, a) : Mr(t, n, a) : !0 : !1;
	};
}
function jr(e, t, n, r) {
	let i = e.tr, a = r.end, o = r.$to.end(r.depth);
	a < o && (i.step(new Ot(a - 1, o, a, o, new h(l.from(n.create(null, r.parent.copy())), 1, 0), 1, !0)), r = new le(i.doc.resolve(r.$from.pos), i.doc.resolve(o), r.depth));
	let s = Pt(r);
	if (s == null) return !1;
	i.lift(r, s);
	let c = i.doc.resolve(i.mapping.map(a, -1) - 1);
	return Jt(i.doc, c.pos) && c.nodeBefore.type == c.nodeAfter.type && i.join(c.pos), t(i.scrollIntoView()), !0;
}
function Mr(e, t, n) {
	let r = e.tr, i = n.parent;
	for (let e = n.end, t = n.endIndex - 1, a = n.startIndex; t > a; t--) e -= i.child(t).nodeSize, r.delete(e - 1, e + 1);
	let a = r.doc.resolve(n.start), o = a.nodeAfter;
	if (r.mapping.map(n.end) != n.start + a.nodeAfter.nodeSize) return !1;
	let s = n.startIndex == 0, c = n.endIndex == i.childCount, u = a.node(-1), d = a.index(-1);
	if (!u.canReplace(d + +!s, d + 1, o.content.append(c ? l.empty : l.from(i)))) return !1;
	let f = a.pos, p = f + o.nodeSize;
	return r.step(new Ot(f - +!!s, p + +!!c, f + 1, p - 1, new h((s ? l.empty : l.from(i.copy(l.empty))).append(c ? l.empty : l.from(i.copy(l.empty))), +!s, +!c), +!s)), t(r.scrollIntoView()), !0;
}
function Nr(e) {
	return function(t, n) {
		let { $from: r, $to: i } = t.selection, a = r.blockRange(i, (t) => t.childCount > 0 && t.firstChild.type == e);
		if (!a) return !1;
		let o = a.startIndex;
		if (o == 0) return !1;
		let s = a.parent, c = s.child(o - 1);
		if (c.type != e) return !1;
		if (n) {
			let r = c.lastChild && c.lastChild.type == s.type, i = l.from(r ? e.create() : null), o = new h(l.from(e.create(null, l.from(s.type.create(null, i)))), r ? 3 : 1, 0), u = a.start, d = a.end;
			n(t.tr.step(new Ot(u - (r ? 3 : 1), d, u, d, o, 1, !0)).scrollIntoView());
		}
		return !0;
	};
}
//#endregion
//#region node_modules/prosemirror-view/dist/index.js
var Pr = function(e) {
	for (var t = 0;; t++) if (e = e.previousSibling, !e) return t;
}, Fr = function(e) {
	let t = e.assignedSlot || e.parentNode;
	return t && t.nodeType == 11 ? t.host : t;
}, Ir = null, Lr = function(e, t, n) {
	let r = Ir || (Ir = document.createRange());
	return r.setEnd(e, n ?? e.nodeValue.length), r.setStart(e, t || 0), r;
}, Rr = function() {
	Ir = null;
}, zr = function(e, t, n, r) {
	return n && (Vr(e, t, n, r, -1) || Vr(e, t, n, r, 1));
}, Br = /^(img|br|input|textarea|hr)$/i;
function Vr(e, t, n, r, i) {
	for (;;) {
		if (e == n && t == r) return !0;
		if (t == (i < 0 ? 0 : Hr(e))) {
			let n = e.parentNode;
			if (!n || !O(n) || qr(e) || Br.test(e.nodeName) || e.contentEditable == "false") return !1;
			t = Pr(e) + (i < 0 ? 0 : 1), e = n;
		} else if (O(e)) {
			let n = e.childNodes[t + (i < 0 ? -1 : 0)];
			if (O(n) && n.contentEditable == "false") {
				if (n.pmViewDesc?.ignoreForSelection) t += i;
				else return !1;
			} else e = n, t = i < 0 ? Hr(e) : 0;
		} else return !1;
	}
}
function Hr(e) {
	return e.nodeType == 3 ? e.nodeValue.length : e.childNodes.length;
}
function O(e) {
	return e.nodeType == 1;
}
function k(e) {
	return e.nodeType == 3;
}
function Ur(e, t) {
	return t ? e.contains(O(t) ? t : t.parentNode) : !1;
}
function Wr(e, t) {
	for (;;) {
		if (k(e) && t) return e;
		if (O(e) && t > 0) {
			if (e.contentEditable == "false") return null;
			e = e.childNodes[t - 1], t = Hr(e);
		} else if (e.parentNode && !qr(e)) t = Pr(e), e = e.parentNode;
		else return null;
	}
}
function Gr(e, t) {
	for (;;) {
		if (k(e) && t < e.nodeValue.length) return e;
		if (O(e) && t < e.childNodes.length) {
			if (e.contentEditable == "false") return null;
			e = e.childNodes[t], t = 0;
		} else if (e.parentNode && !qr(e)) t = Pr(e) + 1, e = e.parentNode;
		else return null;
	}
}
function Kr(e, t, n) {
	for (let r = t == 0, i = t == Hr(e); r || i;) {
		if (e == n) return !0;
		let t = Pr(e);
		if (e = e.parentNode, !e) return !1;
		r = r && t == 0, i = i && t == Hr(e);
	}
}
function qr(e) {
	let t;
	for (let n = e; n && !(t = n.pmViewDesc); n = n.parentNode);
	return t && t.node && t.node.isBlock && (t.dom == e || t.contentDOM == e);
}
var Jr = function(e) {
	return e.focusNode && zr(e.focusNode, e.focusOffset, e.anchorNode, e.anchorOffset);
};
function Yr(e, t) {
	let n = document.createEvent("Event");
	return n.initEvent("keydown", !0, !0), n.keyCode = e, n.key = n.code = t, n;
}
function Xr(e) {
	let t = e.activeElement;
	for (; t && t.shadowRoot;) t = t.shadowRoot.activeElement;
	return t;
}
function Zr(e, t, n) {
	if (e.caretPositionFromPoint) try {
		let r = e.caretPositionFromPoint(t, n);
		if (r) return {
			node: r.offsetNode,
			offset: Math.min(Hr(r.offsetNode), r.offset)
		};
	} catch {}
	if (e.caretRangeFromPoint) {
		let r = e.caretRangeFromPoint(t, n);
		if (r) return {
			node: r.startContainer,
			offset: Math.min(Hr(r.startContainer), r.startOffset)
		};
	}
}
var Qr = typeof navigator < "u" ? navigator : null, $r = typeof document < "u" ? document : null, ei = Qr && Qr.userAgent || "", ti = /Edge\/(\d+)/.exec(ei), ni = /MSIE \d/.exec(ei), ri = /Trident\/(?:[7-9]|\d{2,})\..*rv:(\d+)/.exec(ei), ii = !!(ni || ri || ti), ai = ni ? document.documentMode : ri ? +ri[1] : ti ? +ti[1] : 0, oi = !ii && /gecko\/(\d+)/i.test(ei);
oi && +(/Firefox\/(\d+)/.exec(ei) || [0, 0])[1];
var si = !ii && /Chrome\/(\d+)/.exec(ei), ci = !!si, li = si ? +si[1] : 0, ui = !ii && !!Qr && /Apple Computer/.test(Qr.vendor), di = ui && (/Mobile\/\w+/.test(ei) || !!Qr && Qr.maxTouchPoints > 2), fi = di || (Qr ? /Mac/.test(Qr.platform) : !1), pi = Qr ? /Win/.test(Qr.platform) : !1, mi = /Android \d/.test(ei), hi = !!$r && "webkitFontSmoothing" in $r.documentElement.style, gi = hi ? +(/\bAppleWebKit\/(\d+)/.exec(navigator.userAgent) || [0, 0])[1] : 0;
function _i(e) {
	let t = e.defaultView && e.defaultView.visualViewport;
	return t ? {
		left: 0,
		right: t.width,
		top: 0,
		bottom: t.height
	} : {
		left: 0,
		right: e.documentElement.clientWidth,
		top: 0,
		bottom: e.documentElement.clientHeight
	};
}
function vi(e, t) {
	return typeof e == "number" ? e : e[t];
}
function yi(e) {
	let t = e.getBoundingClientRect(), n = t.width / e.offsetWidth || 1, r = t.height / e.offsetHeight || 1;
	return {
		left: t.left,
		right: t.left + e.clientWidth * n,
		top: t.top,
		bottom: t.top + e.clientHeight * r
	};
}
function bi(e, t, n) {
	if (!Fi(t) && t.left == 0) return;
	let r = e.someProp("scrollThreshold") || 0, i = e.someProp("scrollMargin") || 5, a = e.dom.ownerDocument;
	for (let o = n || e.dom; o;) {
		if (!O(o)) {
			o = Fr(o);
			continue;
		}
		let e = o == a.body, n = e ? _i(a) : yi(o), s = 0, c = 0;
		if (t.top < n.top + vi(r, "top") ? c = -(n.top - t.top + vi(i, "top")) : t.bottom > n.bottom - vi(r, "bottom") && (c = t.bottom - t.top > n.bottom - n.top ? t.top + vi(i, "top") - n.top : t.bottom - n.bottom + vi(i, "bottom")), t.left < n.left + vi(r, "left") ? s = -(n.left - t.left + vi(i, "left")) : t.right > n.right - vi(r, "right") && (s = t.right - n.right + vi(i, "right")), s || c) {
			if (e) a.defaultView.scrollBy(s, c);
			else {
				let e = o.scrollLeft, n = o.scrollTop;
				c && (o.scrollTop += c), s && (o.scrollLeft += s);
				let r = o.scrollLeft - e, i = o.scrollTop - n;
				t = {
					left: t.left - r,
					top: t.top - i,
					right: t.right - r,
					bottom: t.bottom - i
				};
			}
		}
		let l = e ? "fixed" : getComputedStyle(o).position;
		if (/^(fixed|sticky)$/.test(l)) break;
		o = l == "absolute" ? o.offsetParent : Fr(o);
	}
}
function xi(e) {
	let t = e.dom.getBoundingClientRect(), n = Math.max(0, t.top), r, i;
	for (let a = (t.left + t.right) / 2, o = n + 1; o < Math.min(innerHeight, t.bottom); o += 5) {
		let t = e.root.elementFromPoint(a, o);
		if (!t || t == e.dom || !e.dom.contains(t)) continue;
		let s = t.getBoundingClientRect();
		if (s.top >= n - 20) {
			r = t, i = s.top;
			break;
		}
	}
	return {
		refDOM: r,
		refTop: i,
		stack: Si(e.dom)
	};
}
function Si(e) {
	let t = [], n = e.ownerDocument;
	for (let r = e; r && (t.push({
		dom: r,
		top: r.scrollTop,
		left: r.scrollLeft
	}), e != n); r = Fr(r));
	return t;
}
function Ci({ refDOM: e, refTop: t, stack: n }) {
	let r = e ? e.getBoundingClientRect().top : 0;
	wi(n, r == 0 ? 0 : r - t);
}
function wi(e, t) {
	for (let n = 0; n < e.length; n++) {
		let { dom: r, top: i, left: a } = e[n];
		r.scrollTop != i + t && (r.scrollTop = i + t), r.scrollLeft != a && (r.scrollLeft = a);
	}
}
var Ti = null;
function Ei(e) {
	if (e.setActive) return e.setActive();
	if (Ti) return e.focus(Ti);
	let t = Si(e);
	e.focus(Ti == null ? { get preventScroll() {
		return Ti = { preventScroll: !0 }, !0;
	} } : void 0), Ti || (Ti = !1, wi(t, 0));
}
function Di(e, t) {
	let n, r = 2e8, i, a = 0, o = t.top, s = t.top, c, l;
	for (let u = e.firstChild, d = 0; u; u = u.nextSibling, d++) {
		let e;
		if (O(u)) e = u.getClientRects();
		else if (k(u)) e = Lr(u).getClientRects();
		else continue;
		for (let f = 0; f < e.length; f++) {
			let p = e[f];
			if (p.top <= o && p.bottom >= s) {
				o = Math.max(p.bottom, o), s = Math.min(p.top, s);
				let e = p.left > t.left ? p.left - t.left : p.right < t.left ? t.left - p.right : 0;
				if (e < r) {
					n = u, r = e, i = e && k(n) ? {
						left: p.right < t.left ? p.right : p.left,
						top: t.top
					} : t, O(u) && e && (a = d + +(t.left >= (p.left + p.right) / 2));
					continue;
				}
			} else p.top > t.top && !c && p.left <= t.left && p.right >= t.left && (c = u, l = {
				left: Math.max(p.left, Math.min(p.right, t.left)),
				top: p.top
			});
			!n && (t.left >= p.right && t.top >= p.top || t.left >= p.left && t.top >= p.bottom) && (a = d + 1);
		}
	}
	return !n && c && (n = c, i = l, r = 0), n && k(n) ? Oi(n, i) : !n || r ? {
		node: e,
		offset: a
	} : Di(n, i);
}
function Oi(e, t) {
	let n = e.nodeValue.length, r = document.createRange(), i;
	for (let a = 0; a < n; a++) {
		r.setEnd(e, a + 1), r.setStart(e, a);
		let n = Ii(r, 1);
		if (n.top != n.bottom && ki(t, n)) {
			i = {
				node: e,
				offset: a + +(t.left >= (n.left + n.right) / 2)
			};
			break;
		}
	}
	return r.detach(), i || {
		node: e,
		offset: 0
	};
}
function ki(e, t) {
	return e.left >= t.left - 1 && e.left <= t.right + 1 && e.top >= t.top - 1 && e.top <= t.bottom + 1;
}
function Ai(e, t) {
	let n = e.parentNode;
	return n && /^li$/i.test(n.nodeName) && t.left < e.getBoundingClientRect().left ? n : e;
}
function ji(e, t, n) {
	let { node: r, offset: i } = Di(t, n), a = -1;
	if (O(r) && !r.firstChild) {
		let e = r.getBoundingClientRect();
		a = e.left != e.right && n.left > (e.left + e.right) / 2 ? 1 : -1;
	}
	return e.docView.posFromDOM(r, i, a);
}
function Mi(e, t, n, r) {
	let i = -1;
	for (let n = t, a = !1; n != e.dom;) {
		let t = e.docView.nearestDesc(n, !0), o;
		if (!t) return null;
		if (O(t.dom) && (t.node.isBlock && t.parent || !t.contentDOM) && ((o = t.dom.getBoundingClientRect()).width || o.height) && (t.node.isBlock && t.parent && !/^T(R|BODY|HEAD|FOOT)$/.test(t.dom.nodeName) && (!a && o.left > r.left || o.top > r.top ? i = t.posBefore : (!a && o.right < r.left || o.bottom < r.top) && (i = t.posAfter), a = !0), !t.contentDOM && i < 0 && !t.node.isText)) return (t.node.isBlock ? r.top < (o.top + o.bottom) / 2 : r.left < (o.left + o.right) / 2) ? t.posBefore : t.posAfter;
		n = t.dom.parentNode;
	}
	return i > -1 ? i : e.docView.posFromDOM(t, n, -1);
}
function Ni(e, t, n) {
	let r = e.childNodes.length;
	if (r && n.top < n.bottom) for (let i = Math.max(0, Math.min(r - 1, Math.floor(r * (t.top - n.top) / (n.bottom - n.top)) - 2)), a = i;;) {
		let n = e.childNodes[a];
		if (O(n)) {
			let e = n.getClientRects();
			for (let r = 0; r < e.length; r++) {
				let i = e[r];
				if (ki(t, i)) return Ni(n, t, i);
			}
		}
		if ((a = (a + 1) % r) == i) break;
	}
	return e;
}
function Pi(e, t) {
	let n = e.dom.ownerDocument, r, i = 0, a = Zr(n, t.left, t.top);
	a && ({node: r, offset: i} = a);
	let o = (e.root.elementFromPoint ? e.root : n).elementFromPoint(t.left, t.top), s;
	if (!o || !e.dom.contains(o)) {
		let n = e.dom.getBoundingClientRect();
		if (!ki(t, n) || (o = Ni(e.dom, t, n), !o)) return null;
	}
	if (ui) for (let e = o; r && e; e = Fr(e)) e.draggable && (r = void 0);
	if (o = Ai(o, t), r) {
		if (oi && O(r) && (i = Math.min(i, r.childNodes.length), i < r.childNodes.length)) {
			let e = r.childNodes[i], n;
			e.nodeName == "IMG" && (n = e.getBoundingClientRect()).right <= t.left && n.bottom > t.top && i++;
		}
		let n;
		hi && i && O(r) && O(n = r.childNodes[i - 1]) && n.contentEditable == "false" && n.getBoundingClientRect().top >= t.top && i--, r == e.dom && i == r.childNodes.length - 1 && O(r.lastChild) && t.top > r.lastChild.getBoundingClientRect().bottom ? s = e.state.doc.content.size : (i == 0 || !O(r) || r.childNodes[i - 1].nodeName != "BR") && (s = Mi(e, r, i, t));
	}
	s ?? (s = ji(e, o, t));
	let c = e.docView.nearestDesc(o, !0);
	return {
		pos: s,
		inside: c ? c.posAtStart - c.border : -1
	};
}
function Fi(e) {
	return e.top < e.bottom || e.left < e.right;
}
function Ii(e, t) {
	let n = e.getClientRects();
	if (n.length) {
		let e = n[t < 0 ? 0 : n.length - 1];
		if (Fi(e)) return e;
	}
	return Array.prototype.find.call(n, Fi) || e.getBoundingClientRect();
}
var Li = /[\u0590-\u05f4\u0600-\u06ff\u0700-\u08ac]/;
function Ri(e, t, n) {
	let { node: r, offset: i, atom: a } = e.docView.domFromPos(t, n < 0 ? -1 : 1), o = hi || oi;
	if (k(r)) {
		if (o && (Li.test(r.nodeValue) || (n < 0 ? !i : i == r.nodeValue.length))) {
			let e = Ii(Lr(r, i, i), n);
			if (oi && i && /\s/.test(r.nodeValue[i - 1]) && i < r.nodeValue.length) {
				let t = Ii(Lr(r, i - 1, i - 1), -1);
				if (t.top == e.top) {
					let n = Ii(Lr(r, i, i + 1), -1);
					if (n.top != e.top) return zi(n, n.left < t.left);
				}
			}
			return e;
		}
		{
			let e = i, t = i, a = n < 0 ? 1 : -1;
			return n < 0 && !i ? (t++, a = -1) : n >= 0 && i == r.nodeValue.length ? (e--, a = 1) : n < 0 ? e-- : t++, zi(Ii(Lr(r, e, t), a), a < 0);
		}
	}
	if (!e.state.doc.resolve(t - (a || 0)).parent.inlineContent) {
		if (a == null && i && (n < 0 || i == Hr(r))) {
			let e = r.childNodes[i - 1];
			if (O(e)) return Bi(e.getBoundingClientRect(), !1);
		}
		if (a == null && i < Hr(r)) {
			let e = r.childNodes[i];
			if (O(e)) return Bi(e.getBoundingClientRect(), !0);
		}
		return Bi(r.getBoundingClientRect(), n >= 0);
	}
	if (a == null && i && (n < 0 || i == Hr(r))) {
		let e = r.childNodes[i - 1], t = k(e) ? Lr(e, Hr(e) - +!o) : O(e) && (e.nodeName != "BR" || !e.nextSibling) ? e : null;
		if (t) return zi(Ii(t, 1), !1);
	}
	if (a == null && i < Hr(r)) {
		let e = r.childNodes[i];
		for (; e.pmViewDesc && e.pmViewDesc.ignoreForCoords;) e = e.nextSibling;
		let t = e ? k(e) ? Lr(e, 0, +!o) : O(e) ? e : null : null;
		if (t) return zi(Ii(t, -1), !0);
	}
	return zi(Ii(k(r) ? Lr(r) : r, -n), n >= 0);
}
function zi(e, t) {
	if (e.width == 0) return e;
	let n = t ? e.left : e.right;
	return {
		top: e.top,
		bottom: e.bottom,
		left: n,
		right: n
	};
}
function Bi(e, t) {
	if (e.height == 0) return e;
	let n = t ? e.top : e.bottom;
	return {
		top: n,
		bottom: n,
		left: e.left,
		right: e.right
	};
}
function Vi(e, t, n) {
	let r = e.state, i = e.root.activeElement;
	r != t && e.updateState(t), i != e.dom && e.focus();
	try {
		return n();
	} finally {
		r != t && e.updateState(r), i != e.dom && i && i.focus();
	}
}
function Hi(e, t, n) {
	let r = t.selection, i = n == "up" ? r.$from : r.$to;
	return Vi(e, t, () => {
		let { node: t } = e.docView.domFromPos(i.pos, n == "up" ? -1 : 1);
		for (;;) {
			let n = e.docView.nearestDesc(t, !0);
			if (!n) break;
			if (n.node.isBlock) {
				t = n.contentDOM || n.dom;
				break;
			}
			t = n.dom.parentNode;
		}
		let r = Ri(e, i.pos, 1);
		for (let e = t.firstChild; e; e = e.nextSibling) {
			let t;
			if (O(e)) t = e.getClientRects();
			else if (k(e)) t = Lr(e, 0, e.nodeValue.length).getClientRects();
			else continue;
			for (let e = 0; e < t.length; e++) {
				let i = t[e];
				if (i.bottom > i.top + 1 && (n == "up" ? r.top - i.top > (i.bottom - r.top) * 2 : i.bottom - r.bottom > (r.bottom - i.top) * 2)) return !1;
			}
		}
		return !0;
	});
}
var Ui = /[\u0590-\u08ac]/;
function Wi(e, t, n) {
	let { $head: r } = t.selection;
	if (!r.parent.isTextblock) return !1;
	let i = r.parentOffset, a = !i, o = i == r.parent.content.size, s = e.domSelection();
	return s ? !Ui.test(r.parent.textContent) || !s.modify ? n == "left" || n == "backward" ? a : o : Vi(e, t, () => {
		let { focusNode: t, focusOffset: i, anchorNode: a, anchorOffset: o } = e.domSelectionRange(), c = s.caretBidiLevel;
		s.modify("move", n, "character");
		let l = r.depth ? e.docView.domAfterPos(r.before()) : e.dom, { focusNode: u, focusOffset: d } = e.domSelectionRange(), f = u && !Ur(l, u) || t == u && i == d;
		try {
			s.collapse(a, o), t && (t != a || i != o) && s.extend && s.extend(t, i);
		} catch {}
		return c != null && (s.caretBidiLevel = c), f;
	}) : r.pos == r.start() || r.pos == r.end();
}
var Gi = null, Ki = null, qi = !1;
function Ji(e, t, n) {
	return Gi == t && Ki == n ? qi : (Gi = t, Ki = n, qi = n == "up" || n == "down" ? Hi(e, t, n) : Wi(e, t, n));
}
var Yi = 0, Xi = 1, Zi = 2, Qi = 3, $i = class {
	constructor(e, t, n, r) {
		this.parent = e, this.children = t, this.dom = n, this.contentDOM = r, this.dirty = Yi, n.pmViewDesc = this;
	}
	matchesWidget(e) {
		return !1;
	}
	matchesMark(e) {
		return !1;
	}
	matchesNode(e, t, n) {
		return !1;
	}
	matchesHack(e) {
		return !1;
	}
	parseRule(e) {
		return null;
	}
	stopEvent(e) {
		return !1;
	}
	get size() {
		let e = 0;
		for (let t = 0; t < this.children.length; t++) e += this.children[t].size;
		return e;
	}
	get border() {
		return 0;
	}
	destroy() {
		this.parent = void 0, this.dom.pmViewDesc == this && (this.dom.pmViewDesc = void 0);
		for (let e = 0; e < this.children.length; e++) this.children[e].destroy();
	}
	posBeforeChild(e) {
		for (let t = 0, n = this.posAtStart;; t++) {
			let r = this.children[t];
			if (r == e) return n;
			n += r.size;
		}
	}
	get posBefore() {
		return this.parent.posBeforeChild(this);
	}
	get posAtStart() {
		return this.parent ? this.parent.posBeforeChild(this) + this.border : 0;
	}
	get posAfter() {
		return this.posBefore + this.size;
	}
	get posAtEnd() {
		return this.posAtStart + this.size - 2 * this.border;
	}
	localPosFromDOM(e, t, n) {
		if (this.contentDOM && Ur(this.contentDOM, e)) {
			if (n < 0) {
				let n, r;
				if (e == this.contentDOM) n = e.childNodes[t - 1];
				else {
					for (; e.parentNode != this.contentDOM;) e = e.parentNode;
					n = e.previousSibling;
				}
				for (; n && !((r = n.pmViewDesc) && r.parent == this);) n = n.previousSibling;
				return n ? this.posBeforeChild(r) + r.size : this.posAtStart;
			}
			{
				let n, r;
				if (e == this.contentDOM) n = e.childNodes[t];
				else {
					for (; e.parentNode != this.contentDOM;) e = e.parentNode;
					n = e.nextSibling;
				}
				for (; n && !((r = n.pmViewDesc) && r.parent == this);) n = n.nextSibling;
				return n ? this.posBeforeChild(r) : this.posAtEnd;
			}
		}
		let r;
		if (e == this.dom && this.contentDOM) r = t > Pr(this.contentDOM);
		else if (this.contentDOM && this.contentDOM != this.dom && this.dom.contains(this.contentDOM)) r = !!(e.compareDocumentPosition(this.contentDOM) & 4);
		else if (this.dom.firstChild) {
			if (t == 0) for (let t = e;; t = t.parentNode) {
				if (t == this.dom) {
					r = !1;
					break;
				}
				if (t.previousSibling) break;
			}
			if (r == null && t == e.childNodes.length) for (let t = e;; t = t.parentNode) {
				if (t == this.dom) {
					r = !0;
					break;
				}
				if (t.nextSibling) break;
			}
		}
		return r ?? n > 0 ? this.posAtEnd : this.posAtStart;
	}
	nearestDesc(e, t = !1) {
		for (let n = !0, r = e; r; r = r.parentNode) {
			let i = this.getDesc(r), a;
			if (i && (!t || i.node)) {
				if (n && (a = i.nodeDOM) && !(O(a) ? Ur(a, e) : a == e)) n = !1;
				else return i;
			}
		}
	}
	getDesc(e) {
		let t = e.pmViewDesc;
		for (let e = t; e; e = e.parent) if (e == this) return t;
	}
	posFromDOM(e, t, n) {
		for (let r = e; r; r = r.parentNode) {
			let i = this.getDesc(r);
			if (i) return i.localPosFromDOM(e, t, n);
		}
		return -1;
	}
	descAt(e) {
		for (let t = 0, n = 0; t < this.children.length; t++) {
			let r = this.children[t], i = n + r.size;
			if (n == e && i != n) {
				for (; !r.border && r.children.length;) for (let e = 0; e < r.children.length; e++) {
					let t = r.children[e];
					if (t.size) {
						r = t;
						break;
					}
				}
				return r;
			}
			if (e < i) return r.descAt(e - n - r.border);
			n = i;
		}
	}
	domFromPos(e, t) {
		if (!this.contentDOM) return {
			node: this.dom,
			offset: 0,
			atom: e + 1
		};
		let n = 0, r = 0;
		for (let t = 0; n < this.children.length; n++) {
			let i = this.children[n], a = t + i.size;
			if (a > e) {
				r = e - t;
				break;
			}
			t = a;
		}
		if (r) return this.children[n].domFromPos(r - this.children[n].border, t);
		for (; n > 0; n--) {
			let e = this.children[n - 1];
			if (e.size || !(e instanceof oa || e instanceof ea && e.side >= 0)) break;
		}
		if (t <= 0) {
			let e, r = !0;
			for (; e = n ? this.children[n - 1] : null, e && e.dom.parentNode != this.contentDOM; n--, r = !1);
			return e && t && r && !e.border && !e.domAtom ? e.domFromPos(e.size, t) : {
				node: this.contentDOM,
				offset: e ? Pr(e.dom) + 1 : 0
			};
		}
		{
			let e, r = !0;
			for (; e = n < this.children.length ? this.children[n] : null, e && e.dom.parentNode != this.contentDOM; n++, r = !1);
			return e && r && !e.border && !e.domAtom ? e.domFromPos(0, t) : {
				node: this.contentDOM,
				offset: e ? Pr(e.dom) : this.contentDOM.childNodes.length
			};
		}
	}
	parseRange(e, t, n = 0) {
		if (this.children.length == 0) return {
			node: this.contentDOM,
			from: e,
			to: t,
			fromOffset: 0,
			toOffset: this.contentDOM.childNodes.length
		};
		let r = -1, i = -1;
		for (let a = n, o = 0;; o++) {
			let n = this.children[o], s = a + n.size;
			if (r == -1 && e <= s) {
				let i = a + n.border;
				if (e >= i && t <= s - n.border && n.node && n.contentDOM && this.contentDOM.contains(n.contentDOM)) return n.parseRange(e, t, i);
				e = a;
				for (let t = o; t > 0; t--) {
					let n = this.children[t - 1];
					if (n.size && n.dom.parentNode == this.contentDOM && !n.emptyChildAt(1)) {
						r = Pr(n.dom) + 1;
						break;
					}
					e -= n.size;
				}
				r == -1 && (r = 0);
			}
			if (r > -1 && (s > t || o == this.children.length - 1)) {
				t = s;
				for (let e = o + 1; e < this.children.length; e++) {
					let n = this.children[e];
					if (n.size && n.dom.parentNode == this.contentDOM && !n.emptyChildAt(-1)) {
						i = Pr(n.dom);
						break;
					}
					t += n.size;
				}
				i == -1 && (i = this.contentDOM.childNodes.length);
				break;
			}
			a = s;
		}
		return {
			node: this.contentDOM,
			from: e,
			to: t,
			fromOffset: r,
			toOffset: i
		};
	}
	emptyChildAt(e) {
		if (this.border || !this.contentDOM || !this.children.length) return !1;
		let t = this.children[e < 0 ? 0 : this.children.length - 1];
		return t.size == 0 || t.emptyChildAt(e);
	}
	domAfterPos(e) {
		let { node: t, offset: n } = this.domFromPos(e, 0);
		if (!O(t) || n == t.childNodes.length) throw RangeError("No node after pos " + e);
		return t.childNodes[n];
	}
	setSelection(e, t, n, r = !1) {
		let i = Math.min(e, t), a = Math.max(e, t);
		for (let o = 0, s = 0; o < this.children.length; o++) {
			let c = this.children[o], l = s + c.size;
			if (i > s && a < l) return c.setSelection(e - s - c.border, t - s - c.border, n, r);
			s = l;
		}
		let o = this.domFromPos(e, e ? -1 : 1), s = t == e ? o : this.domFromPos(t, t ? -1 : 1), c = n.root.getSelection(), l = n.domSelectionRange(), u = !1;
		if ((oi || ui) && e == t) {
			let { node: e, offset: t } = o;
			if (k(e)) {
				if (u = !!(t && e.nodeValue[t - 1] == "\n"), u && t == e.nodeValue.length) for (let t = e, n; t; t = t.parentNode) {
					if (n = t.nextSibling) {
						n.nodeName == "BR" && (o = s = {
							node: n.parentNode,
							offset: Pr(n) + 1
						});
						break;
					}
					let e = t.pmViewDesc;
					if (e && e.node && e.node.isBlock) break;
				}
			} else {
				let n = e.childNodes[t - 1];
				u = n && (n.nodeName == "BR" || n.contentEditable == "false");
			}
		}
		if (oi && l.focusNode && l.focusNode != s.node && O(l.focusNode)) {
			let e = l.focusNode.childNodes[l.focusOffset];
			e && e.contentEditable == "false" && (r = !0);
		}
		if (!(r || u && ui) && zr(o.node, o.offset, l.anchorNode, l.anchorOffset) && zr(s.node, s.offset, l.focusNode, l.focusOffset)) return;
		let d = !1;
		if ((c.extend || e == t) && !(u && oi)) {
			c.collapse(o.node, o.offset);
			try {
				e != t && c.extend(s.node, s.offset), d = !0;
			} catch {}
		}
		if (!d) {
			if (e > t) {
				let e = o;
				o = s, s = e;
			}
			let n = document.createRange();
			n.setEnd(s.node, s.offset), n.setStart(o.node, o.offset), c.removeAllRanges(), c.addRange(n);
		}
	}
	ignoreMutation(e) {
		return !this.contentDOM && e.type != "selection";
	}
	get contentLost() {
		return this.contentDOM && this.contentDOM != this.dom && !this.dom.contains(this.contentDOM);
	}
	markDirty(e, t) {
		for (let n = 0, r = 0; r < this.children.length; r++) {
			let i = this.children[r], a = n + i.size;
			if (n == a ? e <= a && t >= n : e < a && t > n) {
				let r = n + i.border, o = a - i.border;
				if (e >= r && t <= o) {
					this.dirty = e == n || t == a ? Zi : Xi, e == r && t == o && (i.contentLost || i.dom.parentNode != this.contentDOM) ? i.dirty = Qi : i.markDirty(e - r, t - r);
					return;
				}
				i.dirty = i.dom == i.contentDOM && i.dom.parentNode == this.contentDOM && !i.children.length ? Zi : Qi;
			}
			n = a;
		}
		this.dirty = Zi;
	}
	markParentsDirty() {
		let e = 1;
		for (let t = this.parent; t; t = t.parent, e++) {
			let n = e == 1 ? Zi : Xi;
			t.dirty < n && (t.dirty = n);
		}
	}
	get domAtom() {
		return !1;
	}
	get ignoreForCoords() {
		return !1;
	}
	get ignoreForSelection() {
		return !1;
	}
	isText(e) {
		return !1;
	}
}, ea = class extends $i {
	constructor(e, t, n, r) {
		let i, a = t.type.toDOM;
		if (typeof a == "function" && (a = a(n, () => {
			if (!i) return r;
			if (i.parent) return i.parent.posBeforeChild(i);
		})), !t.type.spec.raw) {
			let e = O(a) ? a : document.createElement("span");
			e != a && e.appendChild(a), e.hasAttribute("contenteditable") || (e.contentEditable = "false"), e.classList.add("ProseMirror-widget");
		}
		super(e, [], a, null), this.widget = t, this.widget = t, i = this;
	}
	matchesWidget(e) {
		return this.dirty == Yi && e.type.eq(this.widget.type);
	}
	parseRule() {
		return { ignore: !0 };
	}
	stopEvent(e) {
		let t = this.widget.spec.stopEvent;
		return t ? t(e) : !1;
	}
	ignoreMutation(e) {
		return e.type != "selection" || this.widget.spec.ignoreSelection;
	}
	destroy() {
		this.widget.type.destroy(this.dom), super.destroy();
	}
	get domAtom() {
		return !0;
	}
	get ignoreForSelection() {
		return !!this.widget.type.spec.relaxedSide;
	}
	get side() {
		return this.widget.type.side;
	}
}, ta = class extends $i {
	constructor(e, t, n, r) {
		super(e, [], t, null), this.textDOM = n, this.text = r;
	}
	get size() {
		return this.text.length;
	}
	localPosFromDOM(e, t) {
		return e == this.textDOM ? this.posAtStart + t : this.posAtStart + (t ? this.size : 0);
	}
	domFromPos(e) {
		return {
			node: this.textDOM,
			offset: e
		};
	}
	ignoreMutation(e) {
		return e.type === "characterData" && e.target.nodeValue == e.oldValue;
	}
}, na = class e extends $i {
	constructor(e, t, n, r, i) {
		super(e, [], n, r), this.mark = t, this.spec = i;
	}
	static create(t, n, r, i) {
		let a = i.nodeViews[n.type.name], o = a && a(n, i, r);
		return (!o || !o.dom) && (o = et.renderSpec(document, n.type.spec.toDOM(n, r), null, n.attrs)), new e(t, n, o.dom, o.contentDOM || o.dom, o);
	}
	parseRule() {
		return this.dirty & Qi || this.mark.type.spec.reparseInView ? null : {
			mark: this.mark.type.name,
			attrs: this.mark.attrs,
			contentElement: this.contentDOM
		};
	}
	matchesMark(e) {
		return this.dirty != Qi && this.mark.eq(e);
	}
	markDirty(e, t) {
		if (super.markDirty(e, t), this.dirty != Yi) {
			let e = this.parent;
			for (; !e.node;) e = e.parent;
			e.dirty < this.dirty && (e.dirty = this.dirty), this.dirty = Yi;
		}
	}
	slice(t, n, r) {
		let i = e.create(this.parent, this.mark, !0, r), a = this.children, o = this.size;
		n < o && (a = Ca(a, n, o, r)), t > 0 && (a = Ca(a, 0, t, r));
		for (let e = 0; e < a.length; e++) a[e].parent = i;
		return i.children = a, i;
	}
	ignoreMutation(e) {
		return this.spec.ignoreMutation ? this.spec.ignoreMutation(e) : super.ignoreMutation(e);
	}
	destroy() {
		this.spec.destroy && this.spec.destroy(), super.destroy();
	}
}, ra = class e extends $i {
	constructor(e, t, n, r, i, a, o) {
		super(e, [], i, a), this.node = t, this.outerDeco = n, this.innerDeco = r, this.nodeDOM = o;
	}
	static create(t, n, r, i, a, o) {
		let s = a.nodeViews[n.type.name], c, l = s && s(n, a, () => {
			if (!c) return o;
			if (c.parent) return c.parent.posBeforeChild(c);
		}, r, i), u = l && l.dom, d = l && l.contentDOM;
		if (n.isText) {
			if (!u) u = document.createTextNode(n.text);
			else if (!k(u)) throw RangeError("Text must be rendered as a DOM text node");
		} else if (!u) {
			let e = et.renderSpec(document, n.type.spec.toDOM(n), null, n.attrs);
			({dom: u, contentDOM: d} = e);
		}
		!d && !n.isText && u.nodeName != "BR" && (u.hasAttribute("contenteditable") || (u.contentEditable = "false"), n.type.spec.draggable && (u.draggable = !0));
		let f = u;
		return u = ma(u, r, n), l ? c = new sa(t, n, r, i, u, d || null, f, l) : n.isText ? new aa(t, n, r, i, u, f) : new e(t, n, r, i, u, d || null, f);
	}
	parseRule(e) {
		if (this.node.type.spec.reparseInView) return null;
		let t = {
			node: this.node.type.name,
			attrs: this.node.attrs
		};
		if (this.node.type.whitespace == "pre" && (t.preserveWhitespace = "full"), !this.contentDOM) t.getContent = () => this.node.content;
		else if (!this.contentLost) t.contentElement = this.contentDOM;
		else {
			for (let e = this.children.length - 1; e >= 0; e--) {
				let n = this.children[e];
				if (this.dom.contains(n.dom.parentNode)) {
					t.contentElement = n.dom.parentNode;
					break;
				}
			}
			if (!t.contentElement) {
				let n = e && e.find((t) => O(t) && e.indexOf(t.parentNode) < 0 && this.dom.contains(t));
				n ? t.contentElement = n : t.getContent = () => l.empty;
			}
		}
		return t;
	}
	matchesNode(e, t, n) {
		return this.dirty == Yi && e.eq(this.node) && ha(t, this.outerDeco) && n.eq(this.innerDeco);
	}
	get size() {
		return this.node.nodeSize;
	}
	get border() {
		return +!this.node.isLeaf;
	}
	updateChildren(e, t) {
		let n = this.node.inlineContent, r = t, i = e.composing ? this.localCompositionInfo(e, t) : null, a = i && i.pos > -1 ? i : null, o = i && i.pos < 0, s = new _a(this, a && a.node, e), c = this.node.inlineContent, l = c && this.node.isBlock;
		ba(this.node, this.innerDeco, (t, i, a) => {
			t.spec.marks ? s.syncToMarks(t.spec.marks, n, e, i) : t.type.side >= 0 && !a && s.syncToMarks(i == this.node.childCount ? p.none : this.node.child(i).marks, n, e, i), s.placeWidget(t, e, r);
		}, (t, a, u, d) => {
			s.syncToMarks(t.marks, n, e, d), l && t.isAtom && !t.isText && s.addHackNode("IMG", s.top);
			let f;
			s.findNodeMatch(t, a, u, d) || o && e.state.selection.from > r && e.state.selection.to < r + t.nodeSize && (f = s.findIndexWithChild(i.node)) > -1 && s.updateNodeAt(t, a, u, f, e) || s.updateNextNode(t, a, u, e, d, r) || s.addNode(t, a, u, e, r), r += t.nodeSize, c && (l = t.isAtom && !t.isText);
		}), s.syncToMarks([], n, e, 0), this.node.isTextblock && s.addTextblockHacks(), s.destroyRest(), (s.changed || this.dirty == Zi) && (a && this.protectLocalComposition(e, a), ca(this.contentDOM, this.children, e), di && xa(this.dom));
	}
	localCompositionInfo(e, t) {
		let { from: n, to: r } = e.state.selection;
		if (!(e.state.selection instanceof w) || n < t || r > t + this.node.content.size) return null;
		let i = e.input.compositionNode;
		if (!i || !this.dom.contains(i.parentNode)) return null;
		if (this.node.inlineContent) {
			let e = i.nodeValue, a = Sa(this.node.content, e, n - t, r - t);
			return a < 0 ? null : {
				node: i,
				pos: a,
				text: e
			};
		}
		return {
			node: i,
			pos: -1,
			text: ""
		};
	}
	protectLocalComposition(e, { node: t, pos: n, text: r }) {
		if (this.getDesc(t)) return;
		let i = t;
		for (; i.parentNode != this.contentDOM; i = i.parentNode) {
			for (; i.previousSibling;) i.parentNode.removeChild(i.previousSibling);
			for (; i.nextSibling;) i.parentNode.removeChild(i.nextSibling);
			i.pmViewDesc && (i.pmViewDesc = void 0);
		}
		let a = new ta(this, i, t, r);
		e.input.compositionNodes.push(a), this.children = Ca(this.children, n, n + r.length, e, a);
	}
	update(e, t, n, r) {
		return this.dirty == Qi || !e.sameMarkup(this.node) ? !1 : (this.updateInner(e, t, n, r), !0);
	}
	updateInner(e, t, n, r) {
		this.updateOuterDeco(t), this.node = e, this.innerDeco = n, this.contentDOM && this.updateChildren(r, this.posAtStart), this.dirty = Yi;
	}
	updateOuterDeco(e) {
		if (ha(e, this.outerDeco)) return;
		let t = !O(this.nodeDOM), n = this.dom;
		this.dom = fa(this.dom, this.nodeDOM, da(this.outerDeco, this.node, t), da(e, this.node, t)), this.dom != n && (n.pmViewDesc = void 0, this.dom.pmViewDesc = this), this.outerDeco = e;
	}
	selectNode() {
		O(this.nodeDOM) && (this.nodeDOM.classList.add("ProseMirror-selectednode"), (this.contentDOM || !this.node.type.spec.draggable) && (this.nodeDOM.draggable = !0));
	}
	deselectNode() {
		O(this.nodeDOM) && (this.nodeDOM.classList.remove("ProseMirror-selectednode"), (this.contentDOM || !this.node.type.spec.draggable) && this.nodeDOM.removeAttribute("draggable"));
	}
	get domAtom() {
		return this.node.isAtom;
	}
};
function ia(e, t, n, r, i) {
	ma(r, t, e);
	let a = new ra(void 0, e, t, n, r, r, r);
	return a.contentDOM && a.updateChildren(i, 0), a;
}
var aa = class e extends ra {
	constructor(e, t, n, r, i, a) {
		super(e, t, n, r, i, null, a);
	}
	parseRule() {
		let e = this.nodeDOM.parentNode;
		for (; e && e != this.dom && !e.pmIsDeco;) e = e.parentNode;
		return { skip: e || !0 };
	}
	update(e, t, n, r) {
		return this.dirty == Qi || this.dirty != Yi && !this.inParent() || !e.sameMarkup(this.node) ? !1 : (this.updateOuterDeco(t), (this.dirty != Yi || e.text != this.node.text) && e.text != this.nodeDOM.nodeValue && (this.nodeDOM.nodeValue = e.text, r.trackWrites == this.nodeDOM && (r.trackWrites = null)), this.node = e, this.dirty = Yi, !0);
	}
	inParent() {
		let e = this.parent.contentDOM;
		for (let t = this.nodeDOM; t; t = t.parentNode) if (t == e) return !0;
		return !1;
	}
	domFromPos(e) {
		return {
			node: this.nodeDOM,
			offset: e
		};
	}
	localPosFromDOM(e, t, n) {
		return e == this.nodeDOM ? this.posAtStart + Math.min(t, this.node.text.length) : super.localPosFromDOM(e, t, n);
	}
	ignoreMutation(e) {
		return e.type != "characterData" && e.type != "selection";
	}
	slice(t, n, r) {
		let i = this.node.cut(t, n), a = document.createTextNode(i.text);
		return new e(this.parent, i, this.outerDeco, this.innerDeco, a, a);
	}
	markDirty(e, t) {
		super.markDirty(e, t), this.dom != this.nodeDOM && (e == 0 || t == this.nodeDOM.nodeValue.length) && (this.dirty = Qi);
	}
	get domAtom() {
		return !1;
	}
	isText(e) {
		return this.node.text == e;
	}
}, oa = class extends $i {
	parseRule() {
		return { ignore: !0 };
	}
	matchesHack(e) {
		return this.dirty == Yi && this.dom.nodeName == e;
	}
	get domAtom() {
		return !0;
	}
	get ignoreForCoords() {
		return this.dom.nodeName == "IMG";
	}
}, sa = class extends ra {
	constructor(e, t, n, r, i, a, o, s) {
		super(e, t, n, r, i, a, o), this.spec = s;
	}
	update(e, t, n, r) {
		if (this.dirty == Qi) return !1;
		if (this.spec.update && (this.node.type == e.type || this.spec.multiType)) {
			let i = this.spec.update(e, t, n);
			return i && this.updateInner(e, t, n, r), i;
		}
		return !this.contentDOM && !e.isLeaf ? !1 : super.update(e, t, n, r);
	}
	selectNode() {
		this.spec.selectNode ? this.spec.selectNode() : super.selectNode();
	}
	deselectNode() {
		this.spec.deselectNode ? this.spec.deselectNode() : super.deselectNode();
	}
	setSelection(e, t, n, r) {
		this.spec.setSelection ? this.spec.setSelection(e, t, n.root) : super.setSelection(e, t, n, r);
	}
	destroy() {
		this.spec.destroy && this.spec.destroy(), super.destroy();
	}
	stopEvent(e) {
		return this.spec.stopEvent ? this.spec.stopEvent(e) : !1;
	}
	ignoreMutation(e) {
		return this.spec.ignoreMutation ? this.spec.ignoreMutation(e) : super.ignoreMutation(e);
	}
};
function ca(e, t, n) {
	let r = e.firstChild, i = !1;
	for (let a = 0; a < t.length; a++) {
		let o = t[a], s = o.dom;
		if (s.parentNode == e) {
			for (; s != r;) r = ga(r), i = !0;
			r = r.nextSibling;
		} else i = !0, e.insertBefore(s, r);
		if (o instanceof na) {
			let t = r ? r.previousSibling : e.lastChild;
			ca(o.contentDOM, o.children, n), r = t ? t.nextSibling : e.firstChild;
		}
	}
	for (; r;) r = ga(r), i = !0;
	i && n.trackWrites == e && (n.trackWrites = null);
}
var la = function(e) {
	e && (this.nodeName = e);
};
la.prototype = Object.create(null);
var ua = [new la()];
function da(e, t, n) {
	if (e.length == 0) return ua;
	let r = n ? ua[0] : new la(), i = [r];
	for (let a = 0; a < e.length; a++) {
		let o = e[a].type.attrs;
		if (o) {
			o.nodeName && i.push(r = new la(o.nodeName));
			for (let e in o) {
				let a = o[e];
				a != null && (n && i.length == 1 && i.push(r = new la(t.isInline ? "span" : "div")), e == "class" ? r.class = (r.class ? r.class + " " : "") + a : e == "style" ? r.style = (r.style ? r.style + ";" : "") + a : e != "nodeName" && (r[e] = a));
			}
		}
	}
	return i;
}
function fa(e, t, n, r) {
	if (n == ua && r == ua) return t;
	let i = t;
	for (let t = 0; t < r.length; t++) {
		let a = r[t], o = n[t];
		if (t) {
			let t;
			o && o.nodeName == a.nodeName && i != e && (t = i.parentNode) && t.nodeName.toLowerCase() == a.nodeName ? i = t : (t = document.createElement(a.nodeName), t.pmIsDeco = !0, t.appendChild(i), o = ua[0], i = t);
		}
		pa(i, o || ua[0], a);
	}
	return i;
}
function pa(e, t, n) {
	for (let r in t) r != "class" && r != "style" && r != "nodeName" && !(r in n) && e.removeAttribute(r);
	for (let r in n) r != "class" && r != "style" && r != "nodeName" && n[r] != t[r] && e.setAttribute(r, n[r]);
	if (t.class != n.class) {
		let r = t.class ? t.class.split(" ").filter(Boolean) : [], i = n.class ? n.class.split(" ").filter(Boolean) : [];
		for (let t = 0; t < r.length; t++) i.indexOf(r[t]) == -1 && e.classList.remove(r[t]);
		for (let t = 0; t < i.length; t++) r.indexOf(i[t]) == -1 && e.classList.add(i[t]);
		e.classList.length == 0 && e.removeAttribute("class");
	}
	if (t.style != n.style) {
		if (t.style) {
			let n = /\s*([\w\-\xa1-\uffff]+)\s*:(?:"(?:\\.|[^"])*"|'(?:\\.|[^'])*'|\(.*?\)|[^;])*/g, r;
			for (; r = n.exec(t.style);) e.style.removeProperty(r[1]);
		}
		n.style && (e.style.cssText += n.style);
	}
}
function ma(e, t, n) {
	return fa(e, e, ua, da(t, n, !O(e)));
}
function ha(e, t) {
	if (e.length != t.length) return !1;
	for (let n = 0; n < e.length; n++) if (!e[n].type.eq(t[n].type)) return !1;
	return !0;
}
function ga(e) {
	let t = e.nextSibling;
	return e.parentNode.removeChild(e), t;
}
var _a = class {
	constructor(e, t, n) {
		this.lock = t, this.view = n, this.index = 0, this.stack = [], this.changed = !1, this.top = e, this.preMatch = va(e.node.content, e);
	}
	destroyBetween(e, t) {
		if (e != t) {
			for (let n = e; n < t; n++) this.top.children[n].destroy();
			this.top.children.splice(e, t - e), this.changed = !0;
		}
	}
	destroyRest() {
		this.destroyBetween(this.index, this.top.children.length);
	}
	syncToMarks(e, t, n, r) {
		let i = 0, a = this.stack.length >> 1, o = Math.min(a, e.length);
		for (; i < o && (i == a - 1 ? this.top : this.stack[i + 1 << 1]).matchesMark(e[i]) && e[i].type.spec.spanning !== !1;) i++;
		for (; i < a;) this.destroyRest(), this.top.dirty = Yi, this.index = this.stack.pop(), this.top = this.stack.pop(), a--;
		for (; a < e.length;) {
			this.stack.push(this.top, this.index + 1);
			let i = -1, o = this.top.children.length;
			r < this.preMatch.index && (o = Math.min(this.index + 3, o));
			for (let t = this.index; t < o; t++) {
				let n = this.top.children[t];
				if (n.matchesMark(e[a]) && !this.isLocked(n.dom)) {
					i = t;
					break;
				}
			}
			if (i < 0 && this.index < this.top.children.length) {
				let t = this.top.children[this.index];
				t instanceof na && t.dirty != Qi && t.mark.type == e[a].type && t.spec.update && !this.isLocked(t.dom) && t.spec.update(e[a]) && (t.mark = e[a], i = this.index, this.changed = !0);
			}
			if (i > -1) i > this.index && (this.changed = !0, this.destroyBetween(this.index, i)), this.top = this.top.children[this.index];
			else {
				let r = na.create(this.top, e[a], t, n);
				this.top.children.splice(this.index, 0, r), this.top = r, this.changed = !0;
			}
			this.index = 0, a++;
		}
	}
	findNodeMatch(e, t, n, r) {
		let i = -1, a;
		if (r >= this.preMatch.index && (a = this.preMatch.matches[r - this.preMatch.index]).parent == this.top && a.matchesNode(e, t, n)) i = this.top.children.indexOf(a, this.index);
		else for (let r = this.index, a = Math.min(this.top.children.length, r + 5); r < a; r++) {
			let a = this.top.children[r];
			if (a.matchesNode(e, t, n) && !this.preMatch.matched.has(a)) {
				i = r;
				break;
			}
		}
		return i < 0 ? !1 : (this.destroyBetween(this.index, i), this.index++, !0);
	}
	updateNodeAt(e, t, n, r, i) {
		let a = this.top.children[r];
		return a.dirty == Qi && a.dom == a.contentDOM && (a.dirty = Zi), a.update(e, t, n, i) ? (this.destroyBetween(this.index, r), this.index++, !0) : !1;
	}
	findIndexWithChild(e) {
		for (;;) {
			let t = e.parentNode;
			if (!t) return -1;
			if (t == this.top.contentDOM) {
				let t = e.pmViewDesc;
				if (t) {
					for (let e = this.index; e < this.top.children.length; e++) if (this.top.children[e] == t) return e;
				}
				return -1;
			}
			e = t;
		}
	}
	updateNextNode(e, t, n, r, i, a) {
		for (let o = this.index; o < this.top.children.length; o++) {
			let s = this.top.children[o];
			if (s instanceof ra) {
				let c = this.preMatch.matched.get(s);
				if (c != null && c != i) return !1;
				let l = s.dom, u, d = this.isLocked(l) && !(e.isText && s.node && s.node.isText && s.nodeDOM.nodeValue == e.text && s.dirty != Qi && ha(t, s.outerDeco));
				if (!d && s.update(e, t, n, r)) return this.destroyBetween(this.index, o), s.dom != l && (this.changed = !0), this.index++, !0;
				if (!d && (u = this.recreateWrapper(s, e, t, n, r, a))) return this.destroyBetween(this.index, o), this.top.children[this.index] = u, u.contentDOM && (u.dirty = Zi, u.updateChildren(r, a + 1), u.dirty = Yi), this.changed = !0, this.index++, !0;
				break;
			}
		}
		return !1;
	}
	recreateWrapper(e, t, n, r, i, a) {
		if (e.dirty || t.isAtom || !e.children.length || !e.node.content.eq(t.content) || !ha(n, e.outerDeco) || !r.eq(e.innerDeco)) return null;
		let o = ra.create(this.top, t, n, r, i, a);
		if (o.contentDOM) {
			o.children = e.children, e.children = [];
			for (let e of o.children) e.parent = o;
		}
		return e.destroy(), o;
	}
	addNode(e, t, n, r, i) {
		let a = ra.create(this.top, e, t, n, r, i);
		a.contentDOM && a.updateChildren(r, i + 1), this.top.children.splice(this.index++, 0, a), this.changed = !0;
	}
	placeWidget(e, t, n) {
		let r = this.index < this.top.children.length ? this.top.children[this.index] : null;
		if (r && r.matchesWidget(e) && (e == r.widget || !r.widget.type.toDOM.parentNode)) this.index++;
		else {
			let r = new ea(this.top, e, t, n);
			this.top.children.splice(this.index++, 0, r), this.changed = !0;
		}
	}
	addTextblockHacks() {
		let e = this.top.children[this.index - 1], t = this.top;
		for (; e instanceof na;) t = e, e = t.children[t.children.length - 1];
		(!e || !(e instanceof aa) || /\n$/.test(e.node.text) || this.view.requiresGeckoHackNode && /\s$/.test(e.node.text)) && ((ui || ci) && e && e.dom.contentEditable == "false" && this.addHackNode("IMG", t), this.addHackNode("BR", this.top));
	}
	addHackNode(e, t) {
		if (t == this.top && this.index < t.children.length && t.children[this.index].matchesHack(e)) this.index++;
		else {
			let n = document.createElement(e);
			e == "IMG" && (n.className = "ProseMirror-separator", n.alt = ""), e == "BR" && (n.className = "ProseMirror-trailingBreak");
			let r = new oa(this.top, [], n, null);
			t == this.top ? t.children.splice(this.index++, 0, r) : t.children.push(r), this.changed = !0;
		}
	}
	isLocked(e) {
		return this.lock && (e == this.lock || O(e) && e.contains(this.lock.parentNode));
	}
};
function va(e, t) {
	let n = t, r = n.children.length, i = e.childCount, a = /* @__PURE__ */ new Map(), o = [];
	outer: for (; i > 0;) {
		let s;
		for (;;) if (r) {
			let e = n.children[r - 1];
			if (e instanceof na) n = e, r = e.children.length;
			else {
				s = e, r--;
				break;
			}
		} else if (n == t) break outer;
		else r = n.parent.children.indexOf(n), n = n.parent;
		let c = s.node;
		if (c) {
			if (c != e.child(i - 1)) break;
			--i, a.set(s, i), o.push(s);
		}
	}
	return {
		index: i,
		matched: a,
		matches: o.reverse()
	};
}
function ya(e, t) {
	return e.type.side - t.type.side;
}
function ba(e, t, n, r) {
	let i = t.locals(e), a = 0;
	if (i.length == 0) {
		for (let n = 0; n < e.childCount; n++) {
			let o = e.child(n);
			r(o, i, t.forChild(a, o), n), a += o.nodeSize;
		}
		return;
	}
	let o = 0, s = [], c = null;
	for (let l = 0;;) {
		let u, d;
		for (; o < i.length && i[o].to == a;) {
			let e = i[o++];
			e.widget && (u ? (d || (d = [u])).push(e) : u = e);
		}
		if (u) {
			if (d) {
				d.sort(ya);
				for (let e = 0; e < d.length; e++) n(d[e], l, !!c);
			} else n(u, l, !!c);
		}
		let f, p;
		if (c) p = -1, f = c, c = null;
		else if (l < e.childCount) p = l, f = e.child(l++);
		else break;
		for (let e = 0; e < s.length; e++) s[e].to <= a && s.splice(e--, 1);
		for (; o < i.length && i[o].from <= a && i[o].to > a;) s.push(i[o++]);
		let m = a + f.nodeSize;
		if (f.isText) {
			let e = m;
			o < i.length && i[o].from < e && (e = i[o].from);
			for (let t = 0; t < s.length; t++) s[t].to < e && (e = s[t].to);
			e < m && (c = f.cut(e - a), f = f.cut(0, e - a), m = e, p = -1);
		} else for (; o < i.length && i[o].to < m;) o++;
		let h = f.isInline && !f.isLeaf ? s.filter((e) => !e.inline) : s.slice();
		r(f, h, t.forChild(a, f), p), a = m;
	}
}
function xa(e) {
	if (e.nodeName == "UL" || e.nodeName == "OL") {
		let t = e.style.cssText;
		e.style.cssText = t + "; list-style: square !important", window.getComputedStyle(e).listStyle, e.style.cssText = t;
	}
}
function Sa(e, t, n, r) {
	for (let i = 0, a = 0; i < e.childCount && a <= r;) {
		let o = e.child(i++), s = a;
		if (a += o.nodeSize, !o.isText) continue;
		let c = o.text;
		for (; i < e.childCount;) {
			let t = e.child(i++);
			if (a += t.nodeSize, !t.isText) break;
			c += t.text;
		}
		if (a >= n) {
			if (a >= r && c.slice(r - t.length - s, r - s) == t) return r - t.length;
			let e = s < r ? c.lastIndexOf(t, r - s - 1) : -1;
			if (e >= 0 && e + t.length + s >= n) return s + e;
			if (n == r && c.length >= r + t.length - s && c.slice(r - s, r - s + t.length) == t) return r;
		}
	}
	return -1;
}
function Ca(e, t, n, r, i) {
	let a = [];
	for (let o = 0, s = 0; o < e.length; o++) {
		let c = e[o], l = s, u = s += c.size;
		l >= n || u <= t ? a.push(c) : (l < t && a.push(c.slice(0, t - l, r)), i && (a.push(i), i = void 0), u > n && a.push(c.slice(n - l, c.size, r)));
	}
	return a;
}
function wa(e, t = null) {
	let n = e.domSelectionRange(), r = e.state.doc;
	if (!n.focusNode) return null;
	let i = e.docView.nearestDesc(n.focusNode), a = i && i.size == 0, o = e.docView.posFromDOM(n.focusNode, n.focusOffset, 1);
	if (o < 0) return null;
	let s = r.resolve(o), c, l;
	if (Jr(n)) {
		for (c = o; i && !i.node;) i = i.parent;
		if (i && i.node.isAtom && T.isSelectable(i.node) && i.parent && !(i.node.isInline && Kr(n.focusNode, n.focusOffset, i.dom))) {
			let e = i.posBefore;
			l = new T(o == e ? s : r.resolve(e));
		} else if (oi && t == "key" && o > e.state.selection.to && O(n.focusNode) && n.focusOffset == n.focusNode.childNodes.length && i && i.posAtStart > e.state.selection.to) for (let e = n.focusNode;; e = e.parentNode) {
			if (getComputedStyle(e).overflow != "visible") {
				l = C.findFrom(r.resolve(i.posAtStart), 1);
				break;
			}
			if (e == i.dom || e.nextSibling || !e.parentNode || !O(e.parentNode)) break;
		}
	} else {
		if (n instanceof e.dom.ownerDocument.defaultView.Selection && n.rangeCount > 1) {
			let t = o, i = o;
			for (let r = 0; r < n.rangeCount; r++) {
				let a = n.getRangeAt(r);
				t = Math.min(t, e.docView.posFromDOM(a.startContainer, a.startOffset, 1)), i = Math.max(i, e.docView.posFromDOM(a.endContainer, a.endOffset, -1));
			}
			if (t < 0) return null;
			[c, o] = i == e.state.selection.anchor ? [i, t] : [t, i], s = r.resolve(o);
		} else c = e.docView.posFromDOM(n.anchorNode, n.anchorOffset, 1);
		if (c < 0) return null;
	}
	let u = r.resolve(c);
	if (!l) {
		let n = t == "pointer" || e.state.selection.head < s.pos && !a ? 1 : -1;
		l = Fa(e, u, s, n);
	}
	return l;
}
function Ta(e) {
	return e.editable ? e.hasFocus() : La(e) && document.activeElement && document.activeElement.contains(e.dom);
}
function Ea(e, t = !1) {
	let n = e.state.selection;
	if (Na(e, n), !Ta(e)) return;
	let r = e.input.mouseDown;
	if (!t && ci && r) {
		let t = e.domSelectionRange(), n = e.domObserver.currentSelection;
		if (t.anchorNode && n.anchorNode && zr(t.anchorNode, t.anchorOffset, n.anchorNode, n.anchorOffset) && r.delaySelUpdate()) {
			e.domObserver.setCurSelection();
			return;
		}
	}
	if (e.domObserver.disconnectSelection(), e.cursorWrapper) Ma(e);
	else {
		let { anchor: r, head: i } = n, a, o;
		Da && !(n instanceof w) && (n.$from.parent.inlineContent || (a = Oa(e, n.from)), !n.empty && !n.$from.parent.inlineContent && (o = Oa(e, n.to))), e.docView.setSelection(r, i, e, t), Da && (a && Aa(a), o && Aa(o)), n.visible ? e.dom.classList.remove("ProseMirror-hideselection") : (e.dom.classList.add("ProseMirror-hideselection"), "onselectionchange" in document && ja(e));
	}
	e.domObserver.setCurSelection(), e.domObserver.connectSelection();
}
var Da = ui || ci && li < 63;
function Oa(e, t) {
	let { node: n, offset: r } = e.docView.domFromPos(t, 0), i = r < n.childNodes.length ? n.childNodes[r] : null, a = r ? n.childNodes[r - 1] : null;
	if (ui && i && i.contentEditable == "false") return ka(i);
	if ((!i || i.contentEditable == "false") && (!a || a.contentEditable == "false")) {
		if (i) return ka(i);
		if (a) return ka(a);
	}
}
function ka(e) {
	return e.contentEditable = "true", ui && e.draggable && (e.draggable = !1, e.wasDraggable = !0), e;
}
function Aa(e) {
	e.contentEditable = "false", e.wasDraggable && (e.draggable = !0, e.wasDraggable = null);
}
function ja(e) {
	let t = e.dom.ownerDocument;
	t.removeEventListener("selectionchange", e.input.hideSelectionGuard);
	let n = e.domSelectionRange(), r = n.anchorNode, i = n.anchorOffset;
	t.addEventListener("selectionchange", e.input.hideSelectionGuard = () => {
		(n.anchorNode != r || n.anchorOffset != i) && (t.removeEventListener("selectionchange", e.input.hideSelectionGuard), setTimeout(() => {
			(!Ta(e) || e.state.selection.visible) && e.dom.classList.remove("ProseMirror-hideselection");
		}, 20));
	});
}
function Ma(e) {
	let t = e.domSelection();
	if (!t) return;
	let n = e.cursorWrapper.dom, r = n.nodeName == "IMG";
	r ? t.collapse(n.parentNode, Pr(n) + 1) : t.collapse(n, 0), !r && !e.state.selection.visible && ii && ai <= 11 && (n.disabled = !0, n.disabled = !1);
}
function Na(e, t) {
	if (t instanceof T) {
		let n = e.docView.descAt(t.from);
		n != e.lastSelectedViewDesc && (Pa(e), n && n.selectNode(), e.lastSelectedViewDesc = n);
	} else Pa(e);
}
function Pa(e) {
	e.lastSelectedViewDesc && (e.lastSelectedViewDesc.parent && e.lastSelectedViewDesc.deselectNode(), e.lastSelectedViewDesc = void 0);
}
function Fa(e, t, n, r) {
	return e.someProp("createSelectionBetween", (r) => r(e, t, n)) || w.between(t, n, r);
}
function Ia(e) {
	return e.editable && !e.hasFocus() ? !1 : La(e);
}
function La(e) {
	let t = e.domSelectionRange();
	if (!t.anchorNode) return !1;
	try {
		return Ur(e.dom, t.anchorNode) && (e.editable || Ur(e.dom, t.focusNode));
	} catch {
		return !1;
	}
}
function Ra(e) {
	let t = e.docView.domFromPos(e.state.selection.anchor, 0), n = e.domSelectionRange();
	return zr(t.node, t.offset, n.anchorNode, n.anchorOffset);
}
function za(e, t) {
	let { $anchor: n, $head: r } = e.selection, i = t > 0 ? n.max(r) : n.min(r), a = i.parent.inlineContent ? i.depth ? e.doc.resolve(t > 0 ? i.after() : i.before()) : null : i;
	return a && C.findFrom(a, t);
}
function Ba(e, t) {
	return e.dispatch(e.state.tr.setSelection(t).scrollIntoView()), !0;
}
function Va(e, t, n) {
	let r = e.state.selection;
	if (r instanceof w) {
		if (n.indexOf("s") > -1) {
			let { $head: n } = r, i = n.textOffset ? null : t < 0 ? n.nodeBefore : n.nodeAfter;
			if (!i || i.isText || !i.isLeaf) return !1;
			let a = e.state.doc.resolve(n.pos + i.nodeSize * (t < 0 ? -1 : 1));
			return Ba(e, new w(r.$anchor, a));
		}
		if (!r.empty) return !1;
		if (e.endOfTextblock(t > 0 ? "forward" : "backward")) {
			let n = za(e.state, t);
			return n && n instanceof T ? Ba(e, n) : !1;
		}
		if (!(fi && n.indexOf("m") > -1)) {
			let n = r.$head, i = n.textOffset ? null : t < 0 ? n.nodeBefore : n.nodeAfter, a;
			if (!i || i.isText) return !1;
			let o = t < 0 ? n.pos - i.nodeSize : n.pos;
			return i.isAtom || (a = e.docView.descAt(o)) && !a.contentDOM ? T.isSelectable(i) ? Ba(e, new T(t < 0 ? e.state.doc.resolve(n.pos - i.nodeSize) : n)) : Ba(e, new w(e.state.doc.resolve(t < 0 ? o : o + i.nodeSize))) : !1;
		}
	} else if (r instanceof T && r.node.isInline) return Ba(e, new w(t > 0 ? r.$to : r.$from));
	else {
		let n = za(e.state, t);
		return n ? Ba(e, n) : !1;
	}
}
function Ha(e, t) {
	let n = e.pmViewDesc;
	return n ? n.size == 0 && (t < 0 || e.nextSibling || e.nodeName != "BR") : O(e) && e.contentEditable == "false";
}
function Ua(e, t) {
	return t < 0 ? Wa(e) : Ga(e);
}
function Wa(e) {
	let t = e.domSelectionRange(), n = t.focusNode, r = t.focusOffset;
	if (!n) return;
	let i, a, o = !1;
	for (oi && O(n) && r < Hr(n) && Ha(n.childNodes[r], -1) && (o = !0);;) if (r > 0) {
		if (!O(n)) break;
		let e = n.childNodes[r - 1];
		if (Ha(e, -1)) i = n, a = --r;
		else if (k(e)) n = e, r = n.nodeValue.length;
		else break;
	} else if (Ka(n)) break;
	else {
		let t = n.previousSibling;
		for (; t && Ha(t, -1);) i = n.parentNode, a = Pr(t), t = t.previousSibling;
		if (t) n = t, r = Hr(n);
		else {
			if (n = n.parentNode, n == e.dom) break;
			r = 0;
		}
	}
	o ? Ya(e, n, r) : i && Ya(e, i, a);
}
function Ga(e) {
	let t = e.domSelectionRange(), n = t.focusNode, r = t.focusOffset;
	if (!n) return;
	let i = Hr(n), a, o;
	for (;;) if (r < i) {
		if (!O(n)) break;
		let e = n.childNodes[r];
		if (Ha(e, 1)) a = n, o = ++r;
		else break;
	} else if (Ka(n)) break;
	else {
		let t = n.nextSibling;
		for (; t && Ha(t, 1);) a = t.parentNode, o = Pr(t) + 1, t = t.nextSibling;
		if (t) n = t, r = 0, i = Hr(n);
		else {
			if (n = n.parentNode, n == e.dom) break;
			r = i = 0;
		}
	}
	a && Ya(e, a, o);
}
function Ka(e) {
	let t = e.pmViewDesc;
	return t && t.node && t.node.isBlock;
}
function qa(e, t) {
	for (; e && t == e.childNodes.length && !qr(e);) t = Pr(e) + 1, e = e.parentNode;
	for (; e && t < e.childNodes.length;) {
		let n = e.childNodes[t];
		if (k(n)) return n;
		if (O(n) && n.contentEditable == "false") break;
		e = n, t = 0;
	}
}
function Ja(e, t) {
	for (; e && !t && !qr(e);) t = Pr(e), e = e.parentNode;
	for (; e && t;) {
		let n = e.childNodes[t - 1];
		if (k(n)) return n;
		if (O(n) && n.contentEditable == "false") break;
		e = n, t = e.childNodes.length;
	}
}
function Ya(e, t, n) {
	if (!k(t)) {
		let e, r;
		(r = qa(t, n)) ? (t = r, n = 0) : (e = Ja(t, n)) && (t = e, n = e.nodeValue.length);
	}
	let r = e.domSelection();
	if (!r) return;
	if (Jr(r)) {
		let e = document.createRange();
		e.setEnd(t, n), e.setStart(t, n), r.removeAllRanges(), r.addRange(e);
	} else r.extend && r.extend(t, n);
	e.domObserver.setCurSelection();
	let { state: i } = e;
	setTimeout(() => {
		e.state == i && Ea(e);
	}, 50);
}
function Xa(e, t) {
	let n = e.state.doc.resolve(t);
	if (!(ci || pi) && n.parent.inlineContent) {
		let r = e.coordsAtPos(t);
		if (t > n.start()) {
			let n = e.coordsAtPos(t - 1), i = (n.top + n.bottom) / 2;
			if (i > r.top && i < r.bottom && Math.abs(n.left - r.left) > 1) return n.left < r.left ? "ltr" : "rtl";
		}
		if (t < n.end()) {
			let n = e.coordsAtPos(t + 1), i = (n.top + n.bottom) / 2;
			if (i > r.top && i < r.bottom && Math.abs(n.left - r.left) > 1) return n.left > r.left ? "ltr" : "rtl";
		}
	}
	return getComputedStyle(e.dom).direction == "rtl" ? "rtl" : "ltr";
}
function Za(e, t, n) {
	let r = e.state.selection;
	if (r instanceof w && !r.empty || n.indexOf("s") > -1 || fi && n.indexOf("m") > -1) return !1;
	let { $from: i, $to: a } = r;
	if (!i.parent.inlineContent || e.endOfTextblock(t < 0 ? "up" : "down")) {
		let n = za(e.state, t);
		if (n && n instanceof T) return Ba(e, n);
	}
	if (!i.parent.inlineContent) {
		let n = t < 0 ? i : a, o = r instanceof On ? C.near(n, t) : C.findFrom(n, t);
		return o ? Ba(e, o) : !1;
	}
	return !1;
}
function Qa(e, t) {
	if (!(e.state.selection instanceof w)) return !0;
	let { $head: n, $anchor: r, empty: i } = e.state.selection;
	if (!n.sameParent(r)) return !0;
	if (!i) return !1;
	if (e.endOfTextblock(t > 0 ? "forward" : "backward")) return !0;
	let a = !n.textOffset && (t < 0 ? n.nodeBefore : n.nodeAfter);
	if (a && !a.isText) {
		let r = e.state.tr;
		return t < 0 ? r.delete(n.pos - a.nodeSize, n.pos) : r.delete(n.pos, n.pos + a.nodeSize), e.dispatch(r), !0;
	}
	return !1;
}
function $a(e, t, n) {
	e.domObserver.stop(), t.contentEditable = n, e.domObserver.start();
}
function eo(e) {
	if (!ui || e.state.selection.$head.parentOffset > 0) return !1;
	let { focusNode: t, focusOffset: n } = e.domSelectionRange();
	if (t && O(t) && n == 0 && t.firstChild && O(t.firstChild) && t.firstChild.contentEditable == "false") {
		let n = t.firstChild;
		$a(e, n, "true"), setTimeout(() => $a(e, n, "false"), 20);
	}
	return !1;
}
function to(e) {
	let t = "";
	return e.ctrlKey && (t += "c"), e.metaKey && (t += "m"), e.altKey && (t += "a"), e.shiftKey && (t += "s"), t;
}
function no(e, t) {
	let n = t.keyCode, r = to(t);
	if (n == 8 || fi && n == 72 && r == "c") return Qa(e, -1) || Ua(e, -1);
	if (n == 46 && !t.shiftKey || fi && n == 68 && r == "c") return Qa(e, 1) || Ua(e, 1);
	if (n == 13 || n == 27) return !0;
	if (n == 37 || fi && n == 66 && r == "c") {
		let t = n == 37 ? Xa(e, e.state.selection.from) == "ltr" ? -1 : 1 : -1;
		return Va(e, t, r) || Ua(e, t);
	}
	if (n == 39 || fi && n == 70 && r == "c") {
		let t = n == 39 ? Xa(e, e.state.selection.from) == "ltr" ? 1 : -1 : 1;
		return Va(e, t, r) || Ua(e, t);
	}
	return n == 38 || fi && n == 80 && r == "c" ? Za(e, -1, r) || Ua(e, -1) : n == 40 || fi && n == 78 && r == "c" ? eo(e) || Za(e, 1, r) || Ua(e, 1) : !(r != (fi ? "m" : "c") || n != 66 && n != 73 && n != 89 && n != 90);
}
function ro(e, t) {
	e.someProp("transformCopied", (n) => {
		t = n(t, e);
	});
	let n = [], { content: r, openStart: i, openEnd: a } = t;
	for (; i > 1 && a > 1 && r.childCount == 1 && r.firstChild.childCount == 1;) {
		i--, a--;
		let e = r.firstChild;
		n.push(e.type.name, e.attrs == e.type.defaultAttrs ? null : e.attrs), r = e.content;
	}
	let o = e.someProp("clipboardSerializer") || et.fromSchema(e.state.schema), s = mo(), c = s.createElement("div");
	c.appendChild(o.serializeFragment(r, { document: s }));
	let l = c.firstChild, u, d = 0;
	for (; l && O(l) && (u = po[l.nodeName.toLowerCase()]);) {
		for (let e = u.length - 1; e >= 0; e--) {
			let t = s.createElement(u[e]);
			for (; c.firstChild;) t.appendChild(c.firstChild);
			c.appendChild(t), d++;
		}
		l = c.firstChild;
	}
	return l && O(l) && l.setAttribute("data-pm-slice", `${i} ${a}${d ? ` -${d}` : ""} ${JSON.stringify(n)}`), {
		dom: c,
		text: e.someProp("clipboardTextSerializer", (n) => n(t, e)) || t.content.textBetween(0, t.content.size, "\n\n"),
		slice: t
	};
}
function io(e, t, n, r, i) {
	let a = i.parent.type.spec.code, o, s;
	if (!n && !t) return null;
	let c = !!t && (r || a || !n);
	if (c) {
		if (e.someProp("transformPastedText", (n) => {
			t = n(t, a || r, e);
		}), a) return s = new h(l.from(e.state.schema.text(t.replace(/\r\n?/g, "\n"))), 0, 0), e.someProp("transformPasted", (t) => {
			s = t(s, e, !0);
		}), s;
		let n = e.someProp("clipboardTextParser", (n) => n(t, i, r, e));
		if (n) s = n;
		else {
			let n = i.marks(), { schema: r } = e.state, a = et.fromSchema(r);
			o = document.createElement("div"), t.split(/(?:\r\n?|\n)+/).forEach((e) => {
				let t = o.appendChild(document.createElement("p"));
				e && t.appendChild(a.serializeNode(r.text(e, n)));
			});
		}
	} else e.someProp("transformPastedHTML", (t) => {
		n = t(n, e);
	}), o = _o(n), hi && vo(o);
	let u = o && o.querySelector("[data-pm-slice]"), d = u && /^(\d+) (\d+)(?: -(\d+))? (.*)/.exec(u.getAttribute("data-pm-slice") || "");
	if (d && d[3]) for (let e = +d[3]; e > 0; e--) {
		let e = o.firstChild;
		for (; e && !O(e);) e = e.nextSibling;
		if (!e) break;
		o = e;
	}
	if (s || (s = (e.someProp("clipboardParser") || e.someProp("domParser") || Be.fromSchema(e.state.schema)).parseSlice(o, {
		preserveWhitespace: !!(c || d),
		context: i,
		ruleFromNode(e) {
			return e.nodeName == "BR" && !e.nextSibling && e.parentNode && !ao.test(e.parentNode.nodeName) ? { ignore: !0 } : null;
		}
	})), d) s = yo(fo(s, +d[1], +d[2]), d[4]);
	else if (s = h.maxOpen(oo(s.content, i), !0), s.openStart || s.openEnd) {
		let e = 0, t = 0;
		for (let t = s.content.firstChild; e < s.openStart && !t.type.spec.isolating; e++, t = t.firstChild);
		for (let e = s.content.lastChild; t < s.openEnd && !e.type.spec.isolating; t++, e = e.lastChild);
		s = fo(s, e, t);
	}
	return e.someProp("transformPasted", (t) => {
		s = t(s, e, c);
	}), s;
}
var ao = /^(a|abbr|acronym|b|cite|code|del|em|i|ins|kbd|label|output|q|ruby|s|samp|span|strong|sub|sup|time|u|tt|var)$/i;
function oo(e, t) {
	if (e.childCount < 2) return e;
	for (let n = t.depth; n >= 0; n--) {
		let r = t.node(n).contentMatchAt(t.index(n)), i, a = [];
		if (e.forEach((e) => {
			if (!a) return;
			let t = r.findWrapping(e.type), n;
			if (!t) return a = null;
			if (n = a.length && i.length && co(t, i, e, a[a.length - 1], 0)) a[a.length - 1] = n;
			else {
				a.length && (a[a.length - 1] = lo(a[a.length - 1], i.length));
				let n = so(e, t);
				a.push(n), r = r.matchType(n.type), i = t;
			}
		}), a) return l.from(a);
	}
	return e;
}
function so(e, t, n = 0) {
	for (let r = t.length - 1; r >= n; r--) e = t[r].create(null, l.from(e));
	return e;
}
function co(e, t, n, r, i) {
	if (i < e.length && i < t.length && e[i] == t[i]) {
		let a = co(e, t, n, r.lastChild, i + 1);
		if (a) return r.copy(r.content.replaceChild(r.childCount - 1, a));
		if (r.contentMatchAt(r.childCount).matchType(i == e.length - 1 ? n.type : e[i + 1])) return r.copy(r.content.append(l.from(so(n, e, i + 1))));
	}
}
function lo(e, t) {
	if (t == 0) return e;
	let n = e.content.replaceChild(e.childCount - 1, lo(e.lastChild, t - 1)), r = e.contentMatchAt(e.childCount).fillBefore(l.empty, !0);
	return e.copy(n.append(r));
}
function uo(e, t, n, r, i, a) {
	let o = t < 0 ? e.firstChild : e.lastChild, s = o.content;
	return e.childCount > 1 && (a = 0), i < r - 1 && (s = uo(s, t, n, r, i + 1, a)), i >= n && (s = t < 0 ? o.contentMatchAt(0).fillBefore(s, a <= i).append(s) : s.append(o.contentMatchAt(o.childCount).fillBefore(l.empty, !0))), e.replaceChild(t < 0 ? 0 : e.childCount - 1, o.copy(s));
}
function fo(e, t, n) {
	return t < e.openStart && (e = new h(uo(e.content, -1, t, e.openStart, 0, e.openEnd), t, e.openEnd)), n < e.openEnd && (e = new h(uo(e.content, 1, n, e.openEnd, 0, 0), e.openStart, n)), e;
}
var po = {
	thead: ["table"],
	tbody: ["table"],
	tfoot: ["table"],
	caption: ["table"],
	colgroup: ["table"],
	col: ["table", "colgroup"],
	tr: ["table", "tbody"],
	td: [
		"table",
		"tbody",
		"tr"
	],
	th: [
		"table",
		"tbody",
		"tr"
	]
};
function mo() {
	return document.implementation.createHTMLDocument("title");
}
var ho = null;
function go(e) {
	let t = window.trustedTypes;
	if (!t) return e;
	if (!ho) {
		if (ho = t.defaultPolicy) try {
			return ho.createHTML(e);
		} catch {}
		ho = t.createPolicy("ProseMirrorClipboard", { createHTML: (e) => e });
	}
	return ho.createHTML(e);
}
function _o(e) {
	let t = /^(\s*<meta [^>]*>)*/.exec(e);
	t && (e = e.slice(t[0].length));
	let n = mo(), r = n.body, i = /<([a-z][^>\s]+)/i.exec(e), a;
	if ((a = i && po[i[1].toLowerCase()]) && (e = a.map((e) => "<" + e + ">").join("") + e + a.map((e) => "</" + e + ">").reverse().join("")), r.innerHTML = go(e), a) for (let e = 0; e < a.length; e++) r = r.querySelector(a[e]) || r;
	for (let e = 0; e < n.styleSheets.length; e++) {
		let t = n.styleSheets[e];
		for (let e = 0; e < t.rules.length; e++) {
			let n = t.rules[e];
			if (n instanceof CSSStyleRule) {
				let e = r.querySelectorAll(n.selectorText);
				for (let t = 0; t < e.length; t++) e[t].style.cssText += n.style.cssText;
			}
		}
	}
	return r;
}
function vo(e) {
	let t = e.querySelectorAll(ci ? "span:not([class]):not([style])" : "span.Apple-converted-space");
	for (let n = 0; n < t.length; n++) {
		let r = t[n];
		r.childNodes.length == 1 && r.textContent == "\xA0" && r.parentNode && r.parentNode.replaceChild(e.ownerDocument.createTextNode(" "), r);
	}
}
function yo(e, t) {
	if (!e.size) return e;
	let n = e.content.firstChild.type.schema, r;
	try {
		r = JSON.parse(t);
	} catch {
		return e;
	}
	let { content: i, openStart: a, openEnd: o } = e;
	for (let e = r.length - 2; e >= 0; e -= 2) {
		let t = n.nodes[r[e]];
		if (!t || t.hasRequiredAttrs()) break;
		try {
			i = l.from(t.create(r[e + 1], i));
		} catch {
			break;
		}
		a++, o++;
	}
	return new h(i, a, o);
}
var bo = {}, xo = {}, So = {
	touchstart: !0,
	touchmove: !0
}, Co = class {
	constructor() {
		this.shiftKey = !1, this.mouseDown = null, this.lastKeyCode = null, this.lastKeyCodeTime = 0, this.lastClick = {
			time: 0,
			x: 0,
			y: 0,
			type: "",
			button: 0
		}, this.lastSelectionOrigin = null, this.lastSelectionTime = 0, this.lastIOSEnter = 0, this.lastIOSEnterFallbackTimeout = -1, this.lastFocus = 0, this.lastTouch = 0, this.lastChromeDelete = 0, this.composing = !1, this.compositionNode = null, this.composingTimeout = -1, this.compositionNodes = [], this.compositionEndedAt = -2e8, this.compositionID = 1, this.badSafariComposition = !1, this.compositionPendingChanges = 0, this.domChangeCount = 0, this.eventHandlers = Object.create(null), this.hideSelectionGuard = null;
	}
};
function wo(e) {
	for (let t in bo) {
		let n = bo[t];
		e.dom.addEventListener(t, e.input.eventHandlers[t] = (t) => {
			ko(e, t) && !Oo(e, t) && (e.editable || !(t.type in xo)) && n(e, t);
		}, So[t] ? { passive: !0 } : void 0);
	}
	ui && e.dom.addEventListener("input", () => null), Do(e);
}
function To(e, t) {
	e.input.lastSelectionOrigin = t, e.input.lastSelectionTime = Date.now();
}
function Eo(e) {
	e.input.mouseDown && e.input.mouseDown.done(), e.domObserver.stop();
	for (let t in e.input.eventHandlers) e.dom.removeEventListener(t, e.input.eventHandlers[t]);
	clearTimeout(e.input.composingTimeout), clearTimeout(e.input.lastIOSEnterFallbackTimeout);
}
function Do(e) {
	e.someProp("handleDOMEvents", (t) => {
		for (let n in t) e.input.eventHandlers[n] || e.dom.addEventListener(n, e.input.eventHandlers[n] = (t) => Oo(e, t));
	});
}
function Oo(e, t) {
	return e.someProp("handleDOMEvents", (n) => {
		let r = n[t.type];
		return r ? r(e, t) || t.defaultPrevented : !1;
	});
}
function ko(e, t) {
	if (!t.bubbles) return !0;
	if (t.defaultPrevented) return !1;
	for (let n = t.target; n != e.dom; n = n.parentNode) if (!n || n.nodeType == 11 || n.pmViewDesc && n.pmViewDesc.stopEvent(t)) return !1;
	return !0;
}
function Ao(e, t) {
	!Oo(e, t) && bo[t.type] && (e.editable || !(t.type in xo)) && bo[t.type](e, t);
}
xo.keydown = (e, t) => {
	let n = t;
	if (e.input.shiftKey = n.keyCode == 16 || n.shiftKey, !qo(e) && (e.input.lastKeyCode = n.keyCode, e.input.lastKeyCodeTime = Date.now(), !(mi && ci && n.keyCode == 13))) {
		if (n.keyCode != 229 && e.domObserver.forceFlush(), di && n.keyCode == 13 && !n.ctrlKey && !n.altKey && !n.metaKey) {
			let t = Date.now();
			e.input.lastIOSEnter = t, e.input.lastIOSEnterFallbackTimeout = setTimeout(() => {
				e.input.lastIOSEnter == t && (e.someProp("handleKeyDown", (t) => t(e, Yr(13, "Enter"))), e.input.lastIOSEnter = 0);
			}, 200);
		} else e.someProp("handleKeyDown", (t) => t(e, n)) || no(e, n) ? n.preventDefault() : To(e, "key");
	}
}, xo.keyup = (e, t) => {
	t.keyCode == 16 && (e.input.shiftKey = !1);
}, xo.keypress = (e, t) => {
	let n = t;
	if (qo(e) || !n.charCode || n.ctrlKey && !n.altKey || fi && n.metaKey) return;
	if (e.someProp("handleKeyPress", (t) => t(e, n))) {
		n.preventDefault();
		return;
	}
	let r = e.state.selection;
	if (!(r instanceof w) || !r.$from.sameParent(r.$to)) {
		let t = String.fromCharCode(n.charCode), i = () => e.state.tr.insertText(t).scrollIntoView();
		!/[\r\n]/.test(t) && !e.someProp("handleTextInput", (n) => n(e, r.$from.pos, r.$to.pos, t, i)) && e.dispatch(i()), n.preventDefault();
	}
};
function jo(e) {
	return {
		left: e.clientX,
		top: e.clientY
	};
}
function Mo(e, t) {
	let n = t.x - e.clientX, r = t.y - e.clientY;
	return n * n + r * r < 100;
}
function No(e, t, n, r, i) {
	if (r == -1) return !1;
	let a = e.state.doc.resolve(r);
	for (let r = a.depth + 1; r > 0; r--) if (e.someProp(t, (t) => r > a.depth ? t(e, n, a.nodeAfter, a.before(r), i, !0) : t(e, n, a.node(r), a.before(r), i, !1))) return !0;
	return !1;
}
function Po(e, t, n) {
	if (e.focused || e.focus(), e.state.selection.eq(t)) return;
	let r = e.state.tr.setSelection(t);
	r.setMeta("pointer", !0), e.dispatch(r);
}
function Fo(e, t) {
	if (t == -1) return !1;
	let n = e.state.doc.resolve(t), r = n.nodeAfter;
	return r && r.isAtom && T.isSelectable(r) ? (Po(e, new T(n)), !0) : !1;
}
function Io(e, t) {
	if (t == -1) return !1;
	let n = e.state.selection, r, i;
	n instanceof T && (r = n.node);
	let a = e.state.doc.resolve(t);
	for (let e = a.depth + 1; e > 0; e--) {
		let t = e > a.depth ? a.nodeAfter : a.node(e);
		if (T.isSelectable(t)) {
			i = r && n.$from.depth > 0 && e >= n.$from.depth && a.before(n.$from.depth + 1) == n.$from.pos ? a.before(n.$from.depth) : a.before(e);
			break;
		}
	}
	return i != null && (Po(e, T.create(e.state.doc, i)), !0);
}
function Lo(e, t, n, r, i) {
	return No(e, "handleClickOn", t, n, r) || e.someProp("handleClick", (n) => n(e, t, r)) || (i ? Io(e, n) : Fo(e, n));
}
function Ro(e, t, n, r) {
	return No(e, "handleDoubleClickOn", t, n, r) || e.someProp("handleDoubleClick", (n) => n(e, t, r));
}
function zo(e, t, n, r) {
	return No(e, "handleTripleClickOn", t, n, r) || e.someProp("handleTripleClick", (n) => n(e, t, r)) || Bo(e, n, r);
}
function Bo(e, t, n) {
	if (n.button != 0) return !1;
	let r = Vo(e, t, !0), i = e.state.doc;
	return r ? (Po(e, r), r instanceof w && i.eq(e.state.doc) && (e.input.mouseDown = new Ko(e, r)), !0) : !1;
}
function Vo(e, t, n) {
	let r = e.state.doc;
	if (t == -1) return r.inlineContent ? w.create(r, 0, r.content.size) : null;
	let i = r.resolve(t);
	for (let e = i.depth + 1; e > 0; e--) {
		let t = e > i.depth ? i.nodeAfter : i.node(e), a = i.before(e);
		if (t.inlineContent) return w.create(r, a + 1, a + 1 + t.content.size);
		if (n && T.isSelectable(t)) return T.create(r, a);
	}
	return null;
}
function Ho(e) {
	return $o(e);
}
var Uo = fi ? "metaKey" : "ctrlKey";
bo.mousedown = (e, t) => {
	let n = t;
	e.input.shiftKey = n.shiftKey;
	let r = Ho(e), i = Date.now(), a = "singleClick";
	i - e.input.lastClick.time < 500 && Mo(n, e.input.lastClick) && !n[Uo] && e.input.lastClick.button == n.button && (e.input.lastClick.type == "singleClick" ? a = "doubleClick" : e.input.lastClick.type == "doubleClick" && (a = "tripleClick")), e.input.lastClick = {
		time: i,
		x: n.clientX,
		y: n.clientY,
		type: a,
		button: n.button
	}, e.input.mouseDown && e.input.mouseDown.done();
	let o = e.posAtCoords(jo(n));
	o && (a == "singleClick" ? e.input.mouseDown = new Go(e, o, n, !!r) : (a == "doubleClick" ? Ro : zo)(e, o.pos, o.inside, n) ? n.preventDefault() : To(e, "pointer"));
};
var Wo = class {
	constructor(e) {
		this.view = e, this.mightDrag = null, e.root.addEventListener("mouseup", this.up = this.up.bind(this)), e.root.addEventListener("mousemove", this.move = this.move.bind(this));
	}
	up(e) {
		this.done();
	}
	move(e) {
		e.buttons == 0 && this.done();
	}
	done() {
		this.view.root.removeEventListener("mouseup", this.up), this.view.root.removeEventListener("mousemove", this.move), this.view.input.mouseDown == this && (this.view.input.mouseDown = null);
	}
	delaySelUpdate() {
		return !1;
	}
}, Go = class extends Wo {
	constructor(e, t, n, r) {
		super(e), this.pos = t, this.event = n, this.flushed = r, this.delayedSelectionSync = !1, this.startDoc = e.state.doc, this.selectNode = !!n[Uo], this.allowDefault = n.shiftKey;
		let i, a;
		if (t.inside > -1) i = e.state.doc.nodeAt(t.inside), a = t.inside;
		else {
			let n = e.state.doc.resolve(t.pos);
			i = n.parent, a = n.depth ? n.before() : 0;
		}
		let o = r ? null : n.target, s = o ? e.docView.nearestDesc(o, !0) : null;
		this.target = s && O(s.nodeDOM) ? s.nodeDOM : null;
		let { selection: c } = e.state;
		n.button == 0 && (i.type.spec.draggable && i.type.spec.selectable !== !1 || c instanceof T && c.from <= a && c.to > a) && (this.mightDrag = {
			node: i,
			pos: a,
			addAttr: !(!this.target || this.target.draggable),
			setUneditable: !!(this.target && oi && !this.target.hasAttribute("contentEditable"))
		}), this.target && this.mightDrag && (this.mightDrag.addAttr || this.mightDrag.setUneditable) && (this.view.domObserver.stop(), this.mightDrag.addAttr && (this.target.draggable = !0), this.mightDrag.setUneditable && setTimeout(() => {
			this.view.input.mouseDown == this && this.target.setAttribute("contentEditable", "false");
		}, 20), this.view.domObserver.start()), To(e, "pointer");
	}
	done() {
		super.done(), this.mightDrag && this.target && (this.view.domObserver.stop(), this.mightDrag.addAttr && this.target.removeAttribute("draggable"), this.mightDrag.setUneditable && this.target.removeAttribute("contentEditable"), this.view.domObserver.start()), this.delayedSelectionSync && setTimeout(() => {
			this.view.isDestroyed || Ea(this.view);
		});
	}
	up(e) {
		if (this.done(), !this.view.dom.contains(e.target)) return;
		let t = this.pos;
		this.view.state.doc != this.startDoc && (t = this.view.posAtCoords(jo(e))), this.updateAllowDefault(e), this.allowDefault || !t ? To(this.view, "pointer") : Lo(this.view, t.pos, t.inside, e, this.selectNode) ? e.preventDefault() : e.button == 0 && (this.flushed || ui && this.mightDrag && !this.mightDrag.node.isAtom || ci && !this.view.state.selection.visible && Math.min(Math.abs(t.pos - this.view.state.selection.from), Math.abs(t.pos - this.view.state.selection.to)) <= 2) ? (Po(this.view, C.near(this.view.state.doc.resolve(t.pos))), e.preventDefault()) : To(this.view, "pointer");
	}
	move(e) {
		this.updateAllowDefault(e), To(this.view, "pointer"), super.move(e);
	}
	updateAllowDefault(e) {
		!this.allowDefault && (Math.abs(this.event.x - e.clientX) > 4 || Math.abs(this.event.y - e.clientY) > 4) && (this.allowDefault = !0);
	}
	delaySelUpdate() {
		return this.allowDefault ? (this.delayedSelectionSync = !0, !0) : !1;
	}
}, Ko = class extends Wo {
	constructor(e, t) {
		super(e), this.startSelection = t, this.startDoc = e.state.doc;
	}
	move(e) {
		if (e.buttons == 0 || this.view.isDestroyed || !this.view.state.doc.eq(this.startDoc)) {
			this.done();
			return;
		}
		e.preventDefault(), To(this.view, "pointer");
		let t = this.view.posAtCoords(jo(e)), n = t && Vo(this.view, t.inside, !1);
		if (!n) return;
		let { doc: r } = this.view.state, i = this.startSelection, [a, o] = n.from < i.from ? [i.to, n.from] : [i.from, n.to];
		Po(this.view, w.create(r, a, o));
	}
};
bo.touchstart = (e) => {
	e.input.lastTouch = Date.now(), Ho(e), To(e, "pointer");
}, bo.touchmove = (e) => {
	e.input.lastTouch = Date.now(), To(e, "pointer");
}, bo.contextmenu = (e) => Ho(e);
function qo(e, t) {
	return e.composing ? !0 : ui && Math.abs(Date.now() - e.input.compositionEndedAt) < 500 ? (e.input.compositionEndedAt = -2e8, !0) : !1;
}
var Jo = mi ? 5e3 : -1;
xo.compositionstart = xo.compositionupdate = (e) => {
	if (!e.composing) {
		e.domObserver.flush();
		let { state: t } = e, n = t.selection.$to;
		if (t.selection instanceof w && t.selection.empty && (t.storedMarks || !n.textOffset && n.parentOffset && n.nodeBefore.marks.some((e) => e.type.spec.inclusive === !1) || ci && pi && Yo(e))) e.markCursor = e.state.storedMarks || n.marks(), $o(e, !0), e.markCursor = null;
		else if ($o(e, !t.selection.empty), oi && t.selection.empty && n.parentOffset && !n.textOffset && n.nodeBefore.marks.length) {
			let t = e.domSelectionRange();
			for (let n = t.focusNode, r = t.focusOffset; n && O(n) && r != 0;) {
				let t = r < 0 ? n.lastChild : n.childNodes[r - 1];
				if (!t) break;
				if (k(t)) {
					let n = e.domSelection();
					n && n.collapse(t, t.nodeValue.length);
					break;
				}
				n = t, r = -1;
			}
		}
		e.input.composing = !0;
	}
	Xo(e, Jo);
};
function Yo(e) {
	let { focusNode: t, focusOffset: n } = e.domSelectionRange();
	if (!t || !O(t) || n >= t.childNodes.length) return !1;
	let r = t.childNodes[n];
	return O(r) && r.contentEditable == "false";
}
xo.compositionend = (e, t) => {
	e.composing && (e.input.composing = !1, e.input.compositionEndedAt = Date.now(), e.input.compositionPendingChanges = e.domObserver.pendingRecords().length ? e.input.compositionID : 0, e.input.compositionNode = null, e.input.badSafariComposition ? e.domObserver.forceFlush() : e.input.compositionPendingChanges && Promise.resolve().then(() => e.domObserver.flush()), e.input.compositionID++, Xo(e, 20));
};
function Xo(e, t) {
	clearTimeout(e.input.composingTimeout), t > -1 && (e.input.composingTimeout = setTimeout(() => $o(e), t));
}
function Zo(e) {
	for (e.composing && (e.input.composing = !1, e.input.compositionEndedAt = Date.now()); e.input.compositionNodes.length > 0;) e.input.compositionNodes.pop().markParentsDirty();
}
function Qo(e) {
	let t = e.domSelectionRange();
	if (!t.focusNode) return null;
	let n = Wr(t.focusNode, t.focusOffset), r = Gr(t.focusNode, t.focusOffset);
	if (n && r && n != r) {
		let t = r.pmViewDesc, i = e.domObserver.lastChangedTextNode;
		if (n == i || r == i) return i;
		if (!t || !t.isText(r.nodeValue)) return r;
		if (e.input.compositionNode == r) {
			let e = n.pmViewDesc;
			if (e && e.isText(n.nodeValue)) return r;
		}
	}
	return n || r;
}
function $o(e, t = !1) {
	if (!(mi && e.domObserver.flushingSoon >= 0)) {
		if (e.domObserver.forceFlush(), Zo(e), t || e.docView && e.docView.dirty) {
			let n = wa(e), r = e.state.selection;
			return n && !n.eq(r) ? e.dispatch(e.state.tr.setSelection(n)) : (e.markCursor || t) && !r.$from.node(r.$from.sharedDepth(r.to)).inlineContent ? e.dispatch(e.state.tr.deleteSelection()) : e.updateState(e.state), !0;
		}
		return !1;
	}
}
function es(e, t) {
	if (!e.dom.parentNode) return;
	let n = e.dom.parentNode.appendChild(document.createElement("div"));
	n.appendChild(t), n.style.cssText = "position: fixed; left: -10000px; top: 10px";
	let r = getSelection(), i = document.createRange();
	i.selectNodeContents(t), e.dom.blur(), r.removeAllRanges(), r.addRange(i), setTimeout(() => {
		n.parentNode && n.parentNode.removeChild(n), e.focus();
	}, 50);
}
var ts = ii && ai < 15 || di && gi < 604;
bo.copy = xo.cut = (e, t) => {
	let n = t, r = e.state.selection, i = n.type == "cut";
	if (r.empty) return;
	let a = ts ? null : n.clipboardData, { dom: o, text: s } = ro(e, r.content());
	a ? (n.preventDefault(), a.clearData(), a.setData("text/html", o.innerHTML), a.setData("text/plain", s)) : es(e, o), i && e.dispatch(e.state.tr.deleteSelection().scrollIntoView().setMeta("uiEvent", "cut"));
};
function ns(e) {
	return e.openStart == 0 && e.openEnd == 0 && e.content.childCount == 1 ? e.content.firstChild : null;
}
function rs(e, t) {
	if (!e.dom.parentNode) return;
	let n = e.input.shiftKey || e.state.selection.$from.parent.type.spec.code, r = e.dom.parentNode.appendChild(document.createElement(n ? "textarea" : "div"));
	n || (r.contentEditable = "true"), r.style.cssText = "position: fixed; left: -10000px; top: 10px", r.focus();
	let i = e.input.shiftKey && e.input.lastKeyCode != 45;
	setTimeout(() => {
		e.focus(), r.parentNode && r.parentNode.removeChild(r), n ? is(e, r.value, null, i, t) : is(e, r.textContent, r.innerHTML, i, t);
	}, 50);
}
function is(e, t, n, r, i) {
	let a = io(e, t, n, r, e.state.selection.$from);
	if (e.someProp("handlePaste", (t) => t(e, i, a || h.empty))) return !0;
	if (!a) return !1;
	let o = ns(a), s = o ? e.state.tr.replaceSelectionWith(o, r) : e.state.tr.replaceSelection(a);
	return e.dispatch(s.scrollIntoView().setMeta("paste", !0).setMeta("uiEvent", "paste")), !0;
}
function as(e) {
	let t = e.getData("text/plain") || e.getData("Text");
	if (t) return t;
	let n = e.getData("text/uri-list");
	return n ? n.replace(/\r?\n/g, " ") : "";
}
xo.paste = (e, t) => {
	let n = t;
	if (e.composing && !mi) return;
	let r = ts ? null : n.clipboardData, i = e.input.shiftKey && e.input.lastKeyCode != 45;
	r && is(e, as(r), r.getData("text/html"), i, n) ? n.preventDefault() : rs(e, n);
};
var ss = class {
	constructor(e, t, n) {
		this.slice = e, this.move = t, this.node = n;
	}
}, cs = fi ? "altKey" : "ctrlKey";
function ls(e, t) {
	let n;
	return e.someProp("dragCopies", (e) => {
		n = n || e(t);
	}), n == null ? !t[cs] : !n;
}
bo.dragstart = (e, t) => {
	let n = t, r = e.input.mouseDown;
	if (r && r.done(), !n.dataTransfer) return;
	let i = e.state.selection, a = i.empty ? null : e.posAtCoords(jo(n)), o;
	if (!(a && a.pos >= i.from && a.pos <= (i instanceof T ? i.to - 1 : i.to))) {
		if (r && r.mightDrag) o = T.create(e.state.doc, r.mightDrag.pos);
		else if (n.target && O(n.target)) {
			let t = e.docView.nearestDesc(n.target, !0);
			t && t.node.type.spec.draggable && t != e.docView && (o = T.create(e.state.doc, t.posBefore));
		}
	}
	let { dom: s, text: c, slice: l } = ro(e, (o || e.state.selection).content());
	(!n.dataTransfer.files.length || !ci || li > 120) && n.dataTransfer.clearData(), n.dataTransfer.setData(ts ? "Text" : "text/html", s.innerHTML), n.dataTransfer.effectAllowed = "copyMove", ts || n.dataTransfer.setData("text/plain", c), e.dragging = new ss(l, ls(e, n), o);
}, bo.dragend = (e) => {
	let t = e.dragging;
	window.setTimeout(() => {
		e.dragging == t && (e.dragging = null);
	}, 50);
}, xo.dragover = xo.dragenter = (e, t) => t.preventDefault(), xo.drop = (e, t) => {
	try {
		us(e, t, e.dragging);
	} finally {
		e.dragging = null;
	}
};
function us(e, t, n) {
	if (!t.dataTransfer) return;
	let r = e.posAtCoords(jo(t));
	if (!r) return;
	let i = e.state.doc.resolve(r.pos), a = n && n.slice;
	a ? e.someProp("transformPasted", (t) => {
		a = t(a, e, !1);
	}) : a = io(e, as(t.dataTransfer), ts ? null : t.dataTransfer.getData("text/html"), !1, i);
	let o = !!(n && ls(e, t));
	if (e.someProp("handleDrop", (n) => n(e, t, a || h.empty, o))) {
		t.preventDefault();
		return;
	}
	if (!a) return;
	t.preventDefault();
	let s = a ? en(e.state.doc, i.pos, a) : i.pos;
	s ?? (s = i.pos);
	let c = e.state.tr;
	if (o) {
		let { node: e } = n;
		e ? e.replace(c) : c.deleteSelection();
	}
	let l = c.mapping.map(s), u = a.openStart == 0 && a.openEnd == 0 && a.content.childCount == 1, d = c.doc;
	if (u ? c.replaceRangeWith(l, l, a.content.firstChild) : c.replaceRange(l, l, a), c.doc.eq(d)) return;
	let f = c.doc.resolve(l);
	if (u && T.isSelectable(a.content.firstChild) && f.nodeAfter && f.nodeAfter.sameMarkup(a.content.firstChild)) c.setSelection(new T(f));
	else {
		let t = c.mapping.map(s);
		c.mapping.maps[c.mapping.maps.length - 1].forEach((e, n, r, i) => t = i), c.setSelection(Fa(e, f, c.doc.resolve(t)));
	}
	e.focus(), e.dispatch(c.setMeta("uiEvent", "drop"));
}
bo.focus = (e) => {
	e.input.lastFocus = Date.now(), e.focused || (e.domObserver.stop(), e.dom.classList.add("ProseMirror-focused"), e.domObserver.start(), e.focused = !0, setTimeout(() => {
		e.docView && e.hasFocus() && !e.domObserver.currentSelection.eq(e.domSelectionRange()) && Ea(e);
	}, 20));
}, bo.blur = (e, t) => {
	let n = t;
	e.focused && (e.domObserver.stop(), e.dom.classList.remove("ProseMirror-focused"), e.domObserver.start(), n.relatedTarget && e.dom.contains(n.relatedTarget) && e.domObserver.currentSelection.clear(), e.focused = !1);
}, bo.beforeinput = (e, t) => {
	if (mi && t.inputType == "deleteContentBackward") {
		e.domObserver.flushSoon();
		let { domChangeCount: t } = e.input;
		setTimeout(() => {
			if (e.input.domChangeCount != t || (e.dom.blur(), e.focus(), e.someProp("handleKeyDown", (t) => t(e, Yr(8, "Backspace"))))) return;
			let { $cursor: n } = e.state.selection;
			n && n.pos > 0 && e.dispatch(e.state.tr.delete(n.pos - 1, n.pos).scrollIntoView());
		}, 50);
	}
};
for (let e in xo) bo[e] = xo[e];
function ds(e, t) {
	if (e == t) return !0;
	for (let n in e) if (e[n] !== t[n]) return !1;
	for (let n in t) if (!(n in e)) return !1;
	return !0;
}
var fs = class e {
	constructor(e, t) {
		this.toDOM = e, this.spec = t || gs, this.side = this.spec.side || 0;
	}
	map(e, t, n, r) {
		let { pos: i, deleted: a } = e.mapResult(t.from + r, this.side < 0 ? -1 : 1);
		return a ? null : new A(i - n, i - n, this);
	}
	valid() {
		return !0;
	}
	eq(t) {
		return this == t || t instanceof e && (this.spec.key && this.spec.key == t.spec.key || this.toDOM == t.toDOM && ds(this.spec, t.spec));
	}
	destroy(e) {
		this.spec.destroy && this.spec.destroy(e);
	}
}, ps = class e {
	constructor(e, t) {
		this.attrs = e, this.spec = t || gs;
	}
	map(e, t, n, r) {
		let i = e.map(t.from + r, this.spec.inclusiveStart ? -1 : 1) - n, a = e.map(t.to + r, this.spec.inclusiveEnd ? 1 : -1) - n;
		return i >= a ? null : new A(i, a, this);
	}
	valid(e, t) {
		return t.from < t.to;
	}
	eq(t) {
		return this == t || t instanceof e && ds(this.attrs, t.attrs) && ds(this.spec, t.spec);
	}
	static is(t) {
		return t.type instanceof e;
	}
	destroy() {}
}, ms = class e {
	constructor(e, t) {
		this.attrs = e, this.spec = t || gs;
	}
	map(e, t, n, r) {
		let i = e.mapResult(t.from + r, 1);
		if (i.deleted) return null;
		let a = e.mapResult(t.to + r, -1);
		return a.deleted || a.pos <= i.pos ? null : new A(i.pos - n, a.pos - n, this);
	}
	valid(e, t) {
		let { index: n, offset: r } = e.content.findIndex(t.from), i;
		return r == t.from && !(i = e.child(n)).isText && r + i.nodeSize == t.to;
	}
	eq(t) {
		return this == t || t instanceof e && ds(this.attrs, t.attrs) && ds(this.spec, t.spec);
	}
	destroy() {}
}, A = class e {
	constructor(e, t, n) {
		this.from = e, this.to = t, this.type = n;
	}
	copy(t, n) {
		return new e(t, n, this.type);
	}
	eq(e, t = 0) {
		return this.type.eq(e.type) && this.from + t == e.from && this.to + t == e.to;
	}
	map(e, t, n) {
		return this.type.map(e, this, t, n);
	}
	static widget(t, n, r) {
		return new e(t, t, new fs(n, r));
	}
	static inline(t, n, r, i) {
		return new e(t, n, new ps(r, i));
	}
	static node(t, n, r, i) {
		return new e(t, n, new ms(r, i));
	}
	get spec() {
		return this.type.spec;
	}
	get inline() {
		return this.type instanceof ps;
	}
	get widget() {
		return this.type instanceof fs;
	}
}, hs = [], gs = {}, j = class e {
	constructor(e, t) {
		this.local = e.length ? e : hs, this.children = t.length ? t : hs;
	}
	static create(e, t) {
		return t.length ? ws(t, e, 0, gs) : _s;
	}
	find(e, t, n) {
		let r = [];
		return this.findInner(e ?? 0, t ?? 1e9, r, 0, n), r;
	}
	findInner(e, t, n, r, i) {
		for (let a = 0; a < this.local.length; a++) {
			let o = this.local[a];
			o.from <= t && o.to >= e && (!i || i(o.spec)) && n.push(o.copy(o.from + r, o.to + r));
		}
		for (let a = 0; a < this.children.length; a += 3) if (this.children[a] < t && this.children[a + 1] > e) {
			let o = this.children[a] + 1;
			this.children[a + 2].findInner(e - o, t - o, n, r + o, i);
		}
	}
	map(e, t, n) {
		return this == _s || e.maps.length == 0 ? this : this.mapInner(e, t, 0, 0, n || gs);
	}
	mapInner(t, n, r, i, a) {
		let o;
		for (let e = 0; e < this.local.length; e++) {
			let s = this.local[e].map(t, r, i);
			s && s.type.valid(n, s) ? (o || (o = [])).push(s) : a.onRemove && a.onRemove(this.local[e].spec);
		}
		return this.children.length ? ys(this.children, o || [], t, n, r, i, a) : o ? new e(o.sort(Ts), hs) : _s;
	}
	add(t, n) {
		return n.length ? this == _s ? e.create(t, n) : this.addInner(t, n, 0) : this;
	}
	addInner(t, n, r) {
		let i, a = 0;
		t.forEach((e, t) => {
			let o = t + r, s;
			if (s = Ss(n, e, o)) {
				for (i || (i = this.children.slice()); a < i.length && i[a] < t;) a += 3;
				i[a] == t ? i[a + 2] = i[a + 2].addInner(e, s, o + 1) : i.splice(a, 0, t, t + e.nodeSize, ws(s, e, o + 1, gs)), a += 3;
			}
		});
		let o = bs(a ? Cs(n) : n, -r);
		for (let e = 0; e < o.length; e++) o[e].type.valid(t, o[e]) || o.splice(e--, 1);
		return new e(o.length ? this.local.concat(o).sort(Ts) : this.local, i || this.children);
	}
	remove(e) {
		return e.length == 0 || this == _s ? this : this.removeInner(e, 0);
	}
	removeInner(t, n) {
		let r = this.children, i = this.local;
		for (let e = 0; e < r.length; e += 3) {
			let i, a = r[e] + n, o = r[e + 1] + n;
			for (let e = 0, n; e < t.length; e++) (n = t[e]) && n.from > a && n.to < o && (t[e] = null, (i || (i = [])).push(n));
			if (!i) continue;
			r == this.children && (r = this.children.slice());
			let s = r[e + 2].removeInner(i, a + 1);
			s == _s ? (r.splice(e, 3), e -= 3) : r[e + 2] = s;
		}
		if (i.length) {
			for (let e = 0, r; e < t.length; e++) if (r = t[e]) for (let e = 0; e < i.length; e++) i[e].eq(r, n) && (i == this.local && (i = this.local.slice()), i.splice(e--, 1));
		}
		return r == this.children && i == this.local ? this : i.length || r.length ? new e(i, r) : _s;
	}
	forChild(t, n) {
		if (this == _s) return this;
		if (n.isLeaf) return e.empty;
		let r, i;
		for (let e = 0; e < this.children.length; e += 3) if (this.children[e] >= t) {
			this.children[e] == t && (r = this.children[e + 2]);
			break;
		}
		let a = t + 1, o = a + n.content.size;
		for (let e = 0; e < this.local.length; e++) {
			let t = this.local[e];
			if (t.from < o && t.to > a && t.type instanceof ps) {
				let e = Math.max(a, t.from) - a, n = Math.min(o, t.to) - a;
				e < n && (i || (i = [])).push(t.copy(e, n));
			}
		}
		if (i) {
			let t = new e(i.sort(Ts), hs);
			return r ? new vs([t, r]) : t;
		}
		return r || _s;
	}
	eq(t) {
		if (this == t) return !0;
		if (!(t instanceof e) || this.local.length != t.local.length || this.children.length != t.children.length) return !1;
		for (let e = 0; e < this.local.length; e++) if (!this.local[e].eq(t.local[e])) return !1;
		for (let e = 0; e < this.children.length; e += 3) if (this.children[e] != t.children[e] || this.children[e + 1] != t.children[e + 1] || !this.children[e + 2].eq(t.children[e + 2])) return !1;
		return !0;
	}
	locals(e) {
		return Es(this.localsInner(e));
	}
	localsInner(e) {
		if (this == _s) return hs;
		if (e.inlineContent || !this.local.some(ps.is)) return this.local;
		let t = [];
		for (let e = 0; e < this.local.length; e++) this.local[e].type instanceof ps || t.push(this.local[e]);
		return t;
	}
	forEachSet(e) {
		e(this);
	}
};
j.empty = new j([], []), j.removeOverlap = Es;
var _s = j.empty, vs = class e {
	constructor(e) {
		this.members = e;
	}
	map(t, n) {
		let r = this.members.map((e) => e.map(t, n, gs));
		return e.from(r);
	}
	forChild(t, n) {
		if (n.isLeaf) return j.empty;
		let r = [];
		for (let i = 0; i < this.members.length; i++) {
			let a = this.members[i].forChild(t, n);
			a != _s && (a instanceof e ? r = r.concat(a.members) : r.push(a));
		}
		return e.from(r);
	}
	eq(t) {
		if (!(t instanceof e) || t.members.length != this.members.length) return !1;
		for (let e = 0; e < this.members.length; e++) if (!this.members[e].eq(t.members[e])) return !1;
		return !0;
	}
	locals(e) {
		let t, n = !0;
		for (let r = 0; r < this.members.length; r++) {
			let i = this.members[r].localsInner(e);
			if (i.length) {
				if (!t) t = i;
				else {
					n && (t = t.slice(), n = !1);
					for (let e = 0; e < i.length; e++) t.push(i[e]);
				}
			}
		}
		return t ? Es(n ? t : t.sort(Ts)) : hs;
	}
	static from(t) {
		switch (t.length) {
			case 0: return _s;
			case 1: return t[0];
			default: return new e(t.every((e) => e instanceof j) ? t : t.reduce((e, t) => e.concat(t instanceof j ? t : t.members), []));
		}
	}
	forEachSet(e) {
		for (let t = 0; t < this.members.length; t++) this.members[t].forEachSet(e);
	}
};
function ys(e, t, n, r, i, a, o) {
	let s = e.slice();
	for (let e = 0, t = a; e < n.maps.length; e++) {
		let r = 0;
		n.maps[e].forEach((e, n, i, a) => {
			let o = a - i - (n - e);
			for (let i = 0; i < s.length; i += 3) {
				let a = s[i + 1];
				if (a < 0 || e > a + t - r) continue;
				let c = s[i] + t - r;
				n >= c ? s[i + 1] = e <= c ? -2 : -1 : e >= t && o && (s[i] += o, s[i + 1] += o);
			}
			r += o;
		}), t = n.maps[e].map(t, -1);
	}
	let c = !1;
	for (let t = 0; t < s.length; t += 3) if (s[t + 1] < 0) {
		if (s[t + 1] == -2) {
			c = !0, s[t + 1] = -1;
			continue;
		}
		let l = n.map(e[t] + a), u = l - i;
		if (u < 0 || u >= r.content.size) {
			c = !0;
			continue;
		}
		let d = n.map(e[t + 1] + a, -1) - i, { index: f, offset: p } = r.content.findIndex(u), m = r.maybeChild(f);
		if (m && p == u && p + m.nodeSize == d) {
			let r = s[t + 2].mapInner(n, m, l + 1, e[t] + a + 1, o);
			r == _s ? (s[t + 1] = -2, c = !0) : (s[t] = u, s[t + 1] = d, s[t + 2] = r);
		} else c = !0;
	}
	if (c) {
		let c = ws(xs(s, e, t, n, i, a, o), r, 0, o);
		t = c.local;
		for (let e = 0; e < s.length; e += 3) s[e + 1] < 0 && (s.splice(e, 3), e -= 3);
		for (let e = 0, t = 0; e < c.children.length; e += 3) {
			let n = c.children[e];
			for (; t < s.length && s[t] < n;) t += 3;
			s.splice(t, 0, c.children[e], c.children[e + 1], c.children[e + 2]);
		}
	}
	return new j(t.sort(Ts), s);
}
function bs(e, t) {
	if (!t || !e.length) return e;
	let n = [];
	for (let r = 0; r < e.length; r++) {
		let i = e[r];
		n.push(new A(i.from + t, i.to + t, i.type));
	}
	return n;
}
function xs(e, t, n, r, i, a, o) {
	function s(e, t) {
		for (let a = 0; a < e.local.length; a++) {
			let s = e.local[a].map(r, i, t);
			s ? n.push(s) : o.onRemove && o.onRemove(e.local[a].spec);
		}
		for (let n = 0; n < e.children.length; n += 3) s(e.children[n + 2], e.children[n] + t + 1);
	}
	for (let n = 0; n < e.length; n += 3) e[n + 1] == -1 && s(e[n + 2], t[n] + a + 1);
	return n;
}
function Ss(e, t, n) {
	if (t.isLeaf) return null;
	let r = n + t.nodeSize, i = null;
	for (let t = 0, a; t < e.length; t++) (a = e[t]) && a.from > n && a.to < r && ((i || (i = [])).push(a), e[t] = null);
	return i;
}
function Cs(e) {
	let t = [];
	for (let n = 0; n < e.length; n++) e[n] != null && t.push(e[n]);
	return t;
}
function ws(e, t, n, r) {
	let i = [], a = !1;
	t.forEach((t, o) => {
		let s = Ss(e, t, o + n);
		if (s) {
			a = !0;
			let e = ws(s, t, n + o + 1, r);
			e != _s && i.push(o, o + t.nodeSize, e);
		}
	});
	let o = bs(a ? Cs(e) : e, -n).sort(Ts);
	for (let e = 0; e < o.length; e++) o[e].type.valid(t, o[e]) || (r.onRemove && r.onRemove(o[e].spec), o.splice(e--, 1));
	return o.length || i.length ? new j(o, i) : _s;
}
function Ts(e, t) {
	return e.from - t.from || e.to - t.to;
}
function Es(e) {
	let t = e;
	for (let n = 0; n < t.length - 1; n++) {
		let r = t[n];
		if (r.from != r.to) for (let i = n + 1; i < t.length; i++) {
			let a = t[i];
			if (a.from == r.from) {
				a.to != r.to && (t == e && (t = e.slice()), t[i] = a.copy(a.from, r.to), Ds(t, i + 1, a.copy(r.to, a.to)));
				continue;
			}
			a.from < r.to && (t == e && (t = e.slice()), t[n] = r.copy(r.from, a.from), Ds(t, i, r.copy(a.from, r.to)));
			break;
		}
	}
	return t;
}
function Ds(e, t, n) {
	for (; t < e.length && Ts(n, e[t]) > 0;) t++;
	e.splice(t, 0, n);
}
function Os(e) {
	let t = [];
	return e.someProp("decorations", (n) => {
		let r = n(e.state);
		r && r != _s && t.push(r);
	}), e.cursorWrapper && t.push(j.create(e.state.doc, [e.cursorWrapper.deco])), vs.from(t);
}
var ks = {
	childList: !0,
	characterData: !0,
	characterDataOldValue: !0,
	attributes: !0,
	attributeOldValue: !0,
	subtree: !0
}, As = ii && ai <= 11, js = class {
	constructor() {
		this.anchorNode = null, this.anchorOffset = 0, this.focusNode = null, this.focusOffset = 0;
	}
	set(e) {
		this.anchorNode = e.anchorNode, this.anchorOffset = e.anchorOffset, this.focusNode = e.focusNode, this.focusOffset = e.focusOffset;
	}
	clear() {
		this.anchorNode = this.focusNode = null;
	}
	eq(e) {
		return e.anchorNode == this.anchorNode && e.anchorOffset == this.anchorOffset && e.focusNode == this.focusNode && e.focusOffset == this.focusOffset;
	}
}, Ms = class {
	constructor(e, t) {
		this.view = e, this.handleDOMChange = t, this.queue = [], this.flushingSoon = -1, this.observer = null, this.currentSelection = new js(), this.onCharData = null, this.suppressingSelectionUpdates = !1, this.lastChangedTextNode = null, this.observer = window.MutationObserver && new window.MutationObserver((t) => {
			for (let e = 0; e < t.length; e++) this.queue.push(t[e]);
			ii && ai <= 11 && t.some((e) => e.type == "childList" && e.removedNodes.length || e.type == "characterData" && e.oldValue.length > e.target.nodeValue.length) ? this.flushSoon() : ui && e.composing && t.some((e) => e.type == "childList" && e.target.nodeName == "TR") ? (e.input.badSafariComposition = !0, this.flushSoon()) : this.flush();
		}), As && (this.onCharData = (e) => {
			this.queue.push({
				target: e.target,
				type: "characterData",
				oldValue: e.prevValue
			}), this.flushSoon();
		}), this.onSelectionChange = this.onSelectionChange.bind(this);
	}
	flushSoon() {
		this.flushingSoon < 0 && (this.flushingSoon = window.setTimeout(() => {
			this.flushingSoon = -1, this.flush();
		}, 20));
	}
	forceFlush() {
		this.flushingSoon > -1 && (window.clearTimeout(this.flushingSoon), this.flushingSoon = -1, this.flush());
	}
	start() {
		this.observer && (this.observer.takeRecords(), this.observer.observe(this.view.dom, ks)), this.onCharData && this.view.dom.addEventListener("DOMCharacterDataModified", this.onCharData), this.connectSelection();
	}
	stop() {
		if (this.observer) {
			let e = this.observer.takeRecords();
			if (e.length) {
				for (let t = 0; t < e.length; t++) this.queue.push(e[t]);
				window.setTimeout(() => this.flush(), 20);
			}
			this.observer.disconnect();
		}
		this.onCharData && this.view.dom.removeEventListener("DOMCharacterDataModified", this.onCharData), this.disconnectSelection();
	}
	connectSelection() {
		this.view.dom.ownerDocument.addEventListener("selectionchange", this.onSelectionChange);
	}
	disconnectSelection() {
		this.view.dom.ownerDocument.removeEventListener("selectionchange", this.onSelectionChange);
	}
	suppressSelectionUpdates() {
		this.suppressingSelectionUpdates = !0, setTimeout(() => this.suppressingSelectionUpdates = !1, 50);
	}
	onSelectionChange() {
		if (Ia(this.view)) {
			if (this.suppressingSelectionUpdates) return Ea(this.view);
			if (ii && ai <= 11 && !this.view.state.selection.empty) {
				let e = this.view.domSelectionRange();
				if (e.focusNode && zr(e.focusNode, e.focusOffset, e.anchorNode, e.anchorOffset)) return this.flushSoon();
			}
			this.flush();
		}
	}
	setCurSelection() {
		this.currentSelection.set(this.view.domSelectionRange());
	}
	ignoreSelectionChange(e) {
		if (!e.focusNode) return !0;
		let t = /* @__PURE__ */ new Set(), n;
		for (let n = e.focusNode; n; n = Fr(n)) t.add(n);
		for (let r = e.anchorNode; r; r = Fr(r)) if (t.has(r)) {
			n = r;
			break;
		}
		let r = n && this.view.docView.nearestDesc(n);
		if (r && r.ignoreMutation({
			type: "selection",
			target: k(n) ? n.parentNode : n
		})) return this.setCurSelection(), !0;
	}
	pendingRecords() {
		if (this.observer) for (let e of this.observer.takeRecords()) this.queue.push(e);
		return this.queue;
	}
	flush() {
		let { view: e } = this;
		if (!e.docView || this.flushingSoon > -1) return;
		let t = this.pendingRecords();
		t.length && (this.queue = []);
		let n = e.domSelectionRange(), r = !this.suppressingSelectionUpdates && !this.currentSelection.eq(n) && Ia(e) && !this.ignoreSelectionChange(n), i = -1, a = -1, o = !1, s = [];
		if (e.editable) for (let e = 0; e < t.length; e++) {
			let n = this.registerMutation(t[e], s);
			n && (i = i < 0 ? n.from : Math.min(n.from, i), a = a < 0 ? n.to : Math.max(n.to, a), n.typeOver && (o = !0));
		}
		if (s.some((e) => e.nodeName == "BR") && (e.input.lastKeyCode == 8 || e.input.lastKeyCode == 46 || ci && (e.composing || e.input.compositionEndedAt > Date.now() - 50) && t.some((e) => e.type == "childList" && e.removedNodes.length))) {
			for (let e of s) if (e.nodeName == "BR" && e.parentNode) {
				let t = e.nextSibling;
				for (; t && O(t);) {
					if (t.contentEditable == "false") {
						e.parentNode.removeChild(e);
						break;
					}
					t = t.firstChild;
				}
			}
		} else if (oi && s.length) {
			let t = s.filter((e) => e.nodeName == "BR");
			if (t.length == 2) {
				let [e, n] = t;
				e.parentNode && e.parentNode.parentNode == n.parentNode ? n.remove() : e.remove();
			} else {
				let { focusNode: n } = this.currentSelection;
				for (let r of t) {
					let t = r.parentNode;
					t && t.nodeName == "LI" && (!n || Rs(e, n) != t) && r.remove();
				}
			}
		}
		let c = null;
		i < 0 && r && e.input.lastFocus > Date.now() - 200 && Math.max(e.input.lastTouch, e.input.lastClick.time) < Date.now() - 300 && Jr(n) && (c = wa(e)) && c.eq(C.near(e.state.doc.resolve(0), 1)) ? (e.input.lastFocus = 0, Ea(e), this.currentSelection.set(n), e.scrollToSelection()) : (i > -1 || r) && (i > -1 && (e.docView.markDirty(i, a), Fs(e)), e.input.badSafariComposition && (e.input.badSafariComposition = !1, zs(e, s)), this.handleDOMChange(i, a, o, s), e.docView && e.docView.dirty ? e.updateState(e.state) : this.currentSelection.eq(n) || Ea(e), this.currentSelection.set(n));
	}
	registerMutation(e, t) {
		if (t.indexOf(e.target) > -1) return null;
		let n = this.view.docView.nearestDesc(e.target);
		if (e.type == "attributes" && (n == this.view.docView || e.attributeName == "contenteditable" || e.attributeName == "style" && !e.oldValue && !e.target.getAttribute("style")) || !n || n.ignoreMutation(e)) return null;
		if (e.type == "childList") {
			for (let n = 0; n < e.addedNodes.length; n++) {
				let r = e.addedNodes[n];
				t.push(r), k(r) && (this.lastChangedTextNode = r);
			}
			if (n.contentDOM && n.contentDOM != n.dom && !n.contentDOM.contains(e.target)) return {
				from: n.posBefore,
				to: n.posAfter
			};
			let r = e.previousSibling, i = e.nextSibling;
			if (ii && ai <= 11 && e.addedNodes.length) for (let t = 0; t < e.addedNodes.length; t++) {
				let { previousSibling: n, nextSibling: a } = e.addedNodes[t];
				(!n || Array.prototype.indexOf.call(e.addedNodes, n) < 0) && (r = n), (!a || Array.prototype.indexOf.call(e.addedNodes, a) < 0) && (i = a);
			}
			let a = r && r.parentNode == e.target ? Pr(r) + 1 : 0, o = n.localPosFromDOM(e.target, a, -1), s = i && i.parentNode == e.target ? Pr(i) : e.target.childNodes.length;
			return {
				from: o,
				to: n.localPosFromDOM(e.target, s, 1)
			};
		}
		return e.type == "attributes" ? {
			from: n.posAtStart - n.border,
			to: n.posAtEnd + n.border
		} : (this.lastChangedTextNode = e.target, {
			from: n.posAtStart,
			to: n.posAtEnd,
			typeOver: e.target.nodeValue == e.oldValue
		});
	}
}, Ns = /* @__PURE__ */ new WeakMap(), Ps = !1;
function Fs(e) {
	if (!Ns.has(e) && (Ns.set(e, null), [
		"normal",
		"nowrap",
		"pre-line"
	].indexOf(getComputedStyle(e.dom).whiteSpace) !== -1)) {
		if (e.requiresGeckoHackNode = oi, Ps) return;
		console.warn("ProseMirror expects the CSS white-space property to be set, preferably to 'pre-wrap'. It is recommended to load style/prosemirror.css from the prosemirror-view package."), Ps = !0;
	}
}
function Is(e, t) {
	let n = t.startContainer, r = t.startOffset, i = t.endContainer, a = t.endOffset, o = e.domAtPos(e.state.selection.anchor);
	return zr(o.node, o.offset, i, a) && ([n, r, i, a] = [
		i,
		a,
		n,
		r
	]), {
		anchorNode: n,
		anchorOffset: r,
		focusNode: i,
		focusOffset: a
	};
}
function Ls(e, t) {
	if (t.getComposedRanges) {
		let n = t.getComposedRanges(e.root)[0];
		if (n) return Is(e, n);
	}
	let n;
	function r(e) {
		e.preventDefault(), e.stopImmediatePropagation(), n = e.getTargetRanges()[0];
	}
	return e.dom.addEventListener("beforeinput", r, !0), document.execCommand("indent"), e.dom.removeEventListener("beforeinput", r, !0), n ? Is(e, n) : null;
}
function Rs(e, t) {
	for (let n = t.parentNode; n && n != e.dom; n = n.parentNode) {
		let t = e.docView.nearestDesc(n, !0);
		if (t && t.node.isBlock) return n;
	}
	return null;
}
function zs(e, t) {
	let { focusNode: n, focusOffset: r } = e.domSelectionRange();
	for (let i of t) if (i.parentNode?.nodeName == "TR") {
		let t = i.nextSibling;
		for (; t && t.nodeName != "TD" && t.nodeName != "TH";) t = t.nextSibling;
		if (t) {
			let a = t;
			for (;;) {
				let e = a.firstChild;
				if (!e || !O(e) || e.contentEditable == "false" || /^(BR|IMG)$/.test(e.nodeName)) break;
				a = e;
			}
			a.insertBefore(i, a.firstChild), n == i && e.domSelection().collapse(i, r);
		} else i.parentNode.removeChild(i);
	}
}
function Bs(e, t, n, r) {
	let { node: i, fromOffset: a, toOffset: o, from: s, to: c } = e.docView.parseRange(t, n), l = e.domSelectionRange(), u, d = l.anchorNode;
	if (d && Ur(e.dom, d) && (u = [{
		node: d,
		offset: l.anchorOffset
	}], Jr(l) || u.push({
		node: l.focusNode,
		offset: l.focusOffset
	})), ci && e.input.lastKeyCode === 8) for (let e = o; e > a; e--) {
		let t = i.childNodes[e - 1], n = t.pmViewDesc;
		if (t.nodeName == "BR" && !n) {
			o = e;
			break;
		}
		if (!n || n.size) break;
	}
	let f = e.state.doc, p = e.someProp("domParser") || Be.fromSchema(e.state.schema), m = f.resolve(s), h = null, g = p.parse(i, {
		topNode: m.parent,
		topMatch: m.parent.contentMatchAt(m.index()),
		topOpen: !0,
		from: a,
		to: o,
		preserveWhitespace: m.parent.type.whitespace != "pre" || "full",
		findPositions: u,
		ruleFromNode: Vs(r),
		context: m
	});
	if (u && u[0].pos != null) {
		let e = u[0].pos, t = u[1] && u[1].pos;
		t ?? (t = e), h = {
			anchor: e + s,
			head: t + s
		};
	}
	return {
		doc: g,
		sel: h,
		from: s,
		to: c
	};
}
var Vs = (e) => (t) => {
	let n = t.pmViewDesc;
	if (n) return n.parseRule(e);
	if (t.nodeName == "BR" && t.parentNode) {
		if (ui && /^(ul|ol)$/i.test(t.parentNode.nodeName)) {
			let e = document.createElement("div");
			return e.appendChild(document.createElement("li")), { skip: e };
		}
		if (t.parentNode.lastChild == t || ui && /^(tr|table)$/i.test(t.parentNode.nodeName)) return { ignore: !0 };
	} else if (t.nodeName == "IMG" && t.getAttribute("mark-placeholder")) return { ignore: !0 };
	return null;
}, Hs = /^(a|abbr|acronym|b|bd[io]|big|br|button|cite|code|data(list)?|del|dfn|em|i|img|ins|kbd|label|map|mark|meter|output|q|ruby|s|samp|small|span|strong|su[bp]|time|u|tt|var)$/i;
function Us(e, t, n, r, i) {
	let a = e.input.compositionPendingChanges || (e.composing ? e.input.compositionID : 0);
	if (e.input.compositionPendingChanges = 0, t < 0) {
		let t = e.input.lastSelectionTime > Date.now() - 50 ? e.input.lastSelectionOrigin : null, n = wa(e, t);
		if (n && !e.state.selection.eq(n)) {
			if (ci && mi && e.input.lastKeyCode === 13 && Date.now() - 100 < e.input.lastKeyCodeTime && e.someProp("handleKeyDown", (t) => t(e, Yr(13, "Enter")))) return;
			let r = e.state.tr.setSelection(n);
			t == "pointer" ? r.setMeta("pointer", !0) : t == "key" && r.scrollIntoView(), a && r.setMeta("composition", a), e.dispatch(r);
		}
		return;
	}
	let o = e.state.doc.resolve(t), s = o.sharedDepth(n);
	t = o.before(s + 1), n = e.state.doc.resolve(n).after(s + 1);
	let c = e.state.selection, l = Bs(e, t, n, i), u = e.state.doc, d = u.slice(l.from, l.to), f, p;
	e.input.lastKeyCode === 8 && Date.now() - 100 < e.input.lastKeyCodeTime ? (f = e.state.selection.to, p = "end") : (f = e.state.selection.from, p = "start"), e.input.lastKeyCode = null;
	let m = Ys(d.content, l.doc.content, l.from, f, p);
	if (m && e.input.domChangeCount++, (di && e.input.lastIOSEnter > Date.now() - 225 || mi) && i.some((e) => O(e) && !Hs.test(e.nodeName)) && (!m || m.endA >= m.endB) && e.someProp("handleKeyDown", (t) => t(e, Yr(13, "Enter")))) {
		e.input.lastIOSEnter = 0;
		return;
	}
	if (!m) {
		if (r && c instanceof w && !c.empty && c.$head.sameParent(c.$anchor) && !e.composing && !(l.sel && l.sel.anchor != l.sel.head)) m = {
			start: c.from,
			endA: c.to,
			endB: c.to
		};
		else {
			if (l.sel) {
				let t = Ws(e, e.state.doc, l.sel);
				if (t && !t.eq(e.state.selection)) {
					let n = e.state.tr.setSelection(t);
					a && n.setMeta("composition", a), e.dispatch(n);
				}
			}
			return;
		}
	}
	e.state.selection.from < e.state.selection.to && m.start == m.endB && e.state.selection instanceof w && (m.start > e.state.selection.from && m.start <= e.state.selection.from + 2 && e.state.selection.from >= l.from ? m.start = e.state.selection.from : m.endA < e.state.selection.to && m.endA >= e.state.selection.to - 2 && e.state.selection.to <= l.to && (m.endB += e.state.selection.to - m.endA, m.endA = e.state.selection.to)), ii && ai <= 11 && m.endB == m.start + 1 && m.endA == m.start && m.start > l.from && l.doc.textBetween(m.start - l.from - 1, m.start - l.from + 1) == " \xA0" && (m.start--, m.endA--, m.endB--);
	let h = l.doc.resolveNoCache(m.start - l.from), g = l.doc.resolveNoCache(m.endB - l.from), _ = u.resolve(m.start), ee = h.sameParent(g) && h.parent.inlineContent && _.end() >= m.endA;
	if ((di && e.input.lastIOSEnter > Date.now() - 225 && (!ee || i.some((e) => e.nodeName == "DIV" || e.nodeName == "P")) || !ee && qs(l.doc, h.pos, g.pos)) && e.someProp("handleKeyDown", (t) => t(e, Yr(13, "Enter")))) {
		e.input.lastIOSEnter = 0;
		return;
	}
	if (e.state.selection.anchor > m.start && Ks(u, m.start, m.endA, h, g) && e.someProp("handleKeyDown", (t) => t(e, Yr(8, "Backspace")))) {
		mi && ci && e.domObserver.suppressSelectionUpdates();
		return;
	}
	ci && m.endB == m.start && (e.input.lastChromeDelete = Date.now()), mi && !ee && h.start() != g.start() && g.parentOffset == 0 && h.depth == g.depth && l.sel && l.sel.anchor == l.sel.head && l.sel.head == m.endA && (m.endB -= 2, g = l.doc.resolveNoCache(m.endB - l.from), setTimeout(() => {
		e.someProp("handleKeyDown", function(t) {
			return t(e, Yr(13, "Enter"));
		});
	}, 20));
	let v = m.start, y = m.endA, b = (t) => {
		let n = t || e.state.tr.replace(v, y, l.doc.slice(m.start - l.from, m.endB - l.from));
		if (l.sel) {
			let t = Ws(e, n.doc, l.sel);
			t && !(ci && e.composing && t.empty && (m.start != m.endB || e.input.lastChromeDelete < Date.now() - 100) && (t.head == v || t.head == n.mapping.map(y) - 1) || ii && t.empty && t.head == v) && n.setSelection(t);
		}
		return a && n.setMeta("composition", a), n.scrollIntoView();
	}, x;
	if (ee) {
		if (h.pos == g.pos) {
			ii && ai <= 11 && h.parentOffset == 0 && (e.domObserver.suppressSelectionUpdates(), setTimeout(() => Ea(e), 20));
			let t = b(e.state.tr.delete(v, y)), n = u.resolve(m.start).marksAcross(u.resolve(m.endA));
			n && t.ensureMarks(n), e.dispatch(t);
		} else if (m.endA == m.endB && (x = Gs(h.parent.content.cut(h.parentOffset, g.parentOffset), _.parent.content.cut(_.parentOffset, m.endA - _.start())))) {
			let t = b(e.state.tr);
			x.type == "add" ? t.addMark(v, y, x.mark) : t.removeMark(v, y, x.mark), e.dispatch(t);
		} else if (h.parent.child(h.index()).isText && h.index() == g.index() - +!g.textOffset) {
			let t = h.parent.textBetween(h.parentOffset, g.parentOffset), n = () => b(e.state.tr.insertText(t, v, y));
			e.someProp("handleTextInput", (r) => r(e, v, y, t, n)) || e.dispatch(n());
		} else e.dispatch(b());
	} else e.dispatch(b());
}
function Ws(e, t, n) {
	return Math.max(n.anchor, n.head) > t.content.size ? null : Fa(e, t.resolve(n.anchor), t.resolve(n.head));
}
function Gs(e, t) {
	let n = e.firstChild.marks, r = t.firstChild.marks, i = n, a = r, o, s, c;
	for (let e = 0; e < r.length; e++) i = r[e].removeFromSet(i);
	for (let e = 0; e < n.length; e++) a = n[e].removeFromSet(a);
	if (i.length == 1 && a.length == 0) s = i[0], o = "add", c = (e) => e.mark(s.addToSet(e.marks));
	else if (i.length == 0 && a.length == 1) s = a[0], o = "remove", c = (e) => e.mark(s.removeFromSet(e.marks));
	else return null;
	let u = [];
	for (let e = 0; e < t.childCount; e++) u.push(c(t.child(e)));
	if (l.from(u).eq(e)) return {
		mark: s,
		type: o
	};
}
function Ks(e, t, n, r, i) {
	if (n - t <= i.pos - r.pos || Js(r, !0, !1) < i.pos) return !1;
	let a = e.resolve(t);
	if (!r.parent.isTextblock) {
		let e = a.nodeAfter;
		return e != null && n == t + e.nodeSize;
	}
	if (a.parentOffset < a.parent.content.size || !a.parent.isTextblock) return !1;
	let o = e.resolve(Js(a, !0, !0));
	return !o.parent.isTextblock || o.pos > n || Js(o, !0, !1) < n ? !1 : r.parent.content.cut(r.parentOffset).eq(o.parent.content);
}
function qs(e, t, n) {
	let r = 0, i = 0;
	return e.nodesBetween(t, n, (e, a) => {
		e.isTextblock && a >= t && a < n ? r++ : (e.isText ? /\S/.test(e.text.slice(Math.max(0, t - a), Math.min(e.nodeSize, n - a))) : e.isLeaf) && i++;
	}), i == 0 && r == 1;
}
function Js(e, t, n) {
	let r = e.depth, i = t ? e.end() : e.pos;
	for (; r > 0 && (t || e.indexAfter(r) == e.node(r).childCount);) r--, i++, t = !1;
	if (n) {
		let t = e.node(r).maybeChild(e.indexAfter(r));
		for (; t && !t.isLeaf;) t = t.firstChild, i++;
	}
	return i;
}
function Ys(e, t, n, r, i) {
	let a = e.findDiffStart(t, n), o = n + e.size, s = n + t.size;
	if (a == null) return null;
	let { a: c, b: l } = e.findDiffEnd(t, o, s);
	if (i == "end") {
		let e = Math.max(0, a - Math.min(c, l));
		r -= c + e - a;
	}
	if (c < a && o < s) {
		let e = r <= a && r >= c ? a - r : 0;
		a -= e, l = a + (l - c), c = a;
	} else if (l < a) {
		let e = r <= a && r >= l ? a - r : 0;
		a -= e, c = a + (c - l), l = a;
	}
	return {
		start: a,
		endA: c,
		endB: l
	};
}
var Xs = class {
	constructor(e, t) {
		this._root = null, this.focused = !1, this.trackWrites = null, this.mounted = !1, this.markCursor = null, this.cursorWrapper = null, this.lastSelectedViewDesc = void 0, this.input = new Co(), this.prevDirectPlugins = [], this.pluginViews = [], this.requiresGeckoHackNode = !1, this.dragging = null, this._props = t, this.state = t.state, this.directPlugins = t.plugins || [], this.directPlugins.forEach(rc), this.dispatch = this.dispatch.bind(this), this.dom = e && e.mount || document.createElement("div"), e && (e.appendChild ? e.appendChild(this.dom) : typeof e == "function" ? e(this.dom) : e.mount && (this.mounted = !0)), this.editable = $s(this), Qs(this), this.nodeViews = tc(this), this.docView = ia(this.state.doc, Zs(this), Os(this), this.dom, this), this.domObserver = new Ms(this, (e, t, n, r) => Us(this, e, t, n, r)), this.domObserver.start(), wo(this), this.updatePluginViews();
	}
	get composing() {
		return this.input.composing;
	}
	get props() {
		if (this._props.state != this.state) {
			let e = this._props;
			this._props = {};
			for (let t in e) this._props[t] = e[t];
			this._props.state = this.state;
		}
		return this._props;
	}
	update(e) {
		e.handleDOMEvents != this._props.handleDOMEvents && Do(this);
		let t = this._props;
		this._props = e, e.plugins && (e.plugins.forEach(rc), this.directPlugins = e.plugins), this.updateStateInner(e.state, t);
	}
	setProps(e) {
		let t = {};
		for (let e in this._props) t[e] = this._props[e];
		t.state = this.state;
		for (let n in e) t[n] = e[n];
		this.update(t);
	}
	updateState(e) {
		this.updateStateInner(e, this._props);
	}
	updateStateInner(e, t) {
		let n = this.state, r = !1, i = !1;
		e.storedMarks && this.composing && (Zo(this), i = !0), this.state = e;
		let a = n.plugins != e.plugins || this._props.plugins != t.plugins;
		if (a || this._props.plugins != t.plugins || this._props.nodeViews != t.nodeViews) {
			let e = tc(this);
			nc(e, this.nodeViews) && (this.nodeViews = e, r = !0);
		}
		(a || t.handleDOMEvents != this._props.handleDOMEvents) && Do(this), this.editable = $s(this), Qs(this);
		let o = Os(this), s = Zs(this), c = n.plugins != e.plugins && !n.doc.eq(e.doc) ? "reset" : e.scrollToSelection > n.scrollToSelection ? "to selection" : "preserve", l = r || !this.docView.matchesNode(e.doc, s, o);
		(l || !e.selection.eq(n.selection)) && (i = !0);
		let u = c == "preserve" && i && this.dom.style.overflowAnchor == null && xi(this);
		if (i) {
			this.domObserver.stop();
			let t = l && (ii || ci) && !this.composing && !n.selection.empty && !e.selection.empty && ec(n.selection, e.selection);
			if (l) {
				let n = ci ? this.trackWrites = this.domSelectionRange().focusNode : null;
				this.composing && (this.input.compositionNode = Qo(this)), (r || !this.docView.update(e.doc, s, o, this)) && (this.docView.updateOuterDeco(s), this.docView.destroy(), this.docView = ia(e.doc, s, o, this.dom, this)), n && (!this.trackWrites || !this.dom.contains(this.trackWrites)) && (t = !0);
			}
			let i = this.input.mouseDown;
			t || !(i && this.domObserver.currentSelection.eq(this.domSelectionRange()) && Ra(this) && i.delaySelUpdate()) ? Ea(this, t) : (Na(this, e.selection), this.domObserver.setCurSelection()), this.domObserver.start();
		}
		this.updatePluginViews(n), this.dragging?.node && !n.doc.eq(e.doc) && this.updateDraggedNode(this.dragging, n), c == "reset" ? this.dom.scrollTop = 0 : c == "to selection" ? this.scrollToSelection() : u && Ci(u);
	}
	scrollToSelection() {
		let e = this.domSelectionRange().focusNode;
		if (e && Ur(this.dom, e) && !this.someProp("handleScrollToSelection", (e) => e(this))) {
			if (this.state.selection instanceof T) {
				let t = this.docView.domAfterPos(this.state.selection.from);
				O(t) && bi(this, t.getBoundingClientRect(), e);
			} else bi(this, this.coordsAtPos(this.state.selection.head, 1), e);
		}
	}
	destroyPluginViews() {
		let e;
		for (; e = this.pluginViews.pop();) e.destroy && e.destroy();
	}
	updatePluginViews(e) {
		if (!e || e.plugins != this.state.plugins || this.directPlugins != this.prevDirectPlugins) {
			this.prevDirectPlugins = this.directPlugins, this.destroyPluginViews();
			for (let e = 0; e < this.directPlugins.length; e++) {
				let t = this.directPlugins[e];
				t.spec.view && this.pluginViews.push(t.spec.view(this));
			}
			for (let e = 0; e < this.state.plugins.length; e++) {
				let t = this.state.plugins[e];
				t.spec.view && this.pluginViews.push(t.spec.view(this));
			}
		} else for (let t = 0; t < this.pluginViews.length; t++) {
			let n = this.pluginViews[t];
			n.update && n.update(this, e);
		}
	}
	updateDraggedNode(e, t) {
		let n = e.node, r = -1;
		if (n.from < this.state.doc.content.size && this.state.doc.nodeAt(n.from) == n.node) r = n.from;
		else {
			let e = n.from + (this.state.doc.content.size - t.doc.content.size);
			(e > 0 && e < this.state.doc.content.size && this.state.doc.nodeAt(e)) == n.node && (r = e);
		}
		this.dragging = new ss(e.slice, e.move, r < 0 ? void 0 : T.create(this.state.doc, r));
	}
	someProp(e, t) {
		let n = this._props && this._props[e], r;
		if (n != null && (r = t ? t(n) : n)) return r;
		for (let n = 0; n < this.directPlugins.length; n++) {
			let i = this.directPlugins[n].props[e];
			if (i != null && (r = t ? t(i) : i)) return r;
		}
		let i = this.state.plugins;
		if (i) for (let n = 0; n < i.length; n++) {
			let a = i[n].props[e];
			if (a != null && (r = t ? t(a) : a)) return r;
		}
	}
	hasFocus() {
		if (ii) {
			let e = this.root.activeElement;
			if (e == this.dom) return !0;
			if (!e || !this.dom.contains(e)) return !1;
			for (; e && this.dom != e && this.dom.contains(e);) {
				if (e.contentEditable == "false") return !1;
				e = e.parentElement;
			}
			return !0;
		}
		return this.root.activeElement == this.dom;
	}
	focus() {
		this.domObserver.stop(), this.editable && Ei(this.dom), Ea(this), this.domObserver.start();
	}
	get root() {
		let e = this._root;
		if (e == null) {
			for (let e = this.dom.parentNode; e; e = e.parentNode) if (e.nodeType == 9 || e.nodeType == 11 && e.host) return e.getSelection || (Object.getPrototypeOf(e).getSelection = () => e.ownerDocument.getSelection()), this._root = e;
		}
		return e || document;
	}
	updateRoot() {
		this._root = null;
	}
	posAtCoords(e) {
		return Pi(this, e);
	}
	coordsAtPos(e, t = 1) {
		return Ri(this, e, t);
	}
	domAtPos(e, t = 0) {
		return this.docView.domFromPos(e, t);
	}
	nodeDOM(e) {
		let t = this.docView.descAt(e);
		return t ? t.nodeDOM : null;
	}
	posAtDOM(e, t, n = -1) {
		let r = this.docView.posFromDOM(e, t, n);
		if (r == null) throw RangeError("DOM position not inside the editor");
		return r;
	}
	endOfTextblock(e, t) {
		return Ji(this, t || this.state, e);
	}
	pasteHTML(e, t) {
		return is(this, "", e, !1, t || new ClipboardEvent("paste"));
	}
	pasteText(e, t) {
		return is(this, e, null, !0, t || new ClipboardEvent("paste"));
	}
	serializeForClipboard(e) {
		return ro(this, e);
	}
	destroy() {
		this.docView && (Eo(this), this.destroyPluginViews(), this.mounted ? (this.docView.update(this.state.doc, [], Os(this), this), this.dom.textContent = "") : this.dom.parentNode && this.dom.parentNode.removeChild(this.dom), this.docView.destroy(), this.docView = null, Rr());
	}
	get isDestroyed() {
		return this.docView == null;
	}
	dispatchEvent(e) {
		return Ao(this, e);
	}
	domSelectionRange() {
		let e = this.dom.isConnected !== !1 && this.domSelection();
		return e ? ui && this.root.nodeType === 11 && Xr(this.dom.ownerDocument) == this.dom && Ls(this, e) || e : {
			focusNode: null,
			focusOffset: 0,
			anchorNode: null,
			anchorOffset: 0
		};
	}
	domSelection() {
		return this.root.getSelection();
	}
};
Xs.prototype.dispatch = function(e) {
	let t = this._props.dispatchTransaction;
	t ? t.call(this, e) : this.updateState(this.state.apply(e));
};
function Zs(e) {
	let t = Object.create(null);
	return t.class = "ProseMirror", t.contenteditable = String(e.editable), e.someProp("attributes", (n) => {
		if (typeof n == "function" && (n = n(e.state)), n) for (let e in n) e == "class" ? t.class += " " + n[e] : e == "style" ? t.style = (t.style ? t.style + ";" : "") + n[e] : !t[e] && e != "contenteditable" && e != "nodeName" && (t[e] = String(n[e]));
	}), t.translate || (t.translate = "no"), [A.node(0, e.state.doc.content.size, t)];
}
function Qs(e) {
	if (e.markCursor) {
		let t = document.createElement("img");
		t.className = "ProseMirror-separator", t.setAttribute("mark-placeholder", "true"), t.setAttribute("alt", ""), e.cursorWrapper = {
			dom: t,
			deco: A.widget(e.state.selection.from, t, {
				raw: !0,
				marks: e.markCursor
			})
		};
	} else e.cursorWrapper = null;
}
function $s(e) {
	return !e.someProp("editable", (t) => t(e.state) === !1);
}
function ec(e, t) {
	let n = Math.min(e.$anchor.sharedDepth(e.head), t.$anchor.sharedDepth(t.head));
	return e.$anchor.start(n) != t.$anchor.start(n);
}
function tc(e) {
	let t = Object.create(null);
	function n(e) {
		for (let n in e) Object.prototype.hasOwnProperty.call(t, n) || (t[n] = e[n]);
	}
	return e.someProp("nodeViews", n), e.someProp("markViews", n), t;
}
function nc(e, t) {
	let n = 0, r = 0;
	for (let r in e) {
		if (e[r] != t[r]) return !0;
		n++;
	}
	for (let e in t) r++;
	return n != r;
}
function rc(e) {
	if (e.spec.state || e.spec.filterTransaction || e.spec.appendTransaction) throw RangeError("Plugins passed directly to the view must not have a state component");
}
for (var ic = {
	8: "Backspace",
	9: "Tab",
	10: "Enter",
	12: "NumLock",
	13: "Enter",
	16: "Shift",
	17: "Control",
	18: "Alt",
	20: "CapsLock",
	27: "Escape",
	32: " ",
	33: "PageUp",
	34: "PageDown",
	35: "End",
	36: "Home",
	37: "ArrowLeft",
	38: "ArrowUp",
	39: "ArrowRight",
	40: "ArrowDown",
	44: "PrintScreen",
	45: "Insert",
	46: "Delete",
	59: ";",
	61: "=",
	91: "Meta",
	92: "Meta",
	106: "*",
	107: "+",
	108: ",",
	109: "-",
	110: ".",
	111: "/",
	144: "NumLock",
	145: "ScrollLock",
	160: "Shift",
	161: "Shift",
	162: "Control",
	163: "Control",
	164: "Alt",
	165: "Alt",
	173: "-",
	186: ";",
	187: "=",
	188: ",",
	189: "-",
	190: ".",
	191: "/",
	192: "`",
	219: "[",
	220: "\\",
	221: "]",
	222: "'"
}, ac = {
	48: ")",
	49: "!",
	50: "@",
	51: "#",
	52: "$",
	53: "%",
	54: "^",
	55: "&",
	56: "*",
	57: "(",
	59: ":",
	61: "+",
	173: "_",
	186: ":",
	187: "+",
	188: "<",
	189: "_",
	190: ">",
	191: "?",
	192: "~",
	219: "{",
	220: "|",
	221: "}",
	222: "\""
}, oc = typeof navigator < "u" && /Mac/.test(navigator.platform), sc = typeof navigator < "u" && /MSIE \d|Trident\/(?:[7-9]|\d{2,})\..*rv:(\d+)/.exec(navigator.userAgent), cc = 0; cc < 10; cc++) ic[48 + cc] = ic[96 + cc] = String(cc);
for (var cc = 1; cc <= 24; cc++) ic[cc + 111] = "F" + cc;
for (var cc = 65; cc <= 90; cc++) ic[cc] = String.fromCharCode(cc + 32), ac[cc] = String.fromCharCode(cc);
for (var lc in ic) ac.hasOwnProperty(lc) || (ac[lc] = ic[lc]);
function uc(e) {
	var t = !(oc && e.metaKey && e.shiftKey && !e.ctrlKey && !e.altKey || sc && e.shiftKey && e.key && e.key.length == 1 || e.key == "Unidentified") && e.key || (e.shiftKey ? ac : ic)[e.keyCode] || e.key || "Unidentified";
	return t == "Esc" && (t = "Escape"), t == "Del" && (t = "Delete"), t == "Left" && (t = "ArrowLeft"), t == "Up" && (t = "ArrowUp"), t == "Right" && (t = "ArrowRight"), t == "Down" && (t = "ArrowDown"), t;
}
//#endregion
//#region node_modules/prosemirror-keymap/dist/index.js
var dc = typeof navigator < "u" && /Mac|iP(hone|[oa]d)/.test(navigator.platform), fc = typeof navigator < "u" && /Win/.test(navigator.platform);
function pc(e) {
	let t = e.split(/-(?!$)/), n = t[t.length - 1];
	n == "Space" && (n = " ");
	let r, i, a, o;
	for (let e = 0; e < t.length - 1; e++) {
		let n = t[e];
		if (/^(cmd|meta|m)$/i.test(n)) o = !0;
		else if (/^a(lt)?$/i.test(n)) r = !0;
		else if (/^(c|ctrl|control)$/i.test(n)) i = !0;
		else if (/^s(hift)?$/i.test(n)) a = !0;
		else if (/^mod$/i.test(n)) dc ? o = !0 : i = !0;
		else throw Error("Unrecognized modifier name: " + n);
	}
	return r && (n = "Alt-" + n), i && (n = "Ctrl-" + n), o && (n = "Meta-" + n), a && (n = "Shift-" + n), n;
}
function mc(e) {
	let t = Object.create(null);
	for (let n in e) t[pc(n)] = e[n];
	return t;
}
function hc(e, t, n = !0) {
	return t.altKey && (e = "Alt-" + e), t.ctrlKey && (e = "Ctrl-" + e), t.metaKey && (e = "Meta-" + e), n && t.shiftKey && (e = "Shift-" + e), e;
}
function gc(e) {
	return new E({ props: { handleKeyDown: _c(e) } });
}
function _c(e) {
	let t = mc(e);
	return function(e, n) {
		let r = uc(n), i, a = t[hc(r, n)];
		if (a && a(e.state, e.dispatch, e)) return !0;
		if (r.length == 1 && r != " ") {
			if (n.shiftKey) {
				let i = t[hc(r, n, !1)];
				if (i && i(e.state, e.dispatch, e)) return !0;
			}
			if ((n.altKey || n.metaKey || n.ctrlKey) && !(fc && n.ctrlKey && n.altKey) && (i = ic[n.keyCode]) && i != r) {
				let r = t[hc(i, n)];
				if (r && r(e.state, e.dispatch, e)) return !0;
			}
		}
		return !1;
	};
}
//#endregion
//#region node_modules/@tiptap/core/dist/index.js
function vc(e) {
	let { state: t, transaction: n } = e, { selection: r } = n, { doc: i } = n, { storedMarks: a } = n;
	return {
		...t,
		apply: t.apply.bind(t),
		applyTransaction: t.applyTransaction.bind(t),
		plugins: t.plugins,
		schema: t.schema,
		reconfigure: t.reconfigure.bind(t),
		toJSON: t.toJSON.bind(t),
		get storedMarks() {
			return a;
		},
		get selection() {
			return r;
		},
		get doc() {
			return i;
		},
		get tr() {
			return r = n.selection, i = n.doc, a = n.storedMarks, n;
		}
	};
}
var yc = class e {
	constructor(e) {
		this.editor = e.editor, this.rawCommands = this.editor.extensionManager.commands, this.customState = e.state;
	}
	get hasCustomState() {
		return !!this.customState;
	}
	get state() {
		return this.customState || this.editor.state;
	}
	get commands() {
		let { rawCommands: e, editor: t, state: n } = this, { view: r } = t, { tr: i } = n, a = this.buildProps(i);
		return Object.fromEntries(Object.entries(e).map(([e, t]) => [e, (...e) => {
			let n = t(...e)(a);
			return !i.getMeta("preventDispatch") && !this.hasCustomState && r.dispatch(i), n;
		}]));
	}
	get chain() {
		return () => this.createChain();
	}
	get can() {
		return () => this.createCan();
	}
	createChain(e, t = !0) {
		let { rawCommands: n, editor: r, state: i } = this, { view: a } = r, o = [], s = !!e, c = e || i.tr, l = () => (!s && t && !c.getMeta("preventDispatch") && !this.hasCustomState && a.dispatch(c), o.every((e) => e === !0)), u = {
			...Object.fromEntries(Object.entries(n).map(([e, n]) => [e, (...e) => {
				let r = this.buildProps(c, t), i = n(...e)(r);
				return o.push(i), u;
			}])),
			run: l
		};
		return u;
	}
	static createFakeChain() {
		let e = new Proxy({}, { get: (t, n) => {
			if (n !== "then") return n === "run" ? () => !1 : () => e;
		} });
		return e;
	}
	createCan(e) {
		let { rawCommands: t, state: n } = this, r = e || n.tr, i = this.buildProps(r, !1);
		return {
			...Object.fromEntries(Object.entries(t).map(([e, t]) => [e, (...e) => t(...e)({
				...i,
				dispatch: void 0
			})])),
			chain: () => this.createChain(r, !1)
		};
	}
	static createFallbackCan() {
		let t = e.createFakeChain();
		return new Proxy({ chain: () => t }, { get: (e, t) => {
			if (t !== "then") return t === "chain" ? e.chain : () => !1;
		} });
	}
	buildProps(e, t = !0) {
		let { rawCommands: n, editor: r, state: i } = this, { view: a } = r, o = {
			tr: e,
			editor: r,
			view: a,
			state: vc({
				state: i,
				transaction: e
			}),
			dispatch: t ? () => void 0 : void 0,
			chain: () => this.createChain(e, t),
			can: () => this.createCan(e),
			get commands() {
				return Object.fromEntries(Object.entries(n).map(([e, t]) => [e, (...e) => t(...e)(o)]));
			}
		};
		return o;
	}
}, bc = () => ({ editor: e, view: t }) => (requestAnimationFrame(() => {
	if (!e.isDestroyed) {
		var n;
		t.dom.blur(), (n = window) == null || (n = n.getSelection()) == null || n.removeAllRanges();
	}
}), !0), xc = (e = !0) => ({ commands: t }) => t.setContent("", { emitUpdate: e }), Sc = () => ({ state: e, tr: t, dispatch: n }) => {
	let { selection: r } = t, { ranges: i } = r;
	return n && i.forEach(({ $from: n, $to: r }) => {
		e.doc.nodesBetween(n.pos, r.pos, (e, n) => {
			if (e.type.isText) return;
			let { doc: r, mapping: i } = t, a = r.resolve(i.map(n)), o = r.resolve(i.map(n + e.nodeSize)), s = a.blockRange(o);
			if (!s) return;
			let c = Pt(s);
			if (e.type.isTextblock) {
				let { defaultType: e } = a.parent.contentMatchAt(a.index());
				t.setNodeMarkup(s.start, e);
			}
			(c || c === 0) && t.lift(s, c);
		});
	}), !0;
}, Cc = (e) => (t) => e(t), wc = () => ({ state: e, dispatch: t }) => lr(e, t), Tc = (e, t) => ({ editor: n, tr: r }) => {
	let { state: i } = n, a = i.doc.slice(e.from, e.to);
	r.deleteRange(e.from, e.to);
	let o = r.mapping.map(t);
	return r.insert(o, a.content), r.setSelection(new w(r.doc.resolve(Math.max(o - 1, 0)))), !0;
}, Ec = () => ({ tr: e, dispatch: t }) => {
	let { selection: n } = e, r = n.$anchor.node();
	if (r.content.size > 0) return !1;
	let i = e.selection.$anchor;
	for (let n = i.depth; n > 0; --n) if (i.node(n).type === r.type) {
		if (t) {
			let t = i.before(n), r = i.after(n);
			e.delete(t, r).scrollIntoView();
		}
		return !0;
	}
	return !1;
};
function M(e, t) {
	if (typeof e == "string") {
		if (!t.nodes[e]) throw Error(`There is no node type named '${e}'. Maybe you forgot to add the extension?`);
		return t.nodes[e];
	}
	return e;
}
var Dc = (e) => ({ tr: t, state: n, dispatch: r }) => {
	let i = M(e, n.schema), a = t.selection.$anchor;
	for (let e = a.depth; e > 0; --e) if (a.node(e).type === i) {
		if (r) {
			let n = a.before(e), r = a.after(e);
			t.delete(n, r).scrollIntoView();
		}
		return !0;
	}
	return !1;
}, Oc = (e) => ({ tr: t, dispatch: n }) => {
	let { from: r, to: i } = e;
	return n && t.delete(r, i), !0;
}, kc = (e) => e.content ? /^text(\*|\+)/.test(e.content) : !1, Ac = (e, t, n) => {
	if (!e.parent.isInline || n === "left" && e.pos > e.start() || n === "right" && e.pos < e.end()) return e.pos;
	let r = t.nodes[e.parent.type.name].spec;
	return kc(r) ? n === "left" ? e.start() - 1 : e.end() + 1 : e.pos;
}, jc = (e, t, n) => ({
	from: Ac(e, n, "left"),
	to: Ac(t, n, "right")
}), Mc = () => ({ state: e, dispatch: t }) => {
	if (e.selection.empty) return !1;
	if (t) {
		let n = e.tr, { ranges: r } = e.selection, i = n.steps.length;
		r.forEach((t) => {
			let r = n.mapping.slice(i), { from: a, to: o } = jc(n.doc.resolve(r.map(t.$from.pos)), n.doc.resolve(r.map(t.$to.pos)), e.schema);
			n.deleteRange(a, o);
		}), n.selection.empty || n.setSelection(w.near(n.doc.resolve(n.selection.from))), n.scrollIntoView(), t(n);
	}
	return !0;
}, Nc = () => ({ commands: e }) => e.keyboardShortcut("Enter"), Pc = () => ({ state: e, dispatch: t }) => cr(e, t);
function Fc(e) {
	return Object.prototype.toString.call(e) === "[object RegExp]";
}
function Ic(e, t, n = { strict: !0 }) {
	let r = Object.keys(t);
	return !r.length || r.every((r) => n.strict ? t[r] === e[r] : Fc(t[r]) ? t[r].test(e[r]) : t[r] === e[r]);
}
function Lc(e, t, n = {}) {
	return e.find((e) => e.type === t && Ic(Object.fromEntries(Object.keys(n).map((t) => [t, e.attrs[t]])), n));
}
function Rc(e, t, n = {}) {
	return !!Lc(e, t, n);
}
function zc(e, t, n) {
	if (!e || !t) return;
	let r = e.parent.childAfter(e.parentOffset);
	if ((!r.node || !r.node.marks.some((e) => e.type === t)) && (r = e.parent.childBefore(e.parentOffset)), !r.node || !r.node.marks.some((e) => e.type === t)) return;
	if (!n) {
		let e = r.node.marks.find((e) => e.type === t);
		e && (n = e.attrs);
	}
	if (!Lc([...r.node.marks], t, n)) return;
	let i = r.index, a = e.start() + r.offset, o = i + 1, s = a + r.node.nodeSize;
	for (; i > 0 && Rc([...e.parent.child(i - 1).marks], t, n);) --i, a -= e.parent.child(i).nodeSize;
	for (; o < e.parent.childCount && Rc([...e.parent.child(o).marks], t, n);) s += e.parent.child(o).nodeSize, o += 1;
	return {
		from: a,
		to: s
	};
}
function Bc(e, t) {
	if (typeof e == "string") {
		if (!t.marks[e]) throw Error(`There is no mark type named '${e}'. Maybe you forgot to add the extension?`);
		return t.marks[e];
	}
	return e;
}
var Vc = (e, t) => ({ tr: n, state: r, dispatch: i }) => {
	let a = Bc(e, r.schema), { doc: o, selection: s } = n, { $from: c, from: l, to: u } = s;
	if (i) {
		let e = zc(c, a, t);
		if (e && e.from <= l && e.to >= u) {
			let t = w.create(o, e.from, e.to);
			n.setSelection(t);
		}
	}
	return !0;
}, Hc = (e) => (t) => {
	let n = typeof e == "function" ? e(t) : e;
	for (let e = 0; e < n.length; e += 1) if (n[e](t)) return !0;
	return !1;
};
function Uc(e) {
	return e instanceof w;
}
function Wc(e = 0, t = 0, n = 0) {
	return Math.min(Math.max(e, t), n);
}
function Gc(e, t = null) {
	if (!t) return null;
	let n = C.atStart(e), r = C.atEnd(e);
	if (t === "start" || t === !0) return n;
	if (t === "end") return r;
	let i = n.from, a = r.to;
	return t === "all" ? w.create(e, Wc(0, i, a), Wc(e.content.size, i, a)) : w.create(e, Wc(t, i, a), Wc(t, i, a));
}
function Kc() {
	return ["Android"].includes(navigator.platform) || /android/i.test(navigator.userAgent);
}
function qc() {
	return [
		"iPad Simulator",
		"iPhone Simulator",
		"iPod Simulator",
		"iPad",
		"iPhone",
		"iPod"
	].includes(navigator.platform) || navigator.userAgent.includes("Mac") && "ontouchend" in document;
}
function Jc() {
	return typeof navigator < "u" && /^((?!chrome|android).)*safari/i.test(navigator.userAgent);
}
var Yc = (e = null, t = {}) => ({ editor: n, view: r, tr: i, dispatch: a }) => {
	t = {
		scrollIntoView: !0,
		...t
	};
	let o = () => {
		(qc() || Kc()) && r.dom.focus(), Jc() && !qc() && !Kc() && r.dom.focus({ preventScroll: !0 }), requestAnimationFrame(() => {
			n.isDestroyed || (r.focus(), t?.scrollIntoView && n.commands.scrollIntoView());
		});
	};
	try {
		if (r.hasFocus() && e === null || e === !1) return !0;
	} catch {
		return !1;
	}
	if (a && e === null && !Uc(n.state.selection)) return o(), !0;
	let s = Gc(i.doc, e) || n.state.selection, c = n.state.selection.eq(s);
	return a && (c || i.setSelection(s), c && i.storedMarks && i.setStoredMarks(i.storedMarks), o()), !0;
}, Xc = (e, t) => (n) => e.every((e, r) => t(e, {
	...n,
	index: r
})), Zc = (e, t) => ({ tr: n, commands: r }) => r.insertContentAt({
	from: n.selection.from,
	to: n.selection.to
}, e, t), Qc = (e) => {
	let t = e.childNodes;
	for (let n = t.length - 1; n >= 0; --n) {
		let r = t[n];
		r.nodeType === 3 && r.nodeValue && /^(\n\s\s|\n)$/.test(r.nodeValue) ? e.removeChild(r) : r.nodeType === 1 && Qc(r);
	}
	return e;
};
function $c(e) {
	if (typeof window > "u") throw Error("[tiptap error]: there is no window object available, so this function cannot be used");
	let t = `<body>${e}</body>`, n = new window.DOMParser().parseFromString(t, "text/html").body;
	return Qc(n);
}
function el(e) {
	return typeof e?.nodesBetween == "function";
}
function tl(e, t, n) {
	if (el(e)) return e;
	let r = typeof e == "object" && !!e;
	n = {
		slice: !0,
		parseOptions: {},
		...n
	};
	let i = typeof e == "string";
	if (r) try {
		if (Array.isArray(e) && e.length > 0) return l.fromArray(e.map((e) => t.nodeFromJSON(e)));
		let r = t.nodeFromJSON(e);
		return n.errorOnInvalidContent && r.check(), r;
	} catch (r) {
		if (n.errorOnInvalidContent) throw Error("[tiptap error]: Invalid JSON content", { cause: r });
		return console.warn("[tiptap warn]: Invalid content.", "Passed value:", e, "Error:", r), tl("", t, n);
	}
	if (i) {
		if (n.errorOnInvalidContent) {
			let r = !1, i = "", a = new Ie({
				topNode: t.spec.topNode,
				marks: t.spec.marks,
				nodes: t.spec.nodes.append({ __tiptap__private__unknown__catch__all__node: {
					content: "inline*",
					group: "block",
					parseDOM: [{
						tag: "*",
						getAttrs: (e) => (r = !0, i = typeof e == "string" ? e : e.outerHTML, null)
					}]
				} })
			});
			if (n.slice ? Be.fromSchema(a).parseSlice($c(e), n.parseOptions) : Be.fromSchema(a).parse($c(e), n.parseOptions), n.errorOnInvalidContent && r) throw Error("[tiptap error]: Invalid HTML content", { cause: /* @__PURE__ */ Error(`Invalid element found: ${i}`) });
		}
		let r = Be.fromSchema(t);
		return n.slice ? r.parseSlice($c(e), n.parseOptions).content : r.parse($c(e), n.parseOptions);
	}
	return tl("", t, n);
}
function nl(e) {
	return !("type" in e);
}
function rl(e, t, n) {
	let r = e.steps.length - 1;
	if (r < t) return;
	let i = e.steps[r];
	if (!(i instanceof Dt || i instanceof Ot)) return;
	let a = e.mapping.maps[r], o = 0;
	a.forEach((e, t, n, r) => {
		o === 0 && (o = r);
	}), e.setSelection(C.near(e.doc.resolve(o), n));
}
var il = (e, t, n) => ({ tr: r, dispatch: i, editor: a }) => {
	if (i) {
		n = {
			parseOptions: a.options.parseOptions,
			updateSelection: !0,
			applyInputRules: !1,
			applyPasteRules: !1,
			...n
		};
		let i, o = (e) => {
			a.emit("contentError", {
				editor: a,
				error: e,
				disableCollaboration: () => {
					"collaboration" in a.storage && typeof a.storage.collaboration == "object" && a.storage.collaboration && (a.storage.collaboration.isDisabled = !0);
				}
			});
		}, s = {
			preserveWhitespace: "full",
			...n.parseOptions
		};
		if (!n.errorOnInvalidContent && !a.options.enableContentCheck && a.options.emitContentError) try {
			tl(t, a.schema, {
				parseOptions: s,
				errorOnInvalidContent: !0
			});
		} catch (e) {
			o(e);
		}
		try {
			i = tl(t, a.schema, {
				parseOptions: s,
				errorOnInvalidContent: n.errorOnInvalidContent ?? a.options.enableContentCheck
			});
		} catch (e) {
			return o(e), !1;
		}
		let { from: c, to: u } = typeof e == "number" ? {
			from: e,
			to: e
		} : {
			from: e.from,
			to: e.to
		}, d = !0, f = !0, p = nl(i) ? i.content : [i];
		if (p.forEach((e) => {
			e.check(), d = d ? e.isText && e.marks.length === 0 : !1, f = f ? e.isBlock : !1;
		}), c === u && f) {
			let { parent: e } = r.doc.resolve(c);
			e.isTextblock && !e.type.spec.code && !e.childCount && (--c, u += 1);
		}
		let m;
		if (d) m = Array.isArray(t) ? t.map((e) => e.text || "").join("") : el(t) ? p.map((e) => e.text ?? "").join("") : typeof t == "object" && t && t.text ? t.text : t, r.insertText(m, c, u);
		else {
			m = l.from(p);
			let e = r.doc.resolve(c), t = e.node(), n = e.parentOffset === 0, i = t.isText || t.isTextblock, a = t.content.size > 0;
			n && i && a && f && (c = Math.max(0, c - 1)), r.replaceWith(c, u, p);
		}
		n.updateSelection && rl(r, r.steps.length - 1, -1), n.applyInputRules && r.setMeta("applyInputRules", {
			from: c,
			text: m
		}), n.applyPasteRules && r.setMeta("applyPasteRules", {
			from: c,
			text: m
		});
	}
	return !0;
};
function al(e) {
	for (let t = 0; t < e.edgeCount; t += 1) {
		let { type: n } = e.edge(t);
		if (n.isTextblock && !n.hasRequiredAttrs()) return n;
	}
	return null;
}
var ol = (e = {}) => ({ tr: t, dispatch: n, editor: r }) => {
	let { pos: i, attrs: a, content: o, updateSelection: s = !0 } = e, c;
	c = typeof i == "number" ? t.doc.resolve(i) : i || t.selection.$from;
	let l = al(c.parent.contentMatchAt(c.index()));
	if (!l) return !1;
	let u = Object.keys(l.spec.attrs || {}), d = a ? Object.fromEntries(Object.entries(a).filter(([e]) => u.includes(e))) : {}, f;
	if (o) {
		let e = tl(o, r.schema);
		f = l.createAndFill(d, e);
	} else f = l.createAndFill(d);
	return f ? (n && (t.insert(c.pos, f), s && rl(t, t.steps.length - 1, -1)), !0) : !1;
}, sl = () => ({ state: e, dispatch: t }) => rr(e, t), cl = () => ({ state: e, dispatch: t }) => ir(e, t), ll = () => ({ state: e, dispatch: t }) => Kn(e, t), ul = () => ({ state: e, dispatch: t }) => er(e, t), dl = () => ({ state: e, dispatch: t, tr: n }) => {
	try {
		let r = Zt(e.doc, e.selection.$from.pos, -1);
		return r != null && (n.join(r, 2), t && t(n), !0);
	} catch {
		return !1;
	}
}, fl = () => ({ state: e, dispatch: t, tr: n }) => {
	try {
		let r = Zt(e.doc, e.selection.$from.pos, 1);
		return r != null && (n.join(r, 2), t && t(n), !0);
	} catch {
		return !1;
	}
}, pl = () => ({ state: e, dispatch: t }) => qn(e, t), ml = () => ({ state: e, dispatch: t }) => Jn(e, t);
function hl() {
	return typeof navigator < "u" && /Mac/.test(navigator.platform);
}
function gl(e) {
	let t = e.split(/-(?!$)/), n = t[t.length - 1];
	n === "Space" && (n = " ");
	let r, i, a, o;
	for (let e = 0; e < t.length - 1; e += 1) {
		let n = t[e];
		if (/^(cmd|meta|m)$/i.test(n)) o = !0;
		else if (/^a(lt)?$/i.test(n)) r = !0;
		else if (/^(c|ctrl|control)$/i.test(n)) i = !0;
		else if (/^s(hift)?$/i.test(n)) a = !0;
		else if (/^mod$/i.test(n)) qc() || hl() ? o = !0 : i = !0;
		else throw Error(`Unrecognized modifier name: ${n}`);
	}
	return r && (n = `Alt-${n}`), i && (n = `Ctrl-${n}`), o && (n = `Meta-${n}`), a && (n = `Shift-${n}`), n;
}
var _l = (e) => ({ editor: t, view: n, tr: r, dispatch: i }) => {
	let a = gl(e).split(/-(?!$)/), o = a.find((e) => ![
		"Alt",
		"Ctrl",
		"Meta",
		"Shift"
	].includes(e)), s = new KeyboardEvent("keydown", {
		key: o === "Space" ? " " : o,
		altKey: a.includes("Alt"),
		ctrlKey: a.includes("Ctrl"),
		metaKey: a.includes("Meta"),
		shiftKey: a.includes("Shift"),
		bubbles: !0,
		cancelable: !0
	});
	return t.captureTransaction(() => {
		n.someProp("handleKeyDown", (e) => e(n, s));
	})?.steps.forEach((e) => {
		let t = e.map(r.mapping);
		t && i && r.maybeStep(t);
	}), !0;
};
function vl(e, t, n = {}) {
	let { from: r, to: i, empty: a } = e.selection, o = t ? M(t, e.schema) : null, s = [];
	e.doc.nodesBetween(r, i, (e, t) => {
		if (e.isText) return;
		let n = Math.max(r, t), a = Math.min(i, t + e.nodeSize);
		s.push({
			node: e,
			from: n,
			to: a
		});
	});
	let c = i - r, l = s.filter((e) => !o || o.name === e.node.type.name).filter((e) => Ic(e.node.attrs, n, { strict: !1 }));
	return a ? !!l.length : l.reduce((e, t) => e + t.to - t.from, 0) >= c;
}
var yl = (e, t = {}) => ({ state: n, dispatch: r }) => vl(n, M(e, n.schema), t) ? ar(n, r) : !1, bl = () => ({ state: e, dispatch: t }) => ur(e, t), xl = (e) => ({ state: t, dispatch: n }) => Ar(M(e, t.schema))(t, n), Sl = () => ({ state: e, dispatch: t }) => or(e, t);
function Cl(e, t) {
	return t.nodes[e] ? "node" : t.marks[e] ? "mark" : null;
}
function wl(e, t) {
	let n = typeof t == "string" ? [t] : t;
	return Object.keys(e).reduce((t, r) => (n.includes(r) || (t[r] = e[r]), t), {});
}
var Tl = (e, t) => ({ tr: n, state: r, dispatch: i }) => {
	let a = null, o = null, s = Cl(typeof e == "string" ? e : e.name, r.schema);
	if (!s) return !1;
	s === "node" && (a = M(e, r.schema)), s === "mark" && (o = Bc(e, r.schema));
	let c = !1;
	return n.selection.ranges.forEach((e) => {
		r.doc.nodesBetween(e.$from.pos, e.$to.pos, (e, r) => {
			a && a === e.type && (c = !0, i && n.setNodeMarkup(r, void 0, wl(e.attrs, t))), o && e.marks.length && e.marks.forEach((a) => {
				o === a.type && (c = !0, i && n.addMark(r, r + e.nodeSize, o.create(wl(a.attrs, t))));
			});
		});
	}), c;
}, El = () => ({ tr: e, dispatch: t }) => (t && e.scrollIntoView(), !0), Dl = () => ({ tr: e, dispatch: t }) => {
	if (t) {
		let t = new On(e.doc);
		e.setSelection(t);
	}
	return !0;
}, Ol = () => ({ state: e, dispatch: t }) => Zn(e, t), kl = () => ({ state: e, dispatch: t }) => tr(e, t), Al = () => ({ state: e, dispatch: t }) => pr(e, t), jl = () => ({ state: e, dispatch: t }) => yr(e, t), Ml = () => ({ state: e, dispatch: t }) => vr(e, t);
function Nl(e, t, n = {}, r = {}) {
	return tl(e, t, {
		slice: !1,
		parseOptions: n,
		errorOnInvalidContent: r.errorOnInvalidContent
	});
}
var Pl = (e, { errorOnInvalidContent: t, emitUpdate: n = !0, parseOptions: r = {} } = {}) => ({ editor: i, tr: a, dispatch: o, commands: s }) => {
	let { doc: c } = a;
	if (r.preserveWhitespace !== "full") {
		let s = Nl(e, i.schema, r, { errorOnInvalidContent: t ?? i.options.enableContentCheck });
		if (o) {
			let e = nl(s) ? s.content : [s];
			a.replaceWith(0, c.content.size, e).setMeta("preventUpdate", !n);
		}
		return !0;
	}
	return o && a.setMeta("preventUpdate", !n), s.insertContentAt({
		from: 0,
		to: c.content.size
	}, e, {
		parseOptions: r,
		errorOnInvalidContent: t ?? i.options.enableContentCheck
	});
};
function Fl(e, t) {
	let n = Bc(t, e.schema), { from: r, to: i, empty: a } = e.selection, o = [];
	a ? (e.storedMarks && o.push(...e.storedMarks), o.push(...e.selection.$head.marks())) : e.doc.nodesBetween(r, i, (e) => {
		o.push(...e.marks);
	});
	let s = o.find((e) => e.type.name === n.name);
	return s ? { ...s.attrs } : {};
}
function Il(e, t) {
	let n = new xn(e);
	return t.forEach((e) => {
		e.steps.forEach((e) => {
			n.step(e);
		});
	}), n;
}
function Ll(e, t, n) {
	let r = [];
	return e.nodesBetween(t.from, t.to, (e, t) => {
		n(e) && r.push({
			node: e,
			pos: t
		});
	}), r;
}
function Rl(e, t) {
	for (let n = e.depth; n > 0; --n) {
		let r = e.node(n);
		if (t(r)) return {
			pos: n > 0 ? e.before(n) : 0,
			start: e.start(n),
			depth: n,
			node: r
		};
	}
}
function zl(e) {
	return (t) => Rl(t.$from, e);
}
function N(e, t, n) {
	return e.config[t] === void 0 && e.parent ? N(e.parent, t, n) : typeof e.config[t] == "function" ? e.config[t].bind({
		...n,
		parent: e.parent ? N(e.parent, t, n) : null
	}) : e.config[t];
}
function Bl(e) {
	return e.map((e) => {
		let t = N(e, "addExtensions", {
			name: e.name,
			options: e.options,
			storage: e.storage
		});
		return t ? [e, ...Bl(t())] : e;
	}).flat(10);
}
function Vl(e, t) {
	let n = et.fromSchema(t).serializeFragment(e), r = document.implementation.createHTMLDocument().createElement("div");
	return r.appendChild(n), r.innerHTML;
}
function Hl(e) {
	return typeof e == "function";
}
function P(e, t = void 0, ...n) {
	return Hl(e) ? t ? e.bind(t)(...n) : e(...n) : e;
}
function Ul(e = {}) {
	return Object.keys(e).length === 0 && e.constructor === Object;
}
function Wl(e) {
	return {
		baseExtensions: e.filter((e) => e.type === "extension"),
		nodeExtensions: e.filter((e) => e.type === "node"),
		markExtensions: e.filter((e) => e.type === "mark")
	};
}
function Gl(e) {
	let t = [], { nodeExtensions: n, markExtensions: r } = Wl(e), i = [...n, ...r], a = {
		default: null,
		validate: void 0,
		rendered: !0,
		renderHTML: null,
		parseHTML: null,
		keepOnSplit: !0,
		isRequired: !1
	}, o = n.filter((e) => e.name !== "text").map((e) => e.name), s = r.map((e) => e.name), c = [...o, ...s];
	return e.forEach((e) => {
		let n = N(e, "addGlobalAttributes", {
			name: e.name,
			options: e.options,
			storage: e.storage,
			extensions: i
		});
		n && n().forEach((e) => {
			let n;
			n = Array.isArray(e.types) ? e.types : e.types === "*" ? c : e.types === "nodes" ? o : e.types === "marks" ? s : [], n.forEach((n) => {
				Object.entries(e.attributes).forEach(([e, r]) => {
					t.push({
						type: n,
						name: e,
						attribute: {
							...a,
							...r
						}
					});
				});
			});
		});
	}), i.forEach((e) => {
		let n = N(e, "addAttributes", {
			name: e.name,
			options: e.options,
			storage: e.storage
		});
		if (!n) return;
		let r = n();
		Object.entries(r).forEach(([n, r]) => {
			let i = {
				...a,
				...r
			};
			typeof i?.default == "function" && (i.default = i.default()), i?.isRequired && i?.default === void 0 && delete i.default, t.push({
				type: e.name,
				name: n,
				attribute: i
			});
		});
	}), t;
}
function Kl(e) {
	let t = [], n = "", r = !1, i = !1, a = 0, o = e.length;
	for (let s = 0; s < o; s += 1) {
		let o = e[s];
		if (o === "'" && !i) {
			r = !r, n += o;
			continue;
		}
		if (o === "\"" && !r) {
			i = !i, n += o;
			continue;
		}
		if (!r && !i) {
			if (o === "(") {
				a += 1, n += o;
				continue;
			}
			if (o === ")" && a > 0) {
				--a, n += o;
				continue;
			}
			if (o === ";" && a === 0) {
				t.push(n), n = "";
				continue;
			}
		}
		n += o;
	}
	return n && t.push(n), t;
}
function ql(e) {
	let t = [], n = Kl(e || ""), r = n.length;
	for (let e = 0; e < r; e += 1) {
		let r = n[e], i = r.indexOf(":");
		if (i === -1) continue;
		let a = r.slice(0, i).trim(), o = r.slice(i + 1).trim();
		a && o && t.push([a, o]);
	}
	return t;
}
function F(...e) {
	return e.filter((e) => !!e).reduce((e, t) => {
		let n = { ...e };
		return Object.entries(t).forEach(([e, t]) => {
			if (e === "__proto__") {
				Object.defineProperty(n, e, {
					configurable: !0,
					enumerable: !0,
					value: t,
					writable: !0
				});
				return;
			}
			if (!n[e]) {
				n[e] = t;
				return;
			}
			if (e === "class") {
				let r = t ? String(t).split(" ") : [], i = n[e] ? n[e].split(" ") : [], a = r.filter((e) => !i.includes(e));
				n[e] = [...i, ...a].join(" ");
			} else if (e === "style") {
				let r = new Map([...ql(n[e]), ...ql(t)]);
				n[e] = Array.from(r.entries()).map(([e, t]) => `${e}: ${t}`).join("; ");
			} else n[e] = t;
		}), n;
	}, {});
}
function Jl(e, t) {
	return t.filter((t) => t.type === e.type.name).filter((e) => e.attribute.rendered).map((t) => t.attribute.renderHTML ? t.attribute.renderHTML(e.attrs) || {} : { [t.name]: e.attrs[t.name] }).reduce((e, t) => F(e, t), {});
}
function Yl(e) {
	return typeof e == "string" ? e.match(/^[+-]?(?:\d*\.)?\d+$/) ? Number(e) : e === "true" || e !== "false" && e : e;
}
function Xl(e, t) {
	return "style" in e ? e : {
		...e,
		getAttrs: (n) => {
			let r = e.getAttrs ? e.getAttrs(n) : e.attrs;
			if (r === !1) return !1;
			let i = t.reduce((e, t) => {
				let r = t.attribute.parseHTML ? t.attribute.parseHTML(n) : Yl(n.getAttribute(t.name));
				return r == null ? e : {
					...e,
					[t.name]: r
				};
			}, {});
			return {
				...r,
				...i
			};
		}
	};
}
function Zl(e) {
	return Object.fromEntries(Object.entries(e).filter(([e, t]) => e === "attrs" && Ul(t) ? !1 : t != null));
}
function Ql(e) {
	var t, n;
	let r = {};
	return !(e != null && (t = e.attribute) != null && t.isRequired) && "default" in (e?.attribute || {}) && (r.default = e.attribute.default), (e == null || (n = e.attribute) == null ? void 0 : n.validate) !== void 0 && (r.validate = e.attribute.validate), [e.name, r];
}
function $l(e, t) {
	let n = Gl(e), { nodeExtensions: r, markExtensions: i } = Wl(e), a = r.find((e) => N(e, "topNode"))?.name;
	return new Ie({
		topNode: a,
		nodes: Object.fromEntries(r.map((r) => {
			let i = n.filter((e) => e.type === r.name), a = {
				name: r.name,
				options: r.options,
				storage: r.storage,
				editor: t
			}, o = Zl({
				...e.reduce((e, t) => {
					let n = N(t, "extendNodeSchema", a);
					return {
						...e,
						...n ? n(r) : {}
					};
				}, {}),
				content: P(N(r, "content", a)),
				marks: P(N(r, "marks", a)),
				group: P(N(r, "group", a)),
				inline: P(N(r, "inline", a)),
				atom: P(N(r, "atom", a)),
				selectable: P(N(r, "selectable", a)),
				draggable: P(N(r, "draggable", a)),
				code: P(N(r, "code", a)),
				whitespace: P(N(r, "whitespace", a)),
				linebreakReplacement: P(N(r, "linebreakReplacement", a)),
				defining: P(N(r, "defining", a)),
				isolating: P(N(r, "isolating", a)),
				attrs: Object.fromEntries(i.map(Ql))
			}), s = P(N(r, "parseHTML", a));
			s && (o.parseDOM = s.map((e) => Xl(e, i)));
			let c = N(r, "renderHTML", a);
			c && (o.toDOM = (e) => c({
				node: e,
				HTMLAttributes: Jl(e, i)
			}));
			let l = N(r, "renderText", a);
			return l && (o.toText = l), [r.name, o];
		})),
		marks: Object.fromEntries(i.map((r) => {
			let i = n.filter((e) => e.type === r.name), a = {
				name: r.name,
				options: r.options,
				storage: r.storage,
				editor: t
			}, o = Zl({
				...e.reduce((e, t) => {
					let n = N(t, "extendMarkSchema", a);
					return {
						...e,
						...n ? n(r) : {}
					};
				}, {}),
				inclusive: P(N(r, "inclusive", a)),
				excludes: P(N(r, "excludes", a)),
				group: P(N(r, "group", a)),
				spanning: P(N(r, "spanning", a)),
				code: P(N(r, "code", a)),
				attrs: Object.fromEntries(i.map(Ql))
			}), s = P(N(r, "parseHTML", a));
			s && (o.parseDOM = s.map((e) => Xl(e, i)));
			let c = N(r, "renderHTML", a);
			return c && (o.toDOM = (e) => c({
				mark: e,
				HTMLAttributes: Jl(e, i)
			})), [r.name, o];
		}))
	});
}
function eu(e) {
	let t = e.filter((t, n) => e.indexOf(t) !== n);
	return Array.from(new Set(t));
}
function tu(e) {
	return e.sort((e, t) => {
		let n = N(e, "priority") || 100, r = N(t, "priority") || 100;
		return n > r ? -1 : +(n < r);
	});
}
function nu(e) {
	let t = tu(Bl(e)), n = eu(t.map((e) => e.name));
	return n.length && console.warn(`[tiptap warn]: Duplicate extension names found: [${n.map((e) => `'${e}'`).join(", ")}]. This can lead to issues.`), t;
}
function ru(e, t) {
	return $l(nu(e), t);
}
function iu(e, t, n) {
	let { from: r, to: i } = t, { blockSeparator: a = "\n\n", textSerializers: o = {} } = n || {}, s = "";
	return e.nodesBetween(r, i, (e, n, c, l) => {
		e.isBlock && n > r && (s += a);
		let u = o?.[e.type.name];
		if (u) return c && (s += u({
			node: e,
			pos: n,
			parent: c,
			index: l,
			range: t
		})), !1;
		if (e.isText) {
			var d;
			s += e == null || (d = e.text) == null ? void 0 : d.slice(Math.max(r, n) - n, i - n);
		}
	}), s;
}
function au(e, t) {
	return iu(e, {
		from: 0,
		to: e.content.size
	}, t);
}
function ou(e) {
	return Object.fromEntries(Object.entries(e.nodes).filter(([, e]) => e.spec.toText).map(([e, t]) => [e, t.spec.toText]));
}
function su(e, t) {
	let n = M(t, e.schema), { from: r, to: i } = e.selection, a = [];
	e.doc.nodesBetween(r, i, (e) => {
		a.push(e);
	});
	let o = a.reverse().find((e) => e.type.name === n.name);
	return o ? { ...o.attrs } : {};
}
function cu(e, t) {
	let n = Cl(typeof t == "string" ? t : t.name, e.schema);
	return n === "node" ? su(e, t) : n === "mark" ? Fl(e, t) : {};
}
function lu(e, t = JSON.stringify) {
	let n = {};
	return e.filter((e) => {
		let r = t(e);
		return Object.prototype.hasOwnProperty.call(n, r) ? !1 : n[r] = !0;
	});
}
function uu(e) {
	let t = lu(e);
	return t.length === 1 ? t : t.filter((e, n) => !t.filter((e, t) => t !== n).some((t) => e.oldRange.from >= t.oldRange.from && e.oldRange.to <= t.oldRange.to && e.newRange.from >= t.newRange.from && e.newRange.to <= t.newRange.to));
}
function du(e) {
	let { mapping: t, steps: n } = e, r = [];
	return t.maps.forEach((e, i) => {
		let a = [];
		if (e.ranges.length) e.forEach((e, t) => {
			a.push({
				from: e,
				to: t
			});
		});
		else {
			let { from: e, to: t } = n[i];
			if (e === void 0 || t === void 0) return;
			a.push({
				from: e,
				to: t
			});
		}
		a.forEach(({ from: e, to: n }) => {
			let a = t.slice(i).map(e, -1), o = t.slice(i).map(n), s = t.invert().map(a, -1), c = t.invert().map(o);
			r.push({
				oldRange: {
					from: s,
					to: c
				},
				newRange: {
					from: a,
					to: o
				}
			});
		});
	}), uu(r);
}
function fu(e, t, n) {
	let r = [];
	return e === t ? n.resolve(e).marks().forEach((t) => {
		let i = zc(n.resolve(e), t.type);
		i && r.push({
			mark: t,
			...i
		});
	}) : n.nodesBetween(e, t, (e, t) => {
		e && e?.nodeSize !== void 0 && r.push(...e.marks.map((n) => ({
			from: t,
			to: t + e.nodeSize,
			mark: n
		})));
	}), r;
}
var pu = (e, t, n, r = 20) => {
	let i = e.doc.resolve(n), a = r, o = null;
	for (; a > 0 && o === null;) {
		let e = i.node(a);
		e?.type.name === t ? o = e : --a;
	}
	return [o, a];
}, mu = (e) => {
	let t = e.depth - 1;
	if (t < 0) return null;
	let n = e.index(t);
	return n === 0 ? null : e.node(t).child(n - 1);
};
function hu(e, t) {
	return t.nodes[e] || t.marks[e] || null;
}
function gu(e, t, n) {
	return Object.fromEntries(Object.entries(n).filter(([n]) => {
		let r = e.find((e) => e.type === t && e.name === n);
		return r ? r.attribute.keepOnSplit : !1;
	}));
}
var _u = (e, t = 500) => {
	let n = "", r = e.parentOffset;
	return e.parent.nodesBetween(Math.max(0, r - t), r, (e, t, i, a) => {
		var o;
		let s = (o = e.type.spec).toText?.call(o, {
			node: e,
			pos: t,
			parent: i,
			index: a
		}) || e.textContent || "%leaf%";
		n += e.isAtom && !e.isText ? s : s.slice(0, Math.max(0, r - t));
	}), n;
};
function vu(e, t, n = {}) {
	let { empty: r, ranges: i } = e.selection, a = t ? Bc(t, e.schema) : null;
	if (r) return !!(e.storedMarks || e.selection.$from.marks()).filter((e) => !a || a.name === e.type.name).find((e) => Ic(e.attrs, n, { strict: !1 }));
	let o = 0, s = [];
	if (i.forEach(({ $from: t, $to: n }) => {
		let r = t.pos, i = n.pos;
		e.doc.nodesBetween(r, i, (e, t) => {
			if (a && e.inlineContent && !e.type.allowsMarkType(a)) return !1;
			if (!e.isText && !e.marks.length) return;
			let n = Math.max(r, t), c = Math.min(i, t + e.nodeSize), l = c - n;
			o += l, s.push(...e.marks.map((e) => ({
				mark: e,
				from: n,
				to: c
			})));
		});
	}), o === 0) return !1;
	let c = s.filter((e) => !a || a.name === e.mark.type.name).filter((e) => Ic(e.mark.attrs, n, { strict: !1 })).reduce((e, t) => e + t.to - t.from, 0), l = s.filter((e) => !a || e.mark.type !== a && e.mark.type.excludes(a)).reduce((e, t) => e + t.to - t.from, 0);
	return (c > 0 ? c + l : c) >= o;
}
function yu(e, t, n = {}) {
	if (!t) return vl(e, null, n) || vu(e, null, n);
	let r = Cl(t, e.schema);
	return r === "node" ? vl(e, t, n) : r === "mark" && vu(e, t, n);
}
var bu = (e, t) => {
	let { $from: n, $to: r, $anchor: i } = e.selection;
	if (t) {
		let n = zl((e) => e.type.name === t)(e.selection);
		if (!n) return !1;
		let r = e.doc.resolve(n.pos + 1);
		return i.pos + 1 === r.end();
	}
	return !(r.parentOffset < r.parent.nodeSize - 2 || n.pos !== r.pos);
}, xu = (e) => {
	let { $from: t, $to: n } = e.selection;
	return !(t.parentOffset > 0 || t.pos !== n.pos);
};
function Su(e, t) {
	return Array.isArray(t) ? t.some((t) => (typeof t == "string" ? t : t.name) === e.name) : t;
}
function Cu(e, t) {
	let { nodeExtensions: n } = Wl(t), r = n.find((t) => t.name === e);
	if (!r) return !1;
	let i = P(N(r, "group", {
		name: r.name,
		options: r.options,
		storage: r.storage
	}));
	return typeof i == "string" && i.split(" ").includes("list");
}
function wu(e, { checkChildren: t = !0, ignoreWhitespace: n = !1 } = {}) {
	if (n) {
		if (e.type.name === "hardBreak") return !0;
		if (e.isText) return !/\S/.test(e.text ?? "");
	}
	if (e.isText) return !e.text;
	if (e.isAtom || e.isLeaf) return !1;
	if (e.content.childCount === 0) return !0;
	if (t) {
		let r = !0;
		return e.content.forEach((e) => {
			r !== !1 && (wu(e, {
				ignoreWhitespace: n,
				checkChildren: t
			}) || (r = !1));
		}), r;
	}
	return !1;
}
function Tu(e) {
	return e instanceof T;
}
var Eu = class e {
	constructor(e) {
		this.position = e;
	}
	static fromJSON(t) {
		return new e(t.position);
	}
	toJSON() {
		return { position: this.position };
	}
};
function Du(e, t) {
	let n = t.mapping.mapResult(e.position);
	return {
		position: new Eu(n.pos),
		mapResult: n
	};
}
function Ou(e) {
	return new Eu(e);
}
function ku(e, t, n) {
	let { selection: r } = t, i = null;
	if (Uc(r) && (i = r.$cursor), i) {
		let t = e.storedMarks ?? i.marks();
		return i.parent.type.allowsMarkType(n) && (!!n.isInSet(t) || !t.some((e) => e.type.excludes(n)));
	}
	let { ranges: a } = r;
	return a.some(({ $from: t, $to: r }) => {
		let i = t.depth === 0 && e.doc.inlineContent && e.doc.type.allowsMarkType(n);
		return e.doc.nodesBetween(t.pos, r.pos, (e, t, r) => {
			if (i) return !1;
			if (e.isInline) {
				let t = !r || r.type.allowsMarkType(n), a = !!n.isInSet(e.marks) || !e.marks.some((e) => e.type.excludes(n));
				i = t && a;
			}
			return !i;
		}), i;
	});
}
var Au = (e, t = {}) => ({ tr: n, state: r, dispatch: i }) => {
	let { selection: a } = n, { empty: o, ranges: s } = a, c = Bc(e, r.schema);
	if (i) {
		if (o) {
			let e = Fl(r, c);
			n.addStoredMark(c.create({
				...e,
				...t
			}));
		} else s.forEach((e) => {
			let i = e.$from.pos, a = e.$to.pos;
			r.doc.nodesBetween(i, a, (e, r) => {
				let o = Math.max(r, i), s = Math.min(r + e.nodeSize, a);
				e.marks.find((e) => e.type === c) ? e.marks.forEach((e) => {
					c === e.type && n.addMark(o, s, c.create({
						...e.attrs,
						...t
					}));
				}) : n.addMark(o, s, c.create(t));
			});
		});
	}
	return ku(r, n, c);
}, ju = (e, t) => ({ tr: n }) => (n.setMeta(e, t), !0), Mu = (e, t = {}) => ({ state: n, dispatch: r, chain: i }) => {
	let a = M(e, n.schema), o;
	return n.selection.$anchor.sameParent(n.selection.$head) && (o = n.selection.$anchor.parent.attrs), a.isTextblock ? i().command(({ commands: e }) => xr(a, {
		...o,
		...t
	})(n) ? !0 : e.clearNodes()).command(({ state: e }) => xr(a, {
		...o,
		...t
	})(e, r)).run() : (console.warn("[tiptap warn]: Currently \"setNode()\" only supports text block nodes."), !1);
}, Nu = (e) => ({ tr: t, dispatch: n }) => {
	if (n) {
		let { doc: n } = t, r = Wc(e, 0, n.content.size), i = T.create(n, r);
		t.setSelection(i);
	}
	return !0;
}, Pu = (e, t) => ({ tr: n, state: r, dispatch: i }) => {
	let { selection: a } = r, o, s;
	return typeof t == "number" ? (o = t, s = t) : t && "from" in t && "to" in t ? (o = t.from, s = t.to) : (o = a.from, s = a.to), i && n.doc.nodesBetween(o, s, (t, r) => {
		t.isText || n.setNodeMarkup(r, void 0, {
			...t.attrs,
			dir: e
		});
	}), !0;
}, Fu = (e) => ({ tr: t, dispatch: n }) => {
	if (n) {
		let { doc: n } = t, { from: r, to: i } = typeof e == "number" ? {
			from: e,
			to: e
		} : e, a = w.atStart(n).from, o = w.atEnd(n).to, s = Wc(r, a, o), c = Wc(i, a, o), l = w.create(n, s, c);
		t.setSelection(l);
	}
	return !0;
}, Iu = (e) => ({ state: t, dispatch: n }) => Nr(M(e, t.schema))(t, n);
function Lu(e, t) {
	let n = e.storedMarks || e.selection.$to.parentOffset && e.selection.$from.marks();
	if (n) {
		let r = n.filter((e) => t?.includes(e.type.name));
		e.tr.ensureMarks(r);
	}
}
var Ru = ({ keepMarks: e = !0 } = {}) => ({ tr: t, state: n, dispatch: r, editor: i }) => {
	let { selection: a, doc: o } = t, { $from: s, $to: c } = a, l = i.extensionManager.attributes, u = gu(l, s.node().type.name, s.node().attrs);
	if (a instanceof T && a.node.isBlock) return !s.parentOffset || !Kt(o, s.pos) ? !1 : (r && (e && Lu(n, i.extensionManager.splittableMarks), t.split(s.pos).scrollIntoView()), !0);
	if (!s.parent.isBlock) return !1;
	let d = c.parentOffset === c.parent.content.size, f = s.depth === 0 ? void 0 : al(s.node(-1).contentMatchAt(s.indexAfter(-1))), p = d && f ? [{
		type: f,
		attrs: u
	}] : void 0, m = Kt(t.doc, t.mapping.map(s.pos), 1, p);
	if (!p && !m && Kt(t.doc, t.mapping.map(s.pos), 1, f ? [{ type: f }] : void 0) && (m = !0, p = f ? [{
		type: f,
		attrs: u
	}] : void 0), r) {
		if (m && (a instanceof w && t.deleteSelection(), t.split(t.mapping.map(s.pos), 1, p), f && !d && !s.parentOffset && s.parent.type !== f)) {
			let e = t.mapping.map(s.before()), n = t.doc.resolve(e);
			s.node(-1).canReplaceWith(n.index(), n.index() + 1, f) && t.setNodeMarkup(t.mapping.map(s.before()), f);
		}
		e && Lu(n, i.extensionManager.splittableMarks), t.scrollIntoView();
	}
	return m;
}, zu = (e, t = {}) => ({ tr: n, state: r, dispatch: i, editor: a }) => {
	let o = M(e, r.schema), { $from: s, $to: c } = r.selection, u = r.selection.node;
	if (u && u.isBlock || s.depth < 2 || !s.sameParent(c)) return !1;
	let d = s.node(-1);
	if (d.type !== o) return !1;
	let f = a.extensionManager.attributes;
	if (s.parent.content.size === 0 && s.node(-1).childCount === s.indexAfter(-1)) {
		if (s.depth === 2 || s.node(-3).type !== o || s.index(-2) !== s.node(-2).childCount - 1) return !1;
		if (i) {
			let e = l.empty, r = s.index(-1) ? 1 : s.index(-2) ? 2 : 3;
			for (let t = s.depth - r; t >= s.depth - 3; --t) e = l.from(s.node(t).copy(e));
			let i = s.indexAfter(-1) < s.node(-2).childCount ? 1 : s.indexAfter(-2) < s.node(-3).childCount ? 2 : 3, a = {
				...gu(f, s.node().type.name, s.node().attrs),
				...t
			}, c = o.contentMatch.defaultType?.createAndFill(a) || void 0;
			e = e.append(l.from(o.createAndFill(null, c) || void 0));
			let u = s.before(s.depth - (r - 1));
			n.replace(u, s.after(-i), new h(e, 4 - r, 0));
			let d = -1;
			n.doc.nodesBetween(u, n.doc.content.size, (e, t) => {
				if (d > -1) return !1;
				e.isTextblock && e.content.size === 0 && (d = t + 1);
			}), d > -1 && n.setSelection(w.near(n.doc.resolve(d))), n.scrollIntoView();
		}
		return !0;
	}
	let p = c.pos === s.end() ? d.contentMatchAt(0).defaultType : null, m = {
		...gu(f, d.type.name, d.attrs),
		...t
	}, g = {
		...gu(f, s.node().type.name, s.node().attrs),
		...t
	};
	n.delete(s.pos, c.pos);
	let _ = p ? [{
		type: o,
		attrs: m
	}, {
		type: p,
		attrs: g
	}] : [{
		type: o,
		attrs: m
	}];
	if (!Kt(n.doc, s.pos, 2)) return !1;
	if (i) {
		let { selection: e, storedMarks: t } = r, { splittableMarks: o } = a.extensionManager, c = t || e.$to.parentOffset && e.$from.marks();
		if (n.split(s.pos, 2, _).scrollIntoView(), !c || !i) return !0;
		let l = c.filter((e) => o.includes(e.type.name));
		n.ensureMarks(l);
	}
	return !0;
};
function Bu(e) {
	return !e || e === "1" ? null : e;
}
function Vu(e, t) {
	return Bu(e) === Bu(t);
}
var Hu = (e, t) => {
	let n = zl((e) => e.type === t)(e.selection);
	if (!n) return !0;
	let r = e.doc.resolve(Math.max(0, n.pos - 1)).before(n.depth);
	if (r === void 0) return !0;
	let i = e.doc.nodeAt(r);
	return !(n.node.type === i?.type && Jt(e.doc, n.pos)) || !Vu(n.node.attrs.type, i?.attrs.type) || e.join(n.pos), !0;
}, Uu = (e, t) => {
	let n = zl((e) => e.type === t)(e.selection);
	if (!n) return !0;
	let r = e.doc.resolve(n.start).after(n.depth);
	if (r === void 0) return !0;
	let i = e.doc.nodeAt(r);
	return !(n.node.type === i?.type && Jt(e.doc, r)) || !Vu(n.node.attrs.type, i?.attrs.type) || e.join(r), !0;
};
function Wu(e) {
	let t = e.doc, n = t.firstChild;
	if (!n) return null;
	let r = t.resolve(1), i = t.resolve(n.nodeSize - 1);
	return w.between(r, i);
}
var Gu = (e, t, n, r = {}) => ({ editor: i, tr: a, state: o, dispatch: s, chain: c, commands: l, can: u }) => {
	let { extensions: d, splittableMarks: f } = i.extensionManager, p = M(e, o.schema), m = M(t, o.schema), { selection: h, storedMarks: g } = o, { $from: _, $to: ee } = h, v = _.blockRange(ee), y = g || h.$to.parentOffset && h.$from.marks();
	if (!v) return !1;
	let b = zl((e) => Cu(e.type.name, d))(h), x = h.from === 0 && h.to === o.doc.content.size, te = o.doc.content.content, ne = te.length === 1 ? te[0] : null, re = x && ne && Cu(ne.type.name, d) ? {
		node: ne,
		pos: 0,
		depth: 0
	} : null, ie = b ?? re, ae = !!b && v.depth >= 1 && v.depth - b.depth <= 1, oe = !!re;
	if ((ae || oe) && ie) {
		if (ie.node.type === p) return x && oe ? c().command(({ tr: e, dispatch: t }) => {
			let n = Wu(e);
			return n ? (e.setSelection(n), t && t(e), !0) : !1;
		}).liftListItem(m).run() : l.liftListItem(m);
		if (Cu(ie.node.type.name, d) && p.validContent(ie.node.content)) return c().command(() => (a.setNodeMarkup(ie.pos, p), !0)).command(() => Hu(a, p)).command(() => Uu(a, p)).run();
	}
	return !n || !y || !s ? c().command(() => u().wrapInList(p, r) ? !0 : l.clearNodes()).wrapInList(p, r).command(() => Hu(a, p)).command(() => Uu(a, p)).run() : c().command(() => {
		let e = u().wrapInList(p, r), t = y.filter((e) => f.includes(e.type.name));
		return a.ensureMarks(t), e ? !0 : l.clearNodes();
	}).wrapInList(p, r).command(() => Hu(a, p)).command(() => Uu(a, p)).run();
}, Ku = (e, t = {}, n = {}) => ({ state: r, commands: i }) => {
	let { extendEmptyMarkRange: a = !1 } = n, o = Bc(e, r.schema);
	return vu(r, o, t) ? i.unsetMark(o, { extendEmptyMarkRange: a }) : i.setMark(o, t);
}, qu = (e, t, n = {}) => ({ state: r, commands: i }) => {
	let a = M(e, r.schema), o = M(t, r.schema), s = vl(r, a, n), c;
	return r.selection.$anchor.sameParent(r.selection.$head) && (c = r.selection.$anchor.parent.attrs), s ? i.setNode(o, c) : i.setNode(a, {
		...c,
		...n
	});
}, Ju = (e, t = {}) => ({ state: n, commands: r }) => {
	let i = M(e, n.schema);
	return vl(n, i, t) ? r.lift(i) : r.wrapIn(i, t);
}, Yu = () => ({ state: e, dispatch: t }) => {
	let n = e.plugins;
	for (let r = 0; r < n.length; r += 1) {
		let i = n[r], a;
		if (i.spec.isInputRules && (a = i.getState(e))) {
			if (t) {
				let t = e.tr, n = a.transform;
				for (let e = n.steps.length - 1; e >= 0; --e) t.step(n.steps[e].invert(n.docs[e]));
				if (a.text) {
					let n = t.doc.resolve(a.from).marks();
					t.replaceWith(a.from, a.to, e.schema.text(a.text, n));
				} else t.delete(a.from, a.to);
			}
			return !0;
		}
	}
	return !1;
}, Xu = (e = {}) => ({ tr: t, dispatch: n, editor: r }) => {
	let { ignoreClearable: i = !1 } = e, { selection: a } = t, { empty: o, ranges: s } = a;
	if (o) return !0;
	let { nonClearableMarks: c } = r.extensionManager;
	if (n) {
		let e = Object.values(r.schema.marks).filter((e) => i || !c.includes(e.name));
		s.forEach((n) => {
			for (let r of e) t.removeMark(n.$from.pos, n.$to.pos, r);
		});
	}
	return !0;
}, Zu = (e, t = {}) => ({ tr: n, state: r, dispatch: i }) => {
	let { extendEmptyMarkRange: a = !1 } = t, { selection: o } = n, s = Bc(e, r.schema), { $from: c, empty: l, ranges: u } = o;
	if (!i) return !0;
	if (l && a) {
		let { from: e, to: t } = o, r = zc(c, s, c.marks().find((e) => e.type === s)?.attrs);
		r && (e = r.from, t = r.to), n.removeMark(e, t, s);
	} else u.forEach((e) => {
		n.removeMark(e.$from.pos, e.$to.pos, s);
	});
	return n.removeStoredMark(s), !0;
}, Qu = (e) => ({ tr: t, state: n, dispatch: r }) => {
	let { selection: i } = n, a, o;
	return typeof e == "number" ? (a = e, o = e) : e && "from" in e && "to" in e ? (a = e.from, o = e.to) : (a = i.from, o = i.to), r && t.doc.nodesBetween(a, o, (e, n) => {
		if (e.isText) return;
		let r = { ...e.attrs };
		delete r.dir, t.setNodeMarkup(n, void 0, r);
	}), !0;
}, $u = (e, t = {}) => ({ tr: n, state: r, dispatch: i }) => {
	let a = null, o = null, s = Cl(typeof e == "string" ? e : e.name, r.schema);
	if (!s) return !1;
	s === "node" && (a = M(e, r.schema)), s === "mark" && (o = Bc(e, r.schema));
	let c = !1;
	return n.selection.ranges.forEach((e) => {
		let s = e.$from.pos, l = e.$to.pos, u, d, f, p;
		n.selection.empty ? r.doc.nodesBetween(s, l, (e, t) => {
			a && a === e.type && (c = !0, f = Math.max(t, s), p = Math.min(t + e.nodeSize, l), u = t, d = e);
		}) : r.doc.nodesBetween(s, l, (e, r) => {
			r < s && a && a === e.type && (c = !0, f = Math.max(r, s), p = Math.min(r + e.nodeSize, l), u = r, d = e), r >= s && r <= l && (a && a === e.type && (c = !0, i && n.setNodeMarkup(r, void 0, {
				...e.attrs,
				...t
			})), o && e.marks.length && e.marks.forEach((a) => {
				if (o === a.type && (c = !0, i)) {
					let i = Math.max(r, s), c = Math.min(r + e.nodeSize, l);
					n.addMark(i, c, o.create({
						...a.attrs,
						...t
					}));
				}
			}));
		}), d && (u !== void 0 && i && n.setNodeMarkup(u, void 0, {
			...d.attrs,
			...t
		}), o && d.marks.length && d.marks.forEach((e) => {
			o === e.type && i && n.addMark(f, p, o.create({
				...e.attrs,
				...t
			}));
		}));
	}), c;
}, ed = new D("__tiptap_decorations__"), td = (e) => ({ tr: t, dispatch: n }) => (n && t.setMeta(ed, {
	type: "force",
	name: e
}), !0), nd = (e, t = {}) => ({ state: n, dispatch: r }) => br(M(e, n.schema), t)(n, r), rd = (e, t = {}) => ({ state: n, dispatch: r }) => Dr(M(e, n.schema), t)(n, r), id = /* @__PURE__ */ r({
	blur: () => bc,
	clearContent: () => xc,
	clearNodes: () => Sc,
	command: () => Cc,
	createParagraphNear: () => wc,
	cut: () => Tc,
	deleteCurrentNode: () => Ec,
	deleteNode: () => Dc,
	deleteRange: () => Oc,
	deleteSelection: () => Mc,
	enter: () => Nc,
	exitCode: () => Pc,
	extendMarkRange: () => Vc,
	first: () => Hc,
	focus: () => Yc,
	forEach: () => Xc,
	insertContent: () => Zc,
	insertContentAt: () => il,
	insertDefaultBlock: () => ol,
	joinBackward: () => ll,
	joinDown: () => cl,
	joinForward: () => ul,
	joinItemBackward: () => dl,
	joinItemForward: () => fl,
	joinTextblockBackward: () => pl,
	joinTextblockForward: () => ml,
	joinUp: () => sl,
	keyboardShortcut: () => _l,
	lift: () => yl,
	liftEmptyBlock: () => bl,
	liftListItem: () => xl,
	newlineInCode: () => Sl,
	resetAttributes: () => Tl,
	scrollIntoView: () => El,
	selectAll: () => Dl,
	selectNodeBackward: () => Ol,
	selectNodeForward: () => kl,
	selectParentNode: () => Al,
	selectTextblockEnd: () => jl,
	selectTextblockStart: () => Ml,
	setContent: () => Pl,
	setMark: () => Au,
	setMeta: () => ju,
	setNode: () => Mu,
	setNodeSelection: () => Nu,
	setTextDirection: () => Pu,
	setTextSelection: () => Fu,
	sinkListItem: () => Iu,
	splitBlock: () => Ru,
	splitListItem: () => zu,
	toggleList: () => Gu,
	toggleMark: () => Ku,
	toggleNode: () => qu,
	toggleWrap: () => Ju,
	undoInputRule: () => Yu,
	unsetAllMarks: () => Xu,
	unsetMark: () => Zu,
	unsetTextDirection: () => Qu,
	updateAttributes: () => $u,
	updateDecorations: () => td,
	wrapIn: () => nd,
	wrapInList: () => rd
}), ad = /* @__PURE__ */ new WeakMap();
function od(e, t) {
	ad.set(e, (ad.get(e) ?? 0) + 1);
	try {
		return t();
	} finally {
		let t = (ad.get(e) ?? 1) - 1;
		t > 0 ? ad.set(e, t) : ad.delete(e);
	}
}
function sd(e) {
	return ad.has(e);
}
var cd = class {
	constructor() {
		this.callbacks = {};
	}
	on(e, t) {
		return this.callbacks[e] || (this.callbacks[e] = []), this.callbacks[e].push(t), this;
	}
	emit(e, ...t) {
		let n = this.callbacks[e];
		return n && n.forEach((e) => e.apply(this, t)), this;
	}
	off(e, t) {
		let n = this.callbacks[e];
		return n && (t ? this.callbacks[e] = n.filter((e) => e !== t) : delete this.callbacks[e]), this;
	}
	once(e, t) {
		let n = (...r) => {
			this.off(e, n), t.apply(this, r);
		};
		return this.on(e, n);
	}
	removeAllListeners() {
		this.callbacks = {};
	}
}, ld = typeof process < "u" && process.env.NODE_ENV !== "production";
function ud(e) {
	return e.kind === "widget";
}
function dd(e, t) {
	let n = [], r = /* @__PURE__ */ new Set();
	for (let i of e) i.kind === "widget" && ud(i) && r.add(i.key), n.push(i.toPMDecoration(t));
	return {
		decorations: n,
		widgetKeys: r
	};
}
function fd(e, t, n) {
	let { decorations: r, widgetKeys: i } = dd(t, n);
	return {
		set: j.create(e, r),
		widgetKeys: i
	};
}
function pd({ position: e, from: t, to: n, docSize: r }) {
	return e < t ? !1 : e < n || e === n && n === r;
}
function md({ decorations: e, from: t, to: n, docSize: r, extensionName: i, warnedExtensions: a }) {
	return e.filter((e) => pd({
		position: e.anchor,
		from: t,
		to: n,
		docSize: r
	}) ? !0 : (e.anchor === n || a.has(i) || (a.add(i), console.warn(`[tiptap warn]: Extension "${i}" returned a decoration outside the requested range [${t}, ${n}). It was ignored.`)), !1));
}
function hd(e) {
	let t = e.spec?.key;
	return typeof t == "string" ? t : void 0;
}
function gd(e) {
	let t = /* @__PURE__ */ new Map(), n = /* @__PURE__ */ new Map();
	for (let r of e.find()) {
		let e = hd(r);
		if (!e) continue;
		let i = r.spec.extensionName ?? "unknown", a = t.get(e) ?? /* @__PURE__ */ new Set();
		a.add(i), t.set(e, a), n.set(e, (n.get(e) ?? 0) + 1);
	}
	return Array.from(t, ([e, t]) => ({
		key: e,
		extensions: t
	})).filter(({ key: e }) => (n.get(e) ?? 0) > 1);
}
function _d(e) {
	return e.jsonID === "attr";
}
function vd(e) {
	let t = !1;
	if (e.getMap().forEach(() => {
		t = !0;
	}), t || _d(e)) return !0;
	let n = e;
	return typeof n.from == "number" && typeof n.to == "number";
}
function yd(e, t) {
	let n = null, r = 0, i = 0;
	for (let a = 0; a < e.childCount && !(i > t.to); a += 1) {
		let o = i + e.child(a).nodeSize;
		o >= t.from && (n === null && (n = i), r = o), i = o;
	}
	return n === null ? null : {
		from: n,
		to: r
	};
}
function bd(e, t) {
	if (e.steps.some((e) => !vd(e))) return { type: "full" };
	let n = du(e).map(({ newRange: e }) => e);
	e.steps.forEach((t, r) => {
		if (!_d(t)) return;
		let i = e.mapping.slice(r);
		n.push({
			from: i.map(t.pos, -1),
			to: i.map(t.pos + 1)
		});
	});
	let r = [];
	for (let e of n) {
		let n = yd(t, e);
		n && r.push(n);
	}
	r.sort((e, t) => e.from - t.from);
	let i = [];
	for (let e of r) {
		let t = i[i.length - 1];
		t && e.from <= t.to ? t.to = Math.max(t.to, e.to) : i.push({ ...e });
	}
	return {
		type: "ranges",
		ranges: i
	};
}
function xd(e, t, n, r) {
	return e.map(t, n, { onRemove: (e) => {
		let t = e?.key;
		typeof t == "string" && r.delete(t);
	} });
}
function Sd(e, t, n) {
	let r = t.decorationSetsByExtension[e] ?? j.empty, i = new Set(t.widgetKeysByExtension[e] ?? []);
	return {
		set: xd(r, n.mapping, n.doc, i),
		widgetKeys: i
	};
}
function Cd(e, t) {
	let n = Object.values(t).flatMap((e) => e.find());
	return j.create(e, n);
}
function wd(e) {
	let t = /* @__PURE__ */ new Set();
	for (let n of Object.values(e)) for (let e of n) t.add(e);
	return t;
}
function Td(e, t) {
	switch (t.update ?? "document") {
		case "document":
			if (t.createInRange) throw Error(`[tiptap error]: Extension "${e}" provides createInRange() but does not use the "changedRanges" decoration update strategy.`);
			return;
		case "changedRanges":
			if (!t.createInRange) throw Error(`[tiptap error]: Extension "${e}" uses the "changedRanges" decoration update strategy but does not provide createInRange().`);
			return;
		case "manual":
			if (t.createInRange) throw Error(`[tiptap error]: Extension "${e}" uses the "manual" decoration update strategy, which is not compatible with createInRange(). createInRange() requires the "changedRanges" strategy.`);
			if (t.shouldUpdate) throw Error(`[tiptap error]: Extension "${e}" cannot combine the "manual" decoration update strategy with shouldUpdate().`);
			return;
		default: throw Error(`[tiptap error]: Extension "${e}" uses an unknown decoration update strategy. Expected "document", "changedRanges", or "manual".`);
	}
}
function Ed(e, t, n) {
	return n ? !0 : e.update === "manual" ? !1 : e.shouldUpdate ? e.shouldUpdate(t) : t.tr.docChanged;
}
var Dd = /* @__PURE__ */ new Set(), Od = class {
	constructor(e) {
		this.warnedWidgetKeys = /* @__PURE__ */ new Set(), this.warnedOutOfRangeExtensions = /* @__PURE__ */ new Set(), this.handleBeforeTransaction = ({ nextState: e }) => {
			let t = ed.getState(e);
			t && this.warnDuplicateWidgetKeys(t);
		}, this.editor = e.editor, this.entries = this.resolveEntries(e.entries), this.entries.forEach(({ name: e, spec: t }) => Td(e, t)), this.plugin = this.entries.length > 0 ? this.createPlugin() : null, this.editor.on("beforeTransaction", this.handleBeforeTransaction);
	}
	destroy() {
		this.editor.off("beforeTransaction", this.handleBeforeTransaction);
	}
	liveWidgetKeys() {
		return ed.getState(this.editor.state)?.widgetKeys ?? Dd;
	}
	get mountedView() {
		return this.editor.isDestroyed ? null : this.editor.view;
	}
	resolveEntries(e) {
		let t = [];
		for (let { name: n, addDecorations: r } of e) {
			let e = r();
			e && t.push({
				name: n,
				spec: e
			});
		}
		return t;
	}
	createPlugin() {
		let { editor: e, entries: t } = this;
		return new E({
			key: ed,
			state: {
				init: (e, n) => {
					let r = {}, i = {};
					for (let { name: e, spec: a } of t) {
						let { set: t, widgetKeys: o } = this.buildFullSet(e, a, n);
						r[e] = t, i[e] = o;
					}
					let a = {
						decorationSetsByExtension: r,
						widgetKeysByExtension: i,
						mergedDecorationSet: this.buildMergedSet(n.doc, r),
						widgetKeys: wd(i)
					};
					return this.warnDuplicateWidgetKeys(a), a;
				},
				apply: (n, r, i, a) => {
					let o = n.getMeta(ed), s = o?.type === "force" && !o.name, c = o?.type === "force" ? o.name : void 0, l = {}, u = {}, d = /* @__PURE__ */ new Set();
					return od(e, () => {
						for (let { name: o, spec: f } of t) {
							let t = s || c === o;
							if (!Ed(f, {
								editor: e,
								tr: n,
								oldState: i,
								newState: a
							}, t)) {
								let e = Sd(o, r, n);
								l[o] = e.set, u[o] = e.widgetKeys;
							} else if (f.update === "changedRanges" && n.docChanged && !t) {
								let e = this.applyChangedRangesRecompute(o, f, r, n, a);
								l[o] = e.set, u[o] = e.widgetKeys, d.add(o);
							} else {
								let { set: e, widgetKeys: t } = this.buildFullSet(o, f, a);
								l[o] = e, u[o] = t, d.add(o);
							}
						}
					}), d.size === 0 && !n.docChanged ? r : {
						decorationSetsByExtension: l,
						widgetKeysByExtension: u,
						mergedDecorationSet: this.mergeAfterApply({
							entries: t,
							previous: r,
							tr: n,
							decorationSetsByExtension: l,
							recomputedNames: d
						}),
						widgetKeys: wd(u)
					};
				}
			},
			props: { decorations(e) {
				return ed.getState(e)?.mergedDecorationSet ?? j.empty;
			} }
		});
	}
	applyChangedRangesRecompute(e, t, n, r, i) {
		let a = bd(r, i.doc);
		return a.type === "full" ? this.buildFullSet(e, t, i) : this.rebuildRanges(e, t, n, r, i, a.ranges);
	}
	rebuildRanges(e, t, n, r, i, a) {
		let o = n.decorationSetsByExtension[e] ?? j.empty, s = new Set(n.widgetKeysByExtension[e] ?? []), c = xd(o, r.mapping, r.doc, s), l = i.doc.content.size;
		for (let { from: n, to: r } of a) {
			let a = c.find(n, r).filter((e) => pd({
				position: e.from,
				from: n,
				to: r,
				docSize: l
			}));
			for (let e of a) {
				let t = hd(e);
				t && s.delete(t);
			}
			c = c.remove(a);
			let { decorations: o, widgetKeys: u } = dd(md({
				decorations: this.runCreate(e, "createInRange", () => t.createInRange({
					editor: this.editor,
					state: i,
					view: this.mountedView,
					from: n,
					to: r
				})),
				from: n,
				to: r,
				docSize: l,
				extensionName: e,
				warnedExtensions: this.warnedOutOfRangeExtensions
			}), e);
			c = c.add(i.doc, o);
			for (let e of u) s.add(e);
		}
		return {
			set: c,
			widgetKeys: s
		};
	}
	buildFullSet(e, t, n) {
		let r = this.runCreate(e, "create", () => t.create({
			editor: this.editor,
			state: n,
			view: this.mountedView
		}));
		return fd(n.doc, r, e);
	}
	runCreate(e, t, n) {
		try {
			return n();
		} catch (n) {
			return console.error(`[tiptap error]: Extension "${e}" threw in \`addDecorations().${t}()\`. Its decorations were dropped for this update.`, n), [];
		}
	}
	warnDuplicateWidgetKeys(e) {
		if (!ld) return;
		if (e.widgetKeys.size === 0) {
			this.warnedWidgetKeys.clear();
			return;
		}
		let t = gd(e.mergedDecorationSet), n = new Set(t.map(({ key: e }) => e));
		for (let { key: e, extensions: n } of t) {
			if (this.warnedWidgetKeys.has(e)) continue;
			let t = Array.from(n).map((e) => `"${e}"`).join(", ");
			console.warn(`[tiptap warn]: Duplicate widget decoration key "${e}" in extension${n.size === 1 ? "" : "s"} ${t}. Widget decoration keys must be globally unique, otherwise ProseMirror misplaces the widget DOM. Use a stable, unique key (e.g. \`comment-\${id}\`).`);
		}
		this.warnedWidgetKeys = n;
	}
	buildMergedSet(e, t) {
		let n = Object.keys(t);
		return n.length === 1 ? t[n[0]] : Cd(e, t);
	}
	mergeAfterApply({ entries: e, previous: t, tr: n, decorationSetsByExtension: r, recomputedNames: i }) {
		return e.length === 1 ? r[e[0].name] : i.size === 0 ? t.mergedDecorationSet.map(n.mapping, n.doc) : Cd(n.doc, r);
	}
};
function kd(e, t) {
	let { selection: n } = e, { $from: r } = n;
	if (n instanceof T) {
		let e = r.index();
		return r.parent.canReplaceWith(e, e + 1, t);
	}
	let i = r.depth;
	for (; i >= 0;) {
		let e = r.index(i);
		if (r.node(i).contentMatchAt(e).matchType(t)) return !0;
		--i;
	}
	return !1;
}
function Ad(e, t, n) {
	let r = document.querySelector(`style[data-tiptap-style${n ? `-${n}` : ""}]`);
	if (r !== null) return r;
	let i = document.createElement("style");
	return t && i.setAttribute("nonce", t), i.setAttribute(`data-tiptap-style${n ? `-${n}` : ""}`, ""), i.innerHTML = e, document.getElementsByTagName("head")[0].appendChild(i), i;
}
function jd(e) {
	return typeof e == "number";
}
function Md(e) {
	return Object.prototype.toString.call(e).slice(8, -1);
}
function Nd(e) {
	return Md(e) === "Object" && e.constructor === Object && Object.getPrototypeOf(e) === Object.prototype;
}
function Pd(e, t, n) {
	let r = e.split("\n"), i = [], a = "", o = 0, s = t.baseIndentSize || 2;
	for (; o < r.length;) {
		let e = r[o], u = e.match(t.itemPattern);
		if (!u) {
			if (i.length > 0) break;
			if (e.trim() === "") {
				o += 1, a = `${a}${e}\n`;
				continue;
			}
			return;
		}
		let d = t.extractItemData(u), { indentLevel: f, mainContent: p } = d;
		a = `${a}${e}\n`;
		let m = [p];
		for (o += 1; o < r.length;) {
			var c;
			let e = r[o];
			if (e.trim() === "") {
				var l;
				let t = r.slice(o + 1).findIndex((e) => e.trim() !== "");
				if (t === -1) break;
				if ((((l = r[o + 1 + t].match(/^(\s*)/)) == null || (l = l[1]) == null ? void 0 : l.length) || 0) > f) {
					m.push(e), a = `${a}${e}\n`, o += 1;
					continue;
				}
				break;
			}
			if ((((c = e.match(/^(\s*)/)) == null || (c = c[1]) == null ? void 0 : c.length) || 0) > f) m.push(e), a = `${a}${e}\n`, o += 1;
			else break;
		}
		let h, g = m.slice(1);
		if (g.length > 0) {
			let e = g.map((e) => e.slice(f + s)).join("\n");
			e.trim() && (h = t.customNestedParser ? t.customNestedParser(e) : n.blockTokens(e));
		}
		let _ = t.createToken(d, h);
		i.push(_);
	}
	if (i.length !== 0) return {
		items: i,
		raw: a
	};
}
var Fd = 4;
function Id(e) {
	let t = 0;
	for (let n of e) t = n === "	" ? t + Fd - t % Fd : t + 1;
	return t;
}
function Ld(e, t, n, r, i) {
	if (!e || !Array.isArray(e.content)) return "";
	let a = typeof n == "function" ? n(r) : n, [o, ...s] = e.content, c = `${a}${t.renderChildren([o])}`;
	return s && s.length > 0 && s.forEach((e, n) => {
		let r = t.renderChild?.call(t, e, n + 1) ?? t.renderChildren([e]);
		if (r != null) {
			let n = (e) => {
				if (!i?.alignNestedToPrefix) return t.indent(e);
				let n = t.indent(""), r = Id(a);
				return (Id(n) >= r ? n : " ".repeat(r)) + e;
			}, o = r.split("\n").map((e) => n(e || "")).join("\n");
			c += e.type === "paragraph" ? `\n\n${o}` : `\n${o}`;
		}
	}), c;
}
function Rd(e, t) {
	let n = { ...e };
	return Nd(e) && Nd(t) && Object.keys(t).forEach((r) => {
		Nd(t[r]) && Nd(e[r]) ? n[r] = Rd(e[r], t[r]) : n[r] = t[r];
	}), n;
}
function zd(e, t, n = {}) {
	let { state: r } = t, { doc: i, tr: a } = r, o = e;
	i.descendants((t, r) => {
		let i = a.mapping.map(r), s = a.mapping.map(r) + t.nodeSize, c = null;
		if (t.marks.forEach((e) => {
			if (e !== o) return !1;
			c = e;
		}), !c) return;
		let l = !1;
		if (Object.keys(n).forEach((e) => {
			n[e] !== c.attrs[e] && (l = !0);
		}), l) {
			let t = e.type.create({
				...e.attrs,
				...n
			});
			a.removeMark(i, s, e.type), a.addMark(i, s, t);
		}
	}), a.docChanged && t.view.dispatch(a);
}
var Bd = class {
	constructor(e) {
		this.find = e.find, this.handler = e.handler, this.undoable = e.undoable ?? !0;
	}
}, Vd = (e, t) => {
	if (Fc(t)) return t.exec(e);
	let n = t(e);
	if (!n) return null;
	let r = [n.text];
	return r.index = n.index, r.input = e, r.data = n.data, n.replaceWith && (n.text.includes(n.replaceWith) || console.warn("[tiptap warn]: \"inputRuleMatch.replaceWith\" must be part of \"inputRuleMatch.text\"."), r.push(n.replaceWith)), r;
};
function Hd(e) {
	let { editor: t, from: n, to: r, text: i, rules: a, plugin: o } = e, { view: s } = t;
	if (s.composing) return !1;
	let c = s.state.doc.resolve(n);
	if (c.parent.type.spec.code || (c.nodeBefore || c.nodeAfter)?.marks.find((e) => e.type.spec.code)) return !1;
	let l = !1, u = _u(c) + i;
	return a.forEach((e) => {
		if (l) return;
		let a = Vd(u, e.find);
		if (!a) return;
		let d = a[0].length - i.length;
		if (d > 0) {
			let e = c.parentOffset - d;
			if (e < 0 || c.parent.textBetween(e, c.parentOffset) !== a[0].slice(0, d)) return;
		}
		let f = s.state.tr, p = vc({
			state: s.state,
			transaction: f
		}), m = {
			from: n - (a[0].length - i.length),
			to: r
		}, { commands: h, chain: g, can: _ } = new yc({
			editor: t,
			state: p
		});
		e.handler({
			state: p,
			range: m,
			match: a,
			commands: h,
			chain: g,
			can: _
		}) !== null && f.steps.length && (e.undoable && f.setMeta(o, {
			transform: f,
			from: n,
			to: r,
			text: i
		}), s.dispatch(f), l = !0);
	}), l;
}
function Ud(e) {
	let { editor: t, rules: n } = e, r = new E({
		state: {
			init() {
				return null;
			},
			apply(e, i, a) {
				let o = e.getMeta(r);
				if (o) return o;
				let s = e.getMeta("applyInputRules");
				return s && setTimeout(() => {
					let { text: e } = s;
					e = typeof e == "string" ? e : Vl(l.from(e), a.schema);
					let { from: i } = s, o = i + e.length;
					Hd({
						editor: t,
						from: i,
						to: o,
						text: e,
						rules: n,
						plugin: r
					});
				}), e.selectionSet || e.docChanged ? null : i;
			}
		},
		props: {
			handleTextInput(e, i, a, o) {
				return Hd({
					editor: t,
					from: i,
					to: a,
					text: o,
					rules: n,
					plugin: r
				});
			},
			handleDOMEvents: { compositionend: (e) => (setTimeout(() => {
				let { $cursor: i } = e.state.selection;
				i && Hd({
					editor: t,
					from: i.pos,
					to: i.pos,
					text: "",
					rules: n,
					plugin: r
				});
			}), !1) },
			handleKeyDown(e, i) {
				if (i.key !== "Enter") return !1;
				let { $cursor: a } = e.state.selection;
				return a ? Hd({
					editor: t,
					from: a.pos,
					to: a.pos,
					text: "\n",
					rules: n,
					plugin: r
				}) : !1;
			}
		},
		isInputRules: !0
	});
	return r;
}
var Wd = class {
	constructor(e = {}) {
		this.type = "extendable", this.parent = null, this.child = null, this.name = "", this.config = { name: this.name }, this.config = {
			...this.config,
			...e
		}, this.name = this.config.name;
	}
	get options() {
		return { ...P(N(this, "addOptions", { name: this.name })) };
	}
	get storage() {
		return { ...P(N(this, "addStorage", {
			name: this.name,
			options: this.options
		})) };
	}
	configure(e = {}) {
		let t = this.extend({
			...this.config,
			addOptions: () => Rd(this.options, e)
		});
		return t.name = this.name, t.parent = this.parent, this.child = null, t;
	}
	extend(e = {}) {
		let t = new this.constructor({
			...this.config,
			...e
		});
		return t.parent = this, this.child = t, t.name = "name" in e ? e.name : t.parent.name, t;
	}
}, Gd = class e extends Wd {
	constructor(...e) {
		super(...e), this.type = "mark";
	}
	static create(t = {}) {
		let n = typeof t == "function" ? t() : t;
		return new e(n);
	}
	static handleExit({ editor: e, mark: t }) {
		let { tr: n } = e.state, r = e.state.selection.$from;
		if (r.pos === r.end()) {
			let i = r.marks();
			if (!i.find((e) => e?.type.name === t.name)) return !1;
			let a = i.find((e) => e?.type.name === t.name);
			return a && n.removeStoredMark(a), n.insertText(" ", r.pos), e.view.dispatch(n), !0;
		}
		return !1;
	}
	configure(e) {
		return super.configure(e);
	}
	extend(e) {
		let t = typeof e == "function" ? e() : e;
		return super.extend(t);
	}
}, Kd = class {
	constructor(e) {
		this.find = e.find, this.handler = e.handler;
	}
}, qd = (e, t, n) => {
	if (Fc(t)) return [...e.matchAll(t)];
	let r = t(e, n);
	return r ? r.map((t) => {
		let n = [t.text];
		return n.index = t.index, n.input = e, n.data = t.data, t.replaceWith && (t.text.includes(t.replaceWith) || console.warn("[tiptap warn]: \"pasteRuleMatch.replaceWith\" must be part of \"pasteRuleMatch.text\"."), n.push(t.replaceWith)), n;
	}) : [];
};
function Jd(e) {
	let { editor: t, state: n, from: r, to: i, rule: a, pasteEvent: o, dropEvent: s } = e, { commands: c, chain: l, can: u } = new yc({
		editor: t,
		state: n
	}), d = [];
	return n.doc.nodesBetween(r, i, (e, t) => {
		var f;
		if ((f = e.type) != null && (f = f.spec) != null && f.code || !(e.isText || e.isTextblock || e.isInline)) return;
		let p = e.content?.size ?? e.nodeSize ?? 0, m = Math.max(r, t), h = Math.min(i, t + p);
		m >= h || qd(e.isText ? e.text || "" : e.textBetween(m - t, h - t, void 0, "￼"), a.find, o).forEach((e) => {
			if (e.index === void 0) return;
			let t = m + e.index + 1, r = t + e[0].length, i = {
				from: n.tr.mapping.map(t),
				to: n.tr.mapping.map(r)
			}, f = a.handler({
				state: n,
				range: i,
				match: e,
				commands: c,
				chain: l,
				can: u,
				pasteEvent: o,
				dropEvent: s
			});
			d.push(f);
		});
	}), d.every((e) => e !== null);
}
var Yd = null, Xd = (e) => {
	var t;
	let n = new ClipboardEvent("paste", { clipboardData: new DataTransfer() });
	return (t = n.clipboardData) == null || t.setData("text/html", e), n;
};
function Zd(e) {
	let { editor: t, rules: n } = e, r = null, i = !1, a = !1, o = typeof ClipboardEvent < "u" ? new ClipboardEvent("paste") : null, s;
	try {
		s = typeof DragEvent < "u" ? new DragEvent("drop") : null;
	} catch {
		s = null;
	}
	let c = ({ state: e, from: n, to: r, rule: i, pasteEvt: a }) => {
		let c = e.tr, l = vc({
			state: e,
			transaction: c
		});
		if (Jd({
			editor: t,
			state: l,
			from: Math.max(n - 1, 0),
			to: r.b - 1,
			rule: i,
			pasteEvent: a,
			dropEvent: s
		}) && c.steps.length) {
			try {
				s = typeof DragEvent < "u" ? new DragEvent("drop") : null;
			} catch {
				s = null;
			}
			return o = typeof ClipboardEvent < "u" ? new ClipboardEvent("paste") : null, c;
		}
	};
	return n.map((e) => new E({
		view(e) {
			let n = (n) => {
				r = e.dom.parentElement?.contains(n.target) ? e.dom.parentElement : null, r && (Yd = t);
			}, i = () => {
				Yd && (Yd = null);
			};
			return window.addEventListener("dragstart", n), window.addEventListener("dragend", i), { destroy() {
				window.removeEventListener("dragstart", n), window.removeEventListener("dragend", i);
			} };
		},
		props: { handleDOMEvents: {
			drop: (e, t) => {
				if (a = r === e.dom.parentElement, s = t, !a) {
					let e = Yd;
					e?.isEditable && setTimeout(() => {
						let t = e.state.selection;
						t && e.commands.deleteRange({
							from: t.from,
							to: t.to
						});
					}, 10);
				}
				return !1;
			},
			paste: (e, t) => {
				let n = t.clipboardData?.getData("text/html");
				return o = t, i = !!n?.includes("data-pm-slice"), !1;
			}
		} },
		appendTransaction: (t, n, r) => {
			let s = t[0], u = s.getMeta("uiEvent") === "paste" && !i, d = s.getMeta("uiEvent") === "drop" && !a, f = s.getMeta("applyPasteRules"), p = !!f;
			if (!u && !d && !p) return;
			if (p) {
				let { text: t } = f;
				t = typeof t == "string" ? t : Vl(l.from(t), r.schema);
				let { from: n } = f, i = n + t.length, a = Xd(t);
				return c({
					rule: e,
					state: r,
					from: n,
					to: { b: i },
					pasteEvt: a
				});
			}
			let m = n.doc.content.findDiffStart(r.doc.content), h = n.doc.content.findDiffEnd(r.doc.content);
			if (jd(m) && h && m !== h.b) return c({
				rule: e,
				state: r,
				from: m,
				to: h,
				pasteEvt: o
			});
		}
	}));
}
var Qd = class {
	constructor(e, t) {
		this.splittableMarks = [], this.nonClearableMarks = [], this.decorationManager = null, this.editor = t, this.baseExtensions = e, this.extensions = nu(e), this.schema = $l(this.extensions, t), this.setupExtensions();
	}
	get commands() {
		return this.extensions.reduce((e, t) => {
			let n = N(t, "addCommands", {
				name: t.name,
				options: t.options,
				storage: this.editor.extensionStorage[t.name],
				editor: this.editor,
				type: hu(t.name, this.schema)
			});
			return n ? {
				...e,
				...n()
			} : e;
		}, {});
	}
	get plugins() {
		let { editor: e } = this, t = tu([...this.extensions].reverse()).flatMap((t) => {
			let n = {
				name: t.name,
				options: t.options,
				storage: this.editor.extensionStorage[t.name],
				editor: e,
				type: hu(t.name, this.schema)
			}, r = [], i = N(t, "addKeyboardShortcuts", n), a = {};
			if (t.type === "mark" && N(t, "exitable", n) && (a.ArrowRight = () => Gd.handleExit({
				editor: e,
				mark: t
			})), i) {
				let t = Object.fromEntries(Object.entries(i()).map(([t, n]) => [t, () => n({ editor: e })]));
				a = {
					...a,
					...t
				};
			}
			let o = gc(a);
			r.push(o);
			let s = N(t, "addInputRules", n);
			if (Su(t, e.options.enableInputRules) && s) {
				let t = s();
				if (t && t.length) {
					let n = Ud({
						editor: e,
						rules: t
					}), i = Array.isArray(n) ? n : [n];
					r.push(...i);
				}
			}
			let c = N(t, "addPasteRules", n);
			if (Su(t, e.options.enablePasteRules) && c) {
				let t = c();
				if (t && t.length) {
					let n = Zd({
						editor: e,
						rules: t
					});
					r.push(...n);
				}
			}
			let l = N(t, "addProseMirrorPlugins", n);
			if (l) {
				let e = l();
				r.push(...e);
			}
			return r;
		}), n = this.createDecorationPlugin();
		return n && t.push(n), t;
	}
	createDecorationPlugin() {
		var e;
		let { editor: t } = this;
		(e = this.decorationManager) == null || e.destroy();
		let n = [];
		return this.extensions.forEach((e) => {
			let r = N(e, "addDecorations", {
				name: e.name,
				options: e.options,
				storage: this.editor.extensionStorage[e.name],
				editor: t,
				type: hu(e.name, this.schema)
			});
			r && n.push({
				name: e.name,
				addDecorations: r
			});
		}), this.decorationManager = new Od({
			editor: t,
			entries: n
		}), this.decorationManager.plugin;
	}
	get attributes() {
		return Gl(this.extensions);
	}
	get nodeViews() {
		let { editor: e } = this, { nodeExtensions: t } = Wl(this.extensions);
		return Object.fromEntries(t.filter((e) => !!N(e, "addNodeView")).map((t) => {
			let n = this.attributes.filter((e) => e.type === t.name), r = N(t, "addNodeView", {
				name: t.name,
				options: t.options,
				storage: this.editor.extensionStorage[t.name],
				editor: e,
				type: M(t.name, this.schema)
			});
			if (!r) return [];
			let i = r();
			return i ? [t.name, (r, a, o, s, c) => {
				let l = Jl(r, n);
				return i({
					node: r,
					view: a,
					getPos: o,
					decorations: s,
					innerDecorations: c,
					editor: e,
					extension: t,
					HTMLAttributes: l
				});
			}] : [];
		}));
	}
	dispatchTransaction(e) {
		let { editor: t } = this;
		return tu([...this.extensions].reverse()).reduceRight((e, n) => {
			let r = {
				name: n.name,
				options: n.options,
				storage: this.editor.extensionStorage[n.name],
				editor: t,
				type: hu(n.name, this.schema)
			}, i = N(n, "dispatchTransaction", r);
			return i ? (t) => {
				i.call(r, {
					transaction: t,
					next: e
				});
			} : e;
		}, e);
	}
	transformPastedHTML(e) {
		let { editor: t } = this;
		return tu([...this.extensions]).reduce((e, n) => {
			let r = {
				name: n.name,
				options: n.options,
				storage: this.editor.extensionStorage[n.name],
				editor: t,
				type: hu(n.name, this.schema)
			}, i = N(n, "transformPastedHTML", r);
			return i ? (t, n) => {
				let a = e(t, n);
				return i.call(r, a);
			} : e;
		}, e || ((e) => e));
	}
	get markViews() {
		let { editor: e } = this, { markExtensions: t } = Wl(this.extensions);
		return Object.fromEntries(t.filter((e) => !!N(e, "addMarkView")).map((t) => {
			let n = this.attributes.filter((e) => e.type === t.name), r = N(t, "addMarkView", {
				name: t.name,
				options: t.options,
				storage: this.editor.extensionStorage[t.name],
				editor: e,
				type: Bc(t.name, this.schema)
			});
			return r ? [t.name, (i, a, o) => {
				let s = Jl(i, n);
				return r()({
					mark: i,
					view: a,
					inline: o,
					editor: e,
					extension: t,
					HTMLAttributes: s,
					updateAttributes: (t) => {
						zd(i, e, t);
					}
				});
			}] : [];
		}));
	}
	destroy() {
		var e;
		(e = this.decorationManager) == null || e.destroy(), this.extensions.forEach((e) => {
			let t = e;
			for (; t.parent;) {
				let e = t.parent;
				e.child === t && (e.child = null), t = e;
			}
		}), this.extensions = [], this.baseExtensions = [], this.decorationManager = null, this.schema = null, this.editor = null;
	}
	setupExtensions() {
		let e = this.extensions;
		this.editor.extensionStorage = Object.fromEntries(e.map((e) => [e.name, e.storage])), e.forEach((e) => {
			let t = {
				name: e.name,
				options: e.options,
				storage: this.editor.extensionStorage[e.name],
				editor: this.editor,
				type: hu(e.name, this.schema)
			};
			e.type === "mark" && ((P(N(e, "keepOnSplit", t)) ?? !0) && this.splittableMarks.push(e.name), (P(N(e, "clearable", t)) ?? !0) || this.nonClearableMarks.push(e.name));
			let n = N(e, "onBeforeCreate", t), r = N(e, "onCreate", t), i = N(e, "onUpdate", t), a = N(e, "onSelectionUpdate", t), o = N(e, "onTransaction", t), s = N(e, "onFocus", t), c = N(e, "onBlur", t), l = N(e, "onDestroy", t);
			n && this.editor.on("beforeCreate", n), r && this.editor.on("create", r), i && this.editor.on("update", i), a && this.editor.on("selectionUpdate", a), o && this.editor.on("transaction", o), s && this.editor.on("focus", s), c && this.editor.on("blur", c), l && this.editor.on("destroy", l);
		});
	}
};
Qd.resolve = nu, Qd.sort = tu, Qd.flatten = Bl;
var I = class e extends Wd {
	constructor(...e) {
		super(...e), this.type = "extension";
	}
	static create(t = {}) {
		let n = typeof t == "function" ? t() : t;
		return new e(n);
	}
	configure(e) {
		return super.configure(e);
	}
	extend(e) {
		let t = typeof e == "function" ? e() : e;
		return super.extend(t);
	}
}, $d = I.create({
	name: "clipboardTextSerializer",
	addOptions() {
		return { blockSeparator: void 0 };
	},
	addProseMirrorPlugins() {
		return [new E({
			key: new D("clipboardTextSerializer"),
			props: { clipboardTextSerializer: () => {
				let { editor: e } = this, { state: t, schema: n } = e, { doc: r, selection: i } = t, a = ou(n), { blockSeparator: o } = this.options, s = {
					...o === void 0 ? {} : { blockSeparator: o },
					textSerializers: a
				};
				return [...i.ranges].sort((e, t) => e.$from.pos - t.$from.pos).map(({ $from: e, $to: t }) => iu(r, {
					from: e.pos,
					to: t.pos
				}, s)).join(o ?? "\n\n");
			} }
		})];
	}
}), ef = I.create({
	name: "commands",
	addCommands() {
		return { ...id };
	}
}), tf = I.create({
	name: "delete",
	onUpdate({ transaction: e, appendedTransactions: t }) {
		var n;
		let r = () => {
			var n, r;
			if (((n = this.editor.options.coreExtensionOptions) == null || (n = n.delete) == null || (r = n.filterTransaction) == null ? void 0 : r.call(n, e)) ?? e.getMeta("y-sync$")) return;
			let i = Il(e.before, [e, ...t]);
			du(i).forEach((t) => {
				i.mapping.mapResult(t.oldRange.from).deletedAfter && i.mapping.mapResult(t.oldRange.to).deletedBefore && i.before.nodesBetween(t.oldRange.from, t.oldRange.to, (n, r) => {
					let a = r + n.nodeSize - 2, o = t.oldRange.from <= r && a <= t.oldRange.to;
					this.editor.emit("delete", {
						type: "node",
						node: n,
						from: r,
						to: a,
						newFrom: i.mapping.map(r),
						newTo: i.mapping.map(a),
						deletedRange: t.oldRange,
						newRange: t.newRange,
						partial: !o,
						editor: this.editor,
						transaction: e,
						combinedTransform: i
					});
				});
			});
			let a = i.mapping;
			i.steps.forEach((t, n) => {
				if (t instanceof wt) {
					let r = a.slice(n).map(t.from, -1), o = a.slice(n).map(t.to), s = a.invert().map(r, -1), c = a.invert().map(o), l = r > 0 && i.doc.nodeAt(r - 1)?.marks.some((e) => e.eq(t.mark)), u = i.doc.nodeAt(o)?.marks.some((e) => e.eq(t.mark));
					this.editor.emit("delete", {
						type: "mark",
						mark: t.mark,
						from: t.from,
						to: t.to,
						deletedRange: {
							from: s,
							to: c
						},
						newRange: {
							from: r,
							to: o
						},
						partial: !!(u || l),
						editor: this.editor,
						transaction: e,
						combinedTransform: i
					});
				}
			});
		};
		((n = this.editor.options.coreExtensionOptions) == null || (n = n.delete) == null ? void 0 : n.async) ?? !0 ? setTimeout(r, 0) : r();
	}
}), nf = I.create({
	name: "drop",
	addProseMirrorPlugins() {
		return [new E({
			key: new D("tiptapDrop"),
			props: { handleDrop: (e, t, n, r) => {
				this.editor.emit("drop", {
					editor: this.editor,
					event: t,
					slice: n,
					moved: r
				});
			} }
		})];
	}
}), rf = I.create({
	name: "editable",
	addProseMirrorPlugins() {
		return [new E({
			key: new D("editable"),
			props: { editable: () => this.editor.options.editable }
		})];
	}
}), af = new D("focusEvents"), of = I.create({
	name: "focusEvents",
	addProseMirrorPlugins() {
		let { editor: e } = this;
		return [new E({
			key: af,
			props: { handleDOMEvents: {
				focus: (t, n) => {
					e.isFocused = !0;
					let r = e.state.tr.setMeta("focus", { event: n }).setMeta("addToHistory", !1);
					return t.dispatch(r), !1;
				},
				blur: (t, n) => {
					e.isFocused = !1;
					let r = e.state.tr.setMeta("blur", { event: n }).setMeta("addToHistory", !1);
					return t.dispatch(r), !1;
				}
			} }
		})];
	}
}), sf = I.create({
	name: "keymap",
	addKeyboardShortcuts() {
		let e = () => this.editor.commands.first(({ commands: e }) => [
			() => e.undoInputRule(),
			() => e.command(({ tr: t }) => {
				let { selection: n, doc: r } = t, { empty: i, $anchor: a } = n, { pos: o, parent: s } = a, c = a.parent.isTextblock && o > 0 ? t.doc.resolve(o - 1) : a, l = c.parent.type.spec.isolating, u = a.pos - a.parentOffset, d = l && c.parent.childCount === 1 ? u === a.pos : C.atStart(r).from === o;
				return !i || !s.type.isTextblock || s.textContent.length || !d || d && a.parent.type.name === "paragraph" ? !1 : e.clearNodes();
			}),
			() => e.deleteSelection(),
			() => e.joinBackward(),
			() => e.selectNodeBackward()
		]), t = () => this.editor.commands.first(({ commands: e }) => [
			() => e.deleteSelection(),
			() => e.deleteCurrentNode(),
			() => e.joinForward(),
			() => e.selectNodeForward()
		]), n = {
			Enter: () => this.editor.commands.first(({ commands: e }) => [
				() => e.newlineInCode(),
				() => e.createParagraphNear(),
				() => e.liftEmptyBlock(),
				() => e.splitBlock()
			]),
			"Mod-Enter": () => this.editor.commands.exitCode(),
			Backspace: e,
			"Mod-Backspace": e,
			"Shift-Backspace": e,
			Delete: t,
			"Mod-Delete": t,
			"Mod-a": () => this.editor.commands.selectAll()
		}, r = { ...n }, i = {
			...n,
			"Ctrl-h": e,
			"Alt-Backspace": e,
			"Ctrl-d": t,
			"Ctrl-Alt-Backspace": t,
			"Alt-Delete": t,
			"Alt-d": t,
			"Ctrl-a": () => this.editor.commands.selectTextblockStart(),
			"Ctrl-e": () => this.editor.commands.selectTextblockEnd()
		};
		return qc() || hl() ? i : r;
	},
	addProseMirrorPlugins() {
		return [new E({
			key: new D("clearDocument"),
			appendTransaction: (e, t, n) => {
				if (e.some((e) => e.getMeta("composition"))) return;
				let r = e.some((e) => e.docChanged) && !t.doc.eq(n.doc), i = e.some((e) => e.getMeta("preventClearDocument"));
				if (!r || i) return;
				let { empty: a, from: o, to: s } = t.selection, c = C.atStart(t.doc).from, l = C.atEnd(t.doc).to;
				if (a || o !== c || s !== l || !wu(n.doc)) return;
				let u = n.tr, d = vc({
					state: n,
					transaction: u
				}), { commands: f } = new yc({
					editor: this.editor,
					state: d
				});
				if (f.clearNodes(), u.steps.length) return u;
			}
		})];
	}
}), cf = I.create({
	name: "paste",
	addProseMirrorPlugins() {
		return [new E({
			key: new D("tiptapPaste"),
			props: { handlePaste: (e, t, n) => {
				this.editor.emit("paste", {
					editor: this.editor,
					event: t,
					slice: n
				});
			} }
		})];
	}
}), lf = I.create({
	name: "tabindex",
	addOptions() {
		return { value: void 0 };
	},
	addProseMirrorPlugins() {
		return [new E({
			key: new D("tabindex"),
			props: { attributes: () => !this.editor.isEditable && this.options.value === void 0 ? {} : { tabindex: this.options.value ?? "0" } }
		})];
	}
}), uf = I.create({
	name: "textDirection",
	addOptions() {
		return { direction: void 0 };
	},
	addGlobalAttributes() {
		if (!this.options.direction) return [];
		let { nodeExtensions: e } = Wl(this.extensions);
		return [{
			types: e.filter((e) => e.name !== "text").map((e) => e.name),
			attributes: { dir: {
				default: this.options.direction,
				parseHTML: (e) => {
					let t = e.getAttribute("dir");
					return t && (t === "ltr" || t === "rtl" || t === "auto") ? t : this.options.direction;
				},
				renderHTML: (e) => e.dir ? { dir: e.dir } : {}
			} }
		}];
	},
	addProseMirrorPlugins() {
		return [new E({
			key: new D("textDirection"),
			props: { attributes: () => {
				let e = this.options.direction;
				return e ? { dir: e } : {};
			} }
		})];
	}
}), df = !1;
function ff(e) {
	if (df) return;
	df = !0;
	let t;
	try {
		t = Dt.fromJSON(e, {
			from: 0,
			to: 0
		}).slice.content;
	} catch {
		return;
	}
	t instanceof l || console.warn("[tiptap warn]: prosemirror-model is loaded more than once. Wrapping and splitting nodes will fail. Deduplicate it in your lock file, or alias it to a single copy in your bundler.");
}
var pf = class e {
	get name() {
		return this.node.type.name;
	}
	constructor(e, t, n = !1, r = null) {
		this.currentNode = null, this.actualDepth = null, this.isBlock = n, this.resolvedPos = e, this.editor = t, this.currentNode = r;
	}
	get node() {
		return this.currentNode || this.resolvedPos.node();
	}
	get element() {
		return this.editor.view.domAtPos(this.pos).node;
	}
	get depth() {
		return this.actualDepth ?? this.resolvedPos.depth;
	}
	get pos() {
		return this.resolvedPos.pos;
	}
	get content() {
		return this.node.content;
	}
	set content(e) {
		let t = this.from, n = this.to;
		if (this.isBlock) {
			if (this.content.size === 0) {
				console.error(`You can’t set content on a block node. Tried to set content on ${this.name} at ${this.pos}`);
				return;
			}
			t = this.from + 1, n = this.to - 1;
		}
		this.editor.commands.insertContentAt({
			from: t,
			to: n
		}, e);
	}
	get attributes() {
		return this.node.attrs;
	}
	get textContent() {
		return this.node.textContent;
	}
	get size() {
		return this.node.nodeSize;
	}
	get from() {
		return this.isBlock ? this.pos : this.resolvedPos.start(this.resolvedPos.depth);
	}
	get range() {
		return {
			from: this.from,
			to: this.to
		};
	}
	get to() {
		return this.isBlock ? this.pos + this.size : this.resolvedPos.end(this.resolvedPos.depth) + +!this.node.isText;
	}
	get parent() {
		if (this.depth === 0) return null;
		let t = this.resolvedPos.start(this.resolvedPos.depth - 1), n = this.resolvedPos.doc.resolve(t);
		return new e(n, this.editor);
	}
	get before() {
		let t = this.resolvedPos.doc.resolve(this.from - (this.isBlock ? 1 : 2));
		return t.depth !== this.depth && (t = this.resolvedPos.doc.resolve(this.from - 3)), new e(t, this.editor);
	}
	get after() {
		let t = this.resolvedPos.doc.resolve(this.to + (this.isBlock ? 2 : 1));
		return t.depth !== this.depth && (t = this.resolvedPos.doc.resolve(this.to + 3)), new e(t, this.editor);
	}
	get children() {
		let t = [];
		return this.node.content.forEach((n, r) => {
			let i = n.isBlock && !n.isTextblock, a = n.isAtom && !n.isText, o = n.isInline, s = this.pos + r + +!a;
			if (s < 0 || s > this.resolvedPos.doc.nodeSize - 2) return;
			let c = this.resolvedPos.doc.resolve(s);
			if (!i && !o && c.depth <= this.depth) return;
			let l = new e(c, this.editor, i, i || o ? n : null);
			i && (l.actualDepth = this.depth + 1), t.push(l);
		}), t;
	}
	get firstChild() {
		return this.children[0] || null;
	}
	get lastChild() {
		let e = this.children;
		return e[e.length - 1] || null;
	}
	closest(e, t = {}) {
		let n = null, r = this.parent;
		for (; r && !n;) {
			if (r.node.type.name === e) {
				if (Object.keys(t).length > 0) {
					let e = r.node.attrs, n = Object.keys(t);
					for (let r = 0; r < n.length; r += 1) {
						let i = n[r];
						if (e[i] !== t[i]) break;
					}
				} else n = r;
			}
			r = r.parent;
		}
		return n;
	}
	querySelector(e, t = {}) {
		return this.querySelectorAll(e, t, !0)[0] || null;
	}
	querySelectorAll(e, t = {}, n = !1) {
		let r = [];
		if (!this.children || this.children.length === 0) return r;
		let i = Object.keys(t);
		return this.children.forEach((a) => {
			n && r.length > 0 || (a.node.type.name === e && i.every((e) => t[e] === a.node.attrs[e]) && r.push(a), !(n && r.length > 0) && (r = r.concat(a.querySelectorAll(e, t, n))));
		}), r;
	}
	setAttribute(e) {
		let { tr: t } = this.editor.state;
		t.setNodeMarkup(this.from, void 0, {
			...this.node.attrs,
			...e
		}), this.editor.view.dispatch(t);
	}
}, mf = ".ProseMirror {\n  position: relative;\n}\n\n.ProseMirror {\n  word-wrap: break-word;\n  white-space: pre-wrap;\n  white-space: break-spaces;\n  -webkit-font-variant-ligatures: none;\n  font-variant-ligatures: none;\n  font-feature-settings: \"liga\" 0; /* the above doesn't seem to work in Edge */\n}\n\n.ProseMirror [contenteditable=\"false\"] {\n  white-space: normal;\n}\n\n.ProseMirror [contenteditable=\"false\"] [contenteditable=\"true\"] {\n  white-space: pre-wrap;\n}\n\n.ProseMirror pre {\n  white-space: pre-wrap;\n}\n\nimg.ProseMirror-separator {\n  display: inline !important;\n  border: none !important;\n  margin: 0 !important;\n  width: 0 !important;\n  height: 0 !important;\n}\n\n.ProseMirror-gapcursor {\n  display: none;\n  pointer-events: none;\n  position: absolute;\n  margin: 0;\n}\n\n.ProseMirror-gapcursor:after {\n  content: \"\";\n  display: block;\n  position: absolute;\n  top: -2px;\n  width: 20px;\n  border-top: 1px solid black;\n  animation: ProseMirror-cursor-blink 1.1s steps(2, start) infinite;\n}\n\n@keyframes ProseMirror-cursor-blink {\n  to {\n    visibility: hidden;\n  }\n}\n\n.ProseMirror-hideselection *::selection {\n  background: transparent;\n}\n\n.ProseMirror-hideselection *::-moz-selection {\n  background: transparent;\n}\n\n.ProseMirror-hideselection * {\n  caret-color: transparent;\n}\n\n.ProseMirror-focused .ProseMirror-gapcursor {\n  display: block;\n}", hf = class extends cd {
	constructor(e = {}) {
		super(), this.css = null, this.className = "tiptap", this.editorView = null, this.isFocused = !1, this.destroyed = !1, this.isInitialized = !1, this.extensionStorage = {}, this.instanceId = Math.random().toString(36).slice(2, 9), this.hasWarnedStaleDecorationRead = !1, this.options = {
			element: typeof document < "u" ? document.createElement("div") : null,
			content: "",
			injectCSS: !0,
			injectNonce: void 0,
			extensions: [],
			autofocus: !1,
			editable: !0,
			textDirection: void 0,
			editorProps: {},
			parseOptions: {},
			coreExtensionOptions: {},
			enableInputRules: !0,
			enablePasteRules: !0,
			enableCoreExtensions: !0,
			enableContentCheck: !1,
			emitContentError: !1,
			onBeforeCreate: () => null,
			onCreate: () => null,
			onMount: () => null,
			onUnmount: () => null,
			onUpdate: () => null,
			onSelectionUpdate: () => null,
			onTransaction: () => null,
			onFocus: () => null,
			onBlur: () => null,
			onDestroy: () => null,
			onContentError: ({ error: e }) => {
				throw e;
			},
			onPaste: () => null,
			onDrop: () => null,
			onDelete: () => null,
			enableExtensionDispatchTransaction: !0
		}, this.isCapturingTransaction = !1, this.capturedTransaction = null, this.utils = {
			getUpdatedPosition: Du,
			createMappablePosition: Ou
		}, this.setOptions(e), this.createExtensionManager(), this.createCommandManager(), this.createSchema(), this.on("beforeCreate", this.options.onBeforeCreate), this.emit("beforeCreate", { editor: this }), this.on("mount", this.options.onMount), this.on("unmount", this.options.onUnmount), this.on("contentError", this.options.onContentError), this.on("create", this.options.onCreate), this.on("update", this.options.onUpdate), this.on("selectionUpdate", this.options.onSelectionUpdate), this.on("transaction", this.options.onTransaction), this.on("focus", this.options.onFocus), this.on("blur", this.options.onBlur), this.on("destroy", this.options.onDestroy), this.on("drop", ({ event: e, slice: t, moved: n }) => this.options.onDrop(e, t, n)), this.on("paste", ({ event: e, slice: t }) => this.options.onPaste(e, t)), this.on("delete", this.options.onDelete);
		let t = this.createDoc();
		if (!this.editorState) {
			let e = Gc(t, this.options.autofocus);
			this.editorState = Bn.create({
				doc: t,
				schema: this.schema,
				selection: e || void 0
			});
		}
		ff(this.schema), this.options.element && this.mount(this.options.element);
	}
	mount(e) {
		if (typeof document > "u") throw Error("[tiptap error]: The editor cannot be mounted because there is no 'document' defined in this environment.");
		this.createView(e), this.emit("mount", { editor: this }), this.css && !document.head.contains(this.css) && document.head.appendChild(this.css), window.setTimeout(() => {
			this.isDestroyed || (this.options.autofocus !== !1 && this.options.autofocus !== null && this.commands.focus(this.options.autofocus), this.emit("create", { editor: this }), this.isInitialized = !0);
		}, 0);
	}
	unmount() {
		if (this.editorView) {
			this.editorState = this.editorView.state;
			let e = this.editorView.dom;
			e?.editor && delete e.editor, this.editorView.destroy();
		}
		if (this.editorView = null, this.isInitialized = !1, this.css && !document.querySelectorAll(`.${this.className}`).length) try {
			typeof this.css.remove == "function" ? this.css.remove() : this.css.parentNode && this.css.parentNode.removeChild(this.css);
		} catch (e) {
			console.warn("Failed to remove CSS element:", e);
		}
		this.css = null, this.emit("unmount", { editor: this });
	}
	get storage() {
		return this.extensionStorage;
	}
	get commands() {
		return this.commandManager.commands;
	}
	chain() {
		return this.commandManager ? this.commandManager.chain() : yc.createFakeChain();
	}
	can() {
		return this.commandManager ? this.commandManager.can() : yc.createFallbackCan();
	}
	injectCSS() {
		this.options.injectCSS && typeof document < "u" && (this.css = Ad(mf, this.options.injectNonce));
	}
	setOptions(e = {}) {
		this.options = {
			...this.options,
			...e
		}, this.editorView && this.state && !this.isDestroyed && (this.options.editorProps && this.view.setProps(this.options.editorProps), this.view.updateState(this.state));
	}
	setEditable(e, t = !0) {
		this.setOptions({ editable: e }), t && this.emit("update", {
			editor: this,
			transaction: this.state.tr,
			appendedTransactions: []
		});
	}
	get isEditable() {
		return this.options.editable && this.view && this.view.editable;
	}
	get view() {
		return this.editorView ? this.editorView : new Proxy({
			state: this.editorState,
			updateState: (e) => {
				this.editorState = e;
			},
			dispatch: (e) => {
				this.dispatchTransaction(e);
			},
			composing: !1,
			dragging: null,
			editable: !0,
			isDestroyed: !1
		}, { get: (e, t) => {
			if (this.editorView) return this.editorView[t];
			if (t === "state") return this.editorState;
			if (t in e) return Reflect.get(e, t);
			throw Error(`[tiptap error]: The editor view is not available. Cannot access view['${t}']. The editor may not be mounted yet.`);
		} });
	}
	get state() {
		return ld && !this.hasWarnedStaleDecorationRead && sd(this) && (this.hasWarnedStaleDecorationRead = !0, console.warn("[tiptap warn]: `editor.state` was read while decoration `create()` was running. It returns the pre-transaction document. Use the `state` argument passed to `create()` instead. Helpers like `editor.isActive()` read `editor.state` too, so pass `state` to their standalone versions instead of calling them on the editor.")), this.editorView && (this.editorState = this.view.state), this.editorState;
	}
	registerPlugin(e, t) {
		let n = Hl(t) ? t(e, [...this.state.plugins]) : [...this.state.plugins, e], r = this.state.reconfigure({ plugins: n });
		return this.view.updateState(r), r;
	}
	unregisterPlugin(e) {
		if (this.isDestroyed) return;
		let t = this.state.plugins, n = t;
		if ([].concat(e).forEach((e) => {
			let t = typeof e == "string" ? `${e}$` : e.key;
			n = n.filter((e) => !e.key.startsWith(t));
		}), t.length === n.length) return;
		let r = this.state.reconfigure({ plugins: n });
		return this.view.updateState(r), r;
	}
	createExtensionManager() {
		var e, t;
		let n = [...this.options.enableCoreExtensions ? [
			rf,
			$d.configure({ blockSeparator: (e = this.options.coreExtensionOptions) == null || (e = e.clipboardTextSerializer) == null ? void 0 : e.blockSeparator }),
			ef,
			of,
			sf,
			lf.configure({ value: (t = this.options.coreExtensionOptions) == null || (t = t.tabindex) == null ? void 0 : t.value }),
			nf,
			cf,
			tf,
			uf.configure({ direction: this.options.textDirection })
		].filter((e) => typeof this.options.enableCoreExtensions != "object" || this.options.enableCoreExtensions[e.name] !== !1) : [], ...this.options.extensions].filter((e) => [
			"extension",
			"node",
			"mark"
		].includes(e?.type));
		this.extensionManager = new Qd(n, this);
	}
	createCommandManager() {
		this.commandManager = new yc({ editor: this });
	}
	createSchema() {
		this.schema = this.extensionManager.schema;
	}
	createDoc() {
		let e;
		try {
			e = Nl(this.options.content, this.schema, this.options.parseOptions, { errorOnInvalidContent: this.options.enableContentCheck });
		} catch (e) {
			if (!(e instanceof Error) || !["[tiptap error]: Invalid JSON content", "[tiptap error]: Invalid HTML content"].includes(e.message)) throw e;
			let t = Nl(this.options.content, this.schema, this.options.parseOptions, { errorOnInvalidContent: !1 });
			return this.editorState = Bn.create({
				doc: t,
				schema: this.schema,
				selection: Gc(t, this.options.autofocus) || void 0
			}), this.emit("contentError", {
				editor: this,
				error: e,
				disableCollaboration: () => {
					"collaboration" in this.storage && typeof this.storage.collaboration == "object" && this.storage.collaboration && (this.storage.collaboration.isDisabled = !0), this.options.extensions = this.options.extensions.filter((e) => e.name !== "collaboration"), this.createExtensionManager();
				}
			}), this.editorState.doc;
		}
		return e;
	}
	createView(e) {
		let { editorProps: t, enableExtensionDispatchTransaction: n } = this.options, r = t.dispatchTransaction || this.dispatchTransaction.bind(this), i = n ? this.extensionManager.dispatchTransaction(r) : r, a = t.transformPastedHTML, o = this.extensionManager.transformPastedHTML(a);
		this.editorView = new Xs(e, {
			...t,
			attributes: {
				role: "textbox",
				...t?.attributes
			},
			dispatchTransaction: i,
			transformPastedHTML: o,
			state: this.editorState,
			markViews: this.extensionManager.markViews,
			nodeViews: this.extensionManager.nodeViews
		});
		let s = this.state.reconfigure({ plugins: this.extensionManager.plugins });
		this.view.updateState(s), this.prependClass(), this.injectCSS();
		let c = this.view.dom;
		c.editor = this;
	}
	createNodeViews() {
		this.view.isDestroyed || this.view.setProps({
			markViews: this.extensionManager.markViews,
			nodeViews: this.extensionManager.nodeViews
		});
	}
	prependClass() {
		this.view.dom.className = `${this.className} ${this.view.dom.className}`;
	}
	captureTransaction(e) {
		this.isCapturingTransaction = !0, e(), this.isCapturingTransaction = !1;
		let t = this.capturedTransaction;
		return this.capturedTransaction = null, t;
	}
	dispatchTransaction(e) {
		if (this.view.isDestroyed) return;
		if (this.isCapturingTransaction) {
			if (!this.capturedTransaction) {
				this.capturedTransaction = e;
				return;
			}
			e.steps.forEach((e) => this.capturedTransaction?.step(e));
			return;
		}
		let { state: t, transactions: n } = this.state.applyTransaction(e), r = !this.state.selection.eq(t.selection), i = n.includes(e), a = this.state;
		if (this.emit("beforeTransaction", {
			editor: this,
			transaction: e,
			nextState: t
		}), !i) return;
		this.view.updateState(t), this.emit("transaction", {
			editor: this,
			transaction: e,
			appendedTransactions: n.slice(1)
		}), r && this.emit("selectionUpdate", {
			editor: this,
			transaction: e
		});
		let o = n.findLast((e) => e.getMeta("focus") || e.getMeta("blur")), s = o?.getMeta("focus"), c = o?.getMeta("blur");
		s && this.emit("focus", {
			editor: this,
			event: s.event,
			transaction: o
		}), c && this.emit("blur", {
			editor: this,
			event: c.event,
			transaction: o
		}), !(e.getMeta("preventUpdate") || !n.some((e) => e.docChanged) || a.doc.eq(t.doc)) && this.emit("update", {
			editor: this,
			transaction: e,
			appendedTransactions: n.slice(1)
		});
	}
	getAttributes(e) {
		return cu(this.state, e);
	}
	isActive(e, t) {
		let n = typeof e == "string" ? e : null, r = typeof e == "string" ? t : e;
		return yu(this.state, n, r);
	}
	getJSON() {
		return this.state.doc.toJSON();
	}
	getHTML() {
		return Vl(this.state.doc.content, this.schema);
	}
	getText(e) {
		let { blockSeparator: t = "\n\n", textSerializers: n = {} } = e || {};
		return au(this.state.doc, {
			blockSeparator: t,
			textSerializers: {
				...ou(this.schema),
				...n
			}
		});
	}
	get isEmpty() {
		return wu(this.state.doc);
	}
	destroy() {
		this.destroyed || (this.destroyed = !0, this.emit("destroy"), this.unmount(), this.removeAllListeners(), this.extensionManager.destroy(), this.extensionManager = null, this.schema = null, this.commandManager = null, this.extensionStorage = {});
	}
	get isDestroyed() {
		return this.editorView?.isDestroyed ?? !0;
	}
	$node(e, t) {
		return this.$doc?.querySelector(e, t) || null;
	}
	$nodes(e, t) {
		return this.$doc?.querySelectorAll(e, t) || null;
	}
	$pos(e) {
		let t = this.state.doc.resolve(e), n = e > 0 && t.nodeAfter && !t.nodeAfter.isText && t.nodeAfter.isAtom ? t.nodeAfter : null;
		return new pf(t, this, !1, n);
	}
	get $doc() {
		return this.$pos(0);
	}
};
function gf(e) {
	return new Bd({
		find: e.find,
		handler: ({ state: t, range: n, match: r }) => {
			let i = P(e.getAttributes, void 0, r);
			if (i === !1 || i === null) return null;
			let { tr: a } = t, o = r[r.length - 1], s = r[0];
			if (o) {
				let r = s.search(/\S/), c = n.from + s.indexOf(o), l = c + o.length;
				if (fu(n.from, n.to, t.doc).filter((t) => t.mark.type.excluded.find((n) => n === e.type && n !== t.mark.type)).filter((e) => e.to > c).length) return null;
				l < n.to && a.delete(l, n.to), c > n.from && a.delete(n.from + r, c);
				let u = n.from + r + o.length;
				a.addMark(n.from + r, u, e.type.create(i || {})), a.removeStoredMark(e.type);
			}
		},
		undoable: e.undoable
	});
}
function _f(e) {
	return new Bd({
		find: e.find,
		handler: ({ state: t, range: n, match: r }) => {
			let i = P(e.getAttributes, void 0, r) || {}, { tr: a } = t, o = n.from, s = n.to, c = e.type.create(i);
			if (r[1]) {
				let e = o + r[0].lastIndexOf(r[1]);
				e > s ? e = s : s = e + r[1].length;
				let t = r[0][r[0].length - 1];
				a.insertText(t, o + r[0].length - 1), a.replaceWith(e, s, c);
			} else if (r[0]) {
				let t = e.type.isInline ? o : o - 1;
				a.insert(t, e.type.create(i)).delete(a.mapping.map(o), a.mapping.map(s));
			}
			a.scrollIntoView();
		},
		undoable: e.undoable
	});
}
function vf(e) {
	return new Bd({
		find: e.find,
		handler: ({ state: t, range: n, match: r }) => {
			let i = t.doc.resolve(n.from), a = P(e.getAttributes, void 0, r) || {};
			if (!i.node(-1).canReplaceWith(i.index(-1), i.indexAfter(-1), e.type)) return null;
			t.tr.delete(n.from, n.to).setBlockType(n.from, n.from, e.type, a);
		},
		undoable: e.undoable
	});
}
function yf(e) {
	return new Bd({
		find: e.find,
		handler: ({ state: t, range: n, match: r, chain: i }) => {
			let a = P(e.getAttributes, void 0, r) || {}, o = t.tr.delete(n.from, n.to), s = o.doc.resolve(n.from).blockRange(), c = s && It(s, e.type, a);
			if (!c) return null;
			if (o.wrap(s, c), e.keepMarks && e.editor) {
				let { selection: n, storedMarks: r } = t, { splittableMarks: i } = e.editor.extensionManager, a = r || n.$to.parentOffset && n.$from.marks();
				if (a) {
					let e = a.filter((e) => i.includes(e.type.name));
					o.ensureMarks(e);
				}
			}
			if (e.keepAttributes) {
				let t = e.type.name === "bulletList" || e.type.name === "orderedList" ? "listItem" : "taskList";
				i().updateAttributes(t, a).run();
			}
			let l = o.doc.resolve(n.from - 1).nodeBefore;
			l && l.type === e.type && Jt(o.doc, n.from - 1) && (!e.joinPredicate || e.joinPredicate(r, l)) && o.join(n.from - 1);
		},
		undoable: e.undoable
	});
}
var bf = (e) => "touches" in e, xf = class {
	constructor(e) {
		var t;
		this.directions = [
			"bottom-left",
			"bottom-right",
			"top-left",
			"top-right"
		], this.minSize = {
			height: 8,
			width: 8
		}, this.preserveAspectRatio = !1, this.classNames = {
			container: "",
			wrapper: "",
			handle: "",
			resizing: ""
		}, this.initialWidth = 0, this.initialHeight = 0, this.aspectRatio = 1, this.isResizing = !1, this.activeHandle = null, this.startX = 0, this.startY = 0, this.startWidth = 0, this.startHeight = 0, this.isShiftKeyPressed = !1, this.lastEditableState = void 0, this.handleMap = /* @__PURE__ */ new Map(), this.handleMouseMove = (e) => {
			if (!this.isResizing || !this.activeHandle) return;
			let t = e.clientX - this.startX, n = e.clientY - this.startY;
			this.handleResize(t, n);
		}, this.handleTouchMove = (e) => {
			if (!this.isResizing || !this.activeHandle) return;
			let t = e.touches[0];
			if (!t) return;
			let n = t.clientX - this.startX, r = t.clientY - this.startY;
			this.handleResize(n, r);
		}, this.handleMouseUp = () => {
			if (!this.isResizing) return;
			let e = this.element.offsetWidth, t = this.element.offsetHeight;
			this.onCommit(e, t), this.isResizing = !1, this.activeHandle = null, this.container.dataset.resizeState = "false", this.classNames.resizing && this.container.classList.remove(this.classNames.resizing), document.removeEventListener("mousemove", this.handleMouseMove), document.removeEventListener("mouseup", this.handleMouseUp), document.removeEventListener("keydown", this.handleKeyDown), document.removeEventListener("keyup", this.handleKeyUp);
		}, this.handleKeyDown = (e) => {
			e.key === "Shift" && (this.isShiftKeyPressed = !0);
		}, this.handleKeyUp = (e) => {
			e.key === "Shift" && (this.isShiftKeyPressed = !1);
		}, this.node = e.node, this.editor = e.editor, this.element = e.element, this.element.draggable = !1, this.contentElement = e.contentElement, this.getPos = e.getPos, this.onResize = e.onResize, this.onCommit = e.onCommit, this.onUpdate = e.onUpdate, e.options?.min && (this.minSize = {
			...this.minSize,
			...e.options.min
		}), e.options?.max && (this.maxSize = e.options.max), e != null && (t = e.options) != null && t.directions && (this.directions = e.options.directions), e.options?.preserveAspectRatio && (this.preserveAspectRatio = e.options.preserveAspectRatio), e.options?.className && (this.classNames = {
			container: e.options.className.container || "",
			wrapper: e.options.className.wrapper || "",
			handle: e.options.className.handle || "",
			resizing: e.options.className.resizing || ""
		}), e.options?.createCustomHandle && (this.createCustomHandle = e.options.createCustomHandle), this.wrapper = this.createWrapper(), this.container = this.createContainer(), this.applyInitialSize(), this.attachHandles(), this.editor.on("update", this.handleEditorUpdate.bind(this));
	}
	get dom() {
		return this.container;
	}
	get contentDOM() {
		return this.contentElement ?? null;
	}
	handleEditorUpdate() {
		let e = this.editor.isEditable;
		e !== this.lastEditableState && (this.lastEditableState = e, e ? e && this.handleMap.size === 0 && this.attachHandles() : this.removeHandles());
	}
	update(e, t, n) {
		return e.type === this.node.type && (this.node = e, !this.onUpdate || this.onUpdate(e, t, n));
	}
	destroy() {
		this.isResizing && (this.container.dataset.resizeState = "false", this.classNames.resizing && this.container.classList.remove(this.classNames.resizing), document.removeEventListener("mousemove", this.handleMouseMove), document.removeEventListener("mouseup", this.handleMouseUp), document.removeEventListener("keydown", this.handleKeyDown), document.removeEventListener("keyup", this.handleKeyUp), this.isResizing = !1, this.activeHandle = null), this.editor.off("update", this.handleEditorUpdate.bind(this)), this.container.remove();
	}
	createContainer() {
		let e = document.createElement("div");
		return e.dataset.resizeContainer = "", e.dataset.node = this.node.type.name, e.style.display = this.node.type.isInline ? "inline-flex" : "flex", this.classNames.container && (e.className = this.classNames.container), e.appendChild(this.wrapper), e;
	}
	createWrapper() {
		let e = document.createElement("div");
		return e.style.position = "relative", e.style.display = "block", e.dataset.resizeWrapper = "", this.classNames.wrapper && (e.className = this.classNames.wrapper), e.appendChild(this.element), e;
	}
	createHandle(e) {
		let t = document.createElement("div");
		return t.dataset.resizeHandle = e, t.style.position = "absolute", this.classNames.handle && (t.className = this.classNames.handle), t;
	}
	positionHandle(e, t) {
		let n = t.includes("top"), r = t.includes("bottom"), i = t.includes("left"), a = t.includes("right");
		n && (e.style.top = "0"), r && (e.style.bottom = "0"), i && (e.style.left = "0"), a && (e.style.right = "0"), (t === "top" || t === "bottom") && (e.style.left = "0", e.style.right = "0"), (t === "left" || t === "right") && (e.style.top = "0", e.style.bottom = "0");
	}
	attachHandles() {
		this.directions.forEach((e) => {
			let t;
			t = this.createCustomHandle ? this.createCustomHandle(e) : this.createHandle(e), t instanceof HTMLElement || (console.warn(`[ResizableNodeView] createCustomHandle("${e}") did not return an HTMLElement. Falling back to default handle.`), t = this.createHandle(e)), this.createCustomHandle || this.positionHandle(t, e), t.addEventListener("mousedown", (t) => this.handleResizeStart(t, e)), t.addEventListener("touchstart", (t) => this.handleResizeStart(t, e)), this.handleMap.set(e, t), this.wrapper.appendChild(t);
		});
	}
	removeHandles() {
		this.handleMap.forEach((e) => e.remove()), this.handleMap.clear();
	}
	applyInitialSize() {
		let e = this.node.attrs.width, t = this.node.attrs.height;
		e ? (this.element.style.width = `${e}px`, this.initialWidth = e) : this.initialWidth = this.element.offsetWidth, t ? (this.element.style.height = `${t}px`, this.initialHeight = t) : this.initialHeight = this.element.offsetHeight, this.initialWidth > 0 && this.initialHeight > 0 && (this.aspectRatio = this.initialWidth / this.initialHeight);
	}
	handleResizeStart(e, t) {
		e.preventDefault(), e.stopPropagation(), this.isResizing = !0, this.activeHandle = t, bf(e) ? (this.startX = e.touches[0].clientX, this.startY = e.touches[0].clientY) : (this.startX = e.clientX, this.startY = e.clientY), this.startWidth = this.element.offsetWidth, this.startHeight = this.element.offsetHeight, this.startWidth > 0 && this.startHeight > 0 && (this.aspectRatio = this.startWidth / this.startHeight), this.getPos(), this.container.dataset.resizeState = "true", this.classNames.resizing && this.container.classList.add(this.classNames.resizing), document.addEventListener("mousemove", this.handleMouseMove), document.addEventListener("touchmove", this.handleTouchMove), document.addEventListener("mouseup", this.handleMouseUp), document.addEventListener("keydown", this.handleKeyDown), document.addEventListener("keyup", this.handleKeyUp);
	}
	handleResize(e, t) {
		if (!this.activeHandle) return;
		let n = this.preserveAspectRatio || this.isShiftKeyPressed, { width: r, height: i } = this.calculateNewDimensions(this.activeHandle, e, t), a = this.applyConstraints(r, i, n);
		this.element.style.width = `${a.width}px`, this.element.style.height = `${a.height}px`, this.onResize && this.onResize(a.width, a.height);
	}
	calculateNewDimensions(e, t, n) {
		let r = this.startWidth, i = this.startHeight, a = e.includes("right"), o = e.includes("left"), s = e.includes("bottom"), c = e.includes("top");
		return a ? r = this.startWidth + t : o && (r = this.startWidth - t), s ? i = this.startHeight + n : c && (i = this.startHeight - n), (e === "right" || e === "left") && (r = this.startWidth + (a ? t : -t)), (e === "top" || e === "bottom") && (i = this.startHeight + (s ? n : -n)), this.preserveAspectRatio || this.isShiftKeyPressed ? this.applyAspectRatio(r, i, e) : {
			width: r,
			height: i
		};
	}
	applyConstraints(e, t, n) {
		if (!n) {
			let n = Math.max(this.minSize.width, e), r = Math.max(this.minSize.height, t);
			return this.maxSize?.width && (n = Math.min(this.maxSize.width, n)), this.maxSize?.height && (r = Math.min(this.maxSize.height, r)), {
				width: n,
				height: r
			};
		}
		let r = e, i = t;
		return r < this.minSize.width && (r = this.minSize.width, i = r / this.aspectRatio), i < this.minSize.height && (i = this.minSize.height, r = i * this.aspectRatio), this.maxSize?.width && r > this.maxSize.width && (r = this.maxSize.width, i = r / this.aspectRatio), this.maxSize?.height && i > this.maxSize.height && (i = this.maxSize.height, r = i * this.aspectRatio), {
			width: r,
			height: i
		};
	}
	applyAspectRatio(e, t, n) {
		return n === "left" || n === "right" ? {
			width: e,
			height: e / this.aspectRatio
		} : n === "top" || n === "bottom" ? {
			width: t * this.aspectRatio,
			height: t
		} : {
			width: e,
			height: e / this.aspectRatio
		};
	}
}, Sf = class e extends Wd {
	constructor(...e) {
		super(...e), this.type = "node";
	}
	static create(t = {}) {
		let n = typeof t == "function" ? t() : t;
		return new e(n);
	}
	configure(e) {
		return super.configure(e);
	}
	extend(e) {
		let t = typeof e == "function" ? e() : e;
		return super.extend(t);
	}
};
function Cf(e) {
	return new Kd({
		find: e.find,
		handler: ({ state: t, range: n, match: r, pasteEvent: i }) => {
			let a = P(e.getAttributes, void 0, r, i);
			if (a === !1 || a === null) return null;
			let { tr: o } = t, s = r[r.length - 1], c = r[0], l = n.to;
			if (s) {
				let i = c.search(/\S/), u = n.from + c.indexOf(s), d = u + s.length;
				if (fu(n.from, n.to, t.doc).filter((t) => t.mark.type.excluded.find((n) => n === e.type && n !== t.mark.type)).filter((e) => e.to > u).length) return null;
				d < n.to && o.delete(d, n.to), u > n.from && o.delete(n.from + i, u), l = n.from + i + s.length, o.addMark(n.from + i, l, e.type.create(a || {})), r.index !== void 0 && r.input !== void 0 && r.index + r[0].length >= r.input.length || o.removeStoredMark(e.type);
			}
		}
	});
}
//#endregion
//#region src/device-annotations.ts
var wf = new D("deviceAnnotations"), Tf = /* @__PURE__ */ new Set([
	"paragraph",
	"heading",
	"listItem",
	"blockquote",
	"codeBlock",
	"tableCell",
	"tableHeader",
	"hardBreak",
	"horizontalRule"
]);
function Ef(e) {
	let t = "", n = [];
	function r(e, i) {
		if (e.isText) {
			t += e.text;
			for (let t = 0; t < e.text.length; t++) n.push(i + t);
		} else e.forEach((t, n) => r(t, i + n + (e.type.name === "doc" ? 0 : 1))), Tf.has(e.type.name) && (t += "\n", n.push(void 0));
	}
	return r(e, 0), {
		text: t,
		positions: n
	};
}
function Df(e, t) {
	if (!t) return null;
	let n = e.text.indexOf(t);
	if (n < 0 || e.text.indexOf(t, n + 1) >= 0) return null;
	let r = e.positions.slice(n, n + t.length).filter((e) => e !== void 0);
	return r.length ? {
		from: r[0],
		to: r[r.length - 1] + 1
	} : null;
}
function Of(e, t) {
	if (!t.length) return j.empty;
	let n = Ef(e), r = [];
	for (let e of t) {
		let t = Df(n, e.quote);
		if (!t) continue;
		let i = [
			"comment",
			"todo",
			"highlight"
		].includes(e.kind) ? e.kind : "comment", a = e.status === "resolved" ? "resolved" : "open";
		r.push(A.inline(t.from, t.to, {
			class: `wa-annotation wa-annotation-${i} wa-annotation-${a}`,
			"data-annotation-id": e.id,
			title: e.content
		}));
	}
	return j.create(e, r);
}
function kf(e) {
	return I.create({
		name: "deviceAnnotations",
		addProseMirrorPlugins() {
			return [new E({
				key: wf,
				state: {
					init: () => ({
						items: [],
						decorations: j.empty
					}),
					apply(e, t) {
						let n = e.getMeta(wf) ?? t.items;
						return e.docChanged || n !== t.items ? {
							items: n,
							decorations: Of(e.doc, n)
						} : t;
					}
				},
				props: {
					decorations: (e) => wf.getState(e)?.decorations,
					handleDOMEvents: { click(t, n) {
						let r = n.target instanceof Element ? n.target.closest("[data-annotation-id]") : null;
						return r && t.dom.contains(r) && e(r.getAttribute("data-annotation-id")), !1;
					} }
				}
			})];
		}
	});
}
function Af(e, t) {
	e.view.dispatch(e.state.tr.setMeta(wf, t).setMeta("addToHistory", !1));
}
function jf(e, t) {
	let n = wf.getState(e.state)?.items.find((e) => e.id === t), r = n && Df(Ef(e.state.doc), n.quote);
	if (!r || !e.commands.setTextSelection(r)) return !1;
	e.view.focus();
	let i = e.view.dom.querySelectorAll("[data-annotation-id]"), a = Array.from(i).find((e) => e.getAttribute("data-annotation-id") === t);
	return a ? (a.scrollIntoView({
		block: "start",
		inline: "nearest",
		behavior: "instant"
	}), !0) : !1;
}
function Mf(e) {
	let { from: t, to: n, empty: r } = e.state.selection;
	if (r) return "";
	let i = Ef(e.state.doc), a = -1, o = -1;
	return i.positions.forEach((e, r) => {
		e !== void 0 && e >= t && e < n && (a < 0 && (a = r), o = r + 1);
	}), a < 0 ? "" : i.text.slice(a, o);
}
var Nf = new D("deviceQuality");
function Pf(e, t, n = "\n\n") {
	let r = e.textBetween(0, e.content.size, n, "\n");
	if (!Number.isSafeInteger(t.from) || !Number.isSafeInteger(t.to) || t.from < 0 || t.to <= t.from || t.to > r.length || !t.expectedText || r.slice(t.from, t.to) !== t.expectedText) return null;
	let i, a;
	return e.descendants((r, o) => {
		if (!r.isText) return;
		let s = e.textBetween(0, o, n, "\n").length;
		t.from >= s && t.from < s + r.nodeSize && (i = o + t.from - s), t.to > s && t.to <= s + r.nodeSize && (a = o + t.to - s);
	}), i !== void 0 && a !== void 0 && e.textBetween(i, a, n, "\n") === t.expectedText ? {
		from: i,
		to: a
	} : null;
}
var Ff = I.create({
	name: "deviceQuality",
	addProseMirrorPlugins() {
		return [new E({
			key: Nf,
			state: {
				init: () => j.empty,
				apply(e, t) {
					return e.getMeta(Nf) ?? (e.docChanged ? j.empty : t);
				}
			},
			props: { decorations(e) {
				return Nf.getState(e);
			} }
		})];
	}
});
function If(e, t, n, r) {
	let i = [];
	if (e.state.doc.textBetween(0, e.state.doc.content.size, "\n\n", "\n") === t) for (let t of n.slice(0, 200)) {
		let n = Pf(e.state.doc, t);
		if (!n) continue;
		let a = [
			"info",
			"warning",
			"error"
		].includes(t.severity) ? t.severity : "info";
		i.push(A.inline(n.from, n.to, {
			class: "wa-quality wa-quality-" + a + (t.issueKey === r ? " wa-quality-selected" : ""),
			"data-quality-key": t.issueKey
		}));
	}
	e.view.dispatch(e.state.tr.setMeta(Nf, j.create(e.state.doc, i)));
}
function Lf(e, t, n) {
	if (e.state.doc.textBetween(0, e.state.doc.content.size, "\n\n", "\n") !== t) return !1;
	let r = Pf(e.state.doc, n);
	return r ? (e.view.dispatch(e.state.tr.setSelection(w.create(e.state.doc, r.from, r.to)).scrollIntoView()), e.view.focus(), !0) : !1;
}
//#endregion
//#region src/targeted-revision.ts
var Rf = (e) => /[\p{L}\p{N}\p{M}_]/u.test(e), zf = (e, t) => e.slice(0, t).match(/.$/u)?.[0] ?? "", Bf = (e, t) => t < e.length ? String.fromCodePoint(e.codePointAt(t)) : "";
function Vf(e) {
	let t = /* @__PURE__ */ new Set([0, e.length]), n = new Intl.Segmenter(void 0, { granularity: "grapheme" });
	for (let r of n.segment(e)) t.add(r.index);
	return t;
}
function Hf(e, t, n) {
	return n.has(t) && !(Rf(zf(e, t)) && Rf(Bf(e, t)));
}
function Uf(e) {
	return !/[\u0000-\u0008\u000B\u000C\u000E-\u001F]/.test(e) && !/[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/u.test(e);
}
function Wf(e, t, n, r, i = "\n\n") {
	let a = e.state.doc;
	if (!Number.isSafeInteger(t) || !Number.isSafeInteger(n) || t < 1 || n < t || n > a.content.size || typeof r != "string" || !Uf(r)) throw Error("Invalid targeted revision. Check the writing again.");
	let o = a.resolve(t), s = a.resolve(n);
	if (!o.parent.isTextblock || !o.sameParent(s) || /[\r\n]/.test(r) || a.textBetween(t, n, i, "\n") !== r) throw Error("This passage spans blocks, line breaks or changed source. Review a smaller passage.");
	let c = !1;
	if (a.nodesBetween(t, n, (e) => {
		e.isLeaf && !e.isText && (c = !0);
	}), c) throw Error("This passage contains embedded content. Revise it manually.");
	let l = o.parent.textBetween(0, o.parent.content.size, "", "￼"), u = Vf(l);
	if (!Hf(l, o.parentOffset, u) || !Hf(l, s.parentOffset, u)) throw Error("Select whole words and complete Unicode characters before applying this fix.");
}
function Gf(e, t, n, r, i, a = "\n\n") {
	if (Wf(e, t, n, r, a), typeof i != "string" || r === i || i.length > 2e4 || i.length > 0 && !i.trim() && t !== n || !Uf(i) || /\r/.test(i)) throw Error("Invalid targeted revision. Check the writing again.");
	let o = e.state.doc, s = o.resolve(t), c = o.textBetween(0, o.content.size, a, "\n"), u = o.textBetween(0, t, a, "\n").length, d = (e) => {
		if (e.doc.check(), e.doc.textBetween(0, e.doc.content.size, a, "\n") !== c.slice(0, u) + i + c.slice(u + r.length)) throw Error("The targeted revision could not preserve the surrounding writing.");
		return e;
	};
	if (i.includes("\n")) {
		let r = i.split(a), c = o.nodeAt(t)?.marks ?? s.marks(), u = !0;
		if (o.nodesBetween(t, n, (e) => {
			e.isText && JSON.stringify(e.marks) !== JSON.stringify(c) && (u = !1);
		}), s.parent.type.name !== "paragraph" || !u || r.some((e) => /[\r\n]/.test(e))) throw Error("Review a uniformly formatted paragraph split manually to preserve its structure.");
		let f = new h(l.fromArray(r.map((t) => s.parent.type.create(s.parent.attrs, t ? e.schema.text(t, c) : void 0))), 1, 1);
		return d(e.state.tr.replaceRange(t, n, f));
	}
	let f = 0, p = 0;
	for (; f < r.length && f < i.length && r[f] === i[f];) f++;
	let m = Vf(r), g = Vf(i);
	for (; f > 0 && (!Hf(r, f, m) || !Hf(i, f, g));) f--;
	for (; p < r.length - f && p < i.length - f && r[r.length - p - 1] === i[i.length - p - 1];) p++;
	for (; p > 0 && (!Hf(r, r.length - p, m) || !Hf(i, i.length - p, g));) p--;
	let _ = t + f, ee = n - p, v = i.slice(f, i.length - p), y = ee > _ ? o.nodeAt(_)?.marks ?? o.resolve(_).marks() : o.resolve(_).marks(), b = (e) => JSON.stringify(e) === JSON.stringify(y), x = !0;
	if (o.nodesBetween(_, ee, (e) => {
		e.isText && !b(e.marks) && (x = !1);
	}), v && _ === ee) {
		let e = o.resolve(_);
		e.nodeBefore?.isText && e.nodeAfter?.isText && JSON.stringify(e.nodeBefore.marks) !== JSON.stringify(e.nodeAfter.marks) && (x = !1);
	}
	if (v && !x) throw Error("This changed wording spans mixed formatting. Review a smaller passage or revise it manually.");
	return d(e.state.tr.replaceWith(_, ee, v ? e.schema.text(v, y) : l.empty));
}
//#endregion
//#region src/device-consistency.ts
var Kf = new D("deviceConsistencyPassage"), qf = I.create({
	name: "deviceConsistencyPassage",
	addProseMirrorPlugins() {
		return [new E({
			key: Kf,
			state: {
				init: () => j.empty,
				apply(e, t) {
					return e.getMeta(Kf) ?? (e.docChanged ? j.empty : t);
				}
			},
			props: { decorations(e) {
				return Kf.getState(e);
			} }
		})];
	}
});
function Jf(e, t) {
	let n = t ? j.create(e.state.doc, [A.inline(t.from, t.to, { class: "wa-consistency-passage" })]) : j.empty;
	e.view.dispatch(e.state.tr.setMeta(Kf, n).setMeta("addToHistory", !1));
}
//#endregion
//#region src/tiptap-commands.ts
function Yf(e) {
	e && e.chain().focus().toggleBold().run();
}
function Xf(e) {
	e && e.chain().focus().toggleItalic().run();
}
function Zf(e) {
	e && e.chain().focus().setParagraph().run();
}
function Qf(e, t) {
	e && e.chain().focus().setHeading({ level: t }).run();
}
function $f(e) {
	if (!e) return;
	let t = e.state?.selection, n = e.__writerLastSelectionDocRange, r = t && !t.empty ? {
		from: t.from,
		to: t.to
	} : n && Number.isFinite(n.from) && Number.isFinite(n.to) && n.to > n.from ? {
		from: n.from,
		to: n.to
	} : null, i = e.chain().focus();
	r && (i = i.setTextSelection(r)), i.toggleBlockquote().run();
}
function ep(e) {
	e && e.chain().focus().toggleBulletList().run();
}
function tp(e) {
	e && e.chain().focus().toggleOrderedList().run();
}
function np(e) {
	e && e.chain().focus().undo().run();
}
function rp(e) {
	e && e.chain().focus().redo().run();
}
//#endregion
//#region node_modules/lib0/map.js
var ip = () => /* @__PURE__ */ new Map(), ap = (e) => {
	let t = ip();
	return e.forEach((e, n) => {
		t.set(n, e);
	}), t;
}, op = (e, t, n) => {
	let r = e.get(t);
	return r === void 0 && e.set(t, r = n()), r;
}, sp = (e, t) => {
	let n = [];
	for (let [r, i] of e) n.push(t(i, r));
	return n;
}, cp = (e, t) => {
	for (let [n, r] of e) if (t(r, n)) return !0;
	return !1;
}, lp = () => /* @__PURE__ */ new Set(), up = (e) => e[e.length - 1], dp = (e, t) => {
	for (let n = 0; n < t.length; n++) e.push(t[n]);
}, fp = Array.from, pp = (e, t) => {
	for (let n = 0; n < e.length; n++) if (!t(e[n], n, e)) return !1;
	return !0;
}, mp = (e, t) => {
	for (let n = 0; n < e.length; n++) if (t(e[n], n, e)) return !0;
	return !1;
}, hp = (e, t) => {
	let n = Array(e);
	for (let r = 0; r < e; r++) n[r] = t(r, n);
	return n;
}, gp = Array.isArray, _p = class {
	constructor() {
		this._observers = ip();
	}
	on(e, t) {
		return op(this._observers, e, lp).add(t), t;
	}
	once(e, t) {
		let n = (...r) => {
			this.off(e, n), t(...r);
		};
		this.on(e, n);
	}
	off(e, t) {
		let n = this._observers.get(e);
		n !== void 0 && (n.delete(t), n.size === 0 && this._observers.delete(e));
	}
	emit(e, t) {
		return fp((this._observers.get(e) || ip()).values()).forEach((e) => e(...t));
	}
	destroy() {
		this._observers = ip();
	}
}, vp = class {
	constructor() {
		this._observers = ip();
	}
	on(e, t) {
		op(this._observers, e, lp).add(t);
	}
	once(e, t) {
		let n = (...r) => {
			this.off(e, n), t(...r);
		};
		this.on(e, n);
	}
	off(e, t) {
		let n = this._observers.get(e);
		n !== void 0 && (n.delete(t), n.size === 0 && this._observers.delete(e));
	}
	emit(e, t) {
		return fp((this._observers.get(e) || ip()).values()).forEach((e) => e(...t));
	}
	destroy() {
		this._observers = ip();
	}
}, yp = Math.floor, bp = Math.abs, xp = (e, t) => e < t ? e : t, Sp = (e, t) => e > t ? e : t;
Number.isNaN;
var Cp = (e) => e === 0 ? 1 / e < 0 : e < 0, wp = 1 << 29, Tp = 2 ** 53 - 1, Ep = -(2 ** 53 - 1), Dp = Number.isInteger || ((e) => typeof e == "number" && isFinite(e) && yp(e) === e);
Number.isNaN, Number.parseInt;
//#endregion
//#region node_modules/lib0/string.js
var Op = String.fromCharCode;
String.fromCodePoint, Op(65535);
var kp = (e) => e.toLowerCase(), Ap = /^\s*/g, jp = (e) => e.replace(Ap, ""), Mp = /([A-Z])/g, Np = (e, t) => jp(e.replace(Mp, (e) => `${t}${kp(e)}`)), Pp = (e) => {
	let t = unescape(encodeURIComponent(e)), n = t.length, r = new Uint8Array(n);
	for (let e = 0; e < n; e++) r[e] = t.codePointAt(e);
	return r;
}, Fp = typeof TextEncoder < "u" ? new TextEncoder() : null, Ip = Fp ? (e) => Fp.encode(e) : Pp, Lp = typeof TextDecoder > "u" ? null : new TextDecoder("utf-8", {
	fatal: !0,
	ignoreBOM: !0
});
/* c8 ignore start */
Lp && Lp.decode(/* @__PURE__ */ new Uint8Array()).length === 1 && 
/* c8 ignore next */
(Lp = null);
var Rp = (e, t) => hp(t, () => e).join(""), zp = class {
	constructor() {
		this.cpos = 0, this.cbuf = /* @__PURE__ */ new Uint8Array(100), this.bufs = [];
	}
}, Bp = () => new zp(), Vp = (e) => {
	let t = Bp();
	return e(t), Up(t);
}, Hp = (e) => {
	let t = e.cpos;
	for (let n = 0; n < e.bufs.length; n++) t += e.bufs[n].length;
	return t;
}, Up = (e) => {
	let t = new Uint8Array(Hp(e)), n = 0;
	for (let r = 0; r < e.bufs.length; r++) {
		let i = e.bufs[r];
		t.set(i, n), n += i.length;
	}
	return t.set(new Uint8Array(e.cbuf.buffer, 0, e.cpos), n), t;
}, Wp = (e, t) => {
	let n = e.cbuf.length;
	n - e.cpos < t && (e.bufs.push(new Uint8Array(e.cbuf.buffer, 0, e.cpos)), e.cbuf = new Uint8Array(Sp(n, t) * 2), e.cpos = 0);
}, Gp = (e, t) => {
	let n = e.cbuf.length;
	e.cpos === n && (e.bufs.push(e.cbuf), e.cbuf = new Uint8Array(n * 2), e.cpos = 0), e.cbuf[e.cpos++] = t;
}, Kp = Gp, L = (e, t) => {
	for (; t > 127;) Gp(e, 128 | 127 & t), t = yp(t / 128);
	Gp(e, 127 & t);
}, qp = (e, t) => {
	let n = Cp(t);
	for (n && (t = -t), Gp(e, (t > 63 ? 128 : 0) | (n ? 64 : 0) | 63 & t), t = yp(t / 64); t > 0;) Gp(e, (t > 127 ? 128 : 0) | 127 & t), t = yp(t / 128);
}, Jp = /* @__PURE__ */ new Uint8Array(3e4), Yp = Jp.length / 3, Xp = Fp && Fp.encodeInto ? (e, t) => {
	if (t.length < Yp) {
		/* c8 ignore next */
		let n = Fp.encodeInto(t, Jp).written || 0;
		L(e, n);
		for (let t = 0; t < n; t++) Gp(e, Jp[t]);
	} else Qp(e, Ip(t));
} : (e, t) => {
	let n = unescape(encodeURIComponent(t)), r = n.length;
	L(e, r);
	for (let t = 0; t < r; t++) Gp(e, n.codePointAt(t));
}, Zp = (e, t) => {
	let n = e.cbuf.length, r = e.cpos, i = xp(n - r, t.length), a = t.length - i;
	e.cbuf.set(t.subarray(0, i), r), e.cpos += i, a > 0 && (e.bufs.push(e.cbuf), e.cbuf = new Uint8Array(Sp(n * 2, a)), e.cbuf.set(t.subarray(i)), e.cpos = a);
}, Qp = (e, t) => {
	L(e, t.byteLength), Zp(e, t);
}, $p = (e, t) => {
	Wp(e, t);
	let n = new DataView(e.cbuf.buffer, e.cpos, t);
	return e.cpos += t, n;
}, em = (e, t) => $p(e, 4).setFloat32(0, t, !1), tm = (e, t) => $p(e, 8).setFloat64(0, t, !1), nm = (e, t) => $p(e, 8).setBigInt64(0, t, !1), rm = /* @__PURE__ */ new DataView(/* @__PURE__ */ new ArrayBuffer(4)), im = (e) => (rm.setFloat32(0, e), rm.getFloat32(0) === e), am = (e, t) => {
	switch (typeof t) {
		case "string":
			Gp(e, 119), Xp(e, t);
			break;
		case "number":
			Dp(t) && bp(t) <= 2147483647 ? (Gp(e, 125), qp(e, t)) : im(t) ? (Gp(e, 124), em(e, t)) : (Gp(e, 123), tm(e, t));
			break;
		case "bigint":
			Gp(e, 122), nm(e, t);
			break;
		case "object":
			if (t === null) Gp(e, 126);
			else if (gp(t)) {
				Gp(e, 117), L(e, t.length);
				for (let n = 0; n < t.length; n++) am(e, t[n]);
			} else if (t instanceof Uint8Array) Gp(e, 116), Qp(e, t);
			else {
				Gp(e, 118);
				let n = Object.keys(t);
				L(e, n.length);
				for (let r = 0; r < n.length; r++) {
					let i = n[r];
					Xp(e, i), am(e, t[i]);
				}
			}
			break;
		case "boolean":
			Gp(e, t ? 120 : 121);
			break;
		default: Gp(e, 127);
	}
}, om = class extends zp {
	constructor(e) {
		super(), this.w = e, this.s = null, this.count = 0;
	}
	write(e) {
		this.s === e ? this.count++ : (this.count > 0 && L(this, this.count - 1), this.count = 1, this.w(this, e), this.s = e);
	}
}, sm = (e) => {
	e.count > 0 && (qp(e.encoder, e.count === 1 ? e.s : -e.s), e.count > 1 && L(e.encoder, e.count - 2));
}, cm = class {
	constructor() {
		this.encoder = new zp(), this.s = 0, this.count = 0;
	}
	write(e) {
		this.s === e ? this.count++ : (sm(this), this.count = 1, this.s = e);
	}
	toUint8Array() {
		return sm(this), Up(this.encoder);
	}
}, lm = (e) => {
	if (e.count > 0) {
		let t = e.diff * 2 + (e.count === 1 ? 0 : 1);
		qp(e.encoder, t), e.count > 1 && L(e.encoder, e.count - 2);
	}
}, um = class {
	constructor() {
		this.encoder = new zp(), this.s = 0, this.count = 0, this.diff = 0;
	}
	write(e) {
		this.diff === e - this.s ? (this.s = e, this.count++) : (lm(this), this.count = 1, this.diff = e - this.s, this.s = e);
	}
	toUint8Array() {
		return lm(this), Up(this.encoder);
	}
}, dm = class {
	constructor() {
		this.sarr = [], this.s = "", this.lensE = new cm();
	}
	write(e) {
		this.s += e, this.s.length > 19 && (this.sarr.push(this.s), this.s = ""), this.lensE.write(e.length);
	}
	toUint8Array() {
		let e = new zp();
		return this.sarr.push(this.s), this.s = "", Xp(e, this.sarr.join("")), Zp(e, this.lensE.toUint8Array()), Up(e);
	}
}, fm = (e) => Error(e), pm = () => {
	throw fm("Method unimplemented");
}, R = () => {
	throw fm("Unexpected case");
}, mm = fm("Unexpected end of array"), hm = fm("Integer out of Range"), gm = (e, t, n) => {
	if (t < 0 || n < 0 || t + n > e.length) throw mm;
	return new Uint8Array(e.buffer, e.byteOffset + t, n);
}, _m = (e, t, n) => {
	if (t < 0 || n < 0 || t + n > e.length) throw mm;
	return new DataView(e.buffer, e.byteOffset + t, n);
}, vm = class {
	constructor(e) {
		this.arr = e, this.pos = 0;
	}
}, ym = (e) => new vm(e), bm = (e) => e.pos !== e.arr.length, xm = (e, t) => {
	let n = gm(e.arr, e.pos, t);
	return e.pos += t, n;
}, Sm = (e) => xm(e, z(e)), Cm = (e) => e.arr[e.pos++], z = (e) => {
	let t = 0, n = 1, r = e.arr.length;
	for (; e.pos < r;) {
		let r = e.arr[e.pos++];
		if (t += (r & 127) * n, n *= 128, r < 128) return t;
		/* c8 ignore start */
		if (t > Tp) throw hm;
	}
	throw mm;
}, wm = (e) => {
	let t = e.arr[e.pos++], n = t & 63, r = 64, i = (t & 64) > 0 ? -1 : 1;
	if (!(t & 128)) return i * n;
	let a = e.arr.length;
	for (; e.pos < a;) {
		if (t = e.arr[e.pos++], n += (t & 127) * r, r *= 128, t < 128) return i * n;
		/* c8 ignore start */
		if (n > Tp) throw hm;
	}
	throw mm;
}, Tm = Lp ? (e) => Lp.decode(Sm(e)) : (e) => {
	let t = z(e);
	if (t === 0) return "";
	{
		let n = String.fromCodePoint(Cm(e));
		if (--t < 100) for (; t--;) n += String.fromCodePoint(Cm(e));
		else for (; t > 0;) {
			let r = t < 1e4 ? t : 1e4, i = e.arr.subarray(e.pos, e.pos + r);
			e.pos += r, n += String.fromCodePoint.apply(null, i), t -= r;
		}
		return decodeURIComponent(escape(n));
	}
}, Em = (e) => {
	let t = e.pos, n = Tm(e);
	return e.pos = t, n;
}, Dm = (e, t) => {
	let n = _m(e.arr, e.pos, t);
	return e.pos += t, n;
}, Om = [
	(e) => void 0,
	(e) => null,
	wm,
	(e) => Dm(e, 4).getFloat32(0, !1),
	(e) => Dm(e, 8).getFloat64(0, !1),
	(e) => Dm(e, 8).getBigInt64(0, !1),
	(e) => !1,
	(e) => !0,
	Tm,
	(e) => {
		let t = z(e), n = {};
		for (let r = 0; r < t; r++) {
			let t = Tm(e);
			n[t] = km(e);
		}
		return n;
	},
	(e) => {
		let t = z(e), n = [];
		for (let r = 0; r < t; r++) n.push(km(e));
		return n;
	},
	Sm
], km = (e) => Om[127 - Cm(e)](e), Am = class extends vm {
	constructor(e, t) {
		super(e), this.reader = t, this.s = null, this.count = 0;
	}
	read() {
		return this.count === 0 && (this.s = this.reader(this), this.count = bm(this) ? z(this) + 1 : -1), this.count--, this.s;
	}
}, jm = class extends vm {
	constructor(e) {
		super(e), this.s = 0, this.count = 0;
	}
	read() {
		if (this.count === 0) {
			this.s = wm(this);
			let e = Cp(this.s);
			this.count = 1, e && (this.s = -this.s, this.count = z(this) + 2);
		}
		return this.count--, this.s;
	}
}, Mm = class extends vm {
	constructor(e) {
		super(e), this.s = 0, this.count = 0, this.diff = 0;
	}
	read() {
		if (this.count === 0) {
			let e = wm(this), t = e & 1;
			this.diff = yp(e / 2), this.count = 1, t && (this.count = z(this) + 2);
		}
		return this.s += this.diff, this.count--, this.s;
	}
}, Nm = class {
	constructor(e) {
		this.decoder = new jm(e), this.str = Tm(this.decoder), this.spos = 0;
	}
	read() {
		let e = this.spos + this.decoder.read(), t = this.str.slice(this.spos, e);
		return this.spos = e, t;
	}
};
crypto.subtle;
var Pm = crypto.getRandomValues.bind(crypto), Fm = Math.random, Im = () => Pm(/* @__PURE__ */ new Uint32Array(1))[0], Lm = (e) => e[yp(Fm() * e.length)], Rm = "10000000-1000-4000-8000-100000000000", zm = () => Rm.replace(/[018]/g, (e) => (e ^ Im() & 15 >> e / 4).toString(16)), Bm = Date.now, Vm = (e) => new Promise(e);
Promise.all.bind(Promise);
//#endregion
//#region node_modules/lib0/conditions.js
/* c8 ignore next */
var Hm = (e) => e === void 0 ? null : e, Um = new class {
	constructor() {
		this.map = /* @__PURE__ */ new Map();
	}
	setItem(e, t) {
		this.map.set(e, t);
	}
	getItem(e) {
		return this.map.get(e);
	}
}();
/* c8 ignore start */
try {
	typeof localStorage < "u" && localStorage && (Um = localStorage);
} catch {}
/* c8 ignore stop */
/* c8 ignore next */
var Wm = Um, Gm = Symbol("Equality"), Km = (e, t) => e === t || !!e?.[Gm]?.(t) || !1, qm = (e) => typeof e == "object", Jm = Object.assign, Ym = Object.keys, Xm = (e, t) => {
	for (let n in e) t(e[n], n);
}, Zm = (e) => Ym(e).length, Qm = (e) => {
	for (let t in e) return !1;
	return !0;
}, $m = (e, t) => {
	for (let n in e) if (!t(e[n], n)) return !1;
	return !0;
}, eh = (e, t) => Object.prototype.hasOwnProperty.call(e, t), th = (e, t) => e === t || Zm(e) === Zm(t) && $m(e, (e, n) => (e !== void 0 || eh(t, n)) && Km(t[n], e)), nh = Object.freeze, rh = (e) => {
	for (let t in e) {
		let n = e[t];
		(typeof n == "object" || typeof n == "function") && rh(e[t]);
	}
	return nh(e);
}, ih = (e, t, n = 0) => {
	try {
		for (; n < e.length; n++) e[n](...t);
	} finally {
		n < e.length && ih(e, t, n + 1);
	}
}, ah = (e) => e, oh = (e, t) => {
	if (e === t) return !0;
	if (e == null || t == null || e.constructor !== t.constructor && (e.constructor || Object) !== (t.constructor || Object)) return !1;
	if (e[Gm] != null) return e[Gm](t);
	switch (e.constructor) {
		case ArrayBuffer: e = new Uint8Array(e), t = new Uint8Array(t);
		case Uint8Array:
			if (e.byteLength !== t.byteLength) return !1;
			for (let n = 0; n < e.length; n++) if (e[n] !== t[n]) return !1;
			break;
		case Set:
			if (e.size !== t.size) return !1;
			for (let n of e) if (!t.has(n)) return !1;
			break;
		case Map:
			if (e.size !== t.size) return !1;
			for (let n of e.keys()) if (!t.has(n) || !oh(e.get(n), t.get(n))) return !1;
			break;
		case void 0:
		case Object:
			if (Zm(e) !== Zm(t)) return !1;
			for (let n in e) if (!eh(e, n) || !oh(e[n], t[n])) return !1;
			break;
		case Array:
			if (e.length !== t.length) return !1;
			for (let n = 0; n < e.length; n++) if (!oh(e[n], t[n])) return !1;
			break;
		default: return !1;
	}
	return !0;
}, sh = (e, t) => t.includes(e), ch = typeof process < "u" && process.release && /node|io\.js/.test(process.release.name) && Object.prototype.toString.call(typeof process < "u" ? process : 0) === "[object process]", lh = typeof window < "u" && typeof document < "u" && !ch;
typeof navigator < "u" && /Mac/.test(navigator.platform);
var uh, dh = [], fh = () => {
	if (uh === void 0) {
		if (ch) {
			uh = ip();
			let e = process.argv, t = null;
			for (let n = 0; n < e.length; n++) {
				let r = e[n];
				r[0] === "-" ? (t !== null && uh.set(t, ""), t = r) : t === null ? dh.push(r) : (uh.set(t, r), t = null);
			}
			t !== null && uh.set(t, "");
		} else typeof location == "object" ? (uh = ip(), (location.search || "?").slice(1).split("&").forEach((e) => {
			if (e.length !== 0) {
				let [t, n] = e.split("=");
				uh.set(`--${Np(t, "-")}`, n), uh.set(`-${Np(t, "-")}`, n);
			}
		})) : uh = ip();
	}
	return uh;
}, ph = (e) => fh().has(e), mh = (e) => Hm(ch ? process.env[e.toUpperCase().replaceAll("-", "_")] : Wm.getItem(e)), hh = (e) => ph("--" + e) || mh(e) !== null, gh = hh("production"), _h = ch && sh(process.env.FORCE_COLOR, [
	"true",
	"1",
	"2"
]) || !ph("--no-colors") && !hh("no-color") && (!ch || process.stdout.isTTY) && (!ch || ph("--color") || mh("COLORTERM") !== null || (mh("TERM") || "").includes("color")), vh = (e) => new Uint8Array(e), yh = lh ? (e) => {
	let t = "";
	for (let n = 0; n < e.byteLength; n++) t += Op(e[n]);
	return btoa(t);
} : (e) => Buffer.from(e.buffer, e.byteOffset, e.byteLength).toString("base64"), bh = (e) => {
	let t = vh(e.byteLength);
	return t.set(e), t;
}, xh = (e) => Vp((t) => am(t, e)), Sh = class {
	constructor(e, t) {
		this.left = e, this.right = t;
	}
}, Ch = (e, t) => new Sh(e, t), wh = (e) => e.next() >= .5, Th = (e, t, n) => yp(e.next() * (n + 1 - t) + t), Eh = (e, t, n) => yp(e.next() * (n + 1 - t) + t), Dh = (e, t, n) => Eh(e, t, n), Oh = (e) => Op(Dh(e, 97, 122)), kh = (e, t = 0, n = 20) => {
	let r = Dh(e, t, n), i = "";
	for (let t = 0; t < r; t++) i += Oh(e);
	return i;
}, Ah = (e, t) => t[Dh(e, 0, t.length - 1)];
/* c8 ignore stop */
//#endregion
//#region \0@oxc-project+runtime@0.151.0/helpers/esm/typeof.js
function jh(e) {
	"@babel/helpers - typeof";
	return jh = typeof Symbol == "function" && typeof Symbol.iterator == "symbol" ? function(e) {
		return typeof e;
	} : function(e) {
		return e && typeof Symbol == "function" && e.constructor === Symbol && e !== Symbol.prototype ? "symbol" : typeof e;
	}, jh(e);
}
//#endregion
//#region \0@oxc-project+runtime@0.151.0/helpers/esm/toPrimitive.js
function Mh(e, t) {
	if (jh(e) != "object" || !e) return e;
	var n = e[Symbol.toPrimitive];
	if (n !== void 0) {
		var r = n.call(e, t || "default");
		if (jh(r) != "object") return r;
		throw TypeError("@@toPrimitive must return a primitive value.");
	}
	return (t === "string" ? String : Number)(e);
}
//#endregion
//#region \0@oxc-project+runtime@0.151.0/helpers/esm/toPropertyKey.js
function Nh(e) {
	var t = Mh(e, "string");
	return jh(t) == "symbol" ? t : t + "";
}
//#endregion
//#region \0@oxc-project+runtime@0.151.0/helpers/esm/defineProperty.js
function Ph(e, t, n) {
	return (t = Nh(t)) in e ? Object.defineProperty(e, t, {
		value: n,
		enumerable: !0,
		configurable: !0,
		writable: !0
	}) : e[t] = n, e;
}
//#endregion
//#region node_modules/lib0/schema.js
var Fh, Ih = Symbol("0schema"), Lh = class {
	constructor() {
		this._rerrs = [];
	}
	extend(e, t, n, r = null) {
		this._rerrs.push({
			path: e,
			expected: t,
			has: n,
			message: r
		});
	}
	toString() {
		let e = [];
		for (let t = this._rerrs.length - 1; t > 0; t--) {
			let n = this._rerrs[t];
			/* c8 ignore next */
			e.push(Rp(" ", (this._rerrs.length - t) * 2) + `${n.path == null ? "" : `[${n.path}] `}${n.has} doesn't match ${n.expected}. ${n.message}`);
		}
		return e.join("\n");
	}
}, Rh = (e, t) => e === t ? !0 : e == null || t == null || e.constructor !== t.constructor ? !1 : e[Gm] ? Km(e, t) : gp(e) ? pp(e, (e) => mp(t, (t) => Rh(e, t))) : qm(e) ? $m(e, (e, n) => Rh(e, t[n])) : !1;
Fh = Gm;
var zh = class {
	extends(e) {
		let [t, n] = [this.shape, e.shape];
		return this.constructor._dilutes && ([n, t] = [t, n]), Rh(t, n);
	}
	equals(e) {
		return this.constructor === e.constructor && oh(this.shape, e.shape);
	}
	[Ih]() {
		return !0;
	}
	[Fh](e) {
		return this.equals(e);
	}
	validate(e) {
		return this.check(e);
	}
	/* c8 ignore start */
	check(e, t) {
		pm();
	}
	/* c8 ignore stop */
	get nullable() {
		return _g(this, Mg);
	}
	get optional() {
		return new Yh(this);
	}
	cast(e) {
		return Ig(e, this), e;
	}
	expect(e) {
		return Ig(e, this), e;
	}
};
Ph(zh, "_dilutes", !1);
var Bh = class extends zh {
	constructor(e, t) {
		super(), this.shape = e, this._c = t;
	}
	check(e, t = void 0) {
		let n = e?.constructor === this.shape && (this._c == null || this._c(e));
		return !n && t?.extend(null, this.shape.name, e?.constructor.name, e?.constructor === this.shape ? "Check failed" : "Constructor match failed"), n;
	}
}, B = (e, t = null) => new Bh(e, t);
B(Bh);
var Vh = class extends zh {
	constructor(e) {
		super(), this.shape = e;
	}
	check(e, t) {
		let n = this.shape(e);
		return !n && t?.extend(null, "custom prop", e?.constructor.name, "failed to check custom prop"), n;
	}
}, Hh = (e) => new Vh(e);
B(Vh);
var Uh = class extends zh {
	constructor(e) {
		super(), this.shape = e;
	}
	check(e, t) {
		let n = this.shape.some((t) => t === e);
		return !n && t?.extend(null, this.shape.join(" | "), e.toString()), n;
	}
}, Wh = (...e) => new Uh(e), Gh = B(Uh), Kh = RegExp.escape || ((e) => e.replace(/[().|&,$^[\]]/g, (e) => "\\" + e)), qh = (e) => {
	if (Dg.check(e)) return [Kh(e)];
	if (Gh.check(e)) return e.shape.map((e) => e + "");
	if (Eg.check(e)) return ["[+-]?\\d+.?\\d*"];
	if (Og.check(e)) return [".*"];
	if (vg.check(e)) return e.shape.map(qh).flat(1);
	/* c8 ignore next 2 */
	R();
};
B(class extends zh {
	constructor(e) {
		super(), this.shape = e, this._r = RegExp("^" + e.map(qh).map((e) => `(${e.join("|")})`).join("") + "$");
	}
	check(e, t) {
		let n = this._r.exec(e) != null;
		return !n && t?.extend(null, this._r.toString(), e.toString(), "String doesn't match string template."), n;
	}
});
var Jh = Symbol("optional"), Yh = class extends zh {
	constructor(e) {
		super(), this.shape = e;
	}
	check(e, t) {
		let n = e === void 0 || this.shape.check(e);
		return !n && t?.extend(null, "undefined (optional)", "()"), n;
	}
	get [Jh]() {
		return !0;
	}
}, Xh = B(Yh), Zh = class extends zh {
	check(e, t) {
		return t?.extend(null, "never", typeof e), !1;
	}
};
new Zh(), B(Zh);
var Qh = class e extends zh {
	constructor(e, t = !1) {
		super(), this.shape = e, this._isPartial = t;
	}
	get partial() {
		return new e(this.shape, !0);
	}
	check(e, t) {
		return e == null ? 
		/* c8 ignore next */
		(t?.extend(null, "object", "null"), !1) : $m(this.shape, (n, r) => {
			let i = this._isPartial && !eh(e, r) || n.check(e[r], t);
			return !i && t?.extend(r.toString(), n.toString(), typeof e[r], "Object property does not match"), i;
		});
	}
};
Ph(Qh, "_dilutes", !0);
var $h = (e) => new Qh(e), eg = B(Qh), tg = Hh((e) => e != null && (e.constructor === Object || e.constructor == null)), ng = class extends zh {
	constructor(e, t) {
		super(), this.shape = {
			keys: e,
			values: t
		};
	}
	check(e, t) {
		return e != null && $m(e, (n, r) => {
			let i = this.shape.keys.check(r, t);
			return !i && t?.extend(r + "", "Record", typeof e, i ? "Key doesn't match schema" : "Value doesn't match value"), i && this.shape.values.check(n, t);
		});
	}
}, rg = (e, t) => new ng(e, t), ig = B(ng), ag = class extends zh {
	constructor(e) {
		super(), this.shape = e;
	}
	check(e, t) {
		return e != null && $m(this.shape, (n, r) => {
			let i = n.check(e[r], t);
			return !i && t?.extend(r.toString(), "Tuple", typeof n), i;
		});
	}
}, og = (...e) => new ag(e);
B(ag);
var sg = class extends zh {
	constructor(e) {
		super(), this.shape = e.length === 1 ? e[0] : new gg(e);
	}
	check(e, t) {
		let n = gp(e) && pp(e, (e) => this.shape.check(e));
		return !n && t?.extend(null, "Array", ""), n;
	}
}, cg = (...e) => new sg(e), lg = B(sg), ug = Hh((e) => gp(e)), dg = class extends zh {
	constructor(e, t) {
		super(), this.shape = e, this._c = t;
	}
	check(e, t) {
		let n = e instanceof this.shape && (this._c == null || this._c(e));
		return !n && t?.extend(null, this.shape.name, e?.constructor.name), n;
	}
}, fg = (e, t = null) => new dg(e, t);
B(dg);
var pg = fg(zh), mg = B(class extends zh {
	constructor(e) {
		super(), this.len = e.length - 1, this.args = og(...e.slice(-1)), this.res = e[this.len];
	}
	check(e, t) {
		let n = e.constructor === Function && e.length <= this.len;
		return !n && t?.extend(null, "function", typeof e), n;
	}
}), hg = Hh((e) => typeof e == "function");
B(class extends zh {
	constructor(e) {
		super(), this.shape = e;
	}
	check(e, t) {
		let n = pp(this.shape, (n) => n.check(e, t));
		return !n && t?.extend(null, "Intersectinon", typeof e), n;
	}
}, (e) => e.shape.length > 0);
var gg = class extends zh {
	constructor(e) {
		super(), this.shape = e;
	}
	check(e, t) {
		let n = mp(this.shape, (n) => n.check(e, t));
		return t?.extend(null, "Union", typeof e), n;
	}
};
Ph(gg, "_dilutes", !0);
var _g = (...e) => e.findIndex((e) => vg.check(e)) >= 0 ? _g(...e.map((e) => Fg(e)).map((e) => vg.check(e) ? e.shape : [e]).flat(1)) : e.length === 1 ? e[0] : new gg(e), vg = B(gg), yg = () => !0, bg = Hh(yg), xg = B(Vh, (e) => e.shape === yg), Sg = Hh((e) => typeof e == "bigint"), Cg = Hh((e) => e === Sg), wg = Hh((e) => typeof e == "symbol");
Hh((e) => e === wg);
var Tg = Hh((e) => typeof e == "number"), Eg = Hh((e) => e === Tg), Dg = Hh((e) => typeof e == "string"), Og = Hh((e) => e === Dg), kg = Hh((e) => typeof e == "boolean"), Ag = Hh((e) => e === kg), jg = Wh(void 0);
B(Uh, (e) => e.shape.length === 1 && e.shape[0] === void 0), Wh(void 0);
var Mg = Wh(null), Ng = B(Uh, (e) => e.shape.length === 1 && e.shape[0] === null);
B(Uint8Array), B(Bh, (e) => e.shape === Uint8Array);
var Pg = _g(Tg, Dg, Mg, jg, Sg, kg, wg);
(() => {
	let e = cg(bg), t = rg(Dg, bg), n = _g(Tg, Dg, Mg, kg, e, t);
	return e.shape = n, t.shape.values = n, n;
})();
var Fg = (e) => {
	if (pg.check(e)) return e;
	if (tg.check(e)) {
		let t = {};
		for (let n in e) t[n] = Fg(e[n]);
		return $h(t);
	}
	if (ug.check(e)) return _g(...e.map(Fg));
	if (Pg.check(e)) return Wh(e);
	if (hg.check(e)) return B(e);
	/* c8 ignore next */
	R();
}, Ig = gh ? () => {} : (e, t) => {
	let n = new Lh();
	if (!t.check(e, n)) throw fm(`Expected value to be of type ${t.constructor.name}.\n${n.toString()}`);
}, Lg = class {
	constructor(e) {
		this.patterns = [], this.$state = e;
	}
	if(e, t) {
		return this.patterns.push({
			if: Fg(e),
			h: t
		}), this;
	}
	else(e) {
		return this.if(bg, e);
	}
	done() {
		return (e, t) => {
			for (let n = 0; n < this.patterns.length; n++) {
				let r = this.patterns[n];
				if (r.if.check(e)) return r.h(e, t);
			}
			throw fm("Unhandled pattern");
		};
	}
}, Rg = ((e) => new Lg(e))(bg).if(Eg, (e, t) => Th(t, Ep, Tp)).if(Og, (e, t) => kh(t)).if(Ag, (e, t) => wh(t)).if(Cg, (e, t) => BigInt(Th(t, Ep, Tp))).if(vg, (e, t) => zg(t, Ah(t, e.shape))).if(eg, (e, t) => {
	let n = {};
	for (let r in e.shape) {
		let i = e.shape[r];
		if (Xh.check(i)) {
			if (wh(t)) continue;
			i = i.shape;
		}
		n[r] = Rg(i, t);
	}
	return n;
}).if(lg, (e, t) => {
	let n = [], r = Eh(t, 0, 42);
	for (let i = 0; i < r; i++) n.push(zg(t, e.shape));
	return n;
}).if(Gh, (e, t) => Ah(t, e.shape)).if(Ng, (e, t) => null).if(mg, (e, t) => {
	let n = zg(t, e.res);
	return () => n;
}).if(xg, (e, t) => zg(t, Ah(t, [
	Tg,
	Dg,
	Mg,
	jg,
	Sg,
	kg,
	cg(Tg),
	rg(_g("a", "b", "c"), Tg)
]))).if(ig, (e, t) => {
	let n = {}, r = Th(t, 0, 3);
	for (let i = 0; i < r; i++) {
		let r = zg(t, e.shape.keys);
		n[r] = zg(t, e.shape.values);
	}
	return n;
}).done(), zg = (e, t) => Rg(Fg(t), e), Bg = typeof document < "u" ? document : {};
Hh((e) => e.nodeType === Gg), typeof DOMParser < "u" && new DOMParser(), Hh((e) => e.nodeType === Hg), Hh((e) => e.nodeType === Ug);
var Vg = (e) => sp(e, (e, t) => `${t}:${e};`).join(""), Hg = Bg.ELEMENT_NODE, Ug = Bg.TEXT_NODE;
Bg.CDATA_SECTION_NODE, Bg.COMMENT_NODE;
var Wg = Bg.DOCUMENT_NODE;
Bg.DOCUMENT_TYPE_NODE;
var Gg = Bg.DOCUMENT_FRAGMENT_NODE;
Hh((e) => e.nodeType === Wg);
/* c8 ignore stop */
//#endregion
//#region node_modules/lib0/eventloop.js
var Kg = ((e) => class {
	constructor(e) {
		this._ = e;
	}
	destroy() {
		e(this._);
	}
})(clearTimeout), qg = (e, t) => new Kg(setTimeout(t, e)), Jg = Symbol, Yg = Jg(), Xg = Jg(), Zg = Jg(), Qg = Jg(), $g = Jg(), e_ = Jg(), t_ = Jg(), n_ = Jg(), r_ = Jg(), i_ = (e) => {
	e.length === 1 && e[0]?.constructor === Function && (e = e[0]());
	let t = [], n = [], r = 0;
	for (; r < e.length; r++) {
		let n = e[r];
		if (n === void 0) break;
		if (n.constructor === String || n.constructor === Number) t.push(n);
		else if (n.constructor === Object) break;
	}
	for (r > 0 && n.push(t.join("")); r < e.length; r++) {
		let t = e[r];
		t instanceof Symbol || n.push(t);
	}
	return n;
};
Bm();
/* c8 ignore stop */
//#endregion
//#region node_modules/lib0/logging.js
var a_ = {
	[Yg]: Ch("font-weight", "bold"),
	[Xg]: Ch("font-weight", "normal"),
	[Zg]: Ch("color", "blue"),
	[$g]: Ch("color", "green"),
	[Qg]: Ch("color", "grey"),
	[e_]: Ch("color", "red"),
	[t_]: Ch("color", "purple"),
	[n_]: Ch("color", "orange"),
	[r_]: Ch("color", "black")
}, o_ = _h ? (e) => {
	e.length === 1 && e[0]?.constructor === Function && (e = e[0]());
	let t = [], n = [], r = ip(), i = [], a = 0;
	for (; a < e.length; a++) {
		let i = e[a], o = a_[i];
		if (o !== void 0) r.set(o.left, o.right);
		else {
			if (i === void 0) break;
			if (i.constructor === String || i.constructor === Number) {
				let e = Vg(r);
				a > 0 || e.length > 0 ? (t.push("%c" + i), n.push(e)) : t.push(i);
			} else break;
		}
	}
	for (a > 0 && (i = n, i.unshift(t.join(""))); a < e.length; a++) {
		let t = e[a];
		t instanceof Symbol || i.push(t);
	}
	return i;
} : i_, s_ = (...e) => {
	/* c8 ignore next */
	console.log(...o_(e)), l_.forEach((t) => t.print(e));
}, c_ = (...e) => {
	console.warn(...o_(e)), e.unshift(n_), l_.forEach((t) => t.print(e));
}, l_ = lp(), u_ = (e) => ({
	[Symbol.iterator]() {
		return this;
	},
	next: e
}), d_ = (e, t) => u_(() => {
	let n;
	do
		n = e.next();
	while (!n.done && !t(n.value));
	return n;
}), f_ = (e, t) => u_(() => {
	let { done: n, value: r } = e.next();
	return {
		done: n,
		value: n ? void 0 : t(r)
	};
}), p_ = class {
	constructor(e, t) {
		this.clock = e, this.len = t;
	}
}, m_ = class {
	constructor() {
		this.clients = /* @__PURE__ */ new Map();
	}
}, h_ = (e, t, n) => t.clients.forEach((t, r) => {
	let i = e.doc.store.clients.get(r);
	if (i != null) {
		let r = i[i.length - 1], a = r.id.clock + r.length;
		for (let r = 0, o = t[r]; r < t.length && o.clock < a; o = t[++r]) Lv(e, i, o.clock, o.len, n);
	}
}), g_ = (e, t) => {
	let n = 0, r = e.length - 1;
	for (; n <= r;) {
		let i = yp((n + r) / 2), a = e[i], o = a.clock;
		if (o <= t) {
			if (t < o + a.len) return i;
			n = i + 1;
		} else r = i - 1;
	}
	return null;
}, __ = (e, t) => {
	let n = e.clients.get(t.client);
	return n !== void 0 && g_(n, t.clock) !== null;
}, v_ = (e) => {
	e.clients.forEach((e) => {
		e.sort((e, t) => e.clock - t.clock);
		let t, n;
		for (t = 1, n = 1; t < e.length; t++) {
			let r = e[n - 1], i = e[t];
			r.clock + r.len >= i.clock ? e[n - 1] = new p_(r.clock, Sp(r.len, i.clock + i.len - r.clock)) : (n < t && (e[n] = i), n++);
		}
		e.length = n;
	});
}, y_ = (e) => {
	let t = new m_();
	for (let n = 0; n < e.length; n++) e[n].clients.forEach((r, i) => {
		if (!t.clients.has(i)) {
			let a = r.slice();
			for (let t = n + 1; t < e.length; t++) dp(a, e[t].clients.get(i) || []);
			t.clients.set(i, a);
		}
	});
	return v_(t), t;
}, b_ = (e, t, n, r) => {
	op(e.clients, t, () => []).push(new p_(n, r));
}, x_ = () => new m_(), S_ = (e) => {
	let t = x_();
	return e.clients.forEach((e, n) => {
		let r = [];
		for (let t = 0; t < e.length; t++) {
			let n = e[t];
			if (n.deleted) {
				let i = n.id.clock, a = n.length;
				if (t + 1 < e.length) for (let n = e[t + 1]; t + 1 < e.length && n.deleted; n = e[++t + 1]) a += n.length;
				r.push(new p_(i, a));
			}
		}
		r.length > 0 && t.clients.set(n, r);
	}), t;
}, C_ = (e, t) => {
	L(e.restEncoder, t.clients.size), fp(t.clients.entries()).sort((e, t) => t[0] - e[0]).forEach(([t, n]) => {
		e.resetDsCurVal(), L(e.restEncoder, t);
		let r = n.length;
		L(e.restEncoder, r);
		for (let t = 0; t < r; t++) {
			let r = n[t];
			e.writeDsClock(r.clock), e.writeDsLen(r.len);
		}
	});
}, w_ = (e) => {
	let t = new m_(), n = z(e.restDecoder);
	for (let r = 0; r < n; r++) {
		e.resetDsCurVal();
		let n = z(e.restDecoder), r = z(e.restDecoder);
		if (r > 0) {
			let i = op(t.clients, n, () => []);
			for (let t = 0; t < r; t++) i.push(new p_(e.readDsClock(), e.readDsLen()));
		}
	}
	return t;
}, T_ = (e, t, n) => {
	let r = new m_(), i = z(e.restDecoder);
	for (let a = 0; a < i; a++) {
		e.resetDsCurVal();
		let i = z(e.restDecoder), a = z(e.restDecoder), o = n.clients.get(i) || [], s = H(n, i);
		for (let n = 0; n < a; n++) {
			let n = e.readDsClock(), a = n + e.readDsLen();
			if (n < s) {
				s < a && b_(r, i, s, a - s);
				let e = jv(o, n), c = o[e];
				for (!c.deleted && c.id.clock < n && (o.splice(e + 1, 0, Zb(t, c, n - c.id.clock)), e++); e < o.length && (c = o[e++], c.id.clock < a);) c.deleted || (a < c.id.clock + c.length && o.splice(e, 0, Zb(t, c, a - c.id.clock)), c.delete(t));
			} else b_(r, i, n, a - n);
		}
	}
	if (r.clients.size > 0) {
		let e = new F_();
		return L(e.restEncoder, 0), C_(e, r), e.toUint8Array();
	}
	return null;
}, E_ = Im, D_ = class e extends _p {
	constructor({ guid: e = zm(), collectionid: t = null, gc: n = !0, gcFilter: r = () => !0, meta: i = null, autoLoad: a = !1, shouldLoad: o = !0 } = {}) {
		super(), this.gc = n, this.gcFilter = r, this.clientID = E_(), this.guid = e, this.collectionid = t, this.share = /* @__PURE__ */ new Map(), this.store = new Ov(), this._transaction = null, this._transactionCleanups = [], this.subdocs = /* @__PURE__ */ new Set(), this._item = null, this.shouldLoad = o, this.autoLoad = a, this.meta = i, this.isLoaded = !1, this.isSynced = !1, this.isDestroyed = !1, this.whenLoaded = Vm((e) => {
			this.on("load", () => {
				this.isLoaded = !0, e(this);
			});
		});
		let s = () => Vm((e) => {
			let t = (n) => {
				(n === void 0 || n === !0) && (this.off("sync", t), e());
			};
			this.on("sync", t);
		});
		this.on("sync", (e) => {
			e === !1 && this.isSynced && (this.whenSynced = s()), this.isSynced = e === void 0 || e === !0, this.isSynced && !this.isLoaded && this.emit("load", [this]);
		}), this.whenSynced = s();
	}
	load() {
		let e = this._item;
		e !== null && !this.shouldLoad && U(e.parent.doc, (e) => {
			e.subdocsLoaded.add(this);
		}, null, !0), this.shouldLoad = !0;
	}
	getSubdocs() {
		return this.subdocs;
	}
	getSubdocGuids() {
		return new Set(fp(this.subdocs).map((e) => e.guid));
	}
	transact(e, t = null) {
		return U(this, e, t);
	}
	get(e, t = W) {
		let n = op(this.share, e, () => {
			let e = new t();
			return e._integrate(this, null), e;
		}), r = n.constructor;
		if (t !== W && r !== t) {
			if (r === W) {
				let r = new t();
				r._map = n._map, n._map.forEach((e) => {
					for (; e !== null; e = e.left) e.parent = r;
				}), r._start = n._start;
				for (let e = r._start; e !== null; e = e.right) e.parent = r;
				return r._length = n._length, this.share.set(e, r), r._integrate(this, null), r;
			}
			throw Error(`Type with the name ${e} has already been defined with a different constructor`);
		}
		return n;
	}
	getArray(e = "") {
		return this.get(e, By);
	}
	getText(e = "") {
		return this.get(e, sb);
	}
	getMap(e = "") {
		return this.get(e, Uy);
	}
	getXmlElement(e = "") {
		return this.get(e, G);
	}
	getXmlFragment(e = "") {
		return this.get(e, ub);
	}
	toJSON() {
		let e = {};
		return this.share.forEach((t, n) => {
			e[n] = t.toJSON();
		}), e;
	}
	destroy() {
		this.isDestroyed = !0, fp(this.subdocs).forEach((e) => e.destroy());
		let t = this._item;
		if (t !== null) {
			this._item = null;
			let n = t.content;
			n.doc = new e({
				guid: this.guid,
				...n.opts,
				shouldLoad: !1
			}), n.doc._item = t, U(t.parent.doc, (e) => {
				let r = n.doc;
				t.deleted || e.subdocsAdded.add(r), e.subdocsRemoved.add(this);
			}, null, !0);
		}
		this.emit("destroyed", [!0]), this.emit("destroy", [this]), super.destroy();
	}
}, O_ = class {
	constructor(e) {
		this.restDecoder = e;
	}
	resetDsCurVal() {}
	readDsClock() {
		return z(this.restDecoder);
	}
	readDsLen() {
		return z(this.restDecoder);
	}
}, k_ = class extends O_ {
	readLeftID() {
		return V(z(this.restDecoder), z(this.restDecoder));
	}
	readRightID() {
		return V(z(this.restDecoder), z(this.restDecoder));
	}
	readClient() {
		return z(this.restDecoder);
	}
	readInfo() {
		return Cm(this.restDecoder);
	}
	readString() {
		return Tm(this.restDecoder);
	}
	readParentInfo() {
		return z(this.restDecoder) === 1;
	}
	readTypeRef() {
		return z(this.restDecoder);
	}
	readLen() {
		return z(this.restDecoder);
	}
	readAny() {
		return km(this.restDecoder);
	}
	readBuf() {
		return bh(Sm(this.restDecoder));
	}
	readJSON() {
		return JSON.parse(Tm(this.restDecoder));
	}
	readKey() {
		return Tm(this.restDecoder);
	}
}, A_ = class {
	constructor(e) {
		this.dsCurrVal = 0, this.restDecoder = e;
	}
	resetDsCurVal() {
		this.dsCurrVal = 0;
	}
	readDsClock() {
		return this.dsCurrVal += z(this.restDecoder), this.dsCurrVal;
	}
	readDsLen() {
		let e = z(this.restDecoder) + 1;
		return this.dsCurrVal += e, e;
	}
}, j_ = class extends A_ {
	constructor(e) {
		super(e), this.keys = [], z(e), this.keyClockDecoder = new Mm(Sm(e)), this.clientDecoder = new jm(Sm(e)), this.leftClockDecoder = new Mm(Sm(e)), this.rightClockDecoder = new Mm(Sm(e)), this.infoDecoder = new Am(Sm(e), Cm), this.stringDecoder = new Nm(Sm(e)), this.parentInfoDecoder = new Am(Sm(e), Cm), this.typeRefDecoder = new jm(Sm(e)), this.lenDecoder = new jm(Sm(e));
	}
	readLeftID() {
		return new iv(this.clientDecoder.read(), this.leftClockDecoder.read());
	}
	readRightID() {
		return new iv(this.clientDecoder.read(), this.rightClockDecoder.read());
	}
	readClient() {
		return this.clientDecoder.read();
	}
	readInfo() {
		return this.infoDecoder.read();
	}
	readString() {
		return this.stringDecoder.read();
	}
	readParentInfo() {
		return this.parentInfoDecoder.read() === 1;
	}
	readTypeRef() {
		return this.typeRefDecoder.read();
	}
	readLen() {
		return this.lenDecoder.read();
	}
	readAny() {
		return km(this.restDecoder);
	}
	readBuf() {
		return Sm(this.restDecoder);
	}
	readJSON() {
		return km(this.restDecoder);
	}
	readKey() {
		let e = this.keyClockDecoder.read();
		if (e < this.keys.length) return this.keys[e];
		{
			let e = this.stringDecoder.read();
			return this.keys.push(e), e;
		}
	}
}, M_ = class {
	constructor() {
		this.restEncoder = Bp();
	}
	toUint8Array() {
		return Up(this.restEncoder);
	}
	resetDsCurVal() {}
	writeDsClock(e) {
		L(this.restEncoder, e);
	}
	writeDsLen(e) {
		L(this.restEncoder, e);
	}
}, N_ = class extends M_ {
	writeLeftID(e) {
		L(this.restEncoder, e.client), L(this.restEncoder, e.clock);
	}
	writeRightID(e) {
		L(this.restEncoder, e.client), L(this.restEncoder, e.clock);
	}
	writeClient(e) {
		L(this.restEncoder, e);
	}
	writeInfo(e) {
		Kp(this.restEncoder, e);
	}
	writeString(e) {
		Xp(this.restEncoder, e);
	}
	writeParentInfo(e) {
		L(this.restEncoder, +!!e);
	}
	writeTypeRef(e) {
		L(this.restEncoder, e);
	}
	writeLen(e) {
		L(this.restEncoder, e);
	}
	writeAny(e) {
		am(this.restEncoder, e);
	}
	writeBuf(e) {
		Qp(this.restEncoder, e);
	}
	writeJSON(e) {
		Xp(this.restEncoder, JSON.stringify(e));
	}
	writeKey(e) {
		Xp(this.restEncoder, e);
	}
}, P_ = class {
	constructor() {
		this.restEncoder = Bp(), this.dsCurrVal = 0;
	}
	toUint8Array() {
		return Up(this.restEncoder);
	}
	resetDsCurVal() {
		this.dsCurrVal = 0;
	}
	writeDsClock(e) {
		let t = e - this.dsCurrVal;
		this.dsCurrVal = e, L(this.restEncoder, t);
	}
	writeDsLen(e) {
		e === 0 && R(), L(this.restEncoder, e - 1), this.dsCurrVal += e;
	}
}, F_ = class extends P_ {
	constructor() {
		super(), this.keyMap = /* @__PURE__ */ new Map(), this.keyClock = 0, this.keyClockEncoder = new um(), this.clientEncoder = new cm(), this.leftClockEncoder = new um(), this.rightClockEncoder = new um(), this.infoEncoder = new om(Kp), this.stringEncoder = new dm(), this.parentInfoEncoder = new om(Kp), this.typeRefEncoder = new cm(), this.lenEncoder = new cm();
	}
	toUint8Array() {
		let e = Bp();
		return L(e, 0), Qp(e, this.keyClockEncoder.toUint8Array()), Qp(e, this.clientEncoder.toUint8Array()), Qp(e, this.leftClockEncoder.toUint8Array()), Qp(e, this.rightClockEncoder.toUint8Array()), Qp(e, Up(this.infoEncoder)), Qp(e, this.stringEncoder.toUint8Array()), Qp(e, Up(this.parentInfoEncoder)), Qp(e, this.typeRefEncoder.toUint8Array()), Qp(e, this.lenEncoder.toUint8Array()), Zp(e, Up(this.restEncoder)), Up(e);
	}
	writeLeftID(e) {
		this.clientEncoder.write(e.client), this.leftClockEncoder.write(e.clock);
	}
	writeRightID(e) {
		this.clientEncoder.write(e.client), this.rightClockEncoder.write(e.clock);
	}
	writeClient(e) {
		this.clientEncoder.write(e);
	}
	writeInfo(e) {
		this.infoEncoder.write(e);
	}
	writeString(e) {
		this.stringEncoder.write(e);
	}
	writeParentInfo(e) {
		this.parentInfoEncoder.write(+!!e);
	}
	writeTypeRef(e) {
		this.typeRefEncoder.write(e);
	}
	writeLen(e) {
		this.lenEncoder.write(e);
	}
	writeAny(e) {
		am(this.restEncoder, e);
	}
	writeBuf(e) {
		Qp(this.restEncoder, e);
	}
	writeJSON(e) {
		am(this.restEncoder, e);
	}
	writeKey(e) {
		let t = this.keyMap.get(e);
		t === void 0 ? (this.keyClockEncoder.write(this.keyClock++), this.stringEncoder.write(e)) : this.keyClockEncoder.write(t);
	}
}, I_ = (e, t, n, r) => {
	r = Sp(r, t[0].id.clock);
	let i = jv(t, r);
	L(e.restEncoder, t.length - i), e.writeClient(n), L(e.restEncoder, r);
	let a = t[i];
	a.write(e, r - a.id.clock);
	for (let n = i + 1; n < t.length; n++) t[n].write(e, 0);
}, L_ = (e, t, n) => {
	let r = /* @__PURE__ */ new Map();
	n.forEach((e, n) => {
		H(t, n) > e && r.set(n, e);
	}), kv(t).forEach((e, t) => {
		n.has(t) || r.set(t, 0);
	}), L(e.restEncoder, r.size), fp(r.entries()).sort((e, t) => t[0] - e[0]).forEach(([n, r]) => {
		I_(e, t.clients.get(n), n, r);
	});
}, R_ = (e, t) => {
	let n = ip(), r = z(e.restDecoder);
	for (let i = 0; i < r; i++) {
		let r = z(e.restDecoder), i = Array(r), a = e.readClient(), o = z(e.restDecoder);
		n.set(a, {
			i: 0,
			refs: i
		});
		for (let n = 0; n < r; n++) {
			let r = e.readInfo();
			switch (31 & r) {
				case 0: {
					let t = e.readLen();
					i[n] = new bb(V(a, o), t), o += t;
					break;
				}
				case 10: {
					let t = z(e.restDecoder);
					i[n] = new rx(V(a, o), t), o += t;
					break;
				}
				default: {
					let s = !(r & 192), c = new K(V(a, o), null, (r & 128) == 128 ? e.readLeftID() : null, null, (r & 64) == 64 ? e.readRightID() : null, s ? e.readParentInfo() ? t.get(e.readString()) : e.readLeftID() : null, s && (r & 32) == 32 ? e.readString() : null, ex(e, r));
					i[n] = c, o += c.length;
				}
			}
		}
	}
	return n;
}, z_ = (e, t, n) => {
	let r = [], i = fp(n.keys()).sort((e, t) => e - t);
	if (i.length === 0) return null;
	let a = () => {
		if (i.length === 0) return null;
		let e = n.get(i[i.length - 1]);
		for (; e.refs.length === e.i;) if (i.pop(), i.length > 0) e = n.get(i[i.length - 1]);
		else return null;
		return e;
	}, o = a();
	if (o === null) return null;
	let s = new Ov(), c = /* @__PURE__ */ new Map(), l = (e, t) => {
		let n = c.get(e);
		(n == null || n > t) && c.set(e, t);
	}, u = o.refs[o.i++], d = /* @__PURE__ */ new Map(), f = () => {
		for (let e of r) {
			let t = e.id.client, r = n.get(t);
			r ? (r.i--, s.clients.set(t, r.refs.slice(r.i)), n.delete(t), r.i = 0, r.refs = []) : s.clients.set(t, [e]), i = i.filter((e) => e !== t);
		}
		r.length = 0;
	};
	for (;;) {
		if (u.constructor !== rx) {
			let i = op(d, u.id.client, () => H(t, u.id.client)) - u.id.clock;
			if (i < 0) r.push(u), l(u.id.client, u.id.clock - 1), f();
			else {
				let a = u.getMissing(e, t);
				if (a !== null) {
					r.push(u);
					let e = n.get(a) || {
						refs: [],
						i: 0
					};
					if (e.refs.length === e.i) l(a, H(t, a)), f();
					else {
						u = e.refs[e.i++];
						continue;
					}
				} else (i === 0 || i < u.length) && (u.integrate(e, i), d.set(u.id.client, u.id.clock + u.length));
			}
		}
		if (r.length > 0) u = r.pop();
		else if (o !== null && o.i < o.refs.length) u = o.refs[o.i++];
		else {
			if (o = a(), o === null) break;
			u = o.refs[o.i++];
		}
	}
	if (s.clients.size > 0) {
		let e = new F_();
		return L_(e, s, /* @__PURE__ */ new Map()), L(e.restEncoder, 0), {
			missing: c,
			update: e.toUint8Array()
		};
	}
	return null;
}, B_ = (e, t) => L_(e, t.doc.store, t.beforeState), V_ = (e, t, n, r = new j_(e)) => U(t, (e) => {
	e.local = !1;
	let t = !1, n = e.doc, i = n.store, a = z_(e, i, R_(r, n)), o = i.pendingStructs;
	if (o) {
		for (let [e, n] of o.missing) if (n < H(i, e)) {
			t = !0;
			break;
		}
		if (a) {
			for (let [e, t] of a.missing) {
				let n = o.missing.get(e);
				(n == null || n > t) && o.missing.set(e, t);
			}
			o.update = ey([o.update, a.update]);
		}
	} else i.pendingStructs = a;
	let s = T_(r, e, i);
	if (i.pendingDs) {
		let t = new j_(ym(i.pendingDs));
		z(t.restDecoder);
		let n = T_(t, e, i);
		i.pendingDs = s && n ? ey([s, n]) : s || n;
	} else i.pendingDs = s;
	if (t) {
		let t = i.pendingStructs.update;
		i.pendingStructs = null, H_(e.doc, t);
	}
}, n, !1), H_ = (e, t, n, r = j_) => {
	let i = ym(t);
	V_(i, e, n, new r(i));
}, U_ = (e, t, n) => H_(e, t, n, k_), W_ = (e, t, n = /* @__PURE__ */ new Map()) => {
	L_(e, t.store, n), C_(e, S_(t.store));
}, G_ = (e, t = new Uint8Array([0]), n = new F_()) => {
	W_(n, e, J_(t));
	let r = [n.toUint8Array()];
	if (e.store.pendingDs && r.push(e.store.pendingDs), e.store.pendingStructs && r.push(ty(e.store.pendingStructs.update, t)), r.length > 1) {
		if (n.constructor === N_) return Qv(r.map((e, t) => t === 0 ? e : oy(e)));
		if (n.constructor === F_) return ey(r);
	}
	return r[0];
}, K_ = (e, t) => G_(e, t, new N_()), q_ = (e) => {
	let t = /* @__PURE__ */ new Map(), n = z(e.restDecoder);
	for (let r = 0; r < n; r++) {
		let n = z(e.restDecoder), r = z(e.restDecoder);
		t.set(n, r);
	}
	return t;
}, J_ = (e) => q_(new O_(ym(e))), Y_ = (e, t) => (L(e.restEncoder, t.size), fp(t.entries()).sort((e, t) => t[0] - e[0]).forEach(([t, n]) => {
	L(e.restEncoder, t), L(e.restEncoder, n);
}), e), X_ = (e, t) => Y_(e, kv(t.store)), Z_ = (e, t = new P_()) => (e instanceof Map ? Y_(t, e) : X_(t, e), t.toUint8Array()), Q_ = (e) => Z_(e, new M_()), $_ = class {
	constructor() {
		this.l = [];
	}
}, ev = () => new $_(), tv = (e, t) => e.l.push(t), nv = (e, t) => {
	let n = e.l, r = n.length;
	e.l = n.filter((e) => t !== e), r === e.l.length && console.error("[yjs] Tried to remove event handler that doesn't exist.");
}, rv = (e, t, n) => ih(e.l, [t, n]), iv = class {
	constructor(e, t) {
		this.client = e, this.clock = t;
	}
}, av = (e, t) => e === t || e !== null && t !== null && e.client === t.client && e.clock === t.clock, V = (e, t) => new iv(e, t), ov = (e, t) => {
	L(e, t.client), L(e, t.clock);
}, sv = (e) => V(z(e), z(e)), cv = (e) => {
	for (let [t, n] of e.doc.share.entries()) if (n === e) return t;
	throw R();
}, lv = (e, t) => {
	for (; t !== null;) {
		if (t.parent === e) return !0;
		t = t.parent._item;
	}
	return !1;
}, uv = class {
	constructor(e, t, n, r = 0) {
		this.type = e, this.tname = t, this.item = n, this.assoc = r;
	}
}, dv = class {
	constructor(e, t, n = 0) {
		this.type = e, this.index = t, this.assoc = n;
	}
}, fv = (e, t, n = 0) => new dv(e, t, n), pv = (e, t, n) => {
	let r = null, i = null;
	return e._item === null ? i = cv(e) : r = V(e._item.id.client, e._item.id.clock), new uv(r, i, t, n);
}, mv = (e, t, n = 0) => {
	let r = e._start;
	if (n < 0) {
		if (t === 0) return pv(e, null, n);
		t--;
	}
	for (; r !== null;) {
		if (!r.deleted && r.countable) {
			if (r.length > t) return pv(e, V(r.id.client, r.id.clock + t), n);
			t -= r.length;
		}
		if (r.right === null && n < 0) return pv(e, r.lastId, n);
		r = r.right;
	}
	return pv(e, null, n);
}, hv = (e, t) => {
	let { type: n, tname: r, item: i, assoc: a } = t;
	if (i !== null) L(e, 0), ov(e, i);
	else if (r !== null) Kp(e, 1), Xp(e, r);
	else if (n !== null) Kp(e, 2), ov(e, n);
	else throw R();
	return qp(e, a), e;
}, gv = (e) => {
	let t = Bp();
	return hv(t, e), Up(t);
}, _v = (e) => {
	let t = null, n = null, r = null;
	switch (z(e)) {
		case 0:
			r = sv(e);
			break;
		case 1:
			n = Tm(e);
			break;
		case 2: t = sv(e);
	}
	let i = bm(e) ? wm(e) : 0;
	return new uv(t, n, r, i);
}, vv = (e) => _v(ym(e)), yv = (e, t) => {
	let n = Mv(e, t);
	return {
		item: n,
		diff: t.clock - n.id.clock
	};
}, bv = (e, t, n = !0) => {
	let r = t.store, i = e.item, a = e.type, o = e.tname, s = e.assoc, c = null, l = 0;
	if (i !== null) {
		if (H(r, i.client) <= i.clock) return null;
		let e = n ? Yb(r, i) : yv(r, i), t = e.item;
		if (!(t instanceof K)) return null;
		if (c = t.parent, c._item === null || !c._item.deleted) {
			l = t.deleted || !t.countable ? 0 : e.diff + (s >= 0 ? 0 : 1);
			let n = t.left;
			for (; n !== null;) !n.deleted && n.countable && (l += n.length), n = n.left;
		}
	} else {
		if (o !== null) c = t.get(o);
		else if (a !== null) {
			if (H(r, a.client) <= a.clock) return null;
			let { item: e } = n ? Yb(r, a) : { item: Mv(r, a) };
			if (e instanceof K && e.content instanceof qb) c = e.content.type;
			else return null;
		} else throw R();
		l = s >= 0 ? c._length : 0;
	}
	return fv(c, l, e.assoc);
}, xv = (e, t) => e === t || e !== null && t !== null && e.tname === t.tname && av(e.item, t.item) && av(e.type, t.type) && e.assoc === t.assoc, Sv = class {
	constructor(e, t) {
		this.ds = e, this.sv = t;
	}
}, Cv = (e, t) => new Sv(e, t), wv = Cv(x_(), /* @__PURE__ */ new Map()), Tv = (e) => Cv(S_(e.store), kv(e.store)), Ev = (e, t) => t === void 0 ? !e.deleted : t.sv.has(e.id.client) && (t.sv.get(e.id.client) || 0) > e.id.clock && !__(t.ds, e.id), Dv = (e, t) => {
	let n = op(e.meta, Dv, lp), r = e.doc.store;
	n.has(t) || (t.sv.forEach((t, n) => {
		t < H(r, n) && Pv(e, V(n, t));
	}), h_(e, t.ds, (e) => {}), n.add(t));
}, Ov = class {
	constructor() {
		this.clients = /* @__PURE__ */ new Map(), this.pendingStructs = null, this.pendingDs = null;
	}
}, kv = (e) => {
	let t = /* @__PURE__ */ new Map();
	return e.clients.forEach((e, n) => {
		let r = e[e.length - 1];
		t.set(n, r.id.clock + r.length);
	}), t;
}, H = (e, t) => {
	let n = e.clients.get(t);
	if (n === void 0) return 0;
	let r = n[n.length - 1];
	return r.id.clock + r.length;
}, Av = (e, t) => {
	let n = e.clients.get(t.id.client);
	if (n === void 0) n = [], e.clients.set(t.id.client, n);
	else {
		let e = n[n.length - 1];
		if (e.id.clock + e.length !== t.id.clock) throw R();
	}
	n.push(t);
}, jv = (e, t) => {
	let n = 0, r = e.length - 1, i = e[r], a = i.id.clock;
	if (a === t) return r;
	let o = yp(t / (a + i.length - 1) * r);
	for (; n <= r;) {
		if (i = e[o], a = i.id.clock, a <= t) {
			if (t < a + i.length) return o;
			n = o + 1;
		} else r = o - 1;
		o = yp((n + r) / 2);
	}
	throw R();
}, Mv = (e, t) => {
	let n = e.clients.get(t.client);
	return n[jv(n, t.clock)];
}, Nv = (e, t, n) => {
	let r = jv(t, n), i = t[r];
	return i.id.clock < n && i instanceof K ? (t.splice(r + 1, 0, Zb(e, i, n - i.id.clock)), r + 1) : r;
}, Pv = (e, t) => {
	let n = e.doc.store.clients.get(t.client);
	return n[Nv(e, n, t.clock)];
}, Fv = (e, t, n) => {
	let r = t.clients.get(n.client), i = jv(r, n.clock), a = r[i];
	return n.clock !== a.id.clock + a.length - 1 && a.constructor !== bb && r.splice(i + 1, 0, Zb(e, a, n.clock - a.id.clock + 1)), a;
}, Iv = (e, t, n) => {
	let r = e.clients.get(t.id.client);
	r[jv(r, t.id.clock)] = n;
}, Lv = (e, t, n, r, i) => {
	if (r === 0) return;
	let a = n + r, o = Nv(e, t, n), s;
	do
		s = t[o++], a < s.id.clock + s.length && Nv(e, t, a), i(s);
	while (o < t.length && t[o].id.clock < a);
}, Rv = class {
	constructor(e, t, n) {
		this.doc = e, this.deleteSet = new m_(), this.beforeState = kv(e.store), this.afterState = /* @__PURE__ */ new Map(), this.changed = /* @__PURE__ */ new Map(), this.changedParentTypes = /* @__PURE__ */ new Map(), this._mergeStructs = [], this.origin = t, this.meta = /* @__PURE__ */ new Map(), this.local = n, this.subdocsAdded = /* @__PURE__ */ new Set(), this.subdocsRemoved = /* @__PURE__ */ new Set(), this.subdocsLoaded = /* @__PURE__ */ new Set(), this._needFormattingCleanup = !1;
	}
}, zv = (e, t) => t.deleteSet.clients.size === 0 && !cp(t.afterState, (e, n) => t.beforeState.get(n) !== e) ? !1 : (v_(t.deleteSet), B_(e, t), C_(e, t.deleteSet), !0), Bv = (e, t, n) => {
	let r = t._item;
	(r === null || r.id.clock < (e.beforeState.get(r.id.client) || 0) && !r.deleted) && op(e.changed, t, lp).add(n);
}, Vv = (e, t) => {
	let n = e[t], r = e[t - 1], i = t;
	for (; i > 0; n = r, r = e[--i - 1]) {
		if (r.deleted === n.deleted && r.constructor === n.constructor && r.mergeWith(n)) {
			n instanceof K && n.parentSub !== null && n.parent._map.get(n.parentSub) === n && n.parent._map.set(n.parentSub, r);
			continue;
		}
		break;
	}
	let a = t - i;
	return a && e.splice(t + 1 - a, a), a;
}, Hv = (e, t, n) => {
	for (let [r, i] of e.clients.entries()) {
		let e = t.clients.get(r);
		for (let r = i.length - 1; r >= 0; r--) {
			let a = i[r], o = a.clock + a.len;
			for (let r = jv(e, a.clock), i = e[r]; r < e.length && i.id.clock < o; i = e[++r]) {
				let i = e[r];
				if (a.clock + a.len <= i.id.clock) break;
				i instanceof K && i.deleted && !i.keep && n(i) && i.gc(t, !1);
			}
		}
	}
}, Uv = (e, t) => {
	e.clients.forEach((e, n) => {
		let r = t.clients.get(n);
		for (let t = e.length - 1; t >= 0; t--) {
			let n = e[t], i = xp(r.length - 1, 1 + jv(r, n.clock + n.len - 1));
			for (let e = i, t = r[e]; e > 0 && t.id.clock >= n.clock; t = r[e]) e -= 1 + Vv(r, e);
		}
	});
}, Wv = (e, t) => {
	if (t < e.length) {
		let n = e[t], r = n.doc, i = r.store, a = n.deleteSet, o = n._mergeStructs;
		try {
			v_(a), n.afterState = kv(n.doc.store), r.emit("beforeObserverCalls", [n, r]);
			let e = [];
			n.changed.forEach((t, r) => e.push(() => {
				(r._item === null || !r._item.deleted) && r._callObserver(n, t);
			})), e.push(() => {
				n.changedParentTypes.forEach((t, r) => {
					r._dEH.l.length > 0 && (r._item === null || !r._item.deleted) && e.push(() => {
						t = t.filter((e) => e.target._item === null || !e.target._item.deleted), t.forEach((e) => {
							e.currentTarget = r, e._path = null;
						}), t.sort((e, t) => e.path.length - t.path.length), rv(r._dEH, t, n);
					});
				}), e.push(() => r.emit("afterTransaction", [n, r])), e.push(() => {
					n._needFormattingCleanup && ib(n);
				});
			}), ih(e, []);
		} finally {
			r.gc && Hv(a, i, r.gcFilter), Uv(a, i), n.afterState.forEach((e, t) => {
				let r = n.beforeState.get(t) || 0;
				if (r !== e) {
					let e = i.clients.get(t), n = Sp(jv(e, r), 1);
					for (let t = e.length - 1; t >= n;) t -= 1 + Vv(e, t);
				}
			});
			for (let e = o.length - 1; e >= 0; e--) {
				let { client: t, clock: n } = o[e].id, r = i.clients.get(t), a = jv(r, n);
				a + 1 < r.length && Vv(r, a + 1) > 1 || a > 0 && Vv(r, a);
			}
			if (!n.local && n.afterState.get(r.clientID) !== n.beforeState.get(r.clientID) && (s_(n_, Yg, "[yjs] ", Xg, e_, "Changed the client-id because another client seems to be using it."), r.clientID = E_()), r.emit("afterTransactionCleanup", [n, r]), r._observers.has("update")) {
				let e = new N_();
				zv(e, n) && r.emit("update", [
					e.toUint8Array(),
					n.origin,
					r,
					n
				]);
			}
			if (r._observers.has("updateV2")) {
				let e = new F_();
				zv(e, n) && r.emit("updateV2", [
					e.toUint8Array(),
					n.origin,
					r,
					n
				]);
			}
			let { subdocsAdded: s, subdocsLoaded: c, subdocsRemoved: l } = n;
			(s.size > 0 || l.size > 0 || c.size > 0) && (s.forEach((e) => {
				e.clientID = r.clientID, e.collectionid ?? (e.collectionid = r.collectionid), r.subdocs.add(e);
			}), l.forEach((e) => r.subdocs.delete(e)), r.emit("subdocs", [
				{
					loaded: c,
					added: s,
					removed: l
				},
				r,
				n
			]), l.forEach((e) => e.destroy())), e.length <= t + 1 ? (r._transactionCleanups = [], r.emit("afterAllTransactions", [r, e])) : Wv(e, t + 1);
		}
	}
}, U = (e, t, n = null, r = !0) => {
	let i = e._transactionCleanups, a = !1, o = null;
	e._transaction === null && (a = !0, e._transaction = new Rv(e, n, r), i.push(e._transaction), i.length === 1 && e.emit("beforeAllTransactions", [e]), e.emit("beforeTransaction", [e._transaction, e]));
	try {
		o = t(e._transaction);
	} finally {
		if (a) {
			let t = e._transaction === i[0];
			e._transaction = null, t && Wv(i, 0);
		}
	}
	return o;
}, Gv = class {
	constructor(e, t) {
		this.insertions = t, this.deletions = e, this.meta = /* @__PURE__ */ new Map();
	}
}, Kv = (e, t, n) => {
	h_(e, n.deletions, (n) => {
		n instanceof K && t.scope.some((t) => t === e.doc || lv(t, n)) && Xb(n, !1);
	});
}, qv = (e, t, n) => {
	let r = null, i = e.doc, a = e.scope;
	U(i, (n) => {
		for (; t.length > 0 && e.currStackItem === null;) {
			let r = i.store, o = t.pop(), s = /* @__PURE__ */ new Set(), c = [], l = !1;
			h_(n, o.insertions, (e) => {
				if (e instanceof K) {
					if (e.redone !== null) {
						let { item: t, diff: i } = Yb(r, e.id);
						i > 0 && (t = Pv(n, V(t.id.client, t.id.clock + i))), e = t;
					}
					!e.deleted && a.some((t) => t === n.doc || lv(t, e)) && c.push(e);
				}
			}), h_(n, o.deletions, (e) => {
				e instanceof K && a.some((t) => t === n.doc || lv(t, e)) && !__(o.insertions, e.id) && s.add(e);
			}), s.forEach((t) => {
				l = $b(n, t, s, o.insertions, e.ignoreRemoteMapChanges, e) !== null || l;
			});
			for (let t = c.length - 1; t >= 0; t--) {
				let r = c[t];
				e.deleteFilter(r) && (r.delete(n), l = !0);
			}
			e.currStackItem = l ? o : null;
		}
		n.changed.forEach((e, t) => {
			e.has(null) && t._searchMarker && (t._searchMarker.length = 0);
		}), r = n;
	}, e);
	let o = e.currStackItem;
	if (o != null) {
		let t = r.changedParentTypes;
		e.emit("stack-item-popped", [{
			stackItem: o,
			type: n,
			changedParentTypes: t,
			origin: e
		}, e]), e.currStackItem = null;
	}
	return o;
}, Jv = class extends _p {
	constructor(e, { captureTimeout: t = 500, captureTransaction: n = (e) => !0, deleteFilter: r = () => !0, trackedOrigins: i = /* @__PURE__ */ new Set([null]), ignoreRemoteMapChanges: a = !1, doc: o = gp(e) ? e[0].doc : e instanceof D_ ? e : e.doc } = {}) {
		super(), this.scope = [], this.doc = o, this.addToScope(e), this.deleteFilter = r, i.add(this), this.trackedOrigins = i, this.captureTransaction = n, this.undoStack = [], this.redoStack = [], this.undoing = !1, this.redoing = !1, this.currStackItem = null, this.lastChange = 0, this.ignoreRemoteMapChanges = a, this.captureTimeout = t, this.afterTransactionHandler = (e) => {
			if (!this.captureTransaction(e) || !this.scope.some((t) => e.changedParentTypes.has(t) || t === this.doc) || !this.trackedOrigins.has(e.origin) && (!e.origin || !this.trackedOrigins.has(e.origin.constructor))) return;
			let t = this.undoing, n = this.redoing, r = t ? this.redoStack : this.undoStack;
			t ? this.stopCapturing() : n || this.clear(!1, !0);
			let i = new m_();
			e.afterState.forEach((t, n) => {
				let r = e.beforeState.get(n) || 0, a = t - r;
				a > 0 && b_(i, n, r, a);
			});
			let a = Bm(), o = !1;
			if (this.lastChange > 0 && a - this.lastChange < this.captureTimeout && r.length > 0 && !t && !n) {
				let t = r[r.length - 1];
				t.deletions = y_([t.deletions, e.deleteSet]), t.insertions = y_([t.insertions, i]);
			} else r.push(new Gv(e.deleteSet, i)), o = !0;
			!t && !n && (this.lastChange = a), h_(e, e.deleteSet, (t) => {
				t instanceof K && this.scope.some((n) => n === e.doc || lv(n, t)) && Xb(t, !0);
			});
			let s = [{
				stackItem: r[r.length - 1],
				origin: e.origin,
				type: t ? "redo" : "undo",
				changedParentTypes: e.changedParentTypes
			}, this];
			o ? this.emit("stack-item-added", s) : this.emit("stack-item-updated", s);
		}, this.destroy = this.destroy.bind(this), this.doc.on("afterTransaction", this.afterTransactionHandler), this.doc.on("destroy", this.destroy);
	}
	addToScope(e) {
		let t = new Set(this.scope);
		e = gp(e) ? e : [e], e.forEach((e) => {
			t.has(e) || (t.add(e), (e instanceof W ? e.doc !== this.doc : e !== this.doc) && c_("[yjs#509] Not same Y.Doc"), this.scope.push(e));
		});
	}
	addTrackedOrigin(e) {
		this.trackedOrigins.add(e);
	}
	removeTrackedOrigin(e) {
		this.trackedOrigins.delete(e);
	}
	clear(e = !0, t = !0) {
		(e && this.canUndo() || t && this.canRedo()) && this.doc.transact((n) => {
			e && (this.undoStack.forEach((e) => Kv(n, this, e)), this.undoStack = []), t && (this.redoStack.forEach((e) => Kv(n, this, e)), this.redoStack = []), this.emit("stack-cleared", [{
				undoStackCleared: e,
				redoStackCleared: t
			}]);
		});
	}
	stopCapturing() {
		this.lastChange = 0;
	}
	undo() {
		this.undoing = !0;
		let e;
		try {
			e = qv(this, this.undoStack, "undo");
		} finally {
			this.undoing = !1;
		}
		return e;
	}
	redo() {
		this.redoing = !0;
		let e;
		try {
			e = qv(this, this.redoStack, "redo");
		} finally {
			this.redoing = !1;
		}
		return e;
	}
	canUndo() {
		return this.undoStack.length > 0;
	}
	canRedo() {
		return this.redoStack.length > 0;
	}
	destroy() {
		this.trackedOrigins.delete(this), this.doc.off("afterTransaction", this.afterTransactionHandler), this.doc.off("destroy", this.destroy), super.destroy();
	}
};
function* Yv(e) {
	let t = z(e.restDecoder);
	for (let n = 0; n < t; n++) {
		let t = z(e.restDecoder), n = e.readClient(), r = z(e.restDecoder);
		for (let i = 0; i < t; i++) {
			let t = e.readInfo();
			if (t === 10) {
				let t = z(e.restDecoder);
				yield new rx(V(n, r), t), r += t;
			} else if (31 & t) {
				let i = !(t & 192), a = new K(V(n, r), null, (t & 128) == 128 ? e.readLeftID() : null, null, (t & 64) == 64 ? e.readRightID() : null, i ? e.readParentInfo() ? e.readString() : e.readLeftID() : null, i && (t & 32) == 32 ? e.readString() : null, ex(e, t));
				yield a, r += a.length;
			} else {
				let t = e.readLen();
				yield new bb(V(n, r), t), r += t;
			}
		}
	}
}
var Xv = class {
	constructor(e, t) {
		this.gen = Yv(e), this.curr = null, this.done = !1, this.filterSkips = t, this.next();
	}
	next() {
		do
			this.curr = this.gen.next().value || null;
		while (this.filterSkips && this.curr !== null && this.curr.constructor === rx);
		return this.curr;
	}
}, Zv = class {
	constructor(e) {
		this.currClient = 0, this.startClock = 0, this.written = 0, this.encoder = e, this.clientStructs = [];
	}
}, Qv = (e) => ey(e, k_, N_), $v = (e, t) => {
	if (e.constructor === bb) {
		let { client: n, clock: r } = e.id;
		return new bb(V(n, r + t), e.length - t);
	}
	if (e.constructor === rx) {
		let { client: n, clock: r } = e.id;
		return new rx(V(n, r + t), e.length - t);
	}
	{
		let n = e, { client: r, clock: i } = n.id;
		return new K(V(r, i + t), null, V(r, i + t - 1), null, n.rightOrigin, n.parent, n.parentSub, n.content.splice(t));
	}
}, ey = (e, t = j_, n = F_) => {
	if (e.length === 1) return e[0];
	let r = e.map((e) => new t(ym(e))), i = r.map((e) => new Xv(e, !0)), a = null, o = new n(), s = new Zv(o);
	for (; i = i.filter((e) => e.curr !== null), i.sort((e, t) => {
		if (e.curr.id.client === t.curr.id.client) {
			let n = e.curr.id.clock - t.curr.id.clock;
			return n === 0 ? e.curr.constructor === t.curr.constructor ? 0 : e.curr.constructor === rx ? 1 : -1 : n;
		}
		return t.curr.id.client - e.curr.id.client;
	}), i.length !== 0;) {
		let e = i[0], t = e.curr.id.client;
		if (a !== null) {
			let n = e.curr, r = !1;
			for (; n !== null && n.id.clock + n.length <= a.struct.id.clock + a.struct.length && n.id.client >= a.struct.id.client;) n = e.next(), r = !0;
			if (n === null || n.id.client !== t || r && n.id.clock > a.struct.id.clock + a.struct.length) continue;
			if (t !== a.struct.id.client) ry(s, a.struct, a.offset), a = {
				struct: n,
				offset: 0
			}, e.next();
			else if (a.struct.id.clock + a.struct.length < n.id.clock) {
				if (a.struct.constructor === rx) a.struct.length = n.id.clock + n.length - a.struct.id.clock;
				else {
					ry(s, a.struct, a.offset);
					let e = n.id.clock - a.struct.id.clock - a.struct.length;
					a = {
						struct: new rx(V(t, a.struct.id.clock + a.struct.length), e),
						offset: 0
					};
				}
			} else {
				let t = a.struct.id.clock + a.struct.length - n.id.clock;
				t > 0 && (a.struct.constructor === rx ? a.struct.length -= t : n = $v(n, t)), a.struct.mergeWith(n) || (ry(s, a.struct, a.offset), a = {
					struct: n,
					offset: 0
				}, e.next());
			}
		} else a = {
			struct: e.curr,
			offset: 0
		}, e.next();
		for (let n = e.curr; n !== null && n.id.client === t && n.id.clock === a.struct.id.clock + a.struct.length && n.constructor !== rx; n = e.next()) ry(s, a.struct, a.offset), a = {
			struct: n,
			offset: 0
		};
	}
	return a !== null && (ry(s, a.struct, a.offset), a = null), iy(s), C_(o, y_(r.map((e) => w_(e)))), o.toUint8Array();
}, ty = (e, t, n = j_, r = F_) => {
	let i = J_(t), a = new r(), o = new Zv(a), s = new n(ym(e)), c = new Xv(s, !1);
	for (; c.curr;) {
		let e = c.curr, t = e.id.client, n = i.get(t) || 0;
		if (c.curr.constructor === rx) {
			c.next();
			continue;
		}
		if (e.id.clock + e.length > n) for (ry(o, e, Sp(n - e.id.clock, 0)), c.next(); c.curr && c.curr.id.client === t;) ry(o, c.curr, 0), c.next();
		else for (; c.curr && c.curr.id.client === t && c.curr.id.clock + c.curr.length <= n;) c.next();
	}
	return iy(o), C_(a, w_(s)), a.toUint8Array();
}, ny = (e) => {
	e.written > 0 && (e.clientStructs.push({
		written: e.written,
		restEncoder: Up(e.encoder.restEncoder)
	}), e.encoder.restEncoder = Bp(), e.written = 0);
}, ry = (e, t, n) => {
	e.written > 0 && e.currClient !== t.id.client && ny(e), e.written === 0 && (e.currClient = t.id.client, e.encoder.writeClient(t.id.client), L(e.encoder.restEncoder, t.id.clock + n)), t.write(e.encoder, n), e.written++;
}, iy = (e) => {
	ny(e);
	let t = e.encoder.restEncoder;
	L(t, e.clientStructs.length);
	for (let n = 0; n < e.clientStructs.length; n++) {
		let r = e.clientStructs[n];
		L(t, r.written), Zp(t, r.restEncoder);
	}
}, ay = (e, t, n, r) => {
	let i = new n(ym(e)), a = new Xv(i, !1), o = new r(), s = new Zv(o);
	for (let e = a.curr; e !== null; e = a.next()) ry(s, t(e), 0);
	return iy(s), C_(o, w_(i)), o.toUint8Array();
}, oy = (e) => ay(e, ah, j_, N_), sy = "You must not compute changes after the event-handler fired.", cy = class {
	constructor(e, t) {
		this.target = e, this.currentTarget = e, this.transaction = t, this._changes = null, this._keys = null, this._delta = null, this._path = null;
	}
	get path() {
		return this._path || (this._path = ly(this.currentTarget, this.target));
	}
	deletes(e) {
		return __(this.transaction.deleteSet, e.id);
	}
	get keys() {
		if (this._keys === null) {
			if (this.transaction.doc._transactionCleanups.length === 0) throw fm(sy);
			let e = /* @__PURE__ */ new Map(), t = this.target;
			this.transaction.changed.get(t).forEach((n) => {
				if (n !== null) {
					let r = t._map.get(n), i, a;
					if (this.adds(r)) {
						let e = r.left;
						for (; e !== null && this.adds(e);) e = e.left;
						if (this.deletes(r)) {
							if (e !== null && this.deletes(e)) i = "delete", a = up(e.content.getContent());
							else return;
						} else e !== null && this.deletes(e) ? (i = "update", a = up(e.content.getContent())) : (i = "add", a = void 0);
					} else if (this.deletes(r)) i = "delete", a = up(r.content.getContent());
					else return;
					e.set(n, {
						action: i,
						oldValue: a
					});
				}
			}), this._keys = e;
		}
		return this._keys;
	}
	get delta() {
		return this.changes.delta;
	}
	adds(e) {
		return e.id.clock >= (this.transaction.beforeState.get(e.id.client) || 0);
	}
	get changes() {
		let e = this._changes;
		if (e === null) {
			if (this.transaction.doc._transactionCleanups.length === 0) throw fm(sy);
			let t = this.target, n = lp(), r = lp(), i = [];
			if (e = {
				added: n,
				deleted: r,
				delta: i,
				keys: this.keys
			}, this.transaction.changed.get(t).has(null)) {
				let e = null, a = () => {
					e && i.push(e);
				};
				for (let i = t._start; i !== null; i = i.right) i.deleted ? this.deletes(i) && !this.adds(i) && ((e === null || e.delete === void 0) && (a(), e = { delete: 0 }), e.delete += i.length, r.add(i)) : this.adds(i) ? ((e === null || e.insert === void 0) && (a(), e = { insert: [] }), e.insert = e.insert.concat(i.content.getContent()), n.add(i)) : ((e === null || e.retain === void 0) && (a(), e = { retain: 0 }), e.retain += i.length);
				e !== null && e.retain === void 0 && a();
			}
			this._changes = e;
		}
		return e;
	}
}, ly = (e, t) => {
	let n = [];
	for (; t._item !== null && t !== e;) {
		if (t._item.parentSub !== null) n.unshift(t._item.parentSub);
		else {
			let e = 0, r = t._item.parent._start;
			for (; r !== t._item && r !== null;) !r.deleted && r.countable && (e += r.length), r = r.right;
			n.unshift(e);
		}
		t = t._item.parent;
	}
	return n;
}, uy = () => {
	c_("Invalid access: Add Yjs type to a document before reading data.");
}, dy = 80, fy = 0, py = class {
	constructor(e, t) {
		e.marker = !0, this.p = e, this.index = t, this.timestamp = fy++;
	}
}, my = (e) => {
	e.timestamp = fy++;
}, hy = (e, t, n) => {
	e.p.marker = !1, e.p = t, t.marker = !0, e.index = n, e.timestamp = fy++;
}, gy = (e, t, n) => {
	if (e.length >= dy) {
		let r = e.reduce((e, t) => e.timestamp < t.timestamp ? e : t);
		return hy(r, t, n), r;
	}
	{
		let r = new py(t, n);
		return e.push(r), r;
	}
}, _y = (e, t) => {
	if (e._start === null || t === 0 || e._searchMarker === null) return null;
	let n = e._searchMarker.length === 0 ? null : e._searchMarker.reduce((e, n) => bp(t - e.index) < bp(t - n.index) ? e : n), r = e._start, i = 0;
	for (n !== null && (r = n.p, i = n.index, my(n)); r.right !== null && i < t;) {
		if (!r.deleted && r.countable) {
			if (t < i + r.length) break;
			i += r.length;
		}
		r = r.right;
	}
	for (; r.left !== null && i > t;) r = r.left, !r.deleted && r.countable && (i -= r.length);
	for (; r.left !== null && r.left.id.client === r.id.client && r.left.id.clock + r.left.length === r.id.clock;) r = r.left, !r.deleted && r.countable && (i -= r.length);
	return n !== null && bp(n.index - i) < r.parent.length / dy ? (hy(n, r, i), n) : gy(e._searchMarker, r, i);
}, vy = (e, t, n) => {
	for (let r = e.length - 1; r >= 0; r--) {
		let i = e[r];
		if (n > 0) {
			let t = i.p;
			for (t.marker = !1; t && (t.deleted || !t.countable);) t = t.left, t && !t.deleted && t.countable && (i.index -= t.length);
			if (t === null || t.marker === !0) {
				e.splice(r, 1);
				continue;
			}
			i.p = t, t.marker = !0;
		}
		(t < i.index || n > 0 && t === i.index) && (i.index = Sp(t, i.index + n));
	}
}, yy = (e, t, n) => {
	let r = e, i = t.changedParentTypes;
	for (; op(i, e, () => []).push(n), e._item !== null;) e = e._item.parent;
	rv(r._eH, n, t);
}, W = class {
	constructor() {
		this._item = null, this._map = /* @__PURE__ */ new Map(), this._start = null, this.doc = null, this._length = 0, this._eH = ev(), this._dEH = ev(), this._searchMarker = null;
	}
	get parent() {
		return this._item ? this._item.parent : null;
	}
	_integrate(e, t) {
		this.doc = e, this._item = t;
	}
	_copy() {
		throw pm();
	}
	clone() {
		throw pm();
	}
	_write(e) {}
	get _first() {
		let e = this._start;
		for (; e !== null && e.deleted;) e = e.right;
		return e;
	}
	_callObserver(e, t) {
		!e.local && this._searchMarker && (this._searchMarker.length = 0);
	}
	observe(e) {
		tv(this._eH, e);
	}
	observeDeep(e) {
		tv(this._dEH, e);
	}
	unobserve(e) {
		nv(this._eH, e);
	}
	unobserveDeep(e) {
		nv(this._dEH, e);
	}
	toJSON() {}
}, by = (e, t, n) => {
	e.doc ?? uy(), t < 0 && (t = e._length + t), n < 0 && (n = e._length + n);
	let r = n - t, i = [], a = e._start;
	for (; a !== null && r > 0;) {
		if (a.countable && !a.deleted) {
			let e = a.content.getContent();
			if (e.length <= t) t -= e.length;
			else {
				for (let n = t; n < e.length && r > 0; n++) i.push(e[n]), r--;
				t = 0;
			}
		}
		a = a.right;
	}
	return i;
}, xy = (e) => {
	e.doc ?? uy();
	let t = [], n = e._start;
	for (; n !== null;) {
		if (n.countable && !n.deleted) {
			let e = n.content.getContent();
			for (let n = 0; n < e.length; n++) t.push(e[n]);
		}
		n = n.right;
	}
	return t;
}, Sy = (e, t) => {
	let n = [], r = e._start;
	for (; r !== null;) {
		if (r.countable && Ev(r, t)) {
			let e = r.content.getContent();
			for (let t = 0; t < e.length; t++) n.push(e[t]);
		}
		r = r.right;
	}
	return n;
}, Cy = (e, t) => {
	let n = 0, r = e._start;
	for (e.doc ?? uy(); r !== null;) {
		if (r.countable && !r.deleted) {
			let i = r.content.getContent();
			for (let r = 0; r < i.length; r++) t(i[r], n++, e);
		}
		r = r.right;
	}
}, wy = (e, t) => {
	let n = [];
	return Cy(e, (r, i) => {
		n.push(t(r, i, e));
	}), n;
}, Ty = (e) => {
	let t = e._start, n = null, r = 0;
	return {
		[Symbol.iterator]() {
			return this;
		},
		next: () => {
			if (n === null) {
				for (; t !== null && t.deleted;) t = t.right;
				if (t === null) return {
					done: !0,
					value: void 0
				};
				n = t.content.getContent(), r = 0, t = t.right;
			}
			let e = n[r++];
			return n.length <= r && (n = null), {
				done: !1,
				value: e
			};
		}
	};
}, Ey = (e, t) => {
	e.doc ?? uy();
	let n = _y(e, t), r = e._start;
	for (n !== null && (r = n.p, t -= n.index); r !== null; r = r.right) if (!r.deleted && r.countable) {
		if (t < r.length) return r.content.getContent()[t];
		t -= r.length;
	}
}, Dy = (e, t, n, r) => {
	let i = n, a = e.doc, o = a.clientID, s = a.store, c = n === null ? t._start : n.right, l = [], u = () => {
		l.length > 0 && (i = new K(V(o, H(s, o)), i, i && i.lastId, c, c && c.id, t, null, new Fb(l)), i.integrate(e, 0), l = []);
	};
	r.forEach((n) => {
		if (n === null) l.push(n);
		else switch (n.constructor) {
			case Number:
			case Object:
			case Boolean:
			case Array:
			case String:
				l.push(n);
				break;
			default: switch (u(), n.constructor) {
				case Uint8Array:
				case ArrayBuffer:
					i = new K(V(o, H(s, o)), i, i && i.lastId, c, c && c.id, t, null, new xb(new Uint8Array(n))), i.integrate(e, 0);
					break;
				case D_:
					i = new K(V(o, H(s, o)), i, i && i.lastId, c, c && c.id, t, null, new Eb(n)), i.integrate(e, 0);
					break;
				default: if (n instanceof W) i = new K(V(o, H(s, o)), i, i && i.lastId, c, c && c.id, t, null, new qb(n)), i.integrate(e, 0);
				else throw Error("Unexpected content type in insert operation");
			}
		}
	}), u();
}, Oy = () => fm("Length exceeded!"), ky = (e, t, n, r) => {
	if (n > t._length) throw Oy();
	if (n === 0) return t._searchMarker && vy(t._searchMarker, n, r.length), Dy(e, t, null, r);
	let i = n, a = _y(t, n), o = t._start;
	for (a !== null && (o = a.p, n -= a.index, n === 0 && (o = o.prev, n += o && o.countable && !o.deleted ? o.length : 0)); o !== null; o = o.right) if (!o.deleted && o.countable) {
		if (n <= o.length) {
			n < o.length && Pv(e, V(o.id.client, o.id.clock + n));
			break;
		}
		n -= o.length;
	}
	return t._searchMarker && vy(t._searchMarker, i, r.length), Dy(e, t, o, r);
}, Ay = (e, t, n) => {
	let r = (t._searchMarker || []).reduce((e, t) => t.index > e.index ? t : e, {
		index: 0,
		p: t._start
	}).p;
	if (r) for (; r.right;) r = r.right;
	return Dy(e, t, r, n);
}, jy = (e, t, n, r) => {
	if (r === 0) return;
	let i = n, a = r, o = _y(t, n), s = t._start;
	for (o !== null && (s = o.p, n -= o.index); s !== null && n > 0; s = s.right) !s.deleted && s.countable && (n < s.length && Pv(e, V(s.id.client, s.id.clock + n)), n -= s.length);
	for (; r > 0 && s !== null;) s.deleted || (r < s.length && Pv(e, V(s.id.client, s.id.clock + r)), s.delete(e), r -= s.length), s = s.right;
	if (r > 0) throw Oy();
	t._searchMarker && vy(t._searchMarker, i, -a + r);
}, My = (e, t, n) => {
	let r = t._map.get(n);
	r !== void 0 && r.delete(e);
}, Ny = (e, t, n, r) => {
	let i = t._map.get(n) || null, a = e.doc, o = a.clientID, s;
	if (r == null) s = new Fb([r]);
	else switch (r.constructor) {
		case Number:
		case Object:
		case Boolean:
		case Array:
		case String:
		case Date:
		case BigInt:
			s = new Fb([r]);
			break;
		case Uint8Array:
			s = new xb(r);
			break;
		case D_:
			s = new Eb(r);
			break;
		default: if (r instanceof W) s = new qb(r);
		else throw Error("Unexpected content type");
	}
	new K(V(o, H(a.store, o)), i, i && i.lastId, null, null, t, n, s).integrate(e, 0);
}, Py = (e, t) => {
	e.doc ?? uy();
	let n = e._map.get(t);
	return n !== void 0 && !n.deleted ? n.content.getContent()[n.length - 1] : void 0;
}, Fy = (e) => {
	let t = {};
	return e.doc ?? uy(), e._map.forEach((e, n) => {
		e.deleted || (t[n] = e.content.getContent()[e.length - 1]);
	}), t;
}, Iy = (e, t) => {
	e.doc ?? uy();
	let n = e._map.get(t);
	return n !== void 0 && !n.deleted;
}, Ly = (e, t) => {
	let n = {};
	return e._map.forEach((e, r) => {
		let i = e;
		for (; i !== null && (!t.sv.has(i.id.client) || i.id.clock >= (t.sv.get(i.id.client) || 0));) i = i.left;
		i !== null && Ev(i, t) && (n[r] = i.content.getContent()[i.length - 1]);
	}), n;
}, Ry = (e) => (e.doc ?? uy(), d_(e._map.entries(), (e) => !e[1].deleted)), zy = class extends cy {}, By = class e extends W {
	constructor() {
		super(), this._prelimContent = [], this._searchMarker = [];
	}
	static from(t) {
		let n = new e();
		return n.push(t), n;
	}
	_integrate(e, t) {
		super._integrate(e, t), this.insert(0, this._prelimContent), this._prelimContent = null;
	}
	_copy() {
		return new e();
	}
	clone() {
		let t = new e();
		return t.insert(0, this.toArray().map((e) => e instanceof W ? e.clone() : e)), t;
	}
	get length() {
		return this.doc ?? uy(), this._length;
	}
	_callObserver(e, t) {
		super._callObserver(e, t), yy(this, e, new zy(this, e));
	}
	insert(e, t) {
		this.doc === null ? this._prelimContent.splice(e, 0, ...t) : U(this.doc, (n) => {
			ky(n, this, e, t);
		});
	}
	push(e) {
		this.doc === null ? this._prelimContent.push(...e) : U(this.doc, (t) => {
			Ay(t, this, e);
		});
	}
	unshift(e) {
		this.insert(0, e);
	}
	delete(e, t = 1) {
		this.doc === null ? this._prelimContent.splice(e, t) : U(this.doc, (n) => {
			jy(n, this, e, t);
		});
	}
	get(e) {
		return Ey(this, e);
	}
	toArray() {
		return xy(this);
	}
	slice(e = 0, t = this.length) {
		return by(this, e, t);
	}
	toJSON() {
		return this.map((e) => e instanceof W ? e.toJSON() : e);
	}
	map(e) {
		return wy(this, e);
	}
	forEach(e) {
		Cy(this, e);
	}
	[Symbol.iterator]() {
		return Ty(this);
	}
	_write(e) {
		e.writeTypeRef(Bb);
	}
}, Vy = (e) => new By(), Hy = class extends cy {
	constructor(e, t, n) {
		super(e, t), this.keysChanged = n;
	}
}, Uy = class e extends W {
	constructor(e) {
		super(), this._prelimContent = null, this._prelimContent = e === void 0 ? /* @__PURE__ */ new Map() : new Map(e);
	}
	_integrate(e, t) {
		super._integrate(e, t), this._prelimContent.forEach((e, t) => {
			this.set(t, e);
		}), this._prelimContent = null;
	}
	_copy() {
		return new e();
	}
	clone() {
		let t = new e();
		return this.forEach((e, n) => {
			t.set(n, e instanceof W ? e.clone() : e);
		}), t;
	}
	_callObserver(e, t) {
		yy(this, e, new Hy(this, e, t));
	}
	toJSON() {
		this.doc ?? uy();
		let e = {};
		return this._map.forEach((t, n) => {
			if (!t.deleted) {
				let r = t.content.getContent()[t.length - 1];
				e[n] = r instanceof W ? r.toJSON() : r;
			}
		}), e;
	}
	get size() {
		return [...Ry(this)].length;
	}
	keys() {
		return f_(Ry(this), (e) => e[0]);
	}
	values() {
		return f_(Ry(this), (e) => e[1].content.getContent()[e[1].length - 1]);
	}
	entries() {
		return f_(Ry(this), (e) => [e[0], e[1].content.getContent()[e[1].length - 1]]);
	}
	forEach(e) {
		this.doc ?? uy(), this._map.forEach((t, n) => {
			t.deleted || e(t.content.getContent()[t.length - 1], n, this);
		});
	}
	[Symbol.iterator]() {
		return this.entries();
	}
	delete(e) {
		this.doc === null ? this._prelimContent.delete(e) : U(this.doc, (t) => {
			My(t, this, e);
		});
	}
	set(e, t) {
		return this.doc === null ? this._prelimContent.set(e, t) : U(this.doc, (n) => {
			Ny(n, this, e, t);
		}), t;
	}
	get(e) {
		return Py(this, e);
	}
	has(e) {
		return Iy(this, e);
	}
	clear() {
		this.doc === null ? this._prelimContent.clear() : U(this.doc, (e) => {
			this.forEach(function(t, n, r) {
				My(e, r, n);
			});
		});
	}
	_write(e) {
		e.writeTypeRef(Vb);
	}
}, Wy = (e) => new Uy(), Gy = (e, t) => e === t || typeof e == "object" && typeof t == "object" && e && t && th(e, t), Ky = class {
	constructor(e, t, n, r) {
		this.left = e, this.right = t, this.index = n, this.currentAttributes = r;
	}
	forward() {
		switch (this.right === null && R(), this.right.content.constructor) {
			case Ab:
				this.right.deleted || Xy(this.currentAttributes, this.right.content);
				break;
			default: this.right.deleted || (this.index += this.right.length);
		}
		this.left = this.right, this.right = this.right.right;
	}
}, qy = (e, t, n) => {
	for (; t.right !== null && n > 0;) {
		switch (t.right.content.constructor) {
			case Ab:
				t.right.deleted || Xy(t.currentAttributes, t.right.content);
				break;
			default: t.right.deleted || (n < t.right.length && Pv(e, V(t.right.id.client, t.right.id.clock + n)), t.index += t.right.length, n -= t.right.length);
		}
		t.left = t.right, t.right = t.right.right;
	}
	return t;
}, Jy = (e, t, n, r) => {
	let i = /* @__PURE__ */ new Map(), a = r ? _y(t, n) : null;
	return a ? qy(e, new Ky(a.p.left, a.p, a.index, i), n - a.index) : qy(e, new Ky(null, t._start, 0, i), n);
}, Yy = (e, t, n, r) => {
	for (; n.right !== null && (n.right.deleted === !0 || n.right.content.constructor === Ab && Gy(r.get(n.right.content.key), n.right.content.value));) n.right.deleted || r.delete(n.right.content.key), n.forward();
	let i = e.doc, a = i.clientID;
	r.forEach((r, o) => {
		let s = n.left, c = n.right, l = new K(V(a, H(i.store, a)), s, s && s.lastId, c, c && c.id, t, null, new Ab(o, r));
		l.integrate(e, 0), n.right = l, n.forward();
	});
}, Xy = (e, t) => {
	let { key: n, value: r } = t;
	r === null ? e.delete(n) : e.set(n, r);
}, Zy = (e, t) => {
	for (; e.right !== null && (e.right.deleted || e.right.content.constructor === Ab && Gy(t[e.right.content.key] ?? null, e.right.content.value));) e.forward();
}, Qy = (e, t, n, r) => {
	let i = e.doc, a = i.clientID, o = /* @__PURE__ */ new Map();
	for (let s in r) {
		let c = r[s], l = n.currentAttributes.get(s) ?? null;
		if (!Gy(l, c)) {
			o.set(s, l);
			let { left: r, right: u } = n;
			n.right = new K(V(a, H(i.store, a)), r, r && r.lastId, u, u && u.id, t, null, new Ab(s, c)), n.right.integrate(e, 0), n.forward();
		}
	}
	return o;
}, $y = (e, t, n, r, i) => {
	n.currentAttributes.forEach((e, t) => {
		i[t] === void 0 && (i[t] = null);
	});
	let a = e.doc, o = a.clientID;
	Zy(n, i);
	let s = Qy(e, t, n, i), c = r.constructor === String ? new Lb(r) : r instanceof W ? new qb(r) : new Ob(r), { left: l, right: u, index: d } = n;
	t._searchMarker && vy(t._searchMarker, n.index, c.getLength()), u = new K(V(o, H(a.store, o)), l, l && l.lastId, u, u && u.id, t, null, c), u.integrate(e, 0), n.right = u, n.index = d, n.forward(), Yy(e, t, n, s);
}, eb = (e, t, n, r, i) => {
	let a = e.doc, o = a.clientID;
	Zy(n, i);
	let s = Qy(e, t, n, i);
	iterationLoop: for (; n.right !== null && (r > 0 || s.size > 0 && (n.right.deleted || n.right.content.constructor === Ab));) {
		if (!n.right.deleted) switch (n.right.content.constructor) {
			case Ab: {
				let { key: t, value: a } = n.right.content, o = i[t];
				if (o !== void 0) {
					if (Gy(o, a)) s.delete(t);
					else {
						if (r === 0) break iterationLoop;
						s.set(t, a);
					}
					n.right.delete(e);
				} else n.currentAttributes.set(t, a);
				break;
			}
			default: r < n.right.length && Pv(e, V(n.right.id.client, n.right.id.clock + r)), r -= n.right.length;
		}
		n.forward();
	}
	if (r > 0) {
		let i = "";
		for (; r > 0; r--) i += "\n";
		n.right = new K(V(o, H(a.store, o)), n.left, n.left && n.left.lastId, n.right, n.right && n.right.id, t, null, new Lb(i)), n.right.integrate(e, 0), n.forward();
	}
	Yy(e, t, n, s);
}, tb = (e, t, n, r, i) => {
	let a = t, o = ip();
	for (; a && (!a.countable || a.deleted);) {
		if (!a.deleted && a.content.constructor === Ab) {
			let e = a.content;
			o.set(e.key, e);
		}
		a = a.right;
	}
	let s = 0, c = !1;
	for (; t !== a;) {
		if (n === t && (c = !0), !t.deleted) {
			let n = t.content;
			switch (n.constructor) {
				case Ab: {
					let { key: a, value: l } = n, u = r.get(a) ?? null;
					(o.get(a) !== n || u === l) && (t.delete(e), s++, !c && (i.get(a) ?? null) === l && u !== l && (u === null ? i.delete(a) : i.set(a, u))), !c && !t.deleted && Xy(i, n);
					break;
				}
			}
		}
		t = t.right;
	}
	return s;
}, nb = (e, t) => {
	for (; t && t.right && (t.right.deleted || !t.right.countable);) t = t.right;
	let n = /* @__PURE__ */ new Set();
	for (; t && (t.deleted || !t.countable);) {
		if (!t.deleted && t.content.constructor === Ab) {
			let r = t.content.key;
			n.has(r) ? t.delete(e) : n.add(r);
		}
		t = t.left;
	}
}, rb = (e) => {
	let t = 0;
	return U(e.doc, (n) => {
		let r = e._start, i = e._start, a = ip(), o = ap(a);
		for (; i;) {
			if (i.deleted === !1) switch (i.content.constructor) {
				case Ab:
					Xy(o, i.content);
					break;
				default: t += tb(n, r, i, a, o), a = ap(o), r = i;
			}
			i = i.right;
		}
	}), t;
}, ib = (e) => {
	let t = /* @__PURE__ */ new Set(), n = e.doc;
	for (let [r, i] of e.afterState.entries()) {
		let a = e.beforeState.get(r) || 0;
		i !== a && Lv(e, n.store.clients.get(r), a, i, (e) => {
			!e.deleted && e.content.constructor === Ab && e.constructor !== bb && t.add(e.parent);
		});
	}
	U(n, (n) => {
		h_(e, e.deleteSet, (e) => {
			if (e instanceof bb || !e.parent._hasFormatting || t.has(e.parent)) return;
			let r = e.parent;
			e.content.constructor === Ab ? t.add(r) : nb(n, e);
		});
		for (let e of t) rb(e);
	});
}, ab = (e, t, n) => {
	let r = n, i = ap(t.currentAttributes), a = t.right;
	for (; n > 0 && t.right !== null;) {
		if (t.right.deleted === !1) switch (t.right.content.constructor) {
			case qb:
			case Ob:
			case Lb: n < t.right.length && Pv(e, V(t.right.id.client, t.right.id.clock + n)), n -= t.right.length, t.right.delete(e);
		}
		t.forward();
	}
	a && tb(e, a, t.right, i, t.currentAttributes);
	let o = (t.left || t.right).parent;
	return o._searchMarker && vy(o._searchMarker, t.index, -r + n), t;
}, ob = class extends cy {
	constructor(e, t, n) {
		super(e, t), this.childListChanged = !1, this.keysChanged = /* @__PURE__ */ new Set(), n.forEach((e) => {
			e === null ? this.childListChanged = !0 : this.keysChanged.add(e);
		});
	}
	get changes() {
		if (this._changes === null) {
			let e = {
				keys: this.keys,
				delta: this.delta,
				added: /* @__PURE__ */ new Set(),
				deleted: /* @__PURE__ */ new Set()
			};
			this._changes = e;
		}
		return this._changes;
	}
	get delta() {
		if (this._delta === null) {
			let e = this.target.doc, t = [];
			U(e, (e) => {
				let n = /* @__PURE__ */ new Map(), r = /* @__PURE__ */ new Map(), i = this.target._start, a = null, o = {}, s = "", c = 0, l = 0, u = () => {
					if (a !== null) {
						let e = null;
						switch (a) {
							case "delete":
								l > 0 && (e = { delete: l }), l = 0;
								break;
							case "insert":
								(typeof s == "object" || s.length > 0) && (e = { insert: s }, n.size > 0 && (e.attributes = {}, n.forEach((t, n) => {
									t !== null && (e.attributes[n] = t);
								}))), s = "";
								break;
							case "retain": c > 0 && (e = { retain: c }, Qm(o) || (e.attributes = Jm({}, o))), c = 0;
						}
						e && t.push(e), a = null;
					}
				};
				for (; i !== null;) {
					switch (i.content.constructor) {
						case qb:
						case Ob:
							this.adds(i) ? this.deletes(i) || (u(), a = "insert", s = i.content.getContent()[0], u()) : this.deletes(i) ? (a !== "delete" && (u(), a = "delete"), l += 1) : i.deleted || (a !== "retain" && (u(), a = "retain"), c += 1);
							break;
						case Lb:
							this.adds(i) ? this.deletes(i) || (a !== "insert" && (u(), a = "insert"), s += i.content.str) : this.deletes(i) ? (a !== "delete" && (u(), a = "delete"), l += i.length) : i.deleted || (a !== "retain" && (u(), a = "retain"), c += i.length);
							break;
						case Ab: {
							let { key: t, value: s } = i.content;
							if (this.adds(i)) this.deletes(i) || (Gy(n.get(t) ?? null, s) ? s !== null && i.delete(e) : (a === "retain" && u(), Gy(s, r.get(t) ?? null) ? delete o[t] : o[t] = s));
							else if (this.deletes(i)) {
								r.set(t, s);
								let e = n.get(t) ?? null;
								Gy(e, s) || (a === "retain" && u(), o[t] = e);
							} else if (!i.deleted) {
								r.set(t, s);
								let n = o[t];
								n !== void 0 && (Gy(n, s) ? n !== null && i.delete(e) : (a === "retain" && u(), s === null ? delete o[t] : o[t] = s));
							}
							i.deleted || (a === "insert" && u(), Xy(n, i.content));
							break;
						}
					}
					i = i.right;
				}
				for (u(); t.length > 0;) {
					let e = t[t.length - 1];
					if (e.retain !== void 0 && e.attributes === void 0) t.pop();
					else break;
				}
			}), this._delta = t;
		}
		return this._delta;
	}
}, sb = class e extends W {
	constructor(e) {
		super(), this._pending = e === void 0 ? [] : [() => this.insert(0, e)], this._searchMarker = [], this._hasFormatting = !1;
	}
	get length() {
		return this.doc ?? uy(), this._length;
	}
	_integrate(e, t) {
		super._integrate(e, t);
		try {
			this._pending.forEach((e) => e());
		} catch (e) {
			console.error(e);
		}
		this._pending = null;
	}
	_copy() {
		return new e();
	}
	clone() {
		let t = new e();
		return t.applyDelta(this.toDelta()), t;
	}
	_callObserver(e, t) {
		super._callObserver(e, t);
		let n = new ob(this, e, t);
		yy(this, e, n), !e.local && this._hasFormatting && (e._needFormattingCleanup = !0);
	}
	toString() {
		this.doc ?? uy();
		let e = "", t = this._start;
		for (; t !== null;) !t.deleted && t.countable && t.content.constructor === Lb && (e += t.content.str), t = t.right;
		return e;
	}
	toJSON() {
		return this.toString();
	}
	applyDelta(e, { sanitize: t = !0 } = {}) {
		this.doc === null ? this._pending.push(() => this.applyDelta(e)) : U(this.doc, (n) => {
			let r = new Ky(null, this._start, 0, /* @__PURE__ */ new Map());
			for (let i = 0; i < e.length; i++) {
				let a = e[i];
				if (a.insert !== void 0) {
					let o = !t && typeof a.insert == "string" && i === e.length - 1 && r.right === null && a.insert.slice(-1) === "\n" ? a.insert.slice(0, -1) : a.insert;
					(typeof o != "string" || o.length > 0) && $y(n, this, r, o, a.attributes || {});
				} else a.retain === void 0 ? a.delete !== void 0 && ab(n, r, a.delete) : eb(n, this, r, a.retain, a.attributes || {});
			}
		});
	}
	toDelta(e, t, n) {
		this.doc ?? uy();
		let r = [], i = /* @__PURE__ */ new Map(), a = this.doc, o = "", s = this._start;
		function c() {
			if (o.length > 0) {
				let e = {}, t = !1;
				i.forEach((n, r) => {
					t = !0, e[r] = n;
				});
				let n = { insert: o };
				t && (n.attributes = e), r.push(n), o = "";
			}
		}
		let l = () => {
			for (; s !== null;) {
				if (Ev(s, e) || t !== void 0 && Ev(s, t)) switch (s.content.constructor) {
					case Lb: {
						let r = i.get("ychange");
						e !== void 0 && !Ev(s, e) ? (r === void 0 || r.user !== s.id.client || r.type !== "removed") && (c(), i.set("ychange", n ? n("removed", s.id) : { type: "removed" })) : t !== void 0 && !Ev(s, t) ? (r === void 0 || r.user !== s.id.client || r.type !== "added") && (c(), i.set("ychange", n ? n("added", s.id) : { type: "added" })) : r !== void 0 && (c(), i.delete("ychange")), o += s.content.str;
						break;
					}
					case qb:
					case Ob: {
						c();
						let e = { insert: s.content.getContent()[0] };
						if (i.size > 0) {
							let t = {};
							e.attributes = t, i.forEach((e, n) => {
								t[n] = e;
							});
						}
						r.push(e);
						break;
					}
					case Ab: Ev(s, e) && (c(), Xy(i, s.content));
				}
				s = s.right;
			}
			c();
		};
		return e || t ? U(a, (n) => {
			e && Dv(n, e), t && Dv(n, t), l();
		}, "cleanup") : l(), r;
	}
	insert(e, t, n) {
		if (t.length <= 0) return;
		let r = this.doc;
		r === null ? this._pending.push(() => this.insert(e, t, n)) : U(r, (r) => {
			let i = Jy(r, this, e, !n);
			n || (n = {}, i.currentAttributes.forEach((e, t) => {
				n[t] = e;
			})), $y(r, this, i, t, n);
		});
	}
	insertEmbed(e, t, n) {
		let r = this.doc;
		r === null ? this._pending.push(() => this.insertEmbed(e, t, n || {})) : U(r, (r) => {
			let i = Jy(r, this, e, !n);
			$y(r, this, i, t, n || {});
		});
	}
	delete(e, t) {
		if (t === 0) return;
		let n = this.doc;
		n === null ? this._pending.push(() => this.delete(e, t)) : U(n, (n) => {
			ab(n, Jy(n, this, e, !0), t);
		});
	}
	format(e, t, n) {
		if (t === 0) return;
		let r = this.doc;
		r === null ? this._pending.push(() => this.format(e, t, n)) : U(r, (r) => {
			let i = Jy(r, this, e, !1);
			i.right !== null && eb(r, this, i, t, n);
		});
	}
	removeAttribute(e) {
		this.doc === null ? this._pending.push(() => this.removeAttribute(e)) : U(this.doc, (t) => {
			My(t, this, e);
		});
	}
	setAttribute(e, t) {
		this.doc === null ? this._pending.push(() => this.setAttribute(e, t)) : U(this.doc, (n) => {
			Ny(n, this, e, t);
		});
	}
	getAttribute(e) {
		return Py(this, e);
	}
	getAttributes() {
		return Fy(this);
	}
	_write(e) {
		e.writeTypeRef(Hb);
	}
}, cb = (e) => new sb(), lb = class {
	constructor(e, t = () => !0) {
		this._filter = t, this._root = e, this._currentNode = e._start, this._firstCall = !0, e.doc ?? uy();
	}
	[Symbol.iterator]() {
		return this;
	}
	next() {
		let e = this._currentNode, t = e && e.content && e.content.type;
		if (e !== null && (!this._firstCall || e.deleted || !this._filter(t))) do
			if (t = e.content.type, !e.deleted && (t.constructor === G || t.constructor === ub) && t._start !== null) e = t._start;
			else for (; e !== null;) {
				let t = e.next;
				if (t !== null) {
					e = t;
					break;
				}
				e = e.parent === this._root ? null : e.parent._item;
			}
		while (e !== null && (e.deleted || !this._filter(e.content.type)));
		return this._firstCall = !1, e === null ? {
			value: void 0,
			done: !0
		} : (this._currentNode = e, {
			value: e.content.type,
			done: !1
		});
	}
}, ub = class e extends W {
	constructor() {
		super(), this._prelimContent = [];
	}
	get firstChild() {
		let e = this._first;
		return e ? e.content.getContent()[0] : null;
	}
	_integrate(e, t) {
		super._integrate(e, t), this.insert(0, this._prelimContent), this._prelimContent = null;
	}
	_copy() {
		return new e();
	}
	clone() {
		let t = new e();
		return t.insert(0, this.toArray().map((e) => e instanceof W ? e.clone() : e)), t;
	}
	get length() {
		return this.doc ?? uy(), this._prelimContent === null ? this._length : this._prelimContent.length;
	}
	createTreeWalker(e) {
		return new lb(this, e);
	}
	querySelector(e) {
		e = e.toUpperCase();
		let t = new lb(this, (t) => t.nodeName && t.nodeName.toUpperCase() === e).next();
		return t.done ? null : t.value;
	}
	querySelectorAll(e) {
		return e = e.toUpperCase(), fp(new lb(this, (t) => t.nodeName && t.nodeName.toUpperCase() === e));
	}
	_callObserver(e, t) {
		yy(this, e, new pb(this, t, e));
	}
	toString() {
		return wy(this, (e) => e.toString()).join("");
	}
	toJSON() {
		return this.toString();
	}
	toDOM(e = document, t = {}, n) {
		let r = e.createDocumentFragment();
		return n !== void 0 && n._createAssociation(r, this), Cy(this, (i) => {
			r.insertBefore(i.toDOM(e, t, n), null);
		}), r;
	}
	insert(e, t) {
		this.doc === null ? this._prelimContent.splice(e, 0, ...t) : U(this.doc, (n) => {
			ky(n, this, e, t);
		});
	}
	insertAfter(e, t) {
		if (this.doc !== null) U(this.doc, (n) => {
			let r = e && e instanceof W ? e._item : e;
			Dy(n, this, r, t);
		});
		else {
			let n = this._prelimContent, r = e === null ? 0 : n.findIndex((t) => t === e) + 1;
			if (r === 0 && e !== null) throw fm("Reference item not found");
			n.splice(r, 0, ...t);
		}
	}
	delete(e, t = 1) {
		this.doc === null ? this._prelimContent.splice(e, t) : U(this.doc, (n) => {
			jy(n, this, e, t);
		});
	}
	toArray() {
		return xy(this);
	}
	push(e) {
		this.insert(this.length, e);
	}
	unshift(e) {
		this.insert(0, e);
	}
	get(e) {
		return Ey(this, e);
	}
	slice(e = 0, t = this.length) {
		return by(this, e, t);
	}
	forEach(e) {
		Cy(this, e);
	}
	_write(e) {
		e.writeTypeRef(Wb);
	}
}, db = (e) => new ub(), G = class e extends ub {
	constructor(e = "UNDEFINED") {
		super(), this.nodeName = e, this._prelimAttrs = /* @__PURE__ */ new Map();
	}
	get nextSibling() {
		let e = this._item ? this._item.next : null;
		return e ? e.content.type : null;
	}
	get prevSibling() {
		let e = this._item ? this._item.prev : null;
		return e ? e.content.type : null;
	}
	_integrate(e, t) {
		super._integrate(e, t), this._prelimAttrs.forEach((e, t) => {
			this.setAttribute(t, e);
		}), this._prelimAttrs = null;
	}
	_copy() {
		return new e(this.nodeName);
	}
	clone() {
		let t = new e(this.nodeName);
		return Xm(this.getAttributes(), (e, n) => {
			t.setAttribute(n, e);
		}), t.insert(0, this.toArray().map((e) => e instanceof W ? e.clone() : e)), t;
	}
	toString() {
		let e = this.getAttributes(), t = [], n = [];
		for (let t in e) n.push(t);
		n.sort();
		let r = n.length;
		for (let i = 0; i < r; i++) {
			let r = n[i];
			t.push(r + "=\"" + e[r] + "\"");
		}
		let i = this.nodeName.toLocaleLowerCase();
		return `<${i}${t.length > 0 ? " " + t.join(" ") : ""}>${super.toString()}</${i}>`;
	}
	removeAttribute(e) {
		this.doc === null ? this._prelimAttrs.delete(e) : U(this.doc, (t) => {
			My(t, this, e);
		});
	}
	setAttribute(e, t) {
		this.doc === null ? this._prelimAttrs.set(e, t) : U(this.doc, (n) => {
			Ny(n, this, e, t);
		});
	}
	getAttribute(e) {
		return Py(this, e);
	}
	hasAttribute(e) {
		return Iy(this, e);
	}
	getAttributes(e) {
		return e ? Ly(this, e) : Fy(this);
	}
	toDOM(e = document, t = {}, n) {
		let r = e.createElement(this.nodeName), i = this.getAttributes();
		for (let e in i) {
			let t = i[e];
			typeof t == "string" && r.setAttribute(e, t);
		}
		return Cy(this, (i) => {
			r.appendChild(i.toDOM(e, t, n));
		}), n !== void 0 && n._createAssociation(r, this), r;
	}
	_write(e) {
		e.writeTypeRef(Ub), e.writeKey(this.nodeName);
	}
}, fb = (e) => new G(e.readKey()), pb = class extends cy {
	constructor(e, t, n) {
		super(e, n), this.childListChanged = !1, this.attributesChanged = /* @__PURE__ */ new Set(), t.forEach((e) => {
			e === null ? this.childListChanged = !0 : this.attributesChanged.add(e);
		});
	}
}, mb = class e extends Uy {
	constructor(e) {
		super(), this.hookName = e;
	}
	_copy() {
		return new e(this.hookName);
	}
	clone() {
		let t = new e(this.hookName);
		return this.forEach((e, n) => {
			t.set(n, e);
		}), t;
	}
	toDOM(e = document, t = {}, n) {
		let r = t[this.hookName], i;
		return i = r === void 0 ? document.createElement(this.hookName) : r.createDom(this), i.setAttribute("data-yjs-hook", this.hookName), n !== void 0 && n._createAssociation(i, this), i;
	}
	_write(e) {
		e.writeTypeRef(Gb), e.writeKey(this.hookName);
	}
}, hb = (e) => new mb(e.readKey()), gb = class e extends sb {
	get nextSibling() {
		let e = this._item ? this._item.next : null;
		return e ? e.content.type : null;
	}
	get prevSibling() {
		let e = this._item ? this._item.prev : null;
		return e ? e.content.type : null;
	}
	_copy() {
		return new e();
	}
	clone() {
		let t = new e();
		return t.applyDelta(this.toDelta()), t;
	}
	toDOM(e = document, t, n) {
		let r = e.createTextNode(this.toString());
		return n !== void 0 && n._createAssociation(r, this), r;
	}
	toString() {
		return this.toDelta().map((e) => {
			let t = [];
			for (let n in e.attributes) {
				let r = [];
				for (let t in e.attributes[n]) r.push({
					key: t,
					value: e.attributes[n][t]
				});
				r.sort((e, t) => e.key < t.key ? -1 : 1), t.push({
					nodeName: n,
					attrs: r
				});
			}
			t.sort((e, t) => e.nodeName < t.nodeName ? -1 : 1);
			let n = "";
			for (let e = 0; e < t.length; e++) {
				let r = t[e];
				n += `<${r.nodeName}`;
				for (let e = 0; e < r.attrs.length; e++) {
					let t = r.attrs[e];
					n += ` ${t.key}="${t.value}"`;
				}
				n += ">";
			}
			n += e.insert;
			for (let e = t.length - 1; e >= 0; e--) n += `</${t[e].nodeName}>`;
			return n;
		}).join("");
	}
	toJSON() {
		return this.toString();
	}
	_write(e) {
		e.writeTypeRef(Kb);
	}
}, _b = (e) => new gb(), vb = class {
	constructor(e, t) {
		this.id = e, this.length = t;
	}
	get deleted() {
		throw pm();
	}
	mergeWith(e) {
		return !1;
	}
	write(e, t, n) {
		throw pm();
	}
	integrate(e, t) {
		throw pm();
	}
}, yb = 0, bb = class extends vb {
	get deleted() {
		return !0;
	}
	delete() {}
	mergeWith(e) {
		return this.constructor === e.constructor && (this.length += e.length, !0);
	}
	integrate(e, t) {
		t > 0 && (this.id.clock += t, this.length -= t), Av(e.doc.store, this);
	}
	write(e, t) {
		e.writeInfo(yb), e.writeLen(this.length - t);
	}
	getMissing(e, t) {
		return null;
	}
}, xb = class e {
	constructor(e) {
		this.content = e;
	}
	getLength() {
		return 1;
	}
	getContent() {
		return [this.content];
	}
	isCountable() {
		return !0;
	}
	copy() {
		return new e(this.content);
	}
	splice(e) {
		throw pm();
	}
	mergeWith(e) {
		return !1;
	}
	integrate(e, t) {}
	delete(e) {}
	gc(e) {}
	write(e, t) {
		e.writeBuf(this.content);
	}
	getRef() {
		return 3;
	}
}, Sb = (e) => new xb(e.readBuf()), Cb = class e {
	constructor(e) {
		this.len = e;
	}
	getLength() {
		return this.len;
	}
	getContent() {
		return [];
	}
	isCountable() {
		return !1;
	}
	copy() {
		return new e(this.len);
	}
	splice(t) {
		let n = new e(this.len - t);
		return this.len = t, n;
	}
	mergeWith(e) {
		return this.len += e.len, !0;
	}
	integrate(e, t) {
		b_(e.deleteSet, t.id.client, t.id.clock, this.len), t.markDeleted();
	}
	delete(e) {}
	gc(e) {}
	write(e, t) {
		e.writeLen(this.len - t);
	}
	getRef() {
		return 1;
	}
}, wb = (e) => new Cb(e.readLen()), Tb = (e, t) => new D_({
	guid: e,
	...t,
	shouldLoad: t.shouldLoad || t.autoLoad || !1
}), Eb = class e {
	constructor(e) {
		e._item && console.error("This document was already integrated as a sub-document. You should create a second instance instead with the same guid."), this.doc = e;
		let t = {};
		this.opts = t, e.gc || (t.gc = !1), e.autoLoad && (t.autoLoad = !0), e.meta !== null && (t.meta = e.meta);
	}
	getLength() {
		return 1;
	}
	getContent() {
		return [this.doc];
	}
	isCountable() {
		return !0;
	}
	copy() {
		return new e(Tb(this.doc.guid, this.opts));
	}
	splice(e) {
		throw pm();
	}
	mergeWith(e) {
		return !1;
	}
	integrate(e, t) {
		this.doc._item = t, e.subdocsAdded.add(this.doc), this.doc.shouldLoad && e.subdocsLoaded.add(this.doc);
	}
	delete(e) {
		e.subdocsAdded.has(this.doc) ? e.subdocsAdded.delete(this.doc) : e.subdocsRemoved.add(this.doc);
	}
	gc(e) {}
	write(e, t) {
		e.writeString(this.doc.guid), e.writeAny(this.opts);
	}
	getRef() {
		return 9;
	}
}, Db = (e) => new Eb(Tb(e.readString(), e.readAny())), Ob = class e {
	constructor(e) {
		this.embed = e;
	}
	getLength() {
		return 1;
	}
	getContent() {
		return [this.embed];
	}
	isCountable() {
		return !0;
	}
	copy() {
		return new e(this.embed);
	}
	splice(e) {
		throw pm();
	}
	mergeWith(e) {
		return !1;
	}
	integrate(e, t) {}
	delete(e) {}
	gc(e) {}
	write(e, t) {
		e.writeJSON(this.embed);
	}
	getRef() {
		return 5;
	}
}, kb = (e) => new Ob(e.readJSON()), Ab = class e {
	constructor(e, t) {
		this.key = e, this.value = t;
	}
	getLength() {
		return 1;
	}
	getContent() {
		return [];
	}
	isCountable() {
		return !1;
	}
	copy() {
		return new e(this.key, this.value);
	}
	splice(e) {
		throw pm();
	}
	mergeWith(e) {
		return !1;
	}
	integrate(e, t) {
		let n = t.parent;
		n._searchMarker = null, n._hasFormatting = !0;
	}
	delete(e) {}
	gc(e) {}
	write(e, t) {
		e.writeKey(this.key), e.writeJSON(this.value);
	}
	getRef() {
		return 6;
	}
}, jb = (e) => new Ab(e.readKey(), e.readJSON()), Mb = class e {
	constructor(e) {
		this.arr = e;
	}
	getLength() {
		return this.arr.length;
	}
	getContent() {
		return this.arr;
	}
	isCountable() {
		return !0;
	}
	copy() {
		return new e(this.arr);
	}
	splice(t) {
		let n = new e(this.arr.slice(t));
		return this.arr = this.arr.slice(0, t), n;
	}
	mergeWith(e) {
		return this.arr = this.arr.concat(e.arr), !0;
	}
	integrate(e, t) {}
	delete(e) {}
	gc(e) {}
	write(e, t) {
		let n = this.arr.length;
		e.writeLen(n - t);
		for (let r = t; r < n; r++) {
			let t = this.arr[r];
			e.writeString(t === void 0 ? "undefined" : JSON.stringify(t));
		}
	}
	getRef() {
		return 2;
	}
}, Nb = (e) => {
	let t = e.readLen(), n = [];
	for (let r = 0; r < t; r++) {
		let t = e.readString();
		t === "undefined" ? n.push(void 0) : n.push(JSON.parse(t));
	}
	return new Mb(n);
}, Pb = mh("node_env") === "development", Fb = class e {
	constructor(e) {
		this.arr = e, Pb && rh(e);
	}
	getLength() {
		return this.arr.length;
	}
	getContent() {
		return this.arr;
	}
	isCountable() {
		return !0;
	}
	copy() {
		return new e(this.arr);
	}
	splice(t) {
		let n = new e(this.arr.slice(t));
		return this.arr = this.arr.slice(0, t), n;
	}
	mergeWith(e) {
		return this.arr = this.arr.concat(e.arr), !0;
	}
	integrate(e, t) {}
	delete(e) {}
	gc(e) {}
	write(e, t) {
		let n = this.arr.length;
		e.writeLen(n - t);
		for (let r = t; r < n; r++) {
			let t = this.arr[r];
			e.writeAny(t);
		}
	}
	getRef() {
		return 8;
	}
}, Ib = (e) => {
	let t = e.readLen(), n = [];
	for (let r = 0; r < t; r++) n.push(e.readAny());
	return new Fb(n);
}, Lb = class e {
	constructor(e) {
		this.str = e;
	}
	getLength() {
		return this.str.length;
	}
	getContent() {
		return this.str.split("");
	}
	isCountable() {
		return !0;
	}
	copy() {
		return new e(this.str);
	}
	splice(t) {
		let n = new e(this.str.slice(t));
		this.str = this.str.slice(0, t);
		let r = this.str.charCodeAt(t - 1);
		return r >= 55296 && r <= 56319 && (this.str = this.str.slice(0, t - 1) + "�", n.str = "�" + n.str.slice(1)), n;
	}
	mergeWith(e) {
		return this.str += e.str, !0;
	}
	integrate(e, t) {}
	delete(e) {}
	gc(e) {}
	write(e, t) {
		e.writeString(t === 0 ? this.str : this.str.slice(t));
	}
	getRef() {
		return 4;
	}
}, Rb = (e) => new Lb(e.readString()), zb = [
	Vy,
	Wy,
	cb,
	fb,
	db,
	hb,
	_b
], Bb = 0, Vb = 1, Hb = 2, Ub = 3, Wb = 4, Gb = 5, Kb = 6, qb = class e {
	constructor(e) {
		this.type = e;
	}
	getLength() {
		return 1;
	}
	getContent() {
		return [this.type];
	}
	isCountable() {
		return !0;
	}
	copy() {
		return new e(this.type._copy());
	}
	splice(e) {
		throw pm();
	}
	mergeWith(e) {
		return !1;
	}
	integrate(e, t) {
		this.type._integrate(e.doc, t);
	}
	delete(e) {
		let t = this.type._start;
		for (; t !== null;) t.deleted ? t.id.clock < (e.beforeState.get(t.id.client) || 0) && e._mergeStructs.push(t) : t.delete(e), t = t.right;
		this.type._map.forEach((t) => {
			t.deleted ? t.id.clock < (e.beforeState.get(t.id.client) || 0) && e._mergeStructs.push(t) : t.delete(e);
		}), e.changed.delete(this.type);
	}
	gc(e) {
		let t = this.type._start;
		for (; t !== null;) t.gc(e, !0), t = t.right;
		this.type._start = null, this.type._map.forEach((t) => {
			for (; t !== null;) t.gc(e, !0), t = t.left;
		}), this.type._map = /* @__PURE__ */ new Map();
	}
	write(e, t) {
		this.type._write(e);
	}
	getRef() {
		return 7;
	}
}, Jb = (e) => new qb(zb[e.readTypeRef()](e)), Yb = (e, t) => {
	let n = t, r = 0, i;
	do
		r > 0 && (n = V(n.client, n.clock + r)), i = Mv(e, n), r = n.clock - i.id.clock, n = i.redone;
	while (n !== null && i instanceof K);
	return {
		item: i,
		diff: r
	};
}, Xb = (e, t) => {
	for (; e !== null && e.keep !== t;) e.keep = t, e = e.parent._item;
}, Zb = (e, t, n) => {
	let { client: r, clock: i } = t.id, a = new K(V(r, i + n), t, V(r, i + n - 1), t.right, t.rightOrigin, t.parent, t.parentSub, t.content.splice(n));
	return t.deleted && a.markDeleted(), t.keep && (a.keep = !0), t.redone !== null && (a.redone = V(t.redone.client, t.redone.clock + n)), t.right = a, a.right !== null && (a.right.left = a), e._mergeStructs.push(a), a.parentSub !== null && a.right === null && a.parent._map.set(a.parentSub, a), t.length = n, a;
}, Qb = (e, t) => mp(e, (e) => __(e.deletions, t)), $b = (e, t, n, r, i, a) => {
	let o = e.doc, s = o.store, c = o.clientID, l = t.redone;
	if (l !== null) return Pv(e, l);
	let u = t.parent._item, d = null, f;
	if (u !== null && u.deleted === !0) {
		if (u.redone === null && (!n.has(u) || $b(e, u, n, r, i, a) === null)) return null;
		for (; u.redone !== null;) u = Pv(e, u.redone);
	}
	let p = u === null ? t.parent : u.content.type;
	if (t.parentSub === null) {
		for (d = t.left, f = t; d !== null;) {
			let t = d;
			for (; t !== null && t.parent._item !== u;) t = t.redone === null ? null : Pv(e, t.redone);
			if (t !== null && t.parent._item === u) {
				d = t;
				break;
			}
			d = d.left;
		}
		for (; f !== null;) {
			let t = f;
			for (; t !== null && t.parent._item !== u;) t = t.redone === null ? null : Pv(e, t.redone);
			if (t !== null && t.parent._item === u) {
				f = t;
				break;
			}
			f = f.right;
		}
	} else {
		if (f = null, t.right && !i) {
			for (d = t; d !== null && d.right !== null && (d.right.redone || __(r, d.right.id) || Qb(a.undoStack, d.right.id) || Qb(a.redoStack, d.right.id));) for (d = d.right; d.redone;) d = Pv(e, d.redone);
			if (d && d.right !== null) return null;
		} else d = p._map.get(t.parentSub) || null;
		d !== null && d.parent._item !== u && (d = p._map.get(t.parentSub) || null);
	}
	let m = V(c, H(s, c)), h = new K(m, d, d && d.lastId, f, f && f.id, p, t.parentSub, t.content.copy());
	return t.redone = m, Xb(h, !0), h.integrate(e, 0), h;
}, K = class e extends vb {
	constructor(e, t, n, r, i, a, o, s) {
		super(e, s.getLength()), this.origin = n, this.left = t, this.right = r, this.rightOrigin = i, this.parent = a, this.parentSub = o, this.redone = null, this.content = s, this.info = this.content.isCountable() ? 2 : 0;
	}
	set marker(e) {
		(this.info & 8) > 0 !== e && (this.info ^= 8);
	}
	get marker() {
		return (this.info & 8) > 0;
	}
	get keep() {
		return (this.info & 1) > 0;
	}
	set keep(e) {
		this.keep !== e && (this.info ^= 1);
	}
	get countable() {
		return (this.info & 2) > 0;
	}
	get deleted() {
		return (this.info & 4) > 0;
	}
	set deleted(e) {
		this.deleted !== e && (this.info ^= 4);
	}
	markDeleted() {
		this.info |= 4;
	}
	getMissing(t, n) {
		if (this.origin && this.origin.client !== this.id.client && this.origin.clock >= H(n, this.origin.client)) return this.origin.client;
		if (this.rightOrigin && this.rightOrigin.client !== this.id.client && this.rightOrigin.clock >= H(n, this.rightOrigin.client)) return this.rightOrigin.client;
		if (this.parent && this.parent.constructor === iv && this.id.client !== this.parent.client && this.parent.clock >= H(n, this.parent.client)) return this.parent.client;
		if (this.origin && (this.left = Fv(t, n, this.origin), this.origin = this.left.lastId), this.rightOrigin && (this.right = Pv(t, this.rightOrigin), this.rightOrigin = this.right.id), this.left && this.left.constructor === bb || this.right && this.right.constructor === bb) this.parent = null;
		else if (!this.parent) this.left && this.left.constructor === e ? (this.parent = this.left.parent, this.parentSub = this.left.parentSub) : this.right && this.right.constructor === e && (this.parent = this.right.parent, this.parentSub = this.right.parentSub);
		else if (this.parent.constructor === iv) {
			let e = Mv(n, this.parent);
			this.parent = e.constructor === bb ? null : e.content.type;
		}
		return null;
	}
	integrate(e, t) {
		if (t > 0 && (this.id.clock += t, this.left = Fv(e, e.doc.store, V(this.id.client, this.id.clock - 1)), this.origin = this.left.lastId, this.content = this.content.splice(t), this.length -= t), this.parent) {
			if (!this.left && (!this.right || this.right.left !== null) || this.left && this.left.right !== this.right) {
				let t = this.left, n;
				if (t !== null) n = t.right;
				else if (this.parentSub !== null) for (n = this.parent._map.get(this.parentSub) || null; n !== null && n.left !== null;) n = n.left;
				else n = this.parent._start;
				let r = /* @__PURE__ */ new Set(), i = /* @__PURE__ */ new Set();
				for (; n !== null && n !== this.right;) {
					if (i.add(n), r.add(n), av(this.origin, n.origin)) {
						if (n.id.client < this.id.client) t = n, r.clear();
						else if (av(this.rightOrigin, n.rightOrigin)) break;
					} else if (n.origin !== null && i.has(Mv(e.doc.store, n.origin))) r.has(Mv(e.doc.store, n.origin)) || (t = n, r.clear());
					else break;
					n = n.right;
				}
				this.left = t;
			}
			if (this.left !== null) {
				let e = this.left.right;
				this.right = e, this.left.right = this;
			} else {
				let e;
				if (this.parentSub !== null) for (e = this.parent._map.get(this.parentSub) || null; e !== null && e.left !== null;) e = e.left;
				else e = this.parent._start, this.parent._start = this;
				this.right = e;
			}
			this.right === null ? this.parentSub !== null && (this.parent._map.set(this.parentSub, this), this.left !== null && this.left.delete(e)) : this.right.left = this, this.parentSub === null && this.countable && !this.deleted && (this.parent._length += this.length), Av(e.doc.store, this), this.content.integrate(e, this), Bv(e, this.parent, this.parentSub), (this.parent._item !== null && this.parent._item.deleted || this.parentSub !== null && this.right !== null) && this.delete(e);
		} else new bb(this.id, this.length).integrate(e, 0);
	}
	get next() {
		let e = this.right;
		for (; e !== null && e.deleted;) e = e.right;
		return e;
	}
	get prev() {
		let e = this.left;
		for (; e !== null && e.deleted;) e = e.left;
		return e;
	}
	get lastId() {
		return this.length === 1 ? this.id : V(this.id.client, this.id.clock + this.length - 1);
	}
	mergeWith(e) {
		if (this.constructor === e.constructor && av(e.origin, this.lastId) && this.right === e && av(this.rightOrigin, e.rightOrigin) && this.id.client === e.id.client && this.id.clock + this.length === e.id.clock && this.deleted === e.deleted && this.redone === null && e.redone === null && this.content.constructor === e.content.constructor && this.content.mergeWith(e.content)) {
			let t = this.parent._searchMarker;
			return t && t.forEach((t) => {
				t.p === e && (t.p = this, !this.deleted && this.countable && (t.index -= this.length));
			}), e.keep && (this.keep = !0), this.right = e.right, this.right !== null && (this.right.left = this), this.length += e.length, !0;
		}
		return !1;
	}
	delete(e) {
		if (!this.deleted) {
			let t = this.parent;
			this.countable && this.parentSub === null && (t._length -= this.length), this.markDeleted(), b_(e.deleteSet, this.id.client, this.id.clock, this.length), Bv(e, t, this.parentSub), this.content.delete(e);
		}
	}
	gc(e, t) {
		if (!this.deleted) throw R();
		this.content.gc(e), t ? Iv(e, this, new bb(this.id, this.length)) : this.content = new Cb(this.length);
	}
	write(e, t) {
		let n = t > 0 ? V(this.id.client, this.id.clock + t - 1) : this.origin, r = this.rightOrigin, i = this.parentSub, a = this.content.getRef() & 31 | (n === null ? 0 : 128) | (r === null ? 0 : 64) | (i === null ? 0 : 32);
		if (e.writeInfo(a), n !== null && e.writeLeftID(n), r !== null && e.writeRightID(r), n === null && r === null) {
			let t = this.parent;
			if (t._item !== void 0) {
				let n = t._item;
				if (n === null) {
					let n = cv(t);
					e.writeParentInfo(!0), e.writeString(n);
				} else e.writeParentInfo(!1), e.writeLeftID(n.id);
			} else t.constructor === String ? (e.writeParentInfo(!0), e.writeString(t)) : t.constructor === iv ? (e.writeParentInfo(!1), e.writeLeftID(t)) : R();
			i !== null && e.writeString(i);
		}
		this.content.write(e, t);
	}
}, ex = (e, t) => tx[t & 31](e), tx = [
	() => {
		R();
	},
	wb,
	Nb,
	Sb,
	Rb,
	kb,
	jb,
	Jb,
	Ib,
	Db,
	() => {
		R();
	}
], nx = 10, rx = class extends vb {
	get deleted() {
		return !0;
	}
	delete() {}
	mergeWith(e) {
		return this.constructor === e.constructor && (this.length += e.length, !0);
	}
	integrate(e, t) {
		R();
	}
	write(e, t) {
		e.writeInfo(nx), L(e.restEncoder, this.length - t);
	}
	getMissing(e, t) {
		return null;
	}
}, ix = typeof globalThis < "u" ? globalThis : typeof window < "u" ? window : typeof global < "u" ? global : {}, ax = "__ $YJS$ __";
ix[ax] === !0 && console.error("Yjs was already imported. This breaks constructor checks and will lead to issues! - https://github.com/yjs/yjs/issues/438"), ix[ax] = !0;
//#endregion
//#region node_modules/lib0/mutex.js
var ox = () => {
	let e = !0;
	return (t, n) => {
		if (e) {
			e = !1;
			try {
				t();
			} finally {
				e = !0;
			}
		} else n !== void 0 && n();
	};
}, sx = (e, t) => e >>> t | e << 32 - t, cx = (e) => sx(e, 2) ^ sx(e, 13) ^ sx(e, 22), lx = (e) => sx(e, 6) ^ sx(e, 11) ^ sx(e, 25), ux = (e) => sx(e, 7) ^ sx(e, 18) ^ e >>> 3, dx = (e) => sx(e, 17) ^ sx(e, 19) ^ e >>> 10, fx = new Uint32Array([
	1116352408,
	1899447441,
	3049323471,
	3921009573,
	961987163,
	1508970993,
	2453635748,
	2870763221,
	3624381080,
	310598401,
	607225278,
	1426881987,
	1925078388,
	2162078206,
	2614888103,
	3248222580,
	3835390401,
	4022224774,
	264347078,
	604807628,
	770255983,
	1249150122,
	1555081692,
	1996064986,
	2554220882,
	2821834349,
	2952996808,
	3210313671,
	3336571891,
	3584528711,
	113926993,
	338241895,
	666307205,
	773529912,
	1294757372,
	1396182291,
	1695183700,
	1986661051,
	2177026350,
	2456956037,
	2730485921,
	2820302411,
	3259730800,
	3345764771,
	3516065817,
	3600352804,
	4094571909,
	275423344,
	430227734,
	506948616,
	659060556,
	883997877,
	958139571,
	1322822218,
	1537002063,
	1747873779,
	1955562222,
	2024104815,
	2227730452,
	2361852424,
	2428436474,
	2756734187,
	3204031479,
	3329325298
]), px = new Uint32Array([
	1779033703,
	3144134277,
	1013904242,
	2773480762,
	1359893119,
	2600822924,
	528734635,
	1541459225
]), mx = class {
	constructor() {
		let e = /* @__PURE__ */ new ArrayBuffer(320);
		this._H = new Uint32Array(e, 0, 8), this._H.set(px), this._W = new Uint32Array(e, 64, 64);
	}
	_updateHash() {
		let e = this._H, t = this._W;
		for (let e = 16; e < 64; e++) t[e] = dx(t[e - 2]) + t[e - 7] + ux(t[e - 15]) + t[e - 16];
		let n = e[0], r = e[1], i = e[2], a = e[3], o = e[4], s = e[5], c = e[6], l = e[7];
		for (let e = 0, u, d; e < 64; e++) u = l + lx(o) + (o & s ^ ~o & c) + fx[e] + t[e] >>> 0, d = cx(n) + (n & r ^ n & i ^ r & i) >>> 0, l = c, c = s, s = o, o = a + u >>> 0, a = i, i = r, r = n, n = u + d >>> 0;
		e[0] += n, e[1] += r, e[2] += i, e[3] += a, e[4] += o, e[5] += s, e[6] += c, e[7] += l;
	}
	digest(e) {
		let t = 0;
		for (; t + 56 <= e.length;) {
			let n = 0;
			for (; n < 16 && t + 3 < e.length; n++) this._W[n] = e[t++] << 24 | e[t++] << 16 | e[t++] << 8 | e[t++];
			if (t % 64 != 0) {
				for (this._W.fill(0, n, 16); t < e.length;) this._W[n] |= e[t] << (3 - t % 4) * 8, t++;
				this._W[n] |= 128 << (3 - t % 4) * 8;
			}
			this._updateHash();
		}
		let n = t % 64 != 0;
		this._W.fill(0, 0, 16);
		let r = 0;
		for (; t < e.length; r++) for (let n = 3; n >= 0 && t < e.length; n--) this._W[r] |= e[t++] << n * 8;
		n || (this._W[r - (t % 4 == 0 ? 0 : 1)] |= 128 << (3 - t % 4) * 8), this._W[14] = e.byteLength / wp, this._W[15] = e.byteLength * 8, this._updateHash();
		let i = /* @__PURE__ */ new Uint8Array(32);
		for (let e = 0; e < this._H.length; e++) for (let t = 0; t < 4; t++) i[e * 4 + t] = this._H[e] >>> (3 - t) * 8;
		return i;
	}
}, hx = (e) => new mx().digest(e), gx = /[\uD800-\uDBFF]/, _x = /[\uDC00-\uDFFF]/, vx = (e, t) => {
	let n = 0, r = 0;
	for (; n < e.length && n < t.length && e[n] === t[n];) n++;
	for (n > 0 && gx.test(e[n - 1]) && n--; r + n < e.length && r + n < t.length && e[e.length - r - 1] === t[t.length - r - 1];) r++;
	return r > 0 && _x.test(e[e.length - r]) && r--, {
		index: n,
		remove: e.length - n - r,
		insert: t.slice(n, t.length - r)
	};
};
//#endregion
//#region node_modules/@tiptap/y-tiptap/dist/index.js
function yx(e) {
	if (!e || !(e.content instanceof qb)) return null;
	let t = e.content.type;
	if (!(t instanceof ub || t instanceof sb || t instanceof mb)) throw Error("Expected a Yjs XML type");
	return t;
}
function bx(e) {
	if (e === null) return null;
	if (!(e.parent instanceof W)) throw Error("Expected an integrated Yjs parent");
	let t = e.parent;
	if (!(t instanceof ub || t instanceof sb || t instanceof mb)) throw Error("Expected a Yjs XML type");
	return t;
}
var xx = (e, t) => {
	let n = null, r = null;
	return e._item === null ? r = cv(e) : n = V(e._item.id.client, e._item.id.clock), new uv(n, r, t.id);
}, Sx = (e, t, n) => {
	if (e === 0) return mv(t, 0, -1);
	let r = yx(t._first);
	for (; r !== null && t !== r;) {
		if (r instanceof gb) {
			if (r._length >= e) return mv(r, e, -1);
			if (e -= r._length, r._item !== null && r._item.next !== null) r = yx(r._item.next);
			else {
				do
					r = bx(r._item), e--;
				while (r !== t && r !== null && r._item !== null && r._item.next === null);
				r !== null && r !== t && (r = r._item === null ? null : yx(r._item.next));
			}
		} else {
			let i = n.get(r), a = i instanceof de ? i.nodeSize : 0;
			if (r._first !== null && e < a) r = yx(r._first), e--;
			else {
				if (e === 1 && r._length === 0 && a > 1) return new uv(r._item === null ? null : r._item.id, r._item === null ? cv(r) : null, null);
				if (e -= a, r._item !== null && r._item.next !== null) r = yx(r._item.next);
				else {
					if (e === 0) {
						if (r = r._item === null ? r : bx(r._item), r === null) throw R();
						return new uv(r._item === null ? null : r._item.id, r._item === null ? cv(r) : null, null);
					}
					do {
						if (r = bx(r._item), r === null) throw R();
						e--;
					} while (r !== t && r._item?.next === null);
					r !== t && (r = yx(r._item?.next));
				}
			}
		}
		if (r === null) throw R();
		if (e === 0 && r.constructor !== gb && r !== t) {
			let e = r._item, t = bx(e);
			if (e === null || t === null) throw R();
			return xx(t, e);
		}
	}
	return mv(t, t._length, -1);
};
function Cx(e, t) {
	if (e instanceof gb) return e._length;
	let n = t.get(e);
	return n instanceof de ? n.nodeSize : null;
}
function wx(e, t, n) {
	let r = 0, i = e._first;
	for (; i !== null;) {
		let e = yx(i);
		if (e === t) break;
		let a = i.deleted;
		if (i = i.right, a) continue;
		if (e === null) return null;
		let o = Cx(e, n);
		if (o === null) return null;
		r += o;
	}
	return r;
}
function Tx(e, t, n) {
	let r = 0, i = 0, a = e._first;
	for (; i < e._length && i < t && a !== null;) {
		let e = a;
		if (a = a.right, e.deleted) continue;
		let t = yx(e);
		if (t === null) return null;
		let o = Cx(t, n);
		if (o === null) return null;
		i++, r += o;
	}
	return r;
}
var Ex = (e, t, n) => {
	if (n === null) return !1;
	let r = bv(t, e);
	return r !== null && r.type instanceof gb && t.item !== null && r.type._item?.deleted === !0;
}, Dx = (e) => {
	if (!e.doc) throw Error("The Yjs fragment must belong to a document");
	let t = e._item ? `item:${e._item.id.client}:${e._item.id.clock}` : `root:${cv(e)}`;
	return e.doc.getMap(`__tiptap_position_migrations:${t}`);
}, Ox = /* @__PURE__ */ new WeakMap(), kx = (e, t) => {
	let n = t.item ?? t.type;
	if (!n) return [];
	let r;
	t.item || (r = t.assoc < 0 ? "start" : "end");
	let i = Ox.get(e);
	if (!i || i.size !== e.size) {
		let t = /* @__PURE__ */ new Map();
		e.forEach((e) => e.transfers.forEach((n) => {
			let r = t.get(n.source.client) ?? [];
			r.push({
				migration: e,
				transfer: n
			}), t.set(n.source.client, r);
		})), i = {
			size: e.size,
			clients: t
		}, Ox.set(e, i);
	}
	let a = (i.clients.get(n.client) ?? []).filter(({ transfer: e }) => e.source.boundary === r && e.source.clock <= n.clock && n.clock < e.source.clock + e.source.length);
	return a.filter((e) => !a.some((t) => t.migration !== e.migration && t.migration.seen.some(([t, n]) => t === e.migration.client && n > e.migration.clock)));
}, Ax = (e, t, n) => {
	let { source: r, target: i } = e.transfer;
	return i ? new uv(i.boundary ? V(i.client, i.clock) : i.type, i.tname ?? null, i.boundary ? null : V(i.client, i.clock + t.clock - r.clock), i.assoc ?? n) : null;
}, jx = (e, t, n) => {
	let r = Ax(e[0], t, n);
	return !r || e.some((e) => {
		let i = Ax(e, t, n);
		return !i || !xv(r, i);
	}) ? null : r;
}, Mx = (e) => {
	let t = e.item ?? e.type;
	if (!t) return null;
	let n = e.assoc < 0 ? "start" : "end";
	return e.item && (n = "item"), `${t.client}:${t.clock}:${n}`;
}, Nx = (e, t) => {
	let n = Dx(e), r = /* @__PURE__ */ new Set(), i = t, a = i.item ?? i.type;
	for (; a;) {
		let e = Mx(i);
		if (r.has(e)) return null;
		r.add(e);
		let t = kx(n, i);
		if (t.length === 0) return i;
		let o = jx(t, a, i.assoc);
		if (!o) return null;
		if (Mx(o) === e) return o;
		i = o, a = o.item ?? o.type;
	}
	return i;
}, Px = (e, t, n, r) => {
	let i = Nx(t, n);
	if (!i) return null;
	n = i;
	let a = bv(n, e);
	if (a === null || a.type !== t && !lv(t, a.type._item)) return null;
	let o = a.type, s = 0;
	if (o.constructor === gb) s = a.index;
	else if (!o._item?.deleted) {
		let e = Tx(o, a.index, r);
		if (e === null) return null;
		s = e + 1;
	}
	for (; o !== t && o._item !== null;) {
		let e = bx(o._item);
		if (e === null) return null;
		if (e._item?.deleted) {
			o = e;
			continue;
		}
		let t = wx(e, o, r);
		if (t === null) return null;
		s += t + 1, o = e;
	}
	let c = s - 1;
	return Ex(e, n, c) ? null : c;
}, q = new D("y-sync"), Fx = new D("y-undo");
new D("yjs-cursor");
function Ix(e) {
	let t = q.getState(e);
	if (!t) throw Error("The editor requires ySyncPlugin");
	return t;
}
var Lx = (e, t) => t === void 0 ? !e.deleted : t.sv.has(e.id.client) && (t.sv.get(e.id.client) ?? 0) > e.id.clock && !__(t.ds, e.id), Rx = (e, t, n) => {
	if (!e.has(n)) {
		if (e.size < t.length) {
			let n = lp();
			e.forEach((e) => n.add(e)), t = t.filter((e) => !n.has(e));
		}
		e.set(n, Lm(t));
	}
	let r = e.get(n);
	if (!r) throw Error("At least one snapshot color is required");
	return r;
}, zx = (e, t) => {
	if (e === t) return !0;
	let n = Object.keys(e);
	return n.length === Object.keys(t).length && n.every((n) => e[n] === t[n]);
}, Bx = (e) => {
	let t = e.type.spec.attrs || {};
	return Object.keys(e.attrs).some((n) => {
		let r = t[n];
		return r == null || !Object.prototype.hasOwnProperty.call(r, "default") || r.default !== e.attrs[n];
	});
}, Vx = (e, t, n) => {
	let r = 0, i = 0;
	for (; i < e.childCount; i++) {
		let t = e.child(i);
		if (r + t.nodeSize > n) break;
		r += t.nodeSize;
	}
	if (i >= e.childCount) return null;
	let a = e.child(i), o = e.resolve(n), s = (e, t) => {
		if (o.depth === 0) {
			let i = e + (n - r), a = e + 1, o = e + t.nodeSize - 1;
			return Math.max(a, Math.min(i, o));
		}
		if (o.depth > 1) {
			let n = Vx(o.node(1), t, o.pos - o.start(1));
			return n === null ? null : e + 1 + n;
		}
		return t.isTextblock ? e + 1 + Math.min(o.parentOffset, t.content.size) : null;
	}, c = (n, r = !1) => {
		let a = 0;
		for (let t = 0; t <= i; t++) n(e.child(t)) && a++;
		let o = 0, c = -1, l = null, u = 0;
		for (let e = 0; e < t.childCount; e++) {
			let r = t.child(e);
			n(r) && (o++, o === a && (c = u, l = r)), u += r.nodeSize;
		}
		return l === null || r && (a !== 1 || o !== 1) ? null : s(c, l);
	}, l = (e) => e.type === a.type && zx(e.attrs, a.attrs), u = a.textContent, d = c((e) => l(e) && e.textContent === u);
	if (d !== null) return d;
	let f = c((e) => e.type === a.type && e.textContent === u);
	if (f !== null) return f;
	if (!a.isTextblock || Bx(a)) {
		let e = c(l, !0);
		if (e !== null) return e;
	}
	return c((e) => l(e) && u !== "" && e.textContent !== "" && (u.startsWith(e.textContent) || e.textContent.startsWith(u)), !0);
}, Hx = (e, t, n, r) => {
	if (r === null) return !1;
	let i = e.resolve(n), a = t.resolve(r);
	if (!i.parent.isTextblock) return !1;
	if (!a.parent.isTextblock || i.parent.textContent !== a.parent.textContent || i.parentOffset !== 0 && a.parentOffset === 0) return !0;
	let o = i.parentOffset === 0 && a.parentOffset === 0, s = i.parentOffset !== a.parentOffset || i.parent.type !== a.parent.type || !zx(i.parent.attrs, a.parent.attrs);
	if (o || s) {
		let i = Vx(e, t, n);
		return i !== null && i !== r;
	}
	return !1;
}, Ux = (e, t, n, r) => {
	if (n == null || n < 0 || n > t.content.size || !(r === null || n > 1 && r <= 1 || Hx(t, e, n, r))) return r;
	let i = Vx(t, e, n);
	return i === null ? r : i;
}, Wx = (e, t, n, r) => {
	if (t !== null && t.anchor !== null && t.head !== null) {
		if (t.type === "all") e.setSelection(new On(e.doc));
		else if (t.type === "node") {
			let r = Px(n.doc, n.type, t.anchor, n.mapping);
			r !== null && e.setSelection(Gx(e, r));
		} else if (t.type === "nodeRange") {
			let r = Kx(e, Px(n.doc, n.type, t.anchor, n.mapping), Px(n.doc, n.type, t.head, n.mapping), t.depth);
			r !== null && e.setSelection(r);
		} else {
			let i = Px(n.doc, n.type, t.anchor, n.mapping), a = Px(n.doc, n.type, t.head, n.mapping);
			r != null && (Nx(n.type, t.anchor) === t.anchor && (i = Ux(e.doc, r, t.absAnchor, i)), Nx(n.type, t.head) === t.head && (a = Ux(e.doc, r, t.absHead, a))), i === null && (i = a), a === null && (a = i), i !== null && a !== null && e.setSelection(w.between(e.doc.resolve(i), e.doc.resolve(a)));
		}
	}
}, Gx = (e, t) => {
	let n = e.doc.resolve(t);
	return n.nodeAfter ? T.create(e.doc, t) : w.near(n);
}, Kx = (e, t, n, r) => {
	if (t === null || n === null) return null;
	let i = Math.min(Math.max(t, 0), e.doc.content.size), a = Math.min(Math.max(n, 0), e.doc.content.size);
	try {
		let t = C.fromJSON(e.doc, {
			type: "nodeRange",
			anchor: i,
			head: a,
			depth: r
		});
		return t.ranges.length ? t : w.near(e.doc.resolve(i));
	} catch {
		return w.near(e.doc.resolve(i));
	}
}, qx = (e, t) => {
	let n = t.selection, r = n.toJSON(), i = typeof r.type == "string" ? r.type : "text";
	return {
		type: i,
		depth: i === "nodeRange" && "depth" in n && typeof n.depth == "number" ? n.depth : void 0,
		anchor: Sx(t.selection.anchor, e.type, e.mapping),
		head: Sx(t.selection.head, e.type, e.mapping),
		absAnchor: t.selection.anchor,
		absHead: t.selection.head
	};
}, Jx = /* @__PURE__ */ new WeakMap(), Yx = /(.*)(--[a-zA-Z0-9+/=]{8})$/, Xx = (e) => Yx.exec(e)?.[1] ?? e, Zx = (e, t) => {
	let n = [];
	for (let r in e) n.push(t.mark(Xx(r), e[r]));
	return n;
}, Qx = (e, t, n, r, i, a) => e.toDelta(r, i, a).map((e) => t.text(e.insert, Zx(e.attributes, t))), $x = (e, t, n, r, i, a) => {
	let o = n.mapping.get(e);
	if (o !== void 0 && !Array.isArray(o) && Jx.get(n.mapping)?.get(e) === o) return o;
	let s = [], c = (e) => {
		if (e instanceof G) s.push($x(e, t, n, r, i, a));
		else if (e instanceof gb) s.push(...Qx(e, t, n, r, i, a));
		else throw Error("Cannot project a Yjs XML hook");
	};
	r === void 0 || i === void 0 ? e.toArray().forEach(c) : Sy(e, new Sv(i.ds, r.sv)).forEach(c);
	let l = e.getAttributes(r);
	r !== void 0 && e._item && (Lx(e._item, r) ? Lx(e._item, i) || (l.ychange = a ? a("added", e._item.id) : { type: "added" }) : l.ychange = a ? a("removed", e._item.id) : { type: "removed" });
	let u = t.node(e.nodeName, l, s);
	o !== void 0 && !Array.isArray(o) && o.eq(u) && (u = o), n.mapping.set(e, u);
	let d = Jx.get(n.mapping);
	return d === void 0 && (d = /* @__PURE__ */ new WeakMap(), Jx.set(n.mapping, d)), d.set(e, u), u;
}, eS = (e, t) => {
	if (e.childCount > 0 && !t.topNodeType.validContent(e)) throw RangeError("Shared content does not fit the document schema");
}, tS = (e) => {
	for (let t = 6; t < e.length; t++) e[t % 6] = e[t % 6] ^ e[t];
	return e.slice(0, 6);
}, nS = (e) => yh(tS(hx(xh(e)))), rS = (e, t) => {
	let n = {};
	return e.forEach((e) => {
		if (e.type.name !== "ychange") {
			let r = op(t.isOMark, e.type, () => !e.type.excludes(e.type));
			n[r ? `${e.type.name}--${nS(e.toJSON())}` : e.type.name] = e.attrs;
		}
	}), n;
}, iS = (e, t) => {
	let n = new gb(), r = e.map((e) => ({
		insert: e.text ?? "",
		attributes: rS(e.marks, t)
	}));
	return n.applyDelta(r), t.mapping.set(n, e), n;
}, aS = (e) => {
	let t = e.content.content, n = [];
	for (let e = 0; e < t.length; e++) {
		let r = t[e];
		if (r.isText) {
			let r = [];
			for (let n = t[e]; e < t.length && n.isText; n = t[++e]) r.push(n);
			e--, n.push(r);
		} else n.push(r);
	}
	return n;
}, oS = (e, t) => e instanceof Array ? iS(e, t) : sS(e, t), sS = (e, t) => {
	let n = new G(e.type.name);
	for (let t in e.attrs) {
		let r = e.attrs[t];
		r !== null && t !== "ychange" && n.setAttribute(t, r);
	}
	return n.insert(0, aS(e).map((e) => oS(e, t))), t.mapping.set(n, e), n;
}, cS = (e) => typeof e == "object" && !!e, lS = (e, t) => {
	let n = Object.keys(e).filter((t) => e[t] !== null), r = n.length === Object.keys(t).filter((e) => t[e] !== null).length;
	for (let i = 0; i < n.length && r; i++) {
		let a = n[i], o = e[a], s = t[a];
		r = a === "ychange" || o === s || cS(o) && cS(s) && lS(o, s);
	}
	return r;
}, uS = (e, t) => {
	let n = e.toDelta();
	return n.length === t.length && n.every((e, n) => e.insert === t[n].text && Ym(e.attributes || {}).length === t[n].marks.length && Object.entries(e.attributes ?? {}).every(([e, r]) => {
		let i = Xx(e), a = t[n].marks.find((e) => e.type.name === i);
		return a ? lS(r, a.attrs) : !1;
	}));
}, dS = (e, t) => !(t instanceof Array) && e.nodeName === t.type.name, fS = (e, t) => {
	if (e instanceof G && !(t instanceof Array) && dS(e, t)) {
		let n = aS(t);
		return e._length === n.length && lS(e.getAttributes(), t.attrs) && e.toArray().every((e, t) => fS(e, n[t]));
	}
	return e instanceof gb && t instanceof Array && uS(e, t);
}, pS = (e, t) => e === t || e instanceof Array && t instanceof Array && e.length === t.length && e.every((e, n) => t[n] === e), mS = (e, t, n) => {
	let r = /* @__PURE__ */ new Map();
	t.forEach((e, t) => {
		r.has(e) || r.set(e, t);
	});
	let i = [];
	if (e.forEach((e, a) => {
		let o = n.mapping.get(e), s = o === void 0 ? void 0 : r.get(o);
		s !== void 0 && pS(o, t[s]) && i.push({
			yIndex: a,
			pIndex: s,
			yChild: e
		});
	}), i.length === 0) return [];
	let a = [], o = [];
	for (let e = 0; e < i.length; e++) {
		let t = 0, n = o.length;
		for (; t < n;) {
			let r = t + n >> 1;
			i[o[r]].pIndex < i[e].pIndex ? t = r + 1 : n = r;
		}
		a[e] = t > 0 ? o[t - 1] : -1, o[t] = e;
	}
	let s = [];
	for (let e = o[o.length - 1]; e !== -1; e = a[e]) s.push(i[e]);
	return s.reverse();
}, hS = (e) => {
	let t = "", n = e._start, r = {};
	for (; n !== null;) n.deleted || (n.countable && n.content instanceof Lb ? t += n.content.str : n.content instanceof Ab && (r[n.content.key] = null)), n = n.right;
	return {
		str: t,
		nAttrs: r
	};
}, gS = (e, t, n) => {
	n.mapping.set(e, t);
	let { nAttrs: r, str: i } = hS(e), a = t.map((e) => ({
		insert: e.text ?? "",
		attributes: Object.assign({}, r, rS(e.marks, n))
	})), { insert: o, remove: s, index: c } = vx(i, a.map((e) => e.insert).join(""));
	e.delete(c, s), e.insert(c, o), e.applyDelta(a.map((e) => ({
		retain: e.insert.length,
		attributes: e.attributes
	})));
}, _S = (e, t, n, r, i) => t instanceof gb && n instanceof Array ? (uS(t, n) ? r.mapping.set(t, n) : gS(t, n, r), !0) : t instanceof G && !(n instanceof Array) && dS(t, n) ? (fS(t, n) ? r.mapping.set(t, n) : i(e, t, n, r), !0) : !1, vS = (e, t) => {
	var n;
	(t instanceof ub || t instanceof sb || t instanceof mb) && (e.delete(t), (n = Jx.get(e)) == null || n.delete(t));
}, yS = (e, t, n, r) => {
	n > 0 && (e.slice(t, t + n).forEach((e) => vS(r.mapping, e)), e.delete(t, n));
}, bS = (e, t, n, r, i, a) => {
	if (r < i) {
		let o = [];
		for (let e = r; e < i; e++) o.push(oS(n[e], a));
		e.insert(t, o);
	}
}, xS = (e, t, n, r, i, a, o, s, c, l) => {
	let u = xp(a - i, s - o);
	for (let a = 0; a < u; a++) {
		let s = r[i + a], u = n[o + a];
		_S(e, s, u, c, l) || (yS(t, o + a, 1, c), bS(t, o + a, n, o + a, o + a + 1, c));
	}
	yS(t, o + u, a - i - u, c), bS(t, o + u, n, o + u, s, c);
}, SS = (e, t, n, r, i, a) => {
	let o = mS(r, n, i);
	if (o.length < 2 || !o.some((e) => e.yIndex !== e.pIndex) || o.some((e) => {
		let t = n[e.pIndex];
		return !(e.yChild instanceof gb && t instanceof Array || e.yChild instanceof G && !(t instanceof Array));
	})) return !1;
	let s = 0, c = 0;
	for (let l of o) {
		xS(e, t, n, r, s, l.yIndex, c, l.pIndex, i, a);
		let o = n[l.pIndex];
		_S(e, l.yChild, o, i, a), s = l.yIndex + 1, c = l.pIndex + 1;
	}
	return xS(e, t, n, r, s, r.length, c, n.length, i, a), !0;
}, CS = (e, t, n) => {
	let r = e.toArray(), i = aS(t), a = i.length, o = r.length, s = xp(o, a), c = 0, l = 0, u = !1;
	for (; c < s; c++) {
		let e = r[c], t = i[c];
		if (pS(n.mapping.get(e), t)) u = !0;
		else if (!fS(e, t)) break;
	}
	for (; c + l < s; l++) {
		let e = r[o - l - 1], t = i[a - l - 1];
		if (pS(n.mapping.get(e), t)) u = !0;
		else if (!fS(e, t)) break;
	}
	return {
		equalityFactor: c + l,
		foundMappedChild: u
	};
}, wS = (e, t, n, r) => {
	if (t instanceof G && t.nodeName !== n.type.name) throw Error("node name mismatch!");
	if (r.mapping.set(t, n), t instanceof G) {
		let e = t.getAttributes(), r = n.attrs;
		for (let n in r) {
			if (r[n] === null) {
				t.removeAttribute(n);
				continue;
			}
			e[n] !== r[n] && n !== "ychange" && t.setAttribute(n, r[n]);
		}
		for (let n in e) r[n] === void 0 && t.removeAttribute(n);
	}
	let i = aS(n), a = i.length, o = t.toArray(), s = o.length, c = xp(a, s), l = !1;
	if (e.transact(() => {
		l = SS(e, t, i, o, r, wS);
	}, q), l) return;
	let u = 0, d = 0;
	for (; u < c; u++) {
		let e = o[u], t = i[u];
		if (!pS(r.mapping.get(e), t)) {
			if (!fS(e, t)) break;
			r.mapping.set(e, t);
		}
	}
	for (; d + u + 1 < c; d++) {
		let e = o[s - d - 1], t = i[a - d - 1];
		if (!pS(r.mapping.get(e), t)) {
			if (!fS(e, t)) break;
			r.mapping.set(e, t);
		}
	}
	e.transact(() => {
		for (; s - u - d > 0 && a - u - d > 0;) {
			let n = o[u], c = i[u], l = o[s - d - 1], f = i[a - d - 1];
			if (n instanceof gb && c instanceof Array) {
				uS(n, c) || gS(n, c, r), u += 1;
				continue;
			}
			let p = n instanceof G && dS(n, c), m = l instanceof G && dS(l, f);
			if (p && m && n instanceof G && l instanceof G && !Array.isArray(c) && !Array.isArray(f)) {
				let e = CS(n, c, r), t = CS(l, f, r);
				e.foundMappedChild && !t.foundMappedChild ? m = !1 : !e.foundMappedChild && t.foundMappedChild || e.equalityFactor < t.equalityFactor ? p = !1 : m = !1;
			}
			if (p && n instanceof G && !Array.isArray(c)) {
				wS(e, n, c, r), u += 1;
				continue;
			}
			if (m && l instanceof G && !Array.isArray(f)) {
				wS(e, l, f, r), d += 1;
				continue;
			}
			vS(r.mapping, t.get(u)), t.delete(u, 1), t.insert(u, [oS(c, r)]), u += 1;
		}
		let n = s - u - d;
		if (s === 1 && a === 0 && o[0] instanceof gb ? (vS(r.mapping, o[0]), o[0].delete(0, o[0].length)) : n > 0 && (t.slice(u, u + n).forEach((e) => vS(r.mapping, e)), t.delete(u, n)), u + d >= a) return;
		let c = [];
		for (let e = u; e < a - d; e++) c.push(oS(i[e], r));
		t.insert(u, c);
	}, q);
}, TS = (e, t = 0) => {
	let n = [];
	if (!e._item) throw Error("Position tracking requires integrated text");
	for (let r = e._start; r; r = r.right) if (!r.deleted && r.countable) {
		if (!(r.content instanceof Lb)) throw Error("Position tracking requires string text content");
		for (let i = 0; i < r.length; i += 1) n.push({
			client: r.id.client,
			clock: r.id.clock + i,
			text: r.content.str[i],
			type: e._item.id,
			position: t + n.length
		});
	}
	return n;
}, ES = (e, t, n) => {
	let r = [{
		current: -1,
		target: -1
	}];
	return e.forEach((e, n) => {
		let i = t.get(`${e.client}:${e.clock}`);
		i !== void 0 && i > r[r.length - 1].target && r.push({
			current: n,
			target: i
		});
	}), r.push({
		current: e.length,
		target: n
	}), r;
}, DS = (e, t, n, r, i) => {
	let a = TS(e), o = /* @__PURE__ */ new Map();
	for (let e = 0; e < t.length; e += 1) {
		let t = r.get(n + e);
		t && o.set(`${t.client}:${t.clock}`, e);
	}
	let s = ES(a, o, t.length);
	for (let n = s.length - 1; n > 0; --n) {
		let r = s[n - 1], o = s[n], c = a.slice(r.current + 1, o.current), l = t.slice(r.target + 1, o.target);
		(c.some((e) => i.has(`${e.client}:${e.clock}`)) || c.map((e) => e.text).join("") !== l) && (e.delete(r.current + 1, c.length), e.insert(r.current + 1, l));
	}
	return TS(e, n);
}, OS = (e, t) => !e.target || !t.target ? e.target === t.target : e.target.client === t.target.client && e.target.assoc === t.target.assoc && e.target.boundary === t.target.boundary && e.target.tname === t.target.tname && e.target.clock + e.source.length === t.target.clock && av(e.target.type, t.target.type), kS = (e) => {
	let t = [];
	return e.forEach((e) => {
		let n = t[t.length - 1];
		if (n && n.source.client === e.source.client && n.source.boundary === e.source.boundary && n.source.clock + n.source.length === e.source.clock && OS(n, e)) {
			n.source.length += e.source.length;
			return;
		}
		t.push({
			...e,
			source: { ...e.source }
		});
	}), t;
}, AS = (e, t) => {
	let n = e.doc;
	if (!n || t.length === 0) return;
	let r = H(n.store, n.clientID);
	e.set(`${n.clientID}:${r}`, {
		version: 1,
		client: n.clientID,
		clock: r,
		seen: [...J_(Q_(n))],
		transfers: kS(t)
	});
}, jS = (e, t) => {
	let n = /* @__PURE__ */ new Map();
	e.forEach((e) => {
		let t = `${e.type.client}:${e.type.clock}`, r = n.get(t) ?? [];
		r.push(e), n.set(t, r);
	});
	let r = [];
	return n.forEach((e) => {
		let n = e[0], i = e[e.length - 1], a = n.position === null ? void 0 : t.get(n.position), o = i.position === null ? void 0 : t.get(i.position);
		(!a || !av(n.type, a.type)) && r.push({
			source: {
				...n.type,
				length: 1,
				boundary: "start"
			},
			target: a ? {
				...a,
				assoc: 0
			} : null
		}), (!o || !av(i.type, o.type)) && r.push({
			source: {
				...n.type,
				length: 1,
				boundary: "end"
			},
			target: o ? {
				...o,
				assoc: -1
			} : null
		});
	}), r;
}, MS = (e, t, n, r) => {
	let i = [];
	e.forEach((e) => {
		let n = e.position === null ? null : t.get(e.position) ?? null;
		n && av(e.id, n) || i.push({
			source: {
				...e.id,
				length: 1
			},
			target: n
		});
	});
	let a = [...e, ...r];
	for (let e of ["start", "end"]) {
		let r = e === "start" ? t : n;
		a.forEach((t) => {
			let n = e === "start" ? t.position : t.end, a = n === null ? null : r.get(n) ?? null;
			a && av(t.id, a) || i.push({
				source: {
					...t.id,
					length: 1,
					boundary: e
				},
				target: a ? {
					client: a.client,
					clock: a.clock,
					type: null,
					boundary: e
				} : null
			});
		});
	}
	return i;
}, NS = (e, t, n, r, i, a) => {
	let o = e.get(t);
	if (!(o instanceof G) || !o._item) throw Error("Position migration requires synchronized elements");
	let s = i.get(`${o._item.id.client}:${o._item.id.clock}`);
	if (s && s.position !== r && (vS(a.mapping, o), e.delete(t, 1), o = oS(n, a), e.insert(t, [o])), !(o instanceof G) || !o._item) throw Error("Position migration requires integrated elements");
	return o;
}, PS = (e, t, n, r) => {
	let i = new Map(t.characters.map((e) => [e.position, e])), a = new Set(t.characters.map((e) => `${e.client}:${e.clock}`)), o = /* @__PURE__ */ new Map(), s = new Map(t.nodes.map((e) => [`${e.id.client}:${e.id.clock}`, e])), c = /* @__PURE__ */ new Map(), l = /* @__PURE__ */ new Map(), u = (e, t, r) => {
		let d = e.toArray(), f = r;
		aS(t).forEach((t, r) => {
			let p = d[r];
			if (Array.isArray(t)) {
				if (!(p instanceof gb) || !p._item) throw Error("Position migration requires integrated text");
				let e = t.map((e) => e.text ?? "").join(""), r = DS(p, e, f, i, a), s = p._item.id;
				r.forEach((e) => o.set(e.position, {
					client: e.client,
					clock: e.clock,
					type: s
				})), gS(p, t, n), f += e.length;
				return;
			}
			let m = NS(e, r, t, f, s, n), h = m._item.id, g = {
				client: h.client,
				clock: h.clock,
				type: e._item?.id ?? null,
				...e._item ? {} : { tname: cv(e) }
			};
			c.set(f, g), l.set(f + t.nodeSize - 1, g), t.isLeaf || u(m, t, f + 1), f += t.nodeSize;
		});
	};
	u(e, t.doc, 0);
	let d = [];
	t.characters.forEach((e) => {
		let t = e.position === null ? null : o.get(e.position) ?? null;
		t && av(e, t) && av(e.type, t.type) || d.push({
			source: {
				client: e.client,
				clock: e.clock,
				length: 1
			},
			target: t
		});
	}), d.push(...jS(t.characters, o)), d.push(...MS(t.nodes, c, l, t.emptyTexts)), AS(r, d);
}, FS = (e, t) => {
	let n = e.doc.content, r = 0, i = 0;
	for (; r < n.childCount && r < t.childCount && n.child(r) === t.child(r);) i += n.child(r).nodeSize, r++;
	let a = n.childCount, o = t.childCount, s = 0;
	for (; a > r && o > r && n.child(a - 1) === t.child(o - 1);) s += n.child(a - 1).nodeSize, a--, o--;
	if (r === a && r === o) return e;
	let c = new h(t.cut(i, t.size - s), 0, 0), l = tn(e.doc, i, n.size - s, c);
	return l !== null && l.apply(e.doc).doc?.content.eq(t) ? e.step(l) : e.replace(0, n.size, new h(t, 0, 0));
}, IS = class {
	get projectionError() {
		return this._projectionError;
	}
	constructor(e, t = /* @__PURE__ */ new Map()) {
		if (this.prosemirrorView = null, this.mux = ox(), this.isOMark = /* @__PURE__ */ new Map(), this.beforeTransactionSelection = null, this._domSelectionInView = null, this.syncedDoc = null, this._projectionError = null, this.type = e, this.mapping = t, this._observeFunction = this._typeChanged.bind(this), e.doc === null) throw Error("The Yjs fragment must belong to a document");
		this.doc = e.doc, this.beforeAllTransactions = () => {
			this.projectionError === null && this.beforeTransactionSelection === null && this.prosemirrorView != null && (this.beforeTransactionSelection = qx(this, this.view.state));
		}, this.afterAllTransactions = () => {
			this.beforeTransactionSelection = null;
		};
	}
	get view() {
		if (!this.prosemirrorView) throw Error("The binding has no editor view");
		return this.prosemirrorView;
	}
	get _tr() {
		return this.view.state.tr.setMeta("addToHistory", !1);
	}
	_isLocalCursorInView() {
		return this.view.hasFocus() ? (lh && this._domSelectionInView === null && (qg(0, () => {
			this._domSelectionInView = null;
		}), this._domSelectionInView = this._isDomSelectionInView()), this._domSelectionInView) : !1;
	}
	_isDomSelectionInView() {
		let e = this.view.root, t = "getSelection" in e && typeof e.getSelection == "function" ? e.getSelection() : this.view.dom.ownerDocument.getSelection();
		if (t == null || t.anchorNode == null || t.focusNode == null) return !1;
		let n = this.view.dom.ownerDocument.createRange();
		n.setStart(t.anchorNode, t.anchorOffset), n.setEnd(t.focusNode, t.focusOffset), n.getClientRects().length === 0 && n.startContainer && n.collapsed && n.selectNodeContents(n.startContainer);
		let r = n.getBoundingClientRect(), i = Bg.documentElement;
		return r.bottom >= 0 && r.right >= 0 && r.left <= (window.innerWidth || i.clientWidth || 0) && r.top <= (window.innerHeight || i.clientHeight || 0);
	}
	renderSnapshot(e, t) {
		t || (t = Cv(x_(), /* @__PURE__ */ new Map())), this.view.dispatch(this._tr.setMeta(q, {
			snapshot: e,
			prevSnapshot: t
		}));
	}
	createDocumentFragment() {
		try {
			let e = this.type.toArray().map((e) => {
				if (!(e instanceof G)) throw pm();
				return $x(e, this.view.state.schema, this);
			}), t = l.from(e);
			return eS(t, this.view.state.schema), this._projectionError = null, t;
		} catch (e) {
			return this.handleProjectionError(e), null;
		}
	}
	handleProjectionError(e) {
		this._projectionError = e instanceof Error ? e : /* @__PURE__ */ Error("Cannot project shared content"), this.mapping.clear(), Jx.delete(this.mapping), this.beforeTransactionSelection = null, this.view.dispatch(this._tr.setMeta(q, { isChangeOrigin: !0 }));
	}
	createDocumentTransaction() {
		let e = this.createDocumentFragment();
		return e === null ? null : this._tr.replace(0, this.view.state.doc.content.size, new h(e, 0, 0));
	}
	unrenderSnapshot() {
		this.mapping.clear(), Jx.delete(this.mapping), this.mux(() => {
			let e = this.createDocumentTransaction();
			e !== null && (e.setMeta(q, {
				snapshot: null,
				prevSnapshot: null
			}), this.view.dispatch(e));
		});
	}
	_forceRerender() {
		this.mapping.clear(), Jx.delete(this.mapping), this.mux(() => {
			let e = this.beforeTransactionSelection === null ? this.view.state.selection : null, t = this.createDocumentTransaction();
			if (t !== null) {
				if (e) {
					let n = xp(Sp(e.anchor, 0), t.doc.content.size), r = xp(Sp(e.head, 0), t.doc.content.size);
					t.setSelection(w.create(t.doc, n, r));
				}
				this.view.dispatch(t.setMeta(q, {
					isChangeOrigin: !0,
					binding: this
				}));
			}
		});
	}
	_renderSnapshot(e, t, n) {
		let r = this.doc, i = this.type;
		if (e || (e = Tv(this.doc)), e instanceof Uint8Array || t instanceof Uint8Array) {
			if (!(e instanceof Uint8Array) || !(t instanceof Uint8Array)) throw R();
			if (r = new D_({ gc: !1 }), H_(r, t), t = Tv(r), H_(r, e), e = Tv(r), i._item === null) {
				let e = Array.from(this.doc.share.keys()).find((e) => Object.is(this.doc.share.get(e), i));
				i = r.getXmlFragment(e);
			} else {
				let e = r.store.clients.get(i._item.id.client) ?? [], t = e[jv(e, i._item.id.clock)];
				if (!(t instanceof K && t.content instanceof qb && t.content.type instanceof ub)) throw R();
				i = t.content.type;
			}
		}
		let a = e, o = t ?? wv;
		this.mapping.clear(), Jx.delete(this.mapping), this.mux(() => {
			try {
				r.transact((e) => {
					let t = n.permanentUserData;
					t && t.dss.forEach((t) => {
						h_(e, t, (e) => {});
					});
					let r = (e, r) => {
						let i = t ? e === "added" ? t.getUserByClientId(r.client) : t.getUserByDeletedId(r) : null;
						return {
							user: i,
							type: e,
							color: Rx(n.colorMapping, n.colors, i)
						};
					}, s = Sy(i, new Sv(o.ds, a.sv)).map((e) => !(e instanceof G) || !e._item ? null : !e._item.deleted || Lx(e._item, a) || Lx(e._item, o) ? $x(e, this.view.state.schema, {
						mapping: /* @__PURE__ */ new Map(),
						isOMark: /* @__PURE__ */ new Map()
					}, a, o, r) : null).filter((e) => e !== null), c = l.from(s);
					eS(c, this.view.state.schema);
					let u = this._tr.replace(0, this.view.state.doc.content.size, new h(c, 0, 0));
					this._projectionError = null, this.view.dispatch(u.setMeta(q, { isChangeOrigin: !0 }));
				}, q);
			} catch (e) {
				this.handleProjectionError(e);
			}
		});
	}
	_typeChanged(e, t) {
		if (this.prosemirrorView == null) return;
		let n = Ix(this.view.state);
		if (e.length === 0 || n.snapshot != null || n.prevSnapshot != null) {
			this.renderSnapshot(n.snapshot, n.prevSnapshot);
			return;
		}
		this.mux(() => {
			let e = (e, t) => vS(this.mapping, t);
			h_(t, t.deleteSet, (e) => {
				e instanceof K && e.content instanceof qb && vS(this.mapping, e.content.type);
			}), t.changed.forEach(e), t.changedParentTypes.forEach(e);
			let n = this.createDocumentFragment();
			if (n === null) return;
			let r = this.view.state.doc, i = FS(this._tr, n);
			Wx(i, this.beforeTransactionSelection, this, r), i = i.setMeta(q, {
				isChangeOrigin: !0,
				isUndoRedoOperation: t.origin instanceof Jv
			}), this.beforeTransactionSelection !== null && this._isLocalCursorInView() && i.scrollIntoView(), this.view.dispatch(i);
		});
	}
	_prosemirrorChanged(e, t = null) {
		let n = Ix(this.view.state);
		this.projectionError === null && n.snapshot == null && n.prevSnapshot == null && this.doc.transact(() => {
			wS(this.doc, this.type, e, this), t?.doc === e && PS(this.type, t, this, Dx(this.type)), this.syncedDoc = e, this.beforeTransactionSelection = qx(this, this.view.state);
		}, q);
	}
	initView(e) {
		this.prosemirrorView != null && this.destroy(), this.prosemirrorView = e, this.doc.on("beforeAllTransactions", this.beforeAllTransactions), this.doc.on("afterAllTransactions", this.afterAllTransactions), this.type.observeDeep(this._observeFunction);
	}
	destroy() {
		this.prosemirrorView != null && (this.prosemirrorView = null, this.type.unobserveDeep(this._observeFunction), this.doc.off("beforeAllTransactions", this.beforeAllTransactions), this.doc.off("afterAllTransactions", this.afterAllTransactions));
	}
}, LS = (e, t) => {
	let n = [], r = [], i = [];
	if (e.length === 0) return {
		characters: n,
		nodes: r,
		emptyTexts: i
	};
	let a = (e, t, o) => {
		let s = e.toArray(), c = aS(t), l = o;
		t.content.size === 0 && s.forEach((e) => {
			e instanceof gb && e._item && i.push({
				id: e._item.id,
				position: o - 1,
				end: o
			});
		}), c.forEach((e, t) => {
			let i = s[t];
			if (!Array.isArray(e)) {
				if (!(i instanceof G) || !i._item) throw Error("Position tracking requires synchronized editor content");
				r.push({
					id: i._item.id,
					position: l,
					end: l + e.nodeSize - 1
				}), e.isLeaf || a(i, e, l + 1), l += e.nodeSize;
				return;
			}
			if (!(i instanceof gb)) throw Error("Position tracking requires synchronized editor text");
			let o = TS(i, l);
			n.push(...o), l += o.length;
		});
	};
	return a(e, t, 0), {
		characters: n,
		nodes: r,
		emptyTexts: i
	};
}, RS = (e, t, n, r) => {
	let i = e.toJSON();
	if (i.stepType !== "replace" && i.stepType !== "replaceAround" || r !== i.from && r !== i.to - 1) return !1;
	let a = t.nodeAt(i.from), o = n.nodeAt(i.from);
	return !a || !o || a.nodeSize !== i.to - i.from ? !1 : zS(a, o);
}, zS = (e, t) => e.type === t.type && e.content.eq(t.content) && !e.sameMarkup(t), BS = (e, t) => {
	let n = e.toJSON(), r = n.slice ?? { content: [] };
	if (n.stepType !== "replace" || r.openStart > 0 || r.openEnd > 0) return !1;
	let i = t.resolve(n.from), a = t.resolve(n.to);
	return r.content.every((e) => e.type === "text") && i.parent.isTextblock && i.sameParent(a);
}, VS = (e) => {
	let t = /* @__PURE__ */ new Map();
	return e.descendants((e, n) => {
		let r = t.get(e) ?? [];
		r.push(n), t.set(e, r);
	}), t;
}, HS = (e, t, n, r) => {
	if (r[n].get(e)?.length !== 1) return null;
	for (let i = n + 1; i < r.length; i += 1) {
		let n = r[i].get(e);
		if (n?.length === 1) return {
			position: n[0] + t,
			step: i
		};
	}
	return null;
}, US = (e, t, n, r) => {
	let i = n[t].resolve(e), a = [], o = n[t].nodeAt(e);
	o && !o.isText && a.push({
		node: o,
		offset: 0
	});
	for (let t = i.depth; t > 0; --t) a.push({
		node: i.node(t),
		offset: e - i.before(t)
	});
	for (let e of a) {
		let n = HS(e.node, e.offset, t, r);
		if (n) return n;
	}
	return null;
}, WS = (e, t, n, r) => {
	for (let i = 0; i < t.mapping.maps.length; i += 1) {
		let a = t.mapping.maps[i], o = a.mapResult(e, 1), s = a.mapResult(e + 1, -1);
		if (!o.deleted && !s.deleted && s.pos === o.pos + 1) {
			e = o.pos;
			continue;
		}
		if (RS(t.steps[i], n[i], n[i + 1], e)) continue;
		let c = US(e, i, n, r);
		if (!c) return null;
		e = c.position, i = c.step - 1;
	}
	return e;
}, GS = (e, t, n) => {
	let r = n?.doc === e.before && n.doc !== t.syncedDoc ? n : null;
	if (!e.docChanged || !t.prosemirrorView || !r && e.steps.every((t, n) => BS(t, e.docs[n]))) return r;
	let i = r ?? LS(t.type, e.before), a = [...e.docs, e.doc], o = a.map(VS), s = i.characters.map((t) => ({
		...t,
		position: t.position === null ? null : WS(t.position, e, a, o)
	})), c = (t) => ({
		...t,
		position: t.position === null ? null : WS(t.position, e, a, o),
		end: t.end === null ? null : WS(t.end, e, a, o)
	});
	return {
		doc: e.doc,
		characters: s,
		nodes: i.nodes.map(c),
		emptyTexts: i.emptyTexts.map(c)
	};
}, KS = [{
	light: "#ecd44433",
	dark: "#ecd444"
}], qS = (e, { colors: t = KS, colorMapping: n = /* @__PURE__ */ new Map(), permanentUserData: r = null, onFirstRender: i = () => {}, mapping: a } = {}) => {
	let o = !1, s = new IS(e, a);
	return new E({
		filterTransaction: (e) => s.projectionError === null || !e.docChanged,
		props: { editable: (e) => {
			let t = Ix(e);
			return s.projectionError === null && t.snapshot == null && t.prevSnapshot == null;
		} },
		key: q,
		state: {
			init: (i, a) => ({
				type: e,
				doc: s.doc,
				binding: s,
				snapshot: null,
				prevSnapshot: null,
				isChangeOrigin: !1,
				isUndoRedoOperation: !1,
				addToHistory: !0,
				colors: t,
				colorMapping: n,
				permanentUserData: r,
				positionMigration: null
			}),
			apply: (e, t) => {
				let n = e.getMeta(q);
				return n !== void 0 && (t = Object.assign({}, t, n)), e.docChanged && (t.addToHistory = e.getMeta("addToHistory") !== !1), t.isChangeOrigin = n !== void 0 && !!n.isChangeOrigin, t.isUndoRedoOperation = n !== void 0 && !!n.isChangeOrigin && !!n.isUndoRedoOperation, t = {
					...t,
					positionMigration: s.projectionError || t.isChangeOrigin || t.snapshot || t.prevSnapshot || n?.snapshot !== void 0 || n?.prevSnapshot !== void 0 ? null : GS(e, s, t.positionMigration ?? null)
				}, s.prosemirrorView === null || n === void 0 || n.snapshot == null && n.prevSnapshot == null || qg(0, () => {
					if (s.prosemirrorView != null) {
						if (n.restore == null) {
							s._renderSnapshot(n.snapshot, n.prevSnapshot, t);
							return;
						}
						s._renderSnapshot(n.snapshot, n.snapshot, t), s.projectionError === null && s.mux(() => {
							let e = s.prosemirrorView;
							e && (e.dispatch(s._tr.setMeta(q, {
								restore: !1,
								snapshot: null,
								prevSnapshot: null,
								isChangeOrigin: !0
							})), s._prosemirrorChanged(e.state.doc));
						});
					}
				}), t;
			}
		},
		view: (e) => (s.initView(e), a ?? s._forceRerender(), i(), {
			update: () => {
				let t = Ix(e.state);
				if (s.projectionError === null && t.snapshot == null && t.prevSnapshot == null) {
					let n = e.state.doc.type.createAndFill();
					if (!n) throw Error("The schema cannot create an empty document");
					if (o || e.state.doc.content.findDiffStart(n.content) !== null) {
						if (o = !0, t.addToHistory === !1 && !t.isChangeOrigin) {
							let t = Fx.getState(e.state), n = t && t.undoManager;
							n && n.stopCapturing();
						}
						s.mux(() => {
							t.doc.transact((n) => {
								n.meta.set("addToHistory", t.addToHistory), s._prosemirrorChanged(e.state.doc, t.positionMigration ?? null);
							}, q);
						});
					}
				}
			},
			destroy: () => {
				s.destroy();
			}
		})
	});
};
function JS(e) {
	let t = Fx.getState(e);
	if (!t) throw Error("The editor requires yUndoPlugin");
	return t;
}
var YS = (e) => {
	let t = /* @__PURE__ */ new Map(), n = (e) => {
		e.toArray().forEach((r) => {
			if (r instanceof G) {
				let i = r._item.id;
				t.set(`${i.client}:${i.clock}`, {
					client: i.client,
					clock: i.clock,
					type: e._item?.id ?? null,
					...e._item ? {} : { tname: cv(e) }
				}), n(r);
				return;
			}
			if (!(r instanceof gb) || !r._item) return;
			let i = r._item.id;
			TS(r).forEach((e) => {
				t.set(`${e.client}:${e.clock}`, {
					client: e.client,
					clock: e.clock,
					type: i
				});
			});
		});
	};
	return n(e), t;
}, XS = (e, t) => {
	let n = e.doc;
	if (!n) return;
	let r = YS(e), i = /* @__PURE__ */ new Map(), a = /* @__PURE__ */ new Map(), o = (e, t) => {
		let n = `${e.client}:${e.clock}`, r = `${t.client}:${t.clock}`;
		i.set(n, e), i.set(r, t);
		let o = a.get(n) ?? /* @__PURE__ */ new Set(), s = a.get(r) ?? /* @__PURE__ */ new Set();
		o.add(r), s.add(n), a.set(n, o), a.set(r, s);
	};
	t.forEach((e) => e.transfers.forEach(({ source: e, target: t }) => {
		if (t && !e.boundary) for (let n = 0; n < e.length; n += 1) o(V(e.client, e.clock + n), V(t.client, t.clock + n));
	})), n.store.clients.forEach((e) => e.forEach((e) => {
		if (e instanceof K && e.redone && (e.content instanceof Lb || e.content instanceof qb)) for (let t = 0; t < e.length; t += 1) o(V(e.id.client, e.id.clock + t), V(e.redone.client, e.redone.clock + t));
	}));
	let s = [], c = /* @__PURE__ */ new Set();
	a.forEach((e, t) => {
		if (c.has(t)) return;
		let o = [t], l = [], u = [];
		for (; o.length > 0;) {
			let e = o.pop();
			if (c.has(e)) continue;
			c.add(e), l.push(e);
			let t = r.get(e);
			t && u.push(t), o.push(...a.get(e) ?? []);
		}
		u.length === 1 && l.forEach((e) => {
			if (r.has(e)) return;
			let t = i.get(e);
			s.push({
				source: {
					client: t.client,
					clock: t.clock,
					length: 1
				},
				target: u[0]
			});
			let a = Mv(n.store, t);
			if (a.content instanceof qb && a.content.type instanceof G) for (let e of ["start", "end"]) s.push({
				source: {
					client: t.client,
					clock: t.clock,
					length: 1,
					boundary: e
				},
				target: {
					client: u[0].client,
					clock: u[0].clock,
					type: null,
					boundary: e
				}
			});
		});
	}), AS(t, s);
}, ZS = (e, t, n) => {
	let r = Dx(e);
	if (r.size === 0) return t[n]();
	let i = n === "undo" ? "undoing" : "redoing", a = null;
	try {
		t.doc.transact(() => {
			a = t[n](), t[i] = !0, a && XS(e, r);
		}, t);
	} finally {
		t[i] = !1;
	}
	return a;
}, QS = (e, t) => {
	let n = Ix(e);
	if (n.binding.projectionError !== null || n.snapshot != null || n.prevSnapshot != null) return !1;
	let r = JS(e).undoManager;
	if (r != null) return ZS(n.type, r, t), !0;
}, $S = (e) => QS(e, "undo"), eC = (e) => QS(e, "redo"), tC = /* @__PURE__ */ new Set(["paragraph"]), nC = (e, t) => !(e instanceof K) || !(e.content instanceof qb) || !(e.content.type instanceof sb || e.content.type instanceof G && t.has(e.content.type.nodeName)) || e.content.type._length === 0, rC = ({ protectedNodes: e = tC, trackedOrigins: t = [], undoManager: n = null } = {}) => new E({
	key: Fx,
	state: {
		init: (r, i) => {
			let a = Ix(i), o = /* @__PURE__ */ new Set(), s = n;
			if (!s) {
				let n = new Set(a.doc._observers.get("destroy"));
				s = new Jv(a.type, {
					trackedOrigins: /* @__PURE__ */ new Set([q, ...t]),
					deleteFilter: (t) => nC(t, e),
					captureTransaction: (e) => e.meta.get("addToHistory") !== !1
				}), (a.doc._observers.get("destroy") ?? /* @__PURE__ */ new Set()).forEach((e) => {
					n.has(e) || o.add(e);
				});
			}
			return {
				undoManager: s,
				prevSel: null,
				hasUndoOps: s.undoStack.length > 0,
				hasRedoOps: s.redoStack.length > 0,
				destroyListeners: o
			};
		},
		apply: (e, t, n, r) => {
			let i = Ix(r).binding;
			return {
				...t,
				prevSel: i ? qx(i, n) : t.prevSel,
				hasUndoOps: t.undoManager.undoStack.length > 0,
				hasRedoOps: t.undoManager.redoStack.length > 0
			};
		}
	},
	view: (e) => {
		let t = Ix(e.state), { undoManager: n, destroyListeners: r } = JS(e.state);
		return n.on("stack-item-added", ({ stackItem: n }) => {
			let r = t.binding;
			r && n.meta.set(r, JS(e.state).prevSel);
		}), n.on("stack-item-popped", ({ stackItem: e }) => {
			let n = t.binding;
			n && (n.beforeTransactionSelection = e.meta.get(n) || n.beforeTransactionSelection);
		}), { destroy: () => {
			n.destroy(), r.forEach((e) => n.doc.off("destroy", e)), r.clear();
		} };
	}
});
function iC(e, t) {
	let n = t || new ub();
	return wS(n.doc ?? { transact: (e) => e() }, n, e, {
		mapping: /* @__PURE__ */ new Map(),
		isOMark: /* @__PURE__ */ new Map()
	}), n;
}
function aC(e, t = "prosemirror") {
	let n = new D_(), r = n.getXmlFragment(t);
	return r.doc ? (iC(e, r), r.doc) : n;
}
function oC(e, t, n = "prosemirror") {
	return aC(de.fromJSON(e, t), n);
}
function sC(e) {
	let t = e.toArray(), n = (e) => {
		let t;
		if (e instanceof gb) t = e.toDelta().map((e) => {
			let t = {
				type: "text",
				text: e.insert
			}, n = e.attributes;
			return n && (t.marks = Object.keys(n).map((e) => {
				let t = n[e], r = { type: Xx(e) };
				return Object.keys(t) && (r.attrs = t), r;
			})), t;
		});
		else if (e instanceof G) {
			t = { type: e.nodeName };
			let r = e.getAttributes();
			Object.keys(r).length && (t.attrs = r);
			let i = e.toArray();
			i.length && (t.content = i.map(n).flat());
		} else R();
		return t;
	};
	return {
		type: "doc",
		content: t.map(n)
	};
}
function cC(e, t = "prosemirror") {
	return sC(e.getXmlFragment(t));
}
//#endregion
//#region node_modules/@tiptap/extension-collaboration/dist/index.js
function lC(e) {
	return !!e.getMeta(q);
}
function uC(e, t) {
	let n = q.getState(e);
	return Px(n.doc, n.type, t, n.binding.mapping) || 0;
}
function dC(e, t) {
	let n = q.getState(e);
	return Sx(t, n.type, n.binding.mapping);
}
var fC = class e extends Eu {
	constructor(e, t) {
		super(e), this.yRelativePosition = t;
	}
	static fromJSON(t) {
		return new e(t.position, t.yRelativePosition);
	}
	toJSON() {
		return {
			position: this.position,
			yRelativePosition: this.yRelativePosition
		};
	}
};
function pC(e, t) {
	return new fC(e, dC(t, e));
}
function mC(e, t, n) {
	let r = e instanceof fC ? e.yRelativePosition : null;
	if (lC(t) && r) return {
		position: new fC(uC(n, r), r),
		mapResult: null
	};
	let i = Du(e, t), a = i.position.position;
	return {
		position: new fC(a, r ?? dC(n, a)),
		mapResult: i.mapResult
	};
}
var hC = I.create({
	name: "collaboration",
	priority: 1e3,
	addOptions() {
		return {
			document: null,
			field: "default",
			fragment: null,
			provider: null
		};
	},
	addStorage() {
		return { isDisabled: !1 };
	},
	onCreate() {
		this.editor.extensionManager.extensions.find((e) => e.name === "undoRedo") && console.warn("[tiptap warn]: \"@tiptap/extension-collaboration\" comes with its own history support and is not compatible with \"@tiptap/extension-undo-redo\".");
	},
	onBeforeCreate() {
		this.editor.utils.getUpdatedPosition = (e, t) => mC(e, t, this.editor.state), this.editor.utils.createMappablePosition = (e) => pC(e, this.editor.state);
	},
	addCommands() {
		return {
			undo: () => ({ tr: e, state: t, dispatch: n }) => (e.setMeta("preventDispatch", !0), Fx.getState(t).undoManager.undoStack.length === 0 ? !1 : !n || $S(t)),
			redo: () => ({ tr: e, state: t, dispatch: n }) => (e.setMeta("preventDispatch", !0), Fx.getState(t).undoManager.redoStack.length === 0 ? !1 : !n || eC(t))
		};
	},
	addKeyboardShortcuts() {
		return {
			"Mod-z": () => this.editor.commands.undo(),
			"Mod-y": () => this.editor.commands.redo(),
			"Shift-Mod-z": () => this.editor.commands.redo()
		};
	},
	addProseMirrorPlugins() {
		let e = this.options.fragment ? this.options.fragment : this.options.document.getXmlFragment(this.options.field), t = rC(this.options.yUndoOptions), n = t.spec.view;
		return t.spec.view = (e) => {
			let { undoManager: t } = Fx.getState(e.state);
			t.restore && (t.restore(), t.restore = () => {});
			let r = n ? n(e) : void 0;
			return { destroy: () => {
				let e = t.trackedOrigins.has(t), n = t._observers;
				t.restore = () => {
					e && t.trackedOrigins.add(t), t.doc.on("afterTransaction", t.afterTransactionHandler), t._observers = n;
				}, r?.destroy && r.destroy();
			} };
		}, [
			qS(e, {
				...this.options.ySyncOptions,
				onFirstRender: this.options.onFirstRender
			}),
			t,
			this.editor.options.enableContentCheck && new E({
				key: new D("filterInvalidContent"),
				filterTransaction: (t) => {
					if (!lC(t)) return !0;
					if (this.storage.isDisabled) return !1;
					if (!t.docChanged) return !0;
					try {
						return t.doc.check(), !0;
					} catch (t) {
						return this.storage.isDisabled = !0, this.editor.emit("contentError", {
							error: t,
							editor: this.editor,
							disableCollaboration: () => {
								var t;
								(t = e.doc) == null || t.destroy();
							}
						}), !0;
					}
				}
			})
		].filter(Boolean);
	}
}), gC = /* @__PURE__ */ new WeakSet(), _C = /* @__PURE__ */ new WeakSet();
function vC(e) {
	let t = e;
	return gC.add(t), t;
}
function yC(e) {
	return Array.isArray(e) && gC.has(e);
}
function bC(e) {
	return e.flatMap((e) => e == null ? [] : Array.isArray(e) && _C.has(e) && !yC(e) ? bC(e) : [e]);
}
function xC(e, t) {
	if (e === "slot") return 0;
	if (e instanceof Function) {
		let n = e(t);
		return Array.isArray(n) && !yC(n) && !_C.has(n) ? vC(n) : n;
	}
	let { children: n, ...r } = t ?? {};
	if (e === "svg") throw Error("SVG elements are not supported in the JSX syntax, use the array syntax instead");
	if (Array.isArray(n)) {
		if (yC(n)) return vC([
			e,
			r,
			n
		]);
		if (n.length === 0) return vC([e, r]);
		let t = bC(n);
		return t.length === 0 ? vC([e, r]) : vC([
			e,
			r,
			...t
		]);
	}
	return vC(n == null ? [e, r] : [
		e,
		r,
		n
	]);
}
var SC = (e, t) => xC(e, t), CC = (e, t) => {
	let { state: n } = e, { selection: r } = n;
	if (!r.empty) return !1;
	let { $from: i } = r;
	if (i.parentOffset !== 0) return !1;
	let a = i.depth - 1;
	if (a < 0) return !1;
	let o = i.node(a), s = i.index(a);
	if (s === 0) return !1;
	if (o.type === t) return e.commands.lift(t.name);
	let c = o.child(s - 1);
	if (c.type !== t || !c.lastChild?.isTextblock) return !1;
	let l = i.before() - 1 - 1;
	return e.commands.command(({ tr: e, dispatch: t }) => {
		if (!t) return !0;
		let n = i.parent.content, r = new h(n, 0, 0);
		return e.replace(l, i.after(), r), e.setSelection(w.create(e.doc, l + n.size)), e.scrollIntoView(), t(e), !0;
	});
}, wC = /^\s*>\s$/, TC = Sf.create({
	name: "blockquote",
	addOptions() {
		return { HTMLAttributes: {} };
	},
	content: "block+",
	group: "block",
	defining: !0,
	parseHTML() {
		return [{ tag: "blockquote" }];
	},
	renderHTML({ HTMLAttributes: e }) {
		return /* @__PURE__ */ SC("blockquote", {
			...F(this.options.HTMLAttributes, e),
			children: /* @__PURE__ */ SC("slot", {})
		});
	},
	parseMarkdown: (e, t) => {
		let n = t.parseBlockChildren ?? t.parseChildren;
		return t.createNode("blockquote", void 0, n(e.tokens || []));
	},
	renderMarkdown: (e, t) => {
		if (!e.content) return "";
		let n = [];
		return e.content.forEach((e, r) => {
			let i = (t.renderChild?.call(t, e, r) ?? t.renderChildren([e])).split("\n").map((e) => e.trim() === "" ? ">" : `> ${e}`);
			n.push(i.join("\n"));
		}), n.join("\n>\n");
	},
	addCommands() {
		return {
			setBlockquote: () => ({ commands: e }) => e.wrapIn(this.name),
			toggleBlockquote: () => ({ commands: e }) => e.toggleWrap(this.name),
			unsetBlockquote: () => ({ commands: e }) => e.lift(this.name)
		};
	},
	addKeyboardShortcuts() {
		return {
			"Mod-Shift-b": () => this.editor.commands.toggleBlockquote(),
			Backspace: () => CC(this.editor, this.type)
		};
	},
	addInputRules() {
		return [yf({
			find: wC,
			type: this.type
		})];
	}
}), EC = /(?:^|\s)(\*\*(?!\s+\*\*)((?:[^*]+))\*\*(?!\s+\*\*))$/, DC = /(?:^|\s)(\*\*(?!\s+\*\*)((?:[^*]+))\*\*(?!\s+\*\*))/g, OC = /(?:^|\s)(__(?!\s+__)((?:[^_]+))__(?!\s+__))$/, kC = /(?:^|\s)(__(?!\s+__)((?:[^_]+))__(?!\s+__))/g, AC = Gd.create({
	name: "bold",
	addOptions() {
		return { HTMLAttributes: {} };
	},
	parseHTML() {
		return [
			{ tag: "strong" },
			{
				tag: "b",
				getAttrs: (e) => e.style.fontWeight !== "normal" && null
			},
			{
				style: "font-weight=400",
				clearMark: (e) => e.type.name === this.name
			},
			{
				style: "font-weight",
				getAttrs: (e) => /^(bold(er)?|[5-9]\d{2,})$/.test(e) && null
			}
		];
	},
	renderHTML({ HTMLAttributes: e }) {
		return /* @__PURE__ */ SC("strong", {
			...F(this.options.HTMLAttributes, e),
			children: /* @__PURE__ */ SC("slot", {})
		});
	},
	markdownTokenName: "strong",
	parseMarkdown: (e, t) => t.applyMark("bold", t.parseInline(e.tokens || [])),
	markdownOptions: { htmlReopen: {
		open: "<strong>",
		close: "</strong>"
	} },
	renderMarkdown: (e, t) => `**${t.renderChildren(e)}**`,
	addCommands() {
		return {
			setBold: () => ({ commands: e }) => e.setMark(this.name),
			toggleBold: () => ({ commands: e }) => e.toggleMark(this.name),
			unsetBold: () => ({ commands: e }) => e.unsetMark(this.name)
		};
	},
	addKeyboardShortcuts() {
		return {
			"Mod-b": () => this.editor.commands.toggleBold(),
			"Mod-B": () => this.editor.commands.toggleBold()
		};
	},
	addInputRules() {
		return [gf({
			find: EC,
			type: this.type
		}), gf({
			find: OC,
			type: this.type
		})];
	},
	addPasteRules() {
		return [Cf({
			find: DC,
			type: this.type
		}), Cf({
			find: kC,
			type: this.type
		})];
	}
}), jC = (e) => {
	let t = /`([^`]+)`(?!`)$/.exec(e);
	return !t || t.index > 0 && e[t.index - 1] === "`" ? null : {
		index: t.index,
		text: t[0],
		replaceWith: t[1]
	};
}, MC = (e) => {
	let t = /`([^`]+)`(?!`)/g, n = [], r;
	for (; (r = t.exec(e)) !== null;) r.index > 0 && e[r.index - 1] === "`" || n.push({
		index: r.index,
		text: r[0],
		replaceWith: r[1]
	});
	return n;
}, NC = Gd.create({
	name: "code",
	addOptions() {
		return { HTMLAttributes: {} };
	},
	excludes: "_",
	code: !0,
	exitable: !0,
	parseHTML() {
		return [{ tag: "code" }];
	},
	renderHTML({ HTMLAttributes: e }) {
		return [
			"code",
			F(this.options.HTMLAttributes, e),
			0
		];
	},
	markdownTokenName: "codespan",
	parseMarkdown: (e, t) => t.applyMark("code", [{
		type: "text",
		text: e.text || ""
	}]),
	renderMarkdown: (e, t) => e.content ? `\`${t.renderChildren(e.content)}\`` : "",
	addCommands() {
		return {
			setCode: () => ({ commands: e }) => e.setMark(this.name),
			toggleCode: () => ({ commands: e }) => e.toggleMark(this.name),
			unsetCode: () => ({ commands: e }) => e.unsetMark(this.name)
		};
	},
	addKeyboardShortcuts() {
		return { "Mod-e": () => this.editor.commands.toggleCode() };
	},
	addInputRules() {
		return [gf({
			find: jC,
			type: this.type
		})];
	},
	addPasteRules() {
		return [Cf({
			find: MC,
			type: this.type
		})];
	}
}), PC = 4, FC = /^```([a-z]+)?[\s\n]$/, IC = /^~~~([a-z]+)?[\s\n]$/, LC = Sf.create({
	name: "codeBlock",
	addOptions() {
		return {
			languageClassPrefix: "language-",
			exitOnTripleEnter: !0,
			exitOnArrowDown: !0,
			exitOnArrowUp: !0,
			defaultLanguage: null,
			enableTabIndentation: !1,
			tabSize: PC,
			HTMLAttributes: {}
		};
	},
	content: "text*",
	marks: "",
	group: "block",
	code: !0,
	defining: !0,
	addAttributes() {
		return { language: {
			default: this.options.defaultLanguage,
			parseHTML: (e) => {
				let { languageClassPrefix: t } = this.options;
				return t && [...e.firstElementChild?.classList || []].filter((e) => e.startsWith(t)).map((e) => e.replace(t, ""))[0] || null;
			},
			rendered: !1
		} };
	},
	parseHTML() {
		return [{
			tag: "pre",
			preserveWhitespace: "full"
		}];
	},
	renderHTML({ node: e, HTMLAttributes: t }) {
		return [
			"pre",
			F(this.options.HTMLAttributes, t),
			[
				"code",
				{ class: e.attrs.language ? this.options.languageClassPrefix + e.attrs.language : null },
				0
			]
		];
	},
	markdownTokenName: "code",
	parseMarkdown: (e, t) => e.raw?.startsWith("```") === !1 && e.raw?.startsWith("~~~") === !1 && e.codeBlockStyle !== "indented" ? [] : t.createNode("codeBlock", { language: e.lang || null }, e.text ? [t.createTextNode(e.text)] : []),
	renderMarkdown: (e, t) => {
		let n = "", r = e.attrs?.language || "";
		return n = e.content ? [
			`\`\`\`${r}`,
			t.renderChildren(e.content),
			"```"
		].join("\n") : `\`\`\`${r}\n\n\`\`\``, n;
	},
	addCommands() {
		return {
			setCodeBlock: (e) => ({ commands: t }) => t.setNode(this.name, e),
			toggleCodeBlock: (e) => ({ commands: t }) => t.toggleNode(this.name, "paragraph", e)
		};
	},
	addKeyboardShortcuts() {
		return {
			"Mod-Alt-c": () => this.editor.commands.toggleCodeBlock(),
			Backspace: () => {
				let { empty: e, $anchor: t } = this.editor.state.selection, n = t.pos === 1;
				return !e || t.parent.type.name !== this.name ? !1 : n || !t.parent.textContent.length ? this.editor.commands.clearNodes() : !1;
			},
			Tab: ({ editor: e }) => {
				if (!this.options.enableTabIndentation) return !1;
				let t = this.options.tabSize ?? PC, { state: n } = e, { selection: r } = n, { $from: i, empty: a } = r;
				if (i.parent.type !== this.type) return !1;
				let o = " ".repeat(t);
				return a ? e.commands.insertContent(o) : e.commands.command(({ tr: e }) => {
					let { from: t, to: i } = r, a = n.doc.textBetween(t, i, "\n", "\n").split("\n").map((e) => o + e).join("\n");
					return e.replaceWith(t, i, n.schema.text(a)), !0;
				});
			},
			"Shift-Tab": ({ editor: e }) => {
				if (!this.options.enableTabIndentation) return !1;
				let t = this.options.tabSize ?? PC, { state: n } = e, { selection: r } = n, { $from: i, empty: a } = r;
				return i.parent.type === this.type ? a ? e.commands.command(({ tr: e }) => {
					let { pos: r } = i, a = i.start(), o = i.end(), s = n.doc.textBetween(a, o, "\n", "\n").split("\n"), c = 0, l = 0, u = r - a;
					for (let e = 0; e < s.length; e += 1) {
						if (l + s[e].length >= u) {
							c = e;
							break;
						}
						l += s[e].length + 1;
					}
					let d = s[c].match(/^ */)?.[0] || "", f = Math.min(d.length, t);
					if (f === 0) return !0;
					let p = a;
					for (let e = 0; e < c; e += 1) p += s[e].length + 1;
					return e.delete(p, p + f), r - p <= f && e.setSelection(w.create(e.doc, p)), !0;
				}) : e.commands.command(({ tr: e }) => {
					let { from: i, to: a } = r, o = n.doc.textBetween(i, a, "\n", "\n").split("\n").map((e) => {
						let n = e.match(/^ */)?.[0] || "", r = Math.min(n.length, t);
						return e.slice(r);
					}).join("\n");
					return e.replaceWith(i, a, n.schema.text(o)), !0;
				}) : !1;
			},
			Enter: ({ editor: e }) => {
				if (!this.options.exitOnTripleEnter) return !1;
				let { state: t } = e, { selection: n } = t, { $from: r, empty: i } = n;
				if (!i || r.parent.type !== this.type) return !1;
				let a = r.parentOffset === r.parent.nodeSize - 2, o = r.parent.textContent.endsWith("\n\n");
				return !a || !o ? !1 : e.chain().command(({ tr: e }) => (e.delete(r.pos - 2, r.pos), !0)).exitCode().run();
			},
			ArrowUp: ({ editor: e }) => {
				if (!this.options.exitOnArrowUp) return !1;
				let { state: t } = e, { selection: n } = t, { $from: r, empty: i } = n;
				if (!i || r.parent.type !== this.type || r.parentOffset !== 0) return !1;
				let a = r.before();
				return a > 0 ? !1 : e.commands.insertDefaultBlock({ pos: a });
			},
			ArrowDown: ({ editor: e }) => {
				if (!this.options.exitOnArrowDown) return !1;
				let { state: t } = e, { selection: n, doc: r } = t, { $from: i, empty: a } = n;
				if (!a || i.parent.type !== this.type || i.parentOffset !== i.parent.nodeSize - 2) return !1;
				let o = i.after();
				return o === void 0 ? !1 : r.nodeAt(o) ? e.commands.command(({ tr: e }) => (e.setSelection(C.near(r.resolve(o))), !0)) : e.commands.exitCode();
			}
		};
	},
	addInputRules() {
		return [vf({
			find: FC,
			type: this.type,
			getAttributes: (e) => ({ language: e[1] })
		}), vf({
			find: IC,
			type: this.type,
			getAttributes: (e) => ({ language: e[1] })
		})];
	},
	addProseMirrorPlugins() {
		return [new E({
			key: new D("codeBlockVSCodeHandler"),
			props: { handlePaste: (e, t) => {
				if (!t.clipboardData || this.editor.isActive(this.type.name)) return !1;
				let n = t.clipboardData.getData("text/plain"), r = t.clipboardData.getData("vscode-editor-data"), i = (r ? JSON.parse(r) : void 0)?.mode;
				if (!n || !i) return !1;
				let { tr: a, schema: o } = e.state, s = o.text(n.replace(/\r\n?/g, "\n"));
				return a.replaceSelectionWith(this.type.create({ language: i }, s)), a.selection.$from.parent.type !== this.type && a.setSelection(w.near(a.doc.resolve(Math.max(0, a.selection.from - 2)))), a.setMeta("paste", !0), e.dispatch(a), !0;
			} }
		})];
	}
}), RC = Sf.create({
	name: "doc",
	topNode: !0,
	content: "block+",
	renderMarkdown: (e, t) => e.content ? t.renderChildren(e.content, "\n\n") : ""
}), zC = Sf.create({
	name: "hardBreak",
	markdownTokenName: "br",
	addOptions() {
		return {
			keepMarks: !0,
			HTMLAttributes: {}
		};
	},
	inline: !0,
	group: "inline",
	selectable: !1,
	linebreakReplacement: !0,
	parseHTML() {
		return [{ tag: "br" }];
	},
	renderHTML({ HTMLAttributes: e }) {
		return ["br", F(this.options.HTMLAttributes, e)];
	},
	renderText() {
		return "\n";
	},
	renderMarkdown: () => "  \n",
	parseMarkdown: () => ({ type: "hardBreak" }),
	addCommands() {
		return { setHardBreak: () => ({ commands: e, chain: t, state: n, editor: r }) => e.first([() => e.exitCode(), () => e.command(() => {
			let { selection: e, storedMarks: i } = n;
			if (e.$from.parent.type.spec.isolating) return !1;
			let { keepMarks: a } = this.options, { splittableMarks: o } = r.extensionManager, s = i || e.$to.parentOffset && e.$from.marks();
			return t().insertContent({ type: this.name }).command(({ tr: e, dispatch: t }) => {
				if (t && s && a) {
					let t = s.filter((e) => o.includes(e.type.name));
					e.ensureMarks(t);
				}
				return !0;
			}).scrollIntoView().run();
		})]) };
	},
	addKeyboardShortcuts() {
		return {
			"Mod-Enter": () => this.editor.commands.setHardBreak(),
			"Shift-Enter": () => this.editor.commands.setHardBreak()
		};
	}
}), BC = Sf.create({
	name: "heading",
	addOptions() {
		return {
			levels: [
				1,
				2,
				3,
				4,
				5,
				6
			],
			HTMLAttributes: {}
		};
	},
	content: "inline*",
	group: "block",
	defining: !0,
	addAttributes() {
		return { level: {
			default: 1,
			rendered: !1
		} };
	},
	parseHTML() {
		return this.options.levels.map((e) => ({
			tag: `h${e}`,
			attrs: { level: e }
		}));
	},
	renderHTML({ node: e, HTMLAttributes: t }) {
		return [
			`h${this.options.levels.includes(e.attrs.level) ? e.attrs.level : this.options.levels[0]}`,
			F(this.options.HTMLAttributes, t),
			0
		];
	},
	parseMarkdown: (e, t) => t.createNode("heading", { level: e.depth || 1 }, t.parseInline(e.tokens || [])),
	renderMarkdown: (e, t) => {
		let n = e.attrs?.level ? parseInt(e.attrs.level, 10) : 1, r = "#".repeat(n);
		return e.content ? `${r} ${t.renderChildren(e.content)}` : "";
	},
	addCommands() {
		return {
			setHeading: (e) => ({ commands: t }) => this.options.levels.includes(e.level) ? t.setNode(this.name, e) : !1,
			toggleHeading: (e) => ({ commands: t }) => this.options.levels.includes(e.level) ? t.toggleNode(this.name, "paragraph", e) : !1
		};
	},
	addKeyboardShortcuts() {
		return this.options.levels.reduce((e, t) => ({
			...e,
			[`Mod-Alt-${t}`]: () => this.editor.commands.toggleHeading({ level: t })
		}), {});
	},
	addInputRules() {
		return this.options.levels.map((e) => vf({
			find: RegExp(`^(#{${Math.min(...this.options.levels)},${e}})\\s$`),
			type: this.type,
			getAttributes: { level: e }
		}));
	}
}), VC = Sf.create({
	name: "horizontalRule",
	addOptions() {
		return {
			HTMLAttributes: {},
			nextNodeType: "paragraph"
		};
	},
	group: "block",
	parseHTML() {
		return [{ tag: "hr" }];
	},
	renderHTML({ HTMLAttributes: e }) {
		return ["hr", F(this.options.HTMLAttributes, e)];
	},
	markdownTokenName: "hr",
	parseMarkdown: (e, t) => t.createNode("horizontalRule"),
	renderMarkdown: () => "---",
	addCommands() {
		return { setHorizontalRule: () => ({ chain: e, state: t }) => {
			if (!kd(t, t.schema.nodes[this.name])) return !1;
			let { selection: n } = t, { $to: r } = n, i = e();
			return Tu(n) ? i.insertContentAt(r.pos, { type: this.name }) : i.insertContent({ type: this.name }), i.command(({ state: e, tr: t, dispatch: n }) => {
				if (n) {
					let { $to: n } = t.selection, r = n.end();
					if (n.nodeAfter) n.nodeAfter.isTextblock ? t.setSelection(w.create(t.doc, n.pos + 1)) : n.nodeAfter.isBlock ? t.setSelection(T.create(t.doc, n.pos)) : t.setSelection(w.create(t.doc, n.pos));
					else {
						let i = (e.schema.nodes[this.options.nextNodeType] || n.parent.type.contentMatch.defaultType)?.create();
						i && (t.insert(r, i), t.setSelection(w.create(t.doc, r + 1)));
					}
					t.scrollIntoView();
				}
				return !0;
			}).run();
		} };
	},
	addInputRules() {
		return [_f({
			find: /^(?:---|—-|___\s|\*\*\*\s)$/,
			type: this.type
		})];
	}
}), HC = /(?:^|\s)(\*(?!\s+\*)((?:[^*]+))\*(?!\s+\*))$/, UC = /(?:^|\s)(\*(?!\s+\*)((?:[^*]+))\*(?!\s+\*))/g, WC = /(?:^|\s)(_(?!\s+_)((?:[^_]+))_(?!\s+_))$/, GC = /(?:^|\s)(_(?!\s+_)((?:[^_]+))_(?!\s+_))/g, KC = Gd.create({
	name: "italic",
	addOptions() {
		return { HTMLAttributes: {} };
	},
	parseHTML() {
		return [
			{ tag: "em" },
			{
				tag: "i",
				getAttrs: (e) => e.style.fontStyle !== "normal" && null
			},
			{
				style: "font-style=normal",
				clearMark: (e) => e.type.name === this.name
			},
			{ style: "font-style=italic" }
		];
	},
	renderHTML({ HTMLAttributes: e }) {
		return [
			"em",
			F(this.options.HTMLAttributes, e),
			0
		];
	},
	addCommands() {
		return {
			setItalic: () => ({ commands: e }) => e.setMark(this.name),
			toggleItalic: () => ({ commands: e }) => e.toggleMark(this.name),
			unsetItalic: () => ({ commands: e }) => e.unsetMark(this.name)
		};
	},
	markdownTokenName: "em",
	parseMarkdown: (e, t) => t.applyMark("italic", t.parseInline(e.tokens || [])),
	markdownOptions: { htmlReopen: {
		open: "<em>",
		close: "</em>"
	} },
	renderMarkdown: (e, t) => `*${t.renderChildren(e)}*`,
	addKeyboardShortcuts() {
		return {
			"Mod-i": () => this.editor.commands.toggleItalic(),
			"Mod-I": () => this.editor.commands.toggleItalic()
		};
	},
	addInputRules() {
		return [gf({
			find: HC,
			type: this.type
		}), gf({
			find: WC,
			type: this.type
		})];
	},
	addPasteRules() {
		return [Cf({
			find: UC,
			type: this.type
		}), Cf({
			find: GC,
			type: this.type
		})];
	}
}), qC = "aaa1rp3bb0ott3vie4c1le2ogado5udhabi7c0ademy5centure6ountant0s9o1tor4d0s1ult4e0g1ro2tna4f0l1rica5g0akhan5ency5i0g1rbus3force5tel5kdn3l0ibaba4pay4lfinanz6state5y2sace3tom5m0azon4ericanexpress7family11x2fam3ica3sterdam8nalytics7droid5quan4z2o0l2partments8p0le4q0uarelle8r0ab1mco4chi3my2pa2t0e3s0da2ia2sociates9t0hleta5torney7u0ction5di0ble3o3spost5thor3o0s4w0s2x0a2z0ure5ba0by2idu3namex4d1k2r0celona5laycard4s5efoot5gains6seball5ketball8uhaus5yern5b0c1t1va3cg1n2d1e0ats2uty4er2rlin4st0buy5t2f1g1h0arti5i0ble3d1ke2ng0o3o1z2j1lack0friday9ockbuster8g1omberg7ue3m0s1w2n0pparibas9o0ats3ehringer8fa2m1nd2o0k0ing5sch2tik2on4t1utique6x2r0adesco6idgestone9oadway5ker3ther5ussels7s1t1uild0ers6siness6y1zz3v1w1y1z0h3ca0b1fe2l0l1vinklein9m0era3p2non3petown5ital0one8r0avan4ds2e0er0s4s2sa1e1h1ino4t0ering5holic7ba1n1re3c1d1enter4o1rn3f0a1d2g1h0anel2nel4rity4se2t2eap3intai5ristmas6ome4urch5i0priani6rcle4sco3tadel4i0c2y3k1l0aims4eaning6ick2nic1que6othing5ud3ub0med6m1n1o0ach3des3ffee4llege4ogne5m0mbank4unity6pany2re3uter5sec4ndos3struction8ulting7tact3ractors9oking4l1p2rsica5untry4pon0s4rses6pa2r0edit0card4union9icket5own3s1uise0s6u0isinella9v1w1x1y0mru3ou3z2dad1nce3ta1e1ing3sun4y2clk3ds2e0al0er2s3gree4livery5l1oitte5ta3mocrat6ntal2ist5si0gn4v2hl2iamonds6et2gital5rect0ory7scount3ver5h2y2j1k1m1np2o0cs1tor4g1mains5t1wnload7rive4tv2ubai3pont4rban5vag2r2z2earth3t2c0o2deka3u0cation8e1g1mail3erck5nergy4gineer0ing9terprises10pson4quipment8r0icsson6ni3s0q1tate5t1u0rovision8s2vents5xchange6pert3osed4ress5traspace10fage2il1rwinds6th3mily4n0s2rm0ers5shion4t3edex3edback6rrari3ero6i0delity5o2lm2nal1nce1ial7re0stone6mdale6sh0ing5t0ness6j1k1lickr3ghts4r2orist4wers5y2m1o0o0d1tball6rd1ex2sale4um3undation8x2r0ee1senius7l1ogans4ntier7tr2ujitsu5n0d2rniture7tbol5yi3ga0l0lery3o1up4me0s3p1rden4y2b0iz3d0n2e0a1nt0ing5orge5f1g0ee3h1i0ft0s3ves2ing5l0ass3e1obal2o4m0ail3bh2o1x2n1odaddy5ld0point6f2odyear5g0le4p1t1v2p1q1r0ainger5phics5tis4een3ipe3ocery4up4s1t1u0cci3ge2ide2tars5ru3w1y2hair2mburg5ngout5us3bo2dfc0bank7ealth0care8lp1sinki6re1mes5iphop4samitsu7tachi5v2k0t2m1n1ockey4ldings5iday5medepot5goods5s0ense7nda3rse3spital5t0ing5t0els3mail5use3w2r1sbc3t1u0ghes5yatt3undai7ibm2cbc2e1u2d1e0ee3fm2kano4l1m0amat4db2mo0bilien9n0c1dustries8finiti5o2g1k1stitute6urance4e4t0ernational10uit4vestments10o1piranga7q1r0ish4s0maili5t0anbul7t0au2v3jaguar4va3cb2e0ep2tzt3welry6io2ll2m0p2nj2o0bs1urg4t1y2p0morgan6rs3uegos4niper7kaufen5ddi3e0rryhotels6properties14fh2g1h1i0a1ds2m1ndle4tchen5wi3m1n1oeln3matsu5sher5p0mg2n2r0d1ed3uokgroup8w1y0oto4z2la0caixa5mborghini8er3nd0rover6xess5salle5t0ino3robe5w0yer5b1c1ds2ease3clerc5frak4gal2o2xus4gbt3i0dl2fe0insurance9style7ghting6ke2lly3mited4o2ncoln4k2ve1ing5k1lc1p2oan0s3cker3us3l1ndon4tte1o3ve3pl0financial11r1s1t0d0a3u0ndbeck6xe1ury5v1y2ma0drid4if1son4keup4n0agement7go3p1rket0ing3s4riott5shalls7ttel5ba2c0kinsey7d1e0d0ia3et2lbourne7me1orial6n0u2rck0msd7g1h1iami3crosoft7l1ni1t2t0subishi9k1l0b1s2m0a2n1o0bi0le4da2e1i1m1nash3ey2ster5rmon3tgage6scow4to0rcycles9v0ie4p1q1r1s0d2t0n1r2u0seum3ic4v1w1x1y1z2na0b1goya4me2vy3ba2c1e0c1t0bank4flix4work5ustar5w0s2xt0direct7us4f0l2g0o2hk2i0co2ke1on3nja3ssan1y5l1o0kia3rton4w0ruz3tv4p1r0a1w2tt2u1yc2z2obi1server7ffice5kinawa6layan0group9lo3m0ega4ne1g1l0ine5oo2pen3racle3nge4g0anic5igins6saka4tsuka4t2vh3pa0ge2nasonic7ris2s1tners4s1y3y2ccw3e0t2f0izer5g1h0armacy6d1ilips5one2to0graphy6s4ysio5ics1tet2ures6d1n0g1k2oneer5zza4k1l0ace2y0station9umbing5s3m1n0c2ohl2ker3litie5rn2st3r0axi3ess3ime3o0d0uctions8f1gressive8mo2perties3y5tection8u0dential9s1t1ub2w0c2y2qa1pon3uebec3st5racing4dio4e0ad1lestate6tor2y4cipes5d0umbrella9hab3ise0n3t2liance6n0t0als5pair3ort3ublican8st0aurant8view0s5xroth6ich0ardli6oh3l1o1p2o0cks3deo3gers4om3s0vp3u0gby3hr2n2w0e2yukyu6sa0arland6fe0ty4kura4le1on3msclub4ung5ndvik0coromant12ofi4p1rl2s1ve2xo3b0i1s2c0b1haeffler7midt4olarships8ol3ule3warz5ience5ot3d1e0arch3t2cure1ity6ek2lect4ner3rvices6ven3w1x0y3fr2g1h0angrila6rp3ell3ia1ksha5oes2p0ping5uji3w3i0lk2na1gles5te3j1k0i0n2y0pe4l0ing4m0art3ile4n0cf3o0ccer3ial4ftbank4ware6hu2lar2utions7ng1y2y2pa0ce3ort2t3r0l2s1t0ada2ples4r1tebank4farm7c0group6ockholm6rage3e3ream4udio2y3yle4u0cks3pplies3y2ort5rf1gery5zuki5v1watch4iss4x1y0dney4stems6z2tab1ipei4lk2obao4rget4tamotors6r2too4x0i3c0i2d0k2eam2ch0nology8l1masek5nnis4va3f1g1h0d1eater2re6iaa2ckets5enda4ps2res2ol4j0maxx4x2k0maxx5l1m0all4n1o0day3kyo3ols3p1ray3shiba5tal3urs3wn2yota3s3r0ade1ing4ining5vel0ers0insurance16ust3v2t1ube2i1nes3shu4v0s2w1z2ua1bank3s2g1k1nicom3versity8o2ol2ps2s1y1z2va0cations7na1guard7c1e0gas3ntures6risign5mögensberater2ung14sicherung10t2g1i0ajes4deo3g1king4llas4n1p1rgin4sa1ion4va1o3laanderen9n1odka3lvo3te1ing3o2yage5u2wales2mart4ter4ng0gou5tch0es6eather0channel12bcam3er2site5d0ding5ibo2r3f1hoswho6ien2ki2lliamhill9n0dows4e1ners6me2oodside6rk0s2ld3w2s1tc1f3xbox3erox4ihuan4n2xx2yz3yachts4hoo3maxun5ndex5e1odobashi7ga2kohama6u0tube6t1un3za0ppos4ra3ero3ip2m1one3uerich6w2", JC = "ελ1υ2бг1ел3дети4ею2католик6ом3мкд2он1сква6онлайн5рг3рус2ф2сайт3рб3укр3қаз3հայ3ישראל5קום3ابوظبي5رامكو5لاردن4بحرين5جزائر5سعودية6عليان5مغرب5مارات5یران5بارت2زار4يتك3ھارت5تونس4سودان3رية5شبكة4عراق2ب2مان4فلسطين6قطر3كاثوليك6وم3مصر2ليسيا5وريتانيا7قع4همراه5پاکستان7ڀارت4कॉम3नेट3भारत0म्3ोत5संगठन5বাংলা5ভারত2ৰত4ਭਾਰਤ4ભારત4ଭାରତ4இந்தியா6லங்கை6சிங்கப்பூர்11భారత్5ಭಾರತ4ഭാരതം5ලංකා4คอม3ไทย3ລາວ3გე2みんな3アマゾン4クラウド4グーグル4コム2ストア3セール3ファッション6ポイント4世界2中信1国1國1文网3亚马逊3企业2佛山2信息2健康2八卦2公司1益2台湾1灣2商城1店1标2嘉里0大酒店5在线2大拿2天主教3娱乐2家電2广东2微博2慈善2我爱你3手机2招聘2政务1府2新加坡2闻2时尚2書籍2机构2淡马锡3游戏2澳門2点看2移动2组织机构4网址1店1站1络2联通2谷歌2购物2通販2集团2電訊盈科4飞利浦3食品2餐厅2香格里拉3港2닷넷1컴2삼성2한국2", YC = "numeric", XC = "ascii", ZC = "alpha", QC = "asciinumeric", $C = "alphanumeric", ew = "domain", tw = "emoji", nw = "scheme", rw = "slashscheme", iw = "whitespace";
function aw(e, t) {
	return e in t || (t[e] = []), t[e];
}
function ow(e, t, n) {
	t[YC] && (t[QC] = !0, t[$C] = !0), t[XC] && (t[QC] = !0, t[ZC] = !0), t[QC] && (t[$C] = !0), t[ZC] && (t[$C] = !0), t[$C] && (t[ew] = !0), t[tw] && (t[ew] = !0);
	for (let r in t) {
		let t = aw(r, n);
		t.indexOf(e) < 0 && t.push(e);
	}
}
function sw(e, t) {
	let n = {};
	for (let r in t) t[r].indexOf(e) >= 0 && (n[r] = !0);
	return n;
}
function cw(e = null) {
	this.j = {}, this.jr = [], this.jd = null, this.t = e;
}
cw.groups = {}, cw.prototype = {
	accepts() {
		return !!this.t;
	},
	go(e) {
		let t = this, n = t.j[e];
		if (n) return n;
		for (let n = 0; n < t.jr.length; n++) {
			let r = t.jr[n][0], i = t.jr[n][1];
			if (i && r.test(e)) return i;
		}
		return t.jd;
	},
	has(e, t = !1) {
		return t ? e in this.j : !!this.go(e);
	},
	ta(e, t, n, r) {
		for (let i = 0; i < e.length; i++) this.tt(e[i], t, n, r);
	},
	tr(e, t, n, r) {
		r = r || cw.groups;
		let i;
		return t && t.j ? i = t : (i = new cw(t), n && r && ow(t, n, r)), this.jr.push([e, i]), i;
	},
	ts(e, t, n, r) {
		let i = this, a = e.length;
		if (!a) return i;
		for (let t = 0; t < a - 1; t++) i = i.tt(e[t]);
		return i.tt(e[a - 1], t, n, r);
	},
	tt(e, t, n, r) {
		r = r || cw.groups;
		let i = this;
		if (t && t.j) return i.j[e] = t, t;
		let a = t, o, s = i.go(e);
		return s ? (o = new cw(), Object.assign(o.j, s.j), o.jr.push.apply(o.jr, s.jr), o.jd = s.jd, o.t = s.t) : o = new cw(), a && (r && (o.t && typeof o.t == "string" ? ow(a, Object.assign(sw(o.t, r), n), r) : n && ow(a, n, r)), o.t = a), i.j[e] = o, o;
	}
};
var J = (e, t, n, r, i) => e.ta(t, n, r, i), Y = (e, t, n, r, i) => e.tr(t, n, r, i), lw = (e, t, n, r, i) => e.ts(t, n, r, i), X = (e, t, n, r, i) => e.tt(t, n, r, i), uw = "WORD", dw = "UWORD", fw = "ASCIINUMERICAL", pw = "ALPHANUMERICAL", mw = "LOCALHOST", hw = "TLD", gw = "UTLD", _w = "SCHEME", vw = "SLASH_SCHEME", yw = "NUM", bw = "WS", xw = "NL", Sw = "OPENBRACE", Cw = "CLOSEBRACE", ww = "OPENBRACKET", Tw = "CLOSEBRACKET", Ew = "OPENPAREN", Dw = "CLOSEPAREN", Ow = "OPENANGLEBRACKET", kw = "CLOSEANGLEBRACKET", Aw = "FULLWIDTHLEFTPAREN", jw = "FULLWIDTHRIGHTPAREN", Mw = "LEFTCORNERBRACKET", Nw = "RIGHTCORNERBRACKET", Pw = "LEFTWHITECORNERBRACKET", Fw = "RIGHTWHITECORNERBRACKET", Iw = "FULLWIDTHLESSTHAN", Lw = "FULLWIDTHGREATERTHAN", Rw = "AMPERSAND", zw = "APOSTROPHE", Bw = "ASTERISK", Vw = "AT", Hw = "BACKSLASH", Uw = "BACKTICK", Ww = "CARET", Gw = "COLON", Kw = "COMMA", qw = "DOLLAR", Jw = "DOT", Yw = "EQUALS", Xw = "EXCLAMATION", Zw = "HYPHEN", Qw = "PERCENT", $w = "PIPE", eT = "PLUS", tT = "POUND", nT = "QUERY", rT = "QUOTE", iT = "FULLWIDTHMIDDLEDOT", aT = "SEMI", oT = "SLASH", sT = "TILDE", cT = "UNDERSCORE", lT = "EMOJI", uT = "SYM", dT = /*#__PURE__*/ Object.freeze({
	__proto__: null,
	ALPHANUMERICAL: pw,
	AMPERSAND: Rw,
	APOSTROPHE: zw,
	ASCIINUMERICAL: fw,
	ASTERISK: Bw,
	AT: Vw,
	BACKSLASH: Hw,
	BACKTICK: Uw,
	CARET: Ww,
	CLOSEANGLEBRACKET: kw,
	CLOSEBRACE: Cw,
	CLOSEBRACKET: Tw,
	CLOSEPAREN: Dw,
	COLON: Gw,
	COMMA: Kw,
	DOLLAR: qw,
	DOT: Jw,
	EMOJI: lT,
	EQUALS: Yw,
	EXCLAMATION: Xw,
	FULLWIDTHGREATERTHAN: Lw,
	FULLWIDTHLEFTPAREN: Aw,
	FULLWIDTHLESSTHAN: Iw,
	FULLWIDTHMIDDLEDOT: iT,
	FULLWIDTHRIGHTPAREN: jw,
	HYPHEN: Zw,
	LEFTCORNERBRACKET: Mw,
	LEFTWHITECORNERBRACKET: Pw,
	LOCALHOST: mw,
	NL: xw,
	NUM: yw,
	OPENANGLEBRACKET: Ow,
	OPENBRACE: Sw,
	OPENBRACKET: ww,
	OPENPAREN: Ew,
	PERCENT: Qw,
	PIPE: $w,
	PLUS: eT,
	POUND: tT,
	QUERY: nT,
	QUOTE: rT,
	RIGHTCORNERBRACKET: Nw,
	RIGHTWHITECORNERBRACKET: Fw,
	SCHEME: _w,
	SEMI: aT,
	SLASH: oT,
	SLASH_SCHEME: vw,
	SYM: uT,
	TILDE: sT,
	TLD: hw,
	UNDERSCORE: cT,
	UTLD: gw,
	UWORD: dw,
	WORD: uw,
	WS: bw
}), fT = /[a-z]/, pT = /\p{L}/u, mT = /\p{Emoji}/u, hT = /\d/, gT = /\s/, _T = "\r", vT = "\n", yT = "️", bT = "‍", xT = "￼", ST = null, CT = null;
function wT(e = []) {
	let t = {};
	cw.groups = t;
	let n = new cw();
	ST ?? (ST = OT(qC)), CT ?? (CT = OT(JC)), X(n, "'", zw), X(n, "{", Sw), X(n, "}", Cw), X(n, "[", ww), X(n, "]", Tw), X(n, "(", Ew), X(n, ")", Dw), X(n, "<", Ow), X(n, ">", kw), X(n, "（", Aw), X(n, "）", jw), X(n, "「", Mw), X(n, "」", Nw), X(n, "『", Pw), X(n, "』", Fw), X(n, "＜", Iw), X(n, "＞", Lw), X(n, "&", Rw), X(n, "*", Bw), X(n, "@", Vw), X(n, "`", Uw), X(n, "^", Ww), X(n, ":", Gw), X(n, ",", Kw), X(n, "$", qw), X(n, ".", Jw), X(n, "=", Yw), X(n, "!", Xw), X(n, "-", Zw), X(n, "%", Qw), X(n, "|", $w), X(n, "+", eT), X(n, "#", tT), X(n, "?", nT), X(n, "\"", rT), X(n, "/", oT), X(n, ";", aT), X(n, "~", sT), X(n, "_", cT), X(n, "\\", Hw), X(n, "・", iT);
	let r = Y(n, hT, yw, { [YC]: !0 });
	Y(r, hT, r);
	let i = Y(r, fT, fw, { [QC]: !0 }), a = Y(r, pT, pw, { [$C]: !0 }), o = Y(n, fT, uw, { [XC]: !0 });
	Y(o, hT, i), Y(o, fT, o), Y(i, hT, i), Y(i, fT, i);
	let s = Y(n, pT, dw, { [ZC]: !0 });
	Y(s, fT), Y(s, hT, a), Y(s, pT, s), Y(a, hT, a), Y(a, fT), Y(a, pT, a);
	let c = X(n, vT, xw, { [iw]: !0 }), l = X(n, _T, bw, { [iw]: !0 }), u = Y(n, gT, bw, { [iw]: !0 });
	X(n, xT, u), X(l, vT, c), X(l, xT, u), Y(l, gT, u), X(u, _T), X(u, vT), Y(u, gT, u), X(u, xT, u);
	let d = Y(n, mT, lT, { [tw]: !0 });
	X(d, "#"), Y(d, mT, d), X(d, yT, d);
	let f = X(d, bT);
	X(f, "#"), Y(f, mT, d);
	let p = [[fT, o], [hT, i]], m = [
		[fT, null],
		[pT, s],
		[hT, a]
	];
	for (let e = 0; e < ST.length; e++) DT(n, ST[e], hw, uw, p);
	for (let e = 0; e < CT.length; e++) DT(n, CT[e], gw, dw, m);
	ow(hw, {
		tld: !0,
		ascii: !0
	}, t), ow(gw, {
		utld: !0,
		alpha: !0
	}, t), DT(n, "file", _w, uw, p), DT(n, "mailto", _w, uw, p), DT(n, "http", vw, uw, p), DT(n, "https", vw, uw, p), DT(n, "ftp", vw, uw, p), DT(n, "ftps", vw, uw, p), ow(_w, {
		scheme: !0,
		ascii: !0
	}, t), ow(vw, {
		slashscheme: !0,
		ascii: !0
	}, t), e = e.sort((e, t) => e[0] > t[0] ? 1 : -1);
	for (let t = 0; t < e.length; t++) {
		let r = e[t][0], i = e[t][1] ? { [nw]: !0 } : { [rw]: !0 };
		r.indexOf("-") >= 0 ? i[ew] = !0 : fT.test(r) ? hT.test(r) ? i[QC] = !0 : i[XC] = !0 : i[YC] = !0, lw(n, r, r, i);
	}
	return lw(n, "localhost", mw, { ascii: !0 }), n.jd = new cw(uT), {
		start: n,
		tokens: Object.assign({ groups: t }, dT)
	};
}
function TT(e, t) {
	let n = ET(t.replace(/[A-Z]/g, (e) => e.toLowerCase())), r = n.length, i = [], a = 0, o = 0;
	for (; o < r;) {
		let s = e, c = null, l = 0, u = null, d = -1, f = -1;
		for (; o < r && (c = s.go(n[o]));) s = c, s.accepts() ? (d = 0, f = 0, u = s) : d >= 0 && (d += n[o].length, f++), l += n[o].length, a += n[o].length, o++;
		a -= d, o -= f, l -= d, i.push({
			t: u.t,
			v: t.slice(a - l, a),
			s: a - l,
			e: a
		});
	}
	return i;
}
function ET(e) {
	let t = [], n = e.length, r = 0;
	for (; r < n;) {
		let i = e.charCodeAt(r), a, o = i < 55296 || i > 56319 || r + 1 === n || (a = e.charCodeAt(r + 1)) < 56320 || a > 57343 ? e[r] : e.slice(r, r + 2);
		t.push(o), r += o.length;
	}
	return t;
}
function DT(e, t, n, r, i) {
	let a, o = t.length;
	for (let n = 0; n < o - 1; n++) {
		let o = t[n];
		e.j[o] ? a = e.j[o] : (a = new cw(r), a.jr = i.slice(), e.j[o] = a), e = a;
	}
	return a = new cw(n), a.jr = i.slice(), e.j[t[o - 1]] = a, a;
}
function OT(e) {
	let t = [], n = [], r = 0;
	for (; r < e.length;) {
		let i = 0;
		for (; "0123456789".indexOf(e[r + i]) >= 0;) i++;
		if (i > 0) {
			t.push(n.join(""));
			for (let t = parseInt(e.substring(r, r + i), 10); t > 0; t--) n.pop();
			r += i;
		} else n.push(e[r]), r++;
	}
	return t;
}
var kT = {
	defaultProtocol: "http",
	events: null,
	format: jT,
	formatHref: jT,
	nl2br: !1,
	tagName: "a",
	target: null,
	rel: null,
	validate: !0,
	truncate: Infinity,
	className: null,
	attributes: null,
	ignoreTags: [],
	render: null
};
function AT(e, t = null) {
	let n = Object.assign({}, kT);
	e && (n = Object.assign(n, e instanceof AT ? e.o : e));
	let r = n.ignoreTags, i = [];
	for (let e = 0; e < r.length; e++) i.push(r[e].toUpperCase());
	this.o = n, t && (this.defaultRender = t), this.ignoreTags = i;
}
AT.prototype = {
	o: kT,
	ignoreTags: [],
	defaultRender(e) {
		return e;
	},
	check(e) {
		return this.get("validate", e.toString(), e);
	},
	get(e, t, n) {
		let r = t != null, i = this.o[e];
		return i && (typeof i == "object" ? (i = n.t in i ? i[n.t] : kT[e], typeof i == "function" && r && (i = i(t, n))) : typeof i == "function" && r && (i = i(t, n.t, n)), i);
	},
	getObj(e, t, n) {
		let r = this.o[e];
		return typeof r == "function" && t != null && (r = r(t, n.t, n)), r;
	},
	render(e) {
		let t = e.render(this);
		return (this.get("render", null, e) || this.defaultRender)(t, e.t, e);
	}
};
function jT(e) {
	return e;
}
function MT(e, t) {
	this.t = "token", this.v = e, this.tk = t;
}
MT.prototype = {
	isLink: !1,
	toString() {
		return this.v;
	},
	toHref(e) {
		return this.toString();
	},
	toFormattedString(e) {
		let t = this.toString(), n = e.get("truncate", t, this), r = e.get("format", t, this);
		return n && r.length > n ? r.substring(0, n) + "…" : r;
	},
	toFormattedHref(e) {
		return e.get("formatHref", this.toHref(e.get("defaultProtocol")), this);
	},
	startIndex() {
		return this.tk[0].s;
	},
	endIndex() {
		return this.tk[this.tk.length - 1].e;
	},
	toObject(e = kT.defaultProtocol) {
		return {
			type: this.t,
			value: this.toString(),
			isLink: this.isLink,
			href: this.toHref(e),
			start: this.startIndex(),
			end: this.endIndex()
		};
	},
	toFormattedObject(e) {
		return {
			type: this.t,
			value: this.toFormattedString(e),
			isLink: this.isLink,
			href: this.toFormattedHref(e),
			start: this.startIndex(),
			end: this.endIndex()
		};
	},
	validate(e) {
		return e.get("validate", this.toString(), this);
	},
	render(e) {
		let t = this, n = this.toHref(e.get("defaultProtocol")), r = e.get("formatHref", n, this), i = e.get("tagName", n, t), a = this.toFormattedString(e), o = {}, s = e.get("className", n, t), c = e.get("target", n, t), l = e.get("rel", n, t), u = e.getObj("attributes", n, t), d = e.getObj("events", n, t);
		return o.href = r, s && (o.class = s), c && (o.target = c), l && (o.rel = l), u && Object.assign(o, u), {
			tagName: i,
			attributes: o,
			content: a,
			eventListeners: d
		};
	}
};
function NT(e, t) {
	class n extends MT {
		constructor(t, n) {
			super(t, n), this.t = e;
		}
	}
	for (let e in t) n.prototype[e] = t[e];
	return n.t = e, n;
}
var PT = NT("email", {
	isLink: !0,
	toHref() {
		return "mailto:" + this.toString();
	}
}), FT = NT("text"), IT = NT("nl"), LT = NT("url", {
	isLink: !0,
	toHref(e = kT.defaultProtocol) {
		return this.hasProtocol() ? this.v : `${e}://${this.v}`;
	},
	hasProtocol() {
		let e = this.tk;
		return e.length >= 2 && e[0].t !== mw && e[1].t === Gw;
	}
}), RT = (e) => new cw(e);
function zT({ groups: e }) {
	let t = e.domain.concat([
		Rw,
		Bw,
		Vw,
		Hw,
		Uw,
		Ww,
		qw,
		Yw,
		Zw,
		yw,
		Qw,
		$w,
		eT,
		tT,
		oT,
		uT,
		sT,
		cT
	]), n = [
		zw,
		Gw,
		Kw,
		Jw,
		Xw,
		Qw,
		nT,
		rT,
		aT,
		Ow,
		kw,
		Sw,
		Cw,
		Tw,
		ww,
		Ew,
		Dw,
		Aw,
		jw,
		Mw,
		Nw,
		Pw,
		Fw,
		Iw,
		Lw
	], r = [
		Rw,
		zw,
		Bw,
		Hw,
		Uw,
		Ww,
		qw,
		Yw,
		Zw,
		Sw,
		Cw,
		Qw,
		$w,
		eT,
		tT,
		nT,
		oT,
		uT,
		sT,
		cT
	], i = RT(), a = X(i, sT);
	J(a, r, a), J(a, e.domain, a);
	let o = RT(), s = RT(), c = RT();
	J(i, e.domain, o), J(i, e.scheme, s), J(i, e.slashscheme, c), J(o, r, a), J(o, e.domain, o);
	let l = X(o, Vw);
	X(a, Vw, l), X(s, Vw, l), X(c, Vw, l);
	let u = X(a, Jw);
	J(u, r, a), J(u, e.domain, a);
	let d = RT();
	J(l, e.domain, d), J(d, e.domain, d);
	let f = X(d, Jw);
	J(f, e.domain, d);
	let p = RT(PT);
	J(f, e.tld, p), J(f, e.utld, p), X(l, mw, p);
	let m = X(d, Zw);
	X(m, Zw, m), J(m, e.domain, d), J(p, e.domain, d), X(p, Jw, f), X(p, Zw, m);
	let h = X(o, Zw), g = X(o, Jw);
	X(h, Zw, h), J(h, e.domain, o), J(g, r, a), J(g, e.domain, o);
	let _ = RT(LT);
	J(g, e.tld, _), J(g, e.utld, _), J(_, e.domain, o), J(_, r, a), X(_, Jw, g), X(_, Zw, h), X(_, Vw, l);
	let ee = X(_, Gw), v = RT(LT);
	J(ee, e.numeric, v);
	let y = RT(LT), b = RT();
	J(y, t, y), J(y, n, b), J(b, t, y), J(b, n, b), X(_, oT, y), X(v, oT, y);
	let x = X(s, Gw), te = X(X(X(c, Gw), oT), oT);
	J(s, e.domain, o), X(s, Jw, g), X(s, Zw, h), J(c, e.domain, o), X(c, Jw, g), X(c, Zw, h), J(x, e.domain, y), X(x, oT, y), X(x, nT, y), J(te, e.domain, y), J(te, t, y), X(te, oT, y);
	let ne = [
		[Sw, Cw],
		[ww, Tw],
		[Ew, Dw],
		[Ow, kw],
		[Aw, jw],
		[Mw, Nw],
		[Pw, Fw],
		[Iw, Lw]
	];
	for (let e = 0; e < ne.length; e++) {
		let [r, i] = ne[e], a = X(y, r);
		X(b, r, a);
		let o = RT(LT);
		J(a, t, o);
		let s = RT();
		J(a, n, s), X(a, i, y), J(o, t, o), J(o, n, s), J(s, t, o), J(s, n, s), X(o, i, y), X(s, i, y);
	}
	return X(i, mw, _), X(i, xw, IT), {
		start: i,
		tokens: dT
	};
}
function BT(e, t, n) {
	let r = n.length, i = 0, a = [], o = [];
	for (; i < r;) {
		let s = e, c = null, l = null, u = 0, d = null, f = -1;
		for (; i < r && !(c = s.go(n[i].t));) o.push(n[i++]);
		for (; i < r && (l = c || s.go(n[i].t));) c = null, s = l, s.accepts() ? (f = 0, d = s) : f >= 0 && f++, i++, u++;
		if (f < 0) i -= u, i < r && (o.push(n[i]), i++);
		else {
			o.length > 0 && (a.push(VT(FT, t, o)), o = []), i -= f, u -= f;
			let e = d.t, r = n.slice(i - u, i);
			a.push(VT(e, t, r));
		}
	}
	return o.length > 0 && a.push(VT(FT, t, o)), a;
}
function VT(e, t, n) {
	let r = n[0].s, i = n[n.length - 1].e;
	return new e(t.slice(r, i), n);
}
var HT = typeof console < "u" && console && console.warn || (() => {}), UT = "until manual call of linkify.init(). Register all schemes and plugins before invoking linkify the first time.", Z = {
	scanner: null,
	parser: null,
	tokenQueue: [],
	pluginQueue: [],
	customSchemes: [],
	initialized: !1
};
function WT() {
	return cw.groups = {}, Z.scanner = null, Z.parser = null, Z.tokenQueue = [], Z.pluginQueue = [], Z.customSchemes = [], Z.initialized = !1, Z;
}
function GT(e, t = !1) {
	if (Z.initialized && HT(`linkifyjs: already initialized - will not register custom scheme "${e}" ${UT}`), !/^[0-9a-z]+(-[0-9a-z]+)*$/.test(e)) throw Error("linkifyjs: incorrect scheme format.\n1. Must only contain digits, lowercase ASCII letters or \"-\"\n2. Cannot start or end with \"-\"\n3. \"-\" cannot repeat");
	Z.customSchemes.push([e, t]);
}
function KT() {
	Z.scanner = wT(Z.customSchemes);
	for (let e = 0; e < Z.tokenQueue.length; e++) Z.tokenQueue[e][1]({ scanner: Z.scanner });
	Z.parser = zT(Z.scanner.tokens);
	for (let e = 0; e < Z.pluginQueue.length; e++) Z.pluginQueue[e][1]({
		scanner: Z.scanner,
		parser: Z.parser
	});
	return Z.initialized = !0, Z;
}
function qT(e) {
	return Z.initialized || KT(), BT(Z.parser.start, e, TT(Z.scanner.start, e));
}
qT.scan = TT;
function JT(e, t = null, n = null) {
	if (t && typeof t == "object") {
		if (n) throw Error(`linkifyjs: Invalid link type ${t}; must be a string`);
		n = t, t = null;
	}
	let r = new AT(n), i = qT(e), a = [];
	for (let e = 0; e < i.length; e++) {
		let n = i[e];
		n.isLink && (!t || n.t === t) && r.check(n) && a.push(n.toFormattedObject(r));
	}
	return a;
}
//#endregion
//#region node_modules/@tiptap/extension-link/dist/index.js
var YT = "[\0- \xA0 ᠎ -\u2029 　]", XT = new RegExp(YT), ZT = RegExp(`${YT}$`), QT = new RegExp(YT, "g");
function $T(e) {
	return e.length === 1 ? e[0].isLink : e.length === 3 && e[1].isLink ? ["()", "[]"].includes(e[0].value + e[2].value) : !1;
}
function eE(e) {
	return new E({
		key: new D("autolink"),
		appendTransaction: (t, n, r) => {
			let i = t.some((e) => e.docChanged) && !n.doc.eq(r.doc), a = t.some((e) => e.getMeta("preventAutolink"));
			if (!i || a) return;
			let { tr: o } = r;
			if (du(Il(n.doc, [...t])).forEach(({ newRange: t }) => {
				let n = Ll(r.doc, t, (e) => e.isTextblock), i, a;
				if (n.length > 1) i = n[0], a = r.doc.textBetween(i.pos, i.pos + i.node.nodeSize, void 0, " ");
				else if (n.length) {
					let e = r.doc.textBetween(t.from, t.to, " ", " ");
					if (!ZT.test(e)) return;
					i = n[0], a = r.doc.textBetween(i.pos, t.to, void 0, " ");
				}
				if (i && a) {
					let t = a.split(XT).filter(Boolean);
					if (t.length <= 0) return !1;
					let n = t[t.length - 1], s = i.pos + a.lastIndexOf(n);
					if (!n) return !1;
					let c = qT(n).map((t) => t.toObject(e.defaultProtocol));
					if (!$T(c)) return !1;
					c.filter((e) => e.isLink).map((e) => ({
						...e,
						from: s + e.start + 1,
						to: s + e.end + 1
					})).filter((e) => !r.schema.marks.code || !r.doc.rangeHasMark(e.from, e.to, r.schema.marks.code)).filter((t) => e.validate(t.value)).filter((t) => e.shouldAutoLink(t.value)).forEach((t) => {
						fu(t.from, t.to, r.doc).some((t) => t.mark.type === e.type) || o.addMark(t.from, t.to, e.type.create({ href: t.href }));
					});
				}
			}), o.steps.length) return o;
		}
	});
}
function tE(e) {
	return new E({
		key: new D("handleClickLink"),
		props: { handleClick: (t, n, r) => {
			if (r.button !== 0 || !t.editable) return !1;
			let i = null;
			if (r.target instanceof HTMLAnchorElement) i = r.target;
			else {
				let t = r.target;
				if (!t) return !1;
				let n = e.editor.view.dom;
				i = t.closest("a"), i && !n.contains(i) && (i = null);
			}
			if (!i) return !1;
			let a = !1;
			if (e.enableClickSelection && (a = e.editor.commands.extendMarkRange(e.type.name)), e.openOnClick) {
				let n = cu(t.state, e.type.name), r = i.href ?? n.href, o = i.target ?? n.target;
				r && (window.open(r, o), a = !0);
			}
			return a;
		} }
	});
}
var nE = /\[([^[\]]+)\]\(((?:[^\s()]|\([^\s()]*\))+)(?:\s+(?:(["'])(.*?)\3|“(.*?)”|‘(.*?)’))?\)$/, rE = /\[([^[\]]+)\]\(((?:[^\s()]|\([^\s()]*\))+)(?:\s+(?:(["'])(.*?)\3|“(.*?)”|‘(.*?)’))?\)/g;
function iE(e, t) {
	let n = 0;
	for (let r = t - 1; r >= 0 && e[r] === "\\"; --r) n += 1;
	return n % 2 == 1;
}
function aE(e, t) {
	let n = 0, r = 0;
	for (; r < t;) {
		if (e[r] !== "`") {
			r += 1;
			continue;
		}
		if (n === 0 && iE(e, r)) {
			r += 1;
			continue;
		}
		let i = 0;
		for (; r < t && e[r] === "`";) i += 1, r += 1;
		n === 0 ? n = i : i === n && (n = 0);
	}
	return n > 0;
}
function oE(e, t, n) {
	let [, r, i] = t;
	return (t.index ? e[t.index - 1] : void 0) === "!" || iE(e, t.index ?? 0) || aE(e, t.index ?? 0) ? !1 : !!r.trim() && n(i);
}
function sE(e) {
	let [t, n, r, , i, a, o] = e, s = i ?? a ?? o;
	return {
		index: e.index ?? 0,
		text: t,
		replaceWith: n,
		data: {
			href: r,
			title: s || null,
			markdown: !0
		}
	};
}
function cE(e, t) {
	return e.index < t.index + t.text.length && t.index < e.index + e.text.length;
}
function lE(e) {
	return {
		href: e.data?.href,
		title: e.data?.title ?? null
	};
}
function uE(e) {
	let t = gf({
		find: (t) => {
			let n = nE.exec(t);
			return !n || !oE(t, n, e.isAllowedHref) ? null : sE(n);
		},
		type: e.type,
		getAttributes: lE
	});
	return new Bd({
		find: t.find,
		handler: (e) => {
			let n = t.handler(e);
			return n !== null && e.state.tr.steps.length && e.state.tr.setMeta("preventAutolink", !0), n;
		}
	});
}
function dE(e) {
	let t = Cf({
		find: (t) => {
			let n = [];
			for (let r of t.matchAll(rE)) oE(t, r, e.isAllowedHref) && n.push(sE(r));
			let r = (e.findPlainUrls?.call(e, t) ?? []).filter((e) => !n.some((t) => cE(t, e)));
			return [...n, ...r];
		},
		type: e.type,
		getAttributes: lE
	});
	return new Kd({
		find: t.find,
		handler: (e) => {
			let n = t.handler(e);
			return n !== null && e.state.tr.steps.length && e.match.data?.markdown && e.state.tr.setMeta("preventAutolink", !0), n;
		}
	});
}
function fE(e) {
	return new E({
		key: new D("handlePasteLink"),
		props: { handlePaste: (t, n, r) => {
			let { shouldAutoLink: i } = e, { state: a } = t, { selection: o } = a, { empty: s } = o;
			if (s) return !1;
			let c = "";
			r.content.forEach((e) => {
				c += e.textContent;
			});
			let l = JT(c, { defaultProtocol: e.defaultProtocol }).find((e) => e.isLink && e.value === c);
			return !c || !l || i !== void 0 && !i(l.value) ? !1 : e.editor.commands.setMark(e.type, { href: l.href });
		} }
	});
}
function pE(e, t) {
	let n = [
		"http",
		"https",
		"ftp",
		"ftps",
		"mailto",
		"tel",
		"callto",
		"sms",
		"cid",
		"xmpp"
	];
	return t && t.forEach((e) => {
		let t = typeof e == "string" ? e : e.scheme;
		t && n.push(t);
	}), !e || e.replace(QT, "").match(RegExp(`^(?:(?:${n.map((e) => e.replace(/[-/\\^$*+?.()|[\]{}]/g, "\\$&")).join("|")}):|[^a-z]|[a-z0-9+.\\-]+(?:[^a-z+.\\-:]|$))`, "i"));
}
var mE = Gd.create({
	name: "link",
	priority: 1e3,
	keepOnSplit: !1,
	exitable: !0,
	onCreate() {
		this.options.validate && !this.options.shouldAutoLink && (this.options.shouldAutoLink = this.options.validate, console.warn("The `validate` option is deprecated. Rename to the `shouldAutoLink` option instead.")), this.options.protocols.forEach((e) => {
			if (typeof e == "string") {
				GT(e);
				return;
			}
			GT(e.scheme, e.optionalSlashes);
		});
	},
	onDestroy() {
		WT();
	},
	inclusive() {
		return this.options.autolink;
	},
	addOptions() {
		return {
			openOnClick: !0,
			enableClickSelection: !1,
			linkOnPaste: !0,
			markdownLinks: !1,
			autolink: !0,
			protocols: [],
			defaultProtocol: "http",
			HTMLAttributes: {
				target: "_blank",
				rel: "noopener noreferrer nofollow",
				class: null
			},
			isAllowedUri: (e, t) => !!pE(e, t.protocols),
			validate: (e) => !!e,
			shouldAutoLink: (e) => {
				let t = /^[a-z][a-z0-9+.-]*:\/\//i.test(e), n = /^[a-z][a-z0-9+.-]*:/i.test(e);
				if (t || n && !e.includes("@")) return !0;
				let r = (e.includes("@") ? e.split("@").pop() : e).split(/[/?#:]/)[0];
				return !(/^\d{1,3}(\.\d{1,3}){3}$/.test(r) || !/\./.test(r));
			}
		};
	},
	addAttributes() {
		return {
			href: {
				default: null,
				parseHTML(e) {
					return e.getAttribute("href");
				}
			},
			target: { default: this.options.HTMLAttributes.target ?? null },
			rel: { default: this.options.HTMLAttributes.rel ?? null },
			class: { default: this.options.HTMLAttributes.class ?? null },
			title: { default: null }
		};
	},
	parseHTML() {
		return [{
			tag: "a[href]",
			getAttrs: (e) => {
				let t = e.getAttribute("href");
				return !t || !this.options.isAllowedUri(t, {
					defaultValidate: (e) => !!pE(e, this.options.protocols),
					protocols: this.options.protocols,
					defaultProtocol: this.options.defaultProtocol
				}) ? !1 : null;
			}
		}];
	},
	renderHTML({ HTMLAttributes: e }) {
		return this.options.isAllowedUri(e.href, {
			defaultValidate: (e) => !!pE(e, this.options.protocols),
			protocols: this.options.protocols,
			defaultProtocol: this.options.defaultProtocol
		}) ? [
			"a",
			F(this.options.HTMLAttributes, e),
			0
		] : [
			"a",
			F(this.options.HTMLAttributes, {
				...e,
				href: ""
			}),
			0
		];
	},
	markdownTokenName: "link",
	parseMarkdown: (e, t) => t.applyMark("link", t.parseInline(e.tokens || []), {
		href: e.href,
		title: e.title || null
	}),
	renderMarkdown: (e, t) => {
		let n = e.attrs?.href ?? "", r = e.attrs?.title ?? "", i = t.renderChildren(e);
		return r ? `[${i}](${n} "${r}")` : `[${i}](${n})`;
	},
	addCommands() {
		return {
			setLink: (e) => ({ chain: t }) => {
				let { href: n } = e;
				return this.options.isAllowedUri(n, {
					defaultValidate: (e) => !!pE(e, this.options.protocols),
					protocols: this.options.protocols,
					defaultProtocol: this.options.defaultProtocol
				}) ? t().setMark(this.name, e).setMeta("preventAutolink", !0).run() : !1;
			},
			toggleLink: (e) => ({ chain: t }) => {
				let { href: n } = e || {};
				return n && !this.options.isAllowedUri(n, {
					defaultValidate: (e) => !!pE(e, this.options.protocols),
					protocols: this.options.protocols,
					defaultProtocol: this.options.defaultProtocol
				}) ? !1 : t().toggleMark(this.name, e, { extendEmptyMarkRange: !0 }).setMeta("preventAutolink", !0).run();
			},
			unsetLink: () => ({ chain: e }) => e().unsetMark(this.name, { extendEmptyMarkRange: !0 }).setMeta("preventAutolink", !0).run()
		};
	},
	addInputRules() {
		return this.options.markdownLinks ? [uE({
			type: this.type,
			isAllowedHref: (e) => this.options.isAllowedUri(e, {
				defaultValidate: (e) => !!pE(e, this.options.protocols),
				protocols: this.options.protocols,
				defaultProtocol: this.options.defaultProtocol
			})
		})] : [];
	},
	addPasteRules() {
		let e = (e) => {
			let t = [];
			if (e) {
				let { protocols: n, defaultProtocol: r } = this.options;
				JT(e).filter((e) => e.isLink && this.options.isAllowedUri(e.value, {
					defaultValidate: (e) => !!pE(e, n),
					protocols: n,
					defaultProtocol: r
				})).forEach((e) => {
					this.options.shouldAutoLink(e.value) && t.push({
						text: e.value,
						data: { href: e.href },
						index: e.start
					});
				});
			}
			return t;
		};
		return this.options.markdownLinks ? [dE({
			type: this.type,
			isAllowedHref: (e) => this.options.isAllowedUri(e, {
				defaultValidate: (e) => !!pE(e, this.options.protocols),
				protocols: this.options.protocols,
				defaultProtocol: this.options.defaultProtocol
			}),
			findPlainUrls: e
		})] : [Cf({
			find: e,
			type: this.type,
			getAttributes: (e) => ({ href: e.data?.href })
		})];
	},
	addProseMirrorPlugins() {
		let e = [], { protocols: t, defaultProtocol: n } = this.options;
		return this.options.autolink && e.push(eE({
			type: this.type,
			defaultProtocol: this.options.defaultProtocol,
			validate: (e) => this.options.isAllowedUri(e, {
				defaultValidate: (e) => !!pE(e, t),
				protocols: t,
				defaultProtocol: n
			}),
			shouldAutoLink: this.options.shouldAutoLink
		})), e.push(tE({
			type: this.type,
			editor: this.editor,
			openOnClick: this.options.openOnClick === "whenNotEditable" || this.options.openOnClick,
			enableClickSelection: this.options.enableClickSelection
		})), this.options.linkOnPaste && e.push(fE({
			editor: this.editor,
			defaultProtocol: this.options.defaultProtocol,
			type: this.type,
			shouldAutoLink: this.options.shouldAutoLink
		})), e;
	}
}), hE = mE, gE = "listItem", _E = "textStyle", vE = /^\s*([-+*])\s$/, yE = Sf.create({
	name: "bulletList",
	addOptions() {
		return {
			itemTypeName: "listItem",
			HTMLAttributes: {},
			keepMarks: !1,
			keepAttributes: !1
		};
	},
	group: "block list",
	content() {
		return `${this.options.itemTypeName}+`;
	},
	parseHTML() {
		return [{ tag: "ul" }];
	},
	renderHTML({ HTMLAttributes: e }) {
		return [
			"ul",
			F(this.options.HTMLAttributes, e),
			0
		];
	},
	markdownTokenName: "list",
	parseMarkdown: (e, t) => e.type !== "list" || e.ordered ? [] : {
		type: "bulletList",
		content: e.items ? t.parseChildren(e.items) : []
	},
	renderMarkdown: (e, t) => e.content ? t.renderChildren(e.content, "\n") : "",
	markdownOptions: { indentsContent: !0 },
	addCommands() {
		return { toggleBulletList: () => ({ commands: e, chain: t }) => this.options.keepAttributes ? t().toggleList(this.name, this.options.itemTypeName, this.options.keepMarks).updateAttributes(gE, this.editor.getAttributes(_E)).run() : e.toggleList(this.name, this.options.itemTypeName, this.options.keepMarks) };
	},
	addKeyboardShortcuts() {
		return { "Mod-Shift-8": () => this.editor.commands.toggleBulletList() };
	},
	addInputRules() {
		let e = yf({
			find: vE,
			type: this.type
		});
		return (this.options.keepMarks || this.options.keepAttributes) && (e = yf({
			find: vE,
			type: this.type,
			keepMarks: this.options.keepMarks,
			keepAttributes: this.options.keepAttributes,
			getAttributes: () => this.editor.getAttributes(_E),
			editor: this.editor
		})), [e];
	}
}), bE = (e, t, n) => {
	let { selection: r } = e;
	if (!r.empty) return null;
	let { $from: i } = r;
	if (!i.parent.isTextblock || i.parentOffset !== i.parent.content.size) return null;
	let a = -1;
	for (let e = i.depth; e > 0; --e) if (i.node(e).type.name === t) {
		a = e;
		break;
	}
	if (a < 0) return null;
	let o = i.node(a), s = i.index(a);
	if (s + 1 >= o.childCount) return null;
	let c = o.child(s + 1);
	if (!n.includes(c.type.name)) return null;
	let l = e.schema.nodes[t], u = !1;
	if (c.forEach((e) => {
		e.type === l && e.childCount > 1 && (u = !0);
	}), !u) return null;
	let d = e.doc.resolve(i.after()).nodeAfter;
	if (!d || !n.includes(d.type.name)) return null;
	let f = [];
	return d.forEach((e) => {
		f.push(e);
	}), f.length === 0 ? null : {
		listItemDepth: a,
		nestedList: d,
		nestedListPos: i.after(),
		insertPos: i.after(a),
		items: f
	};
}, xE = (e, t, n, r) => {
	let i = bE(e, n, r);
	if (!i) return !1;
	let { selection: a } = e, { nestedList: o, nestedListPos: s, insertPos: c, items: u } = i, d = e.tr;
	d.delete(s, s + o.nodeSize);
	let f = d.mapping.map(c);
	return d.insert(f, l.from(u)), d.setSelection(a.map(d.doc, d.mapping)), t && t(d), !0;
}, SE = (e, t, n) => xE(e.state, e.view.dispatch, t, n), CE = (e, t) => I.create({
	name: `${e}BranchingDeleteKeymap`,
	priority: 101,
	addKeyboardShortcuts() {
		let n = () => SE(this.editor, e, t);
		return {
			Delete: n,
			"Mod-Delete": n
		};
	}
}), wE = [
	[1e3, "m"],
	[900, "cm"],
	[500, "d"],
	[400, "cd"],
	[100, "c"],
	[90, "xc"],
	[50, "l"],
	[40, "xl"],
	[10, "x"],
	[9, "ix"],
	[5, "v"],
	[4, "iv"],
	[1, "i"]
], TE = "abcdefghijklmnopqrstuvwxyz", EE = String.raw`\d+|[ivxlcdmIVXLCDM]+|${"[a-zA-Z]{1,2}"}`;
function DE(e) {
	let t = e, n = "";
	for (let [e, r] of wE) for (; t >= e;) n += r, t -= e;
	return n;
}
function OE(e) {
	return DE(e).toUpperCase();
}
function kE(e) {
	let t = e.toLowerCase(), n = 0, r = 0;
	for (; n < t.length;) {
		let e = !1;
		for (let [i, a] of wE) if (t.startsWith(a, n)) {
			r += i, n += a.length, e = !0;
			break;
		}
		if (!e) return 0;
	}
	return r;
}
function AE(e) {
	if (!/^[ivxlcdmIVXLCDM]+$/.test(e)) return !1;
	let t = kE(e);
	return t <= 0 ? !1 : (e === e.toLowerCase() ? DE(t) : OE(t)) === e;
}
function jE(e) {
	let t = e.toLowerCase();
	if (t.length === 1) return t.charCodeAt(0) - 97 + 1;
	if (t.length === 2) {
		let e = t.charCodeAt(0) - 97, n = t.charCodeAt(1) - 97;
		return (e + 1) * 26 + n + 1;
	}
	return 0;
}
function ME(e) {
	if (e <= 26) return TE[e - 1];
	let t = Math.floor((e - 1) / 26) - 1, n = (e - 1) % 26;
	return t < 0 ? TE[n] : TE[t] + TE[n];
}
function NE(e) {
	if (e && !/^\d+$/.test(e)) {
		if (AE(e)) return e === e.toLowerCase() ? "i" : "I";
		if (/^[a-z]{1,2}$/.test(e)) return "a";
		if (/^[A-Z]{1,2}$/.test(e)) return "A";
	}
}
function PE(e) {
	if (/^\d+$/.test(e)) return parseInt(e, 10);
	let t = NE(e);
	if (t === "i" || t === "I") return kE(e);
	if (t === "a" || t === "A") {
		let t = jE(e);
		return t > 0 ? t : 1;
	}
	let n = parseInt(e, 10);
	return Number.isNaN(n) ? 1 : n;
}
function FE(e, t) {
	if (e === "numeric") return String(t);
	switch (e) {
		case "a": return ME(t);
		case "A": return ME(t).toUpperCase();
		case "i": return DE(t);
		case "I": return OE(t);
		default: return String(t);
	}
}
function IE(e) {
	if (e.length === 0) return !1;
	let t = NE(e[0]) ?? "numeric", n = PE(e[0]);
	if (n < 1) return !1;
	for (let r = 0; r < e.length; r++) {
		let i = FE(t, n + r);
		if (e[r] !== i) return !1;
	}
	return !0;
}
function LE(e) {
	return {
		type: NE(e),
		start: PE(e)
	};
}
function RE(e) {
	let { type: t, start: n } = LE(e), r = {};
	return t && (r.type = t), n !== 1 && (r.start = n), r;
}
function zE(e, t, n = ". ") {
	let r = t + 1;
	if (!e || e === "1") return `${r}${n}`;
	switch (e) {
		case "a": return `${ME(r)}${n}`;
		case "A": return `${ME(r).toUpperCase()}${n}`;
		case "i": return `${DE(r)}${n}`;
		case "I": return `${OE(r)}${n}`;
		default: return `${r}${n}`;
	}
}
function BE(e) {
	let t = e.tokens?.[0];
	return !!(e.text && e.tokens?.length === 1 && t?.type === "list" && t.ordered && t.raw === e.text);
}
function VE(e, t) {
	return t.tokenizeInline ? t.parseInline(t.tokenizeInline(e)) : t.parseInline([{
		type: "text",
		raw: e,
		text: e
	}]);
}
var HE = Sf.create({
	name: "listItem",
	addOptions() {
		return {
			HTMLAttributes: {},
			bulletListTypeName: "bulletList",
			orderedListTypeName: "orderedList"
		};
	},
	content: "paragraph block*",
	defining: !0,
	parseHTML() {
		return [{ tag: "li" }];
	},
	renderHTML({ HTMLAttributes: e }) {
		return [
			"li",
			F(this.options.HTMLAttributes, e),
			0
		];
	},
	markdownTokenName: "list_item",
	parseMarkdown: (e, t) => {
		if (e.type !== "list_item") return [];
		let n = t.parseBlockChildren ?? t.parseChildren, r = [];
		if (e.tokens && e.tokens.length > 0) {
			if (BE(e)) return {
				type: "listItem",
				content: [{
					type: "paragraph",
					content: VE(e.text || "", t)
				}]
			};
			if (e.tokens.some((e) => e.type === "paragraph")) r = n(e.tokens);
			else {
				let i = e.tokens[0];
				if (i && i.type === "text" && i.tokens && i.tokens.length > 0) {
					if (r = [{
						type: "paragraph",
						content: t.parseInline(i.tokens)
					}], e.tokens.length > 1) {
						let t = n(e.tokens.slice(1));
						r.push(...t);
					}
				} else r = n(e.tokens);
			}
		}
		return r.length === 0 && (r = [{
			type: "paragraph",
			content: []
		}]), {
			type: "listItem",
			content: r
		};
	},
	renderMarkdown: (e, t, n) => Ld(e, t, (e) => {
		if (e.parentType === "bulletList") return "- ";
		if (e.parentType === "orderedList") {
			var t, n;
			let r = ((t = e.meta) == null || (t = t.parentAttrs) == null ? void 0 : t.start) || 1;
			return zE((n = e.meta) == null || (n = n.parentAttrs) == null ? void 0 : n.type, r - 1 + (e.index || 0), ". ");
		}
		return "- ";
	}, n, { alignNestedToPrefix: n?.parentType === "orderedList" }),
	addExtensions() {
		return [CE(this.name, [this.options.bulletListTypeName, this.options.orderedListTypeName])];
	},
	addKeyboardShortcuts() {
		return {
			Enter: () => this.editor.commands.splitListItem(this.name),
			Tab: () => this.editor.commands.sinkListItem(this.name),
			"Shift-Tab": () => this.editor.commands.liftListItem(this.name)
		};
	}
}), UE = (e, t) => {
	let { $from: n } = t.selection, r = M(e, t.schema), i = null, a = n.depth, o = n.pos, s = null;
	for (; a > 0 && s === null;) i = n.node(a), i.type === r ? s = a : (--a, --o);
	return s === null ? null : {
		$pos: t.doc.resolve(o),
		depth: s
	};
}, WE = (e, t) => {
	let n = UE(e, t);
	if (!n) return !1;
	let [, r] = pu(t, e, n.$pos.pos + 4);
	return r;
}, GE = (e, t, n) => {
	let { $anchor: r } = e.selection, i = Math.max(0, r.pos - 2), a = e.doc.resolve(i).node();
	return !(!a || !n.includes(a.type.name));
}, KE = (e, t, n) => {
	if (e.commands.undoInputRule()) return !0;
	if (e.state.selection.from !== e.state.selection.to) return !1;
	if (!vl(e.state, t) && GE(e.state, t, n)) {
		let { $anchor: n } = e.state.selection, r = e.state.doc.resolve(n.before() - 1), i = [];
		r.node().descendants((e, n) => {
			e.type.name === t && i.push({
				node: e,
				pos: n
			});
		});
		let a = i.at(-1);
		if (!a) return !1;
		let o = e.state.doc.resolve(r.start() + a.pos + 1);
		return e.chain().cut({
			from: n.start() - 1,
			to: n.end() + 1
		}, o.end()).joinForward().run();
	}
	if (!vl(e.state, t) || !xu(e.state)) return !1;
	let { $from: r } = e.state.selection, i = r.depth - 1;
	return r.node(i).type !== e.schema.nodes[t] || r.index(i) !== 0 ? !1 : e.chain().liftListItem(t).run();
}, qE = (e, t) => {
	let n = WE(e, t), r = UE(e, t);
	return !r || !n ? !1 : n > r.depth;
}, JE = (e, t) => {
	let n = WE(e, t), r = UE(e, t);
	return !r || !n ? !1 : n < r.depth;
}, YE = (e, t) => {
	if (!vl(e.state, t) || !bu(e.state, t)) return !1;
	let { selection: n } = e.state, { $from: r, $to: i } = n;
	return !n.empty && r.sameParent(i) ? !1 : qE(t, e.state) ? e.chain().focus(e.state.selection.from + 4).lift(t).joinBackward().run() : JE(t, e.state) ? e.chain().joinForward().joinBackward().run() : e.commands.joinItemForward();
}, XE = (e, t, n) => {
	let { state: r } = e, { selection: i } = r;
	if (!i.empty) return !1;
	let { $from: a } = i;
	if (a.parentOffset !== 0 || !a.parent.isTextblock || vl(r, t)) return !1;
	let o = mu(a);
	if (!o || !n.includes(o.type.name)) return !1;
	let s = o.lastChild;
	if (!s || s.type.name !== t) return !1;
	let c = a.parent;
	if (!s.canReplace(s.childCount, s.childCount, l.from(c))) return !1;
	let u = a.before(), d = a.after(), f = u - 2;
	return e.commands.command(({ tr: e, dispatch: t }) => (t && (e.delete(u, d).insert(f, l.from(c)), e.setSelection(w.create(e.doc, f + 1)), e.scrollIntoView()), !0));
}, ZE = I.create({
	name: "listKeymap",
	addOptions() {
		return { listTypes: [{
			itemName: "listItem",
			wrapperNames: ["bulletList", "orderedList"]
		}, {
			itemName: "taskItem",
			wrapperNames: ["taskList"]
		}] };
	},
	addKeyboardShortcuts() {
		return {
			Delete: ({ editor: e }) => {
				let t = !1;
				return this.options.listTypes.forEach(({ itemName: n }) => {
					e.state.schema.nodes[n] !== void 0 && YE(e, n) && (t = !0);
				}), t;
			},
			"Mod-Delete": ({ editor: e }) => {
				let t = !1;
				return this.options.listTypes.forEach(({ itemName: n }) => {
					e.state.schema.nodes[n] !== void 0 && YE(e, n) && (t = !0);
				}), t;
			},
			Backspace: ({ editor: e }) => {
				let t = !1;
				return this.options.listTypes.forEach(({ itemName: n, wrapperNames: r }) => {
					e.state.schema.nodes[n] !== void 0 && KE(e, n, r) && (t = !0);
				}), t;
			},
			"Mod-Backspace": ({ editor: e }) => {
				let t = !1;
				return this.options.listTypes.forEach(({ itemName: n, wrapperNames: r }) => {
					e.state.schema.nodes[n] !== void 0 && KE(e, n, r) && (t = !0);
				}), t;
			},
			Tab: ({ editor: e }) => {
				for (let { itemName: t, wrapperNames: n } of this.options.listTypes) if (e.state.schema.nodes[t] !== void 0 && XE(e, t, n)) return !0;
				return !1;
			}
		};
	}
}), QE = RegExp(`^(\\s*)(${EE})([.)])\\s+(.*)$`), $E = /^\s/, eD = {
	heading: /^#{1,6}(?:\s|$)/,
	bulletItem: /^[-+*]\s+/,
	codeFence: /^(?:```|~~~)/,
	blockMath: /^\$\$/,
	thematicBreak: /^(?:(?:-[ \t]*){3,}|(?:_[ \t]*){3,}|(?:\*[ \t]*){3,})$/
};
function tD(e) {
	return QE.test(e.trimStart());
}
function nD(e) {
	let t = e.trimStart();
	return eD.bulletItem.test(t) || tD(t) || eD.heading.test(t) || eD.thematicBreak.test(t) && !t.startsWith("-") || /^>\s?/.test(t) || eD.codeFence.test(t) || eD.blockMath.test(t);
}
function rD(e) {
	return Object.values(eD).some((t) => t.test(e));
}
function iD(e) {
	let t = [], n = [], r = !1;
	return e.forEach((e) => {
		if (r) {
			n.push(e);
			return;
		}
		if (e.trim() === "") {
			r = !0, n.push(e);
			return;
		}
		if (t.length > 0 && nD(e)) {
			r = !0, n.push(e);
			return;
		}
		t.push(e);
	}), {
		paragraphLines: t,
		blockLines: n
	};
}
function aD(e) {
	let t = [], n = 0, r = 0;
	for (; n < e.length;) {
		let i = e[n], a = i.match(QE);
		if (!a) break;
		let [, o, s, c, l] = a, u = o.length, d = parseInt(s, 10), f = isNaN(d) ? NE(s) : void 0, p = isNaN(d) ? PE(s) : d, m = [l], h = n + 1, g = [i], _ = !1;
		for (; h < e.length;) {
			let t = e[h];
			if (t.match(QE)) break;
			if (t.trim() === "") g.push(t), m.push(""), _ = !0, h += 1;
			else if (t.match($E)) {
				let e = t.length - t.trimStart().length, n = u + s.length + 1;
				g.push(t), m.push(t.slice(Math.min(e, n))), h += 1;
			} else {
				if (_ || rD(t)) break;
				g.push(t), m.push(t), h += 1;
			}
		}
		t.push({
			indent: u,
			number: p,
			type: f,
			content: m.join("\n").trim(),
			contentLines: m,
			raw: g.join("\n")
		}), r = h, n = h;
	}
	return [t, r];
}
var oD = RegExp(`^(${EE})([.)])\\s+(.+)$`);
function sD(e) {
	let t = e.split("\n").filter((e) => e.trim().length > 0);
	if (t.length === 0) return null;
	let n = [];
	for (let e of t) {
		let t = e.trim().match(oD);
		if (!t) return null;
		n.push({
			marker: t[1],
			content: t[3]
		});
	}
	return IE(n.map((e) => e.marker)) ? {
		type: "orderedList",
		attrs: RE(n[0].marker),
		content: n.map((e) => ({
			type: "listItem",
			content: [{
				type: "paragraph",
				content: [{
					type: "text",
					text: e.content
				}]
			}]
		}))
	} : null;
}
function cD(e, t, n) {
	let r = [], i = 0;
	for (; i < e.length;) {
		let a = e[i];
		if (a.indent === t) {
			let { paragraphLines: o, blockLines: s } = iD(a.contentLines), c = o.join("\n").trim(), l = [];
			c && l.push({
				type: "paragraph",
				raw: c,
				tokens: n.inlineTokens(c)
			});
			let u = s.join("\n").trim();
			if (u) {
				let e = n.blockTokens(u);
				l.push(...e);
			}
			let d = i + 1, f = [];
			for (; d < e.length && e[d].indent > t;) f.push(e[d]), d += 1;
			if (f.length > 0) {
				let e = cD(f, Math.min(...f.map((e) => e.indent)), n);
				l.push({
					type: "list",
					ordered: !0,
					start: f[0].number,
					typeMarker: f[0].type,
					items: e,
					raw: f.map((e) => e.raw).join("\n")
				});
			}
			r.push({
				type: "list_item",
				raw: a.raw,
				tokens: l
			}), i = d;
		} else i += 1;
	}
	return r;
}
function lD(e, t) {
	return e.map((e) => {
		if (e.type !== "list_item") return t.parseChildren([e])[0];
		let n = [];
		return e.tokens && e.tokens.length > 0 && e.tokens.forEach((e) => {
			if (e.type === "paragraph" || e.type === "list" || e.type === "blockquote" || e.type === "code") n.push(...t.parseChildren([e]));
			else if (e.type === "text" && e.tokens) {
				let r = t.parseChildren([e]);
				n.push({
					type: "paragraph",
					content: r
				});
			} else {
				let r = t.parseChildren([e]);
				r.length > 0 && n.push(...r);
			}
		}), {
			type: "listItem",
			content: n
		};
	});
}
var uD = "listItem", dD = "textStyle", fD = /^(\d+)\.\s$/;
function pD(e) {
	let t = e.match(/list-style-type\s*:\s*([^;]+)/i);
	if (!t) return null;
	switch (t[1].trim().toLowerCase()) {
		case "upper-roman": return "I";
		case "lower-roman": return "i";
		case "upper-alpha":
		case "upper-latin": return "A";
		case "lower-alpha":
		case "lower-latin": return "a";
		default: return null;
	}
}
var mD = Sf.create({
	name: "orderedList",
	addOptions() {
		return {
			itemTypeName: "listItem",
			HTMLAttributes: {},
			keepMarks: !1,
			keepAttributes: !1
		};
	},
	group: "block list",
	content() {
		return `${this.options.itemTypeName}+`;
	},
	addAttributes() {
		return {
			start: {
				default: 1,
				parseHTML: (e) => e.hasAttribute("start") ? parseInt(e.getAttribute("start") || "", 10) : 1
			},
			type: {
				default: null,
				parseHTML: (e) => {
					let t = e.getAttribute("type");
					if (t) return t;
					let n = e.getAttribute("style");
					if (n) {
						let e = pD(n);
						if (e) return e;
					}
					let r = e.querySelector("li");
					if (r) {
						let e = r.getAttribute("style");
						if (e) {
							let t = pD(e);
							if (t) return t;
						}
					}
					return null;
				}
			}
		};
	},
	parseHTML() {
		return [{ tag: "ol" }];
	},
	renderHTML({ HTMLAttributes: e }) {
		let { start: t, type: n, ...r } = e, i = F(this.options.HTMLAttributes, r);
		return t !== 1 && (i.start = t), n && n !== "1" && (i.type = n), [
			"ol",
			i,
			0
		];
	},
	markdownTokenName: "list",
	parseMarkdown: (e, t) => {
		if (e.type !== "list" || !e.ordered) return [];
		let n = e.start || 1, r = e.typeMarker, i = e.items ? lD(e.items, t) : [], a = {};
		return n !== 1 && (a.start = n), r && (a.type = r), Object.keys(a).length > 0 ? {
			type: "orderedList",
			attrs: a,
			content: i
		} : {
			type: "orderedList",
			content: i
		};
	},
	renderMarkdown: (e, t) => e.content ? t.renderChildren(e.content, "\n") : "",
	markdownTokenizer: {
		name: "orderedList",
		level: "block",
		start: () => -1,
		tokenize: (e, t, n) => {
			let r = e.split("\n"), [i, a] = aD(r);
			if (i.length === 0) return;
			let o = cD(i, i[0].indent, n);
			if (o.length !== 0) return {
				type: "list",
				ordered: !0,
				start: i[0]?.number || 1,
				typeMarker: i[0]?.type,
				items: o,
				raw: r.slice(0, a).join("\n")
			};
		}
	},
	markdownOptions: { indentsContent: !0 },
	addCommands() {
		return { toggleOrderedList: () => ({ commands: e, chain: t }) => this.options.keepAttributes ? t().toggleList(this.name, this.options.itemTypeName, this.options.keepMarks).updateAttributes(uD, this.editor.getAttributes(dD)).run() : e.toggleList(this.name, this.options.itemTypeName, this.options.keepMarks) };
	},
	addKeyboardShortcuts() {
		return { "Mod-Shift-7": () => this.editor.commands.toggleOrderedList() };
	},
	addProseMirrorPlugins() {
		return [new E({ props: { handlePaste: (e, t) => {
			if ((t.clipboardData?.getData("text/html"))?.trim()) return !1;
			let n = t.clipboardData?.getData("text/plain");
			if (!n) return !1;
			let r = sD(n);
			if (!r) return !1;
			try {
				let t = e.state.schema.nodeFromJSON(r), n = e.state.tr.replaceSelectionWith(t);
				return e.dispatch(n), !0;
			} catch {
				return !1;
			}
		} } })];
	},
	addInputRules() {
		let e = (e, t) => (!t.attrs.type || t.attrs.type === "1") && t.childCount + t.attrs.start === +e[1], t = yf({
			find: fD,
			type: this.type,
			getAttributes: (e) => ({ start: +e[1] }),
			joinPredicate: e
		});
		return (this.options.keepMarks || this.options.keepAttributes) && (t = yf({
			find: fD,
			type: this.type,
			keepMarks: this.options.keepMarks,
			keepAttributes: this.options.keepAttributes,
			getAttributes: (e) => ({
				start: +e[1],
				...this.editor.getAttributes(dD)
			}),
			joinPredicate: e,
			editor: this.editor
		})), [t];
	}
}), hD = /^\s*(\[([( |x])?\])\s$/, gD = "position:absolute;width:1px;height:1px;padding:0;margin:-1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;border:0", _D = (e, t, n) => {
	var r;
	return (n == null || (r = n.checkboxLabel) == null ? void 0 : r.call(n, e, t)) || `Task item checkbox for ${e.textContent || "empty task item"}`;
}, vD = Sf.create({
	name: "taskItem",
	addOptions() {
		return {
			nested: !1,
			HTMLAttributes: {},
			taskListTypeName: "taskList",
			a11y: void 0
		};
	},
	content() {
		return this.options.nested ? "paragraph block*" : "paragraph+";
	},
	defining: !0,
	addAttributes() {
		return { checked: {
			default: !1,
			keepOnSplit: !1,
			parseHTML: (e) => {
				let t = e.getAttribute("data-checked");
				return t === "" || t === "true";
			},
			renderHTML: (e) => ({ "data-checked": e.checked })
		} };
	},
	parseHTML() {
		return [{
			tag: `li[data-type="${this.name}"]`,
			priority: 51,
			contentElement: (e) => e.querySelector("div") ?? e
		}];
	},
	renderHTML({ node: e, HTMLAttributes: t }) {
		return [
			"li",
			F(this.options.HTMLAttributes, t, { "data-type": this.name }),
			[
				"label",
				["input", {
					type: "checkbox",
					checked: e.attrs.checked ? "checked" : null
				}],
				["span"]
			],
			["div", 0]
		];
	},
	parseMarkdown: (e, t) => {
		let n = [];
		if (e.tokens && e.tokens.length > 0 ? n.push(t.createNode("paragraph", {}, t.parseInline(e.tokens))) : e.text ? n.push(t.createNode("paragraph", {}, [t.createNode("text", { text: e.text })])) : n.push(t.createNode("paragraph", {}, [])), e.nestedTokens && e.nestedTokens.length > 0) {
			let r = t.parseChildren(e.nestedTokens);
			n.push(...r);
		}
		return t.createNode("taskItem", { checked: e.checked || !1 }, n);
	},
	renderMarkdown: (e, t) => Ld(e, t, `- [${e.attrs?.checked ? "x" : " "}] `),
	addExtensions() {
		return this.options.nested ? [CE(this.name, [this.options.taskListTypeName])] : [];
	},
	addKeyboardShortcuts() {
		let e = {
			Enter: () => this.editor.commands.splitListItem(this.name),
			"Shift-Tab": () => this.editor.commands.liftListItem(this.name)
		};
		return this.options.nested ? {
			...e,
			Tab: () => this.editor.commands.sinkListItem(this.name)
		} : e;
	},
	addNodeView() {
		return ({ node: e, HTMLAttributes: t, getPos: n, editor: r }) => {
			let i = document.createElement("li"), a = document.createElement("label"), o = document.createElement("span"), s = document.createElement("input"), c = document.createElement("div");
			o.style.cssText = gD;
			let l = (e) => {
				let t = _D(e, e.attrs.checked, this.options.a11y);
				s.setAttribute("aria-label", t), o.textContent = t;
			};
			l(e), a.contentEditable = "false", s.type = "checkbox", s.addEventListener("mousedown", (e) => e.preventDefault()), s.addEventListener("change", (t) => {
				if (!r.isEditable && !this.options.onReadOnlyChecked) {
					s.checked = !s.checked;
					return;
				}
				let { checked: i } = t.target;
				r.isEditable && typeof n == "function" && r.chain().focus(void 0, { scrollIntoView: !1 }).command(({ tr: e }) => {
					let t = n();
					if (typeof t != "number") return !1;
					let r = e.doc.nodeAt(t);
					return e.setNodeMarkup(t, void 0, {
						...r?.attrs,
						checked: i
					}), !0;
				}).run(), !r.isEditable && this.options.onReadOnlyChecked && (this.options.onReadOnlyChecked(e, i) || (s.checked = !s.checked));
			}), Object.entries(this.options.HTMLAttributes).forEach(([e, t]) => {
				i.setAttribute(e, t);
			}), i.dataset.checked = e.attrs.checked, s.checked = e.attrs.checked, a.append(s, o), i.append(a, c), Object.entries(t).forEach(([e, t]) => {
				i.setAttribute(e, t);
			});
			let u = new Set(Object.keys(t));
			return {
				dom: i,
				contentDOM: c,
				update: (e) => {
					if (e.type !== this.type) return !1;
					i.dataset.checked = e.attrs.checked, s.checked = e.attrs.checked, l(e);
					let t = r.extensionManager.attributes, n = Jl(e, t), a = new Set(Object.keys(n)), o = this.options.HTMLAttributes;
					return u.forEach((e) => {
						a.has(e) || (e in o ? i.setAttribute(e, o[e]) : i.removeAttribute(e));
					}), Object.entries(n).forEach(([e, t]) => {
						t == null ? e in o ? i.setAttribute(e, o[e]) : i.removeAttribute(e) : i.setAttribute(e, t);
					}), u = a, !0;
				}
			};
		};
	},
	addInputRules() {
		return [yf({
			find: hD,
			type: this.type,
			getAttributes: (e) => ({ checked: e[e.length - 1] === "x" })
		})];
	}
}), yD = Sf.create({
	name: "taskList",
	addOptions() {
		return {
			itemTypeName: "taskItem",
			HTMLAttributes: {}
		};
	},
	group: "block list",
	content() {
		return `${this.options.itemTypeName}+`;
	},
	parseHTML() {
		return [{
			tag: `ul[data-type="${this.name}"]`,
			priority: 51
		}];
	},
	renderHTML({ HTMLAttributes: e }) {
		return [
			"ul",
			F(this.options.HTMLAttributes, e, { "data-type": this.name }),
			0
		];
	},
	parseMarkdown: (e, t) => t.createNode("taskList", {}, t.parseChildren(e.items || [])),
	renderMarkdown: (e, t) => e.content ? t.renderChildren(e.content, "\n") : "",
	markdownTokenizer: {
		name: "taskList",
		level: "block",
		start(e) {
			let t = e.match(/^\s*[-+*]\s+\[([ xX])\]\s+/)?.index;
			return t === void 0 ? -1 : t;
		},
		tokenize(e, t, n) {
			let r = (e) => {
				let t = Pd(e, {
					itemPattern: /^(\s*)([-+*])\s+\[([ xX])\]\s+(.*)$/,
					extractItemData: (e) => ({
						indentLevel: e[1].length,
						mainContent: e[4],
						checked: e[3].toLowerCase() === "x"
					}),
					createToken: (e, t) => ({
						type: "taskItem",
						raw: "",
						mainContent: e.mainContent,
						indentLevel: e.indentLevel,
						checked: e.checked,
						text: e.mainContent,
						tokens: n.inlineTokens(e.mainContent),
						nestedTokens: t
					}),
					customNestedParser: r
				}, n);
				if (t) {
					let r = {
						type: "taskList",
						raw: t.raw,
						items: t.items
					}, i = e.slice(t.raw.length);
					return i.trim() ? [r, ...n.blockTokens(i)] : [r];
				}
				return n.blockTokens(e);
			}, i = Pd(e, {
				itemPattern: /^(\s*)([-+*])\s+\[([ xX])\]\s+(.*)$/,
				extractItemData: (e) => ({
					indentLevel: e[1].length,
					mainContent: e[4],
					checked: e[3].toLowerCase() === "x"
				}),
				createToken: (e, t) => ({
					type: "taskItem",
					raw: "",
					mainContent: e.mainContent,
					indentLevel: e.indentLevel,
					checked: e.checked,
					text: e.mainContent,
					tokens: n.inlineTokens(e.mainContent),
					nestedTokens: t
				}),
				customNestedParser: r
			}, n);
			if (i) return {
				type: "taskList",
				raw: i.raw,
				items: i.items
			};
		}
	},
	markdownOptions: { indentsContent: !0 },
	addCommands() {
		return { toggleTaskList: () => ({ commands: e }) => e.toggleList(this.name, this.options.itemTypeName) };
	},
	addKeyboardShortcuts() {
		return { "Mod-Shift-9": () => this.editor.commands.toggleTaskList() };
	}
});
I.create({
	name: "listKit",
	addExtensions() {
		let e = [];
		return this.options.bulletList !== !1 && e.push(yE.configure(this.options.bulletList)), this.options.listItem !== !1 && e.push(HE.configure(this.options.listItem)), this.options.listKeymap !== !1 && e.push(ZE.configure(this.options.listKeymap)), this.options.orderedList !== !1 && e.push(mD.configure(this.options.orderedList)), this.options.taskItem !== !1 && e.push(vD.configure(this.options.taskItem)), this.options.taskList !== !1 && e.push(yD.configure(this.options.taskList)), e;
	}
});
//#endregion
//#region node_modules/@tiptap/extension-paragraph/dist/index.js
var bD = "&nbsp;", xD = "\xA0", SD = Sf.create({
	name: "paragraph",
	priority: 1e3,
	addOptions() {
		return { HTMLAttributes: {} };
	},
	group: "block",
	content: "inline*",
	parseHTML() {
		return [{ tag: "p" }];
	},
	renderHTML({ HTMLAttributes: e }) {
		return [
			"p",
			F(this.options.HTMLAttributes, e),
			0
		];
	},
	parseMarkdown: (e, t) => {
		let n = e.tokens || [];
		if (n.length === 1 && n[0].type === "image") return t.parseChildren([n[0]]);
		let r = t.parseInline(n);
		return n.length === 1 && n[0].type === "text" && (n[0].raw === bD || n[0].text === bD || n[0].raw === xD || n[0].text === xD) && r.length === 1 && r[0].type === "text" && (r[0].text === bD || r[0].text === xD) ? t.createNode("paragraph", void 0, []) : t.createNode("paragraph", void 0, r);
	},
	renderMarkdown: (e, t, n) => {
		if (!e) return "";
		let r = Array.isArray(e.content) ? e.content : [];
		if (r.length === 0) {
			var i, a;
			let e = Array.isArray(n == null || (i = n.previousNode) == null ? void 0 : i.content) ? n.previousNode.content : [];
			return (n == null || (a = n.previousNode) == null ? void 0 : a.type) === "paragraph" && e.length === 0 ? bD : "";
		}
		return t.renderChildren(r);
	},
	addCommands() {
		return { setParagraph: () => ({ commands: e }) => e.setNode(this.name) };
	},
	addKeyboardShortcuts() {
		return { "Mod-Alt-0": () => this.editor.commands.setParagraph() };
	}
}), CD = /(?:^|\s)(~~(?!\s+~~)((?:[^~]+))~~(?!\s+~~))$/, wD = /(?:^|\s)(~~(?!\s+~~)((?:[^~]+))~~(?!\s+~~))/g, TD = Gd.create({
	name: "strike",
	addOptions() {
		return { HTMLAttributes: {} };
	},
	parseHTML() {
		return [
			{ tag: "s" },
			{ tag: "del" },
			{ tag: "strike" },
			{
				style: "text-decoration",
				consuming: !1,
				getAttrs: (e) => e.includes("line-through") ? {} : !1
			}
		];
	},
	renderHTML({ HTMLAttributes: e }) {
		return [
			"s",
			F(this.options.HTMLAttributes, e),
			0
		];
	},
	markdownTokenName: "del",
	parseMarkdown: (e, t) => t.applyMark("strike", t.parseInline(e.tokens || [])),
	renderMarkdown: (e, t) => `~~${t.renderChildren(e)}~~`,
	addCommands() {
		return {
			setStrike: () => ({ commands: e }) => e.setMark(this.name),
			toggleStrike: () => ({ commands: e }) => e.toggleMark(this.name),
			unsetStrike: () => ({ commands: e }) => e.unsetMark(this.name)
		};
	},
	addKeyboardShortcuts() {
		return { "Mod-Shift-s": () => this.editor.commands.toggleStrike() };
	},
	addInputRules() {
		return [gf({
			find: CD,
			type: this.type
		})];
	},
	addPasteRules() {
		return [Cf({
			find: wD,
			type: this.type
		})];
	}
}), ED = Sf.create({
	name: "text",
	group: "inline",
	parseMarkdown: (e) => ({
		type: "text",
		text: e.text || ""
	}),
	renderMarkdown: (e) => e.text || ""
}), DD = Gd.create({
	name: "underline",
	addOptions() {
		return { HTMLAttributes: {} };
	},
	parseHTML() {
		return [{ tag: "u" }, {
			style: "text-decoration",
			consuming: !1,
			getAttrs: (e) => e.includes("underline") ? {} : !1
		}];
	},
	renderHTML({ HTMLAttributes: e }) {
		return [
			"u",
			F(this.options.HTMLAttributes, e),
			0
		];
	},
	parseMarkdown(e, t) {
		return t.applyMark(this.name || "underline", t.parseInline(e.tokens || []));
	},
	renderMarkdown(e, t) {
		return `++${t.renderChildren(e)}++`;
	},
	markdownTokenizer: {
		name: "underline",
		level: "inline",
		start(e) {
			return e.indexOf("++");
		},
		tokenize(e, t, n) {
			let r = /^(\+\+)([\s\S]+?)(\+\+)/.exec(e);
			if (!r) return;
			let i = r[2].trim();
			return {
				type: "underline",
				raw: r[0],
				text: i,
				tokens: n.inlineTokens(i)
			};
		}
	},
	addCommands() {
		return {
			setUnderline: () => ({ commands: e }) => e.setMark(this.name),
			toggleUnderline: () => ({ commands: e }) => e.toggleMark(this.name),
			unsetUnderline: () => ({ commands: e }) => e.unsetMark(this.name)
		};
	},
	addKeyboardShortcuts() {
		return {
			"Mod-u": () => this.editor.commands.toggleUnderline(),
			"Mod-U": () => this.editor.commands.toggleUnderline()
		};
	}
});
//#endregion
//#region node_modules/prosemirror-dropcursor/dist/index.js
function OD(e = {}) {
	return new E({ view(t) {
		return new kD(t, e);
	} });
}
var kD = class {
	constructor(e, t) {
		this.editorView = e, this.cursorPos = null, this.element = null, this.timeout = -1, this.width = t.width ?? 1, this.color = t.color === !1 ? void 0 : t.color || "black", this.class = t.class, this.handlers = [
			"dragover",
			"dragend",
			"drop",
			"dragleave"
		].map((t) => {
			let n = (e) => {
				this[t](e);
			};
			return e.dom.addEventListener(t, n), {
				name: t,
				handler: n
			};
		});
	}
	destroy() {
		this.handlers.forEach(({ name: e, handler: t }) => this.editorView.dom.removeEventListener(e, t));
	}
	update(e, t) {
		this.cursorPos != null && t.doc != e.state.doc && (this.cursorPos > e.state.doc.content.size ? this.setCursor(null) : this.updateOverlay());
	}
	setCursor(e) {
		e != this.cursorPos && (this.cursorPos = e, e == null ? (this.element.parentNode.removeChild(this.element), this.element = null) : this.updateOverlay());
	}
	updateOverlay() {
		let e = this.editorView.state.doc.resolve(this.cursorPos), t = !e.parent.inlineContent, n, r = this.editorView.dom, i = r.getBoundingClientRect(), a = i.width / r.offsetWidth, o = i.height / r.offsetHeight;
		if (t) {
			let t = e.nodeBefore, r = e.nodeAfter;
			if (t || r) {
				let e = this.editorView.nodeDOM(this.cursorPos - (t ? t.nodeSize : 0));
				if (e) {
					let i = e.getBoundingClientRect(), a = t ? i.bottom : i.top;
					t && r && (a = (a + this.editorView.nodeDOM(this.cursorPos).getBoundingClientRect().top) / 2);
					let s = this.width / 2 * o;
					n = {
						left: i.left,
						right: i.right,
						top: a - s,
						bottom: a + s
					};
				}
			}
		}
		if (!n) {
			let e = this.editorView.coordsAtPos(this.cursorPos), t = this.width / 2 * a;
			n = {
				left: e.left - t,
				right: e.left + t,
				top: e.top,
				bottom: e.bottom
			};
		}
		let s = this.editorView.dom.offsetParent;
		this.element || (this.element = s.appendChild(document.createElement("div")), this.class && (this.element.className = this.class), this.element.style.cssText = "position: absolute; z-index: 50; pointer-events: none;", this.color && (this.element.style.backgroundColor = this.color)), this.element.classList.toggle("prosemirror-dropcursor-block", t), this.element.classList.toggle("prosemirror-dropcursor-inline", !t);
		let c, l;
		if (!s || s == document.body && getComputedStyle(s).position == "static") c = -pageXOffset, l = -pageYOffset;
		else {
			let e = s.getBoundingClientRect(), t = e.width / s.offsetWidth, n = e.height / s.offsetHeight;
			c = e.left - s.scrollLeft * t, l = e.top - s.scrollTop * n;
		}
		this.element.style.left = (n.left - c) / a + "px", this.element.style.top = (n.top - l) / o + "px", this.element.style.width = (n.right - n.left) / a + "px", this.element.style.height = (n.bottom - n.top) / o + "px";
	}
	scheduleRemoval(e) {
		clearTimeout(this.timeout), this.timeout = setTimeout(() => this.setCursor(null), e);
	}
	dragover(e) {
		if (!this.editorView.editable) return;
		let t = this.editorView.posAtCoords({
			left: e.clientX,
			top: e.clientY
		}), n = t && t.inside >= 0 && this.editorView.state.doc.nodeAt(t.inside), r = n && n.type.spec.disableDropCursor, i = typeof r == "function" ? r(this.editorView, t, e) : r;
		if (t && !i) {
			let e = t.pos;
			if (this.editorView.dragging && this.editorView.dragging.slice) {
				let t = en(this.editorView.state.doc, e, this.editorView.dragging.slice);
				t != null && (e = t);
			}
			this.setCursor(e), this.scheduleRemoval(5e3);
		}
	}
	dragend() {
		this.scheduleRemoval(20);
	}
	drop() {
		this.scheduleRemoval(20);
	}
	dragleave(e) {
		this.editorView.dom.contains(e.relatedTarget) || this.setCursor(null);
	}
}, AD = class e extends C {
	constructor(e) {
		super(e, e);
	}
	map(t, n) {
		let r = t.resolve(n.map(this.head));
		return e.valid(r) ? new e(r) : C.near(r);
	}
	content() {
		return h.empty;
	}
	eq(t) {
		return t instanceof e && t.head == this.head;
	}
	toJSON() {
		return {
			type: "gapcursor",
			pos: this.head
		};
	}
	static fromJSON(t, n) {
		if (typeof n.pos != "number") throw RangeError("Invalid input for GapCursor.fromJSON");
		return new e(t.resolve(n.pos));
	}
	getBookmark() {
		return new jD(this.anchor);
	}
	static valid(e) {
		let t = e.parent;
		if (t.inlineContent || !ND(e) || !PD(e)) return !1;
		let n = t.type.spec.allowGapCursor;
		if (n != null) return n;
		let r = t.contentMatchAt(e.index()).defaultType;
		return r && r.isTextblock;
	}
	static findGapCursorFrom(t, n, r = !1) {
		search: for (;;) {
			if (!r && e.valid(t)) return t;
			let i = t.pos, a = null;
			for (let r = t.depth;; r--) {
				let o = t.node(r);
				if (n > 0 ? t.indexAfter(r) < o.childCount : t.index(r) > 0) {
					a = o.child(n > 0 ? t.indexAfter(r) : t.index(r) - 1);
					break;
				}
				if (r == 0) return null;
				i += n;
				let s = t.doc.resolve(i);
				if (e.valid(s)) return s;
			}
			for (;;) {
				let o = n > 0 ? a.firstChild : a.lastChild;
				if (!o) {
					if (a.isAtom && !a.isText && !T.isSelectable(a)) {
						t = t.doc.resolve(i + a.nodeSize * n), r = !1;
						continue search;
					}
					break;
				}
				a = o, i += n;
				let s = t.doc.resolve(i);
				if (e.valid(s)) return s;
			}
			return null;
		}
	}
};
AD.prototype.visible = !1, AD.findFrom = AD.findGapCursorFrom, C.jsonID("gapcursor", AD);
var jD = class e {
	constructor(e) {
		this.pos = e;
	}
	map(t) {
		return new e(t.map(this.pos));
	}
	resolve(e) {
		let t = e.resolve(this.pos);
		return AD.valid(t) ? new AD(t) : C.near(t);
	}
};
function MD(e) {
	return e.isAtom || e.spec.isolating || e.spec.createGapCursor;
}
function ND(e) {
	for (let t = e.depth; t >= 0; t--) {
		let n = e.index(t), r = e.node(t);
		if (n == 0) {
			if (r.type.spec.isolating) return !0;
			continue;
		}
		for (let e = r.child(n - 1);; e = e.lastChild) {
			if (e.childCount == 0 && !e.inlineContent || MD(e.type)) return !0;
			if (e.inlineContent) return !1;
		}
	}
	return !0;
}
function PD(e) {
	for (let t = e.depth; t >= 0; t--) {
		let n = e.indexAfter(t), r = e.node(t);
		if (n == r.childCount) {
			if (r.type.spec.isolating) return !0;
			continue;
		}
		for (let e = r.child(n);; e = e.firstChild) {
			if (e.childCount == 0 && !e.inlineContent || MD(e.type)) return !0;
			if (e.inlineContent) return !1;
		}
	}
	return !0;
}
function FD() {
	return new E({ props: {
		decorations: BD,
		createSelectionBetween(e, t, n) {
			return t.pos == n.pos && AD.valid(n) ? new AD(n) : null;
		},
		handleClick: RD,
		handleKeyDown: ID,
		handleDOMEvents: { beforeinput: zD }
	} });
}
var ID = _c({
	ArrowLeft: LD("horiz", -1),
	ArrowRight: LD("horiz", 1),
	ArrowUp: LD("vert", -1),
	ArrowDown: LD("vert", 1)
});
function LD(e, t) {
	let n = e == "vert" ? t > 0 ? "down" : "up" : t > 0 ? "right" : "left";
	return function(e, r, i) {
		let a = e.selection, o = t > 0 ? a.$to : a.$from, s = a.empty;
		if (a instanceof w) {
			if (!i.endOfTextblock(n) || o.depth == 0) return !1;
			s = !1, o = e.doc.resolve(t > 0 ? o.after() : o.before());
		}
		let c = AD.findGapCursorFrom(o, t, s);
		return c ? (r && r(e.tr.setSelection(new AD(c))), !0) : !1;
	};
}
function RD(e, t, n) {
	if (!e || !e.editable) return !1;
	let r = e.state.doc.resolve(t);
	if (!AD.valid(r)) return !1;
	let i = e.posAtCoords({
		left: n.clientX,
		top: n.clientY
	});
	return i && i.inside > -1 && T.isSelectable(e.state.doc.nodeAt(i.inside)) ? !1 : (e.dispatch(e.state.tr.setSelection(new AD(r))), !0);
}
function zD(e, t) {
	if (t.inputType != "insertCompositionText" || !(e.state.selection instanceof AD)) return !1;
	let { $from: n } = e.state.selection, r = n.parent.contentMatchAt(n.index()).findWrapping(e.state.schema.nodes.text);
	if (!r) return !1;
	let i = l.empty;
	for (let e = r.length - 1; e >= 0; e--) i = l.from(r[e].createAndFill(null, i));
	let a = e.state.tr.replace(n.pos, n.pos, new h(i, 0, 0));
	return a.setSelection(w.near(a.doc.resolve(n.pos + 1))), e.dispatch(a), !1;
}
function BD(e) {
	if (!(e.selection instanceof AD)) return null;
	let t = document.createElement("div");
	return t.className = "ProseMirror-gapcursor", j.create(e.doc, [A.widget(e.selection.head, t, { key: "gapcursor" })]);
}
//#endregion
//#region node_modules/rope-sequence/dist/index.js
var VD = 200, HD = function() {};
HD.prototype.append = function(e) {
	return e.length ? (e = HD.from(e), !this.length && e || e.length < VD && this.leafAppend(e) || this.length < VD && e.leafPrepend(this) || this.appendInner(e)) : this;
}, HD.prototype.prepend = function(e) {
	return e.length ? HD.from(e).append(this) : this;
}, HD.prototype.appendInner = function(e) {
	return new WD(this, e);
}, HD.prototype.slice = function(e, t) {
	return e === void 0 && (e = 0), t === void 0 && (t = this.length), e >= t ? HD.empty : this.sliceInner(Math.max(0, e), Math.min(this.length, t));
}, HD.prototype.get = function(e) {
	if (!(e < 0 || e >= this.length)) return this.getInner(e);
}, HD.prototype.forEach = function(e, t, n) {
	t === void 0 && (t = 0), n === void 0 && (n = this.length), t <= n ? this.forEachInner(e, t, n, 0) : this.forEachInvertedInner(e, t, n, 0);
}, HD.prototype.map = function(e, t, n) {
	t === void 0 && (t = 0), n === void 0 && (n = this.length);
	var r = [];
	return this.forEach(function(t, n) {
		return r.push(e(t, n));
	}, t, n), r;
}, HD.from = function(e) {
	return e instanceof HD ? e : e && e.length ? new UD(e) : HD.empty;
};
var UD = /* @__PURE__ */ function(e) {
	function t(t) {
		e.call(this), this.values = t;
	}
	e && (t.__proto__ = e), t.prototype = Object.create(e && e.prototype), t.prototype.constructor = t;
	var n = {
		length: { configurable: !0 },
		depth: { configurable: !0 }
	};
	return t.prototype.flatten = function() {
		return this.values;
	}, t.prototype.sliceInner = function(e, n) {
		return e == 0 && n == this.length ? this : new t(this.values.slice(e, n));
	}, t.prototype.getInner = function(e) {
		return this.values[e];
	}, t.prototype.forEachInner = function(e, t, n, r) {
		for (var i = t; i < n; i++) if (e(this.values[i], r + i) === !1) return !1;
	}, t.prototype.forEachInvertedInner = function(e, t, n, r) {
		for (var i = t - 1; i >= n; i--) if (e(this.values[i], r + i) === !1) return !1;
	}, t.prototype.leafAppend = function(e) {
		if (this.length + e.length <= VD) return new t(this.values.concat(e.flatten()));
	}, t.prototype.leafPrepend = function(e) {
		if (this.length + e.length <= VD) return new t(e.flatten().concat(this.values));
	}, n.length.get = function() {
		return this.values.length;
	}, n.depth.get = function() {
		return 0;
	}, Object.defineProperties(t.prototype, n), t;
}(HD);
HD.empty = new UD([]);
var WD = /* @__PURE__ */ function(e) {
	function t(t, n) {
		e.call(this), this.left = t, this.right = n, this.length = t.length + n.length, this.depth = Math.max(t.depth, n.depth) + 1;
	}
	return e && (t.__proto__ = e), t.prototype = Object.create(e && e.prototype), t.prototype.constructor = t, t.prototype.flatten = function() {
		return this.left.flatten().concat(this.right.flatten());
	}, t.prototype.getInner = function(e) {
		return e < this.left.length ? this.left.get(e) : this.right.get(e - this.left.length);
	}, t.prototype.forEachInner = function(e, t, n, r) {
		var i = this.left.length;
		if (t < i && this.left.forEachInner(e, t, Math.min(n, i), r) === !1 || n > i && this.right.forEachInner(e, Math.max(t - i, 0), Math.min(this.length, n) - i, r + i) === !1) return !1;
	}, t.prototype.forEachInvertedInner = function(e, t, n, r) {
		var i = this.left.length;
		if (t > i && this.right.forEachInvertedInner(e, t - i, Math.max(n, i) - i, r + i) === !1 || n < i && this.left.forEachInvertedInner(e, Math.min(t, i), n, r) === !1) return !1;
	}, t.prototype.sliceInner = function(e, t) {
		if (e == 0 && t == this.length) return this;
		var n = this.left.length;
		return t <= n ? this.left.slice(e, t) : e >= n ? this.right.slice(e - n, t - n) : this.left.slice(e, n).append(this.right.slice(0, t - n));
	}, t.prototype.leafAppend = function(e) {
		var n = this.right.leafAppend(e);
		if (n) return new t(this.left, n);
	}, t.prototype.leafPrepend = function(e) {
		var n = this.left.leafPrepend(e);
		if (n) return new t(n, this.right);
	}, t.prototype.appendInner = function(e) {
		return this.left.depth >= Math.max(this.right.depth, e.depth) + 1 ? new t(this.left, new t(this.right, e)) : new t(this, e);
	}, t;
}(HD), GD = 500, KD = class e {
	constructor(e, t) {
		this.items = e, this.eventCount = t;
	}
	popEvent(t, n) {
		if (this.eventCount == 0) return null;
		let r = this.items.length;
		for (;; r--) if (this.items.get(r - 1).selection) {
			--r;
			break;
		}
		let i, a;
		n && (i = this.remapping(r, this.items.length), a = i.maps.length);
		let o = t.tr, s, c, l = [], u = [];
		return this.items.forEach((t, n) => {
			if (!t.step) {
				i || (i = this.remapping(r, n + 1), a = i.maps.length), a--, u.push(t);
				return;
			}
			if (i) {
				u.push(new JD(t.map));
				let e = t.step.map(i.slice(a)), n;
				e && o.maybeStep(e).doc && (n = o.mapping.maps[o.mapping.maps.length - 1], l.push(new JD(n, void 0, void 0, l.length + u.length))), a--, n && i.appendMap(n, a);
			} else o.maybeStep(t.step);
			if (t.selection) return s = i ? t.selection.map(i.slice(a)) : t.selection, c = new e(this.items.slice(0, r).append(u.reverse().concat(l)), this.eventCount - 1), !1;
		}, this.items.length, 0), {
			remaining: c,
			transform: o,
			selection: s
		};
	}
	addTransform(t, n, r, i) {
		let a = [], o = this.eventCount, s = this.items, c = !i && s.length ? s.get(s.length - 1) : null;
		for (let e = 0; e < t.steps.length; e++) {
			let r = t.steps[e].invert(t.docs[e]), l = new JD(t.mapping.maps[e], r, n), u;
			(u = c && c.merge(l)) && (l = u, e ? a.pop() : s = s.slice(0, s.length - 1)), a.push(l), n && (o++, n = void 0), i || (c = l);
		}
		let l = o - r.depth;
		return l > XD && (s = qD(s, l), o -= l), new e(s.append(a), o);
	}
	remapping(e, t) {
		let n = new vt();
		return this.items.forEach((t, r) => {
			let i = t.mirrorOffset != null && r - t.mirrorOffset >= e ? n.maps.length - t.mirrorOffset : void 0;
			n.appendMap(t.map, i);
		}, e, t), n;
	}
	addMaps(t) {
		return this.eventCount == 0 ? this : new e(this.items.append(t.map((e) => new JD(e))), this.eventCount);
	}
	rebased(t, n) {
		if (!this.eventCount) return this;
		let r = [], i = Math.max(0, this.items.length - n), a = t.mapping, o = t.steps.length, s = this.eventCount;
		this.items.forEach((e) => {
			e.selection && s--;
		}, i);
		let c = n;
		this.items.forEach((e) => {
			let n = a.getMirror(--c);
			if (n == null) return;
			o = Math.min(o, n);
			let i = a.maps[n];
			if (e.step) {
				let o = t.steps[n].invert(t.docs[n]), l = e.selection && e.selection.map(a.slice(c + 1, n));
				l && s++, r.push(new JD(i, o, l));
			} else r.push(new JD(i));
		}, i);
		let l = [];
		for (let e = n; e < o; e++) l.push(new JD(a.maps[e]));
		let u = this.items.slice(0, i).append(l).append(r), d = new e(u, s);
		return d.emptyItemCount() > GD && (d = d.compress(this.items.length - r.length)), d;
	}
	emptyItemCount() {
		let e = 0;
		return this.items.forEach((t) => {
			t.step || e++;
		}), e;
	}
	compress(t = this.items.length) {
		let n = this.remapping(0, t), r = n.maps.length, i = [], a = 0;
		return this.items.forEach((e, o) => {
			if (o >= t) i.push(e), e.selection && a++;
			else if (e.step) {
				let t = e.step.map(n.slice(r)), o = t && t.getMap();
				if (r--, o && n.appendMap(o, r), t) {
					let s = e.selection && e.selection.map(n.slice(r));
					s && a++;
					let c = new JD(o.invert(), t, s), l, u = i.length - 1;
					(l = i.length && i[u].merge(c)) ? i[u] = l : i.push(c);
				}
			} else e.map && r--;
		}, this.items.length, 0), new e(HD.from(i.reverse()), a);
	}
};
KD.empty = new KD(HD.empty, 0);
function qD(e, t) {
	let n;
	return e.forEach((e, r) => {
		if (e.selection && t-- == 0) return n = r, !1;
	}), e.slice(n);
}
var JD = class e {
	constructor(e, t, n, r) {
		this.map = e, this.step = t, this.selection = n, this.mirrorOffset = r;
	}
	merge(t) {
		if (this.step && t.step && !t.selection) {
			let n = t.step.merge(this.step);
			if (n) return new e(n.getMap().invert(), n, this.selection);
		}
	}
}, YD = class {
	constructor(e, t, n, r, i) {
		this.done = e, this.undone = t, this.prevRanges = n, this.prevTime = r, this.prevComposition = i;
	}
}, XD = 20;
function ZD(e, t, n, r) {
	let i = n.getMeta(aO), a;
	if (i) return i.historyState;
	n.getMeta(oO) && (e = new YD(e.done, e.undone, null, 0, -1));
	let o = n.getMeta("appendedTransaction");
	if (n.steps.length == 0) return e;
	if (o && o.getMeta(aO)) return o.getMeta(aO).redo ? new YD(e.done.addTransform(n, void 0, r, iO(t)), e.undone, $D(n.mapping.maps), e.prevTime, e.prevComposition) : new YD(e.done, e.undone.addTransform(n, void 0, r, iO(t)), null, e.prevTime, e.prevComposition);
	if (n.getMeta("addToHistory") !== !1 && !(o && o.getMeta("addToHistory") === !1)) {
		let i = n.getMeta("composition"), a = e.prevTime == 0 || !o && e.prevComposition != i && (e.prevTime < (n.time || 0) - r.newGroupDelay || !QD(n, e.prevRanges)), s = o ? eO(e.prevRanges, n.mapping) : $D(n.mapping.maps);
		return new YD(e.done.addTransform(n, a ? t.selection.getBookmark() : void 0, r, iO(t)), KD.empty, s, n.time, i ?? e.prevComposition);
	}
	return (a = n.getMeta("rebased")) ? new YD(e.done.rebased(n, a), e.undone.rebased(n, a), eO(e.prevRanges, n.mapping), e.prevTime, e.prevComposition) : new YD(e.done.addMaps(n.mapping.maps), e.undone.addMaps(n.mapping.maps), eO(e.prevRanges, n.mapping), e.prevTime, e.prevComposition);
}
function QD(e, t) {
	if (!t) return !1;
	if (!e.docChanged) return !0;
	let n = !1;
	return e.mapping.maps[0].forEach((e, r) => {
		for (let i = 0; i < t.length; i += 2) e <= t[i + 1] && r >= t[i] && (n = !0);
	}), n;
}
function $D(e) {
	let t = [];
	for (let n = e.length - 1; n >= 0 && t.length == 0; n--) e[n].forEach((e, n, r, i) => t.push(r, i));
	return t;
}
function eO(e, t) {
	if (!e) return null;
	let n = [];
	for (let r = 0; r < e.length; r += 2) {
		let i = t.map(e[r], 1), a = t.map(e[r + 1], -1);
		i <= a && n.push(i, a);
	}
	return n;
}
function tO(e, t, n) {
	let r = iO(t), i = aO.get(t).spec.config, a = (n ? e.undone : e.done).popEvent(t, r);
	if (!a) return null;
	let o = a.selection.resolve(a.transform.doc), s = (n ? e.done : e.undone).addTransform(a.transform, t.selection.getBookmark(), i, r), c = new YD(n ? s : a.remaining, n ? a.remaining : s, null, 0, -1);
	return a.transform.setSelection(o).setMeta(aO, {
		redo: n,
		historyState: c
	});
}
var nO = !1, rO = null;
function iO(e) {
	let t = e.plugins;
	if (rO != t) {
		nO = !1, rO = t;
		for (let e = 0; e < t.length; e++) if (t[e].spec.historyPreserveItems) {
			nO = !0;
			break;
		}
	}
	return nO;
}
var aO = new D("history"), oO = new D("closeHistory");
function sO(e = {}) {
	return e = {
		depth: e.depth || 100,
		newGroupDelay: e.newGroupDelay || 500
	}, new E({
		key: aO,
		state: {
			init() {
				return new YD(KD.empty, KD.empty, null, 0, -1);
			},
			apply(t, n, r) {
				return ZD(n, r, t, e);
			}
		},
		config: e,
		props: { handleDOMEvents: { beforeinput(e, t) {
			let n = t.inputType, r = n == "historyUndo" ? lO : n == "historyRedo" ? uO : null;
			return !r || !e.editable ? !1 : (t.preventDefault(), r(e.state, e.dispatch));
		} } }
	});
}
function cO(e, t) {
	return (n, r) => {
		let i = aO.getState(n);
		if (!i || (e ? i.undone : i.done).eventCount == 0) return !1;
		if (r) {
			let a = tO(i, n, e);
			a && r(t ? a.scrollIntoView() : a);
		}
		return !0;
	};
}
var lO = cO(!1, !0), uO = cO(!0, !0);
I.create({
	name: "characterCount",
	addOptions() {
		return {
			limit: null,
			autoTrim: !0,
			mode: "textSize",
			textCounter: (e) => e.length,
			wordCounter: (e) => e.split(" ").filter((e) => e !== "").length
		};
	},
	addStorage() {
		return {
			characters: () => 0,
			words: () => 0
		};
	},
	onBeforeCreate() {
		this.storage.characters = (e) => {
			let t = e?.node || this.editor.state.doc;
			if ((e?.mode || this.options.mode) === "textSize") {
				let e = t.textBetween(0, t.content.size, void 0, " ");
				return this.options.textCounter(e);
			}
			return t.nodeSize;
		}, this.storage.words = (e) => {
			let t = e?.node || this.editor.state.doc, n = t.textBetween(0, t.content.size, " ", " ");
			return this.options.wordCounter(n);
		};
	},
	addProseMirrorPlugins() {
		let e = !1;
		return [new E({
			key: new D("characterCount"),
			appendTransaction: (t, n, r) => {
				if (e) return;
				let i = this.options.limit, a = this.options.autoTrim;
				if (i == null || i === 0 || a === !1) {
					e = !0;
					return;
				}
				let o = this.storage.characters({ node: r.doc });
				if (o > i) {
					let t = o - i;
					console.warn(`[CharacterCount] Initial content exceeded limit of ${i} characters. Content was automatically trimmed.`);
					let n = r.tr.deleteRange(0, t);
					return e = !0, n;
				}
				e = !0;
			},
			filterTransaction: (e, t) => {
				let n = this.options.limit;
				if (!e.docChanged || n === 0 || n == null) return !0;
				let r = this.storage.characters({ node: t.doc }), i = this.storage.characters({ node: e.doc });
				if (i <= n || r > n && i > n && i <= r) return !0;
				if (r > n && i > n && i > r || !e.getMeta("paste")) return !1;
				let a = e.selection.$head.pos, o = a - (i - n), s = a;
				return e.deleteRange(o, s), !(this.storage.characters({ node: e.doc }) > n);
			}
		})];
	}
});
var dO = I.create({
	name: "dropCursor",
	addOptions() {
		return {
			color: "currentColor",
			width: 1,
			class: void 0
		};
	},
	addProseMirrorPlugins() {
		return [OD(this.options)];
	}
});
I.create({
	name: "focus",
	addOptions() {
		return {
			className: "has-focus",
			mode: "all"
		};
	},
	addProseMirrorPlugins() {
		return [new E({
			key: new D("focus"),
			props: { decorations: ({ doc: e, selection: t }) => {
				let { isEditable: n, isFocused: r } = this.editor, { anchor: i } = t, a = [];
				if (!n || !r) return j.create(e, []);
				let o = 0;
				this.options.mode === "deepest" && e.descendants((e, t) => {
					if (!e.isText) {
						if (!(i >= t && i <= t + e.nodeSize - 1)) return !1;
						o += 1;
					}
				});
				let s = 0;
				return e.descendants((e, t) => {
					if (e.isText || !(i >= t && i <= t + e.nodeSize - 1)) return !1;
					if (s += 1, this.options.mode === "deepest" && o - s > 0 || this.options.mode === "shallowest" && s > 1) return this.options.mode === "deepest";
					a.push(A.node(t, t + e.nodeSize, { class: this.options.className }));
				}), j.create(e, a);
			} }
		})];
	}
});
var fO = I.create({
	name: "gapCursor",
	addProseMirrorPlugins() {
		return [FD()];
	},
	extendNodeSchema(e) {
		return { allowGapCursor: P(N(e, "allowGapCursor", {
			name: e.name,
			options: e.options,
			storage: e.storage
		})) ?? null };
	}
}), pO = "placeholder", mO = new D("tiptap__placeholder");
function hO(e) {
	let { editor: t, placeholder: n, dataAttribute: r, pos: i, node: a, isEmptyDoc: o, hasAnchor: s, classes: { emptyNode: c, emptyEditor: l } } = e, u = [c];
	return o && u.push(l), A.node(i, i + a.nodeSize, {
		class: u.join(" "),
		[r]: typeof n == "function" ? n({
			editor: t,
			node: a,
			pos: i,
			hasAnchor: s
		}) : n
	});
}
function gO(e, t) {
	return typeof e == "function" ? e(t) : e;
}
function _O({ editor: e, options: t, dataAttribute: n, doc: r, selection: i, from: a, to: o }) {
	let { anchor: s } = i, c = [], l = e.isEmpty;
	return r.nodesBetween(a, o, (r, i) => {
		let a = s >= i && s <= i + r.nodeSize, o = !r.isLeaf && wu(r);
		return r.type.isTextblock && (a || !t.showOnlyCurrent) && o && c.push(hO({
			editor: e,
			isEmptyDoc: l,
			dataAttribute: n,
			hasAnchor: a,
			placeholder: t.placeholder,
			classes: {
				emptyEditor: t.emptyEditorClass,
				emptyNode: gO(t.emptyNodeClass, {
					editor: e,
					node: r,
					pos: i,
					hasAnchor: a
				})
			},
			node: r,
			pos: i
		})), t.includeChildren;
	}), c;
}
function vO({ editor: e, options: t, dataAttribute: n, doc: r, selection: i }) {
	if (!e.isEditable && t.showOnlyWhenEditable) return null;
	let { anchor: a } = i, o = [], s = e.isEmpty;
	if (t.showOnlyCurrent && !t.includeChildren) {
		let i = r.resolve(a), c = i.depth > 0 ? i.node(1) : i.nodeAfter, l = i.depth > 0 ? i.before(1) : a;
		if (c && c.type.isTextblock && wu(c)) {
			let r = a >= l && a <= l + c.nodeSize;
			o.push(hO({
				editor: e,
				isEmptyDoc: s,
				dataAttribute: n,
				hasAnchor: r,
				placeholder: t.placeholder,
				classes: {
					emptyEditor: t.emptyEditorClass,
					emptyNode: gO(t.emptyNodeClass, {
						editor: e,
						node: c,
						pos: l,
						hasAnchor: r
					})
				},
				node: c,
				pos: l
			}));
		}
	} else o.push(..._O({
		editor: e,
		options: t,
		dataAttribute: n,
		doc: r,
		selection: i,
		from: 0,
		to: r.content.size
	}));
	return j.create(r, o);
}
function yO(e, t) {
	let n = e.resolve(t);
	if (n.depth === 0) {
		let e = n.nodeAfter ?? n.nodeBefore;
		if (!e) return {
			from: t,
			to: t
		};
		let r = n.nodeAfter ? t : t - e.nodeSize;
		return {
			from: r,
			to: r + e.nodeSize
		};
	}
	let r = n.before(1);
	return {
		from: r,
		to: r + n.node(1).nodeSize
	};
}
function bO(e, t) {
	return {
		from: Math.max(0, t.from - 1),
		to: Math.min(e.content.size, t.to - 1)
	};
}
function xO(e, t, n) {
	let r = [];
	return e.forEach((e, i) => {
		let a = i, o = a + e.nodeSize, s = a + 1, c = o + 1;
		s < n && c > t && r.push({
			from: a,
			to: o
		});
	}), r;
}
function SO(e) {
	if (e.length === 0) return [];
	let t = [...e].sort((e, t) => e.from - t.from), n = [{ ...t[0] }];
	for (let e = 1; e < t.length; e += 1) {
		let r = n[n.length - 1], i = t[e];
		i.from <= r.to ? r.to = Math.max(r.to, i.to) : n.push({ ...i });
	}
	return n;
}
function CO(e, t) {
	let n = xO(e, t.from, t.to);
	return n.push(bO(e, yO(e, t.from))), t.to > t.from ? n.push(bO(e, yO(e, Math.min(t.to, e.content.size + 1) - 1))) : t.from < e.content.size + 1 && n.push(bO(e, yO(e, Math.min(t.from + 1, e.content.size)))), n;
}
function wO(e, t, n) {
	let r = [];
	if (e.docChanged) {
		let t = du(e);
		for (let e of t) r.push(...CO(n.doc, e.newRange));
	}
	return e.selectionSet && (r.push(bO(n.doc, yO(n.doc, e.mapping.map(t.selection.anchor)))), r.push(bO(n.doc, yO(n.doc, n.selection.anchor)))), SO(r);
}
function TO(e, t, n) {
	let r = Math.max(0, Math.min(e, n.content.size));
	return {
		from: r,
		to: Math.max(r, Math.min(t, n.content.size))
	};
}
function EO({ decorations: e, ranges: t, editor: n, options: r, dataAttribute: i, doc: a, selection: o }) {
	let s = e;
	for (let e of t) {
		let { from: t, to: c } = TO(e.from, e.to, a), l = s.find(t, c).filter((e) => e.from >= t && e.to <= c);
		l.length && (s = s.remove(l));
		let u = _O({
			editor: n,
			options: r,
			dataAttribute: i,
			doc: a,
			selection: o,
			from: t,
			to: c
		});
		u.length && (s = s.add(a, u));
	}
	return s;
}
function DO({ editor: e, options: t, dataAttribute: n }) {
	return {
		init(r, i) {
			return vO({
				editor: e,
				options: t,
				dataAttribute: n,
				doc: i.doc,
				selection: i.selection
			}) ?? j.empty;
		},
		apply(r, i, a, o) {
			return !r.docChanged && !r.selectionSet ? i : EO({
				decorations: i.map(r.mapping, r.doc),
				ranges: wO(r, a, o),
				editor: e,
				options: t,
				dataAttribute: n,
				doc: o.doc,
				selection: o.selection
			});
		}
	};
}
function OO(e) {
	return e.replace(/\s+/g, "-").replace(/[^a-zA-Z0-9-]/g, "").replace(/^[0-9-]+/, "").replace(/^-+/, "").toLowerCase();
}
function kO({ editor: e, options: t }) {
	let n = t.dataAttribute ? `data-${OO(t.dataAttribute)}` : `data-${pO}`, r = t.showOnlyCurrent && !t.includeChildren;
	return new E({
		key: mO,
		...r ? {} : { state: DO({
			editor: e,
			options: t,
			dataAttribute: n
		}) },
		props: { decorations: r ? ({ doc: r, selection: i }) => vO({
			editor: e,
			options: t,
			dataAttribute: n,
			doc: r,
			selection: i
		}) : (n) => t.showOnlyWhenEditable && !e.isEditable ? j.empty : mO.getState(n) ?? j.empty }
	});
}
I.create({
	name: "placeholder",
	addOptions() {
		return {
			emptyEditorClass: "is-editor-empty",
			emptyNodeClass: "is-empty",
			dataAttribute: pO,
			placeholder: "Write something …",
			showOnlyWhenEditable: !0,
			showOnlyCurrent: !0,
			includeChildren: !1
		};
	},
	addProseMirrorPlugins() {
		return [kO({
			editor: this.editor,
			options: this.options
		})];
	}
});
function AO(e, t) {
	return !e.selection.empty && !Tu(e.selection) && t.isEditable;
}
function jO(e, t) {
	return AO(e, t) && !t.isFocused && !t.view.dragging;
}
function MO() {
	var e;
	(e = window.getSelection()) == null || e.removeAllRanges();
}
function NO(e) {
	e.focus();
}
I.create({
	name: "selection",
	addOptions() {
		return { className: "selection" };
	},
	addProseMirrorPlugins() {
		let { editor: e, options: t } = this;
		return [new E({
			key: new D("selection"),
			props: {
				decorations(n) {
					return jO(n, e) ? j.create(n.doc, [A.inline(n.selection.from, n.selection.to, { class: t.className })]) : null;
				},
				handleDOMEvents: {
					blur(t) {
						return AO(t.state, e) && MO(), !1;
					},
					focus(t) {
						return AO(t.state, e) && requestAnimationFrame(() => {
							!e.isDestroyed && t.hasFocus() && NO(t);
						}), !1;
					}
				}
			}
		})];
	}
});
function PO({ types: e, node: t }) {
	return t && Array.isArray(e) && e.includes(t.type) || t?.type === e;
}
var FO = I.create({
	name: "trailingNode",
	addOptions() {
		return {
			node: void 0,
			notAfter: []
		};
	},
	addProseMirrorPlugins() {
		let e = new D(this.name), t = this.options.node || this.editor.schema.topNodeType.contentMatch.defaultType?.name || "paragraph", n = Object.entries(this.editor.schema.nodes).map(([, e]) => e).filter((e) => (this.options.notAfter || []).concat(t).includes(e.name));
		return [new E({
			key: e,
			appendTransaction: (n, r, i) => {
				let { doc: a, tr: o, schema: s } = i, c = e.getState(i), l = a.content.size, u = s.nodes[t];
				if (!n.some((e) => e.getMeta("skipTrailingNode")) && c) return o.insert(l, u.create());
			},
			state: {
				init: (e, t) => {
					let r = t.tr.doc.lastChild;
					return !PO({
						node: r,
						types: n
					});
				},
				apply: (e, t) => {
					if (!e.docChanged || e.getMeta("__uniqueIDTransaction")) return t;
					let r = e.doc.lastChild;
					return !PO({
						node: r,
						types: n
					});
				}
			}
		})];
	}
}), IO = I.create({
	name: "undoRedo",
	addOptions() {
		return {
			depth: 100,
			newGroupDelay: 500
		};
	},
	addCommands() {
		return {
			undo: () => ({ state: e, dispatch: t }) => lO(e, t),
			redo: () => ({ state: e, dispatch: t }) => uO(e, t)
		};
	},
	addProseMirrorPlugins() {
		return [sO(this.options)];
	},
	addKeyboardShortcuts() {
		return {
			"Mod-z": () => this.editor.commands.undo(),
			"Shift-Mod-z": () => this.editor.commands.redo(),
			"Mod-y": () => this.editor.commands.redo(),
			"Mod-я": () => this.editor.commands.undo(),
			"Shift-Mod-я": () => this.editor.commands.redo()
		};
	}
}), LO = I.create({
	name: "starterKit",
	addExtensions() {
		let e = [];
		return this.options.bold !== !1 && e.push(AC.configure(this.options.bold)), this.options.blockquote !== !1 && e.push(TC.configure(this.options.blockquote)), this.options.bulletList !== !1 && e.push(yE.configure(this.options.bulletList)), this.options.code !== !1 && e.push(NC.configure(this.options.code)), this.options.codeBlock !== !1 && e.push(LC.configure(this.options.codeBlock)), this.options.document !== !1 && e.push(RC.configure(this.options.document)), this.options.dropcursor !== !1 && e.push(dO.configure(this.options.dropcursor)), this.options.gapcursor !== !1 && e.push(fO.configure(this.options.gapcursor)), this.options.hardBreak !== !1 && e.push(zC.configure(this.options.hardBreak)), this.options.heading !== !1 && e.push(BC.configure(this.options.heading)), this.options.undoRedo !== !1 && e.push(IO.configure(this.options.undoRedo)), this.options.horizontalRule !== !1 && e.push(VC.configure(this.options.horizontalRule)), this.options.italic !== !1 && e.push(KC.configure(this.options.italic)), this.options.listItem !== !1 && e.push(HE.configure(this.options.listItem)), this.options.listKeymap !== !1 && e.push(ZE.configure(this.options?.listKeymap)), this.options.link !== !1 && e.push(mE.configure(this.options?.link)), this.options.orderedList !== !1 && e.push(mD.configure(this.options.orderedList)), this.options.paragraph !== !1 && e.push(SD.configure(this.options.paragraph)), this.options.strike !== !1 && e.push(TD.configure(this.options.strike)), this.options.text !== !1 && e.push(ED.configure(this.options.text)), this.options.underline !== !1 && e.push(DD.configure(this.options?.underline)), this.options.trailingNode !== !1 && e.push(FO.configure(this.options?.trailingNode)), e;
	}
}), RO = I.create({
	name: "textAlign",
	addOptions() {
		return {
			types: [],
			alignments: [
				"left",
				"center",
				"right",
				"justify"
			],
			defaultAlignment: null
		};
	},
	addGlobalAttributes() {
		return [{
			types: this.options.types,
			attributes: { textAlign: {
				default: this.options.defaultAlignment,
				parseHTML: (e) => {
					let t = e.style.textAlign;
					return this.options.alignments.includes(t) ? t : this.options.defaultAlignment;
				},
				renderHTML: (e) => e.textAlign ? { style: `text-align: ${e.textAlign}` } : {}
			} }
		}];
	},
	addCommands() {
		return {
			setTextAlign: (e) => ({ commands: t }) => this.options.alignments.includes(e) ? this.options.types.map((n) => t.updateAttributes(n, { textAlign: e })).some((e) => e) : !1,
			unsetTextAlign: () => ({ commands: e }) => this.options.types.map((t) => e.resetAttributes(t, "textAlign")).some((e) => e),
			toggleTextAlign: (e) => ({ editor: t, commands: n }) => this.options.alignments.includes(e) ? t.isActive({ textAlign: e }) ? n.unsetTextAlign() : n.setTextAlign(e) : !1
		};
	},
	addKeyboardShortcuts() {
		return {
			"Mod-Shift-l": () => this.editor.commands.setTextAlign("left"),
			"Mod-Shift-e": () => this.editor.commands.setTextAlign("center"),
			"Mod-Shift-r": () => this.editor.commands.setTextAlign("right"),
			"Mod-Shift-j": () => this.editor.commands.setTextAlign("justify")
		};
	}
}), zO, BO;
if (typeof WeakMap < "u") {
	let e = /* @__PURE__ */ new WeakMap();
	zO = (t) => e.get(t), BO = (t, n) => (e.set(t, n), n);
} else {
	let e = [], t = 0;
	zO = (t) => {
		for (let n = 0; n < e.length; n += 2) if (e[n] == t) return e[n + 1];
	}, BO = (n, r) => (t == 10 && (t = 0), e[t++] = n, e[t++] = r);
}
var Q = class {
	constructor(e, t, n, r) {
		this.width = e, this.height = t, this.map = n, this.problems = r;
	}
	findCell(e) {
		for (let t = 0; t < this.map.length; t++) {
			let n = this.map[t];
			if (n != e) continue;
			let r = t % this.width, i = t / this.width | 0, a = r + 1, o = i + 1;
			for (let e = 1; a < this.width && this.map[t + e] == n; e++) a++;
			for (let e = 1; o < this.height && this.map[t + this.width * e] == n; e++) o++;
			return {
				left: r,
				top: i,
				right: a,
				bottom: o
			};
		}
		throw RangeError(`No cell with offset ${e} found`);
	}
	colCount(e) {
		for (let t = 0; t < this.map.length; t++) if (this.map[t] == e) return t % this.width;
		throw RangeError(`No cell with offset ${e} found`);
	}
	nextCell(e, t, n) {
		let { left: r, right: i, top: a, bottom: o } = this.findCell(e);
		return t == "horiz" ? (n < 0 ? r == 0 : i == this.width) ? null : this.map[a * this.width + (n < 0 ? r - 1 : i)] : (n < 0 ? a == 0 : o == this.height) ? null : this.map[r + this.width * (n < 0 ? a - 1 : o)];
	}
	rectBetween(e, t) {
		let { left: n, right: r, top: i, bottom: a } = this.findCell(e), { left: o, right: s, top: c, bottom: l } = this.findCell(t);
		return {
			left: Math.min(n, o),
			top: Math.min(i, c),
			right: Math.max(r, s),
			bottom: Math.max(a, l)
		};
	}
	cellsInRect(e) {
		let t = [], n = {};
		for (let r = e.top; r < e.bottom; r++) for (let i = e.left; i < e.right; i++) {
			let a = r * this.width + i, o = this.map[a];
			n[o] || (n[o] = !0, !(i == e.left && i && this.map[a - 1] == o || r == e.top && r && this.map[a - this.width] == o) && t.push(o));
		}
		return t;
	}
	positionAt(e, t, n) {
		for (let r = 0, i = 0;; r++) {
			let a = i + n.child(r).nodeSize;
			if (r == e) {
				let n = t + e * this.width, r = (e + 1) * this.width;
				for (; n < r && this.map[n] < i;) n++;
				return n == r ? a - 1 : this.map[n];
			}
			i = a;
		}
	}
	static get(e) {
		return zO(e) || BO(e, VO(e));
	}
};
function VO(e) {
	if (e.type.spec.tableRole != "table") throw RangeError("Not a table node: " + e.type.name);
	let t = HO(e), n = e.childCount, r = [], i = 0, a = null, o = [];
	for (let e = 0, i = t * n; e < i; e++) r[e] = 0;
	for (let s = 0, c = 0; s < n; s++) {
		let l = e.child(s);
		c++;
		for (let e = 0;; e++) {
			for (; i < r.length && r[i] != 0;) i++;
			if (e == l.childCount) break;
			let u = l.child(e), { colspan: d, rowspan: f, colwidth: p } = u.attrs;
			for (let e = 0; e < f; e++) {
				if (e + s >= n) {
					(a || (a = [])).push({
						type: "overlong_rowspan",
						pos: c,
						n: f - e
					});
					break;
				}
				let l = i + e * t;
				for (let e = 0; e < d; e++) {
					r[l + e] == 0 ? r[l + e] = c : (a || (a = [])).push({
						type: "collision",
						row: s,
						pos: c,
						n: d - e
					});
					let n = p && p[e];
					if (n) {
						let r = (l + e) % t * 2, i = o[r];
						i == null || i != n && o[r + 1] == 1 ? (o[r] = n, o[r + 1] = 1) : i == n && o[r + 1]++;
					}
				}
			}
			i += d, c += u.nodeSize;
		}
		let u = (s + 1) * t, d = 0;
		for (; i < u;) r[i++] == 0 && d++;
		d && (a || (a = [])).push({
			type: "missing",
			row: s,
			n: d
		}), c++;
	}
	(t === 0 || n === 0) && (a || (a = [])).push({ type: "zero_sized" });
	let s = new Q(t, n, r, a), c = !1;
	for (let e = 0; !c && e < o.length; e += 2) o[e] != null && o[e + 1] < n && (c = !0);
	return c && UO(s, o, e), s;
}
function HO(e) {
	let t = -1, n = !1;
	for (let r = 0; r < e.childCount; r++) {
		let i = e.child(r), a = 0;
		if (n) for (let t = 0; t < r; t++) {
			let n = e.child(t);
			for (let e = 0; e < n.childCount; e++) {
				let i = n.child(e);
				t + i.attrs.rowspan > r && (a += i.attrs.colspan);
			}
		}
		for (let e = 0; e < i.childCount; e++) {
			let t = i.child(e);
			a += t.attrs.colspan, t.attrs.rowspan > 1 && (n = !0);
		}
		t == -1 ? t = a : t != a && (t = Math.max(t, a));
	}
	return t;
}
function UO(e, t, n) {
	e.problems || (e.problems = []);
	let r = {};
	for (let i = 0; i < e.map.length; i++) {
		let a = e.map[i];
		if (r[a]) continue;
		r[a] = !0;
		let o = n.nodeAt(a);
		if (!o) throw RangeError(`No cell with offset ${a} found`);
		let s = null, c = o.attrs;
		for (let n = 0; n < c.colspan; n++) {
			let r = t[(i + n) % e.width * 2];
			r != null && (!c.colwidth || c.colwidth[n] != r) && ((s || (s = WO(c)))[n] = r);
		}
		s && e.problems.unshift({
			type: "colwidth mismatch",
			pos: a,
			colwidth: s
		});
	}
}
function WO(e) {
	if (e.colwidth) return e.colwidth.slice();
	let t = [];
	for (let n = 0; n < e.colspan; n++) t.push(0);
	return t;
}
function GO(e) {
	let t = e.cached.tableNodeTypes;
	if (!t) {
		t = e.cached.tableNodeTypes = {};
		for (let n in e.nodes) {
			let r = e.nodes[n], i = r.spec.tableRole;
			i && (t[i] = r);
		}
	}
	return t;
}
var KO = new D("selectingCells");
function qO(e) {
	for (let t = e.depth - 1; t > 0; t--) if (e.node(t).type.spec.tableRole == "row") return e.node(0).resolve(e.before(t + 1));
	return null;
}
function JO(e) {
	for (let t = e.depth; t > 0; t--) {
		let n = e.node(t).type.spec.tableRole;
		if (n === "cell" || n === "header_cell") return e.node(t);
	}
	return null;
}
function YO(e) {
	let t = e.selection.$head;
	for (let e = t.depth; e > 0; e--) if (t.node(e).type.spec.tableRole == "row") return !0;
	return !1;
}
function XO(e) {
	let t = e.selection;
	if ("$anchorCell" in t && t.$anchorCell) return t.$anchorCell.pos > t.$headCell.pos ? t.$anchorCell : t.$headCell;
	if ("node" in t && t.node && t.node.type.spec.tableRole == "cell") return t.$anchor;
	let n = qO(t.$head) || ZO(t.$head);
	if (n) return n;
	throw RangeError(`No cell found around position ${t.head}`);
}
function ZO(e) {
	for (let t = e.nodeAfter, n = e.pos; t; t = t.firstChild, n++) {
		let r = t.type.spec.tableRole;
		if (r == "cell" || r == "header_cell") return e.doc.resolve(n);
	}
	for (let t = e.nodeBefore, n = e.pos; t; t = t.lastChild, n--) {
		let r = t.type.spec.tableRole;
		if (r == "cell" || r == "header_cell") return e.doc.resolve(n - t.nodeSize);
	}
}
function QO(e) {
	return e.parent.type.spec.tableRole == "row" && !!e.nodeAfter;
}
function $O(e) {
	return e.node(0).resolve(e.pos + e.nodeAfter.nodeSize);
}
function ek(e, t) {
	return e.depth == t.depth && e.pos >= t.start(-1) && e.pos <= t.end(-1);
}
function tk(e, t, n) {
	let r = e.node(-1), i = Q.get(r), a = e.start(-1), o = i.nextCell(e.pos - a, t, n);
	return o == null ? null : e.node(0).resolve(a + o);
}
function nk(e, t, n = 1) {
	let r = {
		...e,
		colspan: e.colspan - n
	};
	return r.colwidth && (r.colwidth = r.colwidth.slice(), r.colwidth.splice(t, n), r.colwidth.some((e) => e > 0) || (r.colwidth = null)), r;
}
function rk(e, t, n = 1) {
	let r = {
		...e,
		colspan: e.colspan + n
	};
	if (r.colwidth) {
		r.colwidth = r.colwidth.slice();
		for (let e = 0; e < n; e++) r.colwidth.splice(t, 0, 0);
	}
	return r;
}
function ik(e, t, n) {
	let r = GO(t.type.schema).header_cell;
	for (let i = 0; i < e.height; i++) if (t.nodeAt(e.map[n + i * e.width]).type != r) return !1;
	return !0;
}
var $ = class e extends C {
	constructor(e, t = e) {
		let n = e.node(-1), r = Q.get(n), i = e.start(-1), a = r.rectBetween(e.pos - i, t.pos - i), o = e.node(0), s = r.cellsInRect(a).filter((e) => e != t.pos - i);
		s.unshift(t.pos - i);
		let c = s.map((e) => {
			let t = n.nodeAt(e);
			if (!t) throw RangeError(`No cell with offset ${e} found`);
			let r = i + e + 1;
			return new Cn(o.resolve(r), o.resolve(r + t.content.size));
		});
		super(c[0].$from, c[0].$to, c), this.$anchorCell = e, this.$headCell = t;
	}
	map(t, n) {
		let r = t.resolve(n.map(this.$anchorCell.pos)), i = t.resolve(n.map(this.$headCell.pos));
		if (QO(r) && QO(i) && ek(r, i)) {
			let t = this.$anchorCell.node(-1) != r.node(-1);
			return t && this.isRowSelection() ? e.rowSelection(r, i) : t && this.isColSelection() ? e.colSelection(r, i) : new e(r, i);
		}
		return w.between(r, i);
	}
	content() {
		let e = this.$anchorCell.node(-1), t = Q.get(e), n = this.$anchorCell.start(-1), r = t.rectBetween(this.$anchorCell.pos - n, this.$headCell.pos - n), i = {}, a = [];
		for (let n = r.top; n < r.bottom; n++) {
			let o = [];
			for (let a = n * t.width + r.left, s = r.left; s < r.right; s++, a++) {
				let n = t.map[a];
				if (i[n]) continue;
				i[n] = !0;
				let s = t.findCell(n), c = e.nodeAt(n);
				if (!c) throw RangeError(`No cell with offset ${n} found`);
				let l = r.left - s.left, u = s.right - r.right;
				if (l > 0 || u > 0) {
					let e = c.attrs;
					if (l > 0 && (e = nk(e, 0, l)), u > 0 && (e = nk(e, e.colspan - u, u)), s.left < r.left) {
						if (c = c.type.createAndFill(e), !c) throw RangeError(`Could not create cell with attrs ${JSON.stringify(e)}`);
					} else c = c.type.create(e, c.content);
				}
				if (s.top < r.top || s.bottom > r.bottom) {
					let e = {
						...c.attrs,
						rowspan: Math.min(s.bottom, r.bottom) - Math.max(s.top, r.top)
					};
					c = s.top < r.top ? c.type.createAndFill(e) : c.type.create(e, c.content);
				}
				o.push(c);
			}
			a.push(e.child(n).copy(l.from(o)));
		}
		let o = this.isColSelection() && this.isRowSelection() ? e : a;
		return new h(l.from(o), 1, 1);
	}
	replace(e, t = h.empty) {
		let n = e.steps.length, r = this.ranges;
		for (let i = 0; i < r.length; i++) {
			let { $from: a, $to: o } = r[i], s = e.mapping.slice(n);
			e.replace(s.map(a.pos), s.map(o.pos), i ? h.empty : t);
		}
		let i = C.findFrom(e.doc.resolve(e.mapping.slice(n).map(this.to)), -1);
		i && e.setSelection(i);
	}
	replaceWith(e, t) {
		this.replace(e, new h(l.from(t), 0, 0));
	}
	forEachCell(e) {
		let t = this.$anchorCell.node(-1), n = Q.get(t), r = this.$anchorCell.start(-1), i = n.cellsInRect(n.rectBetween(this.$anchorCell.pos - r, this.$headCell.pos - r));
		for (let n = 0; n < i.length; n++) e(t.nodeAt(i[n]), r + i[n]);
	}
	isColSelection() {
		let e = this.$anchorCell.index(-1), t = this.$headCell.index(-1);
		if (Math.min(e, t) > 0) return !1;
		let n = e + this.$anchorCell.nodeAfter.attrs.rowspan, r = t + this.$headCell.nodeAfter.attrs.rowspan;
		return Math.max(n, r) == this.$headCell.node(-1).childCount;
	}
	static colSelection(t, n = t) {
		let r = t.node(-1), i = Q.get(r), a = t.start(-1), o = i.findCell(t.pos - a), s = i.findCell(n.pos - a), c = t.node(0);
		return o.top <= s.top ? (o.top > 0 && (t = c.resolve(a + i.map[o.left])), s.bottom < i.height && (n = c.resolve(a + i.map[i.width * (i.height - 1) + s.right - 1]))) : (s.top > 0 && (n = c.resolve(a + i.map[s.left])), o.bottom < i.height && (t = c.resolve(a + i.map[i.width * (i.height - 1) + o.right - 1]))), new e(t, n);
	}
	isRowSelection() {
		let e = this.$anchorCell.node(-1), t = Q.get(e), n = this.$anchorCell.start(-1), r = t.colCount(this.$anchorCell.pos - n), i = t.colCount(this.$headCell.pos - n);
		if (Math.min(r, i) > 0) return !1;
		let a = r + this.$anchorCell.nodeAfter.attrs.colspan, o = i + this.$headCell.nodeAfter.attrs.colspan;
		return Math.max(a, o) == t.width;
	}
	eq(t) {
		return t instanceof e && t.$anchorCell.pos == this.$anchorCell.pos && t.$headCell.pos == this.$headCell.pos;
	}
	static rowSelection(t, n = t) {
		let r = t.node(-1), i = Q.get(r), a = t.start(-1), o = i.findCell(t.pos - a), s = i.findCell(n.pos - a), c = t.node(0);
		return o.left <= s.left ? (o.left > 0 && (t = c.resolve(a + i.map[o.top * i.width])), s.right < i.width && (n = c.resolve(a + i.map[i.width * (s.top + 1) - 1]))) : (s.left > 0 && (n = c.resolve(a + i.map[s.top * i.width])), o.right < i.width && (t = c.resolve(a + i.map[i.width * (o.top + 1) - 1]))), new e(t, n);
	}
	toJSON() {
		return {
			type: "cell",
			anchor: this.$anchorCell.pos,
			head: this.$headCell.pos
		};
	}
	static fromJSON(t, n) {
		return new e(t.resolve(n.anchor), t.resolve(n.head));
	}
	static create(t, n, r = n) {
		return new e(t.resolve(n), t.resolve(r));
	}
	getBookmark() {
		return new ak(this.$anchorCell.pos, this.$headCell.pos);
	}
};
$.prototype.visible = !1, C.jsonID("cell", $);
var ak = class e {
	constructor(e, t) {
		this.anchor = e, this.head = t;
	}
	map(t) {
		return new e(t.map(this.anchor), t.map(this.head));
	}
	resolve(e) {
		let t = e.resolve(this.anchor), n = e.resolve(this.head);
		return t.parent.type.spec.tableRole == "row" && n.parent.type.spec.tableRole == "row" && t.index() < t.parent.childCount && n.index() < n.parent.childCount && ek(t, n) ? new $(t, n) : C.near(n, 1);
	}
};
function ok(e) {
	if (!(e.selection instanceof $)) return null;
	let t = [];
	return e.selection.forEachCell((e, n) => {
		t.push(A.node(n, n + e.nodeSize, { class: "selectedCell" }));
	}), j.create(e.doc, t);
}
function sk({ $from: e, $to: t }) {
	if (e.pos == t.pos || e.pos < t.pos - 6) return !1;
	let n = e.pos, r = t.pos, i = e.depth;
	for (; i >= 0 && !(e.after(i + 1) < e.end(i)); i--, n++);
	for (let e = t.depth; e >= 0 && !(t.before(e + 1) > t.start(e)); e--, r--);
	return n == r && /row|table/.test(e.node(i).type.spec.tableRole);
}
function ck({ $from: e, $to: t }) {
	let n, r;
	for (let t = e.depth; t > 0; t--) {
		let r = e.node(t);
		if (r.type.spec.tableRole === "cell" || r.type.spec.tableRole === "header_cell") {
			n = r;
			break;
		}
	}
	for (let e = t.depth; e > 0; e--) {
		let n = t.node(e);
		if (n.type.spec.tableRole === "cell" || n.type.spec.tableRole === "header_cell") {
			r = n;
			break;
		}
	}
	return n !== r && t.parentOffset === 0;
}
function lk(e, t, n) {
	let r = (t || e).selection, i = (t || e).doc, a, o;
	if (r instanceof T && (o = r.node.type.spec.tableRole)) {
		if (o == "cell" || o == "header_cell") a = $.create(i, r.from);
		else if (o == "row") {
			let e = i.resolve(r.from + 1);
			a = $.rowSelection(e, e);
		} else if (!n) {
			let e = Q.get(r.node), t = r.from + 1, n = t + e.map[e.width * e.height - 1];
			a = $.create(i, t + 1, n);
		}
	} else r instanceof w && sk(r) ? a = w.create(i, r.from) : r instanceof w && ck(r) && (a = w.create(i, r.$from.start(), r.$from.end()));
	return a && (t || (t = e.tr)).setSelection(a), t;
}
var uk = new D("fix-tables");
function dk(e, t, n, r) {
	let i = e.childCount, a = t.childCount;
	outer: for (let o = 0, s = 0; o < a; o++) {
		let a = t.child(o);
		for (let t = s, r = Math.min(i, o + 3); t < r; t++) if (e.child(t) == a) {
			s = t + 1, n += a.nodeSize;
			continue outer;
		}
		r(a, n), s < i && e.child(s).sameMarkup(a) ? dk(e.child(s), a, n + 1, r) : a.nodesBetween(0, a.content.size, r, n + 1), n += a.nodeSize;
	}
}
function fk(e, t) {
	let n, r = (t, r) => {
		t.type.spec.tableRole == "table" && (n = pk(e, t, r, n));
	};
	return t ? t.doc != e.doc && dk(t.doc, e.doc, 0, r) : e.doc.descendants(r), n;
}
function pk(e, t, n, r) {
	let i = Q.get(t);
	if (!i.problems) return r;
	r || (r = e.tr);
	let a = [];
	for (let e = 0; e < i.height; e++) a.push(0);
	for (let e = 0; e < i.problems.length; e++) {
		let o = i.problems[e];
		if (o.type == "collision") {
			let e = t.nodeAt(o.pos);
			if (!e) continue;
			let i = e.attrs;
			for (let e = 0; e < i.rowspan; e++) a[o.row + e] += o.n;
			r.setNodeMarkup(r.mapping.map(n + 1 + o.pos), null, nk(i, i.colspan - o.n, o.n));
		} else if (o.type == "missing") a[o.row] += o.n;
		else if (o.type == "overlong_rowspan") {
			let e = t.nodeAt(o.pos);
			if (!e) continue;
			r.setNodeMarkup(r.mapping.map(n + 1 + o.pos), null, {
				...e.attrs,
				rowspan: e.attrs.rowspan - o.n
			});
		} else if (o.type == "colwidth mismatch") {
			let e = t.nodeAt(o.pos);
			if (!e) continue;
			r.setNodeMarkup(r.mapping.map(n + 1 + o.pos), null, {
				...e.attrs,
				colwidth: o.colwidth
			});
		} else if (o.type == "zero_sized") {
			let e = r.mapping.map(n);
			r.delete(e, e + t.nodeSize);
		}
	}
	let o, s;
	for (let e = 0; e < a.length; e++) a[e] && (o ?? (o = e), s = e);
	for (let c = 0, l = n + 1; c < i.height; c++) {
		let n = t.child(c), i = l + n.nodeSize, u = a[c];
		if (u > 0) {
			let t = "cell";
			n.firstChild && (t = n.firstChild.type.spec.tableRole);
			let a = [];
			for (let n = 0; n < u; n++) {
				let n = GO(e.schema)[t].createAndFill();
				n && a.push(n);
			}
			let d = (c == 0 || o == c - 1) && s == c ? l + 1 : i - 1;
			r.insert(r.mapping.map(d), a);
		}
		l = i;
	}
	return r.setMeta(uk, { fixTables: !0 });
}
function mk(e) {
	let t = e.selection, n = XO(e), r = n.node(-1), i = n.start(-1), a = Q.get(r);
	return {
		...t instanceof $ ? a.rectBetween(t.$anchorCell.pos - i, t.$headCell.pos - i) : a.findCell(n.pos - i),
		tableStart: i,
		map: a,
		table: r
	};
}
function hk(e, { map: t, tableStart: n, table: r }, i) {
	let a = i > 0 ? -1 : 0;
	ik(t, r, i + a) && (a = i == 0 || i == t.width ? null : 0);
	for (let o = 0; o < t.height; o++) {
		let s = o * t.width + i;
		if (i > 0 && i < t.width && t.map[s - 1] == t.map[s]) {
			let a = t.map[s], c = r.nodeAt(a);
			e.setNodeMarkup(e.mapping.map(n + a), null, rk(c.attrs, i - t.colCount(a))), o += c.attrs.rowspan - 1;
		} else {
			let c = a == null ? GO(r.type.schema).cell : r.nodeAt(t.map[s + a]).type, l = t.positionAt(o, i, r);
			e.insert(e.mapping.map(n + l), c.createAndFill());
		}
	}
	return e;
}
function gk(e, t) {
	if (!YO(e)) return !1;
	if (t) {
		let n = mk(e);
		t(hk(e.tr, n, n.left));
	}
	return !0;
}
function _k(e, t) {
	if (!YO(e)) return !1;
	if (t) {
		let n = mk(e);
		t(hk(e.tr, n, n.right));
	}
	return !0;
}
function vk(e, { map: t, table: n, tableStart: r }, i) {
	let a = e.mapping.maps.length;
	for (let o = 0; o < t.height;) {
		let s = o * t.width + i, c = t.map[s], l = n.nodeAt(c), u = l.attrs;
		if (i > 0 && t.map[s - 1] == c || i < t.width - 1 && t.map[s + 1] == c) e.setNodeMarkup(e.mapping.slice(a).map(r + c), null, nk(u, i - t.colCount(c)));
		else {
			let t = e.mapping.slice(a).map(r + c);
			e.delete(t, t + l.nodeSize);
		}
		o += u.rowspan;
	}
}
function yk(e, t) {
	if (!YO(e)) return !1;
	if (t) {
		let n = mk(e), r = e.tr;
		if (n.left == 0 && n.right == n.map.width) return !1;
		for (let e = n.right - 1; vk(r, n, e), e != n.left; e--) {
			let e = n.tableStart ? r.doc.nodeAt(n.tableStart - 1) : r.doc;
			if (!e) throw RangeError("No table found");
			n.table = e, n.map = Q.get(e);
		}
		t(r);
	}
	return !0;
}
function bk(e, t, n) {
	let r = GO(t.type.schema).header_cell;
	for (let i = 0; i < e.width; i++) if (t.nodeAt(e.map[i + n * e.width])?.type != r) return !1;
	return !0;
}
function xk(e, { map: t, tableStart: n, table: r }, i) {
	let a = n;
	for (let e = 0; e < i; e++) a += r.child(e).nodeSize;
	let o = [], s = i > 0 ? -1 : 0;
	bk(t, r, i + s) && (s = i == 0 || i == t.height ? null : 0);
	for (let a = 0, c = t.width * i; a < t.width; a++, c++) if (i > 0 && i < t.height && t.map[c] == t.map[c - t.width]) {
		let i = t.map[c], o = r.nodeAt(i).attrs;
		e.setNodeMarkup(n + i, null, {
			...o,
			rowspan: o.rowspan + 1
		}), a += o.colspan - 1;
	} else {
		let e = (s == null ? GO(r.type.schema).cell : r.nodeAt(t.map[c + s * t.width])?.type)?.createAndFill();
		e && o.push(e);
	}
	return e.insert(a, GO(r.type.schema).row.create(null, o)), e;
}
function Sk(e, t) {
	if (!YO(e)) return !1;
	if (t) {
		let n = mk(e);
		t(xk(e.tr, n, n.top));
	}
	return !0;
}
function Ck(e, t) {
	if (!YO(e)) return !1;
	if (t) {
		let n = mk(e);
		t(xk(e.tr, n, n.bottom));
	}
	return !0;
}
function wk(e, { map: t, table: n, tableStart: r }, i) {
	let a = 0;
	for (let e = 0; e < i; e++) a += n.child(e).nodeSize;
	let o = a + n.child(i).nodeSize, s = e.mapping.maps.length;
	e.delete(a + r, o + r);
	let c = /* @__PURE__ */ new Set();
	for (let a = 0, o = i * t.width; a < t.width; a++, o++) {
		let l = t.map[o];
		if (!c.has(l)) {
			if (c.add(l), i > 0 && l == t.map[o - t.width]) {
				let t = n.nodeAt(l).attrs;
				e.setNodeMarkup(e.mapping.slice(s).map(l + r), null, {
					...t,
					rowspan: t.rowspan - 1
				}), a += t.colspan - 1;
			} else if (i < t.height && l == t.map[o + t.width]) {
				let o = n.nodeAt(l), c = o.attrs, u = o.type.create({
					...c,
					rowspan: o.attrs.rowspan - 1
				}, o.content), d = t.positionAt(i + 1, a, n);
				e.insert(e.mapping.slice(s).map(r + d), u), a += c.colspan - 1;
			}
		}
	}
}
function Tk(e, t) {
	if (!YO(e)) return !1;
	if (t) {
		let n = mk(e), r = e.tr;
		if (n.top == 0 && n.bottom == n.map.height) return !1;
		for (let e = n.bottom - 1; wk(r, n, e), e != n.top; e--) {
			let e = n.tableStart ? r.doc.nodeAt(n.tableStart - 1) : r.doc;
			if (!e) throw RangeError("No table found");
			n.table = e, n.map = Q.get(n.table);
		}
		t(r);
	}
	return !0;
}
function Ek(e) {
	let t = e.content;
	return t.childCount == 1 && t.child(0).isTextblock && t.child(0).childCount == 0;
}
function Dk({ width: e, height: t, map: n }, r) {
	let i = r.top * e + r.left, a = i, o = (r.bottom - 1) * e + r.left, s = i + (r.right - r.left - 1);
	for (let t = r.top; t < r.bottom; t++) {
		if (r.left > 0 && n[a] == n[a - 1] || r.right < e && n[s] == n[s + 1]) return !0;
		a += e, s += e;
	}
	for (let a = r.left; a < r.right; a++) {
		if (r.top > 0 && n[i] == n[i - e] || r.bottom < t && n[o] == n[o + e]) return !0;
		i++, o++;
	}
	return !1;
}
function Ok(e, t) {
	let n = e.selection;
	if (!(n instanceof $) || n.$anchorCell.pos == n.$headCell.pos) return !1;
	let r = mk(e), { map: i } = r;
	if (Dk(i, r)) return !1;
	if (t) {
		let n = e.tr, a = {}, o = l.empty, s, c;
		for (let e = r.top; e < r.bottom; e++) for (let t = r.left; t < r.right; t++) {
			let l = i.map[e * i.width + t], u = r.table.nodeAt(l);
			if (!a[l] && u) {
				if (a[l] = !0, s == null) s = l, c = u;
				else {
					Ek(u) || (o = o.append(u.content));
					let e = n.mapping.map(l + r.tableStart);
					n.delete(e, e + u.nodeSize);
				}
			}
		}
		if (s == null || c == null) return !0;
		if (n.setNodeMarkup(s + r.tableStart, null, {
			...rk(c.attrs, c.attrs.colspan, r.right - r.left - c.attrs.colspan),
			rowspan: r.bottom - r.top
		}), o.size > 0) {
			let e = s + 1 + c.content.size, t = Ek(c) ? s + 1 : e;
			n.replaceWith(t + r.tableStart, e + r.tableStart, o);
		}
		n.setSelection(new $(n.doc.resolve(s + r.tableStart))), t(n);
	}
	return !0;
}
function kk(e, t) {
	let n = GO(e.schema);
	return Ak(({ node: e }) => n[e.type.spec.tableRole])(e, t);
}
function Ak(e) {
	return (t, n) => {
		let r = t.selection, i, a;
		if (r instanceof $) {
			if (r.$anchorCell.pos != r.$headCell.pos) return !1;
			i = r.$anchorCell.nodeAfter, a = r.$anchorCell.pos;
		} else {
			if (i = JO(r.$from), !i) return !1;
			a = qO(r.$from)?.pos;
		}
		if (i == null || a == null || i.attrs.colspan == 1 && i.attrs.rowspan == 1) return !1;
		if (n) {
			let o = i.attrs, s = [], c = o.colwidth;
			o.rowspan > 1 && (o = {
				...o,
				rowspan: 1
			}), o.colspan > 1 && (o = {
				...o,
				colspan: 1
			});
			let l = mk(t), u = t.tr;
			for (let e = 0; e < l.right - l.left; e++) s.push(c ? {
				...o,
				colwidth: c && c[e] ? [c[e]] : null
			} : o);
			let d;
			for (let t = l.top; t < l.bottom; t++) {
				let n = l.map.positionAt(t, l.left, l.table);
				t == l.top && (n += i.nodeSize);
				for (let r = l.left, a = 0; r < l.right; r++, a++) (r != l.left || t != l.top) && u.insert(d = u.mapping.map(n + l.tableStart, 1), e({
					node: i,
					row: t,
					col: r
				}).createAndFill(s[a]));
			}
			u.setNodeMarkup(a, e({
				node: i,
				row: l.top,
				col: l.left
			}), s[0]), r instanceof $ && u.setSelection(new $(u.doc.resolve(r.$anchorCell.pos), d ? u.doc.resolve(d) : void 0)), n(u);
		}
		return !0;
	};
}
function jk(e, t) {
	return function(n, r) {
		if (!YO(n)) return !1;
		let i = XO(n);
		if (i.nodeAfter.attrs[e] === t) return !1;
		if (r) {
			let a = n.tr;
			n.selection instanceof $ ? n.selection.forEachCell((n, r) => {
				n.attrs[e] !== t && a.setNodeMarkup(r, null, {
					...n.attrs,
					[e]: t
				});
			}) : a.setNodeMarkup(i.pos, null, {
				...i.nodeAfter.attrs,
				[e]: t
			}), r(a);
		}
		return !0;
	};
}
function Mk(e) {
	return function(t, n) {
		if (!YO(t)) return !1;
		if (n) {
			let r = GO(t.schema), i = mk(t), a = t.tr, o = i.map.cellsInRect(e == "column" ? {
				left: i.left,
				top: 0,
				right: i.right,
				bottom: i.map.height
			} : e == "row" ? {
				left: 0,
				top: i.top,
				right: i.map.width,
				bottom: i.bottom
			} : i), s = o.map((e) => i.table.nodeAt(e));
			for (let e = 0; e < o.length; e++) s[e].type == r.header_cell && a.setNodeMarkup(i.tableStart + o[e], r.cell, s[e].attrs);
			if (a.steps.length === 0) for (let e = 0; e < o.length; e++) a.setNodeMarkup(i.tableStart + o[e], r.header_cell, s[e].attrs);
			n(a);
		}
		return !0;
	};
}
function Nk(e, t, n) {
	let r = t.map.cellsInRect({
		left: 0,
		top: 0,
		right: e == "row" ? t.map.width : 1,
		bottom: e == "column" ? t.map.height : 1
	});
	for (let e = 0; e < r.length; e++) {
		let i = t.table.nodeAt(r[e]);
		if (i && i.type !== n.header_cell) return !1;
	}
	return !0;
}
function Pk(e, t) {
	return t = t || { useDeprecatedLogic: !1 }, t.useDeprecatedLogic ? Mk(e) : function(t, n) {
		if (!YO(t)) return !1;
		if (n) {
			let r = GO(t.schema), i = mk(t), a = t.tr, o = Nk("row", i, r), s = Nk("column", i, r), c = (e === "column" ? o : e === "row" && s) ? 1 : 0, l = e == "column" ? {
				left: 0,
				top: c,
				right: 1,
				bottom: i.map.height
			} : e == "row" ? {
				left: c,
				top: 0,
				right: i.map.width,
				bottom: 1
			} : i, u = e == "column" ? s ? r.cell : r.header_cell : e == "row" ? o ? r.cell : r.header_cell : r.cell;
			i.map.cellsInRect(l).forEach((e) => {
				let t = e + i.tableStart, n = a.doc.nodeAt(t);
				n && a.setNodeMarkup(t, u, n.attrs);
			}), n(a);
		}
		return !0;
	};
}
Pk("row", { useDeprecatedLogic: !0 }), Pk("column", { useDeprecatedLogic: !0 });
var Fk = Pk("cell", { useDeprecatedLogic: !0 });
function Ik(e, t) {
	if (t < 0) {
		let t = e.nodeBefore;
		if (t) return e.pos - t.nodeSize;
		for (let t = e.index(-1) - 1, n = e.before(); t >= 0; t--) {
			let r = e.node(-1).child(t), i = r.lastChild;
			if (i) return n - 1 - i.nodeSize;
			n -= r.nodeSize;
		}
	} else {
		if (e.index() < e.parent.childCount - 1) return e.pos + e.nodeAfter.nodeSize;
		let t = e.node(-1);
		for (let n = e.indexAfter(-1), r = e.after(); n < t.childCount; n++) {
			let e = t.child(n);
			if (e.childCount) return r + 1;
			r += e.nodeSize;
		}
	}
	return null;
}
function Lk(e) {
	return function(t, n) {
		if (!YO(t)) return !1;
		let r = Ik(XO(t), e);
		if (r == null) return !1;
		if (n) {
			let e = t.doc.resolve(r);
			n(t.tr.setSelection(w.between(e, $O(e))).scrollIntoView());
		}
		return !0;
	};
}
function Rk(e, t) {
	let n = e.selection.$anchor;
	for (let r = n.depth; r > 0; r--) if (n.node(r).type.spec.tableRole == "table") return t && t(e.tr.delete(n.before(r), n.after(r)).scrollIntoView()), !0;
	return !1;
}
function zk(e, t) {
	let n = e.selection;
	if (!(n instanceof $)) return !1;
	if (t) {
		let r = e.tr, i = GO(e.schema).cell.createAndFill().content;
		n.forEachCell((e, t) => {
			e.content.eq(i) || r.replace(r.mapping.map(t + 1), r.mapping.map(t + e.nodeSize - 1), new h(i, 0, 0));
		}), r.docChanged && t(r);
	}
	return !0;
}
function Bk(e) {
	if (e.size === 0) return null;
	let { content: t, openStart: n, openEnd: r } = e;
	for (; t.childCount == 1 && (n > 0 && r > 0 || t.child(0).type.spec.tableRole == "table");) n--, r--, t = t.child(0).content;
	let i = t.child(0), a = i.type.spec.tableRole, o = i.type.schema, s = [];
	if (a == "row") for (let e = 0; e < t.childCount; e++) {
		let i = t.child(e).content, a = e ? 0 : Math.max(0, n - 1), c = e < t.childCount - 1 ? 0 : Math.max(0, r - 1);
		(a || c) && (i = Hk(GO(o).row, new h(i, a, c)).content), s.push(i);
	}
	else if (a == "cell" || a == "header_cell") s.push(n || r ? Hk(GO(o).row, new h(t, n, r)).content : t);
	else return null;
	return Vk(o, s);
}
function Vk(e, t) {
	let n = [];
	for (let e = 0; e < t.length; e++) {
		let r = t[e];
		for (let t = r.childCount - 1; t >= 0; t--) {
			let { rowspan: i, colspan: a } = r.child(t).attrs;
			for (let t = e; t < e + i; t++) n[t] = (n[t] || 0) + a;
		}
	}
	let r = 0;
	for (let e = 0; e < n.length; e++) r = Math.max(r, n[e]);
	for (let i = 0; i < n.length; i++) if (i >= t.length && t.push(l.empty), n[i] < r) {
		let a = GO(e).cell.createAndFill(), o = [];
		for (let e = n[i]; e < r; e++) o.push(a);
		t[i] = t[i].append(l.from(o));
	}
	return {
		height: t.length,
		width: r,
		rows: t
	};
}
function Hk(e, t) {
	let n = e.createAndFill();
	return new xn(n).replace(0, n.content.size, t).doc;
}
function Uk({ width: e, height: t, rows: n }, r, i) {
	if (e != r) {
		let t = [], i = [];
		for (let e = 0; e < n.length; e++) {
			let a = n[e], o = [];
			for (let n = t[e] || 0, i = 0; n < r; i++) {
				let s = a.child(i % a.childCount);
				n + s.attrs.colspan > r && (s = s.type.createChecked(nk(s.attrs, s.attrs.colspan, n + s.attrs.colspan - r), s.content)), o.push(s), n += s.attrs.colspan;
				for (let n = 1; n < s.attrs.rowspan; n++) t[e + n] = (t[e + n] || 0) + s.attrs.colspan;
			}
			i.push(l.from(o));
		}
		n = i, e = r;
	}
	if (t != i) {
		let e = [];
		for (let r = 0, a = 0; r < i; r++, a++) {
			let o = [], s = n[a % t];
			for (let e = 0; e < s.childCount; e++) {
				let t = s.child(e);
				r + t.attrs.rowspan > i && (t = t.type.create({
					...t.attrs,
					rowspan: Math.max(1, i - t.attrs.rowspan)
				}, t.content)), o.push(t);
			}
			e.push(l.from(o));
		}
		n = e, t = i;
	}
	return {
		width: e,
		height: t,
		rows: n
	};
}
function Wk(e, t, n, r, i, a, o) {
	let s = e.doc.type.schema, c = GO(s), u, d;
	if (i > t.width) for (let a = 0, s = 0; a < t.height; a++) {
		let l = n.child(a);
		s += l.nodeSize;
		let f = [], p;
		p = l.lastChild == null || l.lastChild.type == c.cell ? u || (u = c.cell.createAndFill()) : d || (d = c.header_cell.createAndFill());
		for (let e = t.width; e < i; e++) f.push(p);
		e.insert(e.mapping.slice(o).map(s - 1 + r), f);
	}
	if (a > t.height) {
		let s = [];
		for (let e = 0, r = (t.height - 1) * t.width; e < Math.max(t.width, i); e++) {
			let i = e >= t.width ? !1 : n.nodeAt(t.map[r + e]).type == c.header_cell;
			s.push(i ? d || (d = c.header_cell.createAndFill()) : u || (u = c.cell.createAndFill()));
		}
		let f = c.row.create(null, l.from(s)), p = [];
		for (let e = t.height; e < a; e++) p.push(f);
		e.insert(e.mapping.slice(o).map(r + n.nodeSize - 2), p);
	}
	return !!(u || d);
}
function Gk(e, t, n, r, i, a, o, s) {
	if (o == 0 || o == t.height) return !1;
	let c = !1;
	for (let l = i; l < a; l++) {
		let i = o * t.width + l, a = t.map[i];
		if (t.map[i - t.width] == a) {
			c = !0;
			let i = n.nodeAt(a), { top: u, left: d } = t.findCell(a);
			e.setNodeMarkup(e.mapping.slice(s).map(a + r), null, {
				...i.attrs,
				rowspan: o - u
			}), e.insert(e.mapping.slice(s).map(t.positionAt(o, d, n)), i.type.createAndFill({
				...i.attrs,
				rowspan: u + i.attrs.rowspan - o
			})), l += i.attrs.colspan - 1;
		}
	}
	return c;
}
function Kk(e, t, n, r, i, a, o, s) {
	if (o == 0 || o == t.width) return !1;
	let c = !1;
	for (let l = i; l < a; l++) {
		let i = l * t.width + o, a = t.map[i];
		if (t.map[i - 1] == a) {
			c = !0;
			let i = n.nodeAt(a), u = t.colCount(a), d = e.mapping.slice(s).map(a + r);
			e.setNodeMarkup(d, null, nk(i.attrs, o - u, i.attrs.colspan - (o - u))), e.insert(d + i.nodeSize, i.type.createAndFill(nk(i.attrs, 0, o - u))), l += i.attrs.rowspan - 1;
		}
	}
	return c;
}
function qk(e, t, n, r, i) {
	let a = n ? e.doc.nodeAt(n - 1) : e.doc;
	if (!a) throw Error("No table found");
	let o = Q.get(a), { top: s, left: c } = r, l = c + i.width, u = s + i.height, d = e.tr, f = 0;
	function p() {
		if (a = n ? d.doc.nodeAt(n - 1) : d.doc, !a) throw Error("No table found");
		o = Q.get(a), f = d.mapping.maps.length;
	}
	Wk(d, o, a, n, l, u, f) && p(), Gk(d, o, a, n, c, l, s, f) && p(), Gk(d, o, a, n, c, l, u, f) && p(), Kk(d, o, a, n, s, u, c, f) && p(), Kk(d, o, a, n, s, u, l, f) && p();
	for (let e = s; e < u; e++) {
		let t = o.positionAt(e, c, a), r = o.positionAt(e, l, a);
		d.replace(d.mapping.slice(f).map(t + n), d.mapping.slice(f).map(r + n), new h(i.rows[e - s], 0, 0));
	}
	p(), d.setSelection(new $(d.doc.resolve(n + o.positionAt(s, c, a)), d.doc.resolve(n + o.positionAt(u - 1, l - 1, a)))), t(d);
}
var Jk = _c({
	ArrowLeft: Xk("horiz", -1),
	ArrowRight: Xk("horiz", 1),
	ArrowUp: Xk("vert", -1),
	ArrowDown: Xk("vert", 1),
	"Shift-ArrowLeft": Zk("horiz", -1),
	"Shift-ArrowRight": Zk("horiz", 1),
	"Shift-ArrowUp": Zk("vert", -1),
	"Shift-ArrowDown": Zk("vert", 1),
	Backspace: zk,
	"Mod-Backspace": zk,
	Delete: zk,
	"Mod-Delete": zk
});
function Yk(e, t, n) {
	return !n.eq(e.selection) && (t && t(e.tr.setSelection(n).scrollIntoView()), !0);
}
function Xk(e, t) {
	return (n, r, i) => {
		if (!i) return !1;
		let a = n.selection;
		if (a instanceof $) return Yk(n, r, C.near(a.$headCell, t));
		if (e != "horiz" && !a.empty) return !1;
		let o = tA(i, e, t);
		if (o == null) return !1;
		if (e == "horiz") return Yk(n, r, C.near(n.doc.resolve(a.head + t), t));
		{
			let i = n.doc.resolve(o), a = tk(i, e, t), s;
			return s = a ? C.near(a, 1) : t < 0 ? C.near(n.doc.resolve(i.before(-1)), -1) : C.near(n.doc.resolve(i.after(-1)), 1), Yk(n, r, s);
		}
	};
}
function Zk(e, t) {
	return (n, r, i) => {
		if (!i) return !1;
		let a = n.selection, o;
		if (a instanceof $) o = a;
		else {
			let r = tA(i, e, t);
			if (r == null) return !1;
			o = new $(n.doc.resolve(r));
		}
		let s = tk(o.$headCell, e, t);
		return s ? Yk(n, r, new $(o.$anchorCell, s)) : !1;
	};
}
function Qk(e, t) {
	let n = e.state.doc, r = qO(n.resolve(t));
	return r ? (e.dispatch(e.state.tr.setSelection(new $(r))), !0) : !1;
}
function $k(e, t, n) {
	if (!YO(e.state)) return !1;
	let r = Bk(n), i = e.state.selection;
	if (i instanceof $) {
		r || (r = {
			width: 1,
			height: 1,
			rows: [l.from(Hk(GO(e.state.schema).cell, n))]
		});
		let t = i.$anchorCell.node(-1), a = i.$anchorCell.start(-1), o = Q.get(t).rectBetween(i.$anchorCell.pos - a, i.$headCell.pos - a);
		return r = Uk(r, o.right - o.left, o.bottom - o.top), qk(e.state, e.dispatch, a, o, r), !0;
	}
	if (r) {
		let t = XO(e.state), n = t.start(-1);
		return qk(e.state, e.dispatch, n, Q.get(t.node(-1)).findCell(t.pos - n), r), !0;
	}
	return !1;
}
function eA(e, t) {
	if (t.button != 0 || t.ctrlKey || t.metaKey) return;
	let n = nA(e, t.target), r;
	if (t.shiftKey && e.state.selection instanceof $) i(e.state.selection.$anchorCell, t), t.preventDefault();
	else if (t.shiftKey && n && (r = qO(e.state.selection.$anchor)) != null && rA(e, t)?.pos != r.pos) i(r, t), t.preventDefault();
	else if (!n) return;
	function i(t, n) {
		let r = rA(e, n), i = KO.getState(e.state) == null;
		if (!r || !ek(t, r)) {
			if (i) r = t;
			else return;
		}
		let a = new $(t, r);
		if (i || !e.state.selection.eq(a)) {
			let n = e.state.tr.setSelection(a);
			i && n.setMeta(KO, t.pos), e.dispatch(n);
		}
	}
	function a() {
		e.root.removeEventListener("mouseup", a), e.root.removeEventListener("dragstart", a), e.root.removeEventListener("mousemove", o), KO.getState(e.state) != null && e.dispatch(e.state.tr.setMeta(KO, -1));
	}
	function o(r) {
		let o = r, s = KO.getState(e.state), c;
		if (s != null) c = e.state.doc.resolve(s);
		else if (nA(e, o.target) != n && (c = rA(e, t), !c)) return a();
		c && i(c, o);
	}
	e.root.addEventListener("mouseup", a), e.root.addEventListener("dragstart", a), e.root.addEventListener("mousemove", o);
}
function tA(e, t, n) {
	if (!(e.state.selection instanceof w)) return null;
	let { $head: r } = e.state.selection;
	for (let i = r.depth - 1; i >= 0; i--) {
		let a = r.node(i);
		if ((n < 0 ? r.index(i) : r.indexAfter(i)) != (n < 0 ? 0 : a.childCount)) return null;
		if (a.type.spec.tableRole == "cell" || a.type.spec.tableRole == "header_cell") {
			let a = r.before(i), o = t == "vert" ? n > 0 ? "down" : "up" : n > 0 ? "right" : "left";
			return e.endOfTextblock(o) ? a : null;
		}
	}
	return null;
}
function nA(e, t) {
	for (; t && t != e.dom; t = t.parentNode) if (t.nodeName == "TD" || t.nodeName == "TH") return t;
	return null;
}
function rA(e, t) {
	let n = e.posAtCoords({
		left: t.clientX,
		top: t.clientY
	});
	if (!n) return null;
	let { inside: r, pos: i } = n;
	return r >= 0 && qO(e.state.doc.resolve(r)) || qO(e.state.doc.resolve(i));
}
var iA = class {
	constructor(e, t) {
		this.node = e, this.defaultCellMinWidth = t, this.dom = document.createElement("div"), this.dom.className = "tableWrapper", this.table = this.dom.appendChild(document.createElement("table")), this.table.style.setProperty("--default-cell-min-width", `${t}px`), this.colgroup = this.table.appendChild(document.createElement("colgroup")), aA(e, this.colgroup, this.table, t), this.contentDOM = this.table.appendChild(document.createElement("tbody"));
	}
	update(e) {
		return e.type == this.node.type && (this.node = e, aA(e, this.colgroup, this.table, this.defaultCellMinWidth), !0);
	}
	ignoreMutation(e) {
		return e.type == "attributes" && (e.target == this.table || this.colgroup.contains(e.target));
	}
};
function aA(e, t, n, r, i, a) {
	let o = 0, s = !0, c = t.firstChild, l = e.firstChild;
	if (l) {
		for (let e = 0, n = 0; e < l.childCount; e++) {
			let { colspan: u, colwidth: d } = l.child(e).attrs;
			for (let e = 0; e < u; e++, n++) {
				let l = i == n ? a : d && d[e], u = l ? l + "px" : "";
				if (o += l || r, l || (s = !1), c) c.style.width != u && (c.style.width = u), c = c.nextSibling;
				else {
					let e = document.createElement("col");
					e.style.width = u, t.appendChild(e);
				}
			}
		}
		for (; c;) {
			var u;
			let e = c.nextSibling;
			(u = c.parentNode) == null || u.removeChild(c), c = e;
		}
		s ? (n.style.width = o + "px", n.style.minWidth = "") : (n.style.width = "", n.style.minWidth = o + "px");
	}
}
var oA = new D("tableColumnResizing");
function sA({ handleWidth: e = 5, cellMinWidth: t = 25, defaultCellMinWidth: n = 100, View: r = iA, lastColumnResizable: i = !0 } = {}) {
	let a = new E({
		key: oA,
		state: {
			init(e, t) {
				var i;
				let o = (i = a.spec) == null || (i = i.props) == null ? void 0 : i.nodeViews, s = GO(t.schema).table.name;
				return r && o && (o[s] = (e, t) => new r(e, n, t)), new cA(-1, !1);
			},
			apply(e, t) {
				return t.apply(e);
			}
		},
		props: {
			attributes: (e) => {
				let t = oA.getState(e);
				return t && t.activeHandle > -1 ? { class: "resize-cursor" } : {};
			},
			handleDOMEvents: {
				mousemove: (t, n) => {
					lA(t, n, e, i);
				},
				mouseleave: (e) => {
					uA(e);
				},
				mousedown: (e, r) => {
					dA(e, r, t, n);
				}
			},
			decorations: (e) => {
				let t = oA.getState(e);
				if (t && t.activeHandle > -1) return bA(e, t.activeHandle);
			},
			nodeViews: {}
		}
	});
	return a;
}
var cA = class e {
	constructor(e, t) {
		this.activeHandle = e, this.dragging = t;
	}
	apply(t) {
		let n = this, r = t.getMeta(oA);
		if (r && r.setHandle != null) return new e(r.setHandle, !1);
		if (r && r.setDragging !== void 0) return new e(n.activeHandle, r.setDragging);
		if (n.activeHandle > -1 && t.docChanged) {
			let r = t.mapping.map(n.activeHandle, -1);
			return QO(t.doc.resolve(r)) || (r = -1), new e(r, n.dragging);
		}
		return n;
	}
};
function lA(e, t, n, r) {
	if (!e.editable) return;
	let i = oA.getState(e.state);
	if (i && !i.dragging) {
		let a = pA(t.target), o = -1;
		if (a) {
			let { left: r, right: i } = a.getBoundingClientRect();
			t.clientX - r <= n ? o = mA(e, t, "left", n) : i - t.clientX <= n && (o = mA(e, t, "right", n));
		}
		if (o != i.activeHandle) {
			if (!r && o !== -1) {
				let t = e.state.doc.resolve(o), n = t.node(-1), r = Q.get(n), i = t.start(-1);
				if (r.colCount(t.pos - i) + t.nodeAfter.attrs.colspan - 1 == r.width - 1) return;
			}
			gA(e, o);
		}
	}
}
function uA(e) {
	if (!e.editable) return;
	let t = oA.getState(e.state);
	t && t.activeHandle > -1 && !t.dragging && gA(e, -1);
}
function dA(e, t, n, r) {
	if (!e.editable) return !1;
	let i = e.dom.ownerDocument.defaultView ?? window, a = oA.getState(e.state);
	if (!a || a.activeHandle == -1 || a.dragging) return !1;
	let o = e.state.doc.nodeAt(a.activeHandle), s = fA(e, a.activeHandle, o.attrs);
	e.dispatch(e.state.tr.setMeta(oA, { setDragging: {
		startX: t.clientX,
		startWidth: s
	} }));
	function c(t) {
		i.removeEventListener("mouseup", c), i.removeEventListener("mousemove", l);
		let r = oA.getState(e.state);
		r?.dragging && (_A(e, r.activeHandle, hA(r.dragging, t, n)), e.dispatch(e.state.tr.setMeta(oA, { setDragging: null })));
	}
	function l(t) {
		if (!t.which) return c(t);
		let i = oA.getState(e.state);
		if (i && i.dragging) {
			let a = hA(i.dragging, t, n);
			vA(e, i.activeHandle, a, r);
		}
	}
	return vA(e, a.activeHandle, s, r), i.addEventListener("mouseup", c), i.addEventListener("mousemove", l), t.preventDefault(), !0;
}
function fA(e, t, { colspan: n, colwidth: r }) {
	let i = r && r[r.length - 1];
	if (i) return i;
	let a = e.domAtPos(t), o = a.node.childNodes[a.offset].offsetWidth, s = n;
	if (r) for (let e = 0; e < n; e++) r[e] && (o -= r[e], s--);
	return o / s;
}
function pA(e) {
	for (; e && e.nodeName != "TD" && e.nodeName != "TH";) e = e.classList && e.classList.contains("ProseMirror") ? null : e.parentNode;
	return e;
}
function mA(e, t, n, r) {
	let i = n == "right" ? -r : r, a = e.posAtCoords({
		left: t.clientX + i,
		top: t.clientY
	});
	if (!a) return -1;
	let { pos: o } = a, s = qO(e.state.doc.resolve(o));
	if (!s) return -1;
	if (n == "right") return s.pos;
	let c = Q.get(s.node(-1)), l = s.start(-1), u = c.map.indexOf(s.pos - l);
	return u % c.width == 0 ? -1 : l + c.map[u - 1];
}
function hA(e, t, n) {
	let r = t.clientX - e.startX;
	return Math.max(n, e.startWidth + r);
}
function gA(e, t) {
	e.dispatch(e.state.tr.setMeta(oA, { setHandle: t }));
}
function _A(e, t, n) {
	let r = e.state.doc.resolve(t), i = r.node(-1), a = Q.get(i), o = r.start(-1), s = a.colCount(r.pos - o) + r.nodeAfter.attrs.colspan - 1, c = e.state.tr;
	for (let e = 0; e < a.height; e++) {
		let t = e * a.width + s;
		if (e && a.map[t] == a.map[t - a.width]) continue;
		let r = a.map[t], l = i.nodeAt(r).attrs, u = l.colspan == 1 ? 0 : s - a.colCount(r);
		if (l.colwidth && l.colwidth[u] == n) continue;
		let d = l.colwidth ? l.colwidth.slice() : yA(l.colspan);
		d[u] = n, c.setNodeMarkup(o + r, null, {
			...l,
			colwidth: d
		});
	}
	c.docChanged && e.dispatch(c);
}
function vA(e, t, n, r) {
	let i = e.state.doc.resolve(t), a = i.node(-1), o = i.start(-1), s = Q.get(a).colCount(i.pos - o) + i.nodeAfter.attrs.colspan - 1, c = e.domAtPos(i.start(-1)).node;
	for (; c && c.nodeName != "TABLE";) c = c.parentNode;
	c && aA(a, c.firstChild, c, r, s, n);
}
function yA(e) {
	return Array(e).fill(0);
}
function bA(e, t) {
	let n = [], r = e.doc.resolve(t), i = r.node(-1);
	if (!i) return j.empty;
	let a = Q.get(i), o = r.start(-1), s = a.colCount(r.pos - o) + r.nodeAfter.attrs.colspan - 1;
	for (let t = 0; t < a.height; t++) {
		let r = s + t * a.width;
		if ((s == a.width - 1 || a.map[r] != a.map[r + 1]) && (t == 0 || a.map[r] != a.map[r - a.width])) {
			let t = a.map[r], s = o + t + i.nodeAt(t).nodeSize - 1, c = document.createElement("div");
			c.className = "column-resize-handle", oA.getState(e)?.dragging && n.push(A.node(o + t, o + t + i.nodeAt(t).nodeSize, { class: "column-resize-dragging" })), n.push(A.widget(s, c));
		}
	}
	return j.create(e.doc, n);
}
function xA({ allowTableNodeSelection: e = !1 } = {}) {
	return new E({
		key: KO,
		state: {
			init() {
				return null;
			},
			apply(e, t) {
				let n = e.getMeta(KO);
				if (n != null) return n == -1 ? null : n;
				if (t == null || !e.docChanged) return t;
				let { deleted: r, pos: i } = e.mapping.mapResult(t);
				return r ? null : i;
			}
		},
		props: {
			decorations: ok,
			handleDOMEvents: { mousedown: eA },
			createSelectionBetween(e) {
				return KO.getState(e.state) == null ? null : e.state.selection;
			},
			handleTripleClick: Qk,
			handleKeyDown: Jk,
			handlePaste: $k
		},
		appendTransaction(t, n, r) {
			return lk(r, fk(r, n), e);
		}
	});
}
//#endregion
//#region node_modules/@tiptap/extension-table/dist/index.js
function SA(e) {
	return e === "left" || e === "right" || e === "center" ? e : null;
}
function CA(e) {
	let t = (e.style.textAlign || "").trim().toLowerCase(), n = (e.getAttribute("align") || "").trim().toLowerCase();
	return SA(t || n);
}
function wA(e) {
	return SA(e?.align);
}
function TA() {
	return {
		default: null,
		parseHTML: (e) => CA(e),
		renderHTML: (e) => e.align ? { style: `text-align: ${e.align}` } : {}
	};
}
function EA(e) {
	let t = e.parentElement, n = e.closest("table");
	if (!t || !n) return null;
	let r = Array.from(t.children).indexOf(e), i = n.querySelectorAll("colgroup > col")[r]?.getAttribute("width");
	return i ? [parseInt(i, 10)] : null;
}
function DA(e) {
	let t = e.getAttribute("colwidth");
	return t ? t.split(",").map((e) => parseInt(e, 10)) : EA(e);
}
var OA = /[ \t\r\n\f]+/g;
function kA(e) {
	return e.children.length > 0 ? !1 : (e.textContent ?? "").replace(OA, "") === "";
}
function AA(e) {
	let t = e.createAndFill();
	if (!t) throw Error(`[tiptap error]: "${e.name}" has no default content to backfill.`);
	return t.content;
}
var jA = Sf.create({
	name: "tableCell",
	addOptions() {
		return { HTMLAttributes: {} };
	},
	content: "block+",
	addAttributes() {
		return {
			colspan: { default: 1 },
			rowspan: { default: 1 },
			colwidth: {
				default: null,
				parseHTML: DA
			},
			align: TA()
		};
	},
	tableRole: "cell",
	isolating: !0,
	parseHTML() {
		return [{
			tag: "td",
			getAttrs: (e) => kA(e) ? {} : !1,
			getContent: (e, t) => AA(t.nodes[this.name])
		}, { tag: "td" }];
	},
	renderHTML({ HTMLAttributes: e }) {
		return [
			"td",
			F(this.options.HTMLAttributes, e),
			0
		];
	}
}), MA = Sf.create({
	name: "tableHeader",
	addOptions() {
		return { HTMLAttributes: {} };
	},
	content: "block+",
	addAttributes() {
		return {
			colspan: { default: 1 },
			rowspan: { default: 1 },
			colwidth: {
				default: null,
				parseHTML: DA
			},
			align: TA()
		};
	},
	tableRole: "header_cell",
	isolating: !0,
	parseHTML() {
		return [{
			tag: "th",
			getAttrs: (e) => kA(e) ? {} : !1,
			getContent: (e, t) => AA(t.nodes[this.name])
		}, { tag: "th" }];
	},
	renderHTML({ HTMLAttributes: e }) {
		return [
			"th",
			F(this.options.HTMLAttributes, e),
			0
		];
	}
}), NA = Sf.create({
	name: "tableRow",
	addOptions() {
		return { HTMLAttributes: {} };
	},
	content: "(tableCell | tableHeader)*",
	tableRole: "row",
	parseHTML() {
		return [{ tag: "tr" }];
	},
	renderHTML({ HTMLAttributes: e }) {
		return [
			"tr",
			F(this.options.HTMLAttributes, e),
			0
		];
	}
});
function PA(e, t) {
	return t ? ["width", `${Math.max(t, e)}px`] : ["min-width", `${e}px`];
}
function FA(e, t, n, r, i, a) {
	let o = 0, s = !0, c = t.firstChild, l = e.firstChild;
	if (l !== null) for (let e = 0, n = 0; e < l.childCount; e += 1) {
		let { colspan: u, colwidth: d } = l.child(e).attrs;
		for (let e = 0; e < u; e += 1, n += 1) {
			let l = i === n ? a : d && d[e], u = l ? `${l}px` : "";
			if (o += l || r, l || (s = !1), c) {
				if (c.style.width !== u) {
					let [e, t] = PA(r, l);
					c.style.setProperty(e, t);
				}
				c = c.nextSibling;
			} else {
				let e = document.createElement("col"), [n, i] = PA(r, l);
				e.style.setProperty(n, i), t.appendChild(e);
			}
		}
	}
	for (; c;) {
		var u;
		let e = c.nextSibling;
		(u = c.parentNode) == null || u.removeChild(c), c = e;
	}
	let d = e.attrs.style && typeof e.attrs.style == "string" && /\bwidth\s*:/i.test(e.attrs.style);
	s && !d ? (n.style.width = `${o}px`, n.style.minWidth = "") : (n.style.width = "", n.style.minWidth = `${o}px`);
}
var IA = class {
	constructor(e, t, n, r = {}) {
		this.node = e, this.cellMinWidth = t, this.dom = document.createElement("div"), this.dom.className = "tableWrapper", this.table = this.dom.appendChild(document.createElement("table"));
		for (let [e, t] of Object.entries(r)) t != null && (e === "style" ? this.table.style.cssText = String(t) : this.table.setAttribute(e, String(t)));
		e.attrs.style && (this.table.style.cssText = e.attrs.style), this.colgroup = this.table.appendChild(document.createElement("colgroup")), FA(e, this.colgroup, this.table, t), this.contentDOM = this.table.appendChild(document.createElement("tbody"));
	}
	update(e) {
		return e.type === this.node.type && (this.node = e, FA(e, this.colgroup, this.table, this.cellMinWidth), !0);
	}
	ignoreMutation(e) {
		let t = e.target, n = this.dom.contains(t), r = this.contentDOM.contains(t);
		return !(!n || r || e.type !== "attributes" && e.type !== "childList" && e.type !== "characterData");
	}
};
function LA(e, t, n, r) {
	let i = 0, a = !0, o = [], s = e.firstChild;
	if (!s) return {};
	for (let e = 0, c = 0; e < s.childCount; e += 1) {
		let { colspan: l, colwidth: u } = s.child(e).attrs;
		for (let e = 0; e < l; e += 1, c += 1) {
			let s = n === c ? r : u && u[e];
			i += s || t, s || (a = !1);
			let [l, d] = PA(t, s);
			o.push(["col", { style: `${l}: ${d}` }]);
		}
	}
	let c = a ? `${i}px` : "", l = a ? "" : `${i}px`;
	return {
		colgroup: [
			"colgroup",
			{},
			...o
		],
		tableWidth: c,
		tableMinWidth: l
	};
}
function RA(e, t) {
	return t ? e.createChecked(null, t) : e.createAndFill();
}
function zA(e) {
	if (e.cached.tableNodeTypes) return e.cached.tableNodeTypes;
	let t = {};
	return Object.keys(e.nodes).forEach((n) => {
		let r = e.nodes[n];
		r.spec.tableRole && (t[r.spec.tableRole] = r);
	}), e.cached.tableNodeTypes = t, t;
}
function BA(e, t, n, r, i) {
	let a = zA(e), o = [], s = [];
	for (let e = 0; e < n; e += 1) {
		let e = RA(a.cell, i);
		if (e && s.push(e), r) {
			let e = RA(a.header_cell, i);
			e && o.push(e);
		}
	}
	let c = [];
	for (let e = 0; e < t; e += 1) c.push(a.row.createChecked(null, r && e === 0 ? o : s));
	return a.table.createChecked(null, c);
}
function VA(e) {
	return e instanceof $;
}
var HA = ({ editor: e }) => {
	let { selection: t } = e.state;
	if (!VA(t)) return !1;
	let n = 0;
	return Rl(t.ranges[0].$from, (e) => e.type.name === "table")?.node.descendants((e) => {
		if (e.type.name === "table") return !1;
		["tableCell", "tableHeader"].includes(e.type.name) && (n += 1);
	}), n === t.ranges.length && (e.commands.deleteTable(), !0);
};
function UA(e, t) {
	let n = e.mapping.map(t);
	if (Rl(e.selection.$from, (e) => e.type.name === "table")?.pos === n) return;
	let r = e.doc.nodeAt(n);
	if (!r) return;
	let i = n + r.nodeSize - 1;
	e.setSelection(w.near(e.doc.resolve(i), -1));
}
function WA(e) {
	let t = "", n = 0;
	for (; n < e.length;) {
		if (e[n] === "\\" && n + 1 < e.length) {
			t += e[n] + e[n + 1], n += 2;
			continue;
		}
		if (e[n] !== "`") {
			t += e[n++];
			continue;
		}
		let r = 0;
		for (; n + r < e.length && e[n + r] === "`";) r += 1;
		let i = n + r, a = !1;
		for (; i < e.length;) {
			if (e[i] !== "`") {
				i += 1;
				continue;
			}
			let o = 0;
			for (; i + o < e.length && e[i + o] === "`";) o += 1;
			if (o === r) {
				let o = e.slice(n + r, i);
				t += e.slice(n, n + r) + o.replace(/\\\||\|/g, (e) => e === "|" ? "\\|" : e) + e.slice(i, i + r), n = i + r, a = !0;
				break;
			}
			i += o;
		}
		a || (t += e.slice(n, n + r), n += r);
	}
	return t;
}
function GA(e) {
	return e.split("\n").map((e) => !e.includes("|") || !e.includes("`") ? e : WA(e)).join("\n");
}
function KA(e) {
	return (e || "").replace(/\s+/g, " ").trim();
}
function qA(e, t, n = {}) {
	let r = n.cellLineSeparator ?? "";
	if (!e || !e.content || e.content.length === 0) return "";
	let i = [];
	e.content.forEach((e) => {
		let n = [];
		e.content && e.content.forEach((e) => {
			let i = "";
			i = e.content && Array.isArray(e.content) && e.content.length > 1 ? e.content.map((e) => t.renderChildren(e)).join(r) : e.content ? t.renderChildren(e.content) : "";
			let a = KA(i.split(r).join("\n").replace(/[ \t]*\r?\n[ \t]*/g, "<br>")), o = e.type === "tableHeader", s = wA(e.attrs);
			n.push({
				text: a,
				isHeader: o,
				align: s
			});
		}), i.push(n);
	});
	let a = i.reduce((e, t) => Math.max(e, t.length), 0);
	if (a === 0) return "";
	let o = Array.from({ length: a }).fill(0);
	i.forEach((e) => {
		for (let t = 0; t < a; t += 1) {
			let n = (e[t]?.text || "").length;
			n > o[t] && (o[t] = n), o[t] < 3 && (o[t] = 3);
		}
	});
	let s = (e, t) => e + " ".repeat(Math.max(0, t - e.length)), c = i[0], l = c.some((e) => e.isHeader), u = Array.from({ length: a }).fill(null);
	i.forEach((e) => {
		for (let t = 0; t < a; t += 1) !u[t] && e[t]?.align && (u[t] = e[t].align);
	});
	let d = "\n", f = Array.from({ length: a }).map((e, t) => l && c[t] && c[t].text || "");
	return d += `| ${f.map((e, t) => s(e, o[t])).join(" | ")} |\n`, d += `| ${o.map((e, t) => {
		let n = Math.max(3, e), r = u[t];
		return r === "left" ? `:${"-".repeat(n)}` : r === "right" ? `${"-".repeat(n)}:` : r === "center" ? `:${"-".repeat(n)}:` : "-".repeat(n);
	}).join(" | ")} |\n`, (l ? i.slice(1) : i).forEach((e) => {
		d += `| ${Array.from({ length: a }).fill(0).map((t, n) => s(e[n] && e[n].text || "", o[n])).join(" | ")} |\n`;
	}), d;
}
var JA = Sf.create({
	name: "table",
	addOptions() {
		return {
			HTMLAttributes: {},
			resizable: !1,
			renderWrapper: !1,
			handleWidth: 5,
			cellMinWidth: 25,
			View: IA,
			lastColumnResizable: !0,
			allowTableNodeSelection: !1
		};
	},
	content: "tableRow+",
	tableRole: "table",
	isolating: !0,
	group: "block",
	parseHTML() {
		return [{ tag: "table" }];
	},
	renderHTML({ node: e, HTMLAttributes: t }) {
		let { colgroup: n, tableWidth: r, tableMinWidth: i } = LA(e, this.options.cellMinWidth), a = t.style;
		function o() {
			return a || (r ? `width: ${r}` : `min-width: ${i}`);
		}
		let s = [
			"table",
			F(this.options.HTMLAttributes, t, { style: o() }),
			n,
			["tbody", 0]
		];
		return this.options.renderWrapper ? [
			"div",
			{ class: "tableWrapper" },
			s
		] : s;
	},
	parseMarkdown: (e, t) => {
		let n = [], r = Array.isArray(e.align) ? e.align : [];
		if (e.header) {
			let i = [];
			e.header.forEach((e, n) => {
				let a = SA(r[n] ?? e.align), o = a ? { align: a } : {};
				i.push(t.createNode("tableHeader", o, [{
					type: "paragraph",
					content: t.parseInline(e.tokens)
				}]));
			}), n.push(t.createNode("tableRow", {}, i));
		}
		return e.rows && e.rows.forEach((e) => {
			let i = [];
			e.forEach((e, n) => {
				let a = SA(r[n] ?? e.align), o = a ? { align: a } : {};
				i.push(t.createNode("tableCell", o, [{
					type: "paragraph",
					content: t.parseInline(e.tokens)
				}]));
			}), n.push(t.createNode("tableRow", {}, i));
		}), t.createNode("table", void 0, n);
	},
	renderMarkdown: (e, t) => qA(e, t),
	markdownTokenizer: {
		name: "table",
		level: "block",
		start: (e) => {
			let t = e.split("\n");
			if (t.length < 2) return -1;
			let n = t[1];
			return !/^[ \t|:]*-[ \t|:-]*$/.test(n) || !n.includes("|") ? -1 : t[0].includes("|") ? 0 : -1;
		},
		tokenize(e, t, n) {
			let r = e.indexOf("\n\n"), i = r >= 0 ? e.slice(0, r) : e, a = i.split("\n");
			if (a.length < 2) return;
			let o = a[1];
			if (!/^[ \t|:]*-[ \t|:-]*$/.test(o) || !o.includes("|")) return;
			let s = GA(i);
			if (s === i) return;
			let c = n.blockTokens(s)[0];
			if (c?.type !== "table" || !c.raw) return;
			let l = c.raw.split("\n").length, u = e.split("\n").slice(0, l).join("\n");
			return {
				...c,
				raw: u
			};
		}
	},
	addCommands() {
		return {
			insertTable: ({ rows: e = 3, cols: t = 3, withHeaderRow: n = !0 } = {}) => ({ tr: r, dispatch: i, editor: a }) => {
				let o = BA(a.schema, e, t, n);
				if (i) {
					let e = r.selection.from + 1;
					r.replaceSelectionWith(o).scrollIntoView().setSelection(w.near(r.doc.resolve(e)));
				}
				return !0;
			},
			addColumnBefore: () => ({ state: e, dispatch: t }) => gk(e, t),
			addColumnAfter: () => ({ state: e, dispatch: t }) => _k(e, t),
			deleteColumn: () => ({ state: e, dispatch: t }) => {
				let n = Rl(e.selection.$from, (e) => e.type.name === "table");
				return yk(e, t && ((e) => {
					n && UA(e, n.pos), t(e);
				}));
			},
			addRowBefore: () => ({ state: e, dispatch: t }) => Sk(e, t),
			addRowAfter: () => ({ state: e, dispatch: t }) => Ck(e, t),
			deleteRow: () => ({ state: e, dispatch: t }) => {
				let n = Rl(e.selection.$from, (e) => e.type.name === "table");
				return Tk(e, t && ((e) => {
					n && UA(e, n.pos), t(e);
				}));
			},
			deleteTable: () => ({ state: e, dispatch: t }) => Rk(e, t),
			mergeCells: () => ({ state: e, dispatch: t }) => Ok(e, t),
			splitCell: () => ({ state: e, dispatch: t }) => kk(e, t),
			toggleHeaderColumn: () => ({ state: e, dispatch: t }) => Pk("column")(e, t),
			toggleHeaderRow: () => ({ state: e, dispatch: t }) => Pk("row")(e, t),
			toggleHeaderCell: () => ({ state: e, dispatch: t }) => Fk(e, t),
			mergeOrSplit: () => ({ state: e, dispatch: t }) => Ok(e, t) ? !0 : kk(e, t),
			setCellAttribute: (e, t) => ({ state: n, dispatch: r }) => jk(e, t)(n, r),
			goToNextCell: () => ({ state: e, dispatch: t }) => Lk(1)(e, t),
			goToPreviousCell: () => ({ state: e, dispatch: t }) => Lk(-1)(e, t),
			fixTables: () => ({ state: e, dispatch: t }) => (t && fk(e), !0),
			setCellSelection: (e) => ({ tr: t, dispatch: n }) => {
				if (n) {
					let n = $.create(t.doc, e.anchorCell, e.headCell);
					t.setSelection(n);
				}
				return !0;
			}
		};
	},
	addKeyboardShortcuts() {
		return {
			Tab: () => this.editor.commands.goToNextCell() ? !0 : this.editor.can().addRowAfter() ? this.editor.chain().addRowAfter().goToNextCell().run() : !1,
			"Shift-Tab": () => this.editor.commands.goToPreviousCell(),
			Backspace: HA,
			"Mod-Backspace": HA,
			Delete: HA,
			"Mod-Delete": HA
		};
	},
	addProseMirrorPlugins() {
		return [...this.options.resizable && this.editor.isEditable ? [sA({
			handleWidth: this.options.handleWidth,
			cellMinWidth: this.options.cellMinWidth,
			defaultCellMinWidth: this.options.cellMinWidth,
			View: this.options.View,
			lastColumnResizable: this.options.lastColumnResizable
		})] : [], xA({ allowTableNodeSelection: this.options.allowTableNodeSelection })];
	},
	addNodeView() {
		let e = this.options.resizable && this.editor.isEditable, t = this.options.View;
		return e || !t ? null : ({ node: e, view: n, HTMLAttributes: r }) => {
			let i = F(this.options.HTMLAttributes, r);
			return new t(e, this.options.cellMinWidth, n, i);
		};
	},
	extendNodeSchema(e) {
		return { tableRole: P(N(e, "tableRole", {
			name: e.name,
			options: e.options,
			storage: e.storage
		})) };
	}
});
I.create({
	name: "tableKit",
	addExtensions() {
		let e = [];
		return this.options.table !== !1 && e.push(JA.configure(this.options.table)), this.options.tableCell !== !1 && e.push(jA.configure(this.options.tableCell)), this.options.tableHeader !== !1 && e.push(MA.configure(this.options.tableHeader)), this.options.tableRow !== !1 && e.push(NA.configure(this.options.tableRow)), e;
	}
});
//#endregion
//#region node_modules/@tiptap/extension-image/dist/index.js
var YA = /(?:^|\s)(!\[(.+|:?)]\((\S+)(?:(?:\s+)["'](\S+)["'])?\))$/, XA = Sf.create({
	name: "image",
	addOptions() {
		return {
			inline: !1,
			allowBase64: !1,
			HTMLAttributes: {},
			resize: !1
		};
	},
	inline() {
		return this.options.inline;
	},
	group() {
		return this.options.inline ? "inline" : "block";
	},
	draggable: !0,
	addAttributes() {
		return {
			src: { default: null },
			alt: { default: null },
			title: { default: null },
			width: { default: null },
			height: { default: null }
		};
	},
	parseHTML() {
		return [{ tag: this.options.allowBase64 ? "img[src]" : "img[src]:not([src^=\"data:\"])" }];
	},
	renderHTML({ HTMLAttributes: e }) {
		return ["img", F(this.options.HTMLAttributes, e)];
	},
	parseMarkdown: (e, t) => t.createNode("image", {
		src: e.href,
		title: e.title,
		alt: e.text
	}),
	renderMarkdown: (e) => {
		let t = e.attrs?.src ?? "", n = e.attrs?.alt ?? "", r = e.attrs?.title ?? "";
		return r ? `![${n}](${t} "${r}")` : `![${n}](${t})`;
	},
	addNodeView() {
		if (!this.options.resize || !this.options.resize.enabled || typeof document > "u") return null;
		let { directions: e, minWidth: t, minHeight: n, alwaysPreserveAspectRatio: r } = this.options.resize, i = /* @__PURE__ */ new Set([
			"src",
			"width",
			"height"
		]);
		return ({ node: a, getPos: o, HTMLAttributes: s, editor: c }) => {
			let l = document.createElement("img");
			l.draggable = !1;
			let u = F(this.options.HTMLAttributes, s);
			Object.entries(u).forEach(([e, t]) => {
				if (t != null) switch (e) {
					case "src":
					case "width":
					case "height": break;
					default: l.setAttribute(e, t);
				}
			}), u.src !== null && (l.src = u.src);
			let d = { ...s }, f = (e) => {
				if (typeof e == "string" && e !== "") {
					l.getAttribute("src") !== e && (l.src = e);
					return;
				}
				l.hasAttribute("src") && l.removeAttribute("src"), l.src !== "" && (l.src = "");
			};
			f(s.src);
			let p = new xf({
				element: l,
				editor: c,
				node: a,
				getPos: o,
				onResize: (e, t) => {
					l.style.width = `${e}px`, l.style.height = `${t}px`;
				},
				onCommit: (e, t) => {
					let n = o();
					n !== void 0 && this.editor.chain().setNodeSelection(n).updateAttributes(this.name, {
						width: e,
						height: t
					}).run();
				},
				onUpdate: (e) => {
					if (e.type !== a.type) return !1;
					let t = Jl(e, c.extensionManager.attributes.filter((t) => t.type === e.type.name));
					return Object.keys(d).forEach((e) => {
						!i.has(e) && !(e in t) && l.removeAttribute(e);
					}), Object.entries(t).forEach(([e, t]) => {
						i.has(e) || (t == null ? l.removeAttribute(e) : l.setAttribute(e, t));
					}), f(t.src), d = t, !0;
				},
				options: {
					directions: e,
					min: {
						width: t,
						height: n
					},
					preserveAspectRatio: r === !0
				}
			}), m = p.dom, h = () => {
				m.style.visibility = "", m.style.pointerEvents = "";
			};
			return m.style.visibility = "hidden", m.style.pointerEvents = "none", l.complete && l.naturalWidth > 0 ? h() : (l.onload = h, l.onerror = h), p;
		};
	},
	addCommands() {
		return { setImage: (e) => ({ commands: t }) => t.insertContent({
			type: this.name,
			attrs: e
		}) };
	},
	addInputRules() {
		return [_f({
			find: YA,
			type: this.type,
			getAttributes: (e) => {
				let [, , t, n, r] = e;
				return {
					src: n,
					alt: t,
					title: r
				};
			}
		})];
	}
}).extend({ addAttributes() {
	return {
		src: { default: null },
		alt: { default: null },
		title: { default: null },
		width: {
			default: null,
			parseHTML: (e) => e.getAttribute("width") || null,
			renderHTML: (e) => e.width ? { width: String(e.width) } : {}
		},
		assetUrl: {
			default: null,
			parseHTML: (e) => e.getAttribute("data-asset-url") || null,
			renderHTML: (e) => e.assetUrl ? { "data-asset-url": String(e.assetUrl) } : {}
		},
		assetId: {
			default: null,
			parseHTML: (e) => e.getAttribute("data-asset-id") || null,
			renderHTML: (e) => e.assetId ? { "data-asset-id": String(e.assetId) } : {}
		}
	};
} }).configure({
	inline: !1,
	allowBase64: !0
}), ZA = 2, QA = 8;
function $A(e) {
	if (!e) return 0;
	let t = e.getAttribute?.("data-indent-level");
	if (t) {
		let e = Number.parseInt(t, 10);
		if (Number.isFinite(e)) return Math.max(0, Math.min(QA, e));
	}
	let n = e.style?.marginLeft;
	if (!n) return 0;
	let r = String(n).match(/([\d.]+)/);
	if (!r) return 0;
	let i = Number.parseFloat(r[1]);
	if (!Number.isFinite(i)) return 0;
	let a = Math.round(i / ZA);
	return Math.max(0, Math.min(QA, a));
}
function ej(e) {
	return Number.isFinite(e) ? Math.max(0, Math.min(QA, Math.round(e))) : 0;
}
var tj = I.create({
	name: "indent",
	addOptions() {
		return { types: ["paragraph", "heading"] };
	},
	addGlobalAttributes() {
		return [{
			types: this.options.types,
			attributes: { indentLevel: {
				default: 0,
				parseHTML: (e) => $A(e),
				renderHTML: (e) => {
					let t = ej(e.indentLevel);
					return t ? {
						"data-indent-level": String(t),
						style: `margin-left: ${t * ZA}em;`
					} : {};
				}
			} }
		}];
	},
	addCommands() {
		let e = (e) => ({ state: t, tr: n, dispatch: r }) => {
			let { from: i, to: a, empty: o, $from: s } = t.selection, c = new Set(this.options.types ?? []), l = !1, u = (t, r) => {
				if (!t || !t.isTextblock || !c.has(t.type.name)) return;
				let i = ej(t.attrs?.indentLevel ?? 0), a = ej(i + e);
				a !== i && (n.setNodeMarkup(r, void 0, {
					...t.attrs,
					indentLevel: a
				}), l = !0);
			};
			if (o && s) {
				let e = s.parent;
				u(e, s.before(s.depth));
			} else {
				let e = /* @__PURE__ */ new Set();
				t.doc.nodesBetween(i, a, (t, n) => {
					t.isTextblock && c.has(t.type.name) && (e.has(n) || (e.add(n), u(t, n)));
				});
			}
			return l && r && r(n), l;
		};
		return {
			increaseIndent: () => e(1),
			decreaseIndent: () => e(-1)
		};
	}
}), nj = hE.extend({ addAttributes() {
	let e = { ...this.parent?.() };
	return delete e.title, e;
} }).configure({
	openOnClick: !1,
	autolink: !1,
	linkOnPaste: !1
}), rj = mD.extend({ addAttributes() {
	let e = { ...this.parent?.() };
	return delete e.type, e;
} }), ij = { align: {
	default: null,
	parseHTML: (e) => e.getAttribute("align") || e.style.textAlign || null,
	renderHTML: (e) => e.align ? { align: e.align } : {}
} }, aj = jA.extend({ addAttributes() {
	return {
		...this.parent?.(),
		...ij
	};
} }), oj = MA.extend({ addAttributes() {
	return {
		...this.parent?.(),
		...ij
	};
} }), sj = (e = !1) => [
	LO.configure({
		undoRedo: !e && {},
		link: !1,
		orderedList: !1,
		trailingNode: !1
	}),
	nj,
	rj,
	RO.configure({ types: ["heading", "paragraph"] }),
	XA,
	tj,
	JA.configure({
		resizable: !0,
		allowTableNodeSelection: !0
	}),
	NA,
	oj,
	aj
], cj = {
	version: 2,
	tags: /* @__PURE__ */ "p.h1.h2.h3.h4.h5.h6.strong.b.em.i.s.strike.del.u.code.pre.blockquote.ul.ol.li.br.hr.a.img.table.tbody.thead.tfoot.tr.td.th.colgroup.col".split("."),
	nodes: [
		"doc",
		"paragraph",
		"heading",
		"text",
		"blockquote",
		"bulletList",
		"orderedList",
		"listItem",
		"hardBreak",
		"horizontalRule",
		"codeBlock",
		"image",
		"table",
		"tableRow",
		"tableCell",
		"tableHeader"
	],
	marks: [
		"bold",
		"italic",
		"strike",
		"underline",
		"code",
		"link"
	],
	attributes: {
		a: [
			"href",
			"target",
			"rel",
			"class"
		],
		ol: ["start"],
		code: ["class"],
		p: ["style", "data-indent-level"],
		h1: ["style", "data-indent-level"],
		h2: ["style", "data-indent-level"],
		h3: ["style", "data-indent-level"],
		h4: ["style", "data-indent-level"],
		h5: ["style", "data-indent-level"],
		h6: ["style", "data-indent-level"],
		img: [
			"src",
			"alt",
			"title",
			"width",
			"data-asset-url",
			"data-asset-id"
		],
		table: ["style"],
		col: ["style"],
		td: [
			"colspan",
			"rowspan",
			"colwidth",
			"align",
			"style"
		],
		th: [
			"colspan",
			"rowspan",
			"colwidth",
			"align",
			"style"
		]
	},
	nodeAttributes: {
		paragraph: ["textAlign", "indentLevel"],
		heading: [
			"level",
			"textAlign",
			"indentLevel"
		],
		orderedList: ["start"],
		codeBlock: ["language"],
		image: [
			"src",
			"alt",
			"title",
			"width",
			"assetUrl",
			"assetId"
		],
		tableCell: [
			"colspan",
			"rowspan",
			"colwidth",
			"align"
		],
		tableHeader: [
			"colspan",
			"rowspan",
			"colwidth",
			"align"
		]
	},
	linkSchemes: [
		"http",
		"https",
		"mailto"
	],
	headingLevels: [
		1,
		2,
		3,
		4,
		5,
		6
	],
	inlineTags: [
		"strong",
		"b",
		"em",
		"i",
		"s",
		"strike",
		"del",
		"u",
		"code",
		"br",
		"a",
		"img"
	],
	blockNodes: [
		"paragraph",
		"heading",
		"blockquote",
		"bulletList",
		"orderedList",
		"horizontalRule",
		"codeBlock",
		"image",
		"table"
	],
	inlineNodes: ["text", "hardBreak"]
}, lj = new Set(cj.tags.map((e) => e.toUpperCase())), uj = new Set(cj.nodes), dj = new Set(cj.marks), fj = (e) => {
	if (/[\u0000-\u001f\u007f]/.test(e)) return !1;
	try {
		return cj.linkSchemes.includes(new URL(e.trim()).protocol.slice(0, -1));
	} catch {
		return !1;
	}
}, pj = (e) => {
	if (/[\u0000-\u001f\u007f]/.test(e)) return !1;
	if (/^data:image\/(png|jpeg|gif|webp);base64,[A-Za-z0-9+/]+={0,2}$/.test(e) || e.startsWith("/") && !e.startsWith("//") && !e.includes("\\")) return !0;
	try {
		let t = new URL(e);
		return ["http:", "https:"].includes(t.protocol) && !t.username && !t.password;
	} catch {
		return !1;
	}
};
function mj(e, t) {
	return t.split(";").filter((e) => e.trim()).every((t) => {
		let n = t.split(":").map((e) => e.trim().toLowerCase());
		if (n.length !== 2) return !1;
		let [r, i] = n;
		return /^(p|h[1-6])$/.test(e) ? r === "text-align" && [
			"left",
			"center",
			"right",
			"justify"
		].includes(i) || r === "margin-left" && /^(0|2|4|6|8|10|12|14|16)em$/.test(i) : ["td", "th"].includes(e) ? r === "text-align" && [
			"left",
			"center",
			"right",
			"justify"
		].includes(i) : ["table", "col"].includes(e) && ["width", "min-width"].includes(r) && /^\d+(\.\d+)?px$/.test(i);
	});
}
function hj(e, t = document) {
	let n = t.createElement("template");
	n.innerHTML = e;
	for (let e of n.content.querySelectorAll("*")) {
		if (!lj.has(e.tagName)) throw Error("This page contains unsupported elements. Its saved source is unchanged.");
		let t = e.localName, n = [...e.children], r = cj.inlineTags;
		if (([
			"p",
			"h1",
			"h2",
			"h3",
			"h4",
			"h5",
			"h6"
		].includes(t) || r.includes(t)) && n.some((e) => !r.includes(e.localName)) || ["ul", "ol"].includes(t) && (n.some((e) => e.localName !== "li") || [...e.childNodes].some((e) => e.nodeType === 3 && e.textContent?.trim())) || t === "li" && !["ul", "ol"].includes(e.parentElement?.localName || "") || t === "pre" && (n.length !== 1 || n[0].localName !== "code" || [...e.childNodes].some((e) => e.nodeType === 3 && e.textContent)) || t === "code" && (n.length > 0 || r.includes(e.parentElement?.localName || ""))) throw Error("This page has unsupported content structure. Its saved source is unchanged.");
		if (t === "code" && e.attributes.length && e.parentElement?.localName !== "pre") throw Error("Unsupported inline code attributes.");
		if (t === "img" && !pj(e.getAttribute("src") || "")) throw Error("Unsupported image address.");
		let i = e.parentElement?.localName || "";
		if (t === "table" && n.some((e) => ![
			"colgroup",
			"thead",
			"tbody",
			"tfoot",
			"tr"
		].includes(e.localName)) || [
			"thead",
			"tbody",
			"tfoot"
		].includes(t) && (i !== "table" || n.some((e) => e.localName !== "tr")) || t === "tr" && (![
			"table",
			"thead",
			"tbody",
			"tfoot"
		].includes(i) || !n.length || n.some((e) => !["td", "th"].includes(e.localName))) || ["td", "th"].includes(t) && i !== "tr" || t === "colgroup" && (i !== "table" || n.some((e) => e.localName !== "col")) || t === "col" && i !== "colgroup") throw Error("Unsupported table structure.");
		for (let n of e.attributes) if (!((cj.attributes[e.localName] || []).includes(n.name) && (e.tagName !== "OL" || /^-?\d+$/.test(n.value) && Number.isSafeInteger(Number(n.value)) && Number(n.value) >= -2147483648 && Number(n.value) <= 2147483647) && (e.tagName !== "CODE" || /^language-[\w-]+$/.test(n.value)) && (!["src", "data-asset-url"].includes(n.name) || pj(n.value)) && (n.name !== "style" || mj(t, n.value)) && (n.name !== "data-indent-level" || /^[0-8]$/.test(n.value)) && (![
			"colspan",
			"rowspan",
			"width"
		].includes(n.name) || /^\d+$/.test(n.value) && Number(n.value) > 0 && Number(n.value) <= 1e4) && (n.name !== "colwidth" || n.value.split(",").every((e) => /^\d+$/.test(e) && Number(e) <= 1e4)) && (n.name !== "align" || [
			"left",
			"center",
			"right",
			"justify"
		].includes(n.value)))) throw Error("This page contains formatting this editor cannot preserve yet. Its saved source is unchanged.");
		if (e.tagName === "A" && e.hasAttribute("href") && !fj(e.getAttribute("href"))) throw Error("This page contains an unsupported link. Its saved source is unchanged.");
	}
	return e;
}
function gj(e) {
	if (!e || !uj.has(e.type)) throw Error("This legacy document contains unsupported content.");
	if (Object.keys(e).some((e) => ![
		"type",
		"attrs",
		"content",
		"marks",
		"text"
	].includes(e))) throw Error("Unsupported legacy metadata.");
	let t = cj.nodeAttributes[e.type] || [];
	if (Object.keys(e.attrs || {}).some((e) => !t.includes(e))) throw Error("Unsupported legacy attributes.");
	if (e.type === "heading" && !cj.headingLevels.includes(e.attrs?.level)) throw Error("Unsupported heading level.");
	if (e.attrs?.start !== void 0 && (!Number.isInteger(e.attrs.start) || e.attrs.start < -2147483648 || e.attrs.start > 2147483647)) throw Error("Unsupported list start.");
	if (e.attrs?.language != null && !/^[\w-]+$/.test(e.attrs.language)) throw Error("Unsupported code language.");
	for (let t of ["textAlign", "align"]) if (e.attrs?.[t] != null && ![
		"left",
		"center",
		"right",
		"justify"
	].includes(e.attrs[t])) throw Error("Unsupported alignment.");
	if (e.attrs?.indentLevel != null && (!Number.isInteger(e.attrs.indentLevel) || e.attrs.indentLevel < 0 || e.attrs.indentLevel > 8)) throw Error("Unsupported indentation.");
	for (let t of ["colspan", "rowspan"]) if (e.attrs?.[t] != null && (!Number.isInteger(e.attrs[t]) || e.attrs[t] < 1 || e.attrs[t] > 1e4)) throw Error("Unsupported table span.");
	if (e.attrs?.colwidth != null && (!Array.isArray(e.attrs.colwidth) || e.attrs.colwidth.some((e) => !Number.isInteger(e) || e < 0 || e > 1e4))) throw Error("Unsupported table width.");
	if (e.type === "image") {
		if (typeof e.attrs?.src != "string" || !pj(e.attrs.src)) throw Error("Unsupported image.");
		if (e.attrs.assetUrl != null && !pj(e.attrs.assetUrl)) throw Error("Unsupported image asset URL.");
		if (e.attrs.width != null && (!/^\d+$/.test(String(e.attrs.width)) || Number(e.attrs.width) < 1 || Number(e.attrs.width) > 1e4)) throw Error("Unsupported image width.");
	}
	for (let t of e.marks || []) {
		if (!dj.has(t.type)) throw Error("Unsupported legacy formatting.");
		if (Object.keys(t).some((e) => !["type", "attrs"].includes(e))) throw Error("Unsupported legacy mark metadata.");
		if (Object.keys(t.attrs || {}).some((e) => t.type !== "link" || !cj.attributes.a.includes(e))) throw Error("Unsupported legacy formatting attributes.");
		if (t.type === "link" && !fj(t.attrs?.href || "")) throw Error("Unsupported legacy link.");
	}
	for (let t of e.content || []) gj(t);
}
function _j(e, t, n = document) {
	if (t === "Html") {
		let t = n.createElement("div");
		t.innerHTML = hj(e, n);
		let r = Be.fromSchema(ru(sj())).parse(t, { preserveWhitespace: "full" });
		return r.check(), r.toJSON();
	}
	if (t === "LegacyJson") {
		let t = JSON.parse(e);
		if (t.type !== "doc") throw Error("The legacy JSON is not a TipTap document.");
		return gj(t), ru(sj()).nodeFromJSON(t).check(), t;
	}
	return {
		type: "doc",
		content: e.split(/\r?\n/).map((e) => ({
			type: "paragraph",
			content: e ? [{
				type: "text",
				text: e
			}] : []
		}))
	};
}
//#endregion
//#region src/collaboration/document-roots.ts
var vj = "__tiptap_position_migrations:root:default", yj = () => {
	throw Error("collaboration_position_metadata_unsupported");
}, bj = (e) => typeof e == "number" && Number.isSafeInteger(e) && e >= 0, xj = (e, t) => e && typeof e == "object" && !Array.isArray(e) && Object.keys(e).every((e) => t.includes(e)), Sj = (e) => xj(e, ["client", "clock"]) && bj(e.client) && bj(e.clock), Cj = (e) => e === void 0 || e === "start" || e === "end";
function wj(e) {
	if (e.store.pendingStructs || e.store.pendingDs) throw Error("collaboration_state_incomplete");
	if ([...e.share.keys()].some((e) => e !== "default" && e !== vj)) throw Error("collaboration_state_unsupported");
	if (!e.share.has(vj)) return;
	let t = e.getMap(vj);
	t.size > 16384 && yj();
	let n = 0, r = 0;
	for (let [e, i] of t.entries()) {
		let t = i;
		(!xj(t, [
			"version",
			"client",
			"clock",
			"seen",
			"transfers"
		]) || t.version !== 1 || !bj(t.client) || !bj(t.clock) || e !== `${t.client}:${t.clock}` || !Array.isArray(t.seen) || t.seen.length > 4096 || !Array.isArray(t.transfers)) && yj(), r += JSON.stringify(t).length, n += t.transfers.length, (r > 8388608 || n > 131072) && yj(), t.seen.some((e) => !Array.isArray(e) || e.length !== 2 || !e.every(bj)) && yj();
		for (let e of t.transfers) {
			if ((!xj(e, ["source", "target"]) || !xj(e.source, [
				"client",
				"clock",
				"length",
				"boundary"
			]) || !bj(e.source.client) || !bj(e.source.clock) || !bj(e.source.length) || e.source.length < 1 || !Number.isSafeInteger(e.source.clock + e.source.length) || !Cj(e.source.boundary)) && yj(), e.target === null) continue;
			let t = e.target;
			(!xj(t, [
				"client",
				"clock",
				"type",
				"tname",
				"assoc",
				"boundary"
			]) || !bj(t.client) || !bj(t.clock) || !(t.type === null || Sj(t.type)) || t.tname !== void 0 && t.tname !== "default" || !(t.assoc === void 0 || Number.isSafeInteger(t.assoc)) || !Cj(t.boundary)) && yj();
		}
	}
}
//#endregion
//#region node_modules/@hocuspocus/common/dist/hocuspocus-common.esm.js
var Tj = /* @__PURE__ */ function(e) {
	return e[e.Token = 0] = "Token", e[e.PermissionDenied = 1] = "PermissionDenied", e[e.Authenticated = 2] = "Authenticated", e;
}({}), Ej = (e, t) => {
	L(e, Tj.Token), Xp(e, t);
}, Dj = (e, t, n, r) => {
	switch (z(e)) {
		case Tj.Token:
			t();
			break;
		case Tj.PermissionDenied:
			n(Tm(e));
			break;
		case Tj.Authenticated: r(Tm(e));
	}
}, Oj = (e) => Array.from(e.entries()).map(([e, t]) => ({
	clientId: e,
	...t
})), kj = "\0";
function Aj(e, t) {
	return e + kj + t;
}
function jj(e) {
	let t = e.indexOf(kj);
	return t === -1 ? {
		documentName: e,
		sessionId: null
	} : {
		documentName: e.substring(0, t),
		sessionId: e.substring(t + 1)
	};
}
var Mj = /* @__PURE__ */ function(e) {
	return e[e.Connecting = 0] = "Connecting", e[e.Open = 1] = "Open", e[e.Closing = 2] = "Closing", e[e.Closed = 3] = "Closed", e;
}({}), Nj = 3e4, Pj = class extends vp {
	constructor(e) {
		super(), this.doc = e, this.clientID = e.clientID, this.states = /* @__PURE__ */ new Map(), this.meta = /* @__PURE__ */ new Map(), this._checkInterval = setInterval(() => {
			let e = Bm();
			this.getLocalState() !== null && 15e3 <= e - this.meta.get(this.clientID).lastUpdated && this.setLocalState(this.getLocalState());
			let t = [];
			this.meta.forEach((n, r) => {
				r !== this.clientID && 3e4 <= e - n.lastUpdated && this.states.has(r) && t.push(r);
			}), t.length > 0 && Fj(this, t, "timeout");
		}, yp(Nj / 10)), e.on("destroy", () => {
			this.destroy();
		}), this.setLocalState({});
	}
	destroy() {
		this.emit("destroy", [this]), this.setLocalState(null), super.destroy(), clearInterval(this._checkInterval);
	}
	getLocalState() {
		return this.states.get(this.clientID) || null;
	}
	setLocalState(e) {
		let t = this.clientID, n = this.meta.get(t), r = n === void 0 ? 0 : n.clock + 1, i = this.states.get(t);
		e === null ? this.states.delete(t) : this.states.set(t, e), this.meta.set(t, {
			clock: r,
			lastUpdated: Bm()
		});
		let a = [], o = [], s = [], c = [];
		e === null ? c.push(t) : i == null ? e != null && a.push(t) : (o.push(t), oh(i, e) || s.push(t)), (a.length > 0 || s.length > 0 || c.length > 0) && this.emit("change", [{
			added: a,
			updated: s,
			removed: c
		}, "local"]), this.emit("update", [{
			added: a,
			updated: o,
			removed: c
		}, "local"]);
	}
	setLocalStateField(e, t) {
		let n = this.getLocalState();
		n !== null && this.setLocalState({
			...n,
			[e]: t
		});
	}
	getStates() {
		return this.states;
	}
}, Fj = (e, t, n) => {
	let r = [];
	for (let n = 0; n < t.length; n++) {
		let i = t[n];
		if (e.states.has(i)) {
			if (e.states.delete(i), i === e.clientID) {
				let t = e.meta.get(i);
				e.meta.set(i, {
					clock: t.clock + 1,
					lastUpdated: Bm()
				});
			}
			r.push(i);
		}
	}
	r.length > 0 && (e.emit("change", [{
		added: [],
		updated: [],
		removed: r
	}, n]), e.emit("update", [{
		added: [],
		updated: [],
		removed: r
	}, n]));
}, Ij = (e, t, n = e.states) => {
	let r = t.length, i = Bp();
	L(i, r);
	for (let a = 0; a < r; a++) {
		let r = t[a], o = n.get(r) || null, s = e.meta.get(r).clock;
		L(i, r), L(i, s), Xp(i, JSON.stringify(o));
	}
	return Up(i);
}, Lj = (e, t, n) => {
	let r = ym(t), i = Bm(), a = [], o = [], s = [], c = [], l = z(r);
	for (let t = 0; t < l; t++) {
		let t = z(r), n = z(r), l = JSON.parse(Tm(r)), u = e.meta.get(t), d = e.states.get(t), f = u === void 0 ? 0 : u.clock;
		(f < n || f === n && l === null && e.states.has(t)) && (l === null ? t === e.clientID && e.getLocalState() != null ? n++ : e.states.delete(t) : e.states.set(t, l), e.meta.set(t, {
			clock: n,
			lastUpdated: i
		}), u === void 0 && l !== null ? a.push(t) : u !== void 0 && l === null ? c.push(t) : l !== null && (oh(l, d) || s.push(t), o.push(t)));
	}
	(a.length > 0 || s.length > 0 || c.length > 0) && e.emit("change", [{
		added: a,
		updated: s,
		removed: c
	}, n]), (a.length > 0 || o.length > 0 || c.length > 0) && e.emit("update", [{
		added: a,
		updated: o,
		removed: c
	}, n]);
};
//#endregion
//#region node_modules/@lifeomic/attempt/dist/es6/src/index.js
function Rj(e) {
	return e || (e = {}), {
		delay: e.delay === void 0 ? 200 : e.delay,
		initialDelay: e.initialDelay === void 0 ? 0 : e.initialDelay,
		minDelay: e.minDelay === void 0 ? 0 : e.minDelay,
		maxDelay: e.maxDelay === void 0 ? 0 : e.maxDelay,
		factor: e.factor === void 0 ? 0 : e.factor,
		maxAttempts: e.maxAttempts === void 0 ? 3 : e.maxAttempts,
		timeout: e.timeout === void 0 ? 0 : e.timeout,
		jitter: e.jitter === !0,
		initialJitter: e.initialJitter === !0,
		handleError: e.handleError === void 0 ? null : e.handleError,
		handleTimeout: e.handleTimeout === void 0 ? null : e.handleTimeout,
		beforeAttempt: e.beforeAttempt === void 0 ? null : e.beforeAttempt,
		calculateDelay: e.calculateDelay === void 0 ? null : e.calculateDelay
	};
}
async function zj(e) {
	return new Promise((t) => setTimeout(t, e));
}
function Bj(e, t) {
	let n = t.delay;
	if (n === 0) return 0;
	if (t.factor && (n *= t.factor ** (e.attemptNum - 1), t.maxDelay !== 0 && (n = Math.min(n, t.maxDelay))), t.jitter) {
		let e = Math.ceil(t.minDelay);
		n = Math.floor(Math.random() * (Math.floor(n) - e + 1)) + e;
	}
	return Math.round(n);
}
async function Vj(e, t) {
	let n = Rj(t);
	for (let e of [
		"delay",
		"initialDelay",
		"minDelay",
		"maxDelay",
		"maxAttempts",
		"timeout"
	]) {
		let t = n[e];
		if (!Number.isInteger(t) || t < 0) throw Error(`Value for ${e} must be an integer greater than or equal to 0`);
	}
	if (n.factor.constructor !== Number || n.factor < 0) throw Error("Value for factor must be a number greater than or equal to 0");
	if (n.delay < n.minDelay) throw Error(`delay cannot be less than minDelay (delay: ${n.delay}, minDelay: ${n.minDelay}`);
	let r = {
		attemptNum: 0,
		attemptsRemaining: n.maxAttempts ? n.maxAttempts : -1,
		aborted: !1,
		abort() {
			r.aborted = !0;
		}
	}, i = n.calculateDelay || Bj;
	async function a() {
		if (n.beforeAttempt && n.beforeAttempt(r, n), r.aborted) {
			let e = /* @__PURE__ */ Error("Attempt aborted");
			throw e.code = "ATTEMPT_ABORTED", e;
		}
		let t = async (e) => {
			if (n.handleError && await n.handleError(e, r, n), r.aborted || r.attemptsRemaining === 0) throw e;
			r.attemptNum++;
			let t = i(r, n);
			return t && await zj(t), a();
		};
		return r.attemptsRemaining > 0 && r.attemptsRemaining--, n.timeout ? new Promise((i, a) => {
			let o = setTimeout(() => {
				if (n.handleTimeout) try {
					i(n.handleTimeout(r, n));
				} catch (e) {
					a(e);
				}
				else {
					let e = /* @__PURE__ */ Error(`Retry timeout (attemptNum: ${r.attemptNum}, timeout: ${n.timeout})`);
					e.code = "ATTEMPT_TIMEOUT", a(e);
				}
			}, n.timeout);
			e(r, n).then((e) => {
				clearTimeout(o), i(e);
			}).catch((e) => {
				clearTimeout(o), t(e).then(i).catch(a);
			});
		}) : e(r, n).catch(t);
	}
	let o = n.calculateDelay ? n.calculateDelay(r, n) : n.initialDelay;
	if (o && await zj(o), r.attemptNum < 1 && n.initialJitter) {
		let e = i(r, n);
		e && await zj(e);
	}
	return a();
}
var Hj = (e, t) => {
	L(e, 0), Qp(e, Q_(t));
}, Uj = (e, t, n) => {
	L(e, 1), Qp(e, K_(t, n));
}, Wj = (e, t, n) => Uj(t, n, Sm(e)), Gj = (e, t, n, r) => {
	try {
		U_(t, Sm(e), n);
	} catch (e) {
		r?.(e), console.error("Caught error while handling a Yjs update", e);
	}
}, Kj = (e, t) => {
	L(e, 2), Qp(e, t);
}, qj = Gj, Jj = (e, t, n, r, i) => {
	let a = z(e);
	switch (a) {
		case 0:
			Wj(e, t, n);
			break;
		case 1:
			Gj(e, n, r, i);
			break;
		case 2:
			qj(e, n, r, i);
			break;
		default: throw Error("Unknown message type");
	}
	return a;
}, Yj, Xj = class {
	constructor() {
		this.callbacks = {};
	}
	on(e, t) {
		return this.callbacks[e] || (this.callbacks[e] = []), this.callbacks[e].push(t), this;
	}
	emit(e, ...t) {
		let n = this.callbacks[e];
		return n && n.forEach((e) => e.apply(this, t)), this;
	}
	off(e, t) {
		let n = this.callbacks[e];
		return n && (t ? this.callbacks[e] = n.filter((e) => e !== t) : delete this.callbacks[e]), this;
	}
	removeAllListeners() {
		this.callbacks = {};
	}
}, Zj = class {
	constructor(e) {
		this.data = e, this.encoder = Bp(), this.decoder = ym(new Uint8Array(this.data));
	}
	peekVarString() {
		return Em(this.decoder);
	}
	readVarUint() {
		return z(this.decoder);
	}
	readVarString() {
		return Tm(this.decoder);
	}
	readVarUint8Array() {
		return Sm(this.decoder);
	}
	writeVarUint(e) {
		return L(this.encoder, e);
	}
	writeVarString(e) {
		return Xp(this.encoder, e);
	}
	writeVarUint8Array(e) {
		return Qp(this.encoder, e);
	}
	length() {
		return Hp(this.encoder);
	}
}, Qj = /* @__PURE__ */ function(e) {
	return e[e.Sync = 0] = "Sync", e[e.Awareness = 1] = "Awareness", e[e.Auth = 2] = "Auth", e[e.QueryAwareness = 3] = "QueryAwareness", e[e.Stateless = 5] = "Stateless", e[e.CLOSE = 7] = "CLOSE", e[e.SyncStatus = 8] = "SyncStatus", e[e.Ping = 9] = "Ping", e[e.Pong = 10] = "Pong", e;
}({}), $j = /* @__PURE__ */ function(e) {
	return e.Connecting = "connecting", e.Connected = "connected", e.Disconnected = "disconnected", e;
}({}), eM = class {
	constructor() {
		this.encoder = Bp();
	}
	get(e) {
		return e.encoder;
	}
	toUint8Array() {
		return Up(this.encoder);
	}
}, tM = class extends eM {
	constructor(...e) {
		super(...e), this.type = Qj.CLOSE, this.description = "Ask the server to close the connection";
	}
	get(e) {
		return Xp(this.encoder, e.documentName), L(this.encoder, this.type), this.encoder;
	}
}, nM = (Yj = class e extends Xj {
	constructor(e) {
		super(), this.messageQueue = [], this.configuration = {
			url: "",
			autoConnect: !0,
			preserveTrailingSlash: !1,
			document: void 0,
			WebSocketPolyfill: void 0,
			messageReconnectTimeout: 3e4,
			delay: 1e3,
			initialDelay: 0,
			factor: 2,
			maxAttempts: 0,
			minDelay: 1e3,
			maxDelay: 3e4,
			jitter: !0,
			timeout: 0,
			onOpen: () => null,
			onConnect: () => null,
			onMessage: () => null,
			onOutgoingMessage: () => null,
			onStatus: () => null,
			onDisconnect: () => null,
			onClose: () => null,
			onMaxAttemptsFailed: () => null,
			onDestroy: () => null,
			onAwarenessUpdate: () => null,
			onAwarenessChange: () => null,
			handleTimeout: null,
			providerMap: /* @__PURE__ */ new Map()
		}, this.webSocket = null, this.webSocketHandlers = {}, this.shouldConnect = !0, this.status = $j.Disconnected, this.lastMessageReceived = 0, this.identifier = 0, this.intervals = { connectionChecker: null }, this.connectionAttempt = null, this.receivedOnOpenPayload = void 0, this.reconnectTimeout = null, this.closeTries = 0, this.setConfiguration(e), this.configuration.WebSocketPolyfill = e.WebSocketPolyfill ? e.WebSocketPolyfill : WebSocket, this.on("open", this.configuration.onOpen), this.on("open", this.onOpen.bind(this)), this.on("connect", this.configuration.onConnect), this.on("message", this.configuration.onMessage), this.on("outgoingMessage", this.configuration.onOutgoingMessage), this.on("status", this.configuration.onStatus), this.on("disconnect", this.configuration.onDisconnect), this.on("close", this.configuration.onClose), this.on("maxAttemptsFailed", this.configuration.onMaxAttemptsFailed), this.on("destroy", this.configuration.onDestroy), this.on("awarenessUpdate", this.configuration.onAwarenessUpdate), this.on("awarenessChange", this.configuration.onAwarenessChange), this.on("close", this.onClose.bind(this)), this.on("message", this.onMessage.bind(this)), this.intervals.connectionChecker = setInterval(this.checkConnection.bind(this), this.configuration.messageReconnectTimeout / 10), this.shouldConnect && this.connect();
	}
	async onOpen(e) {
		this.status = $j.Connected, this.emit("status", { status: $j.Connected }), this.receivedOnOpenPayload = e;
	}
	attach(e) {
		let t = e.effectiveName, n = this.configuration.providerMap.get(t);
		if (n && n !== e && n.isAuthenticated) throw Error(`Cannot attach two providers with the same effective name "${t}". Use sessionAwareness: true to multiplex providers with the same document name.`);
		this.configuration.providerMap.set(t, e), this.status === $j.Disconnected && this.shouldConnect && this.connect(), this.receivedOnOpenPayload && this.status === $j.Connected && e.onOpen(this.receivedOnOpenPayload);
	}
	detach(e) {
		let t = e.effectiveName;
		this.configuration.providerMap.has(t) && (e.send(tM, { documentName: t }), this.configuration.providerMap.delete(t));
	}
	setConfiguration(e = {}) {
		this.configuration = {
			...this.configuration,
			...e
		}, this.configuration.autoConnect || (this.shouldConnect = !1);
	}
	clearReconnectTimeout() {
		this.reconnectTimeout && (clearTimeout(this.reconnectTimeout), this.reconnectTimeout = null);
	}
	async connect() {
		if (this.status === $j.Connected) return;
		this.clearReconnectTimeout(), this.cancelWebsocketRetry && (this.cancelWebsocketRetry(), this.cancelWebsocketRetry = void 0), this.receivedOnOpenPayload = void 0, this.shouldConnect = !0;
		let { retryPromise: e, cancelFunc: t } = (() => {
			let e = !1;
			return {
				retryPromise: Vj(this.createWebSocketConnection.bind(this), {
					delay: this.configuration.delay,
					initialDelay: this.configuration.initialDelay,
					factor: this.configuration.factor,
					maxAttempts: this.configuration.maxAttempts,
					minDelay: this.configuration.minDelay,
					maxDelay: this.configuration.maxDelay,
					jitter: this.configuration.jitter,
					timeout: this.configuration.timeout,
					handleTimeout: this.configuration.handleTimeout,
					beforeAttempt: (t) => {
						(!this.shouldConnect || e) && t.abort();
					}
				}).catch((e) => {
					e?.code !== "ATTEMPT_ABORTED" && this.emit("maxAttemptsFailed", { error: e });
				}),
				cancelFunc: () => {
					e = !0;
				}
			};
		})();
		return this.cancelWebsocketRetry = t, e;
	}
	attachWebSocketListeners(e, t) {
		let { identifier: n } = e, r = (e) => this.emit("message", e), i = (e) => this.emit("close", { event: e }), a = (e) => this.emit("open", e), o = (e) => {
			t(e);
		};
		this.webSocketHandlers[n] = {
			message: r,
			close: i,
			open: a,
			error: o
		};
		let s = this.webSocketHandlers[e.identifier];
		Object.keys(s).forEach((t) => {
			e.addEventListener(t, s[t]);
		});
	}
	cleanupWebSocket() {
		if (!this.webSocket) return;
		let { identifier: e } = this.webSocket, t = this.webSocketHandlers[e];
		Object.keys(t).forEach((n) => {
			this.webSocket?.removeEventListener(n, t[n]), delete this.webSocketHandlers[e];
		}), this.webSocket.close(), this.webSocket = null;
	}
	createWebSocketConnection() {
		return new Promise((e, t) => {
			this.webSocket && (this.messageQueue = [], this.cleanupWebSocket()), this.lastMessageReceived = 0, this.identifier += 1;
			let n = new this.configuration.WebSocketPolyfill(this.url);
			n.binaryType = "arraybuffer", n.identifier = this.identifier, this.attachWebSocketListeners(n, t), this.webSocket = n, this.status = $j.Connecting, this.emit("status", { status: $j.Connecting }), this.connectionAttempt = {
				resolve: e,
				reject: t
			};
		});
	}
	onMessage(e) {
		this.resolveConnectionAttempt(), this.lastMessageReceived = Bm();
		let t = new Uint8Array(e.data);
		if (t.length === 1 && t[0] === Qj.Ping) {
			this.sendPong();
			return;
		}
		let n = new Zj(t).peekVarString();
		this.configuration.providerMap.get(n)?.onMessage(e);
	}
	sendPong() {
		let e = Bp();
		L(e, Qj.Pong), this.send(Up(e));
	}
	resolveConnectionAttempt() {
		this.connectionAttempt && (this.cancelWebsocketRetry = void 0, this.connectionAttempt.resolve(), this.connectionAttempt = null, this.status = $j.Connected, this.emit("status", { status: $j.Connected }), this.emit("connect"), this.messageQueue.forEach((e) => this.send(e)), this.messageQueue = []);
	}
	stopConnectionAttempt() {
		this.connectionAttempt = null;
	}
	rejectConnectionAttempt(e) {
		this.connectionAttempt?.reject(e), this.connectionAttempt = null;
	}
	checkConnection() {
		this.status === $j.Connected && this.lastMessageReceived && (this.configuration.messageReconnectTimeout >= Bm() - this.lastMessageReceived || (this.closeTries += 1, this.closeTries > 2 ? (this.onClose({ event: {
			code: 4408,
			reason: "forced"
		} }), this.closeTries = 0) : (this.webSocket?.close(), this.messageQueue = [])));
	}
	get serverUrl() {
		if (this.configuration.preserveTrailingSlash) return this.configuration.url;
		let e = this.configuration.url;
		for (; e[e.length - 1] === "/";) e = e.slice(0, e.length - 1);
		return e;
	}
	get url() {
		return this.serverUrl;
	}
	disconnect() {
		if (this.shouldConnect = !1, this.clearReconnectTimeout(), this.webSocket !== null) try {
			this.webSocket.close(), this.messageQueue = [];
		} catch (e) {
			console.error(e);
		}
	}
	parseQueuedMessage(e) {
		try {
			let t = ym(e);
			return {
				documentName: Tm(t),
				messageType: z(t)
			};
		} catch {
			return null;
		}
	}
	addToQueue(t) {
		if (t instanceof Uint8Array) {
			let n = this.parseQueuedMessage(t);
			n && e.DEDUPLICATABLE_TYPES.has(n.messageType) && (this.messageQueue = this.messageQueue.filter((e) => {
				if (!(e instanceof Uint8Array)) return !0;
				let t = this.parseQueuedMessage(e);
				return !t || t.documentName !== n.documentName || t.messageType !== n.messageType;
			}));
		}
		this.messageQueue.push(t);
	}
	send(e) {
		this.webSocket?.readyState === Mj.Open ? this.webSocket.send(e) : this.addToQueue(e);
	}
	onClose({ event: e }) {
		this.closeTries = 0, this.cleanupWebSocket(), this.connectionAttempt && this.rejectConnectionAttempt(/* @__PURE__ */ Error(`WebSocket closed before the connection was established (code: ${e?.code}, reason: ${e?.reason || "none"})`)), this.status = $j.Disconnected, this.emit("status", { status: $j.Disconnected }), this.emit("disconnect", { event: e }), !this.cancelWebsocketRetry && this.shouldConnect && (this.reconnectTimeout = setTimeout(() => {
			this.reconnectTimeout = null, this.connect();
		}, this.configuration.delay));
	}
	destroy() {
		this.emit("destroy"), clearInterval(this.intervals.connectionChecker), this.stopConnectionAttempt(), this.disconnect(), this.removeAllListeners(), this.cleanupWebSocket();
	}
}, Yj.DEDUPLICATABLE_TYPES = /* @__PURE__ */ new Set([Qj.Awareness, Qj.QueryAwareness]), Yj), rM = class {
	constructor(e) {
		this.message = e;
	}
	apply(e, t) {
		let { message: n } = this, r = n.readVarUint(), i = n.length();
		switch (r) {
			case Qj.Sync:
				this.applySyncMessage(e, t);
				break;
			case Qj.Awareness:
				this.applyAwarenessMessage(e);
				break;
			case Qj.Auth:
				this.applyAuthMessage(e);
				break;
			case Qj.QueryAwareness:
				this.applyQueryAwarenessMessage(e);
				break;
			case Qj.Stateless:
				e.receiveStateless(Tm(n.decoder));
				break;
			case Qj.SyncStatus:
				this.applySyncStatusMessage(e, wm(n.decoder) === 1);
				break;
			case Qj.CLOSE: {
				let t = {
					code: 1e3,
					reason: Tm(n.decoder)
				};
				e.onClose(), e.configuration.onClose({ event: t }), e.forwardClose({ event: t });
				break;
			}
			default: console.error(`Can’t apply message of unknown type: ${r}`);
		}
		n.length() > i + 1 && e.send(eM, { encoder: n.encoder });
	}
	applySyncMessage(e, t) {
		let { message: n } = this;
		n.writeVarUint(Qj.Sync);
		let r = Jj(n.decoder, n.encoder, e.document, e);
		t && r === 1 && (e.synced = !0);
	}
	applySyncStatusMessage(e, t) {
		t && e.decrementUnsyncedChanges();
	}
	applyAwarenessMessage(e) {
		if (!e.awareness) return;
		let { message: t } = this;
		Lj(e.awareness, t.readVarUint8Array(), e);
	}
	applyAuthMessage(e) {
		let { message: t } = this;
		Dj(t.decoder, e.sendToken.bind(e), e.permissionDeniedHandler.bind(e), e.authenticatedHandler.bind(e));
	}
	applyQueryAwarenessMessage(e) {
		if (!e.awareness) return;
		let { message: t } = this;
		t.writeVarUint(Qj.Awareness), t.writeVarUint8Array(Ij(e.awareness, Array.from(e.awareness.getStates().keys())));
	}
}, iM = class {
	constructor(e, t = {}) {
		this.message = new e(), this.encoder = this.message.get(t);
	}
	create() {
		return Up(this.encoder);
	}
	send(e) {
		e?.send(this.create());
	}
}, aM = "4.7.1", oM = class extends eM {
	constructor(...e) {
		super(...e), this.type = Qj.Auth, this.description = "Authentication";
	}
	get(e) {
		if (e.token === void 0) throw Error("The authentication message requires `token` as an argument.");
		return Xp(this.encoder, e.documentName), L(this.encoder, this.type), Ej(this.encoder, e.token), Xp(this.encoder, aM), this.encoder;
	}
}, sM = class extends eM {
	constructor(...e) {
		super(...e), this.type = Qj.Awareness, this.description = "Awareness states update";
	}
	get(e) {
		if (e.awareness === void 0) throw Error("The awareness message requires awareness as an argument");
		if (e.clients === void 0) throw Error("The awareness message requires clients as an argument");
		Xp(this.encoder, e.documentName), L(this.encoder, this.type);
		let t;
		return t = e.states === void 0 ? Ij(e.awareness, e.clients) : Ij(e.awareness, e.clients, e.states), Qp(this.encoder, t), this.encoder;
	}
}, cM = class extends eM {
	constructor(...e) {
		super(...e), this.type = Qj.Stateless, this.description = "A stateless message";
	}
	get(e) {
		return Xp(this.encoder, e.documentName), L(this.encoder, this.type), Xp(this.encoder, e.payload ?? ""), this.encoder;
	}
}, lM = class extends eM {
	constructor(...e) {
		super(...e), this.type = Qj.Sync, this.description = "First sync step";
	}
	get(e) {
		if (e.document === void 0) throw Error("The sync step one message requires document as an argument");
		return Xp(this.encoder, e.documentName), L(this.encoder, this.type), Hj(this.encoder, e.document), this.encoder;
	}
}, uM = class extends eM {
	constructor(...e) {
		super(...e), this.type = Qj.Sync, this.description = "A document update";
	}
	get(e) {
		return Xp(this.encoder, e.documentName), L(this.encoder, this.type), Kj(this.encoder, e.update), this.encoder;
	}
}, dM = class extends Error {
	constructor(...e) {
		super(...e), this.code = 1001;
	}
}, fM = class extends Xj {
	get effectiveName() {
		return this.configuration.sessionAwareness ? Aj(this.configuration.name, this.sessionId) : this.configuration.name;
	}
	constructor(e) {
		super(), this.configuration = {
			name: "",
			document: void 0,
			awareness: void 0,
			token: null,
			sessionAwareness: !1,
			forceSyncInterval: !1,
			flushDelay: !1,
			onAuthenticated: () => null,
			onAuthenticationFailed: () => null,
			onOpen: () => null,
			onConnect: () => null,
			onMessage: () => null,
			onOutgoingMessage: () => null,
			onSynced: () => null,
			onStatus: () => null,
			onDisconnect: () => null,
			onClose: () => null,
			onDestroy: () => null,
			onAwarenessUpdate: () => null,
			onAwarenessChange: () => null,
			onStateless: () => null,
			onUnsyncedChanges: () => null
		}, this.isSynced = !1, this.unsyncedChanges = 0, this.isAuthenticated = !1, this.authorizedScope = void 0, this.manageSocket = !1, this._isAttached = !1, this.pendingUpdates = [], this.pendingAwarenessClients = /* @__PURE__ */ new Set(), this.flushTimeout = null, this.sessionId = Math.random().toString(36).slice(2), this.intervals = { forceSync: null }, this.boundDocumentUpdateHandler = this.documentUpdateHandler.bind(this), this.boundAwarenessUpdateHandler = this.awarenessUpdateHandler.bind(this), this.boundPageHide = this.pageHide.bind(this), this.boundOnOpen = this.onOpen.bind(this), this.boundOnClose = this.onClose.bind(this), this.forwardConnect = () => this.emit("connect"), this.forwardStatus = (e) => this.emit("status", e), this.forwardClose = (e) => this.emit("close", e), this.forwardDisconnect = (e) => this.emit("disconnect", e), this.forwardDestroy = () => this.emit("destroy"), this.setConfiguration(e), this.configuration.document = e.document ? e.document : new D_(), this.configuration.awareness = e.awareness === void 0 ? new Pj(this.document) : e.awareness, this.on("open", this.configuration.onOpen), this.on("message", this.configuration.onMessage), this.on("outgoingMessage", this.configuration.onOutgoingMessage), this.on("synced", this.configuration.onSynced), this.on("destroy", this.configuration.onDestroy), this.on("awarenessUpdate", this.configuration.onAwarenessUpdate), this.on("awarenessChange", this.configuration.onAwarenessChange), this.on("stateless", this.configuration.onStateless), this.on("unsyncedChanges", this.configuration.onUnsyncedChanges), this.on("authenticated", this.configuration.onAuthenticated), this.on("authenticationFailed", this.configuration.onAuthenticationFailed), this.awareness?.on("update", () => {
			this.emit("awarenessUpdate", { states: Oj(this.awareness.getStates()) });
		}), this.awareness?.on("change", () => {
			this.emit("awarenessChange", { states: Oj(this.awareness.getStates()) });
		}), this.document.on("update", this.boundDocumentUpdateHandler), this.awareness?.on("update", this.boundAwarenessUpdateHandler), this.registerEventListeners(), this.configuration.forceSyncInterval && typeof this.configuration.forceSyncInterval == "number" && (this.intervals.forceSync = setInterval(this.forceSync.bind(this), this.configuration.forceSyncInterval)), this.manageSocket && this.attach();
	}
	setConfiguration(e = {}) {
		e.websocketProvider || (this.manageSocket = !0, this.configuration.websocketProvider = new nM(e)), this.configuration = {
			...this.configuration,
			...e
		};
	}
	get document() {
		return this.configuration.document;
	}
	get isAttached() {
		return this._isAttached;
	}
	get awareness() {
		return this.configuration.awareness;
	}
	get hasUnsyncedChanges() {
		return this.unsyncedChanges > 0;
	}
	resetUnsyncedChanges() {
		this.unsyncedChanges = 1, this.emit("unsyncedChanges", { number: this.unsyncedChanges });
	}
	incrementUnsyncedChanges() {
		this.unsyncedChanges += 1, this.emit("unsyncedChanges", { number: this.unsyncedChanges });
	}
	decrementUnsyncedChanges() {
		this.unsyncedChanges > 0 && --this.unsyncedChanges, this.unsyncedChanges === 0 && (this.synced = !0), this.emit("unsyncedChanges", { number: this.unsyncedChanges });
	}
	forceSync() {
		this.resetUnsyncedChanges(), this.send(lM, {
			document: this.document,
			documentName: this.effectiveName
		});
	}
	pageHide() {
		this.awareness && Fj(this.awareness, [this.document.clientID], "page hide"), this.flushPendingUpdates();
	}
	registerEventListeners() {
		typeof window < "u" && "addEventListener" in window && window.addEventListener("pagehide", this.boundPageHide);
	}
	sendStateless(e) {
		this.send(cM, {
			documentName: this.effectiveName,
			payload: e
		});
	}
	async sendToken() {
		let e;
		try {
			e = await this.getToken();
		} catch (e) {
			this.permissionDeniedHandler(`Failed to get token during sendToken(): ${e}`);
			return;
		}
		this.send(oM, {
			token: e ?? "",
			documentName: this.effectiveName
		});
	}
	get batchingEnabled() {
		return !!this.configuration.flushDelay && typeof this.configuration.flushDelay == "number";
	}
	documentUpdateHandler(e, t) {
		if (t !== this) {
			if (!this.batchingEnabled) {
				this.incrementUnsyncedChanges(), this.send(uM, {
					update: e,
					documentName: this.effectiveName
				});
				return;
			}
			this.pendingUpdates.length === 0 && this.incrementUnsyncedChanges(), this.pendingUpdates.push(e), this.scheduleFlush();
		}
	}
	awarenessUpdateHandler({ added: e, updated: t, removed: n }, r) {
		if (r === this) return;
		let i = e.concat(t).concat(n);
		if (!this.batchingEnabled) {
			this.send(sM, {
				awareness: this.awareness,
				clients: i,
				documentName: this.effectiveName
			});
			return;
		}
		for (let e of i) this.pendingAwarenessClients.add(e);
		this.scheduleFlush();
	}
	scheduleFlush() {
		this.flushTimeout === null && (this.flushTimeout = setTimeout(() => {
			this.flushTimeout = null, this.flushPendingUpdates();
		}, this.configuration.flushDelay));
	}
	flushPendingUpdates() {
		if (this.flushTimeout !== null && (clearTimeout(this.flushTimeout), this.flushTimeout = null), this.pendingUpdates.length > 0) {
			let e = this.pendingUpdates.length === 1 ? this.pendingUpdates[0] : Qv(this.pendingUpdates);
			this.pendingUpdates = [], this.send(uM, {
				update: e,
				documentName: this.effectiveName
			});
		}
		if (this.pendingAwarenessClients.size > 0) {
			let e = Array.from(this.pendingAwarenessClients);
			this.pendingAwarenessClients.clear(), this.send(sM, {
				awareness: this.awareness,
				clients: e,
				documentName: this.effectiveName
			});
		}
	}
	get synced() {
		return this.isSynced;
	}
	set synced(e) {
		this.isSynced !== e && (this.isSynced = e, e && this.emit("synced", { state: e }));
	}
	receiveStateless(e) {
		this.emit("stateless", { payload: e });
	}
	async connect() {
		if (this.manageSocket) return this.configuration.websocketProvider.connect();
		console.warn("HocuspocusProvider::connect() is deprecated and does not do anything. Please connect/disconnect on the websocketProvider, or attach/deattach providers.");
	}
	disconnect() {
		if (this.manageSocket) return this.configuration.websocketProvider.disconnect();
		console.warn("HocuspocusProvider::disconnect() is deprecated and does not do anything. Please connect/disconnect on the websocketProvider, or attach/deattach providers.");
	}
	async onOpen(e) {
		this.isAuthenticated = !1, this.emit("open", { event: e }), await this.sendToken(), this.startSync();
	}
	async getToken() {
		return typeof this.configuration.token == "function" ? await this.configuration.token() : this.configuration.token;
	}
	startSync() {
		this.resetUnsyncedChanges(), this.send(lM, {
			document: this.document,
			documentName: this.effectiveName
		}), this.awareness && this.awareness.getLocalState() !== null && this.send(sM, {
			awareness: this.awareness,
			clients: [this.document.clientID],
			documentName: this.effectiveName
		});
	}
	send(e, t) {
		if (!this._isAttached) return;
		let n = new iM(e, t);
		this.emit("outgoingMessage", { message: n.message }), n.send(this.configuration.websocketProvider);
	}
	onMessage(e) {
		let t = new Zj(e.data), { documentName: n } = jj(t.readVarString());
		t.writeVarString(this.effectiveName), this.emit("message", {
			event: e,
			message: new Zj(e.data)
		}), new rM(t).apply(this, !0);
	}
	onClose() {
		this.isAuthenticated = !1, this.synced = !1, this.flushTimeout !== null && (clearTimeout(this.flushTimeout), this.flushTimeout = null), this.pendingUpdates = [], this.pendingAwarenessClients.clear(), this.awareness && Fj(this.awareness, Array.from(this.awareness.getStates().keys()).filter((e) => e !== this.document.clientID), this);
	}
	destroy() {
		this.emit("destroy"), this.intervals.forceSync && clearInterval(this.intervals.forceSync), this.awareness && (Fj(this.awareness, [this.document.clientID], "provider destroy"), this.awareness.off("update", this.boundAwarenessUpdateHandler), this.awareness.destroy()), this.flushPendingUpdates(), this.document.off("update", this.boundDocumentUpdateHandler), this.removeAllListeners(), this.detach(), this.manageSocket && this.configuration.websocketProvider.destroy(), typeof window < "u" && "removeEventListener" in window && window.removeEventListener("pagehide", this.boundPageHide);
	}
	detach() {
		this.configuration.websocketProvider.off("connect", this.configuration.onConnect), this.configuration.websocketProvider.off("connect", this.forwardConnect), this.configuration.websocketProvider.off("status", this.forwardStatus), this.configuration.websocketProvider.off("status", this.configuration.onStatus), this.configuration.websocketProvider.off("open", this.boundOnOpen), this.configuration.websocketProvider.off("close", this.boundOnClose), this.configuration.websocketProvider.off("close", this.configuration.onClose), this.configuration.websocketProvider.off("close", this.forwardClose), this.configuration.websocketProvider.off("disconnect", this.configuration.onDisconnect), this.configuration.websocketProvider.off("disconnect", this.forwardDisconnect), this.configuration.websocketProvider.off("destroy", this.configuration.onDestroy), this.configuration.websocketProvider.off("destroy", this.forwardDestroy), this.configuration.websocketProvider.detach(this), this._isAttached = !1;
	}
	attach() {
		this._isAttached || (this.configuration.websocketProvider.on("connect", this.configuration.onConnect), this.configuration.websocketProvider.on("connect", this.forwardConnect), this.configuration.websocketProvider.on("status", this.configuration.onStatus), this.configuration.websocketProvider.on("status", this.forwardStatus), this.configuration.websocketProvider.on("open", this.boundOnOpen), this.configuration.websocketProvider.on("close", this.boundOnClose), this.configuration.websocketProvider.on("close", this.configuration.onClose), this.configuration.websocketProvider.on("close", this.forwardClose), this.configuration.websocketProvider.on("disconnect", this.configuration.onDisconnect), this.configuration.websocketProvider.on("disconnect", this.forwardDisconnect), this.configuration.websocketProvider.on("destroy", this.configuration.onDestroy), this.configuration.websocketProvider.on("destroy", this.forwardDestroy), this.configuration.websocketProvider.attach(this), this._isAttached = !0);
	}
	permissionDeniedHandler(e) {
		this.emit("authenticationFailed", { reason: e }), this.isAuthenticated = !1;
	}
	authenticatedHandler(e) {
		this.isAuthenticated = !0, this.authorizedScope = e, this.emit("authenticated", { scope: e });
	}
	setAwarenessField(e, t) {
		if (!this.awareness) throw new dM(`Cannot set awareness field "${e}" to ${JSON.stringify(t)}. You have disabled Awareness for this provider by explicitly passing awareness: null in the provider configuration.`);
		this.awareness.setLocalStateField(e, t);
	}
};
//#endregion
//#region wwwroot/js/indexeddb-connection.js
function pM(e, t, n) {
	let r = null, i = null, a = !1, o = (e) => {
		i === e && (i = null, r = null), e.close();
	};
	async function s() {
		for (let s = 0;; s++) {
			if (a) throw new DOMException("Local storage connection was closed.", "InvalidStateError");
			try {
				return await new Promise((s, c) => {
					let l = indexedDB.open(e, t), u = !1;
					l.onupgradeneeded = () => n(l.result, l.transaction), l.onblocked = () => {
						u = !0, c(new DOMException("Another tab is blocking local storage. Close other Prosa tabs and retry; retained data is unchanged.", "InvalidStateError"));
					}, l.onerror = () => {
						u = !0, c(l.error ?? new DOMException("Local storage could not be opened.", "UnknownError"));
					}, l.onsuccess = () => {
						let e = l.result;
						if (u || a) {
							e.close(), c(new DOMException("Local storage connection was closed.", "InvalidStateError"));
							return;
						}
						i = e, e.onversionchange = () => o(e), e.onclose = () => {
							i === e && (i = null, r = null);
						}, s(e);
					};
				});
			} catch (e) {
				if (a || s >= 2 || !["AbortError", "UnknownError"].includes(e?.name)) throw e;
				await new Promise((e) => setTimeout(e, 50 * (s + 1)));
			}
		}
	}
	async function c() {
		if (a) throw new DOMException("Local storage connection was closed.", "InvalidStateError");
		if (!r) {
			let e = s();
			r = e, e.catch(() => {
				r === e && (r = null);
			});
		}
		return r;
	}
	return {
		get: c,
		async transaction(e, t = "readonly", n) {
			for (let r = 0;; r++) {
				let i = await c();
				try {
					return i.transaction(e, t, n);
				} catch (e) {
					if (r || a || e?.name !== "InvalidStateError") throw e;
					o(i);
				}
			}
		},
		async close() {
			a = !0;
			let e = r;
			r = null, i = null;
			try {
				(await e)?.close();
			} catch {}
		}
	};
}
//#endregion
//#region src/collaboration/journal.ts
var mM = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/, hM = /^[0-9a-f]{64}$/, gM = (e) => typeof e == "string" && mM.test(e) && e !== "00000000-0000-0000-0000-000000000000", _M = (e, t) => Object.keys(e).every((e) => t.includes(e)), vM = (e = "collaboration_journal_invalid") => {
	throw Error(e);
};
function yM(e, t) {
	if (e === t) return !0;
	if (!e || !t || typeof e != "object" || typeof t != "object" || Array.isArray(e) !== Array.isArray(t)) return !1;
	let n = Object.keys(e).sort(), r = Object.keys(t).sort();
	return n.length === r.length && n.every((n, i) => n === r[i] && yM(e[n], t[n]));
}
async function bM(e) {
	let t = await crypto.subtle.digest("SHA-256", typeof e == "string" ? new TextEncoder().encode(e) : new Uint8Array(e));
	return [...new Uint8Array(t)].map((e) => e.toString(16).padStart(2, "0")).join("");
}
function xM(e) {
	let t = "";
	for (let n = 0; n < e.length; n += 8192) t += String.fromCharCode(...e.subarray(n, n + 8192));
	return btoa(t);
}
function SM(e, t = 16777216) {
	if (typeof e != "string" || e.length > Math.ceil(t / 3) * 4 || !/^[A-Za-z0-9+/]+={0,2}$/.test(e)) return vM();
	let n;
	try {
		n = atob(e);
	} catch {
		return vM();
	}
	let r = new Uint8Array(n.length);
	for (let e = 0; e < n.length; e++) r[e] = n.charCodeAt(e);
	return !r.length || r.length > t || xM(r) !== e ? vM() : r;
}
function CM(e) {
	let t = new URL(e.backend);
	return !(t.protocol === "https:" || t.protocol === "http:" && [
		"localhost",
		"127.0.0.1",
		"[::1]"
	].includes(t.hostname)) || t.username || t.password || t.search || t.hash || typeof e.ownerId != "string" || !e.ownerId.trim() || e.ownerId.length > 256 || /[\u0000-\u001f\u007f-\u009f]/.test(e.ownerId) || !/^[A-Za-z0-9_-]{1,64}$/.test(e.environment) || ![
		e.documentId,
		e.pageId,
		e.generation
	].every((e) => gM(e?.toLowerCase())) || !Number.isSafeInteger(e.membershipRevision ?? 0) || (e.membershipRevision ?? 0) < 0 || e.support?.contentContractVersion !== 2 || e.support?.protocolVersion !== 1 || !hM.test(e.support?.schemaFingerprint) || !_M(e.support, [
		"contentContractVersion",
		"protocolVersion",
		"schemaFingerprint"
	]) ? vM() : Object.freeze({
		backend: t.href.replace(/\/+$/, "") + "/",
		ownerId: e.ownerId,
		environment: e.environment,
		documentId: e.documentId.toLowerCase(),
		pageId: e.pageId.toLowerCase(),
		generation: e.generation.toLowerCase(),
		support: Object.freeze({ ...e.support }),
		...e.membershipRevision ? { membershipRevision: e.membershipRevision } : {}
	});
}
function wM(e) {
	return e = CM(e), [
		e.backend,
		e.ownerId,
		e.environment,
		e.documentId,
		e.pageId,
		e.generation,
		...e.membershipRevision ? [String(e.membershipRevision)] : []
	].join("\n");
}
function TM(e, t) {
	(!t || t.pageId !== e.scope.pageId || t.generation !== e.scope.generation || t.installationId !== e.installationId || !gM(t.updateId) || !hM.test(t.sha256) || !Number.isSafeInteger(t.commitSequence) || t.commitSequence < 1 || !Number.isSafeInteger(t.committedAtUnixMilliseconds) || t.committedAtUnixMilliseconds < 1 || !_M(t, [
		"pageId",
		"generation",
		"installationId",
		"updateId",
		"sha256",
		"commitSequence",
		"committedAtUnixMilliseconds"
	])) && vM("collaboration_receipt_invalid");
}
async function EM(e, t) {
	(!t || t.type !== "commitUpdate" || t.protocolVersion !== 1 || t.documentId !== e.scope.documentId || t.pageId !== e.scope.pageId || t.generation !== e.scope.generation || t.installationId !== e.installationId || !gM(t.updateId) || !hM.test(t.sha256) || !_M(t, [
		"type",
		"protocolVersion",
		"documentId",
		"pageId",
		"generation",
		"installationId",
		"updateId",
		"update",
		"sha256"
	]) || await bM(SM(t.update, 524288)) !== t.sha256) && vM();
}
async function DM(e, t) {
	(!e || e.version !== 1 || !yM(CM(e.scope), CM(t)) || !gM(e.installationId) || !Number.isSafeInteger(e.revision) || e.revision < 0 || !hM.test(e.stateSha256) || !_M(e, [
		"version",
		"scope",
		"installationId",
		"revision",
		"state",
		"stateSha256",
		"pending",
		"receipts",
		"recoveryReason"
	]) || !Array.isArray(e.pending) || !Array.isArray(e.receipts) || e.pending.length > 4096 || e.receipts.length > 5e4 || ![
		null,
		"access_unavailable",
		"generation_changed",
		"schema_changed"
	].includes(e.recoveryReason) || await bM(SM(e.state)) !== e.stateSha256) && vM();
	let n = /* @__PURE__ */ new Set(), r = /* @__PURE__ */ new Set(), i = 0;
	for (let t of e.pending) await EM(e, t), n.has(t.updateId) && vM(), n.add(t.updateId), i += t.update.length;
	for (let t of e.receipts) TM(e, t), (n.has(t.updateId) || r.has(t.commitSequence)) && vM(), n.add(t.updateId), r.add(t.commitSequence);
	return (i > 46137344 || new TextEncoder().encode(JSON.stringify(e)).length > 67108864) && vM("collaboration_journal_full"), e;
}
var OM = class {
	constructor(e, t = "prosa-collaboration-v1") {
		this.scope = Object.freeze(CM(e)), this.key = bM(wM(this.scope)), this.channel = new BroadcastChannel(t), this.connection = pM(t, 1, (e) => {
			e.createObjectStore("pages", { keyPath: "key" }), e.createObjectStore("settings", { keyPath: "key" });
		});
	}
	async assertCurrent() {}
	async readRaw() {
		let e = await this.key, t = await this.connection.transaction("pages");
		return new Promise((n, r) => {
			let i = t.objectStore("pages").get(e);
			t.oncomplete = () => n(i.result), t.onabort = () => r(t.error), t.onerror = () => {};
		});
	}
	async unpack(e) {
		return e ? typeof e.payload != "string" || e.payload.length > 67108864 || await bM(e.payload) !== e.sha256 ? vM() : DM(JSON.parse(e.payload), this.scope) : null;
	}
	async load() {
		return this.unpack(await this.readRaw());
	}
	async replace(e, t) {
		await DM(t, this.scope);
		let n = await this.key, r = JSON.stringify(t), i = await bM(r), a = await this.connection.transaction("pages", "readwrite", { durability: "strict" });
		return await new Promise((t, o) => {
			let s = !1, c = a.objectStore("pages"), l = c.get(n);
			l.onsuccess = () => {
				if (l.result?.sha256 !== e?.sha256 || l.result?.payload !== e?.payload) {
					s = !0, a.abort();
					return;
				}
				c.put({
					key: n,
					payload: r,
					sha256: i
				});
			}, a.oncomplete = () => t(), a.onerror = () => {}, a.onabort = () => o(s ? /* @__PURE__ */ Error("collaboration_journal_revision_changed") : a.error ?? /* @__PURE__ */ Error("collaboration_storage_failed"));
		}), this.channel.postMessage({ key: n }), structuredClone(t);
	}
	async initialize(e) {
		let t = await this.readRaw();
		if (t) return await this.unpack(t);
		let n = await this.connection.transaction("settings", "readwrite", { durability: "strict" }), r = await new Promise((e, t) => {
			let r = n.objectStore("settings"), i = r.get("installation"), a;
			i.onsuccess = () => {
				if (a = i.result?.value ?? crypto.randomUUID(), !gM(a)) {
					n.abort();
					return;
				}
				r.put({
					key: "installation",
					value: a
				});
			}, n.oncomplete = () => e(a), n.onabort = () => t(n.error ?? /* @__PURE__ */ Error("collaboration_installation_invalid")), n.onerror = () => {};
		}), i = {
			version: 1,
			scope: this.scope,
			installationId: r,
			revision: 0,
			state: xM(e),
			stateSha256: await bM(e),
			pending: [],
			receipts: [],
			recoveryReason: null
		};
		try {
			return await this.replace(void 0, i);
		} catch (e) {
			if (String(e).includes("revision_changed")) return await this.load();
			throw e;
		}
	}
	async save(e, t, n) {
		let r = await this.readRaw(), i = await this.unpack(r);
		if (!i) return vM();
		if (n) {
			await EM(i, n);
			let e = i.pending.find((e) => e.updateId === n.updateId), t = i.receipts.find((e) => e.updateId === n.updateId);
			if (e || t) return e && !yM(e, n) || t && t.sha256 !== n.sha256 ? vM() : i;
		}
		return i.revision === e ? this.replace(r, {
			...i,
			revision: i.revision + 1,
			state: xM(t),
			stateSha256: await bM(t),
			pending: n ? [...i.pending, n] : i.pending
		}) : vM("collaboration_journal_revision_changed");
	}
	async mutate(e) {
		for (let t = 0; t < 8; t++) {
			let t = await this.readRaw(), n = await this.unpack(t);
			if (!n) return vM();
			let r = e(n);
			if (r === n) return n;
			try {
				return await this.replace(t, r);
			} catch (e) {
				if (!String(e).includes("revision_changed")) throw e;
			}
		}
		return vM("collaboration_journal_busy");
	}
	async acknowledge(e) {
		return this.mutate((t) => {
			TM(t, e);
			let n = t.receipts.find((t) => t.updateId === e.updateId);
			if (n) return yM(n, e) ? t : vM("collaboration_receipt_invalid");
			let r = t.pending.find((t) => t.updateId === e.updateId);
			return !r || r.sha256 !== e.sha256 ? vM("collaboration_receipt_invalid") : {
				...t,
				revision: t.revision + 1,
				pending: t.pending.filter((t) => t.updateId !== e.updateId),
				receipts: [...t.receipts, e]
			};
		});
	}
	async recovery(e) {
		return this.mutate((t) => ({
			...t,
			revision: t.revision + 1,
			recoveryReason: e
		}));
	}
	watch(e) {
		let t = async (t) => {
			t.data?.key === await this.key && e();
		};
		return this.channel.addEventListener("message", t), () => this.channel.removeEventListener("message", t);
	}
	async listRecovery() {
		let e = await this.connection.transaction("pages"), t = await new Promise((t, n) => {
			let r = e.objectStore("pages").getAll();
			e.oncomplete = () => t(r.result), e.onabort = () => n(e.error);
		}), n = [];
		for (let e of t) {
			if (await bM(e.payload) !== e.sha256) continue;
			let t = JSON.parse(e.payload);
			t.scope.backend === this.scope.backend && t.scope.ownerId === this.scope.ownerId && t.scope.environment === this.scope.environment && (await DM(t, t.scope), (t.pending.length || t.recoveryReason) && n.push(t.scope));
		}
		return n;
	}
	async close() {
		this.channel.close(), await this.connection.close();
	}
}, kM = class {
	constructor(e, t) {
		this.bridge = e, this.scope = CM(t);
	}
	async assertCurrent() {
		await this.bridge.invokeMethodAsync("IsCurrent") || vM("collaboration_account_changed");
	}
	async call(e, ...t) {
		let n = await this.bridge.invokeMethodAsync(e, ...t);
		return n ? DM(n, this.scope) : null;
	}
	load() {
		return this.call("Load");
	}
	async initialize(e) {
		return await this.call("Initialize", xM(e), await bM(e));
	}
	async save(e, t, n) {
		return await this.call("Save", {
			revision: e,
			state: xM(t),
			stateSha256: await bM(t),
			envelope: n ?? null
		});
	}
	async acknowledge(e) {
		return await this.call("Acknowledge", e);
	}
	async recovery(e) {
		return await this.call("Recovery", e);
	}
}, AM = class {
	constructor(e = "prosa-collaboration-account-v1") {
		this.epoch = 0, this.identity = null, this.generation = 0, this.listeners = /* @__PURE__ */ new Set(), e && typeof BroadcastChannel < "u" && (this.channel = new BroadcastChannel(e), this.channel.onmessage = (e) => {
			if (e.data?.identity !== this.identity || e.data?.generation !== this.generation) {
				this.identity = null, this.epoch++;
				for (let e of [...this.listeners]) e();
			}
		});
	}
	activate(e, t = 0) {
		let n = e ? [
			CM(e).backend,
			e.ownerId,
			e.environment
		].join("\n") : null;
		if (n !== this.identity || t !== this.generation) {
			this.identity = n, this.generation = t, this.epoch++, this.channel?.postMessage({
				identity: n,
				generation: t
			});
			for (let e of [...this.listeners]) e();
		}
	}
	capture(e) {
		let t = [
			CM(e).backend,
			e.ownerId,
			e.environment
		].join("\n"), n = this.epoch;
		if (t !== this.identity) throw Error("collaboration_account_changed");
		return {
			valid: () => n === this.epoch && t === this.identity,
			invalidate: () => {
				n === this.epoch && this.activate(null, this.generation);
			},
			subscribe: (e) => {
				let r = () => {
					(n !== this.epoch || t !== this.identity) && e();
				};
				return this.listeners.add(r), () => this.listeners.delete(r);
			}
		};
	}
	close() {
		this.channel?.close(), this.identity = null, this.epoch++;
		for (let e of [...this.listeners]) e();
		this.listeners.clear();
	}
}, jM = ru(sj(!0)), MM = Symbol("journal-committed-remote");
function NM(e) {
	wj(e);
	let t = cC(e, "default");
	gj(t), jM.nodeFromJSON(t).check();
}
function PM(e, t, n) {
	let r = `${e.environment}/${e.documentId}/${e.pageId}/${e.generation}`, i = new URL(t.webSocketUrl);
	if (t.roomName !== r || n && t.readOnly || !t.ticket || t.ticket.length > 8192 || t.expiresAtUnixMilliseconds <= Date.now() || !(i.protocol === "wss:" || i.protocol === "ws:" && [
		"localhost",
		"127.0.0.1",
		"[::1]"
	].includes(i.hostname)) || i.username || i.password || i.search || i.hash) throw Error("collaboration_access_unavailable");
	return r;
}
async function FM(e) {
	if (!e.getTicket) throw Error("collaboration_offline_checkpoint_missing");
	let t = await e.getTicket(crypto.randomUUID());
	if (await e.store.assertCurrent(), !e.account.valid()) throw Error("collaboration_account_changed");
	let n = PM(e.store.scope, t, !1);
	return new Promise((r, i) => {
		let a = new D_(), o, s, c = !1, l = () => {}, u = setTimeout(() => d(/* @__PURE__ */ Error("collaboration_transport_unavailable")), 15e3), d = (e, t) => {
			c || (c = !0, clearTimeout(u), l(), o?.destroy(), s?.destroy(), a.destroy(), e ? i(e) : r(t));
		};
		l = e.account.subscribe(() => d(/* @__PURE__ */ Error("collaboration_account_changed"))), s = new nM({
			url: t.webSocketUrl,
			autoConnect: !1,
			maxAttempts: 1
		}), o = new fM({
			websocketProvider: s,
			name: n,
			token: t.ticket,
			document: a,
			awareness: null,
			onSynced: ({ state: e }) => {
				if (e) try {
					NM(a), d(void 0, K_(a));
				} catch {
					d(/* @__PURE__ */ Error("collaboration_state_unsupported"));
				}
			},
			onAuthenticationFailed: () => d(/* @__PURE__ */ Error("collaboration_access_unavailable")),
			onClose: () => d(/* @__PURE__ */ Error("collaboration_transport_unavailable"))
		}), o.attach(), s.connect().catch(() => d(/* @__PURE__ */ Error("collaboration_transport_unavailable")));
	});
}
var IM = class e {
	watchPresence(e) {
		return this.presenceListeners.add(e), e(this.presenceMembers), () => {
			this.presenceListeners.delete(e);
		};
	}
	clearPresence() {
		this.presenceMembers = [];
		for (let e of this.presenceListeners) e([]);
	}
	publishPresence(e) {
		this.options.interactionsEnabled && this.current() && this.synced && !this.page.recoveryReason && this.provider?.sendStateless(JSON.stringify({
			type: "presence",
			cursor: this.status.remoteDurable ? e : null
		}));
	}
	constructor(e, t, n) {
		this.options = e, this.jobs = [], this.running = null, this.storageError = null, this.changedAccount = !1, this.disposed = !1, this.synced = !1, this.wireGeneration = 0, this.sending = null, this.manuallyOffline = !0, this.resumeRecovery = !1, this.retries = 0, this.statusListeners = /* @__PURE__ */ new Set(), this.presenceListeners = /* @__PURE__ */ new Set(), this.presenceMembers = [], this.localUpdate = (e, t, n, r) => {
			if (!r.local || t === MM || !this.current()) return;
			let i = new Uint8Array(e), a = K_(this.doc), o = crypto.randomUUID();
			this.enqueue(async () => {
				let e = {
					type: "commitUpdate",
					protocolVersion: 1,
					documentId: this.scope.documentId,
					pageId: this.scope.pageId,
					generation: this.scope.generation,
					installationId: this.page.installationId,
					updateId: o,
					update: xM(i),
					sha256: await bM(i)
				};
				await this.persist(a, e);
			}).catch(() => {});
		}, this.doc = n, this.page = t, this.scope = Object.freeze(CM(e.store.scope)), this.stopAccount = e.account.subscribe(() => this.invalidate()), this.stopWatch = e.store.watch?.(() => {
			this.current() && this.enqueue(() => this.reload()).catch(() => {});
		}), n.on("update", this.localUpdate), this.emit();
	}
	static async open(t) {
		if (!t.account.valid()) throw Error("collaboration_account_changed");
		await t.store.assertCurrent();
		let n = await dN();
		if (!yM(t.store.scope.support, n)) throw Error("collaboration_upgrade_required");
		let r = await t.store.load();
		if (!r) {
			let e = t.committedSeed ?? await FM(t), n = new D_();
			try {
				U_(n, e), NM(n);
			} finally {
				n.destroy();
			}
			if (await t.store.assertCurrent(), !t.account.valid()) throw Error("collaboration_account_changed");
			r = await t.store.initialize(e);
		}
		let i = new D_();
		try {
			if (U_(i, SM(r.state), MM), NM(i), await t.store.assertCurrent(), !t.account.valid()) throw Error("collaboration_account_changed");
			return new e(t, r, i);
		} catch (e) {
			throw i.destroy(), e;
		}
	}
	current() {
		return !this.disposed && !this.changedAccount && this.options.account.valid();
	}
	async requireCurrent() {
		if (!this.current() || (await this.options.store.assertCurrent(), !this.current())) throw Error("collaboration_account_changed");
	}
	get status() {
		let e = this.jobs.length === 0 && !this.storageError;
		return Object.freeze({
			localDurable: e,
			remoteDurable: this.current() && !this.page.recoveryReason && e && this.page.pending.length === 0 && (this.page.receipts.length > 0 || this.synced),
			pending: this.page.pending.length,
			queued: this.jobs.length,
			connected: this.synced,
			error: this.storageError,
			recoveryReason: this.page.recoveryReason,
			accountChanged: this.changedAccount
		});
	}
	get installationId() {
		return this.page.installationId;
	}
	get pending() {
		return structuredClone(this.page.pending);
	}
	watchStatus(e) {
		return this.statusListeners.add(e), e(this.status), () => this.statusListeners.delete(e);
	}
	emit() {
		let e = this.status;
		this.options.onStatus?.(e);
		for (let t of this.statusListeners) t(e);
	}
	enqueue(e) {
		let t = new Promise((t, n) => this.jobs.push({
			run: e,
			resolve: t,
			reject: n
		}));
		return this.emit(), this.pump(), t;
	}
	pump() {
		this.running || this.storageError || (this.running = (async () => {
			for (; this.jobs.length && !this.storageError;) {
				let e = this.jobs[0];
				try {
					await e.run(), this.jobs.shift(), e.resolve();
				} catch (t) {
					this.storageError = String(t).includes("account_changed") ? "collaboration_account_changed" : "collaboration_local_save_failed", this.storageError.includes("account_changed") ? this.invalidate() : this.disconnectWire(), e.reject(t);
					break;
				}
				this.emit();
			}
		})().finally(() => {
			this.running = null, this.emit(), this.current() && this.deliver();
		}));
	}
	async persist(e, t) {
		for (let n = 0; n < 16; n++) {
			let n = await this.options.store.load();
			if (!n) throw Error("collaboration_checkpoint_missing");
			let r = new D_();
			try {
				U_(r, SM(n.state)), U_(r, e), NM(r);
				try {
					this.page = await this.options.store.save(n.revision, K_(r), t);
				} catch (e) {
					if (String(e).includes("revision_changed")) continue;
					throw e;
				}
				this.current() && U_(this.doc, SM(this.page.state), MM);
				return;
			} finally {
				r.destroy();
			}
		}
		throw Error("collaboration_journal_busy");
	}
	async reload() {
		await this.requireCurrent();
		let e = await this.options.store.load();
		if (!e) throw Error("collaboration_checkpoint_missing");
		let t = new D_();
		try {
			U_(t, SM(e.state)), NM(t);
		} finally {
			t.destroy();
		}
		await this.requireCurrent(), this.page = e, U_(this.doc, SM(e.state), MM);
	}
	async flush() {
		for (this.pump(); this.running;) await this.running;
		if (this.storageError || this.jobs.length) throw Error(this.storageError ?? "collaboration_local_save_failed");
	}
	async retryLocalPersistence() {
		await this.requireCurrent(), this.storageError = null, await this.flush();
	}
	exportRecovery() {
		return {
			version: 1,
			scope: structuredClone(this.scope),
			state: xM(K_(this.doc)),
			pending: this.pending,
			queuedTransactions: this.jobs.length,
			localDurable: this.status.localDurable
		};
	}
	invalidate(e = !0) {
		this.changedAccount || (this.changedAccount = !0, this.manuallyOffline = !0, this.disconnectWire(), e && this.options.account.invalidate?.(), this.emit());
	}
	disconnect() {
		this.manuallyOffline = !0, this.disconnectWire(), this.emit();
	}
	disconnectWire() {
		this.clearPresence(), this.wireGeneration++, clearTimeout(this.retryTimer), clearTimeout(this.refreshTimer), clearInterval(this.heartbeatTimer), this.synced = !1, this.sending = null;
		let e = this.provider, t = this.transport, n = this.readDoc;
		this.provider = void 0, this.transport = void 0, this.readDoc = void 0, e?.destroy(), t?.destroy(), n?.destroy();
	}
	schedule() {
		this.manuallyOffline || !this.current() || this.storageError || this.page.recoveryReason || this.options.autoReconnect === !1 || (clearTimeout(this.retryTimer), this.retryTimer = setTimeout(() => {
			this.connect().catch(() => {});
		}, Math.min(3e4, 500 * 2 ** Math.min(this.retries++, 6))));
	}
	async connect(e = !1) {
		if (await this.requireCurrent(), await this.flush(), !this.options.getTicket) throw Error("collaboration_transport_not_configured");
		if (await this.reload(), e || (e = this.page.recoveryReason === "access_unavailable" && this.page.pending.length === 0), this.page.recoveryReason && !e) throw Error("collaboration_private_recovery_required");
		this.disconnectWire(), this.manuallyOffline = !1, this.resumeRecovery = e, e && (this.lastTicket = void 0);
		let t = this.wireGeneration;
		try {
			let e = await this.options.getTicket(this.page.installationId, this.lastTicket);
			if (await this.requireCurrent(), t !== this.wireGeneration) return;
			let n = PM(this.scope, e, !0);
			this.lastTicket = e;
			let r = new D_();
			this.readDoc = r;
			let i = new nM({
				url: e.webSocketUrl,
				autoConnect: !1,
				maxAttempts: 1
			});
			this.transport = i;
			let a = () => t === this.wireGeneration && this.current(), o = () => {
				if (!a() || !this.synced) return;
				let e = K_(r);
				this.enqueue(async () => {
					a() && (await this.requireCurrent(), await this.persist(e));
				}).catch(() => {});
			};
			r.on("update", o);
			let s = new fM({
				websocketProvider: i,
				name: n,
				token: e.ticket,
				document: r,
				awareness: null,
				onSynced: ({ state: e }) => {
					e && a() && (this.synced = !0, this.retries = 0, o(), clearInterval(this.heartbeatTimer), this.heartbeatTimer = setInterval(() => {
						a() && this.synced && s.sendStateless(JSON.stringify({ type: "heartbeat" }));
					}, 1e4), this.resumeRecovery && this.enqueue(async () => {
						await this.requireCurrent(), this.page = await this.options.store.recovery(null), this.resumeRecovery = !1;
					}).catch(() => {}), this.emit(), this.deliver());
				},
				onStateless: ({ payload: e }) => {
					a() && this.receiveReceipt(e, t);
				},
				onAuthenticationFailed: () => {
					a() && this.block("access_unavailable");
				},
				onClose: ({ event: e }) => {
					if (a()) {
						if (e.code === 4403 || e.code === 4401) {
							this.block("access_unavailable");
							return;
						}
						this.disconnectWire(), this.emit(), this.schedule();
					}
				}
			});
			this.provider = s, s.attach(), i.connect().catch(() => {
				a() && (this.disconnectWire(), this.emit(), this.schedule());
			}), this.refreshTimer = setTimeout(() => {
				a() && this.connect().catch(() => {});
			}, Math.max(1e3, e.expiresAtUnixMilliseconds - Date.now() - 3e4));
		} catch (e) {
			throw String(e).includes("account_changed") ? this.invalidate() : /access_unavailable|generation_changed|upgrade_required/.test(String(e)) ? await this.block("access_unavailable") : (this.disconnectWire(), this.schedule()), e;
		}
	}
	async deliver() {
		if (!(!this.current() || !this.synced || !this.provider || this.jobs.length || this.storageError || this.page.recoveryReason || this.sending)) try {
			await this.requireCurrent();
			let e = this.wireGeneration, t = await this.options.store.load();
			if (await this.requireCurrent(), e !== this.wireGeneration || !this.synced || !t || this.sending || this.jobs.length) return;
			this.page = t;
			let n = t.pending[0];
			if (!n) {
				this.emit();
				return;
			}
			this.sending = n.updateId, this.provider?.sendStateless(JSON.stringify(n)), this.emit();
		} catch (e) {
			String(e).includes("account_changed") ? this.invalidate() : this.disconnectWire();
		}
	}
	async receiveReceipt(e, t) {
		try {
			if (await this.requireCurrent(), t !== this.wireGeneration) return;
			let n = JSON.parse(e);
			if (n.type === "presenceSnapshot" && this.options.interactionsEnabled) {
				if (!Array.isArray(n.members) || n.members.length > 64 || n.members.some((e) => !e || typeof e.actorId != "string" || e.actorId.length > 128 || e.displayName !== void 0 && (typeof e.displayName != "string" || e.displayName.length > 256) || typeof e.connectionId != "string" || e.connectionId.length > 128 || e.cursor !== null && (!e.cursor || typeof e.cursor.start != "string" || typeof e.cursor.end != "string" || e.cursor.start.length > 1024 || e.cursor.end.length > 1024))) throw Error("collaboration_presence_invalid");
				this.presenceMembers = n.members;
				for (let e of this.presenceListeners) e(this.presenceMembers);
				return;
			}
			if (n.type === "commitRejected") {
				/unsupported|upgrade|generation/.test(n.code) ? await this.block("schema_changed") : (this.disconnectWire(), this.schedule());
				return;
			}
			if (n.type !== "durableReceipt") return;
			let r = n.receipt;
			TM(this.page, r);
			let i = this.page.receipts.find((e) => e.updateId === r.updateId), a = this.page.pending.find((e) => e.updateId === r.updateId);
			if (i ? !yM(i, r) : !a || a.sha256 !== r.sha256) throw Error("collaboration_receipt_invalid");
			await this.enqueue(async () => {
				await this.requireCurrent(), t === this.wireGeneration && (this.page = await this.options.store.acknowledge(r), this.sending === r.updateId && (this.sending = null));
			}), this.emit(), this.deliver();
		} catch (e) {
			String(e).includes("account_changed") ? this.invalidate() : await this.block("access_unavailable");
		}
	}
	async block(e) {
		this.disconnectWire(), this.current() && (await this.enqueue(async () => {
			await this.requireCurrent(), this.page = await this.options.store.recovery(e);
		}).catch(() => {}), this.emit());
	}
	async dispose() {
		this.disposed || (this.manuallyOffline = !0, this.disconnectWire(), await this.flush(), this.doc.off("update", this.localUpdate), this.stopWatch?.(), this.stopAccount(), this.disposed = !0, this.doc.destroy());
	}
};
function LM(e) {
	return e = CM(e), async (t, n) => {
		if (new URL(e.backend).origin !== location.origin) throw Error("collaboration_access_unavailable");
		let r = await fetch(new URL("api/auth/me", e.backend), {
			credentials: "same-origin",
			cache: "no-store",
			signal: AbortSignal.timeout(15e3)
		});
		if (!r.ok) throw Error("collaboration_access_unavailable");
		let i = await r.json();
		if (!i.isAuthenticated || i.userId !== e.ownerId) throw Error("collaboration_account_changed");
		let a = `api/documents/${e.documentId}/pages/${e.pageId}/collaboration/sessions`, o = await fetch(new URL(n ? `${a}/${n.sessionId}/refresh` : a, e.backend), {
			method: "POST",
			credentials: "same-origin",
			cache: "no-store",
			signal: AbortSignal.timeout(15e3),
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify(n ? { version: n.version } : {
				generation: e.generation,
				installationId: t,
				...e.support,
				requestCommit: !0,
				membershipRevision: e.membershipRevision ?? 0,
				environment: e.environment
			})
		});
		if (!o.ok) throw Error(o.status === 403 || o.status === 404 ? "collaboration_access_unavailable" : "collaboration_transport_unavailable");
		return o.json();
	};
}
//#endregion
//#region src/collaboration/presence.css?inline
var RM = ".collaboration-presence{box-sizing:border-box;z-index:12;border:1px solid var(--color-border,#ddd9d1);background:var(--color-surface-2,#f8f7f5);min-width:0;color:var(--color-text,#222429);font:13px/1.4 var(--font-sans,\"Segoe UI\", sans-serif);text-align:start;border-radius:10px;justify-content:space-between;align-items:center;gap:12px;margin:0 0 10px;padding:8px 12px;display:flex;position:relative}.collaboration-presence *,.collaboration-presence :before,.collaboration-presence :after{box-sizing:border-box}.collaboration-presence-heading{flex-wrap:wrap;align-items:center;gap:5px 12px;min-width:0;display:flex}.collaboration-presence-title{font-weight:600}.collaboration-presence-status{color:#526257;white-space:nowrap;align-items:center;gap:6px;font-size:12px;display:inline-flex}.collaboration-presence-status:before{content:\"\";background:#2f7b55;border-radius:50%;flex:none;width:6px;height:6px}.collaboration-presence[data-state=connecting] .collaboration-presence-status,.collaboration-presence[data-state=offline] .collaboration-presence-status{color:#795820}.collaboration-presence[data-state=connecting] .collaboration-presence-status:before,.collaboration-presence[data-state=offline] .collaboration-presence-status:before{background:#a47422}.collaboration-presence[data-state=unavailable] .collaboration-presence-status{color:#9c3540}.collaboration-presence[data-state=unavailable] .collaboration-presence-status:before{background:#b4232f}.collaboration-presence .collaboration-people-button{min-height:36px;color:inherit;font:inherit;cursor:pointer;background:0 0;border:1px solid #0000;border-radius:8px;flex:none;justify-content:center;align-items:center;gap:8px;padding:3px 8px;font-weight:600;display:inline-flex}.collaboration-presence .collaboration-people-button:hover:not(:disabled),.collaboration-presence .collaboration-people-button[aria-expanded=true]{border-color:var(--color-border,#ddd9d1);background:var(--color-surface-1,#fff)}.collaboration-presence .collaboration-people-button:focus-visible{outline-offset:2px;box-shadow:none;outline:2px solid #315b99}.collaboration-presence .collaboration-people-button:disabled{opacity:.65;cursor:default}.collaboration-avatar-stack{padding-inline-start:5px;display:flex}.collaboration-avatar{border:2px solid var(--color-surface-2,#f8f7f5);background:var(--participant-color,#315b99);color:#fff;width:28px;height:28px;font:600 10px/1 var(--font-sans,\"Segoe UI\", sans-serif);-webkit-user-select:none;user-select:none;border-radius:50%;flex:none;justify-content:center;align-items:center;display:inline-flex}.collaboration-avatar-stack .collaboration-avatar{margin-inline-start:-5px}.collaboration-avatar-overflow{color:#454950;background:#e4e1da}.collaboration-people-chevron{color:#646973;font-size:16px;line-height:1;transform:translateY(-2px)}.collaboration-people-panel{z-index:30;border:1px solid var(--color-border,#ddd9d1);background:var(--color-surface-1,#fff);border-radius:12px;width:min(340px,100%);padding:16px;position:absolute;top:calc(100% + 6px);box-shadow:0 10px 30px #11182724}.collaboration-people-panel:not(:-webkit-any(:lang(ae),:lang(ar),:lang(arc),:lang(bcc),:lang(bqi),:lang(ckb),:lang(dv),:lang(fa),:lang(glk),:lang(he),:lang(ku),:lang(mzn),:lang(nqo),:lang(pnb),:lang(ps),:lang(sd),:lang(ug),:lang(ur),:lang(yi))){right:0}.collaboration-people-panel:not(:is(:lang(ae),:lang(ar),:lang(arc),:lang(bcc),:lang(bqi),:lang(ckb),:lang(dv),:lang(fa),:lang(glk),:lang(he),:lang(ku),:lang(mzn),:lang(nqo),:lang(pnb),:lang(ps),:lang(sd),:lang(ug),:lang(ur),:lang(yi))){right:0}.collaboration-people-panel:-webkit-any(:lang(ae),:lang(ar),:lang(arc),:lang(bcc),:lang(bqi),:lang(ckb),:lang(dv),:lang(fa),:lang(glk),:lang(he),:lang(ku),:lang(mzn),:lang(nqo),:lang(pnb),:lang(ps),:lang(sd),:lang(ug),:lang(ur),:lang(yi)){left:0}.collaboration-people-panel:is(:lang(ae),:lang(ar),:lang(arc),:lang(bcc),:lang(bqi),:lang(ckb),:lang(dv),:lang(fa),:lang(glk),:lang(he),:lang(ku),:lang(mzn),:lang(nqo),:lang(pnb),:lang(ps),:lang(sd),:lang(ug),:lang(ur),:lang(yi)){left:0}.collaboration-people-panel[hidden]{display:none}.collaboration-presence .collaboration-people-title{font:600 14px/1.4 var(--font-sans,\"Segoe UI\", sans-serif);color:inherit;margin:0 0 12px;padding:0}.collaboration-presence .collaboration-people-list{overscroll-behavior:contain;max-height:min(320px,45vh);margin:0;padding:0;list-style:none;overflow-y:auto}.collaboration-presence .collaboration-person{border-bottom:1px solid var(--color-border,#ddd9d1);align-items:center;gap:10px;margin:0;padding:9px 0;display:flex}.collaboration-presence .collaboration-person:last-child{border-bottom:0}.collaboration-person .collaboration-avatar{border:0;width:32px;height:32px}.collaboration-person-name{overflow-wrap:anywhere;flex:1;min-width:0;font-weight:600}.collaboration-person-meta{color:var(--color-text-muted,#646973);flex:none;font-size:11px}.collaboration-presence .collaboration-people-hint{font:12px/1.5 var(--font-sans,\"Segoe UI\", sans-serif);color:var(--color-text-muted,#646973);margin:12px 0 0;padding:0}.collaboration-sr-only{clip-path:inset(50%);white-space:nowrap;width:1px;height:1px;position:absolute;overflow:hidden}.collaboration-remote-caret{border-inline-start:2px solid var(--participant-color,#315b99);pointer-events:none;margin-inline-start:-1px;position:relative}.collaboration-remote-caret-label{z-index:6;text-overflow:ellipsis;white-space:nowrap;background:var(--participant-color,#315b99);color:#fff;max-width:160px;font:600 11px/1.3 var(--font-sans,\"Segoe UI\", sans-serif);border-radius:5px 5px 5px 0;padding:3px 7px;position:absolute;bottom:calc(100% + 2px);overflow:hidden;box-shadow:0 1px 3px #0000001f}.collaboration-remote-caret-label:not(:-webkit-any(:lang(ae),:lang(ar),:lang(arc),:lang(bcc),:lang(bqi),:lang(ckb),:lang(dv),:lang(fa),:lang(glk),:lang(he),:lang(ku),:lang(mzn),:lang(nqo),:lang(pnb),:lang(ps),:lang(sd),:lang(ug),:lang(ur),:lang(yi))){left:-2px}.collaboration-remote-caret-label:not(:is(:lang(ae),:lang(ar),:lang(arc),:lang(bcc),:lang(bqi),:lang(ckb),:lang(dv),:lang(fa),:lang(glk),:lang(he),:lang(ku),:lang(mzn),:lang(nqo),:lang(pnb),:lang(ps),:lang(sd),:lang(ug),:lang(ur),:lang(yi))){left:-2px}.collaboration-remote-caret-label:-webkit-any(:lang(ae),:lang(ar),:lang(arc),:lang(bcc),:lang(bqi),:lang(ckb),:lang(dv),:lang(fa),:lang(glk),:lang(he),:lang(ku),:lang(mzn),:lang(nqo),:lang(pnb),:lang(ps),:lang(sd),:lang(ug),:lang(ur),:lang(yi)){right:-2px}.collaboration-remote-caret-label:is(:lang(ae),:lang(ar),:lang(arc),:lang(bcc),:lang(bqi),:lang(ckb),:lang(dv),:lang(fa),:lang(glk),:lang(he),:lang(ku),:lang(mzn),:lang(nqo),:lang(pnb),:lang(ps),:lang(sd),:lang(ug),:lang(ur),:lang(yi)){right:-2px}.collaboration-remote-caret[data-placement=below] .collaboration-remote-caret-label{border-radius:0 5px 5px;top:calc(100% + 2px);bottom:auto}.collaboration-remote-caret[data-align=end] .collaboration-remote-caret-label:not(:-webkit-any(:lang(ae),:lang(ar),:lang(arc),:lang(bcc),:lang(bqi),:lang(ckb),:lang(dv),:lang(fa),:lang(glk),:lang(he),:lang(ku),:lang(mzn),:lang(nqo),:lang(pnb),:lang(ps),:lang(sd),:lang(ug),:lang(ur),:lang(yi))){left:auto;right:0}.collaboration-remote-caret[data-align=end] .collaboration-remote-caret-label:not(:is(:lang(ae),:lang(ar),:lang(arc),:lang(bcc),:lang(bqi),:lang(ckb),:lang(dv),:lang(fa),:lang(glk),:lang(he),:lang(ku),:lang(mzn),:lang(nqo),:lang(pnb),:lang(ps),:lang(sd),:lang(ug),:lang(ur),:lang(yi))){left:auto;right:0}.collaboration-remote-caret[data-align=end] .collaboration-remote-caret-label:-webkit-any(:lang(ae),:lang(ar),:lang(arc),:lang(bcc),:lang(bqi),:lang(ckb),:lang(dv),:lang(fa),:lang(glk),:lang(he),:lang(ku),:lang(mzn),:lang(nqo),:lang(pnb),:lang(ps),:lang(sd),:lang(ug),:lang(ur),:lang(yi)){left:0;right:auto}.collaboration-remote-caret[data-align=end] .collaboration-remote-caret-label:is(:lang(ae),:lang(ar),:lang(arc),:lang(bcc),:lang(bqi),:lang(ckb),:lang(dv),:lang(fa),:lang(glk),:lang(he),:lang(ku),:lang(mzn),:lang(nqo),:lang(pnb),:lang(ps),:lang(sd),:lang(ug),:lang(ur),:lang(yi)){left:0;right:auto}.collaboration-remote-selection{background:color-mix(in srgb, var(--participant-color,#315b99) 16%, transparent)}@media (max-width:480px){.collaboration-presence{gap:6px;padding:6px 8px}.collaboration-presence-heading{gap:2px 8px}.collaboration-presence .collaboration-people-button{gap:5px;min-height:44px;padding:3px 5px}.collaboration-avatar-stack .collaboration-avatar:nth-child(n+3):not(.collaboration-avatar-overflow){display:none}.collaboration-person-meta{white-space:normal;max-width:54px}}@media print{.collaboration-presence,.collaboration-remote-caret{display:none!important}.collaboration-remote-selection{background:0 0!important}}", zM = (e) => e.displayName?.trim() && e.displayName.trim() !== e.actorId ? e.displayName.trim() : "Collaborator", BM = [
	"#315b99",
	"#7b4593",
	"#27745d",
	"#a24937",
	"#286d82",
	"#875c1c",
	"#9b3f6e",
	"#505b88"
];
function VM(e) {
	let t = 2166136261;
	for (let n of e) t = Math.imul(t ^ n.codePointAt(0), 16777619);
	return BM[(t >>> 0) % BM.length];
}
function HM(e, t, n) {
	let r = document.createElement(e);
	return r.className = t, n !== void 0 && (r.textContent = n), r;
}
function UM(e) {
	let t = zM(e).split(/\s+/u), n = HM("span", "collaboration-avatar", (Array.from(t[0])[0] + (t.length > 1 ? Array.from(t[t.length - 1])[0] : "")).toLocaleUpperCase());
	return n.style.setProperty("--participant-color", VM(e.actorId)), n.setAttribute("aria-hidden", "true"), n;
}
var WM = class {
	constructor(e, t) {
		if (this.localActorId = t, this.root = HM("section", "collaboration-presence"), this.status = HM("span", "collaboration-presence-status"), this.button = HM("button", "collaboration-people-button"), this.avatars = HM("span", "collaboration-avatar-stack"), this.count = HM("span", "collaboration-people-count"), this.panel = HM("div", "collaboration-people-panel"), this.list = HM("ul", "collaboration-people-list"), this.announcement = HM("span", "collaboration-sr-only"), this.members = [], this.connected = !1, this.connectedBefore = !1, this.unavailable = !1, this.rendered = "", this.toggle = () => this.open(this.panel.hidden), this.keydown = (e) => {
			e.key === "Escape" && !this.panel.hidden && (e.preventDefault(), e.stopPropagation(), this.open(!1), this.button.focus());
		}, this.outside = (e) => {
			this.root.contains(e.target) || this.open(!1);
		}, this.focusOutside = (e) => {
			let t = e.target;
			t.closest?.("button, a[href], input, select, textarea, [tabindex]") && !this.root.contains(t) && this.open(!1);
		}, !document.getElementById("prosa-presence-styles")) {
			let e = document.createElement("style");
			e.id = "prosa-presence-styles", e.textContent = RM, document.head.append(e);
		}
		this.root.setAttribute("aria-label", "Collaboration");
		let n = HM("div", "collaboration-presence-heading");
		n.append(HM("span", "collaboration-presence-title", "Collaboration"), this.status), this.status.setAttribute("role", "status"), this.button.type = "button", this.button.setAttribute("aria-expanded", "false"), this.panel.id = "collaboration-people-" + crypto.randomUUID(), this.button.setAttribute("aria-controls", this.panel.id), this.avatars.setAttribute("aria-hidden", "true");
		let r = HM("span", "collaboration-people-chevron", "⌄");
		r.setAttribute("aria-hidden", "true"), this.button.append(this.avatars, this.count, r), this.panel.hidden = !0, this.panel.setAttribute("role", "region"), this.panel.setAttribute("aria-label", "People on this page"), this.panel.append(HM("h3", "collaboration-people-title", "People on this page"), this.list, HM("p", "collaboration-people-hint", "Match each color to a cursor in the manuscript.")), this.announcement.setAttribute("role", "status"), this.announcement.setAttribute("aria-live", "polite"), this.announcement.setAttribute("aria-atomic", "true"), this.root.append(n, this.button, this.panel, this.announcement), e.append(this.root), this.button.addEventListener("click", this.toggle), this.root.addEventListener("keydown", this.keydown), document.addEventListener("pointerdown", this.outside), document.addEventListener("focusin", this.focusOutside), this.render();
	}
	open(e) {
		this.panel.hidden = !e, this.button.setAttribute("aria-expanded", String(e));
	}
	setMembers(e) {
		this.members = [...new Map(e.map((e) => [e.actorId, e])).values()].sort((e, t) => Number(t.actorId === this.localActorId) - Number(e.actorId === this.localActorId) || zM(e).localeCompare(zM(t)) || e.actorId.localeCompare(t.actorId)), this.render();
	}
	setStatus(e) {
		this.connected = e.connected, this.connectedBefore || (this.connectedBefore = e.connected), this.unavailable = !!(e.accountChanged || e.error || e.recoveryReason), this.render();
	}
	render() {
		let e = this.unavailable ? "unavailable" : this.connected ? "live" : this.connectedBefore ? "offline" : "connecting", t = {
			unavailable: "Unavailable",
			live: "Live",
			offline: "Not connected",
			connecting: "Connecting…"
		}[e], n = e === "live" ? this.members : [], r = JSON.stringify([e, n.map((e) => [e.actorId, zM(e)])]);
		if (r === this.rendered) return;
		this.rendered = r, this.root.dataset.state = e, this.status.textContent = t, this.status.title = e === "live" ? "Live presence on this page. Save status is shown separately." : "Live participant information is unavailable until connected.", this.count.textContent = n.length === 1 ? n[0].actorId === this.localActorId ? "Only you" : "1 person" : n.length ? `${n.length} people` : "People", this.button.disabled = n.length === 0;
		let i = n.length ? `Show ${n.length === 1 ? "1 person" : n.length + " people"} on this page` : e === "live" ? "Waiting for live participants" : "Live participants unavailable";
		this.button.setAttribute("aria-label", i), this.button.title = i, this.avatars.replaceChildren(...n.slice(0, 3).map(UM)), n.length > 3 && this.avatars.append(HM("span", "collaboration-avatar collaboration-avatar-overflow", "+" + (n.length - 3))), this.list.replaceChildren(...n.map((e) => {
			let t = HM("li", "collaboration-person"), n = HM("span", "collaboration-person-name", zM(e));
			return n.title = zM(e), t.append(UM(e), n, HM("span", "collaboration-person-meta", e.actorId === this.localActorId ? "You" : "On this page")), t;
		})), this.announcement.textContent = e === "live" ? `${n.length} ${n.length === 1 ? "person" : "people"} on this page. ` + n.map((e) => zM(e) + (e.actorId === this.localActorId ? ", you" : "")).join("; ") : t, n.length || this.open(!1);
	}
	destroy() {
		this.button.removeEventListener("click", this.toggle), this.root.removeEventListener("keydown", this.keydown), document.removeEventListener("pointerdown", this.outside), document.removeEventListener("focusin", this.focusOutside), this.root.remove();
	}
}, GM = new D("prosa-collaboration-interactions"), KM = class {
	constructor(e) {
		this.session = e, this.members = [], this.threads = [], this.last = 0, this.connected = !1, this.extension = I.create({
			name: "prosaInteractions",
			addProseMirrorPlugins: () => [new E({
				key: GM,
				view: () => ({ update: () => this.queueLabelLayout() }),
				props: { decorations: (e) => {
					if (!q.getState(e)?.binding) return j.empty;
					let t = [], n = /* @__PURE__ */ new Map();
					for (let e of this.members) (e.cursor || !n.has(e.actorId)) && n.set(e.actorId, e);
					for (let e of n.values()) {
						if (!e.cursor || e.actorId === this.session.scope.ownerId) continue;
						let n = this.range(e.cursor);
						if (!n) continue;
						let r = VM(e.actorId);
						n.from < n.to && t.push(A.inline(n.from, n.to, {
							class: "collaboration-remote-selection",
							style: "--participant-color:" + r
						})), t.push(A.widget(n.to, () => {
							let t = zM(e), n = document.createElement("span");
							n.className = "collaboration-remote-caret", n.style.setProperty("--participant-color", r), n.setAttribute("aria-label", t + " cursor");
							let i = document.createElement("span");
							return i.className = "collaboration-remote-caret-label", i.textContent = t, n.append(i), n;
						}, {
							key: JSON.stringify([e.connectionId, zM(e)]),
							side: 1
						}));
					}
					for (let e of this.threads) {
						if (e.detached || e.resolved || !e.anchor) continue;
						let n = this.range(e.anchor);
						n && n.from < n.to && t.push(A.inline(n.from, n.to, {
							class: "collaboration-comment-anchor",
							"data-thread-id": e.id,
							style: "background:rgba(240,185,30,.25);border-bottom:1px solid #b58400"
						}));
					}
					return j.create(e.doc, t);
				} }
			})]
		}), this.queueLabelLayout = () => {
			this.layoutFrame === void 0 && (this.layoutFrame = requestAnimationFrame(() => {
				if (this.layoutFrame = void 0, !this.editor || this.editor.isDestroyed) return;
				let e = this.editor.view.dom, t = (this.viewport ?? e).getBoundingClientRect();
				for (let n of e.querySelectorAll(".collaboration-remote-caret")) {
					let e = n.querySelector(".collaboration-remote-caret-label");
					if (!e) continue;
					let r = n.getBoundingClientRect(), i = e.getBoundingClientRect();
					n.dataset.placement = r.top - i.height - 3 < t.top ? "below" : "above", n.dataset.align = r.left + i.width > t.right - 4 ? "end" : "start";
				}
			}));
		}, this.publish = () => {
			Date.now() - this.last < 200 || (this.last = Date.now(), this.session.publishPresence(this.selectionAnchor()));
		};
	}
	refresh() {
		this.editor && !this.editor.isDestroyed && this.editor.view.dispatch(this.editor.state.tr.setMeta(GM, !0).setMeta("addToHistory", !1));
	}
	range(e) {
		if (!this.editor) return null;
		let t = q.getState(this.editor.state)?.binding;
		if (!t) return null;
		try {
			let n = this.session.doc.getXmlFragment("default"), r = Px(this.session.doc, n, vv(SM(e.start, 768)), t.mapping), i = Px(this.session.doc, n, vv(SM(e.end, 768)), t.mapping);
			return r === null || i === null || r > i || r < 0 || i > this.editor.state.doc.content.size ? null : {
				from: r,
				to: i
			};
		} catch {
			return null;
		}
	}
	selectionAnchor() {
		if (!this.editor) return null;
		let e = q.getState(this.editor.state)?.binding;
		if (!e) return null;
		let { from: t, to: n } = this.editor.state.selection, r = this.session.doc.getXmlFragment("default"), i = (t, n) => {
			let i = bv(Sx(t, r, e.mapping), this.session.doc);
			if (!i) throw Error("collaboration_anchor_detached");
			return xM(gv(mv(i.type, i.index, n)));
		};
		return {
			start: i(t, 0),
			end: i(n, t === n ? 0 : -1)
		};
	}
	setThreads(e) {
		if (e.length > 200 || e.some((e) => e.pageId !== this.session.scope.pageId || e.generation !== this.session.scope.generation)) throw Error("collaboration_generation_changed");
		this.threads = e, this.refresh();
	}
	focusThread(e) {
		let t = this.threads.find((t) => t.id === e);
		if (!t || t.detached || !t.anchor || !this.editor) return !1;
		let n = this.range(t.anchor);
		return !n || n.from >= n.to ? !1 : (this.editor.view.dispatch(this.editor.state.tr.setSelection(w.create(this.editor.state.doc, n.from, n.to)).scrollIntoView()), this.editor.view.focus(), !0);
	}
	attach(e, t) {
		if (this.editor = e, this.viewport = e.view.dom.closest(".editor-viewport") ?? void 0, this.viewport?.addEventListener("scroll", this.queueLabelLayout, { passive: !0 }), window.addEventListener("resize", this.queueLabelLayout), t) {
			let t = e.options.element, n = t.closest(".editor-main")?.querySelector(".collaboration-presence-slot"), r = n ?? document.createElement("div");
			n || (r.className = "collaboration-presence-slot", t.prepend(r)), this.roster = new WM(r, this.session.scope.ownerId), this.stopStatus = this.session.watchStatus((e) => {
				this.roster?.setStatus(e);
				let t = e.connected && !this.connected;
				this.connected = e.connected, t && this.publish();
			});
		}
		this.stop = this.session.watchPresence((e) => {
			this.members = e, this.roster?.setMembers(e), this.refresh();
		}), t && (e.on("selectionUpdate", this.publish), this.timer = setInterval(this.publish, 5e3));
	}
	clear() {
		this.members = [], this.threads = [], this.roster?.setMembers([]), this.refresh();
	}
	destroy() {
		clearInterval(this.timer), this.stop?.(), this.stopStatus?.(), this.editor?.off("selectionUpdate", this.publish), this.roster?.destroy(), this.layoutFrame !== void 0 && cancelAnimationFrame(this.layoutFrame), this.viewport?.removeEventListener("scroll", this.queueLabelLayout), window.removeEventListener("resize", this.queueLabelLayout), this.members = [], this.threads = [], this.editor = void 0;
	}
}, qM = /* @__PURE__ */ t({
	CollaborationAccountFence: () => AM,
	IndexedDbJournal: () => OM,
	JournaledSession: () => IM,
	NativeJournalAdapter: () => kM,
	checkedContent: () => tN,
	createCheckedPreview: () => eN,
	createIsolatedEditor: () => yN,
	createIsolatedSeed: () => vN,
	createJournaledEditor: () => aN,
	createLiveEditor: () => gN,
	createNativeJournaledEditor: () => oN,
	createReadOnlyEditor: () => rN,
	getSupport: () => dN,
	mountNativeJournaledEditor: () => sN,
	readCapabilities: () => hN,
	requireCapability: () => mN,
	requireLegacyReplacement: () => bN,
	retainCheckedDraft: () => nN,
	updateReadOnlyEditor: () => iN,
	webTicketSource: () => LM
}), JM = ru(sj(!0)), YM = Symbol("committed-remote-update"), XM = /* @__PURE__ */ new WeakMap(), ZM = /* @__PURE__ */ new WeakMap(), QM = /* @__PURE__ */ new WeakSet(), $M = "prosa-viewer-projection";
function eN(e) {
	let t = new hf({
		element: document.createElement("div"),
		extensions: sj(!0),
		content: e.getJSON()
	});
	return t.__prosaCheckedPreview = !0, t.commands.setTextSelection({
		from: e.state.selection.from,
		to: e.state.selection.to
	}), t;
}
function tN(e) {
	let t = e.getJSON();
	gj(t);
	let n = JM.nodeFromJSON(t);
	n.check();
	let r = document.createElement("div");
	return r.appendChild(et.fromSchema(JM).serializeFragment(n.content, { document })), r.innerHTML;
}
function nN(e, t, n, r, i) {
	sessionStorage.setItem(`prosa-checked-draft:${e}:${t}:${n}`, JSON.stringify({
		operation: r,
		html: i,
		createdAt: Date.now()
	}));
}
function rN(e, t, n) {
	let r = I.create({
		name: "prosaReadOnly",
		addProseMirrorPlugins: () => [new E({ filterTransaction: (e) => !e.docChanged || e.getMeta($M) === !0 })]
	}), i = n({
		element: e,
		extensions: [...sj(!0), r],
		content: t,
		editable: !1
	});
	return i.__prosaCanEdit = () => !1, i;
}
function iN(e, t) {
	let n = typeof t == "string" ? JSON.parse(t) : t;
	gj(n);
	let r = e.schema.nodeFromJSON(n);
	r.check(), !e.state.doc.eq(r) && e.view.dispatch(e.state.tr.replaceWith(0, e.state.doc.content.size, r.content).setMeta($M, !0).setMeta("addToHistory", !1));
}
async function aN(e, t, n, r) {
	let i = t.store.scope;
	if (mN(n, i, await dN()), n.environment && n.environment !== i.environment) throw Error("collaboration_environment_changed");
	if ((n.membershipRevision ?? 0) !== (i.membershipRevision ?? 0)) throw Error("collaboration_access_changed");
	if (ZM.has(e) || XM.has(e) || QM.has(e)) throw Error("collaboration_editor_already_mounted");
	QM.add(e);
	let a, o;
	try {
		let n = o = await IM.open(t), i = new KM(n), s = () => {
			let e = n.status;
			return !e.accountChanged && !e.error && !e.recoveryReason;
		}, c = I.create({
			name: "prosaPermissionFence",
			addProseMirrorPlugins: () => [new E({ filterTransaction: (e) => !e.docChanged || !!e.getMeta(q) || s() })]
		}), l = {
			element: e,
			extensions: [
				...sj(!0),
				hC.configure({
					document: n.doc,
					field: "default"
				}),
				lN,
				i.extension,
				c
			],
			enableContentCheck: !0,
			parseOptions: { preserveWhitespace: "full" },
			editorProps: { attributes: {
				role: "textbox",
				"aria-label": "Manuscript text",
				"aria-multiline": "true"
			} },
			onContentError: () => n.invalidate(!1)
		};
		a = r ? r(l) : new hf(l), a.__prosaCanEdit = s;
		let u = n.watchStatus((e) => {
			let t = !e.accountChanged && !e.error && !e.recoveryReason;
			a.isEditable !== t && a.setEditable(t, !1), t || i.clear();
		}), d = !1;
		i.attach(a, t.interactionsEnabled === !0);
		let f = {
			editor: a,
			session: n,
			interactions: i,
			invalidate() {
				i.clear(), n.invalidate(!1), a.setEditable(!1);
			},
			async destroy() {
				d || (await n.flush(), !d && (d = !0, u(), i.destroy(), a.destroy(), await n.dispose(), ZM.get(e) === f && (ZM.delete(e), e.replaceChildren())));
			}
		};
		return ZM.set(e, f), t.getTicket && n.connect().catch(() => {}), f;
	} catch (e) {
		throw a?.destroy(), await o?.dispose(), e;
	} finally {
		QM.delete(e);
	}
}
async function oN(e, t, n, r, i) {
	let a = new kM(t, n), o = !0;
	return aN(e, {
		store: a,
		account: {
			valid: () => o,
			subscribe: () => () => {},
			invalidate: () => {
				o = !1;
			}
		},
		getTicket: (e) => t.invokeMethodAsync("GetTicket", e),
		onStatus: i,
		interactionsEnabled: r.interactionsAvailable === !0
	}, r);
}
async function sN(e, t, n, r, i) {
	let a = await oN(e, t, n, r, (e) => {
		i.invokeMethodAsync("OnStatus", e);
	});
	return globalThis.DotNet.createJSObjectReference(a);
}
var cN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i, lN = I.create({
	name: "collaborationReplacementFence",
	addCommands() {
		return { setContent: () => () => {
			throw Error("collaboration_legacy_write_blocked: Use a reviewed editor transaction, not HTML replacement.");
		} };
	}
}), uN;
function dN() {
	return uN ?? (uN = (async () => {
		let e = JSON.stringify({
			revision: 1,
			tiptap: "3.31.3",
			contract: cj,
			nodes: Object.entries(JM.nodes).map(([e, t]) => [
				e,
				t.spec.content,
				t.spec.inline,
				t.spec.attrs
			]),
			marks: Object.entries(JM.marks).map(([e, t]) => [e, t.spec.attrs])
		}), t = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(e));
		return Object.freeze({
			contentContractVersion: cj.version,
			protocolVersion: 1,
			schemaFingerprint: [...new Uint8Array(t)].map((e) => e.toString(16).padStart(2, "0")).join("")
		});
	})());
}
function fN(e) {
	if (!e || typeof e.environment != "string" || !/^[a-zA-Z0-9_-]{1,64}$/.test(e.environment) || ![
		e.documentId,
		e.pageId,
		e.generation
	].every((e) => typeof e == "string" && cN.test(e))) throw Error("collaboration_identity_invalid: Document, page and generation identities are required.");
	return `${e.environment}/${e.documentId.toLowerCase()}/${e.pageId.toLowerCase()}/${e.generation.toLowerCase()}`;
}
function pN(e, t) {
	if (!e || e.protocolVersion !== t.protocolVersion || e.contentContractVersion !== t.contentContractVersion || e.schemaFingerprint !== t.schemaFingerprint) throw Error("collaboration_upgrade_required: This editor cannot preserve the page schema. Keep its original state.");
}
function mN(e, t, n) {
	if (fN(t), !e || e.documentId?.toLowerCase() !== t.documentId.toLowerCase() || e.mode !== 2 || !e.canRead || !Array.isArray(e.pages)) throw Error("collaboration_access_unavailable: Preserve your local draft.");
	let r = e.pages.filter((e) => e.pageId?.toLowerCase() === t.pageId.toLowerCase());
	if (r.length !== 1 || r[0].generation?.toLowerCase() !== t.generation.toLowerCase()) throw Error("collaboration_generation_changed: Preserve your local draft for recovery.");
	if (pN(r[0], n), !e.featureAvailable || !e.canWriteCollaboratively) throw Error("collaboration_unavailable: Collaborative editing is disabled in this build. Keep your local draft.");
}
async function hN(e, t) {
	fN(e);
	let n = await dN(), r = new URLSearchParams({
		pageId: e.pageId,
		generation: e.generation,
		protocolVersion: String(n.protocolVersion),
		schemaFingerprint: n.schemaFingerprint
	}), i = await fetch(`/api/documents/${e.documentId}/collaboration/capabilities?${r}`, {
		credentials: "same-origin",
		cache: "no-store",
		signal: t
	});
	if (!i.ok) throw Error("collaboration_capabilities_unavailable: Preserve your draft; access could not be verified.");
	return i.json();
}
async function gN(e, t, n) {
	throw mN(n ?? await hN(t), t, await dN()), Error("collaboration_unavailable: Collaborative editing is not available in this build. Preserve your local draft.");
}
function _N(e) {
	wj(e);
	let t = cC(e, "default");
	gj(t), JM.nodeFromJSON(t).check();
}
async function vN(e, t = "Html") {
	if (![
		"Html",
		"LegacyJson",
		"LegacyText"
	].includes(t) || typeof e != "string" || e.length > 16777216 || t === "Html" && e.includes("<!--")) throw Error("collaboration_source_unsupported: Preserve the original source for recovery.");
	let n = _j(e, t);
	gj(n);
	let r = oC(JM, n, "default");
	try {
		return _N(r), {
			support: await dN(),
			state: K_(r)
		};
	} finally {
		r.destroy();
	}
}
async function yN(e, t, n, r = {}) {
	t = Object.freeze({ ...t });
	let i = fN(t);
	if (t.environment !== "isolated") throw Error("collaboration_isolation_required: This surface cannot open application manuscripts.");
	if (ZM.has(e) || QM.has(e)) throw Error("collaboration_editor_already_mounted");
	let a = await dN();
	if (pN(n?.support, a), !(n?.state instanceof Uint8Array) || n.state.byteLength > 16777216) throw Error("collaboration_snapshot_invalid: Preserve the original state.");
	let o = new D_(), s, c = !1, l;
	try {
		U_(o, new Uint8Array(n.state), YM), _N(o), XM.get(e)?.destroy(), s = new hf({
			element: e,
			extensions: [
				...sj(!0),
				hC.configure({
					document: o,
					field: "default"
				}),
				lN
			],
			enableContentCheck: !0,
			parseOptions: { preserveWhitespace: "full" },
			onContentError: ({ error: e }) => {
				throw e;
			},
			editorProps: { attributes: {
				role: "textbox",
				"aria-label": "Isolated collaborative text",
				"aria-multiline": "true"
			} },
			onUpdate: ({ editor: e }) => {
				c || r.onProjection?.(e.getHTML());
			}
		});
		let u = (e, n, i, o) => {
			!c && o.local && r.onLocalUpdate?.({
				identity: { ...t },
				support: a,
				bytes: new Uint8Array(e)
			});
		};
		o.on("update", u);
		let d = () => {
			if (c) throw Error("collaboration_session_disposed: Open the current page.");
		}, f = {
			editor: s,
			identity: Object.freeze({ ...t }),
			support: a,
			receive(e) {
				if (d(), fN(e.identity) !== i) throw Error("collaboration_generation_changed: Update targets another room.");
				if (pN(e.support, a), !(e.bytes instanceof Uint8Array) || e.bytes.byteLength > 1048576) throw Error("collaboration_update_invalid: Invalid or oversized update.");
				let t = new D_();
				try {
					let n = l ? Qv([l, e.bytes]) : new Uint8Array(e.bytes);
					if (n.byteLength > 16777216) throw Error("collaboration_update_invalid: Deferred updates exceed the snapshot limit.");
					if (U_(t, K_(o), YM), U_(t, n, YM), t.store.pendingStructs || t.store.pendingDs) {
						l = n;
						return;
					}
					try {
						_N(t);
					} catch (e) {
						throw l = void 0, e;
					}
					U_(o, n, YM), l = void 0;
				} finally {
					t.destroy();
				}
			},
			snapshot() {
				return d(), K_(o);
			},
			stopUndoCapture() {
				d(), Fx.getState(s.state)?.undoManager.stopCapturing();
			},
			replaceContent(e) {
				throw Error("collaboration_legacy_write_blocked: Use a reviewed editor transaction, not HTML replacement.");
			},
			destroy() {
				c || (c = !0, l = void 0, o.off("update", u), s.destroy(), o.destroy(), XM.get(e) === f && XM.delete(e), e.replaceChildren());
			}
		};
		return XM.set(e, f), f;
	} catch (e) {
		throw s?.destroy(), o.destroy(), e;
	}
}
function bN(e) {
	if (e.extensionManager.extensions.some((e) => e.name === "collaboration")) throw Error("collaboration_legacy_write_blocked: Remote edits must be applied as binary updates.");
}
//#endregion
//#region src/device-editor.ts
function xN(e, t) {
	if (e.length > 5e5) throw Error("This page exceeds the translation source limit. Translate a smaller selection.");
	if (t === "Html" && /<!--/.test(e)) throw Error("Translation cannot preserve embedded HTML comments. Use selection translation; the original is unchanged.");
	if (t === "Html") {
		let t = document.createElement("template");
		if (t.innerHTML = e, t.content.querySelector("img,pre,code")) throw Error("Translation cannot preserve image captions or code semantics yet. Translate a text-only section or selection; the original is unchanged.");
	}
	let n = _j(e, t), r = [];
	function i(e, t) {
		if (["image", "codeBlock"].includes(e.type) || (e.marks || []).some((e) => e.type === "code")) throw Error("Translation cannot preserve image captions or code semantics yet. Translate a text-only section or selection; the original is unchanged.");
		if (e.type === "text") {
			if (/[\r\n\0]/.test(e.text)) throw Error("Translation needs explicit paragraph or hard-break structure. Normalize line breaks in this page or translate a selection.");
			r.push({
				id: t.join("."),
				text: e.text
			});
		}
		(e.content || []).forEach((e, n) => i(e, [...t, n]));
	}
	return i(n, []), { runs: r };
}
function SN(e, t, n) {
	let r = xN(e, t).runs;
	if (r.length !== n.length || r.some((e, t) => e.id !== n[t]?.id) || n.some((e) => typeof e.text != "string" || !e.text.length || /[\r\n\0]/.test(e.text))) throw Error("Translation text markers do not match this complete page.");
	let i = _j(e, t);
	for (let e of n) {
		let t = e.id.split(".").map(Number), n = i;
		for (let e of t) n = n.content[e];
		if (n.type !== "text") throw Error("Invalid translation text marker.");
		n.text = e.text;
	}
	let a = ru(sj()), o = a.nodeFromJSON(i);
	o.check();
	let s = document.createElement("div");
	return s.appendChild(et.fromSchema(a).serializeFragment(o.content)), s.innerHTML;
}
function CN(e, t, n, r, i, a = !1) {
	let o = new hf({
		element: document.createElement("div"),
		extensions: sj(),
		content: _j(e, t),
		editable: !0
	});
	try {
		let e = o.state.doc, t = a ? "\n\n" : "\n", s = e.textBetween(0, e.content.size, t, "\n"), c = i ?? s.indexOf(n);
		if (!n.trim() || !Number.isSafeInteger(c) || c < 0 || s.slice(c, c + n.length) !== n || i === void 0 && s.indexOf(n, c + 1) >= 0) throw Error("The affected writing could not be matched uniquely. Run the consistency check again.");
		if (r === n || r.length > 0 && !r.trim()) throw Error("This suggestion makes no change to the writing.");
		let u, d;
		if (e.descendants((r, i) => {
			if (!r.isText) return;
			let a = e.textBetween(0, i, t, "\n").length;
			c >= a && c < a + r.nodeSize && (u = i + c - a);
			let o = c + n.length;
			o > a && o <= a + r.nodeSize && (d = i + o - a);
		}), u === void 0 || d === void 0 || e.textBetween(u, d, t, "\n") !== n) throw Error("The affected writing cannot be replaced safely. Run the consistency check again.");
		let f = r.replace(/\r\n?/g, "\n").split("\n"), p = e.nodeAt(u)?.marks ?? e.resolve(u).marks(), m = r.length === 0 ? h.empty : f.length === 1 ? new h(l.from(o.schema.text(r, p)), 0, 0) : new h(l.fromArray(f.map((t) => (a ? e.resolve(u).parent.type : o.schema.nodes.paragraph).create(a ? e.resolve(u).parent.attrs : null, t ? o.schema.text(t, p) : void 0))), 1, 1), g = u, _ = d;
		if (!o.commands.command(({ tr: e, dispatch: t }) => (t && e.replaceRange(g, _, m), !0))) throw Error("The suggested revision could not be applied.");
		return o.getHTML();
	} finally {
		o.destroy();
	}
}
function wN(e, t, n, r, i, a = !0, o = !1) {
	if (!Number.isSafeInteger(r) || r !== n + i.length) throw Error("The quality range is invalid.");
	let s = new hf({
		element: document.createElement("div"),
		extensions: sj(),
		content: _j(e, t),
		editable: !1
	});
	try {
		let e = s.state.doc, t = a ? "\n\n" : "\n", c = e.textBetween(0, e.content.size, t, "\n"), l = (e) => /[\p{L}\p{N}]/u.test(e), u = (e) => e > 0 && /[\uD800-\uDBFF]/.test(c[e - 1]) && /[\uDC00-\uDFFF]/.test(c[e] ?? "");
		if (u(n) || u(r) || n > 0 && l(c[n - 1]) && l(c[n] ?? "") || r < c.length && l(c[r - 1] ?? "") && l(c[r])) throw Error("Select a complete passage or whole words before reviewing this fix.");
		let d = Pf(e, {
			issueKey: "",
			from: n,
			to: r,
			expectedText: i,
			severity: "info"
		}, t);
		if (!d) throw Error("The quality passage no longer matches the writing. Check again.");
		let f = e.resolve(d.from), p = e.resolve(d.to), m = JSON.stringify(e.nodeAt(d.from)?.marks ?? f.marks()), h = f.sameParent(p);
		if (e.nodesBetween(d.from, d.to, (e) => {
			(!o && e.isText && JSON.stringify(e.marks) !== m || e.isLeaf && !e.isText) && (h = !1);
		}), !h) throw Error("This passage spans different formatting, blocks or embedded content. Revise it manually to preserve that structure.");
	} finally {
		s.destroy();
	}
}
function TN(e, t, n, r, i, a) {
	return wN(e, t, n, r, i), CN(e, t, i, a, n, !0);
}
function EN(e, t, n, r, i) {
	wN(e, t, n, r, i, !0, !0);
}
function DN(e, t, n, r, i, a) {
	if (EN(e, t, n, r, i), /[\r\n]/.test(a)) return TN(e, t, n, r, i, a);
	if (a === i || a.length > 0 && !a.trim()) throw Error("This suggestion makes no change to the writing.");
	let o = new hf({
		element: document.createElement("div"),
		extensions: sj(),
		content: _j(e, t),
		editable: !1
	});
	try {
		let e = o.state.doc, t = Pf(e, {
			issueKey: "",
			from: n,
			to: r,
			expectedText: i,
			severity: "info"
		}), s = Gf(o, t.from, t.to, i, a, "\n\n"), c = document.createElement("div");
		return c.appendChild(et.fromSchema(o.schema).serializeFragment(s.doc.content)), c.innerHTML;
	} finally {
		o.destroy();
	}
}
function ON(e, t) {
	let n = ru(sj()).nodeFromJSON(_j(e, t));
	return n.textBetween(0, n.content.size, "\n", "\n");
}
function kN(e, t, n, r, i, a, o, s = 0) {
	for (let r of [
		e,
		t,
		n
	]) {
		let e = new DOMParser().parseFromString(r, "text/html").body;
		if ([...e.querySelectorAll("img")].some((t) => t.parentElement !== e)) throw Error("Nested images are unsupported by the checked Keep mapping.");
	}
	let c = _j(e || "<p></p>", "Html"), l = _j(t, "Html"), u = _j(n, "Html"), d = new hf({
		element: document.createElement("div"),
		extensions: sj(),
		content: c,
		editable: !1
	});
	try {
		let e = d.state.doc, t = d.schema.nodeFromJSON(l), c = d.schema.nodeFromJSON(u);
		e.check(), t.check(), c.check();
		let f;
		if (r === 2) f = t;
		else if (r === 0) {
			if (i !== 0 || a !== 0) throw Error("Unsupported insertion point.");
			if (!Number.isInteger(s) || s < 0 || s > e.childCount) throw Error("The paragraph insertion point changed.");
			let n = 0;
			for (let t = 0; t < s; t++) n += e.child(t).nodeSize;
			f = e.childCount === 1 && e.firstChild?.type.name === "paragraph" && e.firstChild.content.size === 0 ? t : d.state.tr.insert(n, t.content).doc;
		} else if (r === 1) {
			let n = Pf(e, {
				issueKey: "",
				from: i,
				to: a,
				expectedText: o,
				severity: "info"
			});
			if (!n) throw Error("The selected passage no longer maps to this source.");
			if (t.childCount === 1 && t.firstChild?.type.name === "paragraph") f = d.state.tr.replaceWith(n.from, n.to, t.firstChild.content).doc;
			else {
				let r = e.resolve(n.from), i = e.resolve(n.to);
				if (r.parent.type.name !== "paragraph" || r.parentOffset !== 0 || i.parentOffset !== i.parent.content.size || !r.sameParent(i)) throw Error("Multiple proposal blocks require a complete paragraph.");
				f = d.state.tr.replaceWith(r.before(), i.after(), t.content).doc;
			}
		} else throw Error("Unsupported Keep mode.");
		if (!f.eq(c)) throw Error("The rich-editor mapping differs from the approved snapshot. Original retained.");
		return n;
	} finally {
		d.destroy();
	}
}
function AN(e, t) {
	let n = ru(sj()).nodeFromJSON(_j(e, t));
	return n.textBetween(0, n.content.size, "\n\n", "\n");
}
function jN(e, t, n) {
	if (!n.trim() || n.length > 2e4 || /[\r\n\0]/.test(n)) throw Error("Review exactly one new paragraph.");
	let r = ru(sj()), i = r.nodeFromJSON(_j(e, t));
	i.check();
	let a = r.nodes.paragraph.create(null, r.text(n)), o = i.copy(i.content.append(a.content.size ? l.from(a) : l.empty));
	o.check();
	let s = document.createElement("div");
	return s.appendChild(et.fromSchema(r).serializeFragment(o.content)), s.innerHTML;
}
function MN(e, t, n, r, i) {
	if (wN(e, t, n, n + r.length, r, !1), /[\r\n]/.test(i)) throw Error("Review a single-block revision manually to preserve its structure.");
	return CN(e, t, r, i, n);
}
function NN(e, t, n, r, i) {
	let a = e.state.doc, o = a.textBetween(0, a.content.size, "\n", "\n");
	if (e.getHTML() !== t || !Number.isSafeInteger(n) || n < 0 || !r || o.slice(n, n + r.length) !== r || !Array.isArray(i) || i.length === 0 || i.length > 24) throw Error("The style review no longer matches the writing. Run the coach again.");
	let s = [], c = [];
	for (let e of i) {
		if (!e || typeof e.original != "string" || typeof e.replacement != "string" || !e.original.trim() || !e.replacement.trim() || e.original === e.replacement || e.replacement.length > 2e4) throw Error("Invalid reviewed style change.");
		let t = r.indexOf(e.original);
		if (t < 0 || r.indexOf(e.original, t + 1) >= 0 || c.some((n) => t < n.to && t + e.original.length > n.from)) throw Error("Ambiguous or overlapping style changes. Review a shorter selection.");
		c.push({
			from: t,
			to: t + e.original.length
		});
		let i = Pf(a, {
			issueKey: "",
			from: n + t,
			to: n + t + e.original.length,
			expectedText: e.original,
			severity: "info"
		}, "\n");
		if (!i || !a.resolve(i.from).sameParent(a.resolve(i.to))) throw Error("Review a single paragraph or phrase to preserve its structure.");
		let o = 0, l = 0;
		for (; o < e.original.length && o < e.replacement.length && e.original[o] === e.replacement[o];) o++;
		for (o > 0 && /[\uD800-\uDBFF]/.test(e.original[o - 1]) && o--; l < e.original.length - o && l < e.replacement.length - o && e.original[e.original.length - l - 1] === e.replacement[e.replacement.length - l - 1];) l++;
		l > 0 && /[\uDC00-\uDFFF]/.test(e.original[e.original.length - l]) && l--;
		let u = (e) => /[\p{L}\p{N}]/u.test(e);
		for (; o > 0 && u(e.original[o - 1]) && u(e.original[o] ?? "");) o--;
		for (; l > 0 && u(e.original[e.original.length - l - 1] ?? "") && u(e.original[e.original.length - l]);) l--;
		let d = i.from + o, f = i.to - l, p = e.replacement.slice(o, e.replacement.length - l), m = e.original.slice(o, e.original.length - l);
		if (/[\r\n]/.test(p) || /[\r\n]/.test(m) || a.textBetween(d, f, "\n", "\n") !== m) throw Error("This style change crosses a line break. Review a shorter passage.");
		let h = f > d ? a.nodeAt(d)?.marks ?? a.resolve(d).marks() : a.resolve(d).marks(), g = !0;
		if (f > d && a.nodesBetween(d, f, (e) => {
			(e.isText && JSON.stringify(e.marks) !== JSON.stringify(h) || e.isLeaf && !e.isText) && (g = !1);
		}), !g) throw Error("This changed wording spans mixed formatting or embedded content. Review a smaller passage.");
		s.push({
			from: d,
			to: f,
			text: p,
			marks: h
		});
	}
	let u = e.state.tr;
	for (let e of s.sort((e, t) => t.from - e.from)) u.replaceWith(e.from, e.to, e.text ? a.type.schema.text(e.text, e.marks) : l.empty);
	return u.doc.check(), u;
}
function PN(e, t, n, r) {
	let i = new hf({
		element: document.createElement("div"),
		extensions: sj(),
		content: _j(e, "Html"),
		editable: !1
	});
	try {
		let a = NN(i, e, t, n, r).doc, o = document.createElement("div");
		return o.appendChild(et.fromSchema(i.schema).serializeFragment(a.content)), o.innerHTML;
	} finally {
		i.destroy();
	}
}
function FN(e, t, n) {
	let r = (t, n) => {
		let r = e.resolve(t);
		if (!r.parent.isTextblock) return t;
		let i = r.parent.textBetween(0, r.parent.content.size, "", "￼"), a = r.parentOffset, o = (e) => i.slice(Math.max(0, e - 2), e).match(/.$/u)?.[0] ?? "", s = (e) => e < i.length ? String.fromCodePoint(i.codePointAt(e)) : "", c = (e) => /[\p{L}\p{N}\p{M}_]/u.test(e), l = (e) => /['’\-]/u.test(e), u = (e) => {
			let t = o(e), n = s(e);
			return c(t) && c(n) || c(t) && l(n) && c(s(e + n.length)) || l(t) && c(o(e - t.length)) && c(n);
		};
		for (a > 0 && /[\uD800-\uDBFF]/.test(i[a - 1]) && /[\uDC00-\uDFFF]/.test(i[a] ?? "") && (a += n); u(a);) a += n < 0 ? -o(a).length : s(a).length;
		return r.start() + a;
	};
	return {
		from: r(t, -1),
		to: r(n, 1)
	};
}
function IN(e, t, n, r, i = !1) {
	let a = _j(t, n), o = document.createElement("div");
	o.className = "device-editor-toolbar", o.hidden = i, o.setAttribute("role", "group"), o.setAttribute("aria-label", "Text formatting");
	let s = document.createElement("div"), c = document.createElement("div");
	c.className = "device-editor-link", c.hidden = !0;
	let l = document.createElement("input");
	l.type = "url", l.setAttribute("aria-label", "Link address (https, http or mailto)"), l.placeholder = "https://example.com";
	let u = document.createElement("p");
	u.className = "device-editor-notice", u.setAttribute("role", "alert");
	let d = document.createElement("form");
	d.className = "device-editor-link", d.hidden = !0, d.setAttribute("aria-label", "Insert image from URL");
	let f = document.createElement("input");
	f.type = "url", f.placeholder = "https://example.com/image.png", f.setAttribute("aria-label", "Image URL");
	let p = document.createElement("input");
	p.placeholder = "Describe the image", p.maxLength = 1e3, p.setAttribute("aria-label", "Image description");
	let m = document.createElement("button");
	m.type = "submit", m.textContent = "Insert image";
	let h = document.createElement("button");
	h.type = "button", h.textContent = "Cancel image", d.append(f, p, m, h);
	let g = document.createElement("input");
	g.type = "file", g.hidden = !0, g.accept = "image/png,image/jpeg,image/gif,image/webp", g.setAttribute("aria-label", "Choose image from your device"), e.replaceChildren(o, c, d, g, u, s);
	let _ = 0, ee = !1, v = null, y = {
		from: 1,
		to: 1
	}, b = {
		from: 1,
		to: 1
	}, x = 0, te = [], ne = (e, ...t) => {
		ee || r.invokeMethodAsync(e, ...t).catch(() => {
			ee || (u.textContent = "The editor could not notify the app. Use Save before leaving this page.");
		});
	}, re = () => {
		S.isEditable && (d.hidden = !0, y = {
			from: S.state.selection.from,
			to: S.state.selection.to
		}, l.value = S.getAttributes("link").href || "", c.hidden = !1, l.focus());
	}, ie = (e) => {
		S.isEditable && (b = {
			from: S.state.selection.from,
			to: S.state.selection.to
		}, x = _, c.hidden = !0, e ? (d.hidden = !0, g.value = "", g.click()) : (f.value = "", p.value = "", d.hidden = !1, f.focus()));
	};
	function ae(e, t = "", n = S.state.selection) {
		return ee || !S.isEditable ? !1 : (e = e.trim(), pj(e) ? S.getHTML().length + e.length + t.length * 6 + 200 > 5e5 ? (u.textContent = "This image would exceed the page size limit. Choose a smaller image or use an image URL.", !1) : (S.chain().focus().setTextSelection({
			from: n.from,
			to: n.to
		}).setImage({
			src: e,
			alt: t
		}).run(), d.hidden = !0, u.textContent = "", !0) : (u.textContent = "Use an http or https image address, or choose a PNG, JPEG, GIF or WebP file.", !1));
	}
	d.addEventListener("submit", (e) => {
		if (e.preventDefault(), _ !== x) {
			u.textContent = "The writing changed. Open Image URL again to choose where to insert the image.";
			return;
		}
		ae(f.value, p.value, b);
	});
	let oe = () => {
		d.hidden = !0, S.commands.focus();
	};
	h.addEventListener("click", oe), d.addEventListener("keydown", (e) => {
		e.key === "Escape" && (e.preventDefault(), oe());
	}), g.addEventListener("change", async () => {
		let e = g.files?.[0];
		if (!e || ee) return;
		let t = { ...b }, n = x;
		if (e.size > 3e5) {
			u.textContent = "Choose an image up to 300 KB, or insert it using an image URL.";
			return;
		}
		try {
			let r = new Uint8Array(await e.arrayBuffer()), i = (e, t) => t.every((t, n) => r[e + n] === t), a = i(0, [
				137,
				80,
				78,
				71,
				13,
				10,
				26,
				10
			]) ? "image/png" : i(0, [
				255,
				216,
				255
			]) ? "image/jpeg" : i(0, [
				71,
				73,
				70,
				56
			]) ? "image/gif" : i(0, [
				82,
				73,
				70,
				70
			]) && i(8, [
				87,
				69,
				66,
				80
			]) ? "image/webp" : null;
			if (!a) throw Error("Choose a PNG, JPEG, GIF or WebP image.");
			if (ee) return;
			if (_ !== n) throw Error("The writing changed. Choose the image again to set its insertion point.");
			let o = "";
			for (let e of r) o += String.fromCharCode(e);
			ae(`data:${a};base64,${btoa(o)}`, e.name, t);
		} catch (e) {
			ee || (u.textContent = e instanceof Error ? e.message : "The image could not be opened.");
		}
	});
	let se = I.create({
		name: "deviceShortcuts",
		addKeyboardShortcuts() {
			return {
				"Mod-s": () => (ne("OnSaveRequested"), !0),
				"Mod-k": () => (re(), !0)
			};
		}
	}), ce = !1, S = new hf({
		element: s,
		extensions: [
			...sj(),
			se,
			kf((e) => ne("OnAnnotationClicked", e)),
			Ff,
			qf
		],
		content: a,
		parseOptions: { preserveWhitespace: "full" },
		enableContentCheck: !0,
		onContentError: ({ error: e }) => {
			throw e;
		},
		editorProps: {
			attributes: {
				role: "textbox",
				"aria-label": "Document text",
				"aria-multiline": "true",
				spellcheck: "true"
			},
			handlePaste(e, t) {
				let n = t.clipboardData?.getData("text/html");
				if (n) try {
					hj(n);
				} catch {
					return t.preventDefault(), e.dispatch(e.state.tr.insertText(t.clipboardData?.getData("text/plain") || "")), u.textContent = "Pasted as plain text because the source has unsupported formatting.", !0;
				}
				return !1;
			},
			handleDrop: () => !0
		},
		onUpdate: () => {
			v = null, _++, ne("OnContentChanged", S.getHTML(), _);
		},
		onSelectionUpdate: () => {
			let e = S.state.selection;
			e.empty || (v = {
				from: e.from,
				to: e.to
			});
		},
		onTransaction: () => {
			i && ce && le();
			for (let e of te) e.active && e.button.setAttribute("aria-pressed", String(e.active())), e.button.disabled = !S.isEditable || (e.enabled ? !e.enabled() : !1);
		}
	});
	function le() {
		let e = S.isEditable, t = S.isActive("heading") ? S.getAttributes("heading").level : null;
		ne("OnFormattingChanged", {
			isBold: S.isActive("bold"),
			isItalic: S.isActive("italic"),
			isLink: S.isActive("link"),
			isStrike: S.isActive("strike"),
			isCode: S.isActive("code"),
			canBold: e && S.can().toggleBold(),
			canItalic: e && S.can().toggleItalic(),
			canStrike: e && S.can().toggleStrike(),
			canCode: e && S.can().toggleCode(),
			canApplyHeading: e && !S.isActive("codeBlock"),
			canToggleList: e,
			canBlockquote: e,
			canHorizontalRule: e && S.can().setHorizontalRule(),
			canInsertTable: e && S.can().insertTable({
				rows: 3,
				cols: 3,
				withHeaderRow: !0
			}),
			canInsertImage: e && !S.isActive("codeBlock"),
			isInTable: S.isActive("table"),
			isHeaderCell: S.isActive("tableHeader"),
			canAddTableRowBefore: e && S.can().addRowBefore(),
			canAddTableRowAfter: e && S.can().addRowAfter(),
			canDeleteTableRow: e && S.can().deleteRow(),
			canAddTableColumnBefore: e && S.can().addColumnBefore(),
			canAddTableColumnAfter: e && S.can().addColumnAfter(),
			canDeleteTableColumn: e && S.can().deleteColumn(),
			canToggleTableHeaderRow: e && S.can().toggleHeaderRow(),
			canToggleTableHeaderColumn: e && S.can().toggleHeaderColumn(),
			canMergeTableCells: e && S.can().mergeCells(),
			canSplitTableCell: e && S.can().splitCell(),
			canDeleteTable: e && S.can().deleteTable(),
			canAlign: e && S.can().setTextAlign("left"),
			canIncreaseIndent: e && S.can().increaseIndent(),
			canDecreaseIndent: e && S.can().decreaseIndent(),
			textAlign: S.getAttributes(t ? "heading" : "paragraph").textAlign || "left",
			blockType: t ? `heading:${t}` : "paragraph"
		}, e && S.can().undo(), e && S.can().redo());
	}
	ce = !0, i && le();
	function ue(e, t, n, r) {
		let i = document.createElement("button");
		i.type = "button", i.textContent = e, i.addEventListener("mousedown", (e) => e.preventDefault()), i.addEventListener("click", () => {
			S.isEditable && t();
		}), n && i.setAttribute("aria-pressed", String(n())), r && (i.disabled = !r()), o.append(i), te.push({
			button: i,
			active: n,
			enabled: r
		});
	}
	ue("Paragraph", () => Zf(S), () => S.isActive("paragraph"));
	for (let e of cj.headingLevels) ue(`Heading ${e}`, () => Qf(S, e), () => S.isActive("heading", { level: e }));
	ue("Bold", () => Yf(S), () => S.isActive("bold")), ue("Italic", () => Xf(S), () => S.isActive("italic")), ue("Bullets", () => ep(S), () => S.isActive("bulletList")), ue("Numbered list", () => tp(S), () => S.isActive("orderedList")), ue("Quote", () => $f(S), () => S.isActive("blockquote")), ue("Link", re, () => S.isActive("link")), ue("Undo", () => np(S), void 0, () => S.can().undo()), ue("Redo", () => rp(S), void 0, () => S.can().redo()), c.append(l);
	let de = (e, t) => {
		let n = document.createElement("button");
		n.type = "button", n.textContent = e, n.addEventListener("click", t), c.append(n);
	}, fe = () => {
		if (!fj(l.value)) {
			u.textContent = "Use an http, https or mailto address.";
			return;
		}
		S.chain().focus().setTextSelection(y).extendMarkRange("link").setLink({ href: l.value.trim() }).run(), c.hidden = !0, u.textContent = "";
	};
	return de("Apply link", fe), de("Remove link", () => {
		S.chain().focus().setTextSelection(y).extendMarkRange("link").unsetLink().run(), c.hidden = !0;
	}), de("Cancel", () => {
		c.hidden = !0, S.commands.focus();
	}), l.addEventListener("keydown", (e) => {
		e.key === "Enter" && (e.preventDefault(), fe()), e.key === "Escape" && (c.hidden = !0, S.commands.focus());
	}), {
		setAnnotations: (e) => Af(S, e),
		setQualityHighlights: (e, t, n) => If(S, e, t, n),
		navigateToQuality: (e, t) => Lf(S, e, t),
		clearConsistencyHighlight: () => Jf(S, null),
		navigateToConsistency(e, t, n) {
			let r = S.state.doc, i = r.textBetween(0, r.content.size, "\n", "\n") === e ? Pf(r, {
				issueKey: "",
				from: t,
				to: t + n.length,
				expectedText: n,
				severity: "info"
			}, "\n") : null;
			return Jf(S, i), i ? (S.view.dispatch(S.state.tr.setSelection(w.create(r, i.from, i.to)).scrollIntoView()), S.view.focus(), !0) : !1;
		},
		navigateToAnnotation: (e) => jf(S, e),
		selectedText: () => Mf(S),
		navigateToText(e) {
			if (!e || e.length > 1e3) return !1;
			let t = "", n = [], r = null;
			S.state.doc.descendants((e, i, a) => {
				if (e.isText && e.text) {
					r && a !== r && (t += "\n", n.push(i));
					for (let t = 0; t < e.text.length; t++) n.push(i + t);
					t += e.text, r = a;
				} else e.type.name === "hardBreak" && (t += "\n", n.push(i));
			});
			let i = t.indexOf(e);
			return i < 0 ? !1 : (S.chain().setTextSelection({
				from: n[i],
				to: n[i + e.length - 1] + 1
			}).scrollIntoView().focus().run(), !0);
		},
		command(e, t) {
			if (S.isEditable) switch (e) {
				case "bold":
					Yf(S);
					break;
				case "italic":
					Xf(S);
					break;
				case "bulletList":
					ep(S);
					break;
				case "orderedList":
					tp(S);
					break;
				case "blockquote":
					$f(S);
					break;
				case "paragraph":
					Zf(S);
					break;
				case "heading":
					t && cj.headingLevels.includes(t) && Qf(S, t);
					break;
				case "link":
					re();
					break;
				case "undo":
					np(S);
					break;
				case "redo":
					rp(S);
					break;
				case "strike":
					S.chain().focus().toggleStrike().run();
					break;
				case "code":
					S.chain().focus().toggleCode().run();
					break;
				case "horizontalRule":
					S.chain().focus().setHorizontalRule().run();
					break;
				case "table":
					S.chain().focus().insertTable({
						rows: 3,
						cols: 3,
						withHeaderRow: !0
					}).run();
					break;
				case "image":
					ie(!0);
					break;
				case "imageUrl":
					ie(!1);
					break;
				case "align:left":
				case "align:center":
				case "align:right":
					S.chain().focus().setTextAlign(e.slice(6)).run();
					break;
				case "increaseIndent":
				case "decreaseIndent":
				case "addRowBefore":
				case "addRowAfter":
				case "deleteRow":
				case "addColumnBefore":
				case "addColumnAfter":
				case "deleteColumn":
				case "toggleHeaderRow":
				case "toggleHeaderColumn":
				case "mergeCells":
				case "splitCell":
				case "deleteTable":
					S.chain().focus()[e]().run();
					break;
				default: throw Error("Unsupported editor command.");
			}
		},
		insertImage: ae,
		setEditable(t) {
			S.setEditable(t, !1), i && le(), e.querySelectorAll("button,input").forEach((e) => {
				e.disabled = !t;
			});
			for (let e of te) e.button.disabled = !t || (e.enabled ? !e.enabled() : !1);
		},
		snapshot: () => ({
			html: S.getHTML(),
			version: _
		}),
		captureAi(e = !1, t = !1, n = !1, r = !1) {
			let i = S.state.doc, a = S.state.selection.empty ? t ? null : v : S.state.selection, o = e ? 0 : a?.from ?? S.state.selection.from, s = e ? i.content.size : a?.to ?? S.state.selection.to;
			if (n && !e && o < s) {
				let e, t;
				i.nodesBetween(o, s, (n, r) => {
					n.isLeaf && (e ?? (e = Math.max(o, r)), t = Math.min(s, r + n.nodeSize));
				}), e !== void 0 && t !== void 0 && (o = e, s = t);
			}
			if (r && !e && o < s && ({from: o, to: s} = FN(i, o, s), o !== S.state.selection.from || s !== S.state.selection.to)) {
				let e = S.state.selection.anchor > S.state.selection.head;
				S.view.dispatch(S.state.tr.setSelection(w.create(i, e ? s : o, e ? o : s)));
			}
			let c = (e, t) => i.textBetween(e, t, n ? "\n\n" : "\n", "\n"), l = c(o, s), u = c(0, o).length;
			return {
				html: S.getHTML(),
				plainText: c(0, i.content.size),
				selectedText: l,
				selectionStart: u,
				selectionEnd: u + l.length,
				from: o,
				to: s,
				version: _
			};
		},
		applyAi(e, t, n, r, i, a) {
			if (!S.isEditable || S.getHTML() !== e) throw Error("The writing changed after the preview. Run the action again.");
			if (!i.trim()) throw Error("The proposed text is empty.");
			let o = S.state.doc;
			if (a === "replace" && (t < 0 || n > o.content.size || t >= n || o.textBetween(t, n, "\n", "\n") !== r)) throw Error("The selection changed after the preview. Run the action again.");
			if (a !== "replace" && a !== "append") throw Error("Unsupported AI apply mode.");
			let s = i.replace(/\r\n?/g, "\n").split("\n").map((e) => ({
				type: "paragraph",
				content: e ? [{
					type: "text",
					text: e
				}] : []
			})), c = a === "replace" ? {
				from: t,
				to: n
			} : o.content.size;
			if (!S.commands.insertContentAt(c, s, {
				updateSelection: !0,
				errorOnInvalidContent: !0
			})) throw Error("The proposal could not be applied. Your writing is unchanged.");
			return v = null, {
				html: S.getHTML(),
				version: _
			};
		},
		applyStyleQuality(e, t, n, r) {
			if (!S.isEditable) throw Error("The editor is read-only.");
			let i = NN(S, e, t, n, r);
			return S.view.dispatch(i.scrollIntoView()), v = null, {
				html: S.getHTML(),
				version: _
			};
		},
		restoreAi(e, t) {
			if (!S.isEditable || S.getHTML() !== e) throw Error("The writing changed after AI was applied. Restore the original as a separate copy instead.");
			return hj(t), S.commands.setContent(_j(t, "Html"), {
				emitUpdate: !0,
				errorOnInvalidContent: !0
			}), {
				html: S.getHTML(),
				version: _
			};
		},
		setContent(e, t = "Html") {
			let n = _j(e, t);
			if (S.schema.nodeFromJSON(n).eq(S.state.doc)) return;
			let r = S.state.selection;
			S.commands.setContent(n, {
				emitUpdate: !1,
				errorOnInvalidContent: !0
			});
			let i = S.state.doc.content.size;
			S.commands.setTextSelection({
				from: Math.min(r.from, i),
				to: Math.min(r.to, i)
			});
		},
		destroy() {
			ee = !0, S.destroy(), e.replaceChildren();
		}
	};
}
//#endregion
export { xN as captureTranslation, qM as collaboration, ON as consistencyPlainText, IN as create, kN as previewBrainstormingKeep, CN as previewConsistencyRevision, jN as previewContinuation, TN as previewQualityRevision, MN as previewSafeConsistencyRevision, PN as previewStyleQualityRevision, DN as previewTargetedQualityRevision, SN as previewTranslation, AN as qualityPlainText, wN as validateQualityRange, EN as validateTargetedQualityRange };
