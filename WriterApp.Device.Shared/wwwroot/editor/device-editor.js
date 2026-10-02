//#region node_modules/@tiptap/core/dist/rolldown-runtime-D7D4PA-g.js
var e = Object.defineProperty, t = (t, n) => {
	let r = {};
	for (var i in t) e(r, i, {
		get: t[i],
		enumerable: !0
	});
	return n || e(r, Symbol.toStringTag, { value: "Module" }), r;
};
//#endregion
//#region node_modules/orderedmap/dist/index.js
function n(e) {
	this.content = e;
}
n.prototype = {
	constructor: n,
	find: function(e) {
		for (var t = 0; t < this.content.length; t += 2) if (this.content[t] === e) return t;
		return -1;
	},
	get: function(e) {
		var t = this.find(e);
		return t == -1 ? void 0 : this.content[t + 1];
	},
	update: function(e, t, r) {
		var i = r && r != e ? this.remove(r) : this, a = i.find(e), o = i.content.slice();
		return a == -1 ? o.push(r || e, t) : (o[a + 1] = t, r && (o[a] = r)), new n(o);
	},
	remove: function(e) {
		var t = this.find(e);
		if (t == -1) return this;
		var r = this.content.slice();
		return r.splice(t, 2), new n(r);
	},
	addToStart: function(e, t) {
		return new n([e, t].concat(this.remove(e).content));
	},
	addToEnd: function(e, t) {
		var r = this.remove(e).content.slice();
		return r.push(e, t), new n(r);
	},
	addBefore: function(e, t, r) {
		var i = this.remove(t), a = i.content.slice(), o = i.find(e);
		return a.splice(o == -1 ? a.length : o, 0, t, r), new n(a);
	},
	forEach: function(e) {
		for (var t = 0; t < this.content.length; t += 2) e(this.content[t], this.content[t + 1]);
	},
	prepend: function(e) {
		return e = n.from(e), e.size ? new n(e.content.concat(this.subtract(e).content)) : this;
	},
	append: function(e) {
		return e = n.from(e), e.size ? new n(this.subtract(e).content.concat(e.content)) : this;
	},
	subtract: function(e) {
		var t = this;
		e = n.from(e);
		for (var r = 0; r < e.content.length; r += 2) t = t.remove(e.content[r]);
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
}, n.from = function(e) {
	if (e instanceof n) return e;
	var t = [];
	if (e) for (var r in e) t.push(r, e[r]);
	return new n(t);
};
//#endregion
//#region node_modules/prosemirror-model/dist/index.js
function r(e, t, n) {
	for (let i = 0;; i++) {
		if (i == e.childCount || i == t.childCount) return e.childCount == t.childCount ? null : n;
		let a = e.child(i), o = t.child(i);
		if (a == o) {
			n += a.nodeSize;
			continue;
		}
		if (!a.sameMarkup(o)) return n;
		if (a.isText && a.text != o.text) {
			for (let e = 0; a.text[e] == o.text[e]; e++) n++;
			return n;
		}
		if (a.content.size || o.content.size) {
			let e = r(a.content, o.content, n + 1);
			if (e != null) return e;
		}
		n += a.nodeSize;
	}
}
function i(e, t, n, r) {
	for (let a = e.childCount, o = t.childCount;;) {
		if (a == 0 || o == 0) return a == o ? null : {
			a: n,
			b: r
		};
		let s = e.child(--a), c = t.child(--o), l = s.nodeSize;
		if (s == c) {
			n -= l, r -= l;
			continue;
		}
		if (!s.sameMarkup(c)) return {
			a: n,
			b: r
		};
		if (s.isText && s.text != c.text) {
			let e = 0, t = Math.min(s.text.length, c.text.length);
			for (; e < t && s.text[s.text.length - e - 1] == c.text[c.text.length - e - 1];) e++, n--, r--;
			return {
				a: n,
				b: r
			};
		}
		if (s.content.size || c.content.size) {
			let e = i(s.content, c.content, n - 1, r - 1);
			if (e) return e;
		}
		n -= l, r -= l;
	}
}
var a = class e {
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
		return r(this, e, t);
	}
	findDiffEnd(e, t = this.size, n = e.size) {
		return i(this, e, t, n);
	}
	findIndex(e, t = -1) {
		if (e == 0) return s(0, e);
		if (e == this.size) return s(this.content.length, e);
		if (e > this.size || e < 0) throw RangeError(`Position ${e} outside of fragment (${this})`);
		for (let n = 0, r = 0;; n++) {
			let i = this.child(n), a = r + i.nodeSize;
			if (a >= e) return a == e || t > 0 ? s(n + 1, a) : s(n, r);
			r = a;
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
		return new e(n.map(t.nodeFromJSON));
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
a.empty = new a([], 0);
var o = {
	index: 0,
	offset: 0
};
function s(e, t) {
	return o.index = e, o.offset = t, o;
}
function c(e, t) {
	if (e === t) return !0;
	if (!(e && typeof e == "object") || !(t && typeof t == "object")) return !1;
	let n = Array.isArray(e);
	if (Array.isArray(t) != n) return !1;
	if (n) {
		if (e.length != t.length) return !1;
		for (let n = 0; n < e.length; n++) if (!c(e[n], t[n])) return !1;
	} else {
		for (let n in e) if (!(n in t) || !c(e[n], t[n])) return !1;
		for (let n in t) if (!(n in e)) return !1;
	}
	return !0;
}
var l = class e {
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
		return this == e || this.type == e.type && c(this.attrs, e.attrs);
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
l.none = [];
var u = class extends Error {}, d = class e {
	constructor(e, t, n) {
		this.content = e, this.openStart = t, this.openEnd = n;
	}
	get size() {
		return this.content.size - this.openStart - this.openEnd;
	}
	insertAt(t, n) {
		let r = p(this.content, t + this.openStart, n);
		return r && new e(r, this.openStart, this.openEnd);
	}
	removeBetween(t, n) {
		return new e(f(this.content, t + this.openStart, n + this.openStart), this.openStart, this.openEnd);
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
		return new e(a.fromJSON(t, n.content), r, i);
	}
	static maxOpen(t, n = !0) {
		let r = 0, i = 0;
		for (let e = t.firstChild; e && !e.isLeaf && (n || !e.type.spec.isolating); e = e.firstChild) r++;
		for (let e = t.lastChild; e && !e.isLeaf && (n || !e.type.spec.isolating); e = e.lastChild) i++;
		return new e(t, r, i);
	}
};
d.empty = new d(a.empty, 0, 0);
function f(e, t, n) {
	let { index: r, offset: i } = e.findIndex(t), a = e.maybeChild(r), { index: o, offset: s } = e.findIndex(n);
	if (i == t || a.isText) {
		if (s != n && !e.child(o).isText) throw RangeError("Removing non-flat range");
		return e.cut(0, t).append(e.cut(n));
	}
	if (r != o) throw RangeError("Removing non-flat range");
	return e.replaceChild(r, a.copy(f(a.content, t - i - 1, n - i - 1)));
}
function p(e, t, n, r) {
	let { index: i, offset: a } = e.findIndex(t), o = e.maybeChild(i);
	if (a == t || o.isText) return r && !r.canReplace(i, i, n) ? null : e.cut(0, t).append(n).append(e.cut(t));
	let s = p(o.content, t - a - 1, n);
	return s && e.replaceChild(i, o.copy(s));
}
function m(e, t, n) {
	if (n.openStart > e.depth) throw new u("Inserted content deeper than insertion position");
	if (e.depth - n.openStart != t.depth - n.openEnd) throw new u("Inconsistent open depths");
	return h(e, t, n, 0);
}
function h(e, t, n, r) {
	let i = e.index(r), a = e.node(r);
	if (i == t.index(r) && r < e.depth - n.openStart) {
		let o = h(e, t, n, r + 1);
		return a.copy(a.content.replaceChild(i, o));
	}
	if (!n.content.size) return b(a, S(e, t, r));
	if (!n.openStart && !n.openEnd && e.depth == r && t.depth == r) {
		let r = e.parent, i = r.content;
		return b(r, i.cut(0, e.parentOffset).append(n.content).append(i.cut(t.parentOffset)));
	}
	{
		let { start: i, end: o } = ee(n, e);
		return b(a, x(e, i, o, t, r));
	}
}
function g(e, t) {
	if (!t.type.compatibleContent(e.type)) throw new u("Cannot join " + t.type.name + " onto " + e.type.name);
}
function _(e, t, n) {
	let r = e.node(n);
	return g(r, t.node(n)), r;
}
function v(e, t) {
	let n = t.length - 1;
	n >= 0 && e.isText && e.sameMarkup(t[n]) ? t[n] = e.withText(t[n].text + e.text) : t.push(e);
}
function y(e, t, n, r) {
	let i = (t || e).node(n), a = 0, o = t ? t.index(n) : i.childCount;
	e && (a = e.index(n), e.depth > n ? a++ : e.textOffset && (v(e.nodeAfter, r), a++));
	for (let e = a; e < o; e++) v(i.child(e), r);
	t && t.depth == n && t.textOffset && v(t.nodeBefore, r);
}
function b(e, t) {
	return e.type.checkContent(t), e.copy(t);
}
function x(e, t, n, r, i) {
	let o = e.depth > i && _(e, t, i + 1), s = r.depth > i && _(n, r, i + 1), c = [];
	return y(null, e, i, c), o && s && t.index(i) == n.index(i) ? (g(o, s), v(b(o, x(e, t, n, r, i + 1)), c)) : (o && v(b(o, S(e, t, i + 1)), c), y(t, n, i, c), s && v(b(s, S(n, r, i + 1)), c)), y(r, null, i, c), new a(c);
}
function S(e, t, n) {
	let r = [];
	return y(null, e, n, r), e.depth > n && v(b(_(e, t, n + 1), S(e, t, n + 1)), r), y(t, null, n, r), new a(r);
}
function ee(e, t) {
	let n = t.depth - e.openStart, r = t.node(n).copy(e.content);
	for (let e = n - 1; e >= 0; e--) r = t.node(e).copy(a.from(r));
	return {
		start: r.resolveNoCache(e.openStart + n),
		end: r.resolveNoCache(r.content.size - e.openEnd - n)
	};
}
var te = class e {
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
		if (e.content.size == 0) return l.none;
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
		for (let n = this.depth - (this.parent.inlineContent || this.pos == e.pos ? 1 : 0); n >= 0; n--) if (e.pos <= this.end(n) && (!t || t(this.node(n)))) return new ae(this, e, n);
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
		for (let e = 0; e < ne.length; e++) {
			let r = ne[e];
			if (r.pos == n && r.doc == t) return r;
		}
		let r = ne[re] = e.resolve(t, n);
		return re = (re + 1) % ie, r;
	}
}, ne = [], re = 0, ie = 12, ae = class {
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
}, oe = Object.create(null), se = class e {
	constructor(e, t, n, r = l.none) {
		this.type = e, this.attrs = t, this.marks = r, this.content = n || a.empty;
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
		return this.type == e && c(this.attrs, t || e.defaultAttrs || oe) && l.sameSet(this.marks, n || l.none);
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
		if (e == t) return d.empty;
		let r = this.resolve(e), i = this.resolve(t), a = n ? 0 : r.sharedDepth(t), o = r.start(a);
		return new d(r.node(a).content.cut(r.pos - o, i.pos - o), r.depth - a, i.depth - a);
	}
	replace(e, t, n) {
		return m(this.resolve(e), this.resolve(t), n);
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
		return te.resolveCached(this, e);
	}
	resolveNoCache(e) {
		return te.resolve(this, e);
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
		return this.content.size && (e += "(" + this.content.toStringInner() + ")"), ce(this.marks, e);
	}
	contentMatchAt(e) {
		let t = this.type.contentMatch.matchFragment(this.content, 0, e);
		if (!t) throw Error("Called contentMatchAt on a node with invalid content");
		return t;
	}
	canReplace(e, t, n = a.empty, r = 0, i = n.childCount) {
		let o = this.contentMatchAt(e).matchFragment(n, r, i), s = o && o.matchFragment(this.content, t);
		if (!s || !s.validEnd) return !1;
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
		this.type.checkContent(this.content);
		let e = l.none;
		for (let t = 0; t < this.marks.length; t++) e = this.marks[t].addToSet(e);
		if (!l.sameSet(e, this.marks)) throw RangeError(`Invalid collection of marks for node ${this.type.name}: ${this.marks.map((e) => e.type.name)}`);
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
		let n = null;
		if (t.marks) {
			if (!Array.isArray(t.marks)) throw RangeError("Invalid mark data for Node.fromJSON");
			n = t.marks.map(e.markFromJSON);
		}
		if (t.type == "text") {
			if (typeof t.text != "string") throw RangeError("Invalid text node in JSON");
			return e.text(t.text, n);
		}
		let r = a.fromJSON(e, t.content);
		return e.nodeType(t.type).create(t.attrs, r, n);
	}
};
se.prototype.text = void 0;
var C = class e extends se {
	constructor(e, t, n, r) {
		if (super(e, t, null, r), !n) throw RangeError("Empty text nodes are not allowed");
		this.text = n;
	}
	toString() {
		return this.type.spec.toDebugString ? this.type.spec.toDebugString(this) : ce(this.marks, JSON.stringify(this.text));
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
function ce(e, t) {
	for (let n = e.length - 1; n >= 0; n--) t = e[n].type.name + "(" + t + ")";
	return t;
}
var w = class e {
	constructor(e) {
		this.validEnd = e, this.next = [], this.wrapCache = [];
	}
	static parse(t, n) {
		let r = new le(t, n);
		if (r.next == null) return e.empty;
		let i = ue(r);
		r.next && r.err("Unexpected trailing text");
		let a = be(_e(i));
		return xe(a, r), a;
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
		function i(o, s) {
			let c = o.matchFragment(e, n);
			if (c && (!t || c.validEnd)) return a.from(s.map((e) => e.createAndFill()));
			for (let e = 0; e < o.next.length; e++) {
				let { type: t, next: n } = o.next[e];
				if (!(t.isText || t.hasRequiredAttrs()) && r.indexOf(n) == -1) {
					r.push(n);
					let e = i(n, s.concat(t));
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
w.empty = new w(!0);
var le = class {
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
function ue(e) {
	let t = [];
	do
		t.push(de(e));
	while (e.eat("|"));
	return t.length == 1 ? t[0] : {
		type: "choice",
		exprs: t
	};
}
function de(e) {
	let t = [];
	do
		t.push(fe(e));
	while (e.next && e.next != ")" && e.next != "|");
	return t.length == 1 ? t[0] : {
		type: "seq",
		exprs: t
	};
}
function fe(e) {
	let t = ge(e);
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
	else if (e.eat("{")) t = me(e, t);
	else break;
	return t;
}
function pe(e) {
	/\D/.test(e.next) && e.err("Expected number, got '" + e.next + "'");
	let t = Number(e.next);
	return e.pos++, t;
}
function me(e, t) {
	let n = pe(e), r = n;
	return e.eat(",") && (r = e.next == "}" ? -1 : pe(e)), e.eat("}") || e.err("Unclosed braced range"), {
		type: "range",
		min: n,
		max: r,
		expr: t
	};
}
function he(e, t) {
	let n = e.nodeTypes, r = n[t];
	if (r) return [r];
	let i = [];
	for (let e in n) {
		let r = n[e];
		r.groups.indexOf(t) > -1 && i.push(r);
	}
	return i.length == 0 && e.err("No node type or group '" + t + "' found"), i;
}
function ge(e) {
	if (e.eat("(")) {
		let t = ue(e);
		return e.eat(")") || e.err("Missing closing paren"), t;
	}
	if (/\W/.test(e.next)) e.err("Unexpected token '" + e.next + "'");
	else {
		let t = he(e, e.next).map((t) => (e.inline == null ? e.inline = t.isInline : e.inline != t.isInline && e.err("Mixing inline and block content"), {
			type: "name",
			value: t
		}));
		return e.pos++, t.length == 1 ? t[0] : {
			type: "choice",
			exprs: t
		};
	}
}
function _e(e) {
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
function ve(e, t) {
	return t - e;
}
function ye(e, t) {
	let n = [];
	return r(t), n.sort(ve);
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
function be(e) {
	let t = Object.create(null);
	return n(ye(e, 0));
	function n(r) {
		let i = [];
		r.forEach((t) => {
			e[t].forEach(({ term: t, to: n }) => {
				if (!t) return;
				let r;
				for (let e = 0; e < i.length; e++) i[e][0] == t && (r = i[e][1]);
				ye(e, n).forEach((e) => {
					r || i.push([t, r = []]), r.indexOf(e) == -1 && r.push(e);
				});
			});
		});
		let a = t[r.join(",")] = new w(r.indexOf(e.length - 1) > -1);
		for (let e = 0; e < i.length; e++) {
			let r = i[e][1].sort(ve);
			a.next.push({
				type: i[e][0],
				next: t[r.join(",")] || n(r)
			});
		}
		return a;
	}
}
function xe(e, t) {
	for (let n = 0, r = [e]; n < r.length; n++) {
		let e = r[n], i = !e.validEnd, a = [];
		for (let t = 0; t < e.next.length; t++) {
			let { type: n, next: o } = e.next[t];
			a.push(n.name), i && !(n.isText || n.hasRequiredAttrs()) && (i = !1), r.indexOf(o) == -1 && r.push(o);
		}
		i && t.err("Only non-generatable nodes (" + a.join(", ") + ") in a required position (see https://prosemirror.net/docs/guide/#generatable)");
	}
}
function Se(e) {
	let t = Object.create(null);
	for (let n in e) {
		let r = e[n];
		if (!r.hasDefault) return null;
		t[n] = r.default;
	}
	return t;
}
function Ce(e, t) {
	let n = Object.create(null);
	for (let r in e) {
		let i = t && t[r];
		if (i === void 0) {
			let t = e[r];
			if (t.hasDefault) i = t.default;
			else throw RangeError("No value supplied for attribute " + r);
		}
		n[r] = i;
	}
	return n;
}
function we(e) {
	let t = Object.create(null);
	if (e) for (let n in e) t[n] = new Ee(e[n]);
	return t;
}
var Te = class e {
	constructor(e, t, n) {
		this.name = e, this.schema = t, this.spec = n, this.markSet = null, this.groups = n.group ? n.group.split(" ") : [], this.attrs = we(n.attrs), this.defaultAttrs = Se(this.attrs), this.contentMatch = null, this.inlineContent = null, this.isBlock = !(n.inline || e == "text"), this.isText = e == "text";
	}
	get isInline() {
		return !this.isBlock;
	}
	get isTextblock() {
		return this.isBlock && this.inlineContent;
	}
	get isLeaf() {
		return this.contentMatch == w.empty;
	}
	get isAtom() {
		return this.isLeaf || !!this.spec.atom;
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
		return !e && this.defaultAttrs ? this.defaultAttrs : Ce(this.attrs, e);
	}
	create(e = null, t, n) {
		if (this.isText) throw Error("NodeType.create can't construct text nodes");
		return new se(this, this.computeAttrs(e), a.from(t), l.setFrom(n));
	}
	createChecked(e = null, t, n) {
		return t = a.from(t), this.checkContent(t), new se(this, this.computeAttrs(e), t, l.setFrom(n));
	}
	createAndFill(e = null, t, n) {
		if (e = this.computeAttrs(e), t = a.from(t), t.size) {
			let e = this.contentMatch.fillBefore(t);
			if (!e) return null;
			t = e.append(t);
		}
		let r = this.contentMatch.matchFragment(t), i = r && r.fillBefore(a.empty, !0);
		return i ? new se(this, e, t.append(i), l.setFrom(n)) : null;
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
		return t ? t.length ? t : l.none : e;
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
}, Ee = class {
	constructor(e) {
		this.hasDefault = Object.prototype.hasOwnProperty.call(e, "default"), this.default = e.default;
	}
	get isRequired() {
		return !this.hasDefault;
	}
}, De = class e {
	constructor(e, t, n, r) {
		this.name = e, this.rank = t, this.schema = n, this.spec = r, this.attrs = we(r.attrs), this.excluded = null;
		let i = Se(this.attrs);
		this.instance = i ? new l(this, i) : null;
	}
	create(e = null) {
		return !e && this.instance ? this.instance : new l(this, Ce(this.attrs, e));
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
}, Oe = class {
	constructor(e) {
		this.cached = Object.create(null);
		let t = this.spec = {};
		for (let n in e) t[n] = e[n];
		t.nodes = n.from(e.nodes), t.marks = n.from(e.marks || {}), this.nodes = Te.compile(this.spec.nodes, this), this.marks = De.compile(this.spec.marks, this);
		let r = Object.create(null);
		for (let e in this.nodes) {
			if (e in this.marks) throw RangeError(e + " can not be both a node and a mark");
			let t = this.nodes[e], n = t.spec.content || "", i = t.spec.marks;
			t.contentMatch = r[n] || (r[n] = w.parse(n, this.nodes)), t.inlineContent = t.contentMatch.inlineContent, t.markSet = i == "_" ? null : i ? ke(this, i.split(" ")) : i == "" || !t.inlineContent ? [] : null;
		}
		for (let e in this.marks) {
			let t = this.marks[e], n = t.spec.excludes;
			t.excluded = n == null ? [t] : n == "" ? [] : ke(this, n.split(" "));
		}
		this.nodeFromJSON = this.nodeFromJSON.bind(this), this.markFromJSON = this.markFromJSON.bind(this), this.topNodeType = this.nodes[this.spec.topNode || "doc"], this.cached.wrappings = Object.create(null);
	}
	node(e, t = null, n, r) {
		if (typeof e == "string") e = this.nodeType(e);
		else if (!(e instanceof Te)) throw RangeError("Invalid node type: " + e);
		else if (e.schema != this) throw RangeError("Node type from different schema used (" + e.name + ")");
		return e.createChecked(t, n, r);
	}
	text(e, t) {
		let n = this.nodes.text;
		return new C(n, n.defaultAttrs, e, l.setFrom(t));
	}
	mark(e, t) {
		return typeof e == "string" && (e = this.marks[e]), e.create(t);
	}
	nodeFromJSON(e) {
		return se.fromJSON(this, e);
	}
	markFromJSON(e) {
		return l.fromJSON(this, e);
	}
	nodeType(e) {
		let t = this.nodes[e];
		if (!t) throw RangeError("Unknown node type: " + e);
		return t;
	}
};
function ke(e, t) {
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
function Ae(e) {
	return e.tag != null;
}
function je(e) {
	return e.style != null;
}
var Me = class e {
	constructor(e, t) {
		this.schema = e, this.rules = t, this.tags = [], this.styles = [], t.forEach((e) => {
			Ae(e) ? this.tags.push(e) : je(e) && this.styles.push(e);
		}), this.normalizeLists = !this.tags.some((t) => {
			if (!/^(ul|ol)\b/.test(t.tag) || !t.node) return !1;
			let n = e.nodes[t.node];
			return n.contentMatch.matchType(n);
		});
	}
	parse(e, t = {}) {
		let n = new Ve(this, t, !1);
		return n.addAll(e, t.from, t.to), n.finish();
	}
	parseSlice(e, t = {}) {
		let n = new Ve(this, t, !0);
		return n.addAll(e, t.from, t.to), d.maxOpen(n.finish());
	}
	matchTag(e, t, n) {
		for (let r = n ? this.tags.indexOf(n) + 1 : 0; r < this.tags.length; r++) {
			let n = this.tags[r];
			if (Ue(e, n.tag) && (n.namespace === void 0 || e.namespaceURI == n.namespace) && (!n.context || t.matchesContext(n.context))) {
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
				n(e = Ge(e)), e.mark || e.ignore || e.clearMark || (e.mark = t);
			});
		}
		for (let t in e.nodes) {
			let r = e.nodes[t].spec.parseDOM;
			r && r.forEach((e) => {
				n(e = Ge(e)), e.node || e.ignore || e.mark || (e.node = t);
			});
		}
		return t;
	}
	static fromSchema(t) {
		return t.cached.domParser || (t.cached.domParser = new e(t, e.schemaRules(t)));
	}
}, Ne = {
	address: !0,
	article: !0,
	aside: !0,
	blockquote: !0,
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
}, Pe = {
	head: !0,
	noscript: !0,
	object: !0,
	script: !0,
	style: !0,
	title: !0
}, Fe = {
	ol: !0,
	ul: !0
}, Ie = 1, Le = 2, Re = 4;
function ze(e, t, n) {
	return t == null ? e && e.whitespace == "pre" ? 3 : n & -5 : (t ? Ie : 0) | (t === "full" ? Le : 0);
}
var Be = class {
	constructor(e, t, n, r, i, a, o) {
		this.type = e, this.attrs = t, this.marks = n, this.pendingMarks = r, this.solid = i, this.options = o, this.content = [], this.activeMarks = l.none, this.stashMarks = [], this.match = a || (o & Re ? null : e.contentMatch);
	}
	findWrapping(e) {
		if (!this.match) {
			if (!this.type) return [];
			let t = this.type.contentMatch.fillBefore(a.from(e));
			if (t) this.match = this.type.contentMatch.matchFragment(t);
			else {
				let t = this.type.contentMatch, n;
				return (n = t.findWrapping(e.type)) ? (this.match = t, n) : null;
			}
		}
		return this.match.findWrapping(e.type);
	}
	finish(e) {
		if (!(this.options & Ie)) {
			let e = this.content[this.content.length - 1], t;
			if (e && e.isText && (t = /[ \t\r\n\u000c]+$/.exec(e.text))) {
				let n = e;
				e.text.length == t[0].length ? this.content.pop() : this.content[this.content.length - 1] = n.withText(n.text.slice(0, n.text.length - t[0].length));
			}
		}
		let t = a.from(this.content);
		return !e && this.match && (t = t.append(this.match.fillBefore(a.empty, !0))), this.type ? this.type.create(this.attrs, t, this.marks) : t;
	}
	popFromStashMark(e) {
		for (let t = this.stashMarks.length - 1; t >= 0; t--) if (e.eq(this.stashMarks[t])) return this.stashMarks.splice(t, 1)[0];
	}
	applyPending(e) {
		for (let t = 0, n = this.pendingMarks; t < n.length; t++) {
			let r = n[t];
			(this.type ? this.type.allowsMarkType(r.type) : Ke(r.type, e)) && !r.isInSet(this.activeMarks) && (this.activeMarks = r.addToSet(this.activeMarks), this.pendingMarks = r.removeFromSet(this.pendingMarks));
		}
	}
	inlineContext(e) {
		return this.type ? this.type.inlineContent : this.content.length ? this.content[0].isInline : e.parentNode && !Ne.hasOwnProperty(e.parentNode.nodeName.toLowerCase());
	}
}, Ve = class {
	constructor(e, t, n) {
		this.parser = e, this.options = t, this.isOpen = n, this.open = 0;
		let r = t.topNode, i, a = ze(null, t.preserveWhitespace, 0) | (n ? Re : 0);
		i = r ? new Be(r.type, r.attrs, l.none, l.none, !0, t.topMatch || r.type.contentMatch, a) : n ? new Be(null, null, l.none, l.none, !0, null, a) : new Be(e.schema.topNodeType, null, l.none, l.none, !0, null, a), this.nodes = [i], this.find = t.findPositions, this.needsBlock = !1;
	}
	get top() {
		return this.nodes[this.open];
	}
	addDOM(e) {
		e.nodeType == 3 ? this.addTextNode(e) : e.nodeType == 1 && this.addElement(e);
	}
	withStyleRules(e, t) {
		let n = e.getAttribute("style");
		if (!n) return t();
		let r = this.readStyles(We(n));
		if (!r) return;
		let [i, a] = r, o = this.top;
		for (let e = 0; e < a.length; e++) this.removePendingMark(a[e], o);
		for (let e = 0; e < i.length; e++) this.addPendingMark(i[e]);
		t();
		for (let e = 0; e < i.length; e++) this.removePendingMark(i[e], o);
		for (let e = 0; e < a.length; e++) this.addPendingMark(a[e]);
	}
	addTextNode(e) {
		let t = e.nodeValue, n = this.top;
		if (n.options & Le || n.inlineContext(e) || /[^ \t\r\n\u000c]/.test(t)) {
			if (n.options & Ie) t = n.options & Le ? t.replace(/\r\n?/g, "\n") : t.replace(/\r?\n|\r/g, " ");
			else if (t = t.replace(/[ \t\r\n\u000c]+/g, " "), /^[ \t\r\n\u000c]/.test(t) && this.open == this.nodes.length - 1) {
				let r = n.content[n.content.length - 1], i = e.previousSibling;
				(!r || i && i.nodeName == "BR" || r.isText && /[ \t\r\n\u000c]$/.test(r.text)) && (t = t.slice(1));
			}
			t && this.insertNode(this.parser.schema.text(t)), this.findInText(e);
		} else this.findInside(e);
	}
	addElement(e, t) {
		let n = e.nodeName.toLowerCase(), r;
		Fe.hasOwnProperty(n) && this.parser.normalizeLists && He(e);
		let i = this.options.ruleFromNode && this.options.ruleFromNode(e) || (r = this.parser.matchTag(e, this, t));
		if (i ? i.ignore : Pe.hasOwnProperty(n)) this.findInside(e), this.ignoreFallback(e);
		else if (!i || i.skip || i.closeParent) {
			i && i.closeParent ? this.open = Math.max(0, this.open - 1) : i && i.skip.nodeType && (e = i.skip);
			let t, r = this.top, a = this.needsBlock;
			if (Ne.hasOwnProperty(n)) r.content.length && r.content[0].isInline && this.open && (this.open--, r = this.top), t = !0, r.type || (this.needsBlock = !0);
			else if (!e.firstChild) {
				this.leafFallback(e);
				return;
			}
			i && i.skip ? this.addAll(e) : this.withStyleRules(e, () => this.addAll(e)), t && this.sync(r), this.needsBlock = a;
		} else this.withStyleRules(e, () => {
			this.addElementByRule(e, i, i.consuming === !1 ? r : void 0);
		});
	}
	leafFallback(e) {
		e.nodeName == "BR" && this.top.type && this.top.type.inlineContent && this.addTextNode(e.ownerDocument.createTextNode("\n"));
	}
	ignoreFallback(e) {
		e.nodeName == "BR" && (!this.top.type || !this.top.type.inlineContent) && this.findPlace(this.parser.schema.text("-"));
	}
	readStyles(e) {
		let t = l.none, n = l.none;
		for (let r = 0; r < e.length; r += 2) for (let i;;) {
			let a = this.parser.matchStyle(e[r], e[r + 1], this, i);
			if (!a) break;
			if (a.ignore) return null;
			if (a.clearMark ? this.top.pendingMarks.concat(this.top.activeMarks).forEach((e) => {
				a.clearMark(e) && (n = e.addToSet(n));
			}) : t = this.parser.schema.marks[a.mark].create(a.attrs).addToSet(t), a.consuming === !1) i = a;
			else break;
		}
		return [t, n];
	}
	addElementByRule(e, t, n) {
		let r, i, a;
		t.node ? (i = this.parser.schema.nodes[t.node], i.isLeaf ? this.insertNode(i.create(t.attrs)) || this.leafFallback(e) : r = this.enter(i, t.attrs || null, t.preserveWhitespace)) : (a = this.parser.schema.marks[t.mark].create(t.attrs), this.addPendingMark(a));
		let o = this.top;
		if (i && i.isLeaf) this.findInside(e);
		else if (n) this.addElement(e, n);
		else if (t.getContent) this.findInside(e), t.getContent(e, this.parser.schema).forEach((e) => this.insertNode(e));
		else {
			let n = e;
			typeof t.contentElement == "string" ? n = e.querySelector(t.contentElement) : typeof t.contentElement == "function" ? n = t.contentElement(e) : t.contentElement && (n = t.contentElement), this.findAround(e, n, !0), this.addAll(n);
		}
		r && this.sync(o) && this.open--, a && this.removePendingMark(a, o);
	}
	addAll(e, t, n) {
		let r = t || 0;
		for (let i = t ? e.childNodes[t] : e.firstChild, a = n == null ? null : e.childNodes[n]; i != a; i = i.nextSibling, ++r) this.findAtPoint(e, r), this.addDOM(i);
		this.findAtPoint(e, r);
	}
	findPlace(e) {
		let t, n;
		for (let r = this.open; r >= 0; r--) {
			let i = this.nodes[r], a = i.findWrapping(e);
			if (a && (!t || t.length > a.length) && (t = a, n = i, !a.length) || i.solid) break;
		}
		if (!t) return !1;
		this.sync(n);
		for (let e = 0; e < t.length; e++) this.enterInner(t[e], null, !1);
		return !0;
	}
	insertNode(e) {
		if (e.isInline && this.needsBlock && !this.top.type) {
			let e = this.textblockFromContext();
			e && this.enterInner(e);
		}
		if (this.findPlace(e)) {
			this.closeExtra();
			let t = this.top;
			t.applyPending(e.type), t.match && (t.match = t.match.matchType(e.type));
			let n = t.activeMarks;
			for (let r = 0; r < e.marks.length; r++) (!t.type || t.type.allowsMarkType(e.marks[r].type)) && (n = e.marks[r].addToSet(n));
			return t.content.push(e.mark(n)), !0;
		}
		return !1;
	}
	enter(e, t, n) {
		let r = this.findPlace(e.create(t));
		return r && this.enterInner(e, t, !0, n), r;
	}
	enterInner(e, t = null, n = !1, r) {
		this.closeExtra();
		let i = this.top;
		i.applyPending(e), i.match = i.match && i.match.matchType(e);
		let a = ze(e, r, i.options);
		i.options & Re && i.content.length == 0 && (a |= Re), this.nodes.push(new Be(e, t, i.activeMarks, i.pendingMarks, n, null, a)), this.open++;
	}
	closeExtra(e = !1) {
		let t = this.nodes.length - 1;
		if (t > this.open) {
			for (; t > this.open; t--) this.nodes[t - 1].content.push(this.nodes[t].finish(e));
			this.nodes.length = this.open + 1;
		}
	}
	finish() {
		return this.open = 0, this.closeExtra(this.isOpen), this.nodes[0].finish(this.isOpen || this.options.topOpen);
	}
	sync(e) {
		for (let t = this.open; t >= 0; t--) if (this.nodes[t] == e) return this.open = t, !0;
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
					if (!e || e.name != s && e.groups.indexOf(s) == -1) return !1;
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
	addPendingMark(e) {
		let t = qe(e, this.top.pendingMarks);
		t && this.top.stashMarks.push(t), this.top.pendingMarks = e.addToSet(this.top.pendingMarks);
	}
	removePendingMark(e, t) {
		for (let n = this.open; n >= 0; n--) {
			let r = this.nodes[n];
			if (r.pendingMarks.lastIndexOf(e) > -1) r.pendingMarks = e.removeFromSet(r.pendingMarks);
			else {
				r.activeMarks = e.removeFromSet(r.activeMarks);
				let t = r.popFromStashMark(e);
				t && r.type && r.type.allowsMarkType(t.type) && (r.activeMarks = t.addToSet(r.activeMarks));
			}
			if (r == t) break;
		}
	}
};
function He(e) {
	for (let t = e.firstChild, n = null; t; t = t.nextSibling) {
		let e = t.nodeType == 1 ? t.nodeName.toLowerCase() : null;
		e && Fe.hasOwnProperty(e) && n ? (n.appendChild(t), t = n) : e == "li" ? n = t : e && (n = null);
	}
}
function Ue(e, t) {
	return (e.matches || e.msMatchesSelector || e.webkitMatchesSelector || e.mozMatchesSelector).call(e, t);
}
function We(e) {
	let t = /\s*([\w-]+)\s*:\s*([^;]+)/g, n, r = [];
	for (; n = t.exec(e);) r.push(n[1], n[2].trim());
	return r;
}
function Ge(e) {
	let t = {};
	for (let n in e) t[n] = e[n];
	return t;
}
function Ke(e, t) {
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
function qe(e, t) {
	for (let n = 0; n < t.length; n++) if (e.eq(t[n])) return t[n];
}
var Je = class e {
	constructor(e, t) {
		this.nodes = e, this.marks = t;
	}
	serializeFragment(e, t = {}, n) {
		n || (n = Xe(t).createDocumentFragment());
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
	serializeNodeInner(t, n) {
		let { dom: r, contentDOM: i } = e.renderSpec(Xe(n), this.nodes[t.type.name](t));
		if (i) {
			if (t.isLeaf) throw RangeError("Content hole not allowed in a leaf node spec");
			this.serializeFragment(t.content, n, i);
		}
		return r;
	}
	serializeNode(e, t = {}) {
		let n = this.serializeNodeInner(e, t);
		for (let r = e.marks.length - 1; r >= 0; r--) {
			let i = this.serializeMark(e.marks[r], e.isInline, t);
			i && ((i.contentDOM || i.dom).appendChild(n), n = i.dom);
		}
		return n;
	}
	serializeMark(t, n, r = {}) {
		let i = this.marks[t.type.name];
		return i && e.renderSpec(Xe(r), i(t, n));
	}
	static renderSpec(t, n, r = null) {
		if (typeof n == "string") return { dom: t.createTextNode(n) };
		if (n.nodeType != null) return { dom: n };
		if (n.dom && n.dom.nodeType != null) return n;
		let i = n[0], a = i.indexOf(" ");
		a > 0 && (r = i.slice(0, a), i = i.slice(a + 1));
		let o, s = r ? t.createElementNS(r, i) : t.createElement(i), c = n[1], l = 1;
		if (c && typeof c == "object" && c.nodeType == null && !Array.isArray(c)) {
			l = 2;
			for (let e in c) if (c[e] != null) {
				let t = e.indexOf(" ");
				t > 0 ? s.setAttributeNS(e.slice(0, t), e.slice(t + 1), c[e]) : s.setAttribute(e, c[e]);
			}
		}
		for (let i = l; i < n.length; i++) {
			let a = n[i];
			if (a === 0) {
				if (i < n.length - 1 || i > l) throw RangeError("Content hole must be the only child of its parent node");
				return {
					dom: s,
					contentDOM: s
				};
			}
			{
				let { dom: n, contentDOM: i } = e.renderSpec(t, a, r);
				if (s.appendChild(n), i) {
					if (o) throw RangeError("Multiple content holes");
					o = i;
				}
			}
		}
		return {
			dom: s,
			contentDOM: o
		};
	}
	static fromSchema(t) {
		return t.cached.domSerializer || (t.cached.domSerializer = new e(this.nodesFromSchema(t), this.marksFromSchema(t)));
	}
	static nodesFromSchema(e) {
		let t = Ye(e.nodes);
		return t.text || (t.text = (e) => e.text), t;
	}
	static marksFromSchema(e) {
		return Ye(e.marks);
	}
};
function Ye(e) {
	let t = {};
	for (let n in e) {
		let r = e[n].spec.toDOM;
		r && (t[n] = r);
	}
	return t;
}
function Xe(e) {
	return e.document || window.document;
}
//#endregion
//#region node_modules/prosemirror-transform/dist/index.js
var Ze = 65535, Qe = 2 ** 16;
function $e(e, t) {
	return e + t * Qe;
}
function et(e) {
	return e & Ze;
}
function tt(e) {
	return (e - (e & Ze)) / Qe;
}
var nt = 1, rt = 2, it = 4, at = 8, ot = class {
	constructor(e, t, n) {
		this.pos = e, this.delInfo = t, this.recover = n;
	}
	get deleted() {
		return (this.delInfo & at) > 0;
	}
	get deletedBefore() {
		return (this.delInfo & 5) > 0;
	}
	get deletedAfter() {
		return (this.delInfo & 6) > 0;
	}
	get deletedAcross() {
		return (this.delInfo & it) > 0;
	}
}, st = class e {
	constructor(t, n = !1) {
		if (this.ranges = t, this.inverted = n, !t.length && e.empty) return e.empty;
	}
	recover(e) {
		let t = 0, n = et(e);
		if (!this.inverted) for (let e = 0; e < n; e++) t += this.ranges[e * 3 + 2] - this.ranges[e * 3 + 1];
		return this.ranges[n * 3] + t + tt(e);
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
				let d = e == (t < 0 ? s : u) ? null : $e(o / 3, e - s), f = e == s ? rt : e == u ? nt : it;
				return (t < 0 ? e != s : e != u) && (f |= at), new ot(a, f, d);
			}
			r += l - c;
		}
		return n ? e + r : new ot(e + r, 0, null);
	}
	touches(e, t) {
		let n = 0, r = et(t), i = this.inverted ? 2 : 1, a = this.inverted ? 1 : 2;
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
st.empty = new st([]);
var ct = class e {
	constructor(e = [], t, n = 0, r = e.length) {
		this.maps = e, this.mirror = t, this.from = n, this.to = r;
	}
	slice(t = 0, n = this.maps.length) {
		return new e(this.maps, this.mirror, t, n);
	}
	copy() {
		return new e(this.maps.slice(), this.mirror && this.mirror.slice(), this.from, this.to);
	}
	appendMap(e, t) {
		this.to = this.maps.push(e), t != null && this.setMirror(this.maps.length - 1, t);
	}
	appendMapping(e) {
		for (let t = 0, n = this.maps.length; t < e.maps.length; t++) {
			let r = e.getMirror(t);
			this.appendMap(e.maps[t], r != null && r < t ? n + r : void 0);
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
		for (let t = e.maps.length - 1, n = this.maps.length + e.maps.length; t >= 0; t--) {
			let r = e.getMirror(t);
			this.appendMap(e.maps[t].invert(), r != null && r > t ? n - r - 1 : void 0);
		}
	}
	invert() {
		let t = new e();
		return t.appendMappingInverted(this), t;
	}
	map(e, t = 1) {
		if (this.mirror) return this._map(e, t, !0);
		for (let n = this.from; n < this.to; n++) e = this.maps[n].map(e, t);
		return e;
	}
	mapResult(e, t = 1) {
		return this._map(e, t, !1);
	}
	_map(e, t, n) {
		let r = 0;
		for (let n = this.from; n < this.to; n++) {
			let i = this.maps[n].mapResult(e, t);
			if (i.recover != null) {
				let t = this.getMirror(n);
				if (t != null && t > n && t < this.to) {
					n = t, e = this.maps[t].recover(i.recover);
					continue;
				}
			}
			r |= i.delInfo, e = i.pos;
		}
		return n ? e : new ot(e, r, null);
	}
}, lt = Object.create(null), ut = class {
	getMap() {
		return st.empty;
	}
	merge(e) {
		return null;
	}
	static fromJSON(e, t) {
		if (!t || !t.stepType) throw RangeError("Invalid input for Step.fromJSON");
		let n = lt[t.stepType];
		if (!n) throw RangeError(`No step type ${t.stepType} defined`);
		return n.fromJSON(e, t);
	}
	static jsonID(e, t) {
		if (e in lt) throw RangeError("Duplicate use of step JSON ID " + e);
		return lt[e] = t, t.prototype.jsonID = e, t;
	}
}, T = class e {
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
			if (t instanceof u) return e.fail(t.message);
			throw t;
		}
	}
};
function dt(e, t, n) {
	let r = [];
	for (let i = 0; i < e.childCount; i++) {
		let a = e.child(i);
		a.content.size && (a = a.copy(dt(a.content, t, a))), a.isInline && (a = t(a, n, i)), r.push(a);
	}
	return a.fromArray(r);
}
var ft = class e extends ut {
	constructor(e, t, n) {
		super(), this.from = e, this.to = t, this.mark = n;
	}
	apply(e) {
		let t = e.slice(this.from, this.to), n = e.resolve(this.from), r = n.node(n.sharedDepth(this.to)), i = new d(dt(t.content, (e, t) => !e.isAtom || !t.type.allowsMarkType(this.mark.type) ? e : e.mark(this.mark.addToSet(e.marks)), r), t.openStart, t.openEnd);
		return T.fromReplace(e, this.from, this.to, i);
	}
	invert() {
		return new pt(this.from, this.to, this.mark);
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
ut.jsonID("addMark", ft);
var pt = class e extends ut {
	constructor(e, t, n) {
		super(), this.from = e, this.to = t, this.mark = n;
	}
	apply(e) {
		let t = e.slice(this.from, this.to), n = new d(dt(t.content, (e) => e.mark(this.mark.removeFromSet(e.marks)), e), t.openStart, t.openEnd);
		return T.fromReplace(e, this.from, this.to, n);
	}
	invert() {
		return new ft(this.from, this.to, this.mark);
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
ut.jsonID("removeMark", pt);
var mt = class e extends ut {
	constructor(e, t) {
		super(), this.pos = e, this.mark = t;
	}
	apply(e) {
		let t = e.nodeAt(this.pos);
		if (!t) return T.fail("No node at mark step's position");
		let n = t.type.create(t.attrs, null, this.mark.addToSet(t.marks));
		return T.fromReplace(e, this.pos, this.pos + 1, new d(a.from(n), 0, +!t.isLeaf));
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
		return new ht(this.pos, this.mark);
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
ut.jsonID("addNodeMark", mt);
var ht = class e extends ut {
	constructor(e, t) {
		super(), this.pos = e, this.mark = t;
	}
	apply(e) {
		let t = e.nodeAt(this.pos);
		if (!t) return T.fail("No node at mark step's position");
		let n = t.type.create(t.attrs, null, this.mark.removeFromSet(t.marks));
		return T.fromReplace(e, this.pos, this.pos + 1, new d(a.from(n), 0, +!t.isLeaf));
	}
	invert(e) {
		let t = e.nodeAt(this.pos);
		return !t || !this.mark.isInSet(t.marks) ? this : new mt(this.pos, this.mark);
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
ut.jsonID("removeNodeMark", ht);
var gt = class e extends ut {
	constructor(e, t, n, r = !1) {
		super(), this.from = e, this.to = t, this.slice = n, this.structure = r;
	}
	apply(e) {
		return this.structure && vt(e, this.from, this.to) ? T.fail("Structure replace would overwrite content") : T.fromReplace(e, this.from, this.to, this.slice);
	}
	getMap() {
		return new st([
			this.from,
			this.to - this.from,
			this.slice.size
		]);
	}
	invert(t) {
		return new e(this.from, this.from + this.slice.size, t.slice(this.from, this.to));
	}
	map(t) {
		let n = t.mapResult(this.from, 1), r = t.mapResult(this.to, -1);
		return n.deletedAcross && r.deletedAcross ? null : new e(n.pos, Math.max(n.pos, r.pos), this.slice);
	}
	merge(t) {
		if (!(t instanceof e) || t.structure || this.structure) return null;
		if (this.from + this.slice.size == t.from && !this.slice.openEnd && !t.slice.openStart) {
			let n = this.slice.size + t.slice.size == 0 ? d.empty : new d(this.slice.content.append(t.slice.content), this.slice.openStart, t.slice.openEnd);
			return new e(this.from, this.to + (t.to - t.from), n, this.structure);
		}
		if (t.to == this.from && !this.slice.openStart && !t.slice.openEnd) {
			let n = this.slice.size + t.slice.size == 0 ? d.empty : new d(t.slice.content.append(this.slice.content), t.slice.openStart, this.slice.openEnd);
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
		return new e(n.from, n.to, d.fromJSON(t, n.slice), !!n.structure);
	}
};
ut.jsonID("replace", gt);
var _t = class e extends ut {
	constructor(e, t, n, r, i, a, o = !1) {
		super(), this.from = e, this.to = t, this.gapFrom = n, this.gapTo = r, this.slice = i, this.insert = a, this.structure = o;
	}
	apply(e) {
		if (this.structure && (vt(e, this.from, this.gapFrom) || vt(e, this.gapTo, this.to))) return T.fail("Structure gap-replace would overwrite content");
		let t = e.slice(this.gapFrom, this.gapTo);
		if (t.openStart || t.openEnd) return T.fail("Gap is not a flat range");
		let n = this.slice.insertAt(this.insert, t.content);
		return n ? T.fromReplace(e, this.from, this.to, n) : T.fail("Content does not fit in gap");
	}
	getMap() {
		return new st([
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
		let n = t.mapResult(this.from, 1), r = t.mapResult(this.to, -1), i = t.map(this.gapFrom, -1), a = t.map(this.gapTo, 1);
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
		return new e(n.from, n.to, n.gapFrom, n.gapTo, d.fromJSON(t, n.slice), n.insert, !!n.structure);
	}
};
ut.jsonID("replaceAround", _t);
function vt(e, t, n) {
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
function yt(e, t, n, r) {
	let i = [], a = [], o, s;
	e.doc.nodesBetween(t, n, (e, c, l) => {
		if (!e.isInline) return;
		let u = e.marks;
		if (!r.isInSet(u) && l.type.allowsMarkType(r.type)) {
			let l = Math.max(c, t), d = Math.min(c + e.nodeSize, n), f = r.addToSet(u);
			for (let e = 0; e < u.length; e++) u[e].isInSet(f) || (o && o.to == l && o.mark.eq(u[e]) ? o.to = d : i.push(o = new pt(l, d, u[e])));
			s && s.to == l ? s.to = d : a.push(s = new ft(l, d, r));
		}
	}), i.forEach((t) => e.step(t)), a.forEach((t) => e.step(t));
}
function bt(e, t, n, r) {
	let i = [], a = 0;
	e.doc.nodesBetween(t, n, (e, o) => {
		if (!e.isInline) return;
		a++;
		let s = null;
		if (r instanceof De) {
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
	}), i.forEach((t) => e.step(new pt(t.from, t.to, t.style)));
}
function xt(e, t, n, r = n.contentMatch) {
	let i = e.doc.nodeAt(t), o = [], s = t + 1;
	for (let t = 0; t < i.childCount; t++) {
		let a = i.child(t), c = s + a.nodeSize, l = r.matchType(a.type);
		if (!l) o.push(new gt(s, c, d.empty));
		else {
			r = l;
			for (let t = 0; t < a.marks.length; t++) n.allowsMarkType(a.marks[t].type) || e.step(new pt(s, c, a.marks[t]));
		}
		s = c;
	}
	if (!r.validEnd) {
		let t = r.fillBefore(a.empty, !0);
		e.replace(s, s, new d(t, 0, 0));
	}
	for (let t = o.length - 1; t >= 0; t--) e.step(o[t]);
}
function St(e, t, n) {
	return (t == 0 || e.canReplace(t, e.childCount)) && (n == e.childCount || e.canReplace(0, n));
}
function Ct(e) {
	let t = e.parent.content.cutByIndex(e.startIndex, e.endIndex);
	for (let n = e.depth;; --n) {
		let r = e.$from.node(n), i = e.$from.index(n), a = e.$to.indexAfter(n);
		if (n < e.depth && r.canReplace(i, a, t)) return n;
		if (n == 0 || r.type.spec.isolating || !St(r, i, a)) break;
	}
	return null;
}
function wt(e, t, n) {
	let { $from: r, $to: i, depth: o } = t, s = r.before(o + 1), c = i.after(o + 1), l = s, u = c, f = a.empty, p = 0;
	for (let e = o, t = !1; e > n; e--) t || r.index(e) > 0 ? (t = !0, f = a.from(r.node(e).copy(f)), p++) : l--;
	let m = a.empty, h = 0;
	for (let e = o, t = !1; e > n; e--) t || i.after(e + 1) < i.end(e) ? (t = !0, m = a.from(i.node(e).copy(m)), h++) : u++;
	e.step(new _t(l, u, s, c, new d(f.append(m), p, h), f.size - p, !0));
}
function Tt(e, t, n = null, r = e) {
	let i = Dt(e, t), a = i && Ot(r, t);
	return a ? i.map(Et).concat({
		type: t,
		attrs: n
	}).concat(a.map(Et)) : null;
}
function Et(e) {
	return {
		type: e,
		attrs: null
	};
}
function Dt(e, t) {
	let { parent: n, startIndex: r, endIndex: i } = e, a = n.contentMatchAt(r).findWrapping(t);
	if (!a) return null;
	let o = a.length ? a[0] : t;
	return n.canReplaceWith(r, i, o) ? a : null;
}
function Ot(e, t) {
	let { parent: n, startIndex: r, endIndex: i } = e, a = n.child(r), o = t.contentMatch.findWrapping(a.type);
	if (!o) return null;
	let s = (o.length ? o[o.length - 1] : t).contentMatch;
	for (let e = r; s && e < i; e++) s = s.matchType(n.child(e).type);
	return !s || !s.validEnd ? null : o;
}
function kt(e, t, n) {
	let r = a.empty;
	for (let e = n.length - 1; e >= 0; e--) {
		if (r.size) {
			let t = n[e].type.contentMatch.matchFragment(r);
			if (!t || !t.validEnd) throw RangeError("Wrapper type given to Transform.wrap does not form valid content of its parent wrapper");
		}
		r = a.from(n[e].type.create(n[e].attrs, r));
	}
	let i = t.start, o = t.end;
	e.step(new _t(i, o, i, o, new d(r, 0, 0), n.length, !0));
}
function At(e, t, n, r, i) {
	if (!r.isTextblock) throw RangeError("Type given to setBlockType should be a textblock");
	let o = e.steps.length;
	e.doc.nodesBetween(t, n, (t, n) => {
		if (t.isTextblock && !t.hasMarkup(r, i) && jt(e.doc, e.mapping.slice(o).map(n), r)) {
			e.clearIncompatible(e.mapping.slice(o).map(n, 1), r);
			let s = e.mapping.slice(o), c = s.map(n, 1), l = s.map(n + t.nodeSize, 1);
			return e.step(new _t(c, l, c + 1, l - 1, new d(a.from(r.create(i, null, t.marks)), 0, 0), 1, !0)), !1;
		}
	});
}
function jt(e, t, n) {
	let r = e.resolve(t), i = r.index();
	return r.parent.canReplaceWith(i, i + 1, n);
}
function Mt(e, t, n, r, i) {
	let o = e.doc.nodeAt(t);
	if (!o) throw RangeError("No node at given position");
	n || (n = o.type);
	let s = n.create(r, null, i || o.marks);
	if (o.isLeaf) return e.replaceWith(t, t + o.nodeSize, s);
	if (!n.validContent(o.content)) throw RangeError("Invalid content for node type " + n.name);
	e.step(new _t(t, t + o.nodeSize, t + 1, t + o.nodeSize - 1, new d(a.from(s), 0, 0), 1, !0));
}
function Nt(e, t, n = 1, r) {
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
function Pt(e, t, n = 1, r) {
	let i = e.doc.resolve(t), o = a.empty, s = a.empty;
	for (let e = i.depth, t = i.depth - n, c = n - 1; e > t; e--, c--) {
		o = a.from(i.node(e).copy(o));
		let t = r && r[c];
		s = a.from(t ? t.type.create(t.attrs, s) : i.node(e).copy(s));
	}
	e.step(new gt(t, t, new d(o.append(s), n, n), !0));
}
function Ft(e, t) {
	let n = e.resolve(t), r = n.index();
	return It(n.nodeBefore, n.nodeAfter) && n.parent.canReplace(r, r + 1);
}
function It(e, t) {
	return !!(e && t && !e.isLeaf && e.canAppend(t));
}
function Lt(e, t, n = -1) {
	let r = e.resolve(t);
	for (let e = r.depth;; e--) {
		let i, a, o = r.index(e);
		if (e == r.depth ? (i = r.nodeBefore, a = r.nodeAfter) : n > 0 ? (i = r.node(e + 1), o++, a = r.node(e).maybeChild(o)) : (i = r.node(e).maybeChild(o - 1), a = r.node(e + 1)), i && !i.isTextblock && It(i, a) && r.node(e).canReplace(o, o + 1)) return t;
		if (e == 0) break;
		t = n < 0 ? r.before(e) : r.after(e);
	}
}
function Rt(e, t, n) {
	let r = new gt(t - n, t + n, d.empty, !0);
	e.step(r);
}
function zt(e, t, n) {
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
function Bt(e, t, n) {
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
function Vt(e, t, n = t, r = d.empty) {
	if (t == n && !r.size) return null;
	let i = e.resolve(t), a = e.resolve(n);
	return Ht(i, a, r) ? new gt(t, n, r) : new Ut(i, a, r).fit();
}
function Ht(e, t, n) {
	return !n.openStart && !n.openEnd && e.start() == t.start() && e.parent.canReplace(e.index(), t.index(), n.content);
}
var Ut = class {
	constructor(e, t, n) {
		this.$from = e, this.$to = t, this.unplaced = n, this.frontier = [], this.placed = a.empty;
		for (let t = 0; t <= e.depth; t++) {
			let n = e.node(t);
			this.frontier.push({
				type: n.type,
				match: n.contentMatchAt(e.indexAfter(t))
			});
		}
		for (let t = e.depth; t > 0; t--) this.placed = a.from(e.node(t).copy(this.placed));
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
		let s = new d(i, a, o);
		return e > -1 ? new _t(n.pos, e, this.$to.pos, this.$to.end(), s, t) : s.size || n.pos != this.$to.pos ? new gt(n.pos, r.pos, s) : null;
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
			n ? (r = Kt(this.unplaced.content, n - 1).firstChild, e = r.content) : e = this.unplaced.content;
			let i = e.firstChild;
			for (let e = this.depth; e >= 0; e--) {
				let { type: o, match: s } = this.frontier[e], c, l = null;
				if (t == 1 && (i ? s.matchType(i.type) || (l = s.fillBefore(a.from(i), !1)) : r && o.compatibleContent(r.type))) return {
					sliceDepth: n,
					frontierDepth: e,
					parent: r,
					inject: l
				};
				if (t == 2 && i && (c = s.findWrapping(i.type))) return {
					sliceDepth: n,
					frontierDepth: e,
					parent: r,
					wrap: c
				};
				if (r && s.matchType(r.type)) break;
			}
		}
	}
	openMore() {
		let { content: e, openStart: t, openEnd: n } = this.unplaced, r = Kt(e, t);
		return !r.childCount || r.firstChild.isLeaf ? !1 : (this.unplaced = new d(e, t + 1, Math.max(n, r.size + t >= e.size - n ? t + 1 : 0)), !0);
	}
	dropNode() {
		let { content: e, openStart: t, openEnd: n } = this.unplaced, r = Kt(e, t);
		if (r.childCount <= 1 && t > 0) {
			let i = e.size - t <= t + r.size;
			this.unplaced = new d(Wt(e, t - 1, 1), t - 1, i ? t - 1 : n);
		} else this.unplaced = new d(Wt(e, t, 1), t, n);
	}
	placeNodes({ sliceDepth: e, frontierDepth: t, parent: n, inject: r, wrap: i }) {
		for (; this.depth > t;) this.closeFrontierNode();
		if (i) for (let e = 0; e < i.length; e++) this.openFrontierNode(i[e]);
		let o = this.unplaced, s = n ? n.content : o.content, c = o.openStart - e, l = 0, u = [], { match: f, type: p } = this.frontier[t];
		if (r) {
			for (let e = 0; e < r.childCount; e++) u.push(r.child(e));
			f = f.matchFragment(r);
		}
		let m = s.size + e - (o.content.size - o.openEnd);
		for (; l < s.childCount;) {
			let e = s.child(l), t = f.matchType(e.type);
			if (!t) break;
			l++, (l > 1 || c == 0 || e.content.size) && (f = t, u.push(qt(e.mark(p.allowedMarks(e.marks)), l == 1 ? c : 0, l == s.childCount ? m : -1)));
		}
		let h = l == s.childCount;
		h || (m = -1), this.placed = Gt(this.placed, t, a.from(u)), this.frontier[t].match = f, h && m < 0 && n && n.type == this.frontier[this.depth].type && this.frontier.length > 1 && this.closeFrontierNode();
		for (let e = 0, t = s; e < m; e++) {
			let e = t.lastChild;
			this.frontier.push({
				type: e.type,
				match: e.contentMatchAt(e.childCount)
			}), t = e.content;
		}
		this.unplaced = h ? e == 0 ? d.empty : new d(Wt(o.content, e - 1, 1), e - 1, m < 0 ? o.openEnd : e - 1) : new d(Wt(o.content, e, l), o.openStart, o.openEnd);
	}
	mustMoveInline() {
		if (!this.$to.parent.isTextblock) return -1;
		let e = this.frontier[this.depth], t;
		if (!e.type.isTextblock || !Jt(this.$to, this.$to.depth, e.type, e.match, !1) || this.$to.depth == this.depth && (t = this.findCloseLevel(this.$to)) && t.depth == this.depth) return -1;
		let { depth: n } = this.$to, r = this.$to.after(n);
		for (; n > 1 && r == this.$to.end(--n);) ++r;
		return r;
	}
	findCloseLevel(e) {
		scan: for (let t = Math.min(this.depth, e.depth); t >= 0; t--) {
			let { match: n, type: r } = this.frontier[t], i = t < e.depth && e.end(t + 1) == e.pos + (e.depth - (t + 1)), a = Jt(e, t, r, n, i);
			if (a) {
				for (let n = t - 1; n >= 0; n--) {
					let { match: t, type: r } = this.frontier[n], i = Jt(e, n, r, t, !0);
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
		t.fit.childCount && (this.placed = Gt(this.placed, t.depth, t.fit)), e = t.move;
		for (let n = t.depth + 1; n <= e.depth; n++) {
			let t = e.node(n), r = t.type.contentMatch.fillBefore(t.content, !0, e.index(n));
			this.openFrontierNode(t.type, t.attrs, r);
		}
		return e;
	}
	openFrontierNode(e, t = null, n) {
		let r = this.frontier[this.depth];
		r.match = r.match.matchType(e), this.placed = Gt(this.placed, this.depth, a.from(e.create(t, n))), this.frontier.push({
			type: e,
			match: e.contentMatch
		});
	}
	closeFrontierNode() {
		let e = this.frontier.pop().match.fillBefore(a.empty, !0);
		e.childCount && (this.placed = Gt(this.placed, this.frontier.length, e));
	}
};
function Wt(e, t, n) {
	return t == 0 ? e.cutByIndex(n, e.childCount) : e.replaceChild(0, e.firstChild.copy(Wt(e.firstChild.content, t - 1, n)));
}
function Gt(e, t, n) {
	return t == 0 ? e.append(n) : e.replaceChild(e.childCount - 1, e.lastChild.copy(Gt(e.lastChild.content, t - 1, n)));
}
function Kt(e, t) {
	for (let n = 0; n < t; n++) e = e.firstChild.content;
	return e;
}
function qt(e, t, n) {
	if (t <= 0) return e;
	let r = e.content;
	return t > 1 && (r = r.replaceChild(0, qt(r.firstChild, t - 1, r.childCount == 1 ? n - 1 : 0))), t > 0 && (r = e.type.contentMatch.fillBefore(r).append(r), n <= 0 && (r = r.append(e.type.contentMatch.matchFragment(r).fillBefore(a.empty, !0)))), e.copy(r);
}
function Jt(e, t, n, r, i) {
	let a = e.node(t), o = i ? e.indexAfter(t) : e.index(t);
	if (o == a.childCount && !n.compatibleContent(a.type)) return null;
	let s = r.fillBefore(a.content, !0, o);
	return s && !Yt(n, a.content, o) ? s : null;
}
function Yt(e, t, n) {
	for (let r = n; r < t.childCount; r++) if (!e.allowsMarks(t.child(r).marks)) return !0;
	return !1;
}
function Xt(e) {
	return e.spec.defining || e.spec.definingForContent;
}
function Zt(e, t, n, r) {
	if (!r.size) return e.deleteRange(t, n);
	let i = e.doc.resolve(t), a = e.doc.resolve(n);
	if (Ht(i, a, r)) return e.step(new gt(t, n, r));
	let o = tn(i, e.doc.resolve(n));
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
		let t = l[e].type, n = Xt(t);
		if (n && i.node(c).type != t) u = e;
		else if (n || !t.isTextblock) break;
	}
	for (let t = r.openStart; t >= 0; t--) {
		let s = (t + u + 1) % (r.openStart + 1), f = l[s];
		if (f) for (let t = 0; t < o.length; t++) {
			let l = o[(t + c) % o.length], u = !0;
			l < 0 && (u = !1, l = -l);
			let p = i.node(l - 1), m = i.index(l - 1);
			if (p.canReplaceWith(m, m, f.type, f.marks)) return e.replace(i.before(l), u ? a.after(l) : n, new d(Qt(r.content, 0, r.openStart, s), s, r.openEnd));
		}
	}
	let f = e.steps.length;
	for (let s = o.length - 1; s >= 0 && (e.replace(t, n, r), !(e.steps.length > f)); s--) {
		let e = o[s];
		e < 0 || (t = i.before(e), n = a.after(e));
	}
}
function Qt(e, t, n, r, i) {
	if (t < n) {
		let i = e.firstChild;
		e = e.replaceChild(0, i.copy(Qt(i.content, t + 1, n, r, i)));
	}
	if (t > r) {
		let t = i.contentMatchAt(0), n = t.fillBefore(e).append(e);
		e = n.append(t.matchFragment(n).fillBefore(a.empty, !0));
	}
	return e;
}
function $t(e, t, n, r) {
	if (!r.isInline && t == n && e.doc.resolve(t).parent.content.size) {
		let i = zt(e.doc, t, r.type);
		i != null && (t = n = i);
	}
	e.replaceRange(t, n, new d(a.from(r), 0, 0));
}
function en(e, t, n) {
	let r = e.doc.resolve(t), i = e.doc.resolve(n), a = tn(r, i);
	for (let t = 0; t < a.length; t++) {
		let n = a[t], o = t == a.length - 1;
		if (o && n == 0 || r.node(n).type.contentMatch.validEnd) return e.delete(r.start(n), i.end(n));
		if (n > 0 && (o || r.node(n - 1).canReplace(r.index(n - 1), i.indexAfter(n - 1)))) return e.delete(r.before(n), i.after(n));
	}
	for (let a = 1; a <= r.depth && a <= i.depth; a++) if (t - r.start(a) == r.depth - a && n > r.end(a) && i.end(a) - n != i.depth - a) return e.delete(r.before(a), n);
	e.delete(t, n);
}
function tn(e, t) {
	let n = [], r = Math.min(e.depth, t.depth);
	for (let i = r; i >= 0; i--) {
		let r = e.start(i);
		if (r < e.pos - (e.depth - i) || t.end(i) > t.pos + (t.depth - i) || e.node(i).type.spec.isolating || t.node(i).type.spec.isolating) break;
		(r == t.start(i) || i == e.depth && i == t.depth && e.parent.inlineContent && t.parent.inlineContent && i && t.start(i - 1) == r - 1) && n.push(i);
	}
	return n;
}
var nn = class e extends ut {
	constructor(e, t, n) {
		super(), this.pos = e, this.attr = t, this.value = n;
	}
	apply(e) {
		let t = e.nodeAt(this.pos);
		if (!t) return T.fail("No node at attribute step's position");
		let n = Object.create(null);
		for (let e in t.attrs) n[e] = t.attrs[e];
		n[this.attr] = this.value;
		let r = t.type.create(n, null, t.marks);
		return T.fromReplace(e, this.pos, this.pos + 1, new d(a.from(r), 0, +!t.isLeaf));
	}
	getMap() {
		return st.empty;
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
ut.jsonID("attr", nn);
var rn = class extends Error {};
rn = function e(t) {
	let n = Error.call(this, t);
	return n.__proto__ = e.prototype, n;
}, rn.prototype = Object.create(Error.prototype), rn.prototype.constructor = rn, rn.prototype.name = "TransformError";
var an = class {
	constructor(e) {
		this.doc = e, this.steps = [], this.docs = [], this.mapping = new ct();
	}
	get before() {
		return this.docs.length ? this.docs[0] : this.doc;
	}
	step(e) {
		let t = this.maybeStep(e);
		if (t.failed) throw new rn(t.failed);
		return this;
	}
	maybeStep(e) {
		let t = e.apply(this.doc);
		return t.failed || this.addStep(e, t.doc), t;
	}
	get docChanged() {
		return this.steps.length > 0;
	}
	addStep(e, t) {
		this.docs.push(this.doc), this.steps.push(e), this.mapping.appendMap(e.getMap()), this.doc = t;
	}
	replace(e, t = e, n = d.empty) {
		let r = Vt(this.doc, e, t, n);
		return r && this.step(r), this;
	}
	replaceWith(e, t, n) {
		return this.replace(e, t, new d(a.from(n), 0, 0));
	}
	delete(e, t) {
		return this.replace(e, t, d.empty);
	}
	insert(e, t) {
		return this.replaceWith(e, e, t);
	}
	replaceRange(e, t, n) {
		return Zt(this, e, t, n), this;
	}
	replaceRangeWith(e, t, n) {
		return $t(this, e, t, n), this;
	}
	deleteRange(e, t) {
		return en(this, e, t), this;
	}
	lift(e, t) {
		return wt(this, e, t), this;
	}
	join(e, t = 1) {
		return Rt(this, e, t), this;
	}
	wrap(e, t) {
		return kt(this, e, t), this;
	}
	setBlockType(e, t = e, n, r = null) {
		return At(this, e, t, n, r), this;
	}
	setNodeMarkup(e, t, n = null, r) {
		return Mt(this, e, t, n, r), this;
	}
	setNodeAttribute(e, t, n) {
		return this.step(new nn(e, t, n)), this;
	}
	addNodeMark(e, t) {
		return this.step(new mt(e, t)), this;
	}
	removeNodeMark(e, t) {
		if (!(t instanceof l)) {
			let n = this.doc.nodeAt(e);
			if (!n) throw RangeError("No node at position " + e);
			if (t = t.isInSet(n.marks), !t) return this;
		}
		return this.step(new ht(e, t)), this;
	}
	split(e, t = 1, n) {
		return Pt(this, e, t, n), this;
	}
	addMark(e, t, n) {
		return yt(this, e, t, n), this;
	}
	removeMark(e, t, n) {
		return bt(this, e, t, n), this;
	}
	clearIncompatible(e, t, n) {
		return xt(this, e, t, n), this;
	}
}, on = Object.create(null), E = class {
	constructor(e, t, n) {
		this.$anchor = e, this.$head = t, this.ranges = n || [new sn(e.min(t), e.max(t))];
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
	replace(e, t = d.empty) {
		let n = t.content.lastChild, r = null;
		for (let e = 0; e < t.openEnd; e++) r = n, n = n.lastChild;
		let i = e.steps.length, a = this.ranges;
		for (let o = 0; o < a.length; o++) {
			let { $from: s, $to: c } = a[o], l = e.mapping.slice(i);
			e.replaceRange(l.map(s.pos), l.map(c.pos), o ? d.empty : t), o == 0 && hn(e, i, (n ? n.isInline : r && r.isTextblock) ? -1 : 1);
		}
	}
	replaceWith(e, t) {
		let n = e.steps.length, r = this.ranges;
		for (let i = 0; i < r.length; i++) {
			let { $from: a, $to: o } = r[i], s = e.mapping.slice(n), c = s.map(a.pos), l = s.map(o.pos);
			i ? e.deleteRange(c, l) : (e.replaceRangeWith(c, l, t), hn(e, n, t.isInline ? -1 : 1));
		}
	}
	static findFrom(e, t, n = !1) {
		let r = e.parent.inlineContent ? new D(e) : mn(e.node(0), e.parent, e.pos, e.index(), t, n);
		if (r) return r;
		for (let r = e.depth - 1; r >= 0; r--) {
			let i = t < 0 ? mn(e.node(0), e.node(r), e.before(r + 1), e.index(r), t, n) : mn(e.node(0), e.node(r), e.after(r + 1), e.index(r) + 1, t, n);
			if (i) return i;
		}
		return null;
	}
	static near(e, t = 1) {
		return this.findFrom(e, t) || this.findFrom(e, -t) || new fn(e.node(0));
	}
	static atStart(e) {
		return mn(e, e, 0, 0, 1) || new fn(e);
	}
	static atEnd(e) {
		return mn(e, e, e.content.size, e.childCount, -1) || new fn(e);
	}
	static fromJSON(e, t) {
		if (!t || !t.type) throw RangeError("Invalid input for Selection.fromJSON");
		let n = on[t.type];
		if (!n) throw RangeError(`No selection type ${t.type} defined`);
		return n.fromJSON(e, t);
	}
	static jsonID(e, t) {
		if (e in on) throw RangeError("Duplicate use of selection JSON ID " + e);
		return on[e] = t, t.prototype.jsonID = e, t;
	}
	getBookmark() {
		return D.between(this.$anchor, this.$head).getBookmark();
	}
};
E.prototype.visible = !0;
var sn = class {
	constructor(e, t) {
		this.$from = e, this.$to = t;
	}
}, cn = !1;
function ln(e) {
	!cn && !e.parent.inlineContent && (cn = !0, console.warn("TextSelection endpoint not pointing into a node with inline content (" + e.parent.type.name + ")"));
}
var D = class e extends E {
	constructor(e, t = e) {
		ln(e), ln(t), super(e, t);
	}
	get $cursor() {
		return this.$anchor.pos == this.$head.pos ? this.$head : null;
	}
	map(t, n) {
		let r = t.resolve(n.map(this.head));
		if (!r.parent.inlineContent) return E.near(r);
		let i = t.resolve(n.map(this.anchor));
		return new e(i.parent.inlineContent ? i : r, r);
	}
	replace(e, t = d.empty) {
		if (super.replace(e, t), t == d.empty) {
			let t = this.$from.marksAcross(this.$to);
			t && e.ensureMarks(t);
		}
	}
	eq(t) {
		return t instanceof e && t.anchor == this.anchor && t.head == this.head;
	}
	getBookmark() {
		return new un(this.anchor, this.head);
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
			let e = E.findFrom(n, r, !0) || E.findFrom(n, -r, !0);
			if (e) n = e.$head;
			else return E.near(n, r);
		}
		return t.parent.inlineContent || (i == 0 ? t = n : (t = (E.findFrom(t, -r, !0) || E.findFrom(t, r, !0)).$anchor, t.pos < n.pos != i < 0 && (t = n))), new e(t, n);
	}
};
E.jsonID("text", D);
var un = class e {
	constructor(e, t) {
		this.anchor = e, this.head = t;
	}
	map(t) {
		return new e(t.map(this.anchor), t.map(this.head));
	}
	resolve(e) {
		return D.between(e.resolve(this.anchor), e.resolve(this.head));
	}
}, O = class e extends E {
	constructor(e) {
		let t = e.nodeAfter, n = e.node(0).resolve(e.pos + t.nodeSize);
		super(e, n), this.node = t;
	}
	map(t, n) {
		let { deleted: r, pos: i } = n.mapResult(this.anchor), a = t.resolve(i);
		return r ? E.near(a) : new e(a);
	}
	content() {
		return new d(a.from(this.node), 0, 0);
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
		return new dn(this.anchor);
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
O.prototype.visible = !1, E.jsonID("node", O);
var dn = class e {
	constructor(e) {
		this.anchor = e;
	}
	map(t) {
		let { deleted: n, pos: r } = t.mapResult(this.anchor);
		return n ? new un(r, r) : new e(r);
	}
	resolve(e) {
		let t = e.resolve(this.anchor), n = t.nodeAfter;
		return n && O.isSelectable(n) ? new O(t) : E.near(t);
	}
}, fn = class e extends E {
	constructor(e) {
		super(e.resolve(0), e.resolve(e.content.size));
	}
	replace(e, t = d.empty) {
		if (t == d.empty) {
			e.delete(0, e.doc.content.size);
			let t = E.atStart(e.doc);
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
		return pn;
	}
};
E.jsonID("all", fn);
var pn = {
	map() {
		return this;
	},
	resolve(e) {
		return new fn(e);
	}
};
function mn(e, t, n, r, i, a = !1) {
	if (t.inlineContent) return D.create(e, n);
	for (let o = r - (i > 0 ? 0 : 1); i > 0 ? o < t.childCount : o >= 0; o += i) {
		let r = t.child(o);
		if (!r.isAtom) {
			let t = mn(e, r, n + i, i < 0 ? r.childCount : 0, i, a);
			if (t) return t;
		} else if (!a && O.isSelectable(r)) return O.create(e, n - (i < 0 ? r.nodeSize : 0));
		n += r.nodeSize * i;
	}
	return null;
}
function hn(e, t, n) {
	let r = e.steps.length - 1;
	if (r < t) return;
	let i = e.steps[r];
	if (!(i instanceof gt || i instanceof _t)) return;
	let a = e.mapping.maps[r], o;
	a.forEach((e, t, n, r) => {
		o ?? (o = r);
	}), e.setSelection(E.near(e.doc.resolve(o), n));
}
var gn = 1, _n = 2, vn = 4, yn = class extends an {
	constructor(e) {
		super(e.doc), this.curSelectionFor = 0, this.updated = 0, this.meta = Object.create(null), this.time = Date.now(), this.curSelection = e.selection, this.storedMarks = e.storedMarks;
	}
	get selection() {
		return this.curSelectionFor < this.steps.length && (this.curSelection = this.curSelection.map(this.doc, this.mapping.slice(this.curSelectionFor)), this.curSelectionFor = this.steps.length), this.curSelection;
	}
	setSelection(e) {
		if (e.$from.doc != this.doc) throw RangeError("Selection passed to setSelection must point at the current document");
		return this.curSelection = e, this.curSelectionFor = this.steps.length, this.updated = (this.updated | gn) & -3, this.storedMarks = null, this;
	}
	get selectionSet() {
		return (this.updated & gn) > 0;
	}
	setStoredMarks(e) {
		return this.storedMarks = e, this.updated |= _n, this;
	}
	ensureMarks(e) {
		return l.sameSet(this.storedMarks || this.selection.$from.marks(), e) || this.setStoredMarks(e), this;
	}
	addStoredMark(e) {
		return this.ensureMarks(e.addToSet(this.storedMarks || this.selection.$head.marks()));
	}
	removeStoredMark(e) {
		return this.ensureMarks(e.removeFromSet(this.storedMarks || this.selection.$head.marks()));
	}
	get storedMarksSet() {
		return (this.updated & _n) > 0;
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
		return t && (e = e.mark(this.storedMarks || (n.empty ? n.$from.marks() : n.$from.marksAcross(n.$to) || l.none))), n.replaceWith(this, e), this;
	}
	deleteSelection() {
		return this.selection.replace(this), this;
	}
	insertText(e, t, n) {
		let r = this.doc.type.schema;
		if (t == null) return e ? this.replaceSelectionWith(r.text(e), !0) : this.deleteSelection();
		{
			if (n ?? (n = t), n = n ?? t, !e) return this.deleteRange(t, n);
			let i = this.storedMarks;
			if (!i) {
				let e = this.doc.resolve(t);
				i = n == t ? e.marks() : e.marksAcross(this.doc.resolve(n));
			}
			return this.replaceRangeWith(t, n, r.text(e, i)), this.selection.empty || this.setSelection(E.near(this.selection.$to)), this;
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
		return this.updated |= vn, this;
	}
	get scrolledIntoView() {
		return (this.updated & vn) > 0;
	}
};
function bn(e, t) {
	return !t || !e ? e : e.bind(t);
}
var xn = class {
	constructor(e, t, n) {
		this.name = e, this.init = bn(t.init, n), this.apply = bn(t.apply, n);
	}
}, Sn = [
	new xn("doc", {
		init(e) {
			return e.doc || e.schema.topNodeType.createAndFill();
		},
		apply(e) {
			return e.doc;
		}
	}),
	new xn("selection", {
		init(e, t) {
			return e.selection || E.atStart(t.doc);
		},
		apply(e) {
			return e.selection;
		}
	}),
	new xn("storedMarks", {
		init(e) {
			return e.storedMarks || null;
		},
		apply(e, t, n, r) {
			return r.selection.$cursor ? e.storedMarks : null;
		}
	}),
	new xn("scrollToSelection", {
		init() {
			return 0;
		},
		apply(e, t) {
			return e.scrolledIntoView ? t + 1 : t;
		}
	})
], Cn = class {
	constructor(e, t) {
		this.schema = e, this.plugins = [], this.pluginsByKey = Object.create(null), this.fields = Sn.slice(), t && t.forEach((e) => {
			if (this.pluginsByKey[e.key]) throw RangeError("Adding different instances of a keyed plugin (" + e.key + ")");
			this.plugins.push(e), this.pluginsByKey[e.key] = e, e.spec.state && this.fields.push(new xn(e.key, e.spec.state, e));
		});
	}
}, wn = class e {
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
		return new yn(this);
	}
	static create(t) {
		let n = new Cn(t.doc ? t.doc.type.schema : t.schema, t.plugins), r = new e(n);
		for (let e = 0; e < n.fields.length; e++) r[n.fields[e].name] = n.fields[e].init(t, r);
		return r;
	}
	reconfigure(t) {
		let n = new Cn(this.schema, t.plugins), r = n.fields, i = new e(n);
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
		let i = new Cn(t.schema, t.plugins), a = new e(i);
		return i.fields.forEach((e) => {
			if (e.name == "doc") a.doc = se.fromJSON(t.schema, n.doc);
			else if (e.name == "selection") a.selection = E.fromJSON(a.doc, n.selection);
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
function Tn(e, t, n) {
	for (let r in e) {
		let i = e[r];
		i instanceof Function ? i = i.bind(t) : r == "handleDOMEvents" && (i = Tn(i, t, {})), n[r] = i;
	}
	return n;
}
var k = class {
	constructor(e) {
		this.spec = e, this.props = {}, e.props && Tn(e.props, this, this.props), this.key = e.key ? e.key.key : Dn("plugin");
	}
	getState(e) {
		return e[this.key];
	}
}, En = Object.create(null);
function Dn(e) {
	return e in En ? e + "$" + ++En[e] : (En[e] = 0, e + "$");
}
var A = class {
	constructor(e = "key") {
		this.key = Dn(e);
	}
	get(e) {
		return e.config.pluginsByKey[this.key];
	}
	getState(e) {
		return e[this.key];
	}
}, On = (e, t) => !e.selection.empty && (t && t(e.tr.deleteSelection().scrollIntoView()), !0);
function kn(e, t) {
	let { $cursor: n } = e.selection;
	return !n || (t ? !t.endOfTextblock("backward", e) : n.parentOffset > 0) ? null : n;
}
var An = (e, t, n) => {
	let r = kn(e, n);
	if (!r) return !1;
	let i = In(r);
	if (!i) {
		let n = r.blockRange(), i = n && Ct(n);
		return i != null && (t && t(e.tr.lift(n, i).scrollIntoView()), !0);
	}
	let a = i.nodeBefore;
	if (er(e, i, t, -1)) return !0;
	if (r.parent.content.size == 0 && (Pn(a, "end") || O.isSelectable(a))) for (let n = r.depth;; n--) {
		let o = Vt(e.doc, r.before(n), r.after(n), d.empty);
		if (o && o.slice.size < o.to - o.from) {
			if (t) {
				let n = e.tr.step(o);
				n.setSelection(Pn(a, "end") ? E.findFrom(n.doc.resolve(n.mapping.map(i.pos, -1)), -1) : O.create(n.doc, i.pos - a.nodeSize)), t(n.scrollIntoView());
			}
			return !0;
		}
		if (n == 1 || r.node(n - 1).childCount > 1) break;
	}
	return a.isAtom && i.depth == r.depth - 1 ? (t && t(e.tr.delete(i.pos - a.nodeSize, i.pos).scrollIntoView()), !0) : !1;
}, jn = (e, t, n) => {
	let r = kn(e, n);
	if (!r) return !1;
	let i = In(r);
	return i ? Nn(e, i, t) : !1;
}, Mn = (e, t, n) => {
	let r = Ln(e, n);
	if (!r) return !1;
	let i = Bn(r);
	return i ? Nn(e, i, t) : !1;
};
function Nn(e, t, n) {
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
	let s = Vt(e.doc, i, o, d.empty);
	if (!s || s.from != i || s instanceof gt && s.slice.size >= o - i) return !1;
	if (n) {
		let t = e.tr.step(s);
		t.setSelection(D.create(t.doc, i)), n(t.scrollIntoView());
	}
	return !0;
}
function Pn(e, t, n = !1) {
	for (let r = e; r; r = t == "start" ? r.firstChild : r.lastChild) {
		if (r.isTextblock) return !0;
		if (n && r.childCount != 1) return !1;
	}
	return !1;
}
var Fn = (e, t, n) => {
	let { $head: r, empty: i } = e.selection, a = r;
	if (!i) return !1;
	if (r.parent.isTextblock) {
		if (n ? !n.endOfTextblock("backward", e) : r.parentOffset > 0) return !1;
		a = In(r);
	}
	let o = a && a.nodeBefore;
	return !o || !O.isSelectable(o) ? !1 : (t && t(e.tr.setSelection(O.create(e.doc, a.pos - o.nodeSize)).scrollIntoView()), !0);
};
function In(e) {
	if (!e.parent.type.spec.isolating) for (let t = e.depth - 1; t >= 0; t--) {
		if (e.index(t) > 0) return e.doc.resolve(e.before(t + 1));
		if (e.node(t).type.spec.isolating) break;
	}
	return null;
}
function Ln(e, t) {
	let { $cursor: n } = e.selection;
	return !n || (t ? !t.endOfTextblock("forward", e) : n.parentOffset < n.parent.content.size) ? null : n;
}
var Rn = (e, t, n) => {
	let r = Ln(e, n);
	if (!r) return !1;
	let i = Bn(r);
	if (!i) return !1;
	let a = i.nodeAfter;
	if (er(e, i, t, 1)) return !0;
	if (r.parent.content.size == 0 && (Pn(a, "start") || O.isSelectable(a))) {
		let n = Vt(e.doc, r.before(), r.after(), d.empty);
		if (n && n.slice.size < n.to - n.from) {
			if (t) {
				let r = e.tr.step(n);
				r.setSelection(Pn(a, "start") ? E.findFrom(r.doc.resolve(r.mapping.map(i.pos)), 1) : O.create(r.doc, r.mapping.map(i.pos))), t(r.scrollIntoView());
			}
			return !0;
		}
	}
	return a.isAtom && i.depth == r.depth - 1 ? (t && t(e.tr.delete(i.pos, i.pos + a.nodeSize).scrollIntoView()), !0) : !1;
}, zn = (e, t, n) => {
	let { $head: r, empty: i } = e.selection, a = r;
	if (!i) return !1;
	if (r.parent.isTextblock) {
		if (n ? !n.endOfTextblock("forward", e) : r.parentOffset < r.parent.content.size) return !1;
		a = Bn(r);
	}
	let o = a && a.nodeAfter;
	return !o || !O.isSelectable(o) ? !1 : (t && t(e.tr.setSelection(O.create(e.doc, a.pos)).scrollIntoView()), !0);
};
function Bn(e) {
	if (!e.parent.type.spec.isolating) for (let t = e.depth - 1; t >= 0; t--) {
		let n = e.node(t);
		if (e.index(t) + 1 < n.childCount) return e.doc.resolve(e.after(t + 1));
		if (n.type.spec.isolating) break;
	}
	return null;
}
var Vn = (e, t) => {
	let n = e.selection, r = n instanceof O, i;
	if (r) {
		if (n.node.isTextblock || !Ft(e.doc, n.from)) return !1;
		i = n.from;
	} else if (i = Lt(e.doc, n.from, -1), i == null) return !1;
	if (t) {
		let n = e.tr.join(i);
		r && n.setSelection(O.create(n.doc, i - e.doc.resolve(i).nodeBefore.nodeSize)), t(n.scrollIntoView());
	}
	return !0;
}, Hn = (e, t) => {
	let n = e.selection, r;
	if (n instanceof O) {
		if (n.node.isTextblock || !Ft(e.doc, n.to)) return !1;
		r = n.to;
	} else if (r = Lt(e.doc, n.to, 1), r == null) return !1;
	return t && t(e.tr.join(r).scrollIntoView()), !0;
}, Un = (e, t) => {
	let { $from: n, $to: r } = e.selection, i = n.blockRange(r), a = i && Ct(i);
	return a != null && (t && t(e.tr.lift(i, a).scrollIntoView()), !0);
}, Wn = (e, t) => {
	let { $head: n, $anchor: r } = e.selection;
	return !n.parent.type.spec.code || !n.sameParent(r) ? !1 : (t && t(e.tr.insertText("\n").scrollIntoView()), !0);
};
function Gn(e) {
	for (let t = 0; t < e.edgeCount; t++) {
		let { type: n } = e.edge(t);
		if (n.isTextblock && !n.hasRequiredAttrs()) return n;
	}
	return null;
}
var Kn = (e, t) => {
	let { $head: n, $anchor: r } = e.selection;
	if (!n.parent.type.spec.code || !n.sameParent(r)) return !1;
	let i = n.node(-1), a = n.indexAfter(-1), o = Gn(i.contentMatchAt(a));
	if (!o || !i.canReplaceWith(a, a, o)) return !1;
	if (t) {
		let r = n.after(), i = e.tr.replaceWith(r, r, o.createAndFill());
		i.setSelection(E.near(i.doc.resolve(r), 1)), t(i.scrollIntoView());
	}
	return !0;
}, qn = (e, t) => {
	let n = e.selection, { $from: r, $to: i } = n;
	if (n instanceof fn || r.parent.inlineContent || i.parent.inlineContent) return !1;
	let a = Gn(i.parent.contentMatchAt(i.indexAfter()));
	if (!a || !a.isTextblock) return !1;
	if (t) {
		let n = (!r.parentOffset && i.index() < i.parent.childCount ? r : i).pos, o = e.tr.insert(n, a.createAndFill());
		o.setSelection(D.create(o.doc, n + 1)), t(o.scrollIntoView());
	}
	return !0;
}, Jn = (e, t) => {
	let { $cursor: n } = e.selection;
	if (!n || n.parent.content.size) return !1;
	if (n.depth > 1 && n.after() != n.end(-1)) {
		let r = n.before();
		if (Nt(e.doc, r)) return t && t(e.tr.split(r).scrollIntoView()), !0;
	}
	let r = n.blockRange(), i = r && Ct(r);
	return i != null && (t && t(e.tr.lift(r, i).scrollIntoView()), !0);
};
function Yn(e) {
	return (t, n) => {
		let { $from: r, $to: i } = t.selection;
		if (t.selection instanceof O && t.selection.node.isBlock) return !r.parentOffset || !Nt(t.doc, r.pos) ? !1 : (n && n(t.tr.split(r.pos).scrollIntoView()), !0);
		if (!r.depth) return !1;
		let a = [], o, s, c = !1, l = !1;
		for (let t = r.depth;; t--) if (r.node(t).isBlock) {
			c = r.end(t) == r.pos + (r.depth - t), l = r.start(t) == r.pos - (r.depth - t), s = Gn(r.node(t - 1).contentMatchAt(r.indexAfter(t - 1)));
			let n = e && e(i.parent, c, r);
			a.unshift(n || (c && s ? { type: s } : null)), o = t;
			break;
		} else {
			if (t == 1) return !1;
			a.unshift(null);
		}
		let u = t.tr;
		(t.selection instanceof D || t.selection instanceof fn) && u.deleteSelection();
		let d = u.mapping.map(r.pos), f = Nt(u.doc, d, a.length, a);
		if (f || (a[0] = s ? { type: s } : null, f = Nt(u.doc, d, a.length, a)), !f) return !1;
		if (u.split(d, a.length, a), !c && l && r.node(o).type != s) {
			let e = u.mapping.map(r.before(o)), t = u.doc.resolve(e);
			s && r.node(o - 1).canReplaceWith(t.index(), t.index() + 1, s) && u.setNodeMarkup(u.mapping.map(r.before(o)), s);
		}
		return n && n(u.scrollIntoView()), !0;
	};
}
var Xn = Yn(), Zn = (e, t) => {
	let { $from: n, to: r } = e.selection, i, a = n.sharedDepth(r);
	return a != 0 && (i = n.before(a), t && t(e.tr.setSelection(O.create(e.doc, i))), !0);
}, Qn = (e, t) => (t && t(e.tr.setSelection(new fn(e.doc))), !0);
function $n(e, t, n) {
	let r = t.nodeBefore, i = t.nodeAfter, a = t.index();
	return !r || !i || !r.type.compatibleContent(i.type) ? !1 : !r.content.size && t.parent.canReplace(a - 1, a) ? (n && n(e.tr.delete(t.pos - r.nodeSize, t.pos).scrollIntoView()), !0) : !t.parent.canReplace(a, a + 1) || !(i.isTextblock || Ft(e.doc, t.pos)) ? !1 : (n && n(e.tr.join(t.pos).scrollIntoView()), !0);
}
function er(e, t, n, r) {
	let i = t.nodeBefore, o = t.nodeAfter, s, c, l = i.type.spec.isolating || o.type.spec.isolating;
	if (!l && $n(e, t, n)) return !0;
	let u = !l && t.parent.canReplace(t.index(), t.index() + 1);
	if (u && (s = (c = i.contentMatchAt(i.childCount)).findWrapping(o.type)) && c.matchType(s[0] || o.type).validEnd) {
		if (n) {
			let r = t.pos + o.nodeSize, c = a.empty;
			for (let e = s.length - 1; e >= 0; e--) c = a.from(s[e].create(null, c));
			c = a.from(i.copy(c));
			let l = e.tr.step(new _t(t.pos - 1, r, t.pos, r, new d(c, 1, 0), s.length, !0)), u = l.doc.resolve(r + 2 * s.length);
			u.nodeAfter && u.nodeAfter.type == i.type && Ft(l.doc, u.pos) && l.join(u.pos), n(l.scrollIntoView());
		}
		return !0;
	}
	let f = o.type.spec.isolating || r > 0 && l ? null : E.findFrom(t, 1), p = f && f.$from.blockRange(f.$to), m = p && Ct(p);
	if (m != null && m >= t.depth) return n && n(e.tr.lift(p, m).scrollIntoView()), !0;
	if (u && Pn(o, "start", !0) && Pn(i, "end")) {
		let r = i, s = [];
		for (; s.push(r), !r.isTextblock;) r = r.lastChild;
		let c = o, l = 1;
		for (; !c.isTextblock; c = c.firstChild) l++;
		if (r.canReplace(r.childCount, r.childCount, c.content)) {
			if (n) {
				let r = a.empty;
				for (let e = s.length - 1; e >= 0; e--) r = a.from(s[e].copy(r));
				n(e.tr.step(new _t(t.pos - s.length, t.pos + o.nodeSize, t.pos + l, t.pos + o.nodeSize - l, new d(r, s.length, 0), 0, !0)).scrollIntoView());
			}
			return !0;
		}
	}
	return !1;
}
function tr(e) {
	return function(t, n) {
		let r = t.selection, i = e < 0 ? r.$from : r.$to, a = i.depth;
		for (; i.node(a).isInline;) {
			if (!a) return !1;
			a--;
		}
		return i.node(a).isTextblock ? (n && n(t.tr.setSelection(D.create(t.doc, e < 0 ? i.start(a) : i.end(a)))), !0) : !1;
	};
}
var nr = tr(-1), rr = tr(1);
function ir(e, t = null) {
	return function(n, r) {
		let { $from: i, $to: a } = n.selection, o = i.blockRange(a), s = o && Tt(o, e, t);
		return s ? (r && r(n.tr.wrap(o, s).scrollIntoView()), !0) : !1;
	};
}
function ar(e, t = null) {
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
function or(...e) {
	return function(t, n, r) {
		for (let i = 0; i < e.length; i++) if (e[i](t, n, r)) return !0;
		return !1;
	};
}
var sr = or(On, An, Fn), cr = or(On, Rn, zn), lr = {
	Enter: or(Wn, qn, Jn, Xn),
	"Mod-Enter": Kn,
	Backspace: sr,
	"Mod-Backspace": sr,
	"Shift-Backspace": sr,
	Delete: cr,
	"Mod-Delete": cr,
	"Mod-a": Qn
}, ur = {
	"Ctrl-h": lr.Backspace,
	"Alt-Backspace": lr["Mod-Backspace"],
	"Ctrl-d": lr.Delete,
	"Ctrl-Alt-Backspace": lr["Mod-Delete"],
	"Alt-Delete": lr["Mod-Delete"],
	"Alt-d": lr["Mod-Delete"],
	"Ctrl-a": nr,
	"Ctrl-e": rr
};
for (let e in lr) ur[e] = lr[e];
typeof navigator < "u" ? /Mac|iP(hone|[oa]d)/.test(navigator.platform) : typeof os < "u" && os.platform && os.platform();
//#endregion
//#region node_modules/@tiptap/pm/node_modules/prosemirror-schema-list/dist/index.js
function dr(e, t = null) {
	return function(n, r) {
		let { $from: i, $to: a } = n.selection, o = i.blockRange(a);
		if (!o) return !1;
		let s = r ? n.tr : null;
		return fr(s, o, e, t) ? (r && r(s.scrollIntoView()), !0) : !1;
	};
}
function fr(e, t, n, r = null) {
	let i = !1, a = t, o = t.$from.doc;
	if (t.depth >= 2 && t.$from.node(t.depth - 1).type.compatibleContent(n) && t.startIndex == 0) {
		if (t.$from.index(t.depth - 1) == 0) return !1;
		let e = o.resolve(t.start - 2);
		a = new ae(e, e, t.depth), t.endIndex < t.parent.childCount && (t = new ae(t.$from, o.resolve(t.$to.end(t.depth)), t.depth)), i = !0;
	}
	let s = Tt(a, n, r, t);
	return s ? (e && pr(e, t, s, i, n), !0) : !1;
}
function pr(e, t, n, r, i) {
	let o = a.empty;
	for (let e = n.length - 1; e >= 0; e--) o = a.from(n[e].type.create(n[e].attrs, o));
	e.step(new _t(t.start - (r ? 2 : 0), t.end, t.start, t.end, new d(o, 0, 0), n.length, !0));
	let s = 0;
	for (let e = 0; e < n.length; e++) n[e].type == i && (s = e + 1);
	let c = n.length - s, l = t.start + n.length - (r ? 2 : 0), u = t.parent;
	for (let n = t.startIndex, r = t.endIndex, i = !0; n < r; n++, i = !1) !i && Nt(e.doc, l, c) && (e.split(l, c), l += 2 * c), l += u.child(n).nodeSize;
	return e;
}
function mr(e) {
	return function(t, n) {
		let { $from: r, $to: i } = t.selection, a = r.blockRange(i, (t) => t.childCount > 0 && t.firstChild.type == e);
		return a ? n ? r.node(a.depth - 1).type == e ? hr(t, n, e, a) : gr(t, n, a) : !0 : !1;
	};
}
function hr(e, t, n, r) {
	let i = e.tr, o = r.end, s = r.$to.end(r.depth);
	o < s && (i.step(new _t(o - 1, s, o, s, new d(a.from(n.create(null, r.parent.copy())), 1, 0), 1, !0)), r = new ae(i.doc.resolve(r.$from.pos), i.doc.resolve(s), r.depth));
	let c = Ct(r);
	if (c == null) return !1;
	i.lift(r, c);
	let l = i.doc.resolve(i.mapping.map(o, -1) - 1);
	return Ft(i.doc, l.pos) && l.nodeBefore.type == l.nodeAfter.type && i.join(l.pos), t(i.scrollIntoView()), !0;
}
function gr(e, t, n) {
	let r = e.tr, i = n.parent;
	for (let e = n.end, t = n.endIndex - 1, a = n.startIndex; t > a; t--) e -= i.child(t).nodeSize, r.delete(e - 1, e + 1);
	let o = r.doc.resolve(n.start), s = o.nodeAfter;
	if (r.mapping.map(n.end) != n.start + o.nodeAfter.nodeSize) return !1;
	let c = n.startIndex == 0, l = n.endIndex == i.childCount, u = o.node(-1), f = o.index(-1);
	if (!u.canReplace(f + +!c, f + 1, s.content.append(l ? a.empty : a.from(i)))) return !1;
	let p = o.pos, m = p + s.nodeSize;
	return r.step(new _t(p - +!!c, m + +!!l, p + 1, m - 1, new d((c ? a.empty : a.from(i.copy(a.empty))).append(l ? a.empty : a.from(i.copy(a.empty))), +!c, +!l), +!c)), t(r.scrollIntoView()), !0;
}
function _r(e) {
	return function(t, n) {
		let { $from: r, $to: i } = t.selection, o = r.blockRange(i, (t) => t.childCount > 0 && t.firstChild.type == e);
		if (!o) return !1;
		let s = o.startIndex;
		if (s == 0) return !1;
		let c = o.parent, l = c.child(s - 1);
		if (l.type != e) return !1;
		if (n) {
			let r = l.lastChild && l.lastChild.type == c.type, i = a.from(r ? e.create() : null), s = new d(a.from(e.create(null, a.from(c.type.create(null, i)))), r ? 3 : 1, 0), u = o.start, f = o.end;
			n(t.tr.step(new _t(u - (r ? 3 : 1), f, u, f, s, 1, !0)).scrollIntoView());
		}
		return !0;
	};
}
//#endregion
//#region node_modules/prosemirror-view/dist/index.js
var j = function(e) {
	for (var t = 0;; t++) if (e = e.previousSibling, !e) return t;
}, vr = function(e) {
	let t = e.assignedSlot || e.parentNode;
	return t && t.nodeType == 11 ? t.host : t;
}, yr = null, br = function(e, t, n) {
	let r = yr || (yr = document.createRange());
	return r.setEnd(e, n ?? e.nodeValue.length), r.setStart(e, t || 0), r;
}, xr = function() {
	yr = null;
}, Sr = function(e, t, n, r) {
	return n && (wr(e, t, n, r, -1) || wr(e, t, n, r, 1));
}, Cr = /^(img|br|input|textarea|hr)$/i;
function wr(e, t, n, r, i) {
	for (;;) {
		if (e == n && t == r) return !0;
		if (t == (i < 0 ? 0 : Tr(e))) {
			let n = e.parentNode;
			if (!n || n.nodeType != 1 || kr(e) || Cr.test(e.nodeName) || e.contentEditable == "false") return !1;
			t = j(e) + (i < 0 ? 0 : 1), e = n;
		} else if (e.nodeType == 1) {
			let n = e.childNodes[t + (i < 0 ? -1 : 0)];
			if (n.nodeType == 1 && n.contentEditable == "false") {
				if (n.pmViewDesc?.ignoreForSelection) t += i;
				else return !1;
			} else e = n, t = i < 0 ? Tr(e) : 0;
		} else return !1;
	}
}
function Tr(e) {
	return e.nodeType == 3 ? e.nodeValue.length : e.childNodes.length;
}
function Er(e, t) {
	for (;;) {
		if (e.nodeType == 3 && t) return e;
		if (e.nodeType == 1 && t > 0) {
			if (e.contentEditable == "false") return null;
			e = e.childNodes[t - 1], t = Tr(e);
		} else if (e.parentNode && !kr(e)) t = j(e), e = e.parentNode;
		else return null;
	}
}
function Dr(e, t) {
	for (;;) {
		if (e.nodeType == 3 && t < e.nodeValue.length) return e;
		if (e.nodeType == 1 && t < e.childNodes.length) {
			if (e.contentEditable == "false") return null;
			e = e.childNodes[t], t = 0;
		} else if (e.parentNode && !kr(e)) t = j(e) + 1, e = e.parentNode;
		else return null;
	}
}
function Or(e, t, n) {
	for (let r = t == 0, i = t == Tr(e); r || i;) {
		if (e == n) return !0;
		let t = j(e);
		if (e = e.parentNode, !e) return !1;
		r = r && t == 0, i = i && t == Tr(e);
	}
}
function kr(e) {
	let t;
	for (let n = e; n && !(t = n.pmViewDesc); n = n.parentNode);
	return t && t.node && t.node.isBlock && (t.dom == e || t.contentDOM == e);
}
var Ar = function(e) {
	return e.focusNode && Sr(e.focusNode, e.focusOffset, e.anchorNode, e.anchorOffset);
};
function jr(e, t) {
	let n = document.createEvent("Event");
	return n.initEvent("keydown", !0, !0), n.keyCode = e, n.key = n.code = t, n;
}
function Mr(e) {
	let t = e.activeElement;
	for (; t && t.shadowRoot;) t = t.shadowRoot.activeElement;
	return t;
}
function Nr(e, t, n) {
	if (e.caretPositionFromPoint) try {
		let r = e.caretPositionFromPoint(t, n);
		if (r) return {
			node: r.offsetNode,
			offset: Math.min(Tr(r.offsetNode), r.offset)
		};
	} catch {}
	if (e.caretRangeFromPoint) {
		let r = e.caretRangeFromPoint(t, n);
		if (r) return {
			node: r.startContainer,
			offset: Math.min(Tr(r.startContainer), r.startOffset)
		};
	}
}
var Pr = typeof navigator < "u" ? navigator : null, Fr = typeof document < "u" ? document : null, Ir = Pr && Pr.userAgent || "", Lr = /Edge\/(\d+)/.exec(Ir), Rr = /MSIE \d/.exec(Ir), zr = /Trident\/(?:[7-9]|\d{2,})\..*rv:(\d+)/.exec(Ir), Br = !!(Rr || zr || Lr), Vr = Rr ? document.documentMode : zr ? +zr[1] : Lr ? +Lr[1] : 0, Hr = !Br && /gecko\/(\d+)/i.test(Ir);
Hr && +(/Firefox\/(\d+)/.exec(Ir) || [0, 0])[1];
var Ur = !Br && /Chrome\/(\d+)/.exec(Ir), M = !!Ur, Wr = Ur ? +Ur[1] : 0, N = !Br && !!Pr && /Apple Computer/.test(Pr.vendor), Gr = N && (/Mobile\/\w+/.test(Ir) || !!Pr && Pr.maxTouchPoints > 2), Kr = Gr || (Pr ? /Mac/.test(Pr.platform) : !1), qr = Pr ? /Win/.test(Pr.platform) : !1, Jr = /Android \d/.test(Ir), Yr = !!Fr && "webkitFontSmoothing" in Fr.documentElement.style, Xr = Yr ? +(/\bAppleWebKit\/(\d+)/.exec(navigator.userAgent) || [0, 0])[1] : 0;
function Zr(e) {
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
function Qr(e, t) {
	return typeof e == "number" ? e : e[t];
}
function $r(e) {
	let t = e.getBoundingClientRect(), n = t.width / e.offsetWidth || 1, r = t.height / e.offsetHeight || 1;
	return {
		left: t.left,
		right: t.left + e.clientWidth * n,
		top: t.top,
		bottom: t.top + e.clientHeight * r
	};
}
function ei(e, t, n) {
	let r = e.someProp("scrollThreshold") || 0, i = e.someProp("scrollMargin") || 5, a = e.dom.ownerDocument;
	for (let o = n || e.dom; o;) {
		if (o.nodeType != 1) {
			o = vr(o);
			continue;
		}
		let e = o, n = e == a.body, s = n ? Zr(a) : $r(e), c = 0, l = 0;
		if (t.top < s.top + Qr(r, "top") ? l = -(s.top - t.top + Qr(i, "top")) : t.bottom > s.bottom - Qr(r, "bottom") && (l = t.bottom - t.top > s.bottom - s.top ? t.top + Qr(i, "top") - s.top : t.bottom - s.bottom + Qr(i, "bottom")), t.left < s.left + Qr(r, "left") ? c = -(s.left - t.left + Qr(i, "left")) : t.right > s.right - Qr(r, "right") && (c = t.right - s.right + Qr(i, "right")), c || l) {
			if (n) a.defaultView.scrollBy(c, l);
			else {
				let n = e.scrollLeft, r = e.scrollTop;
				l && (e.scrollTop += l), c && (e.scrollLeft += c);
				let i = e.scrollLeft - n, a = e.scrollTop - r;
				t = {
					left: t.left - i,
					top: t.top - a,
					right: t.right - i,
					bottom: t.bottom - a
				};
			}
		}
		let u = n ? "fixed" : getComputedStyle(o).position;
		if (/^(fixed|sticky)$/.test(u)) break;
		o = u == "absolute" ? o.offsetParent : vr(o);
	}
}
function ti(e) {
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
		stack: ni(e.dom)
	};
}
function ni(e) {
	let t = [], n = e.ownerDocument;
	for (let r = e; r && (t.push({
		dom: r,
		top: r.scrollTop,
		left: r.scrollLeft
	}), e != n); r = vr(r));
	return t;
}
function ri({ refDOM: e, refTop: t, stack: n }) {
	let r = e ? e.getBoundingClientRect().top : 0;
	ii(n, r == 0 ? 0 : r - t);
}
function ii(e, t) {
	for (let n = 0; n < e.length; n++) {
		let { dom: r, top: i, left: a } = e[n];
		r.scrollTop != i + t && (r.scrollTop = i + t), r.scrollLeft != a && (r.scrollLeft = a);
	}
}
var ai = null;
function oi(e) {
	if (e.setActive) return e.setActive();
	if (ai) return e.focus(ai);
	let t = ni(e);
	e.focus(ai == null ? { get preventScroll() {
		return ai = { preventScroll: !0 }, !0;
	} } : void 0), ai || (ai = !1, ii(t, 0));
}
function si(e, t) {
	let n, r = 2e8, i, a = 0, o = t.top, s = t.top, c, l;
	for (let u = e.firstChild, d = 0; u; u = u.nextSibling, d++) {
		let e;
		if (u.nodeType == 1) e = u.getClientRects();
		else if (u.nodeType == 3) e = br(u).getClientRects();
		else continue;
		for (let f = 0; f < e.length; f++) {
			let p = e[f];
			if (p.top <= o && p.bottom >= s) {
				o = Math.max(p.bottom, o), s = Math.min(p.top, s);
				let e = p.left > t.left ? p.left - t.left : p.right < t.left ? t.left - p.right : 0;
				if (e < r) {
					n = u, r = e, i = e && n.nodeType == 3 ? {
						left: p.right < t.left ? p.right : p.left,
						top: t.top
					} : t, u.nodeType == 1 && e && (a = d + +(t.left >= (p.left + p.right) / 2));
					continue;
				}
			} else p.top > t.top && !c && p.left <= t.left && p.right >= t.left && (c = u, l = {
				left: Math.max(p.left, Math.min(p.right, t.left)),
				top: p.top
			});
			!n && (t.left >= p.right && t.top >= p.top || t.left >= p.left && t.top >= p.bottom) && (a = d + 1);
		}
	}
	return !n && c && (n = c, i = l, r = 0), n && n.nodeType == 3 ? ci(n, i) : !n || r && n.nodeType == 1 ? {
		node: e,
		offset: a
	} : si(n, i);
}
function ci(e, t) {
	let n = e.nodeValue.length, r = document.createRange(), i;
	for (let a = 0; a < n; a++) {
		r.setEnd(e, a + 1), r.setStart(e, a);
		let n = gi(r, 1);
		if (n.top != n.bottom && li(t, n)) {
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
function li(e, t) {
	return e.left >= t.left - 1 && e.left <= t.right + 1 && e.top >= t.top - 1 && e.top <= t.bottom + 1;
}
function ui(e, t) {
	let n = e.parentNode;
	return n && /^li$/i.test(n.nodeName) && t.left < e.getBoundingClientRect().left ? n : e;
}
function di(e, t, n) {
	let { node: r, offset: i } = si(t, n), a = -1;
	if (r.nodeType == 1 && !r.firstChild) {
		let e = r.getBoundingClientRect();
		a = e.left != e.right && n.left > (e.left + e.right) / 2 ? 1 : -1;
	}
	return e.docView.posFromDOM(r, i, a);
}
function fi(e, t, n, r) {
	let i = -1;
	for (let n = t, a = !1; n != e.dom;) {
		let t = e.docView.nearestDesc(n, !0), o;
		if (!t) return null;
		if (t.dom.nodeType == 1 && (t.node.isBlock && t.parent || !t.contentDOM) && ((o = t.dom.getBoundingClientRect()).width || o.height) && (t.node.isBlock && t.parent && !/^T(R|BODY|HEAD|FOOT)$/.test(t.dom.nodeName) && (!a && o.left > r.left || o.top > r.top ? i = t.posBefore : (!a && o.right < r.left || o.bottom < r.top) && (i = t.posAfter), a = !0), !t.contentDOM && i < 0 && !t.node.isText)) return (t.node.isBlock ? r.top < (o.top + o.bottom) / 2 : r.left < (o.left + o.right) / 2) ? t.posBefore : t.posAfter;
		n = t.dom.parentNode;
	}
	return i > -1 ? i : e.docView.posFromDOM(t, n, -1);
}
function pi(e, t, n) {
	let r = e.childNodes.length;
	if (r && n.top < n.bottom) for (let i = Math.max(0, Math.min(r - 1, Math.floor(r * (t.top - n.top) / (n.bottom - n.top)) - 2)), a = i;;) {
		let n = e.childNodes[a];
		if (n.nodeType == 1) {
			let e = n.getClientRects();
			for (let r = 0; r < e.length; r++) {
				let i = e[r];
				if (li(t, i)) return pi(n, t, i);
			}
		}
		if ((a = (a + 1) % r) == i) break;
	}
	return e;
}
function mi(e, t) {
	let n = e.dom.ownerDocument, r, i = 0, a = Nr(n, t.left, t.top);
	a && ({node: r, offset: i} = a);
	let o = (e.root.elementFromPoint ? e.root : n).elementFromPoint(t.left, t.top), s;
	if (!o || !e.dom.contains(o.nodeType == 1 ? o : o.parentNode)) {
		let n = e.dom.getBoundingClientRect();
		if (!li(t, n) || (o = pi(e.dom, t, n), !o)) return null;
	}
	if (N) for (let e = o; r && e; e = vr(e)) e.draggable && (r = void 0);
	if (o = ui(o, t), r) {
		if (Hr && r.nodeType == 1 && (i = Math.min(i, r.childNodes.length), i < r.childNodes.length)) {
			let e = r.childNodes[i], n;
			e.nodeName == "IMG" && (n = e.getBoundingClientRect()).right <= t.left && n.bottom > t.top && i++;
		}
		let n;
		Yr && i && r.nodeType == 1 && (n = r.childNodes[i - 1]).nodeType == 1 && n.contentEditable == "false" && n.getBoundingClientRect().top >= t.top && i--, r == e.dom && i == r.childNodes.length - 1 && r.lastChild.nodeType == 1 && t.top > r.lastChild.getBoundingClientRect().bottom ? s = e.state.doc.content.size : (i == 0 || r.nodeType != 1 || r.childNodes[i - 1].nodeName != "BR") && (s = fi(e, r, i, t));
	}
	s ?? (s = di(e, o, t));
	let c = e.docView.nearestDesc(o, !0);
	return {
		pos: s,
		inside: c ? c.posAtStart - c.border : -1
	};
}
function hi(e) {
	return e.top < e.bottom || e.left < e.right;
}
function gi(e, t) {
	let n = e.getClientRects();
	if (n.length) {
		let e = n[t < 0 ? 0 : n.length - 1];
		if (hi(e)) return e;
	}
	return Array.prototype.find.call(n, hi) || e.getBoundingClientRect();
}
var _i = /[\u0590-\u05f4\u0600-\u06ff\u0700-\u08ac]/;
function vi(e, t, n) {
	let { node: r, offset: i, atom: a } = e.docView.domFromPos(t, n < 0 ? -1 : 1), o = Yr || Hr;
	if (r.nodeType == 3) {
		if (o && (_i.test(r.nodeValue) || (n < 0 ? !i : i == r.nodeValue.length))) {
			let e = gi(br(r, i, i), n);
			if (Hr && i && /\s/.test(r.nodeValue[i - 1]) && i < r.nodeValue.length) {
				let t = gi(br(r, i - 1, i - 1), -1);
				if (t.top == e.top) {
					let n = gi(br(r, i, i + 1), -1);
					if (n.top != e.top) return yi(n, n.left < t.left);
				}
			}
			return e;
		}
		{
			let e = i, t = i, a = n < 0 ? 1 : -1;
			return n < 0 && !i ? (t++, a = -1) : n >= 0 && i == r.nodeValue.length ? (e--, a = 1) : n < 0 ? e-- : t++, yi(gi(br(r, e, t), a), a < 0);
		}
	}
	if (!e.state.doc.resolve(t - (a || 0)).parent.inlineContent) {
		if (a == null && i && (n < 0 || i == Tr(r))) {
			let e = r.childNodes[i - 1];
			if (e.nodeType == 1) return bi(e.getBoundingClientRect(), !1);
		}
		if (a == null && i < Tr(r)) {
			let e = r.childNodes[i];
			if (e.nodeType == 1) return bi(e.getBoundingClientRect(), !0);
		}
		return bi(r.getBoundingClientRect(), n >= 0);
	}
	if (a == null && i && (n < 0 || i == Tr(r))) {
		let e = r.childNodes[i - 1], t = e.nodeType == 3 ? br(e, Tr(e) - +!o) : e.nodeType == 1 && (e.nodeName != "BR" || !e.nextSibling) ? e : null;
		if (t) return yi(gi(t, 1), !1);
	}
	if (a == null && i < Tr(r)) {
		let e = r.childNodes[i];
		for (; e.pmViewDesc && e.pmViewDesc.ignoreForCoords;) e = e.nextSibling;
		let t = e ? e.nodeType == 3 ? br(e, 0, +!o) : e.nodeType == 1 ? e : null : null;
		if (t) return yi(gi(t, -1), !0);
	}
	return yi(gi(r.nodeType == 3 ? br(r) : r, -n), n >= 0);
}
function yi(e, t) {
	if (e.width == 0) return e;
	let n = t ? e.left : e.right;
	return {
		top: e.top,
		bottom: e.bottom,
		left: n,
		right: n
	};
}
function bi(e, t) {
	if (e.height == 0) return e;
	let n = t ? e.top : e.bottom;
	return {
		top: n,
		bottom: n,
		left: e.left,
		right: e.right
	};
}
function xi(e, t, n) {
	let r = e.state, i = e.root.activeElement;
	r != t && e.updateState(t), i != e.dom && e.focus();
	try {
		return n();
	} finally {
		r != t && e.updateState(r), i != e.dom && i && i.focus();
	}
}
function Si(e, t, n) {
	let r = t.selection, i = n == "up" ? r.$from : r.$to;
	return xi(e, t, () => {
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
		let r = vi(e, i.pos, 1);
		for (let e = t.firstChild; e; e = e.nextSibling) {
			let t;
			if (e.nodeType == 1) t = e.getClientRects();
			else if (e.nodeType == 3) t = br(e, 0, e.nodeValue.length).getClientRects();
			else continue;
			for (let e = 0; e < t.length; e++) {
				let i = t[e];
				if (i.bottom > i.top + 1 && (n == "up" ? r.top - i.top > (i.bottom - r.top) * 2 : i.bottom - r.bottom > (r.bottom - i.top) * 2)) return !1;
			}
		}
		return !0;
	});
}
var Ci = /[\u0590-\u08ac]/;
function wi(e, t, n) {
	let { $head: r } = t.selection;
	if (!r.parent.isTextblock) return !1;
	let i = r.parentOffset, a = !i, o = i == r.parent.content.size, s = e.domSelection();
	return s ? !Ci.test(r.parent.textContent) || !s.modify ? n == "left" || n == "backward" ? a : o : xi(e, t, () => {
		let { focusNode: t, focusOffset: i, anchorNode: a, anchorOffset: o } = e.domSelectionRange(), c = s.caretBidiLevel;
		s.modify("move", n, "character");
		let l = r.depth ? e.docView.domAfterPos(r.before()) : e.dom, { focusNode: u, focusOffset: d } = e.domSelectionRange(), f = u && !l.contains(u.nodeType == 1 ? u : u.parentNode) || t == u && i == d;
		try {
			s.collapse(a, o), t && (t != a || i != o) && s.extend && s.extend(t, i);
		} catch {}
		return c != null && (s.caretBidiLevel = c), f;
	}) : r.pos == r.start() || r.pos == r.end();
}
var Ti = null, Ei = null, Di = !1;
function Oi(e, t, n) {
	return Ti == t && Ei == n ? Di : (Ti = t, Ei = n, Di = n == "up" || n == "down" ? Si(e, t, n) : wi(e, t, n));
}
var ki = 0, Ai = 1, ji = 2, Mi = 3, Ni = class {
	constructor(e, t, n, r) {
		this.parent = e, this.children = t, this.dom = n, this.contentDOM = r, this.dirty = ki, n.pmViewDesc = this;
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
	parseRule() {
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
		if (this.contentDOM && this.contentDOM.contains(e.nodeType == 1 ? e : e.parentNode)) {
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
		if (e == this.dom && this.contentDOM) r = t > j(this.contentDOM);
		else if (this.contentDOM && this.contentDOM != this.dom && this.dom.contains(this.contentDOM)) r = e.compareDocumentPosition(this.contentDOM) & 2;
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
				if (n && (a = i.nodeDOM) && !(a.nodeType == 1 ? a.contains(e.nodeType == 1 ? e : e.parentNode) : a == e)) n = !1;
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
			if (a > e || i instanceof Bi) {
				r = e - t;
				break;
			}
			t = a;
		}
		if (r) return this.children[n].domFromPos(r - this.children[n].border, t);
		for (let e; n && !(e = this.children[n - 1]).size && e instanceof Pi && e.side >= 0; n--);
		if (t <= 0) {
			let e, r = !0;
			for (; e = n ? this.children[n - 1] : null, e && e.dom.parentNode != this.contentDOM; n--, r = !1);
			return e && t && r && !e.border && !e.domAtom ? e.domFromPos(e.size, t) : {
				node: this.contentDOM,
				offset: e ? j(e.dom) + 1 : 0
			};
		}
		{
			let e, r = !0;
			for (; e = n < this.children.length ? this.children[n] : null, e && e.dom.parentNode != this.contentDOM; n++, r = !1);
			return e && r && !e.border && !e.domAtom ? e.domFromPos(0, t) : {
				node: this.contentDOM,
				offset: e ? j(e.dom) : this.contentDOM.childNodes.length
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
						r = j(n.dom) + 1;
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
						i = j(n.dom);
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
		if (t.nodeType != 1 || n == t.childNodes.length) throw RangeError("No node after pos " + e);
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
		if ((Hr || N) && e == t) {
			let { node: e, offset: t } = o;
			if (e.nodeType == 3) {
				if (u = !!(t && e.nodeValue[t - 1] == "\n"), u && t == e.nodeValue.length) for (let t = e, n; t; t = t.parentNode) {
					if (n = t.nextSibling) {
						n.nodeName == "BR" && (o = s = {
							node: n.parentNode,
							offset: j(n) + 1
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
		if (Hr && l.focusNode && l.focusNode != s.node && l.focusNode.nodeType == 1) {
			let e = l.focusNode.childNodes[l.focusOffset];
			e && e.contentEditable == "false" && (r = !0);
		}
		if (!(r || u && N) && Sr(o.node, o.offset, l.anchorNode, l.anchorOffset) && Sr(s.node, s.offset, l.focusNode, l.focusOffset)) return;
		let d = !1;
		if ((c.extend || e == t) && !(u && Hr)) {
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
					this.dirty = e == n || t == a ? ji : Ai, e == r && t == o && (i.contentLost || i.dom.parentNode != this.contentDOM) ? i.dirty = Mi : i.markDirty(e - r, t - r);
					return;
				}
				i.dirty = i.dom == i.contentDOM && i.dom.parentNode == this.contentDOM && !i.children.length ? ji : Mi;
			}
			n = a;
		}
		this.dirty = ji;
	}
	markParentsDirty() {
		let e = 1;
		for (let t = this.parent; t; t = t.parent, e++) {
			let n = e == 1 ? ji : Ai;
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
}, Pi = class extends Ni {
	constructor(e, t, n, r) {
		let i, a = t.type.toDOM;
		if (typeof a == "function" && (a = a(n, () => {
			if (!i) return r;
			if (i.parent) return i.parent.posBeforeChild(i);
		})), !t.type.spec.raw) {
			if (a.nodeType != 1) {
				let e = document.createElement("span");
				e.appendChild(a), a = e;
			}
			a.contentEditable = "false", a.classList.add("ProseMirror-widget");
		}
		super(e, [], a, null), this.widget = t, this.widget = t, i = this;
	}
	matchesWidget(e) {
		return this.dirty == ki && e.type.eq(this.widget.type);
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
}, Fi = class extends Ni {
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
}, Ii = class e extends Ni {
	constructor(e, t, n, r, i) {
		super(e, [], n, r), this.mark = t, this.spec = i;
	}
	static create(t, n, r, i) {
		let a = i.nodeViews[n.type.name], o = a && a(n, i, r);
		return (!o || !o.dom) && (o = Je.renderSpec(document, n.type.spec.toDOM(n, r), null, n.attrs)), new e(t, n, o.dom, o.contentDOM || o.dom, o);
	}
	parseRule() {
		return this.dirty & Mi || this.mark.type.spec.reparseInView ? null : {
			mark: this.mark.type.name,
			attrs: this.mark.attrs,
			contentElement: this.contentDOM
		};
	}
	matchesMark(e) {
		return this.dirty != Mi && this.mark.eq(e);
	}
	markDirty(e, t) {
		if (super.markDirty(e, t), this.dirty != ki) {
			let e = this.parent;
			for (; !e.node;) e = e.parent;
			e.dirty < this.dirty && (e.dirty = this.dirty), this.dirty = ki;
		}
	}
	slice(t, n, r) {
		let i = e.create(this.parent, this.mark, !0, r), a = this.children, o = this.size;
		n < o && (a = ra(a, n, o, r)), t > 0 && (a = ra(a, 0, t, r));
		for (let e = 0; e < a.length; e++) a[e].parent = i;
		return i.children = a, i;
	}
	ignoreMutation(e) {
		return this.spec.ignoreMutation ? this.spec.ignoreMutation(e) : super.ignoreMutation(e);
	}
	destroy() {
		this.spec.destroy && this.spec.destroy(), super.destroy();
	}
}, Li = class e extends Ni {
	constructor(e, t, n, r, i, a, o, s, c) {
		super(e, [], i, a), this.node = t, this.outerDeco = n, this.innerDeco = r, this.nodeDOM = o;
	}
	static create(t, n, r, i, a, o) {
		let s = a.nodeViews[n.type.name], c, l = s && s(n, a, () => {
			if (!c) return o;
			if (c.parent) return c.parent.posBeforeChild(c);
		}, r, i), u = l && l.dom, d = l && l.contentDOM;
		if (n.isText) {
			if (!u) u = document.createTextNode(n.text);
			else if (u.nodeType != 3) throw RangeError("Text must be rendered as a DOM text node");
		} else if (!u) {
			let e = Je.renderSpec(document, n.type.spec.toDOM(n), null, n.attrs);
			({dom: u, contentDOM: d} = e);
		}
		!d && !n.isText && u.nodeName != "BR" && (u.hasAttribute("contenteditable") || (u.contentEditable = "false"), n.type.spec.draggable && (u.draggable = !0));
		let f = u;
		return u = Ji(u, r, n), l ? c = new Vi(t, n, r, i, u, d || null, f, l, a, o + 1) : n.isText ? new zi(t, n, r, i, u, f, a) : new e(t, n, r, i, u, d || null, f, a, o + 1);
	}
	parseRule() {
		if (this.node.type.spec.reparseInView) return null;
		let e = {
			node: this.node.type.name,
			attrs: this.node.attrs
		};
		if (this.node.type.whitespace == "pre" && (e.preserveWhitespace = "full"), !this.contentDOM) e.getContent = () => this.node.content;
		else if (!this.contentLost) e.contentElement = this.contentDOM;
		else {
			for (let t = this.children.length - 1; t >= 0; t--) {
				let n = this.children[t];
				if (this.dom.contains(n.dom.parentNode)) {
					e.contentElement = n.dom.parentNode;
					break;
				}
			}
			e.contentElement || (e.getContent = () => a.empty);
		}
		return e;
	}
	matchesNode(e, t, n) {
		return this.dirty == ki && e.eq(this.node) && Yi(t, this.outerDeco) && n.eq(this.innerDeco);
	}
	get size() {
		return this.node.nodeSize;
	}
	get border() {
		return +!this.node.isLeaf;
	}
	updateChildren(e, t) {
		let n = this.node.inlineContent, r = t, i = e.composing ? this.localCompositionInfo(e, t) : null, a = i && i.pos > -1 ? i : null, o = i && i.pos < 0, s = new Zi(this, a && a.node, e);
		ea(this.node, this.innerDeco, (t, i, a) => {
			t.spec.marks ? s.syncToMarks(t.spec.marks, n, e, i) : t.type.side >= 0 && !a && s.syncToMarks(i == this.node.childCount ? l.none : this.node.child(i).marks, n, e, i), s.placeWidget(t, e, r);
		}, (t, a, c, l) => {
			s.syncToMarks(t.marks, n, e, l);
			let u;
			s.findNodeMatch(t, a, c, l) || o && e.state.selection.from > r && e.state.selection.to < r + t.nodeSize && (u = s.findIndexWithChild(i.node)) > -1 && s.updateNodeAt(t, a, c, u, e) || s.updateNextNode(t, a, c, e, l, r) || s.addNode(t, a, c, e, r), r += t.nodeSize;
		}), s.syncToMarks([], n, e, 0), this.node.isTextblock && s.addTextblockHacks(), s.destroyRest(), (s.changed || this.dirty == ji) && (a && this.protectLocalComposition(e, a), Hi(this.contentDOM, this.children, e), Gr && ta(this.dom));
	}
	localCompositionInfo(e, t) {
		let { from: n, to: r } = e.state.selection;
		if (!(e.state.selection instanceof D) || n < t || r > t + this.node.content.size) return null;
		let i = e.input.compositionNode;
		if (!i || !this.dom.contains(i.parentNode)) return null;
		if (this.node.inlineContent) {
			let e = i.nodeValue, a = na(this.node.content, e, n - t, r - t);
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
		let a = new Fi(this, i, t, r);
		e.input.compositionNodes.push(a), this.children = ra(this.children, n, n + r.length, e, a);
	}
	update(e, t, n, r) {
		return this.dirty == Mi || !e.sameMarkup(this.node) ? !1 : (this.updateInner(e, t, n, r), !0);
	}
	updateInner(e, t, n, r) {
		this.updateOuterDeco(t), this.node = e, this.innerDeco = n, this.contentDOM && this.updateChildren(r, this.posAtStart), this.dirty = ki;
	}
	updateOuterDeco(e) {
		if (Yi(e, this.outerDeco)) return;
		let t = this.nodeDOM.nodeType != 1, n = this.dom;
		this.dom = Ki(this.dom, this.nodeDOM, Gi(this.outerDeco, this.node, t), Gi(e, this.node, t)), this.dom != n && (n.pmViewDesc = void 0, this.dom.pmViewDesc = this), this.outerDeco = e;
	}
	selectNode() {
		this.nodeDOM.nodeType == 1 && (this.nodeDOM.classList.add("ProseMirror-selectednode"), (this.contentDOM || !this.node.type.spec.draggable) && (this.nodeDOM.draggable = !0));
	}
	deselectNode() {
		this.nodeDOM.nodeType == 1 && (this.nodeDOM.classList.remove("ProseMirror-selectednode"), (this.contentDOM || !this.node.type.spec.draggable) && this.nodeDOM.removeAttribute("draggable"));
	}
	get domAtom() {
		return this.node.isAtom;
	}
};
function Ri(e, t, n, r, i) {
	Ji(r, t, e);
	let a = new Li(void 0, e, t, n, r, r, r, i, 0);
	return a.contentDOM && a.updateChildren(i, 0), a;
}
var zi = class e extends Li {
	constructor(e, t, n, r, i, a, o) {
		super(e, t, n, r, i, null, a, o, 0);
	}
	parseRule() {
		let e = this.nodeDOM.parentNode;
		for (; e && e != this.dom && !e.pmIsDeco;) e = e.parentNode;
		return { skip: e || !0 };
	}
	update(e, t, n, r) {
		return this.dirty == Mi || this.dirty != ki && !this.inParent() || !e.sameMarkup(this.node) ? !1 : (this.updateOuterDeco(t), (this.dirty != ki || e.text != this.node.text) && e.text != this.nodeDOM.nodeValue && (this.nodeDOM.nodeValue = e.text, r.trackWrites == this.nodeDOM && (r.trackWrites = null)), this.node = e, this.dirty = ki, !0);
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
		return new e(this.parent, i, this.outerDeco, this.innerDeco, a, a, r);
	}
	markDirty(e, t) {
		super.markDirty(e, t), this.dom != this.nodeDOM && (e == 0 || t == this.nodeDOM.nodeValue.length) && (this.dirty = Mi);
	}
	get domAtom() {
		return !1;
	}
	isText(e) {
		return this.node.text == e;
	}
}, Bi = class extends Ni {
	parseRule() {
		return { ignore: !0 };
	}
	matchesHack(e) {
		return this.dirty == ki && this.dom.nodeName == e;
	}
	get domAtom() {
		return !0;
	}
	get ignoreForCoords() {
		return this.dom.nodeName == "IMG";
	}
}, Vi = class extends Li {
	constructor(e, t, n, r, i, a, o, s, c, l) {
		super(e, t, n, r, i, a, o, c, l), this.spec = s;
	}
	update(e, t, n, r) {
		if (this.dirty == Mi) return !1;
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
function Hi(e, t, n) {
	let r = e.firstChild, i = !1;
	for (let a = 0; a < t.length; a++) {
		let o = t[a], s = o.dom;
		if (s.parentNode == e) {
			for (; s != r;) r = Xi(r), i = !0;
			r = r.nextSibling;
		} else i = !0, e.insertBefore(s, r);
		if (o instanceof Ii) {
			let t = r ? r.previousSibling : e.lastChild;
			Hi(o.contentDOM, o.children, n), r = t ? t.nextSibling : e.firstChild;
		}
	}
	for (; r;) r = Xi(r), i = !0;
	i && n.trackWrites == e && (n.trackWrites = null);
}
var Ui = function(e) {
	e && (this.nodeName = e);
};
Ui.prototype = Object.create(null);
var Wi = [new Ui()];
function Gi(e, t, n) {
	if (e.length == 0) return Wi;
	let r = n ? Wi[0] : new Ui(), i = [r];
	for (let a = 0; a < e.length; a++) {
		let o = e[a].type.attrs;
		if (o) {
			o.nodeName && i.push(r = new Ui(o.nodeName));
			for (let e in o) {
				let a = o[e];
				a != null && (n && i.length == 1 && i.push(r = new Ui(t.isInline ? "span" : "div")), e == "class" ? r.class = (r.class ? r.class + " " : "") + a : e == "style" ? r.style = (r.style ? r.style + ";" : "") + a : e != "nodeName" && (r[e] = a));
			}
		}
	}
	return i;
}
function Ki(e, t, n, r) {
	if (n == Wi && r == Wi) return t;
	let i = t;
	for (let t = 0; t < r.length; t++) {
		let a = r[t], o = n[t];
		if (t) {
			let t;
			o && o.nodeName == a.nodeName && i != e && (t = i.parentNode) && t.nodeName.toLowerCase() == a.nodeName ? i = t : (t = document.createElement(a.nodeName), t.pmIsDeco = !0, t.appendChild(i), o = Wi[0], i = t);
		}
		qi(i, o || Wi[0], a);
	}
	return i;
}
function qi(e, t, n) {
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
function Ji(e, t, n) {
	return Ki(e, e, Wi, Gi(t, n, e.nodeType != 1));
}
function Yi(e, t) {
	if (e.length != t.length) return !1;
	for (let n = 0; n < e.length; n++) if (!e[n].type.eq(t[n].type)) return !1;
	return !0;
}
function Xi(e) {
	let t = e.nextSibling;
	return e.parentNode.removeChild(e), t;
}
var Zi = class {
	constructor(e, t, n) {
		this.lock = t, this.view = n, this.index = 0, this.stack = [], this.changed = !1, this.top = e, this.preMatch = Qi(e.node.content, e);
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
		for (; i < a;) this.destroyRest(), this.top.dirty = ki, this.index = this.stack.pop(), this.top = this.stack.pop(), a--;
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
			if (i > -1) i > this.index && (this.changed = !0, this.destroyBetween(this.index, i)), this.top = this.top.children[this.index];
			else {
				let r = Ii.create(this.top, e[a], t, n);
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
		return a.dirty == Mi && a.dom == a.contentDOM && (a.dirty = ji), a.update(e, t, n, i) ? (this.destroyBetween(this.index, r), this.index++, !0) : !1;
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
			if (s instanceof Li) {
				let c = this.preMatch.matched.get(s);
				if (c != null && c != i) return !1;
				let l = s.dom, u, d = this.isLocked(l) && !(e.isText && s.node && s.node.isText && s.nodeDOM.nodeValue == e.text && s.dirty != Mi && Yi(t, s.outerDeco));
				if (!d && s.update(e, t, n, r)) return this.destroyBetween(this.index, o), s.dom != l && (this.changed = !0), this.index++, !0;
				if (!d && (u = this.recreateWrapper(s, e, t, n, r, a))) return this.destroyBetween(this.index, o), this.top.children[this.index] = u, u.contentDOM && (u.dirty = ji, u.updateChildren(r, a + 1), u.dirty = ki), this.changed = !0, this.index++, !0;
				break;
			}
		}
		return !1;
	}
	recreateWrapper(e, t, n, r, i, a) {
		if (e.dirty || t.isAtom || !e.children.length || !e.node.content.eq(t.content) || !Yi(n, e.outerDeco) || !r.eq(e.innerDeco)) return null;
		let o = Li.create(this.top, t, n, r, i, a);
		if (o.contentDOM) {
			o.children = e.children, e.children = [];
			for (let e of o.children) e.parent = o;
		}
		return e.destroy(), o;
	}
	addNode(e, t, n, r, i) {
		let a = Li.create(this.top, e, t, n, r, i);
		a.contentDOM && a.updateChildren(r, i + 1), this.top.children.splice(this.index++, 0, a), this.changed = !0;
	}
	placeWidget(e, t, n) {
		let r = this.index < this.top.children.length ? this.top.children[this.index] : null;
		if (r && r.matchesWidget(e) && (e == r.widget || !r.widget.type.toDOM.parentNode)) this.index++;
		else {
			let r = new Pi(this.top, e, t, n);
			this.top.children.splice(this.index++, 0, r), this.changed = !0;
		}
	}
	addTextblockHacks() {
		let e = this.top.children[this.index - 1], t = this.top;
		for (; e instanceof Ii;) t = e, e = t.children[t.children.length - 1];
		(!e || !(e instanceof zi) || /\n$/.test(e.node.text) || this.view.requiresGeckoHackNode && /\s$/.test(e.node.text)) && ((N || M) && e && e.dom.contentEditable == "false" && this.addHackNode("IMG", t), this.addHackNode("BR", this.top));
	}
	addHackNode(e, t) {
		if (t == this.top && this.index < t.children.length && t.children[this.index].matchesHack(e)) this.index++;
		else {
			let n = document.createElement(e);
			e == "IMG" && (n.className = "ProseMirror-separator", n.alt = ""), e == "BR" && (n.className = "ProseMirror-trailingBreak");
			let r = new Bi(this.top, [], n, null);
			t == this.top ? t.children.splice(this.index++, 0, r) : t.children.push(r), this.changed = !0;
		}
	}
	isLocked(e) {
		return this.lock && (e == this.lock || e.nodeType == 1 && e.contains(this.lock.parentNode));
	}
};
function Qi(e, t) {
	let n = t, r = n.children.length, i = e.childCount, a = /* @__PURE__ */ new Map(), o = [];
	outer: for (; i > 0;) {
		let s;
		for (;;) if (r) {
			let e = n.children[r - 1];
			if (e instanceof Ii) n = e, r = e.children.length;
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
function $i(e, t) {
	return e.type.side - t.type.side;
}
function ea(e, t, n, r) {
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
				d.sort($i);
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
function ta(e) {
	if (e.nodeName == "UL" || e.nodeName == "OL") {
		let t = e.style.cssText;
		e.style.cssText = t + "; list-style: square !important", window.getComputedStyle(e).listStyle, e.style.cssText = t;
	}
}
function na(e, t, n, r) {
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
function ra(e, t, n, r, i) {
	let a = [];
	for (let o = 0, s = 0; o < e.length; o++) {
		let c = e[o], l = s, u = s += c.size;
		l >= n || u <= t ? a.push(c) : (l < t && a.push(c.slice(0, t - l, r)), i && (a.push(i), i = void 0), u > n && a.push(c.slice(n - l, c.size, r)));
	}
	return a;
}
function ia(e, t = null) {
	let n = e.domSelectionRange(), r = e.state.doc;
	if (!n.focusNode) return null;
	let i = e.docView.nearestDesc(n.focusNode), a = i && i.size == 0, o = e.docView.posFromDOM(n.focusNode, n.focusOffset, 1);
	if (o < 0) return null;
	let s = r.resolve(o), c, l;
	if (Ar(n)) {
		for (c = o; i && !i.node;) i = i.parent;
		let e = i.node;
		if (i && e.isAtom && O.isSelectable(e) && i.parent && !(e.isInline && Or(n.focusNode, n.focusOffset, i.dom))) {
			let e = i.posBefore;
			l = new O(o == e ? s : r.resolve(e));
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
		l = ha(e, u, s, n);
	}
	return l;
}
function aa(e) {
	return e.editable ? e.hasFocus() : _a(e) && document.activeElement && document.activeElement.contains(e.dom);
}
function oa(e, t = !1) {
	let n = e.state.selection;
	if (pa(e, n), aa(e)) {
		if (!t && e.input.mouseDown && e.input.mouseDown.allowDefault && M) {
			let t = e.domSelectionRange(), n = e.domObserver.currentSelection;
			if (t.anchorNode && n.anchorNode && Sr(t.anchorNode, t.anchorOffset, n.anchorNode, n.anchorOffset)) {
				e.input.mouseDown.delayedSelectionSync = !0, e.domObserver.setCurSelection();
				return;
			}
		}
		if (e.domObserver.disconnectSelection(), e.cursorWrapper) fa(e);
		else {
			let { anchor: r, head: i } = n, a, o;
			sa && !(n instanceof D) && (n.$from.parent.inlineContent || (a = ca(e, n.from)), !n.empty && !n.$from.parent.inlineContent && (o = ca(e, n.to))), e.docView.setSelection(r, i, e, t), sa && (a && ua(a), o && ua(o)), n.visible ? e.dom.classList.remove("ProseMirror-hideselection") : (e.dom.classList.add("ProseMirror-hideselection"), "onselectionchange" in document && da(e));
		}
		e.domObserver.setCurSelection(), e.domObserver.connectSelection();
	}
}
var sa = N || M && Wr < 63;
function ca(e, t) {
	let { node: n, offset: r } = e.docView.domFromPos(t, 0), i = r < n.childNodes.length ? n.childNodes[r] : null, a = r ? n.childNodes[r - 1] : null;
	if (N && i && i.contentEditable == "false") return la(i);
	if ((!i || i.contentEditable == "false") && (!a || a.contentEditable == "false")) {
		if (i) return la(i);
		if (a) return la(a);
	}
}
function la(e) {
	return e.contentEditable = "true", N && e.draggable && (e.draggable = !1, e.wasDraggable = !0), e;
}
function ua(e) {
	e.contentEditable = "false", e.wasDraggable && (e.draggable = !0, e.wasDraggable = null);
}
function da(e) {
	let t = e.dom.ownerDocument;
	t.removeEventListener("selectionchange", e.input.hideSelectionGuard);
	let n = e.domSelectionRange(), r = n.anchorNode, i = n.anchorOffset;
	t.addEventListener("selectionchange", e.input.hideSelectionGuard = () => {
		(n.anchorNode != r || n.anchorOffset != i) && (t.removeEventListener("selectionchange", e.input.hideSelectionGuard), setTimeout(() => {
			(!aa(e) || e.state.selection.visible) && e.dom.classList.remove("ProseMirror-hideselection");
		}, 20));
	});
}
function fa(e) {
	let t = e.domSelection();
	if (!t) return;
	let n = e.cursorWrapper.dom, r = n.nodeName == "IMG";
	r ? t.collapse(n.parentNode, j(n) + 1) : t.collapse(n, 0), !r && !e.state.selection.visible && Br && Vr <= 11 && (n.disabled = !0, n.disabled = !1);
}
function pa(e, t) {
	if (t instanceof O) {
		let n = e.docView.descAt(t.from);
		n != e.lastSelectedViewDesc && (ma(e), n && n.selectNode(), e.lastSelectedViewDesc = n);
	} else ma(e);
}
function ma(e) {
	e.lastSelectedViewDesc && (e.lastSelectedViewDesc.parent && e.lastSelectedViewDesc.deselectNode(), e.lastSelectedViewDesc = void 0);
}
function ha(e, t, n, r) {
	return e.someProp("createSelectionBetween", (r) => r(e, t, n)) || D.between(t, n, r);
}
function ga(e) {
	return e.editable && !e.hasFocus() ? !1 : _a(e);
}
function _a(e) {
	let t = e.domSelectionRange();
	if (!t.anchorNode) return !1;
	try {
		return e.dom.contains(t.anchorNode.nodeType == 3 ? t.anchorNode.parentNode : t.anchorNode) && (e.editable || e.dom.contains(t.focusNode.nodeType == 3 ? t.focusNode.parentNode : t.focusNode));
	} catch {
		return !1;
	}
}
function va(e) {
	let t = e.docView.domFromPos(e.state.selection.anchor, 0), n = e.domSelectionRange();
	return Sr(t.node, t.offset, n.anchorNode, n.anchorOffset);
}
function ya(e, t) {
	let { $anchor: n, $head: r } = e.selection, i = t > 0 ? n.max(r) : n.min(r), a = i.parent.inlineContent ? i.depth ? e.doc.resolve(t > 0 ? i.after() : i.before()) : null : i;
	return a && E.findFrom(a, t);
}
function ba(e, t) {
	return e.dispatch(e.state.tr.setSelection(t).scrollIntoView()), !0;
}
function xa(e, t, n) {
	let r = e.state.selection;
	if (r instanceof D) {
		if (n.indexOf("s") > -1) {
			let { $head: n } = r, i = n.textOffset ? null : t < 0 ? n.nodeBefore : n.nodeAfter;
			if (!i || i.isText || !i.isLeaf) return !1;
			let a = e.state.doc.resolve(n.pos + i.nodeSize * (t < 0 ? -1 : 1));
			return ba(e, new D(r.$anchor, a));
		}
		if (!r.empty) return !1;
		if (e.endOfTextblock(t > 0 ? "forward" : "backward")) {
			let n = ya(e.state, t);
			return n && n instanceof O ? ba(e, n) : !1;
		}
		if (!(Kr && n.indexOf("m") > -1)) {
			let n = r.$head, i = n.textOffset ? null : t < 0 ? n.nodeBefore : n.nodeAfter, a;
			if (!i || i.isText) return !1;
			let o = t < 0 ? n.pos - i.nodeSize : n.pos;
			return i.isAtom || (a = e.docView.descAt(o)) && !a.contentDOM ? O.isSelectable(i) ? ba(e, new O(t < 0 ? e.state.doc.resolve(n.pos - i.nodeSize) : n)) : Yr ? ba(e, new D(e.state.doc.resolve(t < 0 ? o : o + i.nodeSize))) : !1 : !1;
		}
	} else if (r instanceof O && r.node.isInline) return ba(e, new D(t > 0 ? r.$to : r.$from));
	else {
		let n = ya(e.state, t);
		return n ? ba(e, n) : !1;
	}
}
function Sa(e) {
	return e.nodeType == 3 ? e.nodeValue.length : e.childNodes.length;
}
function Ca(e, t) {
	let n = e.pmViewDesc;
	return n && n.size == 0 && (t < 0 || e.nextSibling || e.nodeName != "BR");
}
function wa(e, t) {
	return t < 0 ? Ta(e) : Ea(e);
}
function Ta(e) {
	let t = e.domSelectionRange(), n = t.focusNode, r = t.focusOffset;
	if (!n) return;
	let i, a, o = !1;
	for (Hr && n.nodeType == 1 && r < Sa(n) && Ca(n.childNodes[r], -1) && (o = !0);;) if (r > 0) {
		if (n.nodeType != 1) break;
		{
			let e = n.childNodes[r - 1];
			if (Ca(e, -1)) i = n, a = --r;
			else if (e.nodeType == 3) n = e, r = n.nodeValue.length;
			else break;
		}
	} else if (Da(n)) break;
	else {
		let t = n.previousSibling;
		for (; t && Ca(t, -1);) i = n.parentNode, a = j(t), t = t.previousSibling;
		if (t) n = t, r = Sa(n);
		else {
			if (n = n.parentNode, n == e.dom) break;
			r = 0;
		}
	}
	o ? Aa(e, n, r) : i && Aa(e, i, a);
}
function Ea(e) {
	let t = e.domSelectionRange(), n = t.focusNode, r = t.focusOffset;
	if (!n) return;
	let i = Sa(n), a, o;
	for (;;) if (r < i) {
		if (n.nodeType != 1) break;
		let e = n.childNodes[r];
		if (Ca(e, 1)) a = n, o = ++r;
		else break;
	} else if (Da(n)) break;
	else {
		let t = n.nextSibling;
		for (; t && Ca(t, 1);) a = t.parentNode, o = j(t) + 1, t = t.nextSibling;
		if (t) n = t, r = 0, i = Sa(n);
		else {
			if (n = n.parentNode, n == e.dom) break;
			r = i = 0;
		}
	}
	a && Aa(e, a, o);
}
function Da(e) {
	let t = e.pmViewDesc;
	return t && t.node && t.node.isBlock;
}
function Oa(e, t) {
	for (; e && t == e.childNodes.length && !kr(e);) t = j(e) + 1, e = e.parentNode;
	for (; e && t < e.childNodes.length;) {
		let n = e.childNodes[t];
		if (n.nodeType == 3) return n;
		if (n.nodeType == 1 && n.contentEditable == "false") break;
		e = n, t = 0;
	}
}
function ka(e, t) {
	for (; e && !t && !kr(e);) t = j(e), e = e.parentNode;
	for (; e && t;) {
		let n = e.childNodes[t - 1];
		if (n.nodeType == 3) return n;
		if (n.nodeType == 1 && n.contentEditable == "false") break;
		e = n, t = e.childNodes.length;
	}
}
function Aa(e, t, n) {
	if (t.nodeType != 3) {
		let e, r;
		(r = Oa(t, n)) ? (t = r, n = 0) : (e = ka(t, n)) && (t = e, n = e.nodeValue.length);
	}
	let r = e.domSelection();
	if (!r) return;
	if (Ar(r)) {
		let e = document.createRange();
		e.setEnd(t, n), e.setStart(t, n), r.removeAllRanges(), r.addRange(e);
	} else r.extend && r.extend(t, n);
	e.domObserver.setCurSelection();
	let { state: i } = e;
	setTimeout(() => {
		e.state == i && oa(e);
	}, 50);
}
function ja(e, t) {
	let n = e.state.doc.resolve(t);
	if (!(M || qr) && n.parent.inlineContent) {
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
function Ma(e, t, n) {
	let r = e.state.selection;
	if (r instanceof D && !r.empty || n.indexOf("s") > -1 || Kr && n.indexOf("m") > -1) return !1;
	let { $from: i, $to: a } = r;
	if (!i.parent.inlineContent || e.endOfTextblock(t < 0 ? "up" : "down")) {
		let n = ya(e.state, t);
		if (n && n instanceof O) return ba(e, n);
	}
	if (!i.parent.inlineContent) {
		let n = t < 0 ? i : a, o = r instanceof fn ? E.near(n, t) : E.findFrom(n, t);
		return o ? ba(e, o) : !1;
	}
	return !1;
}
function Na(e, t) {
	if (!(e.state.selection instanceof D)) return !0;
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
function Pa(e, t, n) {
	e.domObserver.stop(), t.contentEditable = n, e.domObserver.start();
}
function Fa(e) {
	if (!N || e.state.selection.$head.parentOffset > 0) return !1;
	let { focusNode: t, focusOffset: n } = e.domSelectionRange();
	if (t && t.nodeType == 1 && n == 0 && t.firstChild && t.firstChild.contentEditable == "false") {
		let n = t.firstChild;
		Pa(e, n, "true"), setTimeout(() => Pa(e, n, "false"), 20);
	}
	return !1;
}
function Ia(e) {
	let t = "";
	return e.ctrlKey && (t += "c"), e.metaKey && (t += "m"), e.altKey && (t += "a"), e.shiftKey && (t += "s"), t;
}
function La(e, t) {
	let n = t.keyCode, r = Ia(t);
	if (n == 8 || Kr && n == 72 && r == "c") return Na(e, -1) || wa(e, -1);
	if (n == 46 && !t.shiftKey || Kr && n == 68 && r == "c") return Na(e, 1) || wa(e, 1);
	if (n == 13 || n == 27) return !0;
	if (n == 37 || Kr && n == 66 && r == "c") {
		let t = n == 37 ? ja(e, e.state.selection.from) == "ltr" ? -1 : 1 : -1;
		return xa(e, t, r) || wa(e, t);
	}
	if (n == 39 || Kr && n == 70 && r == "c") {
		let t = n == 39 ? ja(e, e.state.selection.from) == "ltr" ? 1 : -1 : 1;
		return xa(e, t, r) || wa(e, t);
	}
	return n == 38 || Kr && n == 80 && r == "c" ? Ma(e, -1, r) || wa(e, -1) : n == 40 || Kr && n == 78 && r == "c" ? Fa(e) || Ma(e, 1, r) || wa(e, 1) : !(r != (Kr ? "m" : "c") || n != 66 && n != 73 && n != 89 && n != 90);
}
function Ra(e, t) {
	e.someProp("transformCopied", (n) => {
		t = n(t, e);
	});
	let n = [], { content: r, openStart: i, openEnd: a } = t;
	for (; i > 1 && a > 1 && r.childCount == 1 && r.firstChild.childCount == 1;) {
		i--, a--;
		let e = r.firstChild;
		n.push(e.type.name, e.attrs == e.type.defaultAttrs ? null : e.attrs), r = e.content;
	}
	let o = e.someProp("clipboardSerializer") || Je.fromSchema(e.state.schema), s = Ya(), c = s.createElement("div");
	c.appendChild(o.serializeFragment(r, { document: s }));
	let l = c.firstChild, u, d = 0;
	for (; l && l.nodeType == 1 && (u = qa[l.nodeName.toLowerCase()]);) {
		for (let e = u.length - 1; e >= 0; e--) {
			let t = s.createElement(u[e]);
			for (; c.firstChild;) t.appendChild(c.firstChild);
			c.appendChild(t), d++;
		}
		l = c.firstChild;
	}
	return l && l.nodeType == 1 && l.setAttribute("data-pm-slice", `${i} ${a}${d ? ` -${d}` : ""} ${JSON.stringify(n)}`), {
		dom: c,
		text: e.someProp("clipboardTextSerializer", (n) => n(t, e)) || t.content.textBetween(0, t.content.size, "\n\n"),
		slice: t
	};
}
function za(e, t, n, r, i) {
	let o = i.parent.type.spec.code, s, c;
	if (!n && !t) return null;
	let l = !!t && (r || o || !n);
	if (l) {
		if (e.someProp("transformPastedText", (n) => {
			t = n(t, o || r, e);
		}), o) return c = new d(a.from(e.state.schema.text(t.replace(/\r\n?/g, "\n"))), 0, 0), e.someProp("transformPasted", (t) => {
			c = t(c, e, !0);
		}), c;
		let n = e.someProp("clipboardTextParser", (n) => n(t, i, r, e));
		if (n) c = n;
		else {
			let n = i.marks(), { schema: r } = e.state, a = Je.fromSchema(r);
			s = document.createElement("div"), t.split(/(?:\r\n?|\n)+/).forEach((e) => {
				let t = s.appendChild(document.createElement("p"));
				e && t.appendChild(a.serializeNode(r.text(e, n)));
			});
		}
	} else e.someProp("transformPastedHTML", (t) => {
		n = t(n, e);
	}), s = Qa(n), Yr && $a(s);
	let u = s && s.querySelector("[data-pm-slice]"), f = u && /^(\d+) (\d+)(?: -(\d+))? (.*)/.exec(u.getAttribute("data-pm-slice") || "");
	if (f && f[3]) for (let e = +f[3]; e > 0; e--) {
		let e = s.firstChild;
		for (; e && e.nodeType != 1;) e = e.nextSibling;
		if (!e) break;
		s = e;
	}
	if (c || (c = (e.someProp("clipboardParser") || e.someProp("domParser") || Me.fromSchema(e.state.schema)).parseSlice(s, {
		preserveWhitespace: !!(l || f),
		context: i,
		ruleFromNode(e) {
			return e.nodeName == "BR" && !e.nextSibling && e.parentNode && !Ba.test(e.parentNode.nodeName) ? { ignore: !0 } : null;
		}
	})), f) c = eo(Ka(c, +f[1], +f[2]), f[4]);
	else if (c = d.maxOpen(Va(c.content, i), !0), c.openStart || c.openEnd) {
		let e = 0, t = 0;
		for (let t = c.content.firstChild; e < c.openStart && !t.type.spec.isolating; e++, t = t.firstChild);
		for (let e = c.content.lastChild; t < c.openEnd && !e.type.spec.isolating; t++, e = e.lastChild);
		c = Ka(c, e, t);
	}
	return e.someProp("transformPasted", (t) => {
		c = t(c, e, l);
	}), c;
}
var Ba = /^(a|abbr|acronym|b|cite|code|del|em|i|ins|kbd|label|output|q|ruby|s|samp|span|strong|sub|sup|time|u|tt|var)$/i;
function Va(e, t) {
	if (e.childCount < 2) return e;
	for (let n = t.depth; n >= 0; n--) {
		let r = t.node(n).contentMatchAt(t.index(n)), i, o = [];
		if (e.forEach((e) => {
			if (!o) return;
			let t = r.findWrapping(e.type), n;
			if (!t) return o = null;
			if (n = o.length && i.length && Ua(t, i, e, o[o.length - 1], 0)) o[o.length - 1] = n;
			else {
				o.length && (o[o.length - 1] = Wa(o[o.length - 1], i.length));
				let n = Ha(e, t);
				o.push(n), r = r.matchType(n.type), i = t;
			}
		}), o) return a.from(o);
	}
	return e;
}
function Ha(e, t, n = 0) {
	for (let r = t.length - 1; r >= n; r--) e = t[r].create(null, a.from(e));
	return e;
}
function Ua(e, t, n, r, i) {
	if (i < e.length && i < t.length && e[i] == t[i]) {
		let o = Ua(e, t, n, r.lastChild, i + 1);
		if (o) return r.copy(r.content.replaceChild(r.childCount - 1, o));
		if (r.contentMatchAt(r.childCount).matchType(i == e.length - 1 ? n.type : e[i + 1])) return r.copy(r.content.append(a.from(Ha(n, e, i + 1))));
	}
}
function Wa(e, t) {
	if (t == 0) return e;
	let n = e.content.replaceChild(e.childCount - 1, Wa(e.lastChild, t - 1)), r = e.contentMatchAt(e.childCount).fillBefore(a.empty, !0);
	return e.copy(n.append(r));
}
function Ga(e, t, n, r, i, o) {
	let s = t < 0 ? e.firstChild : e.lastChild, c = s.content;
	return e.childCount > 1 && (o = 0), i < r - 1 && (c = Ga(c, t, n, r, i + 1, o)), i >= n && (c = t < 0 ? s.contentMatchAt(0).fillBefore(c, o <= i).append(c) : c.append(s.contentMatchAt(s.childCount).fillBefore(a.empty, !0))), e.replaceChild(t < 0 ? 0 : e.childCount - 1, s.copy(c));
}
function Ka(e, t, n) {
	return t < e.openStart && (e = new d(Ga(e.content, -1, t, e.openStart, 0, e.openEnd), t, e.openEnd)), n < e.openEnd && (e = new d(Ga(e.content, 1, n, e.openEnd, 0, 0), e.openStart, n)), e;
}
var qa = {
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
}, Ja = null;
function Ya() {
	return Ja || (Ja = document.implementation.createHTMLDocument("title"));
}
var Xa = null;
function Za(e) {
	let t = window.trustedTypes;
	return t ? (Xa || (Xa = t.defaultPolicy || t.createPolicy("ProseMirrorClipboard", { createHTML: (e) => e })), Xa.createHTML(e)) : e;
}
function Qa(e) {
	let t = /^(\s*<meta [^>]*>)*/.exec(e);
	t && (e = e.slice(t[0].length));
	let n = Ya().createElement("div"), r = /<([a-z][^>\s]+)/i.exec(e), i;
	if ((i = r && qa[r[1].toLowerCase()]) && (e = i.map((e) => "<" + e + ">").join("") + e + i.map((e) => "</" + e + ">").reverse().join("")), n.innerHTML = Za(e), i) for (let e = 0; e < i.length; e++) n = n.querySelector(i[e]) || n;
	return n;
}
function $a(e) {
	let t = e.querySelectorAll(M ? "span:not([class]):not([style])" : "span.Apple-converted-space");
	for (let n = 0; n < t.length; n++) {
		let r = t[n];
		r.childNodes.length == 1 && r.textContent == "\xA0" && r.parentNode && r.parentNode.replaceChild(e.ownerDocument.createTextNode(" "), r);
	}
}
function eo(e, t) {
	if (!e.size) return e;
	let n = e.content.firstChild.type.schema, r;
	try {
		r = JSON.parse(t);
	} catch {
		return e;
	}
	let { content: i, openStart: o, openEnd: s } = e;
	for (let e = r.length - 2; e >= 0; e -= 2) {
		let t = n.nodes[r[e]];
		if (!t || t.hasRequiredAttrs()) break;
		i = a.from(t.create(r[e + 1], i)), o++, s++;
	}
	return new d(i, o, s);
}
var P = {}, F = {}, to = {
	touchstart: !0,
	touchmove: !0
}, no = class {
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
function ro(e) {
	for (let t in P) {
		let n = P[t];
		e.dom.addEventListener(t, e.input.eventHandlers[t] = (t) => {
			co(e, t) && !so(e, t) && (e.editable || !(t.type in F)) && n(e, t);
		}, to[t] ? { passive: !0 } : void 0);
	}
	N && e.dom.addEventListener("input", () => null), oo(e);
}
function io(e, t) {
	e.input.lastSelectionOrigin = t, e.input.lastSelectionTime = Date.now();
}
function ao(e) {
	e.domObserver.stop();
	for (let t in e.input.eventHandlers) e.dom.removeEventListener(t, e.input.eventHandlers[t]);
	clearTimeout(e.input.composingTimeout), clearTimeout(e.input.lastIOSEnterFallbackTimeout);
}
function oo(e) {
	e.someProp("handleDOMEvents", (t) => {
		for (let n in t) e.input.eventHandlers[n] || e.dom.addEventListener(n, e.input.eventHandlers[n] = (t) => so(e, t));
	});
}
function so(e, t) {
	return e.someProp("handleDOMEvents", (n) => {
		let r = n[t.type];
		return r ? r(e, t) || t.defaultPrevented : !1;
	});
}
function co(e, t) {
	if (!t.bubbles) return !0;
	if (t.defaultPrevented) return !1;
	for (let n = t.target; n != e.dom; n = n.parentNode) if (!n || n.nodeType == 11 || n.pmViewDesc && n.pmViewDesc.stopEvent(t)) return !1;
	return !0;
}
function lo(e, t) {
	!so(e, t) && P[t.type] && (e.editable || !(t.type in F)) && P[t.type](e, t);
}
F.keydown = (e, t) => {
	let n = t;
	if (e.input.shiftKey = n.keyCode == 16 || n.shiftKey, !wo(e, n) && (e.input.lastKeyCode = n.keyCode, e.input.lastKeyCodeTime = Date.now(), !(Jr && M && n.keyCode == 13))) {
		if (n.keyCode != 229 && e.domObserver.forceFlush(), Gr && n.keyCode == 13 && !n.ctrlKey && !n.altKey && !n.metaKey) {
			let t = Date.now();
			e.input.lastIOSEnter = t, e.input.lastIOSEnterFallbackTimeout = setTimeout(() => {
				e.input.lastIOSEnter == t && (e.someProp("handleKeyDown", (t) => t(e, jr(13, "Enter"))), e.input.lastIOSEnter = 0);
			}, 200);
		} else e.someProp("handleKeyDown", (t) => t(e, n)) || La(e, n) ? n.preventDefault() : io(e, "key");
	}
}, F.keyup = (e, t) => {
	t.keyCode == 16 && (e.input.shiftKey = !1);
}, F.keypress = (e, t) => {
	let n = t;
	if (wo(e, n) || !n.charCode || n.ctrlKey && !n.altKey || Kr && n.metaKey) return;
	if (e.someProp("handleKeyPress", (t) => t(e, n))) {
		n.preventDefault();
		return;
	}
	let r = e.state.selection;
	if (!(r instanceof D) || !r.$from.sameParent(r.$to)) {
		let t = String.fromCharCode(n.charCode), i = () => e.state.tr.insertText(t).scrollIntoView();
		!/[\r\n]/.test(t) && !e.someProp("handleTextInput", (n) => n(e, r.$from.pos, r.$to.pos, t, i)) && e.dispatch(i()), n.preventDefault();
	}
};
function uo(e) {
	return {
		left: e.clientX,
		top: e.clientY
	};
}
function fo(e, t) {
	let n = t.x - e.clientX, r = t.y - e.clientY;
	return n * n + r * r < 100;
}
function po(e, t, n, r, i) {
	if (r == -1) return !1;
	let a = e.state.doc.resolve(r);
	for (let r = a.depth + 1; r > 0; r--) if (e.someProp(t, (t) => r > a.depth ? t(e, n, a.nodeAfter, a.before(r), i, !0) : t(e, n, a.node(r), a.before(r), i, !1))) return !0;
	return !1;
}
function mo(e, t, n) {
	if (e.focused || e.focus(), e.state.selection.eq(t)) return;
	let r = e.state.tr.setSelection(t);
	n == "pointer" && r.setMeta("pointer", !0), e.dispatch(r);
}
function ho(e, t) {
	if (t == -1) return !1;
	let n = e.state.doc.resolve(t), r = n.nodeAfter;
	return r && r.isAtom && O.isSelectable(r) ? (mo(e, new O(n), "pointer"), !0) : !1;
}
function go(e, t) {
	if (t == -1) return !1;
	let n = e.state.selection, r, i;
	n instanceof O && (r = n.node);
	let a = e.state.doc.resolve(t);
	for (let e = a.depth + 1; e > 0; e--) {
		let t = e > a.depth ? a.nodeAfter : a.node(e);
		if (O.isSelectable(t)) {
			i = r && n.$from.depth > 0 && e >= n.$from.depth && a.before(n.$from.depth + 1) == n.$from.pos ? a.before(n.$from.depth) : a.before(e);
			break;
		}
	}
	return i != null && (mo(e, O.create(e.state.doc, i), "pointer"), !0);
}
function _o(e, t, n, r, i) {
	return po(e, "handleClickOn", t, n, r) || e.someProp("handleClick", (n) => n(e, t, r)) || (i ? go(e, n) : ho(e, n));
}
function vo(e, t, n, r) {
	return po(e, "handleDoubleClickOn", t, n, r) || e.someProp("handleDoubleClick", (n) => n(e, t, r));
}
function yo(e, t, n, r) {
	return po(e, "handleTripleClickOn", t, n, r) || e.someProp("handleTripleClick", (n) => n(e, t, r)) || bo(e, n, r);
}
function bo(e, t, n) {
	if (n.button != 0) return !1;
	let r = e.state.doc;
	if (t == -1) return r.inlineContent ? (mo(e, D.create(r, 0, r.content.size), "pointer"), !0) : !1;
	let i = r.resolve(t);
	for (let t = i.depth + 1; t > 0; t--) {
		let n = t > i.depth ? i.nodeAfter : i.node(t), a = i.before(t);
		if (n.inlineContent) mo(e, D.create(r, a + 1, a + 1 + n.content.size), "pointer");
		else if (O.isSelectable(n)) mo(e, O.create(r, a), "pointer");
		else continue;
		return !0;
	}
}
function xo(e) {
	return jo(e);
}
var So = Kr ? "metaKey" : "ctrlKey";
P.mousedown = (e, t) => {
	let n = t;
	e.input.shiftKey = n.shiftKey;
	let r = xo(e), i = Date.now(), a = "singleClick";
	i - e.input.lastClick.time < 500 && fo(n, e.input.lastClick) && !n[So] && e.input.lastClick.button == n.button && (e.input.lastClick.type == "singleClick" ? a = "doubleClick" : e.input.lastClick.type == "doubleClick" && (a = "tripleClick")), e.input.lastClick = {
		time: i,
		x: n.clientX,
		y: n.clientY,
		type: a,
		button: n.button
	};
	let o = e.posAtCoords(uo(n));
	o && (a == "singleClick" ? (e.input.mouseDown && e.input.mouseDown.done(), e.input.mouseDown = new Co(e, o, n, !!r)) : (a == "doubleClick" ? vo : yo)(e, o.pos, o.inside, n) ? n.preventDefault() : io(e, "pointer"));
};
var Co = class {
	constructor(e, t, n, r) {
		this.view = e, this.pos = t, this.event = n, this.flushed = r, this.delayedSelectionSync = !1, this.mightDrag = null, this.startDoc = e.state.doc, this.selectNode = !!n[So], this.allowDefault = n.shiftKey;
		let i, a;
		if (t.inside > -1) i = e.state.doc.nodeAt(t.inside), a = t.inside;
		else {
			let n = e.state.doc.resolve(t.pos);
			i = n.parent, a = n.depth ? n.before() : 0;
		}
		let o = r ? null : n.target, s = o ? e.docView.nearestDesc(o, !0) : null;
		this.target = s && s.nodeDOM.nodeType == 1 ? s.nodeDOM : null;
		let { selection: c } = e.state;
		(n.button == 0 && i.type.spec.draggable && i.type.spec.selectable !== !1 || c instanceof O && c.from <= a && c.to > a) && (this.mightDrag = {
			node: i,
			pos: a,
			addAttr: !(!this.target || this.target.draggable),
			setUneditable: !!(this.target && Hr && !this.target.hasAttribute("contentEditable"))
		}), this.target && this.mightDrag && (this.mightDrag.addAttr || this.mightDrag.setUneditable) && (this.view.domObserver.stop(), this.mightDrag.addAttr && (this.target.draggable = !0), this.mightDrag.setUneditable && setTimeout(() => {
			this.view.input.mouseDown == this && this.target.setAttribute("contentEditable", "false");
		}, 20), this.view.domObserver.start()), e.root.addEventListener("mouseup", this.up = this.up.bind(this)), e.root.addEventListener("mousemove", this.move = this.move.bind(this)), io(e, "pointer");
	}
	done() {
		this.view.root.removeEventListener("mouseup", this.up), this.view.root.removeEventListener("mousemove", this.move), this.mightDrag && this.target && (this.view.domObserver.stop(), this.mightDrag.addAttr && this.target.removeAttribute("draggable"), this.mightDrag.setUneditable && this.target.removeAttribute("contentEditable"), this.view.domObserver.start()), this.delayedSelectionSync && setTimeout(() => oa(this.view)), this.view.input.mouseDown = null;
	}
	up(e) {
		if (this.done(), !this.view.dom.contains(e.target)) return;
		let t = this.pos;
		this.view.state.doc != this.startDoc && (t = this.view.posAtCoords(uo(e))), this.updateAllowDefault(e), this.allowDefault || !t ? io(this.view, "pointer") : _o(this.view, t.pos, t.inside, e, this.selectNode) ? e.preventDefault() : e.button == 0 && (this.flushed || N && this.mightDrag && !this.mightDrag.node.isAtom || M && !this.view.state.selection.visible && Math.min(Math.abs(t.pos - this.view.state.selection.from), Math.abs(t.pos - this.view.state.selection.to)) <= 2) ? (mo(this.view, E.near(this.view.state.doc.resolve(t.pos)), "pointer"), e.preventDefault()) : io(this.view, "pointer");
	}
	move(e) {
		this.updateAllowDefault(e), io(this.view, "pointer"), e.buttons == 0 && this.done();
	}
	updateAllowDefault(e) {
		!this.allowDefault && (Math.abs(this.event.x - e.clientX) > 4 || Math.abs(this.event.y - e.clientY) > 4) && (this.allowDefault = !0);
	}
};
P.touchstart = (e) => {
	e.input.lastTouch = Date.now(), xo(e), io(e, "pointer");
}, P.touchmove = (e) => {
	e.input.lastTouch = Date.now(), io(e, "pointer");
}, P.contextmenu = (e) => xo(e);
function wo(e, t) {
	return e.composing ? !0 : N && Math.abs(t.timeStamp - e.input.compositionEndedAt) < 500 ? (e.input.compositionEndedAt = -2e8, !0) : !1;
}
var To = Jr ? 5e3 : -1;
F.compositionstart = F.compositionupdate = (e) => {
	if (!e.composing) {
		e.domObserver.flush();
		let { state: t } = e, n = t.selection.$to;
		if (t.selection instanceof D && (t.storedMarks || !n.textOffset && n.parentOffset && n.nodeBefore.marks.some((e) => e.type.spec.inclusive === !1) || M && qr && Eo(e))) e.markCursor = e.state.storedMarks || n.marks(), jo(e, !0), e.markCursor = null;
		else if (jo(e, !t.selection.empty), Hr && t.selection.empty && n.parentOffset && !n.textOffset && n.nodeBefore.marks.length) {
			let t = e.domSelectionRange();
			for (let n = t.focusNode, r = t.focusOffset; n && n.nodeType == 1 && r != 0;) {
				let t = r < 0 ? n.lastChild : n.childNodes[r - 1];
				if (!t) break;
				if (t.nodeType == 3) {
					let n = e.domSelection();
					n && n.collapse(t, t.nodeValue.length);
					break;
				}
				n = t, r = -1;
			}
		}
		e.input.composing = !0;
	}
	Do(e, To);
};
function Eo(e) {
	let { focusNode: t, focusOffset: n } = e.domSelectionRange();
	if (!t || t.nodeType != 1 || n >= t.childNodes.length) return !1;
	let r = t.childNodes[n];
	return r.nodeType == 1 && r.contentEditable == "false";
}
F.compositionend = (e, t) => {
	e.composing && (e.input.composing = !1, e.input.compositionEndedAt = t.timeStamp, e.input.compositionPendingChanges = e.domObserver.pendingRecords().length ? e.input.compositionID : 0, e.input.compositionNode = null, e.input.badSafariComposition ? e.domObserver.forceFlush() : e.input.compositionPendingChanges && Promise.resolve().then(() => e.domObserver.flush()), e.input.compositionID++, Do(e, 20));
};
function Do(e, t) {
	clearTimeout(e.input.composingTimeout), t > -1 && (e.input.composingTimeout = setTimeout(() => jo(e), t));
}
function Oo(e) {
	for (e.composing && (e.input.composing = !1, e.input.compositionEndedAt = Ao()); e.input.compositionNodes.length > 0;) e.input.compositionNodes.pop().markParentsDirty();
}
function ko(e) {
	let t = e.domSelectionRange();
	if (!t.focusNode) return null;
	let n = Er(t.focusNode, t.focusOffset), r = Dr(t.focusNode, t.focusOffset);
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
function Ao() {
	let e = document.createEvent("Event");
	return e.initEvent("event", !0, !0), e.timeStamp;
}
function jo(e, t = !1) {
	if (!(Jr && e.domObserver.flushingSoon >= 0)) {
		if (e.domObserver.forceFlush(), Oo(e), t || e.docView && e.docView.dirty) {
			let n = ia(e), r = e.state.selection;
			return n && !n.eq(r) ? e.dispatch(e.state.tr.setSelection(n)) : (e.markCursor || t) && !r.$from.node(r.$from.sharedDepth(r.to)).inlineContent ? e.dispatch(e.state.tr.deleteSelection()) : e.updateState(e.state), !0;
		}
		return !1;
	}
}
function Mo(e, t) {
	if (!e.dom.parentNode) return;
	let n = e.dom.parentNode.appendChild(document.createElement("div"));
	n.appendChild(t), n.style.cssText = "position: fixed; left: -10000px; top: 10px";
	let r = getSelection(), i = document.createRange();
	i.selectNodeContents(t), e.dom.blur(), r.removeAllRanges(), r.addRange(i), setTimeout(() => {
		n.parentNode && n.parentNode.removeChild(n), e.focus();
	}, 50);
}
var No = Br && Vr < 15 || Gr && Xr < 604;
P.copy = F.cut = (e, t) => {
	let n = t, r = e.state.selection, i = n.type == "cut";
	if (r.empty) return;
	let a = No ? null : n.clipboardData, { dom: o, text: s } = Ra(e, r.content());
	a ? (n.preventDefault(), a.clearData(), a.setData("text/html", o.innerHTML), a.setData("text/plain", s)) : Mo(e, o), i && e.dispatch(e.state.tr.deleteSelection().scrollIntoView().setMeta("uiEvent", "cut"));
};
function Po(e) {
	return e.openStart == 0 && e.openEnd == 0 && e.content.childCount == 1 ? e.content.firstChild : null;
}
function Fo(e, t) {
	if (!e.dom.parentNode) return;
	let n = e.input.shiftKey || e.state.selection.$from.parent.type.spec.code, r = e.dom.parentNode.appendChild(document.createElement(n ? "textarea" : "div"));
	n || (r.contentEditable = "true"), r.style.cssText = "position: fixed; left: -10000px; top: 10px", r.focus();
	let i = e.input.shiftKey && e.input.lastKeyCode != 45;
	setTimeout(() => {
		e.focus(), r.parentNode && r.parentNode.removeChild(r), n ? Io(e, r.value, null, i, t) : Io(e, r.textContent, r.innerHTML, i, t);
	}, 50);
}
function Io(e, t, n, r, i) {
	let a = za(e, t, n, r, e.state.selection.$from);
	if (e.someProp("handlePaste", (t) => t(e, i, a || d.empty))) return !0;
	if (!a) return !1;
	let o = Po(a), s = o ? e.state.tr.replaceSelectionWith(o, r) : e.state.tr.replaceSelection(a);
	return e.dispatch(s.scrollIntoView().setMeta("paste", !0).setMeta("uiEvent", "paste")), !0;
}
function Lo(e) {
	let t = e.getData("text/plain") || e.getData("Text");
	if (t) return t;
	let n = e.getData("text/uri-list");
	return n ? n.replace(/\r?\n/g, " ") : "";
}
F.paste = (e, t) => {
	let n = t;
	if (e.composing && !Jr) return;
	let r = No ? null : n.clipboardData, i = e.input.shiftKey && e.input.lastKeyCode != 45;
	r && Io(e, Lo(r), r.getData("text/html"), i, n) ? n.preventDefault() : Fo(e, n);
};
var Ro = class {
	constructor(e, t, n) {
		this.slice = e, this.move = t, this.node = n;
	}
}, zo = Kr ? "altKey" : "ctrlKey";
function Bo(e, t) {
	return e.someProp("dragCopies", (e) => !e(t)) ?? !t[zo];
}
P.dragstart = (e, t) => {
	let n = t, r = e.input.mouseDown;
	if (r && r.done(), !n.dataTransfer) return;
	let i = e.state.selection, a = i.empty ? null : e.posAtCoords(uo(n)), o;
	if (!(a && a.pos >= i.from && a.pos <= (i instanceof O ? i.to - 1 : i.to))) {
		if (r && r.mightDrag) o = O.create(e.state.doc, r.mightDrag.pos);
		else if (n.target && n.target.nodeType == 1) {
			let t = e.docView.nearestDesc(n.target, !0);
			t && t.node.type.spec.draggable && t != e.docView && (o = O.create(e.state.doc, t.posBefore));
		}
	}
	let { dom: s, text: c, slice: l } = Ra(e, (o || e.state.selection).content());
	(!n.dataTransfer.files.length || !M || Wr > 120) && n.dataTransfer.clearData(), n.dataTransfer.setData(No ? "Text" : "text/html", s.innerHTML), n.dataTransfer.effectAllowed = "copyMove", No || n.dataTransfer.setData("text/plain", c), e.dragging = new Ro(l, Bo(e, n), o);
}, P.dragend = (e) => {
	let t = e.dragging;
	window.setTimeout(() => {
		e.dragging == t && (e.dragging = null);
	}, 50);
}, F.dragover = F.dragenter = (e, t) => t.preventDefault(), F.drop = (e, t) => {
	try {
		Vo(e, t, e.dragging);
	} finally {
		e.dragging = null;
	}
};
function Vo(e, t, n) {
	if (!t.dataTransfer) return;
	let r = e.posAtCoords(uo(t));
	if (!r) return;
	let i = e.state.doc.resolve(r.pos), a = n && n.slice;
	a ? e.someProp("transformPasted", (t) => {
		a = t(a, e, !1);
	}) : a = za(e, Lo(t.dataTransfer), No ? null : t.dataTransfer.getData("text/html"), !1, i);
	let o = !!(n && Bo(e, t));
	if (e.someProp("handleDrop", (n) => n(e, t, a || d.empty, o))) {
		t.preventDefault();
		return;
	}
	if (!a) return;
	t.preventDefault();
	let s = a ? Bt(e.state.doc, i.pos, a) : i.pos;
	s ?? (s = i.pos);
	let c = e.state.tr;
	if (o) {
		let { node: e } = n;
		e ? e.replace(c) : c.deleteSelection();
	}
	let l = c.mapping.map(s), u = a.openStart == 0 && a.openEnd == 0 && a.content.childCount == 1, f = c.doc;
	if (u ? c.replaceRangeWith(l, l, a.content.firstChild) : c.replaceRange(l, l, a), c.doc.eq(f)) return;
	let p = c.doc.resolve(l);
	if (u && O.isSelectable(a.content.firstChild) && p.nodeAfter && p.nodeAfter.sameMarkup(a.content.firstChild)) c.setSelection(new O(p));
	else {
		let t = c.mapping.map(s);
		c.mapping.maps[c.mapping.maps.length - 1].forEach((e, n, r, i) => t = i), c.setSelection(ha(e, p, c.doc.resolve(t)));
	}
	e.focus(), e.dispatch(c.setMeta("uiEvent", "drop"));
}
P.focus = (e) => {
	e.input.lastFocus = Date.now(), e.focused || (e.domObserver.stop(), e.dom.classList.add("ProseMirror-focused"), e.domObserver.start(), e.focused = !0, setTimeout(() => {
		e.docView && e.hasFocus() && !e.domObserver.currentSelection.eq(e.domSelectionRange()) && oa(e);
	}, 20));
}, P.blur = (e, t) => {
	let n = t;
	e.focused && (e.domObserver.stop(), e.dom.classList.remove("ProseMirror-focused"), e.domObserver.start(), n.relatedTarget && e.dom.contains(n.relatedTarget) && e.domObserver.currentSelection.clear(), e.focused = !1);
}, P.beforeinput = (e, t) => {
	if (M && Jr && t.inputType == "deleteContentBackward") {
		e.domObserver.flushSoon();
		let { domChangeCount: t } = e.input;
		setTimeout(() => {
			if (e.input.domChangeCount != t || (e.dom.blur(), e.focus(), e.someProp("handleKeyDown", (t) => t(e, jr(8, "Backspace"))))) return;
			let { $cursor: n } = e.state.selection;
			n && n.pos > 0 && e.dispatch(e.state.tr.delete(n.pos - 1, n.pos).scrollIntoView());
		}, 50);
	}
};
for (let e in F) P[e] = F[e];
function Ho(e, t) {
	if (e == t) return !0;
	for (let n in e) if (e[n] !== t[n]) return !1;
	for (let n in t) if (!(n in e)) return !1;
	return !0;
}
var Uo = class e {
	constructor(e, t) {
		this.toDOM = e, this.spec = t || qo, this.side = this.spec.side || 0;
	}
	map(e, t, n, r) {
		let { pos: i, deleted: a } = e.mapResult(t.from + r, this.side < 0 ? -1 : 1);
		return a ? null : new I(i - n, i - n, this);
	}
	valid() {
		return !0;
	}
	eq(t) {
		return this == t || t instanceof e && (this.spec.key && this.spec.key == t.spec.key || this.toDOM == t.toDOM && Ho(this.spec, t.spec));
	}
	destroy(e) {
		this.spec.destroy && this.spec.destroy(e);
	}
}, Wo = class e {
	constructor(e, t) {
		this.attrs = e, this.spec = t || qo;
	}
	map(e, t, n, r) {
		let i = e.map(t.from + r, this.spec.inclusiveStart ? -1 : 1) - n, a = e.map(t.to + r, this.spec.inclusiveEnd ? 1 : -1) - n;
		return i >= a ? null : new I(i, a, this);
	}
	valid(e, t) {
		return t.from < t.to;
	}
	eq(t) {
		return this == t || t instanceof e && Ho(this.attrs, t.attrs) && Ho(this.spec, t.spec);
	}
	static is(t) {
		return t.type instanceof e;
	}
	destroy() {}
}, Go = class e {
	constructor(e, t) {
		this.attrs = e, this.spec = t || qo;
	}
	map(e, t, n, r) {
		let i = e.mapResult(t.from + r, 1);
		if (i.deleted) return null;
		let a = e.mapResult(t.to + r, -1);
		return a.deleted || a.pos <= i.pos ? null : new I(i.pos - n, a.pos - n, this);
	}
	valid(e, t) {
		let { index: n, offset: r } = e.content.findIndex(t.from), i;
		return r == t.from && !(i = e.child(n)).isText && r + i.nodeSize == t.to;
	}
	eq(t) {
		return this == t || t instanceof e && Ho(this.attrs, t.attrs) && Ho(this.spec, t.spec);
	}
	destroy() {}
}, I = class e {
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
		return new e(t, t, new Uo(n, r));
	}
	static inline(t, n, r, i) {
		return new e(t, n, new Wo(r, i));
	}
	static node(t, n, r, i) {
		return new e(t, n, new Go(r, i));
	}
	get spec() {
		return this.type.spec;
	}
	get inline() {
		return this.type instanceof Wo;
	}
	get widget() {
		return this.type instanceof Uo;
	}
}, Ko = [], qo = {}, L = class e {
	constructor(e, t) {
		this.local = e.length ? e : Ko, this.children = t.length ? t : Ko;
	}
	static create(e, t) {
		return t.length ? es(t, e, 0, qo) : R;
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
		return this == R || e.maps.length == 0 ? this : this.mapInner(e, t, 0, 0, n || qo);
	}
	mapInner(t, n, r, i, a) {
		let o;
		for (let e = 0; e < this.local.length; e++) {
			let s = this.local[e].map(t, r, i);
			s && s.type.valid(n, s) ? (o || (o = [])).push(s) : a.onRemove && a.onRemove(this.local[e].spec);
		}
		return this.children.length ? Yo(this.children, o || [], t, n, r, i, a) : o ? new e(o.sort(ts), Ko) : R;
	}
	add(t, n) {
		return n.length ? this == R ? e.create(t, n) : this.addInner(t, n, 0) : this;
	}
	addInner(t, n, r) {
		let i, a = 0;
		t.forEach((e, t) => {
			let o = t + r, s;
			if (s = Qo(n, e, o)) {
				for (i || (i = this.children.slice()); a < i.length && i[a] < t;) a += 3;
				i[a] == t ? i[a + 2] = i[a + 2].addInner(e, s, o + 1) : i.splice(a, 0, t, t + e.nodeSize, es(s, e, o + 1, qo)), a += 3;
			}
		});
		let o = Xo(a ? $o(n) : n, -r);
		for (let e = 0; e < o.length; e++) o[e].type.valid(t, o[e]) || o.splice(e--, 1);
		return new e(o.length ? this.local.concat(o).sort(ts) : this.local, i || this.children);
	}
	remove(e) {
		return e.length == 0 || this == R ? this : this.removeInner(e, 0);
	}
	removeInner(t, n) {
		let r = this.children, i = this.local;
		for (let e = 0; e < r.length; e += 3) {
			let i, a = r[e] + n, o = r[e + 1] + n;
			for (let e = 0, n; e < t.length; e++) (n = t[e]) && n.from > a && n.to < o && (t[e] = null, (i || (i = [])).push(n));
			if (!i) continue;
			r == this.children && (r = this.children.slice());
			let s = r[e + 2].removeInner(i, a + 1);
			s == R ? (r.splice(e, 3), e -= 3) : r[e + 2] = s;
		}
		if (i.length) {
			for (let e = 0, r; e < t.length; e++) if (r = t[e]) for (let e = 0; e < i.length; e++) i[e].eq(r, n) && (i == this.local && (i = this.local.slice()), i.splice(e--, 1));
		}
		return r == this.children && i == this.local ? this : i.length || r.length ? new e(i, r) : R;
	}
	forChild(t, n) {
		if (this == R) return this;
		if (n.isLeaf) return e.empty;
		let r, i;
		for (let e = 0; e < this.children.length; e += 3) if (this.children[e] >= t) {
			this.children[e] == t && (r = this.children[e + 2]);
			break;
		}
		let a = t + 1, o = a + n.content.size;
		for (let e = 0; e < this.local.length; e++) {
			let t = this.local[e];
			if (t.from < o && t.to > a && t.type instanceof Wo) {
				let e = Math.max(a, t.from) - a, n = Math.min(o, t.to) - a;
				e < n && (i || (i = [])).push(t.copy(e, n));
			}
		}
		if (i) {
			let t = new e(i.sort(ts), Ko);
			return r ? new Jo([t, r]) : t;
		}
		return r || R;
	}
	eq(t) {
		if (this == t) return !0;
		if (!(t instanceof e) || this.local.length != t.local.length || this.children.length != t.children.length) return !1;
		for (let e = 0; e < this.local.length; e++) if (!this.local[e].eq(t.local[e])) return !1;
		for (let e = 0; e < this.children.length; e += 3) if (this.children[e] != t.children[e] || this.children[e + 1] != t.children[e + 1] || !this.children[e + 2].eq(t.children[e + 2])) return !1;
		return !0;
	}
	locals(e) {
		return ns(this.localsInner(e));
	}
	localsInner(e) {
		if (this == R) return Ko;
		if (e.inlineContent || !this.local.some(Wo.is)) return this.local;
		let t = [];
		for (let e = 0; e < this.local.length; e++) this.local[e].type instanceof Wo || t.push(this.local[e]);
		return t;
	}
	forEachSet(e) {
		e(this);
	}
};
L.empty = new L([], []), L.removeOverlap = ns;
var R = L.empty, Jo = class e {
	constructor(e) {
		this.members = e;
	}
	map(t, n) {
		let r = this.members.map((e) => e.map(t, n, qo));
		return e.from(r);
	}
	forChild(t, n) {
		if (n.isLeaf) return L.empty;
		let r = [];
		for (let i = 0; i < this.members.length; i++) {
			let a = this.members[i].forChild(t, n);
			a != R && (a instanceof e ? r = r.concat(a.members) : r.push(a));
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
		return t ? ns(n ? t : t.sort(ts)) : Ko;
	}
	static from(t) {
		switch (t.length) {
			case 0: return R;
			case 1: return t[0];
			default: return new e(t.every((e) => e instanceof L) ? t : t.reduce((e, t) => e.concat(t instanceof L ? t : t.members), []));
		}
	}
	forEachSet(e) {
		for (let t = 0; t < this.members.length; t++) this.members[t].forEachSet(e);
	}
};
function Yo(e, t, n, r, i, a, o) {
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
			r == R ? (s[t + 1] = -2, c = !0) : (s[t] = u, s[t + 1] = d, s[t + 2] = r);
		} else c = !0;
	}
	if (c) {
		let c = es(Zo(s, e, t, n, i, a, o), r, 0, o);
		t = c.local;
		for (let e = 0; e < s.length; e += 3) s[e + 1] < 0 && (s.splice(e, 3), e -= 3);
		for (let e = 0, t = 0; e < c.children.length; e += 3) {
			let n = c.children[e];
			for (; t < s.length && s[t] < n;) t += 3;
			s.splice(t, 0, c.children[e], c.children[e + 1], c.children[e + 2]);
		}
	}
	return new L(t.sort(ts), s);
}
function Xo(e, t) {
	if (!t || !e.length) return e;
	let n = [];
	for (let r = 0; r < e.length; r++) {
		let i = e[r];
		n.push(new I(i.from + t, i.to + t, i.type));
	}
	return n;
}
function Zo(e, t, n, r, i, a, o) {
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
function Qo(e, t, n) {
	if (t.isLeaf) return null;
	let r = n + t.nodeSize, i = null;
	for (let t = 0, a; t < e.length; t++) (a = e[t]) && a.from > n && a.to < r && ((i || (i = [])).push(a), e[t] = null);
	return i;
}
function $o(e) {
	let t = [];
	for (let n = 0; n < e.length; n++) e[n] != null && t.push(e[n]);
	return t;
}
function es(e, t, n, r) {
	let i = [], a = !1;
	t.forEach((t, o) => {
		let s = Qo(e, t, o + n);
		if (s) {
			a = !0;
			let e = es(s, t, n + o + 1, r);
			e != R && i.push(o, o + t.nodeSize, e);
		}
	});
	let o = Xo(a ? $o(e) : e, -n).sort(ts);
	for (let e = 0; e < o.length; e++) o[e].type.valid(t, o[e]) || (r.onRemove && r.onRemove(o[e].spec), o.splice(e--, 1));
	return o.length || i.length ? new L(o, i) : R;
}
function ts(e, t) {
	return e.from - t.from || e.to - t.to;
}
function ns(e) {
	let t = e;
	for (let n = 0; n < t.length - 1; n++) {
		let r = t[n];
		if (r.from != r.to) for (let i = n + 1; i < t.length; i++) {
			let a = t[i];
			if (a.from == r.from) {
				a.to != r.to && (t == e && (t = e.slice()), t[i] = a.copy(a.from, r.to), rs(t, i + 1, a.copy(r.to, a.to)));
				continue;
			}
			a.from < r.to && (t == e && (t = e.slice()), t[n] = r.copy(r.from, a.from), rs(t, i, r.copy(a.from, r.to)));
			break;
		}
	}
	return t;
}
function rs(e, t, n) {
	for (; t < e.length && ts(n, e[t]) > 0;) t++;
	e.splice(t, 0, n);
}
function is(e) {
	let t = [];
	return e.someProp("decorations", (n) => {
		let r = n(e.state);
		r && r != R && t.push(r);
	}), e.cursorWrapper && t.push(L.create(e.state.doc, [e.cursorWrapper.deco])), Jo.from(t);
}
var as = {
	childList: !0,
	characterData: !0,
	characterDataOldValue: !0,
	attributes: !0,
	attributeOldValue: !0,
	subtree: !0
}, ss = Br && Vr <= 11, cs = class {
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
}, ls = class {
	constructor(e, t) {
		this.view = e, this.handleDOMChange = t, this.queue = [], this.flushingSoon = -1, this.observer = null, this.currentSelection = new cs(), this.onCharData = null, this.suppressingSelectionUpdates = !1, this.lastChangedTextNode = null, this.observer = window.MutationObserver && new window.MutationObserver((t) => {
			for (let e = 0; e < t.length; e++) this.queue.push(t[e]);
			Br && Vr <= 11 && t.some((e) => e.type == "childList" && e.removedNodes.length || e.type == "characterData" && e.oldValue.length > e.target.nodeValue.length) ? this.flushSoon() : N && e.composing && t.some((e) => e.type == "childList" && e.target.nodeName == "TR") ? (e.input.badSafariComposition = !0, this.flushSoon()) : this.flush();
		}), ss && (this.onCharData = (e) => {
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
		this.observer && (this.observer.takeRecords(), this.observer.observe(this.view.dom, as)), this.onCharData && this.view.dom.addEventListener("DOMCharacterDataModified", this.onCharData), this.connectSelection();
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
		if (ga(this.view)) {
			if (this.suppressingSelectionUpdates) return oa(this.view);
			if (Br && Vr <= 11 && !this.view.state.selection.empty) {
				let e = this.view.domSelectionRange();
				if (e.focusNode && Sr(e.focusNode, e.focusOffset, e.anchorNode, e.anchorOffset)) return this.flushSoon();
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
		for (let n = e.focusNode; n; n = vr(n)) t.add(n);
		for (let r = e.anchorNode; r; r = vr(r)) if (t.has(r)) {
			n = r;
			break;
		}
		let r = n && this.view.docView.nearestDesc(n);
		if (r && r.ignoreMutation({
			type: "selection",
			target: n.nodeType == 3 ? n.parentNode : n
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
		let n = e.domSelectionRange(), r = !this.suppressingSelectionUpdates && !this.currentSelection.eq(n) && ga(e) && !this.ignoreSelectionChange(n), i = -1, a = -1, o = !1, s = [];
		if (e.editable) for (let e = 0; e < t.length; e++) {
			let n = this.registerMutation(t[e], s);
			n && (i = i < 0 ? n.from : Math.min(n.from, i), a = a < 0 ? n.to : Math.max(n.to, a), n.typeOver && (o = !0));
		}
		if (Hr && s.length) {
			let t = s.filter((e) => e.nodeName == "BR");
			if (t.length == 2) {
				let [e, n] = t;
				e.parentNode && e.parentNode.parentNode == n.parentNode ? n.remove() : e.remove();
			} else {
				let { focusNode: n } = this.currentSelection;
				for (let r of t) {
					let t = r.parentNode;
					t && t.nodeName == "LI" && (!n || hs(e, n) != t) && r.remove();
				}
			}
		} else if ((M || N) && s.some((e) => e.nodeName == "BR") && (e.input.lastKeyCode == 8 || e.input.lastKeyCode == 46)) {
			for (let e of s) if (e.nodeName == "BR" && e.parentNode) {
				let t = e.nextSibling;
				t && t.nodeType == 1 && t.contentEditable == "false" && e.parentNode.removeChild(e);
			}
		}
		let c = null;
		i < 0 && r && e.input.lastFocus > Date.now() - 200 && Math.max(e.input.lastTouch, e.input.lastClick.time) < Date.now() - 300 && Ar(n) && (c = ia(e)) && c.eq(E.near(e.state.doc.resolve(0), 1)) ? (e.input.lastFocus = 0, oa(e), this.currentSelection.set(n), e.scrollToSelection()) : (i > -1 || r) && (i > -1 && (e.docView.markDirty(i, a), fs(e)), e.input.badSafariComposition && (e.input.badSafariComposition = !1, gs(e, s)), this.handleDOMChange(i, a, o, s), e.docView && e.docView.dirty ? e.updateState(e.state) : this.currentSelection.eq(n) || oa(e), this.currentSelection.set(n));
	}
	registerMutation(e, t) {
		if (t.indexOf(e.target) > -1) return null;
		let n = this.view.docView.nearestDesc(e.target);
		if (e.type == "attributes" && (n == this.view.docView || e.attributeName == "contenteditable" || e.attributeName == "style" && !e.oldValue && !e.target.getAttribute("style")) || !n || n.ignoreMutation(e)) return null;
		if (e.type == "childList") {
			for (let n = 0; n < e.addedNodes.length; n++) {
				let r = e.addedNodes[n];
				t.push(r), r.nodeType == 3 && (this.lastChangedTextNode = r);
			}
			if (n.contentDOM && n.contentDOM != n.dom && !n.contentDOM.contains(e.target)) return {
				from: n.posBefore,
				to: n.posAfter
			};
			let r = e.previousSibling, i = e.nextSibling;
			if (Br && Vr <= 11 && e.addedNodes.length) for (let t = 0; t < e.addedNodes.length; t++) {
				let { previousSibling: n, nextSibling: a } = e.addedNodes[t];
				(!n || Array.prototype.indexOf.call(e.addedNodes, n) < 0) && (r = n), (!a || Array.prototype.indexOf.call(e.addedNodes, a) < 0) && (i = a);
			}
			let a = r && r.parentNode == e.target ? j(r) + 1 : 0, o = n.localPosFromDOM(e.target, a, -1), s = i && i.parentNode == e.target ? j(i) : e.target.childNodes.length;
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
}, us = /* @__PURE__ */ new WeakMap(), ds = !1;
function fs(e) {
	if (!us.has(e) && (us.set(e, null), [
		"normal",
		"nowrap",
		"pre-line"
	].indexOf(getComputedStyle(e.dom).whiteSpace) !== -1)) {
		if (e.requiresGeckoHackNode = Hr, ds) return;
		console.warn("ProseMirror expects the CSS white-space property to be set, preferably to 'pre-wrap'. It is recommended to load style/prosemirror.css from the prosemirror-view package."), ds = !0;
	}
}
function ps(e, t) {
	let n = t.startContainer, r = t.startOffset, i = t.endContainer, a = t.endOffset, o = e.domAtPos(e.state.selection.anchor);
	return Sr(o.node, o.offset, i, a) && ([n, r, i, a] = [
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
function ms(e, t) {
	if (t.getComposedRanges) {
		let n = t.getComposedRanges(e.root)[0];
		if (n) return ps(e, n);
	}
	let n;
	function r(e) {
		e.preventDefault(), e.stopImmediatePropagation(), n = e.getTargetRanges()[0];
	}
	return e.dom.addEventListener("beforeinput", r, !0), document.execCommand("indent"), e.dom.removeEventListener("beforeinput", r, !0), n ? ps(e, n) : null;
}
function hs(e, t) {
	for (let n = t.parentNode; n && n != e.dom; n = n.parentNode) {
		let t = e.docView.nearestDesc(n, !0);
		if (t && t.node.isBlock) return n;
	}
	return null;
}
function gs(e, t) {
	let { focusNode: n, focusOffset: r } = e.domSelectionRange();
	for (let i of t) if (i.parentNode?.nodeName == "TR") {
		let t = i.nextSibling;
		for (; t && t.nodeName != "TD" && t.nodeName != "TH";) t = t.nextSibling;
		if (t) {
			let a = t;
			for (;;) {
				let e = a.firstChild;
				if (!e || e.nodeType != 1 || e.contentEditable == "false" || /^(BR|IMG)$/.test(e.nodeName)) break;
				a = e;
			}
			a.insertBefore(i, a.firstChild), n == i && e.domSelection().collapse(i, r);
		} else i.parentNode.removeChild(i);
	}
}
function _s(e, t, n) {
	let { node: r, fromOffset: i, toOffset: a, from: o, to: s } = e.docView.parseRange(t, n), c = e.domSelectionRange(), l, u = c.anchorNode;
	if (u && e.dom.contains(u.nodeType == 1 ? u : u.parentNode) && (l = [{
		node: u,
		offset: c.anchorOffset
	}], Ar(c) || l.push({
		node: c.focusNode,
		offset: c.focusOffset
	})), M && e.input.lastKeyCode === 8) for (let e = a; e > i; e--) {
		let t = r.childNodes[e - 1], n = t.pmViewDesc;
		if (t.nodeName == "BR" && !n) {
			a = e;
			break;
		}
		if (!n || n.size) break;
	}
	let d = e.state.doc, f = e.someProp("domParser") || Me.fromSchema(e.state.schema), p = d.resolve(o), m = null, h = f.parse(r, {
		topNode: p.parent,
		topMatch: p.parent.contentMatchAt(p.index()),
		topOpen: !0,
		from: i,
		to: a,
		preserveWhitespace: p.parent.type.whitespace != "pre" || "full",
		findPositions: l,
		ruleFromNode: vs,
		context: p
	});
	if (l && l[0].pos != null) {
		let e = l[0].pos, t = l[1] && l[1].pos;
		t ?? (t = e), m = {
			anchor: e + o,
			head: t + o
		};
	}
	return {
		doc: h,
		sel: m,
		from: o,
		to: s
	};
}
function vs(e) {
	let t = e.pmViewDesc;
	if (t) return t.parseRule();
	if (e.nodeName == "BR" && e.parentNode) {
		if (N && /^(ul|ol)$/i.test(e.parentNode.nodeName)) {
			let e = document.createElement("div");
			return e.appendChild(document.createElement("li")), { skip: e };
		}
		if (e.parentNode.lastChild == e || N && /^(tr|table)$/i.test(e.parentNode.nodeName)) return { ignore: !0 };
	} else if (e.nodeName == "IMG" && e.getAttribute("mark-placeholder")) return { ignore: !0 };
	return null;
}
var ys = /^(a|abbr|acronym|b|bd[io]|big|br|button|cite|code|data(list)?|del|dfn|em|i|img|ins|kbd|label|map|mark|meter|output|q|ruby|s|samp|small|span|strong|su[bp]|time|u|tt|var)$/i;
function bs(e, t, n, r, i) {
	let a = e.input.compositionPendingChanges || (e.composing ? e.input.compositionID : 0);
	if (e.input.compositionPendingChanges = 0, t < 0) {
		let t = e.input.lastSelectionTime > Date.now() - 50 ? e.input.lastSelectionOrigin : null, n = ia(e, t);
		if (n && !e.state.selection.eq(n)) {
			if (M && Jr && e.input.lastKeyCode === 13 && Date.now() - 100 < e.input.lastKeyCodeTime && e.someProp("handleKeyDown", (t) => t(e, jr(13, "Enter")))) return;
			let r = e.state.tr.setSelection(n);
			t == "pointer" ? r.setMeta("pointer", !0) : t == "key" && r.scrollIntoView(), a && r.setMeta("composition", a), e.dispatch(r);
		}
		return;
	}
	let o = e.state.doc.resolve(t), s = o.sharedDepth(n);
	t = o.before(s + 1), n = e.state.doc.resolve(n).after(s + 1);
	let c = e.state.selection, l = _s(e, t, n), u = e.state.doc, d = u.slice(l.from, l.to), f, p;
	e.input.lastKeyCode === 8 && Date.now() - 100 < e.input.lastKeyCodeTime ? (f = e.state.selection.to, p = "end") : (f = e.state.selection.from, p = "start"), e.input.lastKeyCode = null;
	let m = Ts(d.content, l.doc.content, l.from, f, p);
	if (m && e.input.domChangeCount++, (Gr && e.input.lastIOSEnter > Date.now() - 225 || Jr) && i.some((e) => e.nodeType == 1 && !ys.test(e.nodeName)) && (!m || m.endA >= m.endB) && e.someProp("handleKeyDown", (t) => t(e, jr(13, "Enter")))) {
		e.input.lastIOSEnter = 0;
		return;
	}
	if (!m) {
		if (r && c instanceof D && !c.empty && c.$head.sameParent(c.$anchor) && !e.composing && !(l.sel && l.sel.anchor != l.sel.head)) m = {
			start: c.from,
			endA: c.to,
			endB: c.to
		};
		else {
			if (l.sel) {
				let t = xs(e, e.state.doc, l.sel);
				if (t && !t.eq(e.state.selection)) {
					let n = e.state.tr.setSelection(t);
					a && n.setMeta("composition", a), e.dispatch(n);
				}
			}
			return;
		}
	}
	e.state.selection.from < e.state.selection.to && m.start == m.endB && e.state.selection instanceof D && (m.start > e.state.selection.from && m.start <= e.state.selection.from + 2 && e.state.selection.from >= l.from ? m.start = e.state.selection.from : m.endA < e.state.selection.to && m.endA >= e.state.selection.to - 2 && e.state.selection.to <= l.to && (m.endB += e.state.selection.to - m.endA, m.endA = e.state.selection.to)), Br && Vr <= 11 && m.endB == m.start + 1 && m.endA == m.start && m.start > l.from && l.doc.textBetween(m.start - l.from - 1, m.start - l.from + 1) == " \xA0" && (m.start--, m.endA--, m.endB--);
	let h = l.doc.resolveNoCache(m.start - l.from), g = l.doc.resolveNoCache(m.endB - l.from), _ = u.resolve(m.start), v = h.sameParent(g) && h.parent.inlineContent && _.end() >= m.endA;
	if ((Gr && e.input.lastIOSEnter > Date.now() - 225 && (!v || i.some((e) => e.nodeName == "DIV" || e.nodeName == "P")) || !v && h.pos < l.doc.content.size && (!h.sameParent(g) || !h.parent.inlineContent) && h.pos < g.pos && !/\S/.test(l.doc.textBetween(h.pos, g.pos, "", ""))) && e.someProp("handleKeyDown", (t) => t(e, jr(13, "Enter")))) {
		e.input.lastIOSEnter = 0;
		return;
	}
	if (e.state.selection.anchor > m.start && Cs(u, m.start, m.endA, h, g) && e.someProp("handleKeyDown", (t) => t(e, jr(8, "Backspace")))) {
		Jr && M && e.domObserver.suppressSelectionUpdates();
		return;
	}
	M && m.endB == m.start && (e.input.lastChromeDelete = Date.now()), Jr && !v && h.start() != g.start() && g.parentOffset == 0 && h.depth == g.depth && l.sel && l.sel.anchor == l.sel.head && l.sel.head == m.endA && (m.endB -= 2, g = l.doc.resolveNoCache(m.endB - l.from), setTimeout(() => {
		e.someProp("handleKeyDown", function(t) {
			return t(e, jr(13, "Enter"));
		});
	}, 20));
	let y = m.start, b = m.endA, x = (t) => {
		let n = t || e.state.tr.replace(y, b, l.doc.slice(m.start - l.from, m.endB - l.from));
		if (l.sel) {
			let t = xs(e, n.doc, l.sel);
			t && !(M && e.composing && t.empty && (m.start != m.endB || e.input.lastChromeDelete < Date.now() - 100) && (t.head == y || t.head == n.mapping.map(b) - 1) || Br && t.empty && t.head == y) && n.setSelection(t);
		}
		return a && n.setMeta("composition", a), n.scrollIntoView();
	}, S;
	if (v) {
		if (h.pos == g.pos) {
			Br && Vr <= 11 && h.parentOffset == 0 && (e.domObserver.suppressSelectionUpdates(), setTimeout(() => oa(e), 20));
			let t = x(e.state.tr.delete(y, b)), n = u.resolve(m.start).marksAcross(u.resolve(m.endA));
			n && t.ensureMarks(n), e.dispatch(t);
		} else if (m.endA == m.endB && (S = Ss(h.parent.content.cut(h.parentOffset, g.parentOffset), _.parent.content.cut(_.parentOffset, m.endA - _.start())))) {
			let t = x(e.state.tr);
			S.type == "add" ? t.addMark(y, b, S.mark) : t.removeMark(y, b, S.mark), e.dispatch(t);
		} else if (h.parent.child(h.index()).isText && h.index() == g.index() - +!g.textOffset) {
			let t = h.parent.textBetween(h.parentOffset, g.parentOffset), n = () => x(e.state.tr.insertText(t, y, b));
			e.someProp("handleTextInput", (r) => r(e, y, b, t, n)) || e.dispatch(n());
		} else e.dispatch(x());
	} else e.dispatch(x());
}
function xs(e, t, n) {
	return Math.max(n.anchor, n.head) > t.content.size ? null : ha(e, t.resolve(n.anchor), t.resolve(n.head));
}
function Ss(e, t) {
	let n = e.firstChild.marks, r = t.firstChild.marks, i = n, o = r, s, c, l;
	for (let e = 0; e < r.length; e++) i = r[e].removeFromSet(i);
	for (let e = 0; e < n.length; e++) o = n[e].removeFromSet(o);
	if (i.length == 1 && o.length == 0) c = i[0], s = "add", l = (e) => e.mark(c.addToSet(e.marks));
	else if (i.length == 0 && o.length == 1) c = o[0], s = "remove", l = (e) => e.mark(c.removeFromSet(e.marks));
	else return null;
	let u = [];
	for (let e = 0; e < t.childCount; e++) u.push(l(t.child(e)));
	if (a.from(u).eq(e)) return {
		mark: c,
		type: s
	};
}
function Cs(e, t, n, r, i) {
	if (n - t <= i.pos - r.pos || ws(r, !0, !1) < i.pos) return !1;
	let a = e.resolve(t);
	if (!r.parent.isTextblock) {
		let e = a.nodeAfter;
		return e != null && n == t + e.nodeSize;
	}
	if (a.parentOffset < a.parent.content.size || !a.parent.isTextblock) return !1;
	let o = e.resolve(ws(a, !0, !0));
	return !o.parent.isTextblock || o.pos > n || ws(o, !0, !1) < n ? !1 : r.parent.content.cut(r.parentOffset).eq(o.parent.content);
}
function ws(e, t, n) {
	let r = e.depth, i = t ? e.end() : e.pos;
	for (; r > 0 && (t || e.indexAfter(r) == e.node(r).childCount);) r--, i++, t = !1;
	if (n) {
		let t = e.node(r).maybeChild(e.indexAfter(r));
		for (; t && !t.isLeaf;) t = t.firstChild, i++;
	}
	return i;
}
function Ts(e, t, n, r, i) {
	let a = e.findDiffStart(t, n);
	if (a == null) return null;
	let { a: o, b: s } = e.findDiffEnd(t, n + e.size, n + t.size);
	if (i == "end") {
		let e = Math.max(0, a - Math.min(o, s));
		r -= o + e - a;
	}
	if (o < a && e.size < t.size) {
		let e = r <= a && r >= o ? a - r : 0;
		a -= e, a && a < t.size && Es(t.textBetween(a - 1, a + 1)) && (a += e ? 1 : -1), s = a + (s - o), o = a;
	} else if (s < a) {
		let t = r <= a && r >= s ? a - r : 0;
		a -= t, a && a < e.size && Es(e.textBetween(a - 1, a + 1)) && (a += t ? 1 : -1), o = a + (o - s), s = a;
	}
	return {
		start: a,
		endA: o,
		endB: s
	};
}
function Es(e) {
	if (e.length != 2) return !1;
	let t = e.charCodeAt(0), n = e.charCodeAt(1);
	return t >= 56320 && t <= 57343 && n >= 55296 && n <= 56319;
}
var Ds = class {
	constructor(e, t) {
		this._root = null, this.focused = !1, this.trackWrites = null, this.mounted = !1, this.markCursor = null, this.cursorWrapper = null, this.lastSelectedViewDesc = void 0, this.input = new no(), this.prevDirectPlugins = [], this.pluginViews = [], this.requiresGeckoHackNode = !1, this.dragging = null, this._props = t, this.state = t.state, this.directPlugins = t.plugins || [], this.directPlugins.forEach(Ps), this.dispatch = this.dispatch.bind(this), this.dom = e && e.mount || document.createElement("div"), e && (e.appendChild ? e.appendChild(this.dom) : typeof e == "function" ? e(this.dom) : e.mount && (this.mounted = !0)), this.editable = As(this), ks(this), this.nodeViews = Ms(this), this.docView = Ri(this.state.doc, Os(this), is(this), this.dom, this), this.domObserver = new ls(this, (e, t, n, r) => bs(this, e, t, n, r)), this.domObserver.start(), ro(this), this.updatePluginViews();
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
		e.handleDOMEvents != this._props.handleDOMEvents && oo(this);
		let t = this._props;
		this._props = e, e.plugins && (e.plugins.forEach(Ps), this.directPlugins = e.plugins), this.updateStateInner(e.state, t);
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
		e.storedMarks && this.composing && (Oo(this), i = !0), this.state = e;
		let a = n.plugins != e.plugins || this._props.plugins != t.plugins;
		if (a || this._props.plugins != t.plugins || this._props.nodeViews != t.nodeViews) {
			let e = Ms(this);
			Ns(e, this.nodeViews) && (this.nodeViews = e, r = !0);
		}
		(a || t.handleDOMEvents != this._props.handleDOMEvents) && oo(this), this.editable = As(this), ks(this);
		let o = is(this), s = Os(this), c = n.plugins != e.plugins && !n.doc.eq(e.doc) ? "reset" : e.scrollToSelection > n.scrollToSelection ? "to selection" : "preserve", l = r || !this.docView.matchesNode(e.doc, s, o);
		(l || !e.selection.eq(n.selection)) && (i = !0);
		let u = c == "preserve" && i && this.dom.style.overflowAnchor == null && ti(this);
		if (i) {
			this.domObserver.stop();
			let t = l && (Br || M) && !this.composing && !n.selection.empty && !e.selection.empty && js(n.selection, e.selection);
			if (l) {
				let n = M ? this.trackWrites = this.domSelectionRange().focusNode : null;
				this.composing && (this.input.compositionNode = ko(this)), (r || !this.docView.update(e.doc, s, o, this)) && (this.docView.updateOuterDeco(s), this.docView.destroy(), this.docView = Ri(e.doc, s, o, this.dom, this)), n && (!this.trackWrites || !this.dom.contains(this.trackWrites)) && (t = !0);
			}
			t || !(this.input.mouseDown && this.domObserver.currentSelection.eq(this.domSelectionRange()) && va(this)) ? oa(this, t) : (pa(this, e.selection), this.domObserver.setCurSelection()), this.domObserver.start();
		}
		this.updatePluginViews(n), this.dragging?.node && !n.doc.eq(e.doc) && this.updateDraggedNode(this.dragging, n), c == "reset" ? this.dom.scrollTop = 0 : c == "to selection" ? this.scrollToSelection() : u && ri(u);
	}
	scrollToSelection() {
		let e = this.domSelectionRange().focusNode;
		if (e && this.dom.contains(e.nodeType == 1 ? e : e.parentNode) && !this.someProp("handleScrollToSelection", (e) => e(this))) {
			if (this.state.selection instanceof O) {
				let t = this.docView.domAfterPos(this.state.selection.from);
				t.nodeType == 1 && ei(this, t.getBoundingClientRect(), e);
			} else ei(this, this.coordsAtPos(this.state.selection.head, 1), e);
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
		if (this.state.doc.nodeAt(n.from) == n.node) r = n.from;
		else {
			let e = n.from + (this.state.doc.content.size - t.doc.content.size);
			(e > 0 && this.state.doc.nodeAt(e)) == n.node && (r = e);
		}
		this.dragging = new Ro(e.slice, e.move, r < 0 ? void 0 : O.create(this.state.doc, r));
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
		if (Br) {
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
		this.domObserver.stop(), this.editable && oi(this.dom), oa(this), this.domObserver.start();
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
		return mi(this, e);
	}
	coordsAtPos(e, t = 1) {
		return vi(this, e, t);
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
		return Oi(this, t || this.state, e);
	}
	pasteHTML(e, t) {
		return Io(this, "", e, !1, t || new ClipboardEvent("paste"));
	}
	pasteText(e, t) {
		return Io(this, e, null, !0, t || new ClipboardEvent("paste"));
	}
	serializeForClipboard(e) {
		return Ra(this, e);
	}
	destroy() {
		this.docView && (ao(this), this.destroyPluginViews(), this.mounted ? (this.docView.update(this.state.doc, [], is(this), this), this.dom.textContent = "") : this.dom.parentNode && this.dom.parentNode.removeChild(this.dom), this.docView.destroy(), this.docView = null, xr());
	}
	get isDestroyed() {
		return this.docView == null;
	}
	dispatchEvent(e) {
		return lo(this, e);
	}
	domSelectionRange() {
		let e = this.domSelection();
		return e ? N && this.root.nodeType === 11 && Mr(this.dom.ownerDocument) == this.dom && ms(this, e) || e : {
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
Ds.prototype.dispatch = function(e) {
	let t = this._props.dispatchTransaction;
	t ? t.call(this, e) : this.updateState(this.state.apply(e));
};
function Os(e) {
	let t = Object.create(null);
	return t.class = "ProseMirror", t.contenteditable = String(e.editable), e.someProp("attributes", (n) => {
		if (typeof n == "function" && (n = n(e.state)), n) for (let e in n) e == "class" ? t.class += " " + n[e] : e == "style" ? t.style = (t.style ? t.style + ";" : "") + n[e] : !t[e] && e != "contenteditable" && e != "nodeName" && (t[e] = String(n[e]));
	}), t.translate || (t.translate = "no"), [I.node(0, e.state.doc.content.size, t)];
}
function ks(e) {
	if (e.markCursor) {
		let t = document.createElement("img");
		t.className = "ProseMirror-separator", t.setAttribute("mark-placeholder", "true"), t.setAttribute("alt", ""), e.cursorWrapper = {
			dom: t,
			deco: I.widget(e.state.selection.from, t, {
				raw: !0,
				marks: e.markCursor
			})
		};
	} else e.cursorWrapper = null;
}
function As(e) {
	return !e.someProp("editable", (t) => t(e.state) === !1);
}
function js(e, t) {
	let n = Math.min(e.$anchor.sharedDepth(e.head), t.$anchor.sharedDepth(t.head));
	return e.$anchor.start(n) != t.$anchor.start(n);
}
function Ms(e) {
	let t = Object.create(null);
	function n(e) {
		for (let n in e) Object.prototype.hasOwnProperty.call(t, n) || (t[n] = e[n]);
	}
	return e.someProp("nodeViews", n), e.someProp("markViews", n), t;
}
function Ns(e, t) {
	let n = 0, r = 0;
	for (let r in e) {
		if (e[r] != t[r]) return !0;
		n++;
	}
	for (let e in t) r++;
	return n != r;
}
function Ps(e) {
	if (e.spec.state || e.spec.filterTransaction || e.spec.appendTransaction) throw RangeError("Plugins passed directly to the view must not have a state component");
}
for (var Fs = {
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
}, Is = {
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
}, Ls = typeof navigator < "u" && /Mac/.test(navigator.platform), Rs = typeof navigator < "u" && /MSIE \d|Trident\/(?:[7-9]|\d{2,})\..*rv:(\d+)/.exec(navigator.userAgent), z = 0; z < 10; z++) Fs[48 + z] = Fs[96 + z] = String(z);
for (var z = 1; z <= 24; z++) Fs[z + 111] = "F" + z;
for (var z = 65; z <= 90; z++) Fs[z] = String.fromCharCode(z + 32), Is[z] = String.fromCharCode(z);
for (var zs in Fs) Is.hasOwnProperty(zs) || (Is[zs] = Fs[zs]);
function Bs(e) {
	var t = !(Ls && e.metaKey && e.shiftKey && !e.ctrlKey && !e.altKey || Rs && e.shiftKey && e.key && e.key.length == 1 || e.key == "Unidentified") && e.key || (e.shiftKey ? Is : Fs)[e.keyCode] || e.key || "Unidentified";
	return t == "Esc" && (t = "Escape"), t == "Del" && (t = "Delete"), t == "Left" && (t = "ArrowLeft"), t == "Up" && (t = "ArrowUp"), t == "Right" && (t = "ArrowRight"), t == "Down" && (t = "ArrowDown"), t;
}
//#endregion
//#region node_modules/prosemirror-keymap/dist/index.js
var Vs = typeof navigator < "u" && /Mac|iP(hone|[oa]d)/.test(navigator.platform);
function Hs(e) {
	let t = e.split(/-(?!$)/), n = t[t.length - 1];
	n == "Space" && (n = " ");
	let r, i, a, o;
	for (let e = 0; e < t.length - 1; e++) {
		let n = t[e];
		if (/^(cmd|meta|m)$/i.test(n)) o = !0;
		else if (/^a(lt)?$/i.test(n)) r = !0;
		else if (/^(c|ctrl|control)$/i.test(n)) i = !0;
		else if (/^s(hift)?$/i.test(n)) a = !0;
		else if (/^mod$/i.test(n)) Vs ? o = !0 : i = !0;
		else throw Error("Unrecognized modifier name: " + n);
	}
	return r && (n = "Alt-" + n), i && (n = "Ctrl-" + n), o && (n = "Meta-" + n), a && (n = "Shift-" + n), n;
}
function Us(e) {
	let t = Object.create(null);
	for (let n in e) t[Hs(n)] = e[n];
	return t;
}
function Ws(e, t, n = !0) {
	return t.altKey && (e = "Alt-" + e), t.ctrlKey && (e = "Ctrl-" + e), t.metaKey && (e = "Meta-" + e), n && t.shiftKey && (e = "Shift-" + e), e;
}
function Gs(e) {
	return new k({ props: { handleKeyDown: Ks(e) } });
}
function Ks(e) {
	let t = Us(e);
	return function(e, n) {
		let r = Bs(n), i, a = t[Ws(r, n)];
		if (a && a(e.state, e.dispatch, e)) return !0;
		if (r.length == 1 && r != " ") {
			if (n.shiftKey) {
				let i = t[Ws(r, n, !1)];
				if (i && i(e.state, e.dispatch, e)) return !0;
			}
			if ((n.shiftKey || n.altKey || n.metaKey || r.charCodeAt(0) > 127) && (i = Fs[n.keyCode]) && i != r) {
				let r = t[Ws(i, n)];
				if (r && r(e.state, e.dispatch, e)) return !0;
			}
		}
		return !1;
	};
}
//#endregion
//#region node_modules/@tiptap/core/dist/index.js
function qs(e) {
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
var Js = class e {
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
			state: qs({
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
}, Ys = () => ({ editor: e, view: t }) => (requestAnimationFrame(() => {
	if (!e.isDestroyed) {
		var n;
		t.dom.blur(), (n = window) == null || (n = n.getSelection()) == null || n.removeAllRanges();
	}
}), !0), Xs = (e = !0) => ({ commands: t }) => t.setContent("", { emitUpdate: e }), Zs = () => ({ state: e, tr: t, dispatch: n }) => {
	let { selection: r } = t, { ranges: i } = r;
	return n && i.forEach(({ $from: n, $to: r }) => {
		e.doc.nodesBetween(n.pos, r.pos, (e, n) => {
			if (e.type.isText) return;
			let { doc: r, mapping: i } = t, a = r.resolve(i.map(n)), o = r.resolve(i.map(n + e.nodeSize)), s = a.blockRange(o);
			if (!s) return;
			let c = Ct(s);
			if (e.type.isTextblock) {
				let { defaultType: e } = a.parent.contentMatchAt(a.index());
				t.setNodeMarkup(s.start, e);
			}
			(c || c === 0) && t.lift(s, c);
		});
	}), !0;
}, Qs = (e) => (t) => e(t), $s = () => ({ state: e, dispatch: t }) => qn(e, t), ec = (e, t) => ({ editor: n, tr: r }) => {
	let { state: i } = n, a = i.doc.slice(e.from, e.to);
	r.deleteRange(e.from, e.to);
	let o = r.mapping.map(t);
	return r.insert(o, a.content), r.setSelection(new D(r.doc.resolve(Math.max(o - 1, 0)))), !0;
}, tc = () => ({ tr: e, dispatch: t }) => {
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
function B(e, t) {
	if (typeof e == "string") {
		if (!t.nodes[e]) throw Error(`There is no node type named '${e}'. Maybe you forgot to add the extension?`);
		return t.nodes[e];
	}
	return e;
}
var nc = (e) => ({ tr: t, state: n, dispatch: r }) => {
	let i = B(e, n.schema), a = t.selection.$anchor;
	for (let e = a.depth; e > 0; --e) if (a.node(e).type === i) {
		if (r) {
			let n = a.before(e), r = a.after(e);
			t.delete(n, r).scrollIntoView();
		}
		return !0;
	}
	return !1;
}, rc = (e) => ({ tr: t, dispatch: n }) => {
	let { from: r, to: i } = e;
	return n && t.delete(r, i), !0;
}, ic = (e) => e.content ? /^text(\*|\+)/.test(e.content) : !1, ac = (e, t, n) => {
	if (!e.parent.isInline || n === "left" && e.pos > e.start() || n === "right" && e.pos < e.end()) return e.pos;
	let r = t.nodes[e.parent.type.name].spec;
	return ic(r) ? n === "left" ? e.start() - 1 : e.end() + 1 : e.pos;
}, oc = (e, t, n) => ({
	from: ac(e, n, "left"),
	to: ac(t, n, "right")
}), sc = () => ({ state: e, dispatch: t }) => {
	if (e.selection.empty) return !1;
	if (t) {
		let n = e.tr, { ranges: r } = e.selection, i = n.steps.length;
		r.forEach((t) => {
			let r = n.mapping.slice(i), { from: a, to: o } = oc(n.doc.resolve(r.map(t.$from.pos)), n.doc.resolve(r.map(t.$to.pos)), e.schema);
			n.deleteRange(a, o);
		}), n.selection.empty || n.setSelection(D.near(n.doc.resolve(n.selection.from))), n.scrollIntoView(), t(n);
	}
	return !0;
}, cc = () => ({ commands: e }) => e.keyboardShortcut("Enter"), lc = () => ({ state: e, dispatch: t }) => Kn(e, t);
function uc(e) {
	return Object.prototype.toString.call(e) === "[object RegExp]";
}
function dc(e, t, n = { strict: !0 }) {
	let r = Object.keys(t);
	return !r.length || r.every((r) => n.strict ? t[r] === e[r] : uc(t[r]) ? t[r].test(e[r]) : t[r] === e[r]);
}
function fc(e, t, n = {}) {
	return e.find((e) => e.type === t && dc(Object.fromEntries(Object.keys(n).map((t) => [t, e.attrs[t]])), n));
}
function pc(e, t, n = {}) {
	return !!fc(e, t, n);
}
function mc(e, t, n) {
	if (!e || !t) return;
	let r = e.parent.childAfter(e.parentOffset);
	if ((!r.node || !r.node.marks.some((e) => e.type === t)) && (r = e.parent.childBefore(e.parentOffset)), !r.node || !r.node.marks.some((e) => e.type === t)) return;
	if (!n) {
		let e = r.node.marks.find((e) => e.type === t);
		e && (n = e.attrs);
	}
	if (!fc([...r.node.marks], t, n)) return;
	let i = r.index, a = e.start() + r.offset, o = i + 1, s = a + r.node.nodeSize;
	for (; i > 0 && pc([...e.parent.child(i - 1).marks], t, n);) --i, a -= e.parent.child(i).nodeSize;
	for (; o < e.parent.childCount && pc([...e.parent.child(o).marks], t, n);) s += e.parent.child(o).nodeSize, o += 1;
	return {
		from: a,
		to: s
	};
}
function hc(e, t) {
	if (typeof e == "string") {
		if (!t.marks[e]) throw Error(`There is no mark type named '${e}'. Maybe you forgot to add the extension?`);
		return t.marks[e];
	}
	return e;
}
var gc = (e, t) => ({ tr: n, state: r, dispatch: i }) => {
	let a = hc(e, r.schema), { doc: o, selection: s } = n, { $from: c, from: l, to: u } = s;
	if (i) {
		let e = mc(c, a, t);
		if (e && e.from <= l && e.to >= u) {
			let t = D.create(o, e.from, e.to);
			n.setSelection(t);
		}
	}
	return !0;
}, _c = (e) => (t) => {
	let n = typeof e == "function" ? e(t) : e;
	for (let e = 0; e < n.length; e += 1) if (n[e](t)) return !0;
	return !1;
};
function vc(e) {
	return e instanceof D;
}
function yc(e = 0, t = 0, n = 0) {
	return Math.min(Math.max(e, t), n);
}
function bc(e, t = null) {
	if (!t) return null;
	let n = E.atStart(e), r = E.atEnd(e);
	if (t === "start" || t === !0) return n;
	if (t === "end") return r;
	let i = n.from, a = r.to;
	return t === "all" ? D.create(e, yc(0, i, a), yc(e.content.size, i, a)) : D.create(e, yc(t, i, a), yc(t, i, a));
}
function xc() {
	return ["Android"].includes(navigator.platform) || /android/i.test(navigator.userAgent);
}
function Sc() {
	return [
		"iPad Simulator",
		"iPhone Simulator",
		"iPod Simulator",
		"iPad",
		"iPhone",
		"iPod"
	].includes(navigator.platform) || navigator.userAgent.includes("Mac") && "ontouchend" in document;
}
function Cc() {
	return typeof navigator < "u" && /^((?!chrome|android).)*safari/i.test(navigator.userAgent);
}
var wc = (e = null, t = {}) => ({ editor: n, view: r, tr: i, dispatch: a }) => {
	t = {
		scrollIntoView: !0,
		...t
	};
	let o = () => {
		(Sc() || xc()) && r.dom.focus(), Cc() && !Sc() && !xc() && r.dom.focus({ preventScroll: !0 }), requestAnimationFrame(() => {
			n.isDestroyed || (r.focus(), t?.scrollIntoView && n.commands.scrollIntoView());
		});
	};
	try {
		if (r.hasFocus() && e === null || e === !1) return !0;
	} catch {
		return !1;
	}
	if (a && e === null && !vc(n.state.selection)) return o(), !0;
	let s = bc(i.doc, e) || n.state.selection, c = n.state.selection.eq(s);
	return a && (c || i.setSelection(s), c && i.storedMarks && i.setStoredMarks(i.storedMarks), o()), !0;
}, Tc = (e, t) => (n) => e.every((e, r) => t(e, {
	...n,
	index: r
})), Ec = (e, t) => ({ tr: n, commands: r }) => r.insertContentAt({
	from: n.selection.from,
	to: n.selection.to
}, e, t), Dc = (e) => {
	let t = e.childNodes;
	for (let n = t.length - 1; n >= 0; --n) {
		let r = t[n];
		r.nodeType === 3 && r.nodeValue && /^(\n\s\s|\n)$/.test(r.nodeValue) ? e.removeChild(r) : r.nodeType === 1 && Dc(r);
	}
	return e;
};
function Oc(e) {
	if (typeof window > "u") throw Error("[tiptap error]: there is no window object available, so this function cannot be used");
	let t = `<body>${e}</body>`, n = new window.DOMParser().parseFromString(t, "text/html").body;
	return Dc(n);
}
function kc(e) {
	return typeof e?.nodesBetween == "function";
}
function Ac(e, t, n) {
	if (kc(e)) return e;
	let r = typeof e == "object" && !!e;
	n = {
		slice: !0,
		parseOptions: {},
		...n
	};
	let i = typeof e == "string";
	if (r) try {
		if (Array.isArray(e) && e.length > 0) return a.fromArray(e.map((e) => t.nodeFromJSON(e)));
		let r = t.nodeFromJSON(e);
		return n.errorOnInvalidContent && r.check(), r;
	} catch (r) {
		if (n.errorOnInvalidContent) throw Error("[tiptap error]: Invalid JSON content", { cause: r });
		return console.warn("[tiptap warn]: Invalid content.", "Passed value:", e, "Error:", r), Ac("", t, n);
	}
	if (i) {
		if (n.errorOnInvalidContent) {
			let r = !1, i = "", a = new Oe({
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
			if (n.slice ? Me.fromSchema(a).parseSlice(Oc(e), n.parseOptions) : Me.fromSchema(a).parse(Oc(e), n.parseOptions), n.errorOnInvalidContent && r) throw Error("[tiptap error]: Invalid HTML content", { cause: /* @__PURE__ */ Error(`Invalid element found: ${i}`) });
		}
		let r = Me.fromSchema(t);
		return n.slice ? r.parseSlice(Oc(e), n.parseOptions).content : r.parse(Oc(e), n.parseOptions);
	}
	return Ac("", t, n);
}
function jc(e) {
	return !("type" in e);
}
function Mc(e, t, n) {
	let r = e.steps.length - 1;
	if (r < t) return;
	let i = e.steps[r];
	if (!(i instanceof gt || i instanceof _t)) return;
	let a = e.mapping.maps[r], o = 0;
	a.forEach((e, t, n, r) => {
		o === 0 && (o = r);
	}), e.setSelection(E.near(e.doc.resolve(o), n));
}
var Nc = (e, t, n) => ({ tr: r, dispatch: i, editor: o }) => {
	if (i) {
		n = {
			parseOptions: o.options.parseOptions,
			updateSelection: !0,
			applyInputRules: !1,
			applyPasteRules: !1,
			...n
		};
		let i, s = (e) => {
			o.emit("contentError", {
				editor: o,
				error: e,
				disableCollaboration: () => {
					"collaboration" in o.storage && typeof o.storage.collaboration == "object" && o.storage.collaboration && (o.storage.collaboration.isDisabled = !0);
				}
			});
		}, c = {
			preserveWhitespace: "full",
			...n.parseOptions
		};
		if (!n.errorOnInvalidContent && !o.options.enableContentCheck && o.options.emitContentError) try {
			Ac(t, o.schema, {
				parseOptions: c,
				errorOnInvalidContent: !0
			});
		} catch (e) {
			s(e);
		}
		try {
			i = Ac(t, o.schema, {
				parseOptions: c,
				errorOnInvalidContent: n.errorOnInvalidContent ?? o.options.enableContentCheck
			});
		} catch (e) {
			return s(e), !1;
		}
		let { from: l, to: u } = typeof e == "number" ? {
			from: e,
			to: e
		} : {
			from: e.from,
			to: e.to
		}, d = !0, f = !0, p = jc(i) ? i.content : [i];
		if (p.forEach((e) => {
			e.check(), d = d ? e.isText && e.marks.length === 0 : !1, f = f ? e.isBlock : !1;
		}), l === u && f) {
			let { parent: e } = r.doc.resolve(l);
			e.isTextblock && !e.type.spec.code && !e.childCount && (--l, u += 1);
		}
		let m;
		if (d) m = Array.isArray(t) ? t.map((e) => e.text || "").join("") : kc(t) ? p.map((e) => e.text ?? "").join("") : typeof t == "object" && t && t.text ? t.text : t, r.insertText(m, l, u);
		else {
			m = a.from(p);
			let e = r.doc.resolve(l), t = e.node(), n = e.parentOffset === 0, i = t.isText || t.isTextblock, o = t.content.size > 0;
			n && i && o && f && (l = Math.max(0, l - 1)), r.replaceWith(l, u, p);
		}
		n.updateSelection && Mc(r, r.steps.length - 1, -1), n.applyInputRules && r.setMeta("applyInputRules", {
			from: l,
			text: m
		}), n.applyPasteRules && r.setMeta("applyPasteRules", {
			from: l,
			text: m
		});
	}
	return !0;
};
function Pc(e) {
	for (let t = 0; t < e.edgeCount; t += 1) {
		let { type: n } = e.edge(t);
		if (n.isTextblock && !n.hasRequiredAttrs()) return n;
	}
	return null;
}
var Fc = (e = {}) => ({ tr: t, dispatch: n, editor: r }) => {
	let { pos: i, attrs: a, content: o, updateSelection: s = !0 } = e, c;
	c = typeof i == "number" ? t.doc.resolve(i) : i || t.selection.$from;
	let l = Pc(c.parent.contentMatchAt(c.index()));
	if (!l) return !1;
	let u = Object.keys(l.spec.attrs || {}), d = a ? Object.fromEntries(Object.entries(a).filter(([e]) => u.includes(e))) : {}, f;
	if (o) {
		let e = Ac(o, r.schema);
		f = l.createAndFill(d, e);
	} else f = l.createAndFill(d);
	return f ? (n && (t.insert(c.pos, f), s && Mc(t, t.steps.length - 1, -1)), !0) : !1;
}, Ic = () => ({ state: e, dispatch: t }) => Vn(e, t), Lc = () => ({ state: e, dispatch: t }) => Hn(e, t), Rc = () => ({ state: e, dispatch: t }) => An(e, t), zc = () => ({ state: e, dispatch: t }) => Rn(e, t), Bc = () => ({ state: e, dispatch: t, tr: n }) => {
	try {
		let r = Lt(e.doc, e.selection.$from.pos, -1);
		return r != null && (n.join(r, 2), t && t(n), !0);
	} catch {
		return !1;
	}
}, Vc = () => ({ state: e, dispatch: t, tr: n }) => {
	try {
		let r = Lt(e.doc, e.selection.$from.pos, 1);
		return r != null && (n.join(r, 2), t && t(n), !0);
	} catch {
		return !1;
	}
}, Hc = () => ({ state: e, dispatch: t }) => jn(e, t), Uc = () => ({ state: e, dispatch: t }) => Mn(e, t);
function Wc() {
	return typeof navigator < "u" && /Mac/.test(navigator.platform);
}
function Gc(e) {
	let t = e.split(/-(?!$)/), n = t[t.length - 1];
	n === "Space" && (n = " ");
	let r, i, a, o;
	for (let e = 0; e < t.length - 1; e += 1) {
		let n = t[e];
		if (/^(cmd|meta|m)$/i.test(n)) o = !0;
		else if (/^a(lt)?$/i.test(n)) r = !0;
		else if (/^(c|ctrl|control)$/i.test(n)) i = !0;
		else if (/^s(hift)?$/i.test(n)) a = !0;
		else if (/^mod$/i.test(n)) Sc() || Wc() ? o = !0 : i = !0;
		else throw Error(`Unrecognized modifier name: ${n}`);
	}
	return r && (n = `Alt-${n}`), i && (n = `Ctrl-${n}`), o && (n = `Meta-${n}`), a && (n = `Shift-${n}`), n;
}
var Kc = (e) => ({ editor: t, view: n, tr: r, dispatch: i }) => {
	let a = Gc(e).split(/-(?!$)/), o = a.find((e) => ![
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
function qc(e, t, n = {}) {
	let { from: r, to: i, empty: a } = e.selection, o = t ? B(t, e.schema) : null, s = [];
	e.doc.nodesBetween(r, i, (e, t) => {
		if (e.isText) return;
		let n = Math.max(r, t), a = Math.min(i, t + e.nodeSize);
		s.push({
			node: e,
			from: n,
			to: a
		});
	});
	let c = i - r, l = s.filter((e) => !o || o.name === e.node.type.name).filter((e) => dc(e.node.attrs, n, { strict: !1 }));
	return a ? !!l.length : l.reduce((e, t) => e + t.to - t.from, 0) >= c;
}
var Jc = (e, t = {}) => ({ state: n, dispatch: r }) => qc(n, B(e, n.schema), t) ? Un(n, r) : !1, Yc = () => ({ state: e, dispatch: t }) => Jn(e, t), Xc = (e) => ({ state: t, dispatch: n }) => mr(B(e, t.schema))(t, n), Zc = () => ({ state: e, dispatch: t }) => Wn(e, t);
function Qc(e, t) {
	return t.nodes[e] ? "node" : t.marks[e] ? "mark" : null;
}
function $c(e, t) {
	let n = typeof t == "string" ? [t] : t;
	return Object.keys(e).reduce((t, r) => (n.includes(r) || (t[r] = e[r]), t), {});
}
var el = (e, t) => ({ tr: n, state: r, dispatch: i }) => {
	let a = null, o = null, s = Qc(typeof e == "string" ? e : e.name, r.schema);
	if (!s) return !1;
	s === "node" && (a = B(e, r.schema)), s === "mark" && (o = hc(e, r.schema));
	let c = !1;
	return n.selection.ranges.forEach((e) => {
		r.doc.nodesBetween(e.$from.pos, e.$to.pos, (e, r) => {
			a && a === e.type && (c = !0, i && n.setNodeMarkup(r, void 0, $c(e.attrs, t))), o && e.marks.length && e.marks.forEach((a) => {
				o === a.type && (c = !0, i && n.addMark(r, r + e.nodeSize, o.create($c(a.attrs, t))));
			});
		});
	}), c;
}, tl = () => ({ tr: e, dispatch: t }) => (t && e.scrollIntoView(), !0), nl = () => ({ tr: e, dispatch: t }) => {
	if (t) {
		let t = new fn(e.doc);
		e.setSelection(t);
	}
	return !0;
}, rl = () => ({ state: e, dispatch: t }) => Fn(e, t), il = () => ({ state: e, dispatch: t }) => zn(e, t), al = () => ({ state: e, dispatch: t }) => Zn(e, t), ol = () => ({ state: e, dispatch: t }) => rr(e, t), sl = () => ({ state: e, dispatch: t }) => nr(e, t);
function cl(e, t, n = {}, r = {}) {
	return Ac(e, t, {
		slice: !1,
		parseOptions: n,
		errorOnInvalidContent: r.errorOnInvalidContent
	});
}
var ll = (e, { errorOnInvalidContent: t, emitUpdate: n = !0, parseOptions: r = {} } = {}) => ({ editor: i, tr: a, dispatch: o, commands: s }) => {
	let { doc: c } = a;
	if (r.preserveWhitespace !== "full") {
		let s = cl(e, i.schema, r, { errorOnInvalidContent: t ?? i.options.enableContentCheck });
		if (o) {
			let e = jc(s) ? s.content : [s];
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
function ul(e, t) {
	let n = hc(t, e.schema), { from: r, to: i, empty: a } = e.selection, o = [];
	a ? (e.storedMarks && o.push(...e.storedMarks), o.push(...e.selection.$head.marks())) : e.doc.nodesBetween(r, i, (e) => {
		o.push(...e.marks);
	});
	let s = o.find((e) => e.type.name === n.name);
	return s ? { ...s.attrs } : {};
}
function dl(e, t) {
	let n = new an(e);
	return t.forEach((e) => {
		e.steps.forEach((e) => {
			n.step(e);
		});
	}), n;
}
function fl(e, t, n) {
	let r = [];
	return e.nodesBetween(t.from, t.to, (e, t) => {
		n(e) && r.push({
			node: e,
			pos: t
		});
	}), r;
}
function pl(e, t) {
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
function ml(e) {
	return (t) => pl(t.$from, e);
}
function V(e, t, n) {
	return e.config[t] === void 0 && e.parent ? V(e.parent, t, n) : typeof e.config[t] == "function" ? e.config[t].bind({
		...n,
		parent: e.parent ? V(e.parent, t, n) : null
	}) : e.config[t];
}
function hl(e) {
	return e.map((e) => {
		let t = V(e, "addExtensions", {
			name: e.name,
			options: e.options,
			storage: e.storage
		});
		return t ? [e, ...hl(t())] : e;
	}).flat(10);
}
function gl(e, t) {
	let n = Je.fromSchema(t).serializeFragment(e), r = document.implementation.createHTMLDocument().createElement("div");
	return r.appendChild(n), r.innerHTML;
}
function _l(e) {
	return typeof e == "function";
}
function H(e, t = void 0, ...n) {
	return _l(e) ? t ? e.bind(t)(...n) : e(...n) : e;
}
function vl(e = {}) {
	return Object.keys(e).length === 0 && e.constructor === Object;
}
function yl(e) {
	return {
		baseExtensions: e.filter((e) => e.type === "extension"),
		nodeExtensions: e.filter((e) => e.type === "node"),
		markExtensions: e.filter((e) => e.type === "mark")
	};
}
function bl(e) {
	let t = [], { nodeExtensions: n, markExtensions: r } = yl(e), i = [...n, ...r], a = {
		default: null,
		validate: void 0,
		rendered: !0,
		renderHTML: null,
		parseHTML: null,
		keepOnSplit: !0,
		isRequired: !1
	}, o = n.filter((e) => e.name !== "text").map((e) => e.name), s = r.map((e) => e.name), c = [...o, ...s];
	return e.forEach((e) => {
		let n = V(e, "addGlobalAttributes", {
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
		let n = V(e, "addAttributes", {
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
function xl(e) {
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
function Sl(e) {
	let t = [], n = xl(e || ""), r = n.length;
	for (let e = 0; e < r; e += 1) {
		let r = n[e], i = r.indexOf(":");
		if (i === -1) continue;
		let a = r.slice(0, i).trim(), o = r.slice(i + 1).trim();
		a && o && t.push([a, o]);
	}
	return t;
}
function U(...e) {
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
				let r = new Map([...Sl(n[e]), ...Sl(t)]);
				n[e] = Array.from(r.entries()).map(([e, t]) => `${e}: ${t}`).join("; ");
			} else n[e] = t;
		}), n;
	}, {});
}
function Cl(e, t) {
	return t.filter((t) => t.type === e.type.name).filter((e) => e.attribute.rendered).map((t) => t.attribute.renderHTML ? t.attribute.renderHTML(e.attrs) || {} : { [t.name]: e.attrs[t.name] }).reduce((e, t) => U(e, t), {});
}
function wl(e) {
	return typeof e == "string" ? e.match(/^[+-]?(?:\d*\.)?\d+$/) ? Number(e) : e === "true" || e !== "false" && e : e;
}
function Tl(e, t) {
	return "style" in e ? e : {
		...e,
		getAttrs: (n) => {
			let r = e.getAttrs ? e.getAttrs(n) : e.attrs;
			if (r === !1) return !1;
			let i = t.reduce((e, t) => {
				let r = t.attribute.parseHTML ? t.attribute.parseHTML(n) : wl(n.getAttribute(t.name));
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
function El(e) {
	return Object.fromEntries(Object.entries(e).filter(([e, t]) => e === "attrs" && vl(t) ? !1 : t != null));
}
function Dl(e) {
	var t, n;
	let r = {};
	return !(e != null && (t = e.attribute) != null && t.isRequired) && "default" in (e?.attribute || {}) && (r.default = e.attribute.default), (e == null || (n = e.attribute) == null ? void 0 : n.validate) !== void 0 && (r.validate = e.attribute.validate), [e.name, r];
}
function Ol(e, t) {
	let n = bl(e), { nodeExtensions: r, markExtensions: i } = yl(e), a = r.find((e) => V(e, "topNode"))?.name;
	return new Oe({
		topNode: a,
		nodes: Object.fromEntries(r.map((r) => {
			let i = n.filter((e) => e.type === r.name), a = {
				name: r.name,
				options: r.options,
				storage: r.storage,
				editor: t
			}, o = El({
				...e.reduce((e, t) => {
					let n = V(t, "extendNodeSchema", a);
					return {
						...e,
						...n ? n(r) : {}
					};
				}, {}),
				content: H(V(r, "content", a)),
				marks: H(V(r, "marks", a)),
				group: H(V(r, "group", a)),
				inline: H(V(r, "inline", a)),
				atom: H(V(r, "atom", a)),
				selectable: H(V(r, "selectable", a)),
				draggable: H(V(r, "draggable", a)),
				code: H(V(r, "code", a)),
				whitespace: H(V(r, "whitespace", a)),
				linebreakReplacement: H(V(r, "linebreakReplacement", a)),
				defining: H(V(r, "defining", a)),
				isolating: H(V(r, "isolating", a)),
				attrs: Object.fromEntries(i.map(Dl))
			}), s = H(V(r, "parseHTML", a));
			s && (o.parseDOM = s.map((e) => Tl(e, i)));
			let c = V(r, "renderHTML", a);
			c && (o.toDOM = (e) => c({
				node: e,
				HTMLAttributes: Cl(e, i)
			}));
			let l = V(r, "renderText", a);
			return l && (o.toText = l), [r.name, o];
		})),
		marks: Object.fromEntries(i.map((r) => {
			let i = n.filter((e) => e.type === r.name), a = {
				name: r.name,
				options: r.options,
				storage: r.storage,
				editor: t
			}, o = El({
				...e.reduce((e, t) => {
					let n = V(t, "extendMarkSchema", a);
					return {
						...e,
						...n ? n(r) : {}
					};
				}, {}),
				inclusive: H(V(r, "inclusive", a)),
				excludes: H(V(r, "excludes", a)),
				group: H(V(r, "group", a)),
				spanning: H(V(r, "spanning", a)),
				code: H(V(r, "code", a)),
				attrs: Object.fromEntries(i.map(Dl))
			}), s = H(V(r, "parseHTML", a));
			s && (o.parseDOM = s.map((e) => Tl(e, i)));
			let c = V(r, "renderHTML", a);
			return c && (o.toDOM = (e) => c({
				mark: e,
				HTMLAttributes: Cl(e, i)
			})), [r.name, o];
		}))
	});
}
function kl(e) {
	let t = e.filter((t, n) => e.indexOf(t) !== n);
	return Array.from(new Set(t));
}
function Al(e) {
	return e.sort((e, t) => {
		let n = V(e, "priority") || 100, r = V(t, "priority") || 100;
		return n > r ? -1 : +(n < r);
	});
}
function jl(e) {
	let t = Al(hl(e)), n = kl(t.map((e) => e.name));
	return n.length && console.warn(`[tiptap warn]: Duplicate extension names found: [${n.map((e) => `'${e}'`).join(", ")}]. This can lead to issues.`), t;
}
function Ml(e, t) {
	return Ol(jl(e), t);
}
function Nl(e, t, n) {
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
function Pl(e, t) {
	return Nl(e, {
		from: 0,
		to: e.content.size
	}, t);
}
function Fl(e) {
	return Object.fromEntries(Object.entries(e.nodes).filter(([, e]) => e.spec.toText).map(([e, t]) => [e, t.spec.toText]));
}
function Il(e, t) {
	let n = B(t, e.schema), { from: r, to: i } = e.selection, a = [];
	e.doc.nodesBetween(r, i, (e) => {
		a.push(e);
	});
	let o = a.reverse().find((e) => e.type.name === n.name);
	return o ? { ...o.attrs } : {};
}
function Ll(e, t) {
	let n = Qc(typeof t == "string" ? t : t.name, e.schema);
	return n === "node" ? Il(e, t) : n === "mark" ? ul(e, t) : {};
}
function Rl(e, t = JSON.stringify) {
	let n = {};
	return e.filter((e) => {
		let r = t(e);
		return Object.prototype.hasOwnProperty.call(n, r) ? !1 : n[r] = !0;
	});
}
function zl(e) {
	let t = Rl(e);
	return t.length === 1 ? t : t.filter((e, n) => !t.filter((e, t) => t !== n).some((t) => e.oldRange.from >= t.oldRange.from && e.oldRange.to <= t.oldRange.to && e.newRange.from >= t.newRange.from && e.newRange.to <= t.newRange.to));
}
function Bl(e) {
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
	}), zl(r);
}
function Vl(e, t, n) {
	let r = [];
	return e === t ? n.resolve(e).marks().forEach((t) => {
		let i = mc(n.resolve(e), t.type);
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
var Hl = (e, t, n, r = 20) => {
	let i = e.doc.resolve(n), a = r, o = null;
	for (; a > 0 && o === null;) {
		let e = i.node(a);
		e?.type.name === t ? o = e : --a;
	}
	return [o, a];
}, Ul = (e) => {
	let t = e.depth - 1;
	if (t < 0) return null;
	let n = e.index(t);
	return n === 0 ? null : e.node(t).child(n - 1);
};
function Wl(e, t) {
	return t.nodes[e] || t.marks[e] || null;
}
function Gl(e, t, n) {
	return Object.fromEntries(Object.entries(n).filter(([n]) => {
		let r = e.find((e) => e.type === t && e.name === n);
		return r ? r.attribute.keepOnSplit : !1;
	}));
}
var Kl = (e, t = 500) => {
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
function ql(e, t, n = {}) {
	let { empty: r, ranges: i } = e.selection, a = t ? hc(t, e.schema) : null;
	if (r) return !!(e.storedMarks || e.selection.$from.marks()).filter((e) => !a || a.name === e.type.name).find((e) => dc(e.attrs, n, { strict: !1 }));
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
	let c = s.filter((e) => !a || a.name === e.mark.type.name).filter((e) => dc(e.mark.attrs, n, { strict: !1 })).reduce((e, t) => e + t.to - t.from, 0), l = s.filter((e) => !a || e.mark.type !== a && e.mark.type.excludes(a)).reduce((e, t) => e + t.to - t.from, 0);
	return (c > 0 ? c + l : c) >= o;
}
function Jl(e, t, n = {}) {
	if (!t) return qc(e, null, n) || ql(e, null, n);
	let r = Qc(t, e.schema);
	return r === "node" ? qc(e, t, n) : r === "mark" && ql(e, t, n);
}
var Yl = (e, t) => {
	let { $from: n, $to: r, $anchor: i } = e.selection;
	if (t) {
		let n = ml((e) => e.type.name === t)(e.selection);
		if (!n) return !1;
		let r = e.doc.resolve(n.pos + 1);
		return i.pos + 1 === r.end();
	}
	return !(r.parentOffset < r.parent.nodeSize - 2 || n.pos !== r.pos);
}, Xl = (e) => {
	let { $from: t, $to: n } = e.selection;
	return !(t.parentOffset > 0 || t.pos !== n.pos);
};
function Zl(e, t) {
	return Array.isArray(t) ? t.some((t) => (typeof t == "string" ? t : t.name) === e.name) : t;
}
function Ql(e, t) {
	let { nodeExtensions: n } = yl(t), r = n.find((t) => t.name === e);
	if (!r) return !1;
	let i = H(V(r, "group", {
		name: r.name,
		options: r.options,
		storage: r.storage
	}));
	return typeof i == "string" && i.split(" ").includes("list");
}
function $l(e, { checkChildren: t = !0, ignoreWhitespace: n = !1 } = {}) {
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
			r !== !1 && ($l(e, {
				ignoreWhitespace: n,
				checkChildren: t
			}) || (r = !1));
		}), r;
	}
	return !1;
}
function eu(e) {
	return e instanceof O;
}
var tu = class e {
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
function nu(e, t) {
	let n = t.mapping.mapResult(e.position);
	return {
		position: new tu(n.pos),
		mapResult: n
	};
}
function ru(e) {
	return new tu(e);
}
function iu(e, t, n) {
	let { selection: r } = t, i = null;
	if (vc(r) && (i = r.$cursor), i) {
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
var au = (e, t = {}) => ({ tr: n, state: r, dispatch: i }) => {
	let { selection: a } = n, { empty: o, ranges: s } = a, c = hc(e, r.schema);
	if (i) {
		if (o) {
			let e = ul(r, c);
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
	return iu(r, n, c);
}, ou = (e, t) => ({ tr: n }) => (n.setMeta(e, t), !0), su = (e, t = {}) => ({ state: n, dispatch: r, chain: i }) => {
	let a = B(e, n.schema), o;
	return n.selection.$anchor.sameParent(n.selection.$head) && (o = n.selection.$anchor.parent.attrs), a.isTextblock ? i().command(({ commands: e }) => ar(a, {
		...o,
		...t
	})(n) ? !0 : e.clearNodes()).command(({ state: e }) => ar(a, {
		...o,
		...t
	})(e, r)).run() : (console.warn("[tiptap warn]: Currently \"setNode()\" only supports text block nodes."), !1);
}, cu = (e) => ({ tr: t, dispatch: n }) => {
	if (n) {
		let { doc: n } = t, r = yc(e, 0, n.content.size), i = O.create(n, r);
		t.setSelection(i);
	}
	return !0;
}, lu = (e, t) => ({ tr: n, state: r, dispatch: i }) => {
	let { selection: a } = r, o, s;
	return typeof t == "number" ? (o = t, s = t) : t && "from" in t && "to" in t ? (o = t.from, s = t.to) : (o = a.from, s = a.to), i && n.doc.nodesBetween(o, s, (t, r) => {
		t.isText || n.setNodeMarkup(r, void 0, {
			...t.attrs,
			dir: e
		});
	}), !0;
}, uu = (e) => ({ tr: t, dispatch: n }) => {
	if (n) {
		let { doc: n } = t, { from: r, to: i } = typeof e == "number" ? {
			from: e,
			to: e
		} : e, a = D.atStart(n).from, o = D.atEnd(n).to, s = yc(r, a, o), c = yc(i, a, o), l = D.create(n, s, c);
		t.setSelection(l);
	}
	return !0;
}, du = (e) => ({ state: t, dispatch: n }) => _r(B(e, t.schema))(t, n);
function fu(e, t) {
	let n = e.storedMarks || e.selection.$to.parentOffset && e.selection.$from.marks();
	if (n) {
		let r = n.filter((e) => t?.includes(e.type.name));
		e.tr.ensureMarks(r);
	}
}
var pu = ({ keepMarks: e = !0 } = {}) => ({ tr: t, state: n, dispatch: r, editor: i }) => {
	let { selection: a, doc: o } = t, { $from: s, $to: c } = a, l = i.extensionManager.attributes, u = Gl(l, s.node().type.name, s.node().attrs);
	if (a instanceof O && a.node.isBlock) return !s.parentOffset || !Nt(o, s.pos) ? !1 : (r && (e && fu(n, i.extensionManager.splittableMarks), t.split(s.pos).scrollIntoView()), !0);
	if (!s.parent.isBlock) return !1;
	let d = c.parentOffset === c.parent.content.size, f = s.depth === 0 ? void 0 : Pc(s.node(-1).contentMatchAt(s.indexAfter(-1))), p = d && f ? [{
		type: f,
		attrs: u
	}] : void 0, m = Nt(t.doc, t.mapping.map(s.pos), 1, p);
	if (!p && !m && Nt(t.doc, t.mapping.map(s.pos), 1, f ? [{ type: f }] : void 0) && (m = !0, p = f ? [{
		type: f,
		attrs: u
	}] : void 0), r) {
		if (m && (a instanceof D && t.deleteSelection(), t.split(t.mapping.map(s.pos), 1, p), f && !d && !s.parentOffset && s.parent.type !== f)) {
			let e = t.mapping.map(s.before()), n = t.doc.resolve(e);
			s.node(-1).canReplaceWith(n.index(), n.index() + 1, f) && t.setNodeMarkup(t.mapping.map(s.before()), f);
		}
		e && fu(n, i.extensionManager.splittableMarks), t.scrollIntoView();
	}
	return m;
}, mu = (e, t = {}) => ({ tr: n, state: r, dispatch: i, editor: o }) => {
	let s = B(e, r.schema), { $from: c, $to: l } = r.selection, u = r.selection.node;
	if (u && u.isBlock || c.depth < 2 || !c.sameParent(l)) return !1;
	let f = c.node(-1);
	if (f.type !== s) return !1;
	let p = o.extensionManager.attributes;
	if (c.parent.content.size === 0 && c.node(-1).childCount === c.indexAfter(-1)) {
		if (c.depth === 2 || c.node(-3).type !== s || c.index(-2) !== c.node(-2).childCount - 1) return !1;
		if (i) {
			let e = a.empty, r = c.index(-1) ? 1 : c.index(-2) ? 2 : 3;
			for (let t = c.depth - r; t >= c.depth - 3; --t) e = a.from(c.node(t).copy(e));
			let i = c.indexAfter(-1) < c.node(-2).childCount ? 1 : c.indexAfter(-2) < c.node(-3).childCount ? 2 : 3, o = {
				...Gl(p, c.node().type.name, c.node().attrs),
				...t
			}, l = s.contentMatch.defaultType?.createAndFill(o) || void 0;
			e = e.append(a.from(s.createAndFill(null, l) || void 0));
			let u = c.before(c.depth - (r - 1));
			n.replace(u, c.after(-i), new d(e, 4 - r, 0));
			let f = -1;
			n.doc.nodesBetween(u, n.doc.content.size, (e, t) => {
				if (f > -1) return !1;
				e.isTextblock && e.content.size === 0 && (f = t + 1);
			}), f > -1 && n.setSelection(D.near(n.doc.resolve(f))), n.scrollIntoView();
		}
		return !0;
	}
	let m = l.pos === c.end() ? f.contentMatchAt(0).defaultType : null, h = {
		...Gl(p, f.type.name, f.attrs),
		...t
	}, g = {
		...Gl(p, c.node().type.name, c.node().attrs),
		...t
	};
	n.delete(c.pos, l.pos);
	let _ = m ? [{
		type: s,
		attrs: h
	}, {
		type: m,
		attrs: g
	}] : [{
		type: s,
		attrs: h
	}];
	if (!Nt(n.doc, c.pos, 2)) return !1;
	if (i) {
		let { selection: e, storedMarks: t } = r, { splittableMarks: a } = o.extensionManager, s = t || e.$to.parentOffset && e.$from.marks();
		if (n.split(c.pos, 2, _).scrollIntoView(), !s || !i) return !0;
		let l = s.filter((e) => a.includes(e.type.name));
		n.ensureMarks(l);
	}
	return !0;
};
function hu(e) {
	return !e || e === "1" ? null : e;
}
function gu(e, t) {
	return hu(e) === hu(t);
}
var _u = (e, t) => {
	let n = ml((e) => e.type === t)(e.selection);
	if (!n) return !0;
	let r = e.doc.resolve(Math.max(0, n.pos - 1)).before(n.depth);
	if (r === void 0) return !0;
	let i = e.doc.nodeAt(r);
	return !(n.node.type === i?.type && Ft(e.doc, n.pos)) || !gu(n.node.attrs.type, i?.attrs.type) || e.join(n.pos), !0;
}, vu = (e, t) => {
	let n = ml((e) => e.type === t)(e.selection);
	if (!n) return !0;
	let r = e.doc.resolve(n.start).after(n.depth);
	if (r === void 0) return !0;
	let i = e.doc.nodeAt(r);
	return !(n.node.type === i?.type && Ft(e.doc, r)) || !gu(n.node.attrs.type, i?.attrs.type) || e.join(r), !0;
};
function yu(e) {
	let t = e.doc, n = t.firstChild;
	if (!n) return null;
	let r = t.resolve(1), i = t.resolve(n.nodeSize - 1);
	return D.between(r, i);
}
var bu = (e, t, n, r = {}) => ({ editor: i, tr: a, state: o, dispatch: s, chain: c, commands: l, can: u }) => {
	let { extensions: d, splittableMarks: f } = i.extensionManager, p = B(e, o.schema), m = B(t, o.schema), { selection: h, storedMarks: g } = o, { $from: _, $to: v } = h, y = _.blockRange(v), b = g || h.$to.parentOffset && h.$from.marks();
	if (!y) return !1;
	let x = ml((e) => Ql(e.type.name, d))(h), S = h.from === 0 && h.to === o.doc.content.size, ee = o.doc.content.content, te = ee.length === 1 ? ee[0] : null, ne = S && te && Ql(te.type.name, d) ? {
		node: te,
		pos: 0,
		depth: 0
	} : null, re = x ?? ne, ie = !!x && y.depth >= 1 && y.depth - x.depth <= 1, ae = !!ne;
	if ((ie || ae) && re) {
		if (re.node.type === p) return S && ae ? c().command(({ tr: e, dispatch: t }) => {
			let n = yu(e);
			return n ? (e.setSelection(n), t && t(e), !0) : !1;
		}).liftListItem(m).run() : l.liftListItem(m);
		if (Ql(re.node.type.name, d) && p.validContent(re.node.content)) return c().command(() => (a.setNodeMarkup(re.pos, p), !0)).command(() => _u(a, p)).command(() => vu(a, p)).run();
	}
	return !n || !b || !s ? c().command(() => u().wrapInList(p, r) ? !0 : l.clearNodes()).wrapInList(p, r).command(() => _u(a, p)).command(() => vu(a, p)).run() : c().command(() => {
		let e = u().wrapInList(p, r), t = b.filter((e) => f.includes(e.type.name));
		return a.ensureMarks(t), e ? !0 : l.clearNodes();
	}).wrapInList(p, r).command(() => _u(a, p)).command(() => vu(a, p)).run();
}, xu = (e, t = {}, n = {}) => ({ state: r, commands: i }) => {
	let { extendEmptyMarkRange: a = !1 } = n, o = hc(e, r.schema);
	return ql(r, o, t) ? i.unsetMark(o, { extendEmptyMarkRange: a }) : i.setMark(o, t);
}, Su = (e, t, n = {}) => ({ state: r, commands: i }) => {
	let a = B(e, r.schema), o = B(t, r.schema), s = qc(r, a, n), c;
	return r.selection.$anchor.sameParent(r.selection.$head) && (c = r.selection.$anchor.parent.attrs), s ? i.setNode(o, c) : i.setNode(a, {
		...c,
		...n
	});
}, Cu = (e, t = {}) => ({ state: n, commands: r }) => {
	let i = B(e, n.schema);
	return qc(n, i, t) ? r.lift(i) : r.wrapIn(i, t);
}, wu = () => ({ state: e, dispatch: t }) => {
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
}, Tu = (e = {}) => ({ tr: t, dispatch: n, editor: r }) => {
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
}, Eu = (e, t = {}) => ({ tr: n, state: r, dispatch: i }) => {
	let { extendEmptyMarkRange: a = !1 } = t, { selection: o } = n, s = hc(e, r.schema), { $from: c, empty: l, ranges: u } = o;
	if (!i) return !0;
	if (l && a) {
		let { from: e, to: t } = o, r = mc(c, s, c.marks().find((e) => e.type === s)?.attrs);
		r && (e = r.from, t = r.to), n.removeMark(e, t, s);
	} else u.forEach((e) => {
		n.removeMark(e.$from.pos, e.$to.pos, s);
	});
	return n.removeStoredMark(s), !0;
}, Du = (e) => ({ tr: t, state: n, dispatch: r }) => {
	let { selection: i } = n, a, o;
	return typeof e == "number" ? (a = e, o = e) : e && "from" in e && "to" in e ? (a = e.from, o = e.to) : (a = i.from, o = i.to), r && t.doc.nodesBetween(a, o, (e, n) => {
		if (e.isText) return;
		let r = { ...e.attrs };
		delete r.dir, t.setNodeMarkup(n, void 0, r);
	}), !0;
}, Ou = (e, t = {}) => ({ tr: n, state: r, dispatch: i }) => {
	let a = null, o = null, s = Qc(typeof e == "string" ? e : e.name, r.schema);
	if (!s) return !1;
	s === "node" && (a = B(e, r.schema)), s === "mark" && (o = hc(e, r.schema));
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
}, ku = new A("__tiptap_decorations__"), Au = (e) => ({ tr: t, dispatch: n }) => (n && t.setMeta(ku, {
	type: "force",
	name: e
}), !0), ju = (e, t = {}) => ({ state: n, dispatch: r }) => ir(B(e, n.schema), t)(n, r), Mu = (e, t = {}) => ({ state: n, dispatch: r }) => dr(B(e, n.schema), t)(n, r), Nu = /* @__PURE__ */ t({
	blur: () => Ys,
	clearContent: () => Xs,
	clearNodes: () => Zs,
	command: () => Qs,
	createParagraphNear: () => $s,
	cut: () => ec,
	deleteCurrentNode: () => tc,
	deleteNode: () => nc,
	deleteRange: () => rc,
	deleteSelection: () => sc,
	enter: () => cc,
	exitCode: () => lc,
	extendMarkRange: () => gc,
	first: () => _c,
	focus: () => wc,
	forEach: () => Tc,
	insertContent: () => Ec,
	insertContentAt: () => Nc,
	insertDefaultBlock: () => Fc,
	joinBackward: () => Rc,
	joinDown: () => Lc,
	joinForward: () => zc,
	joinItemBackward: () => Bc,
	joinItemForward: () => Vc,
	joinTextblockBackward: () => Hc,
	joinTextblockForward: () => Uc,
	joinUp: () => Ic,
	keyboardShortcut: () => Kc,
	lift: () => Jc,
	liftEmptyBlock: () => Yc,
	liftListItem: () => Xc,
	newlineInCode: () => Zc,
	resetAttributes: () => el,
	scrollIntoView: () => tl,
	selectAll: () => nl,
	selectNodeBackward: () => rl,
	selectNodeForward: () => il,
	selectParentNode: () => al,
	selectTextblockEnd: () => ol,
	selectTextblockStart: () => sl,
	setContent: () => ll,
	setMark: () => au,
	setMeta: () => ou,
	setNode: () => su,
	setNodeSelection: () => cu,
	setTextDirection: () => lu,
	setTextSelection: () => uu,
	sinkListItem: () => du,
	splitBlock: () => pu,
	splitListItem: () => mu,
	toggleList: () => bu,
	toggleMark: () => xu,
	toggleNode: () => Su,
	toggleWrap: () => Cu,
	undoInputRule: () => wu,
	unsetAllMarks: () => Tu,
	unsetMark: () => Eu,
	unsetTextDirection: () => Du,
	updateAttributes: () => Ou,
	updateDecorations: () => Au,
	wrapIn: () => ju,
	wrapInList: () => Mu
}), Pu = /* @__PURE__ */ new WeakMap();
function Fu(e, t) {
	Pu.set(e, (Pu.get(e) ?? 0) + 1);
	try {
		return t();
	} finally {
		let t = (Pu.get(e) ?? 1) - 1;
		t > 0 ? Pu.set(e, t) : Pu.delete(e);
	}
}
function Iu(e) {
	return Pu.has(e);
}
var Lu = class {
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
}, Ru = typeof process < "u" && process.env.NODE_ENV !== "production";
function zu(e) {
	return e.kind === "widget";
}
function Bu(e, t) {
	let n = [], r = /* @__PURE__ */ new Set();
	for (let i of e) i.kind === "widget" && zu(i) && r.add(i.key), n.push(i.toPMDecoration(t));
	return {
		decorations: n,
		widgetKeys: r
	};
}
function Vu(e, t, n) {
	let { decorations: r, widgetKeys: i } = Bu(t, n);
	return {
		set: L.create(e, r),
		widgetKeys: i
	};
}
function Hu({ position: e, from: t, to: n, docSize: r }) {
	return e < t ? !1 : e < n || e === n && n === r;
}
function Uu({ decorations: e, from: t, to: n, docSize: r, extensionName: i, warnedExtensions: a }) {
	return e.filter((e) => Hu({
		position: e.anchor,
		from: t,
		to: n,
		docSize: r
	}) ? !0 : (e.anchor === n || a.has(i) || (a.add(i), console.warn(`[tiptap warn]: Extension "${i}" returned a decoration outside the requested range [${t}, ${n}). It was ignored.`)), !1));
}
function Wu(e) {
	let t = e.spec?.key;
	return typeof t == "string" ? t : void 0;
}
function Gu(e) {
	let t = /* @__PURE__ */ new Map(), n = /* @__PURE__ */ new Map();
	for (let r of e.find()) {
		let e = Wu(r);
		if (!e) continue;
		let i = r.spec.extensionName ?? "unknown", a = t.get(e) ?? /* @__PURE__ */ new Set();
		a.add(i), t.set(e, a), n.set(e, (n.get(e) ?? 0) + 1);
	}
	return Array.from(t, ([e, t]) => ({
		key: e,
		extensions: t
	})).filter(({ key: e }) => (n.get(e) ?? 0) > 1);
}
function Ku(e) {
	return e.jsonID === "attr";
}
function qu(e) {
	let t = !1;
	if (e.getMap().forEach(() => {
		t = !0;
	}), t || Ku(e)) return !0;
	let n = e;
	return typeof n.from == "number" && typeof n.to == "number";
}
function Ju(e, t) {
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
function Yu(e, t) {
	if (e.steps.some((e) => !qu(e))) return { type: "full" };
	let n = Bl(e).map(({ newRange: e }) => e);
	e.steps.forEach((t, r) => {
		if (!Ku(t)) return;
		let i = e.mapping.slice(r);
		n.push({
			from: i.map(t.pos, -1),
			to: i.map(t.pos + 1)
		});
	});
	let r = [];
	for (let e of n) {
		let n = Ju(t, e);
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
function Xu(e, t, n, r) {
	return e.map(t, n, { onRemove: (e) => {
		let t = e?.key;
		typeof t == "string" && r.delete(t);
	} });
}
function Zu(e, t, n) {
	let r = t.decorationSetsByExtension[e] ?? L.empty, i = new Set(t.widgetKeysByExtension[e] ?? []);
	return {
		set: Xu(r, n.mapping, n.doc, i),
		widgetKeys: i
	};
}
function Qu(e, t) {
	let n = Object.values(t).flatMap((e) => e.find());
	return L.create(e, n);
}
function $u(e) {
	let t = /* @__PURE__ */ new Set();
	for (let n of Object.values(e)) for (let e of n) t.add(e);
	return t;
}
function ed(e, t) {
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
function td(e, t, n) {
	return n ? !0 : e.update === "manual" ? !1 : e.shouldUpdate ? e.shouldUpdate(t) : t.tr.docChanged;
}
var nd = /* @__PURE__ */ new Set(), rd = class {
	constructor(e) {
		this.warnedWidgetKeys = /* @__PURE__ */ new Set(), this.warnedOutOfRangeExtensions = /* @__PURE__ */ new Set(), this.handleBeforeTransaction = ({ nextState: e }) => {
			let t = ku.getState(e);
			t && this.warnDuplicateWidgetKeys(t);
		}, this.editor = e.editor, this.entries = this.resolveEntries(e.entries), this.entries.forEach(({ name: e, spec: t }) => ed(e, t)), this.plugin = this.entries.length > 0 ? this.createPlugin() : null, this.editor.on("beforeTransaction", this.handleBeforeTransaction);
	}
	destroy() {
		this.editor.off("beforeTransaction", this.handleBeforeTransaction);
	}
	liveWidgetKeys() {
		return ku.getState(this.editor.state)?.widgetKeys ?? nd;
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
		return new k({
			key: ku,
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
						widgetKeys: $u(i)
					};
					return this.warnDuplicateWidgetKeys(a), a;
				},
				apply: (n, r, i, a) => {
					let o = n.getMeta(ku), s = o?.type === "force" && !o.name, c = o?.type === "force" ? o.name : void 0, l = {}, u = {}, d = /* @__PURE__ */ new Set();
					return Fu(e, () => {
						for (let { name: o, spec: f } of t) {
							let t = s || c === o;
							if (!td(f, {
								editor: e,
								tr: n,
								oldState: i,
								newState: a
							}, t)) {
								let e = Zu(o, r, n);
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
						widgetKeys: $u(u)
					};
				}
			},
			props: { decorations(e) {
				return ku.getState(e)?.mergedDecorationSet ?? L.empty;
			} }
		});
	}
	applyChangedRangesRecompute(e, t, n, r, i) {
		let a = Yu(r, i.doc);
		return a.type === "full" ? this.buildFullSet(e, t, i) : this.rebuildRanges(e, t, n, r, i, a.ranges);
	}
	rebuildRanges(e, t, n, r, i, a) {
		let o = n.decorationSetsByExtension[e] ?? L.empty, s = new Set(n.widgetKeysByExtension[e] ?? []), c = Xu(o, r.mapping, r.doc, s), l = i.doc.content.size;
		for (let { from: n, to: r } of a) {
			let a = c.find(n, r).filter((e) => Hu({
				position: e.from,
				from: n,
				to: r,
				docSize: l
			}));
			for (let e of a) {
				let t = Wu(e);
				t && s.delete(t);
			}
			c = c.remove(a);
			let { decorations: o, widgetKeys: u } = Bu(Uu({
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
		return Vu(n.doc, r, e);
	}
	runCreate(e, t, n) {
		try {
			return n();
		} catch (n) {
			return console.error(`[tiptap error]: Extension "${e}" threw in \`addDecorations().${t}()\`. Its decorations were dropped for this update.`, n), [];
		}
	}
	warnDuplicateWidgetKeys(e) {
		if (!Ru) return;
		if (e.widgetKeys.size === 0) {
			this.warnedWidgetKeys.clear();
			return;
		}
		let t = Gu(e.mergedDecorationSet), n = new Set(t.map(({ key: e }) => e));
		for (let { key: e, extensions: n } of t) {
			if (this.warnedWidgetKeys.has(e)) continue;
			let t = Array.from(n).map((e) => `"${e}"`).join(", ");
			console.warn(`[tiptap warn]: Duplicate widget decoration key "${e}" in extension${n.size === 1 ? "" : "s"} ${t}. Widget decoration keys must be globally unique, otherwise ProseMirror misplaces the widget DOM. Use a stable, unique key (e.g. \`comment-\${id}\`).`);
		}
		this.warnedWidgetKeys = n;
	}
	buildMergedSet(e, t) {
		let n = Object.keys(t);
		return n.length === 1 ? t[n[0]] : Qu(e, t);
	}
	mergeAfterApply({ entries: e, previous: t, tr: n, decorationSetsByExtension: r, recomputedNames: i }) {
		return e.length === 1 ? r[e[0].name] : i.size === 0 ? t.mergedDecorationSet.map(n.mapping, n.doc) : Qu(n.doc, r);
	}
};
function id(e, t) {
	let { selection: n } = e, { $from: r } = n;
	if (n instanceof O) {
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
function ad(e, t, n) {
	let r = document.querySelector(`style[data-tiptap-style${n ? `-${n}` : ""}]`);
	if (r !== null) return r;
	let i = document.createElement("style");
	return t && i.setAttribute("nonce", t), i.setAttribute(`data-tiptap-style${n ? `-${n}` : ""}`, ""), i.innerHTML = e, document.getElementsByTagName("head")[0].appendChild(i), i;
}
function od(e) {
	return typeof e == "number";
}
function sd(e) {
	return Object.prototype.toString.call(e).slice(8, -1);
}
function cd(e) {
	return sd(e) === "Object" && e.constructor === Object && Object.getPrototypeOf(e) === Object.prototype;
}
function ld(e, t, n) {
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
var ud = 4;
function dd(e) {
	let t = 0;
	for (let n of e) t = n === "	" ? t + ud - t % ud : t + 1;
	return t;
}
function fd(e, t, n, r, i) {
	if (!e || !Array.isArray(e.content)) return "";
	let a = typeof n == "function" ? n(r) : n, [o, ...s] = e.content, c = `${a}${t.renderChildren([o])}`;
	return s && s.length > 0 && s.forEach((e, n) => {
		let r = t.renderChild?.call(t, e, n + 1) ?? t.renderChildren([e]);
		if (r != null) {
			let n = (e) => {
				if (!i?.alignNestedToPrefix) return t.indent(e);
				let n = t.indent(""), r = dd(a);
				return (dd(n) >= r ? n : " ".repeat(r)) + e;
			}, o = r.split("\n").map((e) => n(e || "")).join("\n");
			c += e.type === "paragraph" ? `\n\n${o}` : `\n${o}`;
		}
	}), c;
}
function pd(e, t) {
	let n = { ...e };
	return cd(e) && cd(t) && Object.keys(t).forEach((r) => {
		cd(t[r]) && cd(e[r]) ? n[r] = pd(e[r], t[r]) : n[r] = t[r];
	}), n;
}
function md(e, t, n = {}) {
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
var hd = class {
	constructor(e) {
		this.find = e.find, this.handler = e.handler, this.undoable = e.undoable ?? !0;
	}
}, gd = (e, t) => {
	if (uc(t)) return t.exec(e);
	let n = t(e);
	if (!n) return null;
	let r = [n.text];
	return r.index = n.index, r.input = e, r.data = n.data, n.replaceWith && (n.text.includes(n.replaceWith) || console.warn("[tiptap warn]: \"inputRuleMatch.replaceWith\" must be part of \"inputRuleMatch.text\"."), r.push(n.replaceWith)), r;
};
function _d(e) {
	let { editor: t, from: n, to: r, text: i, rules: a, plugin: o } = e, { view: s } = t;
	if (s.composing) return !1;
	let c = s.state.doc.resolve(n);
	if (c.parent.type.spec.code || (c.nodeBefore || c.nodeAfter)?.marks.find((e) => e.type.spec.code)) return !1;
	let l = !1, u = Kl(c) + i;
	return a.forEach((e) => {
		if (l) return;
		let a = gd(u, e.find);
		if (!a) return;
		let d = a[0].length - i.length;
		if (d > 0) {
			let e = c.parentOffset - d;
			if (e < 0 || c.parent.textBetween(e, c.parentOffset) !== a[0].slice(0, d)) return;
		}
		let f = s.state.tr, p = qs({
			state: s.state,
			transaction: f
		}), m = {
			from: n - (a[0].length - i.length),
			to: r
		}, { commands: h, chain: g, can: _ } = new Js({
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
function vd(e) {
	let { editor: t, rules: n } = e, r = new k({
		state: {
			init() {
				return null;
			},
			apply(e, i, o) {
				let s = e.getMeta(r);
				if (s) return s;
				let c = e.getMeta("applyInputRules");
				return c && setTimeout(() => {
					let { text: e } = c;
					e = typeof e == "string" ? e : gl(a.from(e), o.schema);
					let { from: i } = c, s = i + e.length;
					_d({
						editor: t,
						from: i,
						to: s,
						text: e,
						rules: n,
						plugin: r
					});
				}), e.selectionSet || e.docChanged ? null : i;
			}
		},
		props: {
			handleTextInput(e, i, a, o) {
				return _d({
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
				i && _d({
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
				return a ? _d({
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
var yd = class {
	constructor(e = {}) {
		this.type = "extendable", this.parent = null, this.child = null, this.name = "", this.config = { name: this.name }, this.config = {
			...this.config,
			...e
		}, this.name = this.config.name;
	}
	get options() {
		return { ...H(V(this, "addOptions", { name: this.name })) };
	}
	get storage() {
		return { ...H(V(this, "addStorage", {
			name: this.name,
			options: this.options
		})) };
	}
	configure(e = {}) {
		let t = this.extend({
			...this.config,
			addOptions: () => pd(this.options, e)
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
}, bd = class e extends yd {
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
}, xd = class {
	constructor(e) {
		this.find = e.find, this.handler = e.handler;
	}
}, Sd = (e, t, n) => {
	if (uc(t)) return [...e.matchAll(t)];
	let r = t(e, n);
	return r ? r.map((t) => {
		let n = [t.text];
		return n.index = t.index, n.input = e, n.data = t.data, t.replaceWith && (t.text.includes(t.replaceWith) || console.warn("[tiptap warn]: \"pasteRuleMatch.replaceWith\" must be part of \"pasteRuleMatch.text\"."), n.push(t.replaceWith)), n;
	}) : [];
};
function Cd(e) {
	let { editor: t, state: n, from: r, to: i, rule: a, pasteEvent: o, dropEvent: s } = e, { commands: c, chain: l, can: u } = new Js({
		editor: t,
		state: n
	}), d = [];
	return n.doc.nodesBetween(r, i, (e, t) => {
		var f;
		if ((f = e.type) != null && (f = f.spec) != null && f.code || !(e.isText || e.isTextblock || e.isInline)) return;
		let p = e.content?.size ?? e.nodeSize ?? 0, m = Math.max(r, t), h = Math.min(i, t + p);
		m >= h || Sd(e.isText ? e.text || "" : e.textBetween(m - t, h - t, void 0, "￼"), a.find, o).forEach((e) => {
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
var wd = null, Td = (e) => {
	var t;
	let n = new ClipboardEvent("paste", { clipboardData: new DataTransfer() });
	return (t = n.clipboardData) == null || t.setData("text/html", e), n;
};
function Ed(e) {
	let { editor: t, rules: n } = e, r = null, i = !1, o = !1, s = typeof ClipboardEvent < "u" ? new ClipboardEvent("paste") : null, c;
	try {
		c = typeof DragEvent < "u" ? new DragEvent("drop") : null;
	} catch {
		c = null;
	}
	let l = ({ state: e, from: n, to: r, rule: i, pasteEvt: a }) => {
		let o = e.tr, l = qs({
			state: e,
			transaction: o
		});
		if (Cd({
			editor: t,
			state: l,
			from: Math.max(n - 1, 0),
			to: r.b - 1,
			rule: i,
			pasteEvent: a,
			dropEvent: c
		}) && o.steps.length) {
			try {
				c = typeof DragEvent < "u" ? new DragEvent("drop") : null;
			} catch {
				c = null;
			}
			return s = typeof ClipboardEvent < "u" ? new ClipboardEvent("paste") : null, o;
		}
	};
	return n.map((e) => new k({
		view(e) {
			let n = (n) => {
				r = e.dom.parentElement?.contains(n.target) ? e.dom.parentElement : null, r && (wd = t);
			}, i = () => {
				wd && (wd = null);
			};
			return window.addEventListener("dragstart", n), window.addEventListener("dragend", i), { destroy() {
				window.removeEventListener("dragstart", n), window.removeEventListener("dragend", i);
			} };
		},
		props: { handleDOMEvents: {
			drop: (e, t) => {
				if (o = r === e.dom.parentElement, c = t, !o) {
					let e = wd;
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
				return s = t, i = !!n?.includes("data-pm-slice"), !1;
			}
		} },
		appendTransaction: (t, n, r) => {
			let c = t[0], u = c.getMeta("uiEvent") === "paste" && !i, d = c.getMeta("uiEvent") === "drop" && !o, f = c.getMeta("applyPasteRules"), p = !!f;
			if (!u && !d && !p) return;
			if (p) {
				let { text: t } = f;
				t = typeof t == "string" ? t : gl(a.from(t), r.schema);
				let { from: n } = f, i = n + t.length, o = Td(t);
				return l({
					rule: e,
					state: r,
					from: n,
					to: { b: i },
					pasteEvt: o
				});
			}
			let m = n.doc.content.findDiffStart(r.doc.content), h = n.doc.content.findDiffEnd(r.doc.content);
			if (od(m) && h && m !== h.b) return l({
				rule: e,
				state: r,
				from: m,
				to: h,
				pasteEvt: s
			});
		}
	}));
}
var Dd = class {
	constructor(e, t) {
		this.splittableMarks = [], this.nonClearableMarks = [], this.decorationManager = null, this.editor = t, this.baseExtensions = e, this.extensions = jl(e), this.schema = Ol(this.extensions, t), this.setupExtensions();
	}
	get commands() {
		return this.extensions.reduce((e, t) => {
			let n = V(t, "addCommands", {
				name: t.name,
				options: t.options,
				storage: this.editor.extensionStorage[t.name],
				editor: this.editor,
				type: Wl(t.name, this.schema)
			});
			return n ? {
				...e,
				...n()
			} : e;
		}, {});
	}
	get plugins() {
		let { editor: e } = this, t = Al([...this.extensions].reverse()).flatMap((t) => {
			let n = {
				name: t.name,
				options: t.options,
				storage: this.editor.extensionStorage[t.name],
				editor: e,
				type: Wl(t.name, this.schema)
			}, r = [], i = V(t, "addKeyboardShortcuts", n), a = {};
			if (t.type === "mark" && V(t, "exitable", n) && (a.ArrowRight = () => bd.handleExit({
				editor: e,
				mark: t
			})), i) {
				let t = Object.fromEntries(Object.entries(i()).map(([t, n]) => [t, () => n({ editor: e })]));
				a = {
					...a,
					...t
				};
			}
			let o = Gs(a);
			r.push(o);
			let s = V(t, "addInputRules", n);
			if (Zl(t, e.options.enableInputRules) && s) {
				let t = s();
				if (t && t.length) {
					let n = vd({
						editor: e,
						rules: t
					}), i = Array.isArray(n) ? n : [n];
					r.push(...i);
				}
			}
			let c = V(t, "addPasteRules", n);
			if (Zl(t, e.options.enablePasteRules) && c) {
				let t = c();
				if (t && t.length) {
					let n = Ed({
						editor: e,
						rules: t
					});
					r.push(...n);
				}
			}
			let l = V(t, "addProseMirrorPlugins", n);
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
			let r = V(e, "addDecorations", {
				name: e.name,
				options: e.options,
				storage: this.editor.extensionStorage[e.name],
				editor: t,
				type: Wl(e.name, this.schema)
			});
			r && n.push({
				name: e.name,
				addDecorations: r
			});
		}), this.decorationManager = new rd({
			editor: t,
			entries: n
		}), this.decorationManager.plugin;
	}
	get attributes() {
		return bl(this.extensions);
	}
	get nodeViews() {
		let { editor: e } = this, { nodeExtensions: t } = yl(this.extensions);
		return Object.fromEntries(t.filter((e) => !!V(e, "addNodeView")).map((t) => {
			let n = this.attributes.filter((e) => e.type === t.name), r = V(t, "addNodeView", {
				name: t.name,
				options: t.options,
				storage: this.editor.extensionStorage[t.name],
				editor: e,
				type: B(t.name, this.schema)
			});
			if (!r) return [];
			let i = r();
			return i ? [t.name, (r, a, o, s, c) => {
				let l = Cl(r, n);
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
		return Al([...this.extensions].reverse()).reduceRight((e, n) => {
			let r = {
				name: n.name,
				options: n.options,
				storage: this.editor.extensionStorage[n.name],
				editor: t,
				type: Wl(n.name, this.schema)
			}, i = V(n, "dispatchTransaction", r);
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
		return Al([...this.extensions]).reduce((e, n) => {
			let r = {
				name: n.name,
				options: n.options,
				storage: this.editor.extensionStorage[n.name],
				editor: t,
				type: Wl(n.name, this.schema)
			}, i = V(n, "transformPastedHTML", r);
			return i ? (t, n) => {
				let a = e(t, n);
				return i.call(r, a);
			} : e;
		}, e || ((e) => e));
	}
	get markViews() {
		let { editor: e } = this, { markExtensions: t } = yl(this.extensions);
		return Object.fromEntries(t.filter((e) => !!V(e, "addMarkView")).map((t) => {
			let n = this.attributes.filter((e) => e.type === t.name), r = V(t, "addMarkView", {
				name: t.name,
				options: t.options,
				storage: this.editor.extensionStorage[t.name],
				editor: e,
				type: hc(t.name, this.schema)
			});
			return r ? [t.name, (i, a, o) => {
				let s = Cl(i, n);
				return r()({
					mark: i,
					view: a,
					inline: o,
					editor: e,
					extension: t,
					HTMLAttributes: s,
					updateAttributes: (t) => {
						md(i, e, t);
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
				type: Wl(e.name, this.schema)
			};
			e.type === "mark" && ((H(V(e, "keepOnSplit", t)) ?? !0) && this.splittableMarks.push(e.name), (H(V(e, "clearable", t)) ?? !0) || this.nonClearableMarks.push(e.name));
			let n = V(e, "onBeforeCreate", t), r = V(e, "onCreate", t), i = V(e, "onUpdate", t), a = V(e, "onSelectionUpdate", t), o = V(e, "onTransaction", t), s = V(e, "onFocus", t), c = V(e, "onBlur", t), l = V(e, "onDestroy", t);
			n && this.editor.on("beforeCreate", n), r && this.editor.on("create", r), i && this.editor.on("update", i), a && this.editor.on("selectionUpdate", a), o && this.editor.on("transaction", o), s && this.editor.on("focus", s), c && this.editor.on("blur", c), l && this.editor.on("destroy", l);
		});
	}
};
Dd.resolve = jl, Dd.sort = Al, Dd.flatten = hl;
var W = class e extends yd {
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
}, Od = W.create({
	name: "clipboardTextSerializer",
	addOptions() {
		return { blockSeparator: void 0 };
	},
	addProseMirrorPlugins() {
		return [new k({
			key: new A("clipboardTextSerializer"),
			props: { clipboardTextSerializer: () => {
				let { editor: e } = this, { state: t, schema: n } = e, { doc: r, selection: i } = t, a = Fl(n), { blockSeparator: o } = this.options, s = {
					...o === void 0 ? {} : { blockSeparator: o },
					textSerializers: a
				};
				return [...i.ranges].sort((e, t) => e.$from.pos - t.$from.pos).map(({ $from: e, $to: t }) => Nl(r, {
					from: e.pos,
					to: t.pos
				}, s)).join(o ?? "\n\n");
			} }
		})];
	}
}), kd = W.create({
	name: "commands",
	addCommands() {
		return { ...Nu };
	}
}), Ad = W.create({
	name: "delete",
	onUpdate({ transaction: e, appendedTransactions: t }) {
		var n;
		let r = () => {
			var n, r;
			if (((n = this.editor.options.coreExtensionOptions) == null || (n = n.delete) == null || (r = n.filterTransaction) == null ? void 0 : r.call(n, e)) ?? e.getMeta("y-sync$")) return;
			let i = dl(e.before, [e, ...t]);
			Bl(i).forEach((t) => {
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
				if (t instanceof pt) {
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
}), jd = W.create({
	name: "drop",
	addProseMirrorPlugins() {
		return [new k({
			key: new A("tiptapDrop"),
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
}), Md = W.create({
	name: "editable",
	addProseMirrorPlugins() {
		return [new k({
			key: new A("editable"),
			props: { editable: () => this.editor.options.editable }
		})];
	}
}), Nd = new A("focusEvents"), Pd = W.create({
	name: "focusEvents",
	addProseMirrorPlugins() {
		let { editor: e } = this;
		return [new k({
			key: Nd,
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
}), Fd = W.create({
	name: "keymap",
	addKeyboardShortcuts() {
		let e = () => this.editor.commands.first(({ commands: e }) => [
			() => e.undoInputRule(),
			() => e.command(({ tr: t }) => {
				let { selection: n, doc: r } = t, { empty: i, $anchor: a } = n, { pos: o, parent: s } = a, c = a.parent.isTextblock && o > 0 ? t.doc.resolve(o - 1) : a, l = c.parent.type.spec.isolating, u = a.pos - a.parentOffset, d = l && c.parent.childCount === 1 ? u === a.pos : E.atStart(r).from === o;
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
		return Sc() || Wc() ? i : r;
	},
	addProseMirrorPlugins() {
		return [new k({
			key: new A("clearDocument"),
			appendTransaction: (e, t, n) => {
				if (e.some((e) => e.getMeta("composition"))) return;
				let r = e.some((e) => e.docChanged) && !t.doc.eq(n.doc), i = e.some((e) => e.getMeta("preventClearDocument"));
				if (!r || i) return;
				let { empty: a, from: o, to: s } = t.selection, c = E.atStart(t.doc).from, l = E.atEnd(t.doc).to;
				if (a || o !== c || s !== l || !$l(n.doc)) return;
				let u = n.tr, d = qs({
					state: n,
					transaction: u
				}), { commands: f } = new Js({
					editor: this.editor,
					state: d
				});
				if (f.clearNodes(), u.steps.length) return u;
			}
		})];
	}
}), Id = W.create({
	name: "paste",
	addProseMirrorPlugins() {
		return [new k({
			key: new A("tiptapPaste"),
			props: { handlePaste: (e, t, n) => {
				this.editor.emit("paste", {
					editor: this.editor,
					event: t,
					slice: n
				});
			} }
		})];
	}
}), Ld = W.create({
	name: "tabindex",
	addOptions() {
		return { value: void 0 };
	},
	addProseMirrorPlugins() {
		return [new k({
			key: new A("tabindex"),
			props: { attributes: () => !this.editor.isEditable && this.options.value === void 0 ? {} : { tabindex: this.options.value ?? "0" } }
		})];
	}
}), Rd = W.create({
	name: "textDirection",
	addOptions() {
		return { direction: void 0 };
	},
	addGlobalAttributes() {
		if (!this.options.direction) return [];
		let { nodeExtensions: e } = yl(this.extensions);
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
		return [new k({
			key: new A("textDirection"),
			props: { attributes: () => {
				let e = this.options.direction;
				return e ? { dir: e } : {};
			} }
		})];
	}
}), zd = !1;
function Bd(e) {
	if (zd) return;
	zd = !0;
	let t;
	try {
		t = gt.fromJSON(e, {
			from: 0,
			to: 0
		}).slice.content;
	} catch {
		return;
	}
	t instanceof a || console.warn("[tiptap warn]: prosemirror-model is loaded more than once. Wrapping and splitting nodes will fail. Deduplicate it in your lock file, or alias it to a single copy in your bundler.");
}
var Vd = class e {
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
}, Hd = ".ProseMirror {\n  position: relative;\n}\n\n.ProseMirror {\n  word-wrap: break-word;\n  white-space: pre-wrap;\n  white-space: break-spaces;\n  -webkit-font-variant-ligatures: none;\n  font-variant-ligatures: none;\n  font-feature-settings: \"liga\" 0; /* the above doesn't seem to work in Edge */\n}\n\n.ProseMirror [contenteditable=\"false\"] {\n  white-space: normal;\n}\n\n.ProseMirror [contenteditable=\"false\"] [contenteditable=\"true\"] {\n  white-space: pre-wrap;\n}\n\n.ProseMirror pre {\n  white-space: pre-wrap;\n}\n\nimg.ProseMirror-separator {\n  display: inline !important;\n  border: none !important;\n  margin: 0 !important;\n  width: 0 !important;\n  height: 0 !important;\n}\n\n.ProseMirror-gapcursor {\n  display: none;\n  pointer-events: none;\n  position: absolute;\n  margin: 0;\n}\n\n.ProseMirror-gapcursor:after {\n  content: \"\";\n  display: block;\n  position: absolute;\n  top: -2px;\n  width: 20px;\n  border-top: 1px solid black;\n  animation: ProseMirror-cursor-blink 1.1s steps(2, start) infinite;\n}\n\n@keyframes ProseMirror-cursor-blink {\n  to {\n    visibility: hidden;\n  }\n}\n\n.ProseMirror-hideselection *::selection {\n  background: transparent;\n}\n\n.ProseMirror-hideselection *::-moz-selection {\n  background: transparent;\n}\n\n.ProseMirror-hideselection * {\n  caret-color: transparent;\n}\n\n.ProseMirror-focused .ProseMirror-gapcursor {\n  display: block;\n}", Ud = class extends Lu {
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
			getUpdatedPosition: nu,
			createMappablePosition: ru
		}, this.setOptions(e), this.createExtensionManager(), this.createCommandManager(), this.createSchema(), this.on("beforeCreate", this.options.onBeforeCreate), this.emit("beforeCreate", { editor: this }), this.on("mount", this.options.onMount), this.on("unmount", this.options.onUnmount), this.on("contentError", this.options.onContentError), this.on("create", this.options.onCreate), this.on("update", this.options.onUpdate), this.on("selectionUpdate", this.options.onSelectionUpdate), this.on("transaction", this.options.onTransaction), this.on("focus", this.options.onFocus), this.on("blur", this.options.onBlur), this.on("destroy", this.options.onDestroy), this.on("drop", ({ event: e, slice: t, moved: n }) => this.options.onDrop(e, t, n)), this.on("paste", ({ event: e, slice: t }) => this.options.onPaste(e, t)), this.on("delete", this.options.onDelete);
		let t = this.createDoc();
		if (!this.editorState) {
			let e = bc(t, this.options.autofocus);
			this.editorState = wn.create({
				doc: t,
				schema: this.schema,
				selection: e || void 0
			});
		}
		Bd(this.schema), this.options.element && this.mount(this.options.element);
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
		return this.commandManager ? this.commandManager.chain() : Js.createFakeChain();
	}
	can() {
		return this.commandManager ? this.commandManager.can() : Js.createFallbackCan();
	}
	injectCSS() {
		this.options.injectCSS && typeof document < "u" && (this.css = ad(Hd, this.options.injectNonce));
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
		return Ru && !this.hasWarnedStaleDecorationRead && Iu(this) && (this.hasWarnedStaleDecorationRead = !0, console.warn("[tiptap warn]: `editor.state` was read while decoration `create()` was running. It returns the pre-transaction document. Use the `state` argument passed to `create()` instead. Helpers like `editor.isActive()` read `editor.state` too, so pass `state` to their standalone versions instead of calling them on the editor.")), this.editorView && (this.editorState = this.view.state), this.editorState;
	}
	registerPlugin(e, t) {
		let n = _l(t) ? t(e, [...this.state.plugins]) : [...this.state.plugins, e], r = this.state.reconfigure({ plugins: n });
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
			Md,
			Od.configure({ blockSeparator: (e = this.options.coreExtensionOptions) == null || (e = e.clipboardTextSerializer) == null ? void 0 : e.blockSeparator }),
			kd,
			Pd,
			Fd,
			Ld.configure({ value: (t = this.options.coreExtensionOptions) == null || (t = t.tabindex) == null ? void 0 : t.value }),
			jd,
			Id,
			Ad,
			Rd.configure({ direction: this.options.textDirection })
		].filter((e) => typeof this.options.enableCoreExtensions != "object" || this.options.enableCoreExtensions[e.name] !== !1) : [], ...this.options.extensions].filter((e) => [
			"extension",
			"node",
			"mark"
		].includes(e?.type));
		this.extensionManager = new Dd(n, this);
	}
	createCommandManager() {
		this.commandManager = new Js({ editor: this });
	}
	createSchema() {
		this.schema = this.extensionManager.schema;
	}
	createDoc() {
		let e;
		try {
			e = cl(this.options.content, this.schema, this.options.parseOptions, { errorOnInvalidContent: this.options.enableContentCheck });
		} catch (e) {
			if (!(e instanceof Error) || !["[tiptap error]: Invalid JSON content", "[tiptap error]: Invalid HTML content"].includes(e.message)) throw e;
			let t = cl(this.options.content, this.schema, this.options.parseOptions, { errorOnInvalidContent: !1 });
			return this.editorState = wn.create({
				doc: t,
				schema: this.schema,
				selection: bc(t, this.options.autofocus) || void 0
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
		this.editorView = new Ds(e, {
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
		return Ll(this.state, e);
	}
	isActive(e, t) {
		let n = typeof e == "string" ? e : null, r = typeof e == "string" ? t : e;
		return Jl(this.state, n, r);
	}
	getJSON() {
		return this.state.doc.toJSON();
	}
	getHTML() {
		return gl(this.state.doc.content, this.schema);
	}
	getText(e) {
		let { blockSeparator: t = "\n\n", textSerializers: n = {} } = e || {};
		return Pl(this.state.doc, {
			blockSeparator: t,
			textSerializers: {
				...Fl(this.schema),
				...n
			}
		});
	}
	get isEmpty() {
		return $l(this.state.doc);
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
		return new Vd(t, this, !1, n);
	}
	get $doc() {
		return this.$pos(0);
	}
};
function Wd(e) {
	return new hd({
		find: e.find,
		handler: ({ state: t, range: n, match: r }) => {
			let i = H(e.getAttributes, void 0, r);
			if (i === !1 || i === null) return null;
			let { tr: a } = t, o = r[r.length - 1], s = r[0];
			if (o) {
				let r = s.search(/\S/), c = n.from + s.indexOf(o), l = c + o.length;
				if (Vl(n.from, n.to, t.doc).filter((t) => t.mark.type.excluded.find((n) => n === e.type && n !== t.mark.type)).filter((e) => e.to > c).length) return null;
				l < n.to && a.delete(l, n.to), c > n.from && a.delete(n.from + r, c);
				let u = n.from + r + o.length;
				a.addMark(n.from + r, u, e.type.create(i || {})), a.removeStoredMark(e.type);
			}
		},
		undoable: e.undoable
	});
}
function Gd(e) {
	return new hd({
		find: e.find,
		handler: ({ state: t, range: n, match: r }) => {
			let i = H(e.getAttributes, void 0, r) || {}, { tr: a } = t, o = n.from, s = n.to, c = e.type.create(i);
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
function Kd(e) {
	return new hd({
		find: e.find,
		handler: ({ state: t, range: n, match: r }) => {
			let i = t.doc.resolve(n.from), a = H(e.getAttributes, void 0, r) || {};
			if (!i.node(-1).canReplaceWith(i.index(-1), i.indexAfter(-1), e.type)) return null;
			t.tr.delete(n.from, n.to).setBlockType(n.from, n.from, e.type, a);
		},
		undoable: e.undoable
	});
}
function qd(e) {
	return new hd({
		find: e.find,
		handler: ({ state: t, range: n, match: r, chain: i }) => {
			let a = H(e.getAttributes, void 0, r) || {}, o = t.tr.delete(n.from, n.to), s = o.doc.resolve(n.from).blockRange(), c = s && Tt(s, e.type, a);
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
			l && l.type === e.type && Ft(o.doc, n.from - 1) && (!e.joinPredicate || e.joinPredicate(r, l)) && o.join(n.from - 1);
		},
		undoable: e.undoable
	});
}
var Jd = (e) => "touches" in e, Yd = class {
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
		e.preventDefault(), e.stopPropagation(), this.isResizing = !0, this.activeHandle = t, Jd(e) ? (this.startX = e.touches[0].clientX, this.startY = e.touches[0].clientY) : (this.startX = e.clientX, this.startY = e.clientY), this.startWidth = this.element.offsetWidth, this.startHeight = this.element.offsetHeight, this.startWidth > 0 && this.startHeight > 0 && (this.aspectRatio = this.startWidth / this.startHeight), this.getPos(), this.container.dataset.resizeState = "true", this.classNames.resizing && this.container.classList.add(this.classNames.resizing), document.addEventListener("mousemove", this.handleMouseMove), document.addEventListener("touchmove", this.handleTouchMove), document.addEventListener("mouseup", this.handleMouseUp), document.addEventListener("keydown", this.handleKeyDown), document.addEventListener("keyup", this.handleKeyUp);
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
}, G = class e extends yd {
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
function Xd(e) {
	return new xd({
		find: e.find,
		handler: ({ state: t, range: n, match: r, pasteEvent: i }) => {
			let a = H(e.getAttributes, void 0, r, i);
			if (a === !1 || a === null) return null;
			let { tr: o } = t, s = r[r.length - 1], c = r[0], l = n.to;
			if (s) {
				let i = c.search(/\S/), u = n.from + c.indexOf(s), d = u + s.length;
				if (Vl(n.from, n.to, t.doc).filter((t) => t.mark.type.excluded.find((n) => n === e.type && n !== t.mark.type)).filter((e) => e.to > u).length) return null;
				d < n.to && o.delete(d, n.to), u > n.from && o.delete(n.from + i, u), l = n.from + i + s.length, o.addMark(n.from + i, l, e.type.create(a || {})), r.index !== void 0 && r.input !== void 0 && r.index + r[0].length >= r.input.length || o.removeStoredMark(e.type);
			}
		}
	});
}
//#endregion
//#region node_modules/@tiptap/core/dist/jsx-runtime/jsx-runtime.js
var Zd = /* @__PURE__ */ new WeakSet(), Qd = /* @__PURE__ */ new WeakSet();
function $d(e) {
	let t = e;
	return Zd.add(t), t;
}
function ef(e) {
	return Array.isArray(e) && Zd.has(e);
}
function tf(e) {
	return e.flatMap((e) => e == null ? [] : Array.isArray(e) && Qd.has(e) && !ef(e) ? tf(e) : [e]);
}
function nf(e, t) {
	if (e === "slot") return 0;
	if (e instanceof Function) {
		let n = e(t);
		return Array.isArray(n) && !ef(n) && !Qd.has(n) ? $d(n) : n;
	}
	let { children: n, ...r } = t ?? {};
	if (e === "svg") throw Error("SVG elements are not supported in the JSX syntax, use the array syntax instead");
	if (Array.isArray(n)) {
		if (ef(n)) return $d([
			e,
			r,
			n
		]);
		if (n.length === 0) return $d([e, r]);
		let t = tf(n);
		return t.length === 0 ? $d([e, r]) : $d([
			e,
			r,
			...t
		]);
	}
	return $d(n == null ? [e, r] : [
		e,
		r,
		n
	]);
}
var rf = (e, t) => nf(e, t), af = (e, t) => {
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
		let n = i.parent.content, r = new d(n, 0, 0);
		return e.replace(l, i.after(), r), e.setSelection(D.create(e.doc, l + n.size)), e.scrollIntoView(), t(e), !0;
	});
}, of = /^\s*>\s$/, sf = G.create({
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
		return /* @__PURE__ */ rf("blockquote", {
			...U(this.options.HTMLAttributes, e),
			children: /* @__PURE__ */ rf("slot", {})
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
			Backspace: () => af(this.editor, this.type)
		};
	},
	addInputRules() {
		return [qd({
			find: of,
			type: this.type
		})];
	}
}), cf = /(?:^|\s)(\*\*(?!\s+\*\*)((?:[^*]+))\*\*(?!\s+\*\*))$/, lf = /(?:^|\s)(\*\*(?!\s+\*\*)((?:[^*]+))\*\*(?!\s+\*\*))/g, uf = /(?:^|\s)(__(?!\s+__)((?:[^_]+))__(?!\s+__))$/, df = /(?:^|\s)(__(?!\s+__)((?:[^_]+))__(?!\s+__))/g, ff = bd.create({
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
		return /* @__PURE__ */ rf("strong", {
			...U(this.options.HTMLAttributes, e),
			children: /* @__PURE__ */ rf("slot", {})
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
		return [Wd({
			find: cf,
			type: this.type
		}), Wd({
			find: uf,
			type: this.type
		})];
	},
	addPasteRules() {
		return [Xd({
			find: lf,
			type: this.type
		}), Xd({
			find: df,
			type: this.type
		})];
	}
}), pf = (e) => {
	let t = /`([^`]+)`(?!`)$/.exec(e);
	return !t || t.index > 0 && e[t.index - 1] === "`" ? null : {
		index: t.index,
		text: t[0],
		replaceWith: t[1]
	};
}, mf = (e) => {
	let t = /`([^`]+)`(?!`)/g, n = [], r;
	for (; (r = t.exec(e)) !== null;) r.index > 0 && e[r.index - 1] === "`" || n.push({
		index: r.index,
		text: r[0],
		replaceWith: r[1]
	});
	return n;
}, hf = bd.create({
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
			U(this.options.HTMLAttributes, e),
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
		return [Wd({
			find: pf,
			type: this.type
		})];
	},
	addPasteRules() {
		return [Xd({
			find: mf,
			type: this.type
		})];
	}
}), gf = 4, _f = /^```([a-z]+)?[\s\n]$/, vf = /^~~~([a-z]+)?[\s\n]$/, yf = G.create({
	name: "codeBlock",
	addOptions() {
		return {
			languageClassPrefix: "language-",
			exitOnTripleEnter: !0,
			exitOnArrowDown: !0,
			exitOnArrowUp: !0,
			defaultLanguage: null,
			enableTabIndentation: !1,
			tabSize: gf,
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
			U(this.options.HTMLAttributes, t),
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
				let t = this.options.tabSize ?? gf, { state: n } = e, { selection: r } = n, { $from: i, empty: a } = r;
				if (i.parent.type !== this.type) return !1;
				let o = " ".repeat(t);
				return a ? e.commands.insertContent(o) : e.commands.command(({ tr: e }) => {
					let { from: t, to: i } = r, a = n.doc.textBetween(t, i, "\n", "\n").split("\n").map((e) => o + e).join("\n");
					return e.replaceWith(t, i, n.schema.text(a)), !0;
				});
			},
			"Shift-Tab": ({ editor: e }) => {
				if (!this.options.enableTabIndentation) return !1;
				let t = this.options.tabSize ?? gf, { state: n } = e, { selection: r } = n, { $from: i, empty: a } = r;
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
					return e.delete(p, p + f), r - p <= f && e.setSelection(D.create(e.doc, p)), !0;
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
				return o === void 0 ? !1 : r.nodeAt(o) ? e.commands.command(({ tr: e }) => (e.setSelection(E.near(r.resolve(o))), !0)) : e.commands.exitCode();
			}
		};
	},
	addInputRules() {
		return [Kd({
			find: _f,
			type: this.type,
			getAttributes: (e) => ({ language: e[1] })
		}), Kd({
			find: vf,
			type: this.type,
			getAttributes: (e) => ({ language: e[1] })
		})];
	},
	addProseMirrorPlugins() {
		return [new k({
			key: new A("codeBlockVSCodeHandler"),
			props: { handlePaste: (e, t) => {
				if (!t.clipboardData || this.editor.isActive(this.type.name)) return !1;
				let n = t.clipboardData.getData("text/plain"), r = t.clipboardData.getData("vscode-editor-data"), i = (r ? JSON.parse(r) : void 0)?.mode;
				if (!n || !i) return !1;
				let { tr: a, schema: o } = e.state, s = o.text(n.replace(/\r\n?/g, "\n"));
				return a.replaceSelectionWith(this.type.create({ language: i }, s)), a.selection.$from.parent.type !== this.type && a.setSelection(D.near(a.doc.resolve(Math.max(0, a.selection.from - 2)))), a.setMeta("paste", !0), e.dispatch(a), !0;
			} }
		})];
	}
}), bf = G.create({
	name: "doc",
	topNode: !0,
	content: "block+",
	renderMarkdown: (e, t) => e.content ? t.renderChildren(e.content, "\n\n") : ""
}), xf = G.create({
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
		return ["br", U(this.options.HTMLAttributes, e)];
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
}), Sf = G.create({
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
			U(this.options.HTMLAttributes, t),
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
		return this.options.levels.map((e) => Kd({
			find: RegExp(`^(#{${Math.min(...this.options.levels)},${e}})\\s$`),
			type: this.type,
			getAttributes: { level: e }
		}));
	}
}), Cf = G.create({
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
		return ["hr", U(this.options.HTMLAttributes, e)];
	},
	markdownTokenName: "hr",
	parseMarkdown: (e, t) => t.createNode("horizontalRule"),
	renderMarkdown: () => "---",
	addCommands() {
		return { setHorizontalRule: () => ({ chain: e, state: t }) => {
			if (!id(t, t.schema.nodes[this.name])) return !1;
			let { selection: n } = t, { $to: r } = n, i = e();
			return eu(n) ? i.insertContentAt(r.pos, { type: this.name }) : i.insertContent({ type: this.name }), i.command(({ state: e, tr: t, dispatch: n }) => {
				if (n) {
					let { $to: n } = t.selection, r = n.end();
					if (n.nodeAfter) n.nodeAfter.isTextblock ? t.setSelection(D.create(t.doc, n.pos + 1)) : n.nodeAfter.isBlock ? t.setSelection(O.create(t.doc, n.pos)) : t.setSelection(D.create(t.doc, n.pos));
					else {
						let i = (e.schema.nodes[this.options.nextNodeType] || n.parent.type.contentMatch.defaultType)?.create();
						i && (t.insert(r, i), t.setSelection(D.create(t.doc, r + 1)));
					}
					t.scrollIntoView();
				}
				return !0;
			}).run();
		} };
	},
	addInputRules() {
		return [Gd({
			find: /^(?:---|—-|___\s|\*\*\*\s)$/,
			type: this.type
		})];
	}
}), wf = /(?:^|\s)(\*(?!\s+\*)((?:[^*]+))\*(?!\s+\*))$/, Tf = /(?:^|\s)(\*(?!\s+\*)((?:[^*]+))\*(?!\s+\*))/g, Ef = /(?:^|\s)(_(?!\s+_)((?:[^_]+))_(?!\s+_))$/, Df = /(?:^|\s)(_(?!\s+_)((?:[^_]+))_(?!\s+_))/g, Of = bd.create({
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
			U(this.options.HTMLAttributes, e),
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
		return [Wd({
			find: wf,
			type: this.type
		}), Wd({
			find: Ef,
			type: this.type
		})];
	},
	addPasteRules() {
		return [Xd({
			find: Tf,
			type: this.type
		}), Xd({
			find: Df,
			type: this.type
		})];
	}
}), kf = "aaa1rp3bb0ott3vie4c1le2ogado5udhabi7c0ademy5centure6ountant0s9o1tor4d0s1ult4e0g1ro2tna4f0l1rica5g0akhan5ency5i0g1rbus3force5tel5kdn3l0ibaba4pay4lfinanz6state5y2sace3tom5m0azon4ericanexpress7family11x2fam3ica3sterdam8nalytics7droid5quan4z2o0l2partments8p0le4q0uarelle8r0ab1mco4chi3my2pa2t0e3s0da2ia2sociates9t0hleta5torney7u0ction5di0ble3o3spost5thor3o0s4w0s2x0a2z0ure5ba0by2idu3namex4d1k2r0celona5laycard4s5efoot5gains6seball5ketball8uhaus5yern5b0c1t1va3cg1n2d1e0ats2uty4er2rlin4st0buy5t2f1g1h0arti5i0ble3d1ke2ng0o3o1z2j1lack0friday9ockbuster8g1omberg7ue3m0s1w2n0pparibas9o0ats3ehringer8fa2m1nd2o0k0ing5sch2tik2on4t1utique6x2r0adesco6idgestone9oadway5ker3ther5ussels7s1t1uild0ers6siness6y1zz3v1w1y1z0h3ca0b1fe2l0l1vinklein9m0era3p2non3petown5ital0one8r0avan4ds2e0er0s4s2sa1e1h1ino4t0ering5holic7ba1n1re3c1d1enter4o1rn3f0a1d2g1h0anel2nel4rity4se2t2eap3intai5ristmas6ome4urch5i0priani6rcle4sco3tadel4i0c2y3k1l0aims4eaning6ick2nic1que6othing5ud3ub0med6m1n1o0ach3des3ffee4llege4ogne5m0mbank4unity6pany2re3uter5sec4ndos3struction8ulting7tact3ractors9oking4l1p2rsica5untry4pon0s4rses6pa2r0edit0card4union9icket5own3s1uise0s6u0isinella9v1w1x1y0mru3ou3z2dad1nce3ta1e1ing3sun4y2clk3ds2e0al0er2s3gree4livery5l1oitte5ta3mocrat6ntal2ist5si0gn4v2hl2iamonds6et2gital5rect0ory7scount3ver5h2y2j1k1m1np2o0cs1tor4g1mains5t1wnload7rive4tv2ubai3pont4rban5vag2r2z2earth3t2c0o2deka3u0cation8e1g1mail3erck5nergy4gineer0ing9terprises10pson4quipment8r0icsson6ni3s0q1tate5t1u0rovision8s2vents5xchange6pert3osed4ress5traspace10fage2il1rwinds6th3mily4n0s2rm0ers5shion4t3edex3edback6rrari3ero6i0delity5o2lm2nal1nce1ial7re0stone6mdale6sh0ing5t0ness6j1k1lickr3ghts4r2orist4wers5y2m1o0o0d1tball6rd1ex2sale4um3undation8x2r0ee1senius7l1ogans4ntier7tr2ujitsu5n0d2rniture7tbol5yi3ga0l0lery3o1up4me0s3p1rden4y2b0iz3d0n2e0a1nt0ing5orge5f1g0ee3h1i0ft0s3ves2ing5l0ass3e1obal2o4m0ail3bh2o1x2n1odaddy5ld0point6f2odyear5g0le4p1t1v2p1q1r0ainger5phics5tis4een3ipe3ocery4up4s1t1u0cci3ge2ide2tars5ru3w1y2hair2mburg5ngout5us3bo2dfc0bank7ealth0care8lp1sinki6re1mes5iphop4samitsu7tachi5v2k0t2m1n1ockey4ldings5iday5medepot5goods5s0ense7nda3rse3spital5t0ing5t0els3mail5use3w2r1sbc3t1u0ghes5yatt3undai7ibm2cbc2e1u2d1e0ee3fm2kano4l1m0amat4db2mo0bilien9n0c1dustries8finiti5o2g1k1stitute6urance4e4t0ernational10uit4vestments10o1piranga7q1r0ish4s0maili5t0anbul7t0au2v3jaguar4va3cb2e0ep2tzt3welry6io2ll2m0p2nj2o0bs1urg4t1y2p0morgan6rs3uegos4niper7kaufen5ddi3e0rryhotels6properties14fh2g1h1i0a1ds2m1ndle4tchen5wi3m1n1oeln3matsu5sher5p0mg2n2r0d1ed3uokgroup8w1y0oto4z2la0caixa5mborghini8er3nd0rover6xess5salle5t0ino3robe5w0yer5b1c1ds2ease3clerc5frak4gal2o2xus4gbt3i0dl2fe0insurance9style7ghting6ke2lly3mited4o2ncoln4k2ve1ing5k1lc1p2oan0s3cker3us3l1ndon4tte1o3ve3pl0financial11r1s1t0d0a3u0ndbeck6xe1ury5v1y2ma0drid4if1son4keup4n0agement7go3p1rket0ing3s4riott5shalls7ttel5ba2c0kinsey7d1e0d0ia3et2lbourne7me1orial6n0u2rck0msd7g1h1iami3crosoft7l1ni1t2t0subishi9k1l0b1s2m0a2n1o0bi0le4da2e1i1m1nash3ey2ster5rmon3tgage6scow4to0rcycles9v0ie4p1q1r1s0d2t0n1r2u0seum3ic4v1w1x1y1z2na0b1goya4me2vy3ba2c1e0c1t0bank4flix4work5ustar5w0s2xt0direct7us4f0l2g0o2hk2i0co2ke1on3nja3ssan1y5l1o0kia3rton4w0ruz3tv4p1r0a1w2tt2u1yc2z2obi1server7ffice5kinawa6layan0group9lo3m0ega4ne1g1l0ine5oo2pen3racle3nge4g0anic5igins6saka4tsuka4t2vh3pa0ge2nasonic7ris2s1tners4s1y3y2ccw3e0t2f0izer5g1h0armacy6d1ilips5one2to0graphy6s4ysio5ics1tet2ures6d1n0g1k2oneer5zza4k1l0ace2y0station9umbing5s3m1n0c2ohl2ker3litie5rn2st3r0axi3ess3ime3o0d0uctions8f1gressive8mo2perties3y5tection8u0dential9s1t1ub2w0c2y2qa1pon3uebec3st5racing4dio4e0ad1lestate6tor2y4cipes5d0umbrella9hab3ise0n3t2liance6n0t0als5pair3ort3ublican8st0aurant8view0s5xroth6ich0ardli6oh3l1o1p2o0cks3deo3gers4om3s0vp3u0gby3hr2n2w0e2yukyu6sa0arland6fe0ty4kura4le1on3msclub4ung5ndvik0coromant12ofi4p1rl2s1ve2xo3b0i1s2c0b1haeffler7midt4olarships8ol3ule3warz5ience5ot3d1e0arch3t2cure1ity6ek2lect4ner3rvices6ven3w1x0y3fr2g1h0angrila6rp3ell3ia1ksha5oes2p0ping5uji3w3i0lk2na1gles5te3j1k0i0n2y0pe4l0ing4m0art3ile4n0cf3o0ccer3ial4ftbank4ware6hu2lar2utions7ng1y2y2pa0ce3ort2t3r0l2s1t0ada2ples4r1tebank4farm7c0group6ockholm6rage3e3ream4udio2y3yle4u0cks3pplies3y2ort5rf1gery5zuki5v1watch4iss4x1y0dney4stems6z2tab1ipei4lk2obao4rget4tamotors6r2too4x0i3c0i2d0k2eam2ch0nology8l1masek5nnis4va3f1g1h0d1eater2re6iaa2ckets5enda4ps2res2ol4j0maxx4x2k0maxx5l1m0all4n1o0day3kyo3ols3p1ray3shiba5tal3urs3wn2yota3s3r0ade1ing4ining5vel0ers0insurance16ust3v2t1ube2i1nes3shu4v0s2w1z2ua1bank3s2g1k1nicom3versity8o2ol2ps2s1y1z2va0cations7na1guard7c1e0gas3ntures6risign5mögensberater2ung14sicherung10t2g1i0ajes4deo3g1king4llas4n1p1rgin4sa1ion4va1o3laanderen9n1odka3lvo3te1ing3o2yage5u2wales2mart4ter4ng0gou5tch0es6eather0channel12bcam3er2site5d0ding5ibo2r3f1hoswho6ien2ki2lliamhill9n0dows4e1ners6me2oodside6rk0s2ld3w2s1tc1f3xbox3erox4ihuan4n2xx2yz3yachts4hoo3maxun5ndex5e1odobashi7ga2kohama6u0tube6t1un3za0ppos4ra3ero3ip2m1one3uerich6w2", Af = "ελ1υ2бг1ел3дети4ею2католик6ом3мкд2он1сква6онлайн5рг3рус2ф2сайт3рб3укр3қаз3հայ3ישראל5קום3ابوظبي5رامكو5لاردن4بحرين5جزائر5سعودية6عليان5مغرب5مارات5یران5بارت2زار4يتك3ھارت5تونس4سودان3رية5شبكة4عراق2ب2مان4فلسطين6قطر3كاثوليك6وم3مصر2ليسيا5وريتانيا7قع4همراه5پاکستان7ڀارت4कॉम3नेट3भारत0म्3ोत5संगठन5বাংলা5ভারত2ৰত4ਭਾਰਤ4ભારત4ଭାରତ4இந்தியா6லங்கை6சிங்கப்பூர்11భారత్5ಭಾರತ4ഭാരതം5ලංකා4คอม3ไทย3ລາວ3გე2みんな3アマゾン4クラウド4グーグル4コム2ストア3セール3ファッション6ポイント4世界2中信1国1國1文网3亚马逊3企业2佛山2信息2健康2八卦2公司1益2台湾1灣2商城1店1标2嘉里0大酒店5在线2大拿2天主教3娱乐2家電2广东2微博2慈善2我爱你3手机2招聘2政务1府2新加坡2闻2时尚2書籍2机构2淡马锡3游戏2澳門2点看2移动2组织机构4网址1店1站1络2联通2谷歌2购物2通販2集团2電訊盈科4飞利浦3食品2餐厅2香格里拉3港2닷넷1컴2삼성2한국2", jf = "numeric", Mf = "ascii", Nf = "alpha", Pf = "asciinumeric", Ff = "alphanumeric", If = "domain", Lf = "emoji", Rf = "scheme", zf = "slashscheme", Bf = "whitespace";
function Vf(e, t) {
	return e in t || (t[e] = []), t[e];
}
function Hf(e, t, n) {
	t[jf] && (t[Pf] = !0, t[Ff] = !0), t[Mf] && (t[Pf] = !0, t[Nf] = !0), t[Pf] && (t[Ff] = !0), t[Nf] && (t[Ff] = !0), t[Ff] && (t[If] = !0), t[Lf] && (t[If] = !0);
	for (let r in t) {
		let t = Vf(r, n);
		t.indexOf(e) < 0 && t.push(e);
	}
}
function Uf(e, t) {
	let n = {};
	for (let r in t) t[r].indexOf(e) >= 0 && (n[r] = !0);
	return n;
}
function Wf(e = null) {
	this.j = {}, this.jr = [], this.jd = null, this.t = e;
}
Wf.groups = {}, Wf.prototype = {
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
		r = r || Wf.groups;
		let i;
		return t && t.j ? i = t : (i = new Wf(t), n && r && Hf(t, n, r)), this.jr.push([e, i]), i;
	},
	ts(e, t, n, r) {
		let i = this, a = e.length;
		if (!a) return i;
		for (let t = 0; t < a - 1; t++) i = i.tt(e[t]);
		return i.tt(e[a - 1], t, n, r);
	},
	tt(e, t, n, r) {
		r = r || Wf.groups;
		let i = this;
		if (t && t.j) return i.j[e] = t, t;
		let a = t, o, s = i.go(e);
		return s ? (o = new Wf(), Object.assign(o.j, s.j), o.jr.push.apply(o.jr, s.jr), o.jd = s.jd, o.t = s.t) : o = new Wf(), a && (r && (o.t && typeof o.t == "string" ? Hf(a, Object.assign(Uf(o.t, r), n), r) : n && Hf(a, n, r)), o.t = a), i.j[e] = o, o;
	}
};
var K = (e, t, n, r, i) => e.ta(t, n, r, i), q = (e, t, n, r, i) => e.tr(t, n, r, i), Gf = (e, t, n, r, i) => e.ts(t, n, r, i), J = (e, t, n, r, i) => e.tt(t, n, r, i), Kf = "WORD", qf = "UWORD", Jf = "ASCIINUMERICAL", Yf = "ALPHANUMERICAL", Xf = "LOCALHOST", Zf = "TLD", Qf = "UTLD", $f = "SCHEME", ep = "SLASH_SCHEME", tp = "NUM", np = "WS", rp = "NL", ip = "OPENBRACE", ap = "CLOSEBRACE", op = "OPENBRACKET", sp = "CLOSEBRACKET", cp = "OPENPAREN", lp = "CLOSEPAREN", up = "OPENANGLEBRACKET", dp = "CLOSEANGLEBRACKET", fp = "FULLWIDTHLEFTPAREN", pp = "FULLWIDTHRIGHTPAREN", mp = "LEFTCORNERBRACKET", hp = "RIGHTCORNERBRACKET", gp = "LEFTWHITECORNERBRACKET", _p = "RIGHTWHITECORNERBRACKET", vp = "FULLWIDTHLESSTHAN", yp = "FULLWIDTHGREATERTHAN", bp = "AMPERSAND", xp = "APOSTROPHE", Sp = "ASTERISK", Cp = "AT", wp = "BACKSLASH", Tp = "BACKTICK", Ep = "CARET", Dp = "COLON", Op = "COMMA", kp = "DOLLAR", Ap = "DOT", jp = "EQUALS", Mp = "EXCLAMATION", Np = "HYPHEN", Pp = "PERCENT", Fp = "PIPE", Ip = "PLUS", Lp = "POUND", Rp = "QUERY", zp = "QUOTE", Bp = "FULLWIDTHMIDDLEDOT", Vp = "SEMI", Hp = "SLASH", Up = "TILDE", Wp = "UNDERSCORE", Gp = "EMOJI", Kp = "SYM", qp = /*#__PURE__*/ Object.freeze({
	__proto__: null,
	ALPHANUMERICAL: Yf,
	AMPERSAND: bp,
	APOSTROPHE: xp,
	ASCIINUMERICAL: Jf,
	ASTERISK: Sp,
	AT: Cp,
	BACKSLASH: wp,
	BACKTICK: Tp,
	CARET: Ep,
	CLOSEANGLEBRACKET: dp,
	CLOSEBRACE: ap,
	CLOSEBRACKET: sp,
	CLOSEPAREN: lp,
	COLON: Dp,
	COMMA: Op,
	DOLLAR: kp,
	DOT: Ap,
	EMOJI: Gp,
	EQUALS: jp,
	EXCLAMATION: Mp,
	FULLWIDTHGREATERTHAN: yp,
	FULLWIDTHLEFTPAREN: fp,
	FULLWIDTHLESSTHAN: vp,
	FULLWIDTHMIDDLEDOT: Bp,
	FULLWIDTHRIGHTPAREN: pp,
	HYPHEN: Np,
	LEFTCORNERBRACKET: mp,
	LEFTWHITECORNERBRACKET: gp,
	LOCALHOST: Xf,
	NL: rp,
	NUM: tp,
	OPENANGLEBRACKET: up,
	OPENBRACE: ip,
	OPENBRACKET: op,
	OPENPAREN: cp,
	PERCENT: Pp,
	PIPE: Fp,
	PLUS: Ip,
	POUND: Lp,
	QUERY: Rp,
	QUOTE: zp,
	RIGHTCORNERBRACKET: hp,
	RIGHTWHITECORNERBRACKET: _p,
	SCHEME: $f,
	SEMI: Vp,
	SLASH: Hp,
	SLASH_SCHEME: ep,
	SYM: Kp,
	TILDE: Up,
	TLD: Zf,
	UNDERSCORE: Wp,
	UTLD: Qf,
	UWORD: qf,
	WORD: Kf,
	WS: np
}), Jp = /[a-z]/, Yp = /\p{L}/u, Xp = /\p{Emoji}/u, Zp = /\d/, Qp = /\s/, $p = "\r", em = "\n", tm = "️", nm = "‍", rm = "￼", im = null, am = null;
function om(e = []) {
	let t = {};
	Wf.groups = t;
	let n = new Wf();
	im ?? (im = um(kf)), am ?? (am = um(Af)), J(n, "'", xp), J(n, "{", ip), J(n, "}", ap), J(n, "[", op), J(n, "]", sp), J(n, "(", cp), J(n, ")", lp), J(n, "<", up), J(n, ">", dp), J(n, "（", fp), J(n, "）", pp), J(n, "「", mp), J(n, "」", hp), J(n, "『", gp), J(n, "』", _p), J(n, "＜", vp), J(n, "＞", yp), J(n, "&", bp), J(n, "*", Sp), J(n, "@", Cp), J(n, "`", Tp), J(n, "^", Ep), J(n, ":", Dp), J(n, ",", Op), J(n, "$", kp), J(n, ".", Ap), J(n, "=", jp), J(n, "!", Mp), J(n, "-", Np), J(n, "%", Pp), J(n, "|", Fp), J(n, "+", Ip), J(n, "#", Lp), J(n, "?", Rp), J(n, "\"", zp), J(n, "/", Hp), J(n, ";", Vp), J(n, "~", Up), J(n, "_", Wp), J(n, "\\", wp), J(n, "・", Bp);
	let r = q(n, Zp, tp, { [jf]: !0 });
	q(r, Zp, r);
	let i = q(r, Jp, Jf, { [Pf]: !0 }), a = q(r, Yp, Yf, { [Ff]: !0 }), o = q(n, Jp, Kf, { [Mf]: !0 });
	q(o, Zp, i), q(o, Jp, o), q(i, Zp, i), q(i, Jp, i);
	let s = q(n, Yp, qf, { [Nf]: !0 });
	q(s, Jp), q(s, Zp, a), q(s, Yp, s), q(a, Zp, a), q(a, Jp), q(a, Yp, a);
	let c = J(n, em, rp, { [Bf]: !0 }), l = J(n, $p, np, { [Bf]: !0 }), u = q(n, Qp, np, { [Bf]: !0 });
	J(n, rm, u), J(l, em, c), J(l, rm, u), q(l, Qp, u), J(u, $p), J(u, em), q(u, Qp, u), J(u, rm, u);
	let d = q(n, Xp, Gp, { [Lf]: !0 });
	J(d, "#"), q(d, Xp, d), J(d, tm, d);
	let f = J(d, nm);
	J(f, "#"), q(f, Xp, d);
	let p = [[Jp, o], [Zp, i]], m = [
		[Jp, null],
		[Yp, s],
		[Zp, a]
	];
	for (let e = 0; e < im.length; e++) lm(n, im[e], Zf, Kf, p);
	for (let e = 0; e < am.length; e++) lm(n, am[e], Qf, qf, m);
	Hf(Zf, {
		tld: !0,
		ascii: !0
	}, t), Hf(Qf, {
		utld: !0,
		alpha: !0
	}, t), lm(n, "file", $f, Kf, p), lm(n, "mailto", $f, Kf, p), lm(n, "http", ep, Kf, p), lm(n, "https", ep, Kf, p), lm(n, "ftp", ep, Kf, p), lm(n, "ftps", ep, Kf, p), Hf($f, {
		scheme: !0,
		ascii: !0
	}, t), Hf(ep, {
		slashscheme: !0,
		ascii: !0
	}, t), e = e.sort((e, t) => e[0] > t[0] ? 1 : -1);
	for (let t = 0; t < e.length; t++) {
		let r = e[t][0], i = e[t][1] ? { [Rf]: !0 } : { [zf]: !0 };
		r.indexOf("-") >= 0 ? i[If] = !0 : Jp.test(r) ? Zp.test(r) ? i[Pf] = !0 : i[Mf] = !0 : i[jf] = !0, Gf(n, r, r, i);
	}
	return Gf(n, "localhost", Xf, { ascii: !0 }), n.jd = new Wf(Kp), {
		start: n,
		tokens: Object.assign({ groups: t }, qp)
	};
}
function sm(e, t) {
	let n = cm(t.replace(/[A-Z]/g, (e) => e.toLowerCase())), r = n.length, i = [], a = 0, o = 0;
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
function cm(e) {
	let t = [], n = e.length, r = 0;
	for (; r < n;) {
		let i = e.charCodeAt(r), a, o = i < 55296 || i > 56319 || r + 1 === n || (a = e.charCodeAt(r + 1)) < 56320 || a > 57343 ? e[r] : e.slice(r, r + 2);
		t.push(o), r += o.length;
	}
	return t;
}
function lm(e, t, n, r, i) {
	let a, o = t.length;
	for (let n = 0; n < o - 1; n++) {
		let o = t[n];
		e.j[o] ? a = e.j[o] : (a = new Wf(r), a.jr = i.slice(), e.j[o] = a), e = a;
	}
	return a = new Wf(n), a.jr = i.slice(), e.j[t[o - 1]] = a, a;
}
function um(e) {
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
var dm = {
	defaultProtocol: "http",
	events: null,
	format: pm,
	formatHref: pm,
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
function fm(e, t = null) {
	let n = Object.assign({}, dm);
	e && (n = Object.assign(n, e instanceof fm ? e.o : e));
	let r = n.ignoreTags, i = [];
	for (let e = 0; e < r.length; e++) i.push(r[e].toUpperCase());
	this.o = n, t && (this.defaultRender = t), this.ignoreTags = i;
}
fm.prototype = {
	o: dm,
	ignoreTags: [],
	defaultRender(e) {
		return e;
	},
	check(e) {
		return this.get("validate", e.toString(), e);
	},
	get(e, t, n) {
		let r = t != null, i = this.o[e];
		return i && (typeof i == "object" ? (i = n.t in i ? i[n.t] : dm[e], typeof i == "function" && r && (i = i(t, n))) : typeof i == "function" && r && (i = i(t, n.t, n)), i);
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
function pm(e) {
	return e;
}
function mm(e, t) {
	this.t = "token", this.v = e, this.tk = t;
}
mm.prototype = {
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
	toObject(e = dm.defaultProtocol) {
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
function hm(e, t) {
	class n extends mm {
		constructor(t, n) {
			super(t, n), this.t = e;
		}
	}
	for (let e in t) n.prototype[e] = t[e];
	return n.t = e, n;
}
var gm = hm("email", {
	isLink: !0,
	toHref() {
		return "mailto:" + this.toString();
	}
}), _m = hm("text"), vm = hm("nl"), ym = hm("url", {
	isLink: !0,
	toHref(e = dm.defaultProtocol) {
		return this.hasProtocol() ? this.v : `${e}://${this.v}`;
	},
	hasProtocol() {
		let e = this.tk;
		return e.length >= 2 && e[0].t !== Xf && e[1].t === Dp;
	}
}), bm = (e) => new Wf(e);
function xm({ groups: e }) {
	let t = e.domain.concat([
		bp,
		Sp,
		Cp,
		wp,
		Tp,
		Ep,
		kp,
		jp,
		Np,
		tp,
		Pp,
		Fp,
		Ip,
		Lp,
		Hp,
		Kp,
		Up,
		Wp
	]), n = [
		xp,
		Dp,
		Op,
		Ap,
		Mp,
		Pp,
		Rp,
		zp,
		Vp,
		up,
		dp,
		ip,
		ap,
		sp,
		op,
		cp,
		lp,
		fp,
		pp,
		mp,
		hp,
		gp,
		_p,
		vp,
		yp
	], r = [
		bp,
		xp,
		Sp,
		wp,
		Tp,
		Ep,
		kp,
		jp,
		Np,
		ip,
		ap,
		Pp,
		Fp,
		Ip,
		Lp,
		Rp,
		Hp,
		Kp,
		Up,
		Wp
	], i = bm(), a = J(i, Up);
	K(a, r, a), K(a, e.domain, a);
	let o = bm(), s = bm(), c = bm();
	K(i, e.domain, o), K(i, e.scheme, s), K(i, e.slashscheme, c), K(o, r, a), K(o, e.domain, o);
	let l = J(o, Cp);
	J(a, Cp, l), J(s, Cp, l), J(c, Cp, l);
	let u = J(a, Ap);
	K(u, r, a), K(u, e.domain, a);
	let d = bm();
	K(l, e.domain, d), K(d, e.domain, d);
	let f = J(d, Ap);
	K(f, e.domain, d);
	let p = bm(gm);
	K(f, e.tld, p), K(f, e.utld, p), J(l, Xf, p);
	let m = J(d, Np);
	J(m, Np, m), K(m, e.domain, d), K(p, e.domain, d), J(p, Ap, f), J(p, Np, m);
	let h = J(o, Np), g = J(o, Ap);
	J(h, Np, h), K(h, e.domain, o), K(g, r, a), K(g, e.domain, o);
	let _ = bm(ym);
	K(g, e.tld, _), K(g, e.utld, _), K(_, e.domain, o), K(_, r, a), J(_, Ap, g), J(_, Np, h), J(_, Cp, l);
	let v = J(_, Dp), y = bm(ym);
	K(v, e.numeric, y);
	let b = bm(ym), x = bm();
	K(b, t, b), K(b, n, x), K(x, t, b), K(x, n, x), J(_, Hp, b), J(y, Hp, b);
	let S = J(s, Dp), ee = J(J(J(c, Dp), Hp), Hp);
	K(s, e.domain, o), J(s, Ap, g), J(s, Np, h), K(c, e.domain, o), J(c, Ap, g), J(c, Np, h), K(S, e.domain, b), J(S, Hp, b), J(S, Rp, b), K(ee, e.domain, b), K(ee, t, b), J(ee, Hp, b);
	let te = [
		[ip, ap],
		[op, sp],
		[cp, lp],
		[up, dp],
		[fp, pp],
		[mp, hp],
		[gp, _p],
		[vp, yp]
	];
	for (let e = 0; e < te.length; e++) {
		let [r, i] = te[e], a = J(b, r);
		J(x, r, a);
		let o = bm(ym);
		K(a, t, o);
		let s = bm();
		K(a, n, s), J(a, i, b), K(o, t, o), K(o, n, s), K(s, t, o), K(s, n, s), J(o, i, b), J(s, i, b);
	}
	return J(i, Xf, _), J(i, rp, vm), {
		start: i,
		tokens: qp
	};
}
function Sm(e, t, n) {
	let r = n.length, i = 0, a = [], o = [];
	for (; i < r;) {
		let s = e, c = null, l = null, u = 0, d = null, f = -1;
		for (; i < r && !(c = s.go(n[i].t));) o.push(n[i++]);
		for (; i < r && (l = c || s.go(n[i].t));) c = null, s = l, s.accepts() ? (f = 0, d = s) : f >= 0 && f++, i++, u++;
		if (f < 0) i -= u, i < r && (o.push(n[i]), i++);
		else {
			o.length > 0 && (a.push(Cm(_m, t, o)), o = []), i -= f, u -= f;
			let e = d.t, r = n.slice(i - u, i);
			a.push(Cm(e, t, r));
		}
	}
	return o.length > 0 && a.push(Cm(_m, t, o)), a;
}
function Cm(e, t, n) {
	let r = n[0].s, i = n[n.length - 1].e;
	return new e(t.slice(r, i), n);
}
var wm = typeof console < "u" && console && console.warn || (() => {}), Tm = "until manual call of linkify.init(). Register all schemes and plugins before invoking linkify the first time.", Y = {
	scanner: null,
	parser: null,
	tokenQueue: [],
	pluginQueue: [],
	customSchemes: [],
	initialized: !1
};
function Em() {
	return Wf.groups = {}, Y.scanner = null, Y.parser = null, Y.tokenQueue = [], Y.pluginQueue = [], Y.customSchemes = [], Y.initialized = !1, Y;
}
function Dm(e, t = !1) {
	if (Y.initialized && wm(`linkifyjs: already initialized - will not register custom scheme "${e}" ${Tm}`), !/^[0-9a-z]+(-[0-9a-z]+)*$/.test(e)) throw Error("linkifyjs: incorrect scheme format.\n1. Must only contain digits, lowercase ASCII letters or \"-\"\n2. Cannot start or end with \"-\"\n3. \"-\" cannot repeat");
	Y.customSchemes.push([e, t]);
}
function Om() {
	Y.scanner = om(Y.customSchemes);
	for (let e = 0; e < Y.tokenQueue.length; e++) Y.tokenQueue[e][1]({ scanner: Y.scanner });
	Y.parser = xm(Y.scanner.tokens);
	for (let e = 0; e < Y.pluginQueue.length; e++) Y.pluginQueue[e][1]({
		scanner: Y.scanner,
		parser: Y.parser
	});
	return Y.initialized = !0, Y;
}
function km(e) {
	return Y.initialized || Om(), Sm(Y.parser.start, e, sm(Y.scanner.start, e));
}
km.scan = sm;
function Am(e, t = null, n = null) {
	if (t && typeof t == "object") {
		if (n) throw Error(`linkifyjs: Invalid link type ${t}; must be a string`);
		n = t, t = null;
	}
	let r = new fm(n), i = km(e), a = [];
	for (let e = 0; e < i.length; e++) {
		let n = i[e];
		n.isLink && (!t || n.t === t) && r.check(n) && a.push(n.toFormattedObject(r));
	}
	return a;
}
//#endregion
//#region node_modules/@tiptap/extension-link/dist/index.js
var jm = "[\0- \xA0 ᠎ -\u2029 　]", Mm = new RegExp(jm), Nm = RegExp(`${jm}$`), Pm = new RegExp(jm, "g");
function Fm(e) {
	return e.length === 1 ? e[0].isLink : e.length === 3 && e[1].isLink ? ["()", "[]"].includes(e[0].value + e[2].value) : !1;
}
function Im(e) {
	return new k({
		key: new A("autolink"),
		appendTransaction: (t, n, r) => {
			let i = t.some((e) => e.docChanged) && !n.doc.eq(r.doc), a = t.some((e) => e.getMeta("preventAutolink"));
			if (!i || a) return;
			let { tr: o } = r;
			if (Bl(dl(n.doc, [...t])).forEach(({ newRange: t }) => {
				let n = fl(r.doc, t, (e) => e.isTextblock), i, a;
				if (n.length > 1) i = n[0], a = r.doc.textBetween(i.pos, i.pos + i.node.nodeSize, void 0, " ");
				else if (n.length) {
					let e = r.doc.textBetween(t.from, t.to, " ", " ");
					if (!Nm.test(e)) return;
					i = n[0], a = r.doc.textBetween(i.pos, t.to, void 0, " ");
				}
				if (i && a) {
					let t = a.split(Mm).filter(Boolean);
					if (t.length <= 0) return !1;
					let n = t[t.length - 1], s = i.pos + a.lastIndexOf(n);
					if (!n) return !1;
					let c = km(n).map((t) => t.toObject(e.defaultProtocol));
					if (!Fm(c)) return !1;
					c.filter((e) => e.isLink).map((e) => ({
						...e,
						from: s + e.start + 1,
						to: s + e.end + 1
					})).filter((e) => !r.schema.marks.code || !r.doc.rangeHasMark(e.from, e.to, r.schema.marks.code)).filter((t) => e.validate(t.value)).filter((t) => e.shouldAutoLink(t.value)).forEach((t) => {
						Vl(t.from, t.to, r.doc).some((t) => t.mark.type === e.type) || o.addMark(t.from, t.to, e.type.create({ href: t.href }));
					});
				}
			}), o.steps.length) return o;
		}
	});
}
function Lm(e) {
	return new k({
		key: new A("handleClickLink"),
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
				let n = Ll(t.state, e.type.name), r = i.href ?? n.href, o = i.target ?? n.target;
				r && (window.open(r, o), a = !0);
			}
			return a;
		} }
	});
}
var Rm = /\[([^[\]]+)\]\(((?:[^\s()]|\([^\s()]*\))+)(?:\s+(?:(["'])(.*?)\3|“(.*?)”|‘(.*?)’))?\)$/, zm = /\[([^[\]]+)\]\(((?:[^\s()]|\([^\s()]*\))+)(?:\s+(?:(["'])(.*?)\3|“(.*?)”|‘(.*?)’))?\)/g;
function Bm(e, t) {
	let n = 0;
	for (let r = t - 1; r >= 0 && e[r] === "\\"; --r) n += 1;
	return n % 2 == 1;
}
function Vm(e, t) {
	let n = 0, r = 0;
	for (; r < t;) {
		if (e[r] !== "`") {
			r += 1;
			continue;
		}
		if (n === 0 && Bm(e, r)) {
			r += 1;
			continue;
		}
		let i = 0;
		for (; r < t && e[r] === "`";) i += 1, r += 1;
		n === 0 ? n = i : i === n && (n = 0);
	}
	return n > 0;
}
function Hm(e, t, n) {
	let [, r, i] = t;
	return (t.index ? e[t.index - 1] : void 0) === "!" || Bm(e, t.index ?? 0) || Vm(e, t.index ?? 0) ? !1 : !!r.trim() && n(i);
}
function Um(e) {
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
function Wm(e, t) {
	return e.index < t.index + t.text.length && t.index < e.index + e.text.length;
}
function Gm(e) {
	return {
		href: e.data?.href,
		title: e.data?.title ?? null
	};
}
function Km(e) {
	let t = Wd({
		find: (t) => {
			let n = Rm.exec(t);
			return !n || !Hm(t, n, e.isAllowedHref) ? null : Um(n);
		},
		type: e.type,
		getAttributes: Gm
	});
	return new hd({
		find: t.find,
		handler: (e) => {
			let n = t.handler(e);
			return n !== null && e.state.tr.steps.length && e.state.tr.setMeta("preventAutolink", !0), n;
		}
	});
}
function qm(e) {
	let t = Xd({
		find: (t) => {
			let n = [];
			for (let r of t.matchAll(zm)) Hm(t, r, e.isAllowedHref) && n.push(Um(r));
			let r = (e.findPlainUrls?.call(e, t) ?? []).filter((e) => !n.some((t) => Wm(t, e)));
			return [...n, ...r];
		},
		type: e.type,
		getAttributes: Gm
	});
	return new xd({
		find: t.find,
		handler: (e) => {
			let n = t.handler(e);
			return n !== null && e.state.tr.steps.length && e.match.data?.markdown && e.state.tr.setMeta("preventAutolink", !0), n;
		}
	});
}
function Jm(e) {
	return new k({
		key: new A("handlePasteLink"),
		props: { handlePaste: (t, n, r) => {
			let { shouldAutoLink: i } = e, { state: a } = t, { selection: o } = a, { empty: s } = o;
			if (s) return !1;
			let c = "";
			r.content.forEach((e) => {
				c += e.textContent;
			});
			let l = Am(c, { defaultProtocol: e.defaultProtocol }).find((e) => e.isLink && e.value === c);
			return !c || !l || i !== void 0 && !i(l.value) ? !1 : e.editor.commands.setMark(e.type, { href: l.href });
		} }
	});
}
function Ym(e, t) {
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
	}), !e || e.replace(Pm, "").match(RegExp(`^(?:(?:${n.map((e) => e.replace(/[-/\\^$*+?.()|[\]{}]/g, "\\$&")).join("|")}):|[^a-z]|[a-z0-9+.\\-]+(?:[^a-z+.\\-:]|$))`, "i"));
}
var Xm = bd.create({
	name: "link",
	priority: 1e3,
	keepOnSplit: !1,
	exitable: !0,
	onCreate() {
		this.options.validate && !this.options.shouldAutoLink && (this.options.shouldAutoLink = this.options.validate, console.warn("The `validate` option is deprecated. Rename to the `shouldAutoLink` option instead.")), this.options.protocols.forEach((e) => {
			if (typeof e == "string") {
				Dm(e);
				return;
			}
			Dm(e.scheme, e.optionalSlashes);
		});
	},
	onDestroy() {
		Em();
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
			isAllowedUri: (e, t) => !!Ym(e, t.protocols),
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
					defaultValidate: (e) => !!Ym(e, this.options.protocols),
					protocols: this.options.protocols,
					defaultProtocol: this.options.defaultProtocol
				}) ? !1 : null;
			}
		}];
	},
	renderHTML({ HTMLAttributes: e }) {
		return this.options.isAllowedUri(e.href, {
			defaultValidate: (e) => !!Ym(e, this.options.protocols),
			protocols: this.options.protocols,
			defaultProtocol: this.options.defaultProtocol
		}) ? [
			"a",
			U(this.options.HTMLAttributes, e),
			0
		] : [
			"a",
			U(this.options.HTMLAttributes, {
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
					defaultValidate: (e) => !!Ym(e, this.options.protocols),
					protocols: this.options.protocols,
					defaultProtocol: this.options.defaultProtocol
				}) ? t().setMark(this.name, e).setMeta("preventAutolink", !0).run() : !1;
			},
			toggleLink: (e) => ({ chain: t }) => {
				let { href: n } = e || {};
				return n && !this.options.isAllowedUri(n, {
					defaultValidate: (e) => !!Ym(e, this.options.protocols),
					protocols: this.options.protocols,
					defaultProtocol: this.options.defaultProtocol
				}) ? !1 : t().toggleMark(this.name, e, { extendEmptyMarkRange: !0 }).setMeta("preventAutolink", !0).run();
			},
			unsetLink: () => ({ chain: e }) => e().unsetMark(this.name, { extendEmptyMarkRange: !0 }).setMeta("preventAutolink", !0).run()
		};
	},
	addInputRules() {
		return this.options.markdownLinks ? [Km({
			type: this.type,
			isAllowedHref: (e) => this.options.isAllowedUri(e, {
				defaultValidate: (e) => !!Ym(e, this.options.protocols),
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
				Am(e).filter((e) => e.isLink && this.options.isAllowedUri(e.value, {
					defaultValidate: (e) => !!Ym(e, n),
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
		return this.options.markdownLinks ? [qm({
			type: this.type,
			isAllowedHref: (e) => this.options.isAllowedUri(e, {
				defaultValidate: (e) => !!Ym(e, this.options.protocols),
				protocols: this.options.protocols,
				defaultProtocol: this.options.defaultProtocol
			}),
			findPlainUrls: e
		})] : [Xd({
			find: e,
			type: this.type,
			getAttributes: (e) => ({ href: e.data?.href })
		})];
	},
	addProseMirrorPlugins() {
		let e = [], { protocols: t, defaultProtocol: n } = this.options;
		return this.options.autolink && e.push(Im({
			type: this.type,
			defaultProtocol: this.options.defaultProtocol,
			validate: (e) => this.options.isAllowedUri(e, {
				defaultValidate: (e) => !!Ym(e, t),
				protocols: t,
				defaultProtocol: n
			}),
			shouldAutoLink: this.options.shouldAutoLink
		})), e.push(Lm({
			type: this.type,
			editor: this.editor,
			openOnClick: this.options.openOnClick === "whenNotEditable" || this.options.openOnClick,
			enableClickSelection: this.options.enableClickSelection
		})), this.options.linkOnPaste && e.push(Jm({
			editor: this.editor,
			defaultProtocol: this.options.defaultProtocol,
			type: this.type,
			shouldAutoLink: this.options.shouldAutoLink
		})), e;
	}
}), Zm = "listItem", Qm = "textStyle", $m = /^\s*([-+*])\s$/, eh = G.create({
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
			U(this.options.HTMLAttributes, e),
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
		return { toggleBulletList: () => ({ commands: e, chain: t }) => this.options.keepAttributes ? t().toggleList(this.name, this.options.itemTypeName, this.options.keepMarks).updateAttributes(Zm, this.editor.getAttributes(Qm)).run() : e.toggleList(this.name, this.options.itemTypeName, this.options.keepMarks) };
	},
	addKeyboardShortcuts() {
		return { "Mod-Shift-8": () => this.editor.commands.toggleBulletList() };
	},
	addInputRules() {
		let e = qd({
			find: $m,
			type: this.type
		});
		return (this.options.keepMarks || this.options.keepAttributes) && (e = qd({
			find: $m,
			type: this.type,
			keepMarks: this.options.keepMarks,
			keepAttributes: this.options.keepAttributes,
			getAttributes: () => this.editor.getAttributes(Qm),
			editor: this.editor
		})), [e];
	}
}), th = (e, t, n) => {
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
}, nh = (e, t, n, r) => {
	let i = th(e, n, r);
	if (!i) return !1;
	let { selection: o } = e, { nestedList: s, nestedListPos: c, insertPos: l, items: u } = i, d = e.tr;
	d.delete(c, c + s.nodeSize);
	let f = d.mapping.map(l);
	return d.insert(f, a.from(u)), d.setSelection(o.map(d.doc, d.mapping)), t && t(d), !0;
}, rh = (e, t, n) => nh(e.state, e.view.dispatch, t, n), ih = (e, t) => W.create({
	name: `${e}BranchingDeleteKeymap`,
	priority: 101,
	addKeyboardShortcuts() {
		let n = () => rh(this.editor, e, t);
		return {
			Delete: n,
			"Mod-Delete": n
		};
	}
}), ah = [
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
], oh = "abcdefghijklmnopqrstuvwxyz", sh = String.raw`\d+|[ivxlcdmIVXLCDM]+|${"[a-zA-Z]{1,2}"}`;
function ch(e) {
	let t = e, n = "";
	for (let [e, r] of ah) for (; t >= e;) n += r, t -= e;
	return n;
}
function lh(e) {
	return ch(e).toUpperCase();
}
function uh(e) {
	let t = e.toLowerCase(), n = 0, r = 0;
	for (; n < t.length;) {
		let e = !1;
		for (let [i, a] of ah) if (t.startsWith(a, n)) {
			r += i, n += a.length, e = !0;
			break;
		}
		if (!e) return 0;
	}
	return r;
}
function dh(e) {
	if (!/^[ivxlcdmIVXLCDM]+$/.test(e)) return !1;
	let t = uh(e);
	return t <= 0 ? !1 : (e === e.toLowerCase() ? ch(t) : lh(t)) === e;
}
function fh(e) {
	let t = e.toLowerCase();
	if (t.length === 1) return t.charCodeAt(0) - 97 + 1;
	if (t.length === 2) {
		let e = t.charCodeAt(0) - 97, n = t.charCodeAt(1) - 97;
		return (e + 1) * 26 + n + 1;
	}
	return 0;
}
function ph(e) {
	if (e <= 26) return oh[e - 1];
	let t = Math.floor((e - 1) / 26) - 1, n = (e - 1) % 26;
	return t < 0 ? oh[n] : oh[t] + oh[n];
}
function mh(e) {
	if (e && !/^\d+$/.test(e)) {
		if (dh(e)) return e === e.toLowerCase() ? "i" : "I";
		if (/^[a-z]{1,2}$/.test(e)) return "a";
		if (/^[A-Z]{1,2}$/.test(e)) return "A";
	}
}
function hh(e) {
	if (/^\d+$/.test(e)) return parseInt(e, 10);
	let t = mh(e);
	if (t === "i" || t === "I") return uh(e);
	if (t === "a" || t === "A") {
		let t = fh(e);
		return t > 0 ? t : 1;
	}
	let n = parseInt(e, 10);
	return Number.isNaN(n) ? 1 : n;
}
function gh(e, t) {
	if (e === "numeric") return String(t);
	switch (e) {
		case "a": return ph(t);
		case "A": return ph(t).toUpperCase();
		case "i": return ch(t);
		case "I": return lh(t);
		default: return String(t);
	}
}
function _h(e) {
	if (e.length === 0) return !1;
	let t = mh(e[0]) ?? "numeric", n = hh(e[0]);
	if (n < 1) return !1;
	for (let r = 0; r < e.length; r++) {
		let i = gh(t, n + r);
		if (e[r] !== i) return !1;
	}
	return !0;
}
function vh(e) {
	return {
		type: mh(e),
		start: hh(e)
	};
}
function yh(e) {
	let { type: t, start: n } = vh(e), r = {};
	return t && (r.type = t), n !== 1 && (r.start = n), r;
}
function bh(e, t, n = ". ") {
	let r = t + 1;
	if (!e || e === "1") return `${r}${n}`;
	switch (e) {
		case "a": return `${ph(r)}${n}`;
		case "A": return `${ph(r).toUpperCase()}${n}`;
		case "i": return `${ch(r)}${n}`;
		case "I": return `${lh(r)}${n}`;
		default: return `${r}${n}`;
	}
}
function xh(e) {
	let t = e.tokens?.[0];
	return !!(e.text && e.tokens?.length === 1 && t?.type === "list" && t.ordered && t.raw === e.text);
}
function Sh(e, t) {
	return t.tokenizeInline ? t.parseInline(t.tokenizeInline(e)) : t.parseInline([{
		type: "text",
		raw: e,
		text: e
	}]);
}
var Ch = G.create({
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
			U(this.options.HTMLAttributes, e),
			0
		];
	},
	markdownTokenName: "list_item",
	parseMarkdown: (e, t) => {
		if (e.type !== "list_item") return [];
		let n = t.parseBlockChildren ?? t.parseChildren, r = [];
		if (e.tokens && e.tokens.length > 0) {
			if (xh(e)) return {
				type: "listItem",
				content: [{
					type: "paragraph",
					content: Sh(e.text || "", t)
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
	renderMarkdown: (e, t, n) => fd(e, t, (e) => {
		if (e.parentType === "bulletList") return "- ";
		if (e.parentType === "orderedList") {
			var t, n;
			let r = ((t = e.meta) == null || (t = t.parentAttrs) == null ? void 0 : t.start) || 1;
			return bh((n = e.meta) == null || (n = n.parentAttrs) == null ? void 0 : n.type, r - 1 + (e.index || 0), ". ");
		}
		return "- ";
	}, n, { alignNestedToPrefix: n?.parentType === "orderedList" }),
	addExtensions() {
		return [ih(this.name, [this.options.bulletListTypeName, this.options.orderedListTypeName])];
	},
	addKeyboardShortcuts() {
		return {
			Enter: () => this.editor.commands.splitListItem(this.name),
			Tab: () => this.editor.commands.sinkListItem(this.name),
			"Shift-Tab": () => this.editor.commands.liftListItem(this.name)
		};
	}
}), wh = (e, t) => {
	let { $from: n } = t.selection, r = B(e, t.schema), i = null, a = n.depth, o = n.pos, s = null;
	for (; a > 0 && s === null;) i = n.node(a), i.type === r ? s = a : (--a, --o);
	return s === null ? null : {
		$pos: t.doc.resolve(o),
		depth: s
	};
}, Th = (e, t) => {
	let n = wh(e, t);
	if (!n) return !1;
	let [, r] = Hl(t, e, n.$pos.pos + 4);
	return r;
}, Eh = (e, t, n) => {
	let { $anchor: r } = e.selection, i = Math.max(0, r.pos - 2), a = e.doc.resolve(i).node();
	return !(!a || !n.includes(a.type.name));
}, Dh = (e, t, n) => {
	if (e.commands.undoInputRule()) return !0;
	if (e.state.selection.from !== e.state.selection.to) return !1;
	if (!qc(e.state, t) && Eh(e.state, t, n)) {
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
	if (!qc(e.state, t) || !Xl(e.state)) return !1;
	let { $from: r } = e.state.selection, i = r.depth - 1;
	return r.node(i).type !== e.schema.nodes[t] || r.index(i) !== 0 ? !1 : e.chain().liftListItem(t).run();
}, Oh = (e, t) => {
	let n = Th(e, t), r = wh(e, t);
	return !r || !n ? !1 : n > r.depth;
}, kh = (e, t) => {
	let n = Th(e, t), r = wh(e, t);
	return !r || !n ? !1 : n < r.depth;
}, Ah = (e, t) => {
	if (!qc(e.state, t) || !Yl(e.state, t)) return !1;
	let { selection: n } = e.state, { $from: r, $to: i } = n;
	return !n.empty && r.sameParent(i) ? !1 : Oh(t, e.state) ? e.chain().focus(e.state.selection.from + 4).lift(t).joinBackward().run() : kh(t, e.state) ? e.chain().joinForward().joinBackward().run() : e.commands.joinItemForward();
}, jh = (e, t, n) => {
	let { state: r } = e, { selection: i } = r;
	if (!i.empty) return !1;
	let { $from: o } = i;
	if (o.parentOffset !== 0 || !o.parent.isTextblock || qc(r, t)) return !1;
	let s = Ul(o);
	if (!s || !n.includes(s.type.name)) return !1;
	let c = s.lastChild;
	if (!c || c.type.name !== t) return !1;
	let l = o.parent;
	if (!c.canReplace(c.childCount, c.childCount, a.from(l))) return !1;
	let u = o.before(), d = o.after(), f = u - 2;
	return e.commands.command(({ tr: e, dispatch: t }) => (t && (e.delete(u, d).insert(f, a.from(l)), e.setSelection(D.create(e.doc, f + 1)), e.scrollIntoView()), !0));
}, Mh = W.create({
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
					e.state.schema.nodes[n] !== void 0 && Ah(e, n) && (t = !0);
				}), t;
			},
			"Mod-Delete": ({ editor: e }) => {
				let t = !1;
				return this.options.listTypes.forEach(({ itemName: n }) => {
					e.state.schema.nodes[n] !== void 0 && Ah(e, n) && (t = !0);
				}), t;
			},
			Backspace: ({ editor: e }) => {
				let t = !1;
				return this.options.listTypes.forEach(({ itemName: n, wrapperNames: r }) => {
					e.state.schema.nodes[n] !== void 0 && Dh(e, n, r) && (t = !0);
				}), t;
			},
			"Mod-Backspace": ({ editor: e }) => {
				let t = !1;
				return this.options.listTypes.forEach(({ itemName: n, wrapperNames: r }) => {
					e.state.schema.nodes[n] !== void 0 && Dh(e, n, r) && (t = !0);
				}), t;
			},
			Tab: ({ editor: e }) => {
				for (let { itemName: t, wrapperNames: n } of this.options.listTypes) if (e.state.schema.nodes[t] !== void 0 && jh(e, t, n)) return !0;
				return !1;
			}
		};
	}
}), Nh = RegExp(`^(\\s*)(${sh})([.)])\\s+(.*)$`), Ph = /^\s/, Fh = {
	heading: /^#{1,6}(?:\s|$)/,
	bulletItem: /^[-+*]\s+/,
	codeFence: /^(?:```|~~~)/,
	blockMath: /^\$\$/,
	thematicBreak: /^(?:(?:-[ \t]*){3,}|(?:_[ \t]*){3,}|(?:\*[ \t]*){3,})$/
};
function Ih(e) {
	return Nh.test(e.trimStart());
}
function Lh(e) {
	let t = e.trimStart();
	return Fh.bulletItem.test(t) || Ih(t) || Fh.heading.test(t) || Fh.thematicBreak.test(t) && !t.startsWith("-") || /^>\s?/.test(t) || Fh.codeFence.test(t) || Fh.blockMath.test(t);
}
function Rh(e) {
	return Object.values(Fh).some((t) => t.test(e));
}
function zh(e) {
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
		if (t.length > 0 && Lh(e)) {
			r = !0, n.push(e);
			return;
		}
		t.push(e);
	}), {
		paragraphLines: t,
		blockLines: n
	};
}
function Bh(e) {
	let t = [], n = 0, r = 0;
	for (; n < e.length;) {
		let i = e[n], a = i.match(Nh);
		if (!a) break;
		let [, o, s, c, l] = a, u = o.length, d = parseInt(s, 10), f = isNaN(d) ? mh(s) : void 0, p = isNaN(d) ? hh(s) : d, m = [l], h = n + 1, g = [i], _ = !1;
		for (; h < e.length;) {
			let t = e[h];
			if (t.match(Nh)) break;
			if (t.trim() === "") g.push(t), m.push(""), _ = !0, h += 1;
			else if (t.match(Ph)) {
				let e = t.length - t.trimStart().length, n = u + s.length + 1;
				g.push(t), m.push(t.slice(Math.min(e, n))), h += 1;
			} else {
				if (_ || Rh(t)) break;
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
var Vh = RegExp(`^(${sh})([.)])\\s+(.+)$`);
function Hh(e) {
	let t = e.split("\n").filter((e) => e.trim().length > 0);
	if (t.length === 0) return null;
	let n = [];
	for (let e of t) {
		let t = e.trim().match(Vh);
		if (!t) return null;
		n.push({
			marker: t[1],
			content: t[3]
		});
	}
	return _h(n.map((e) => e.marker)) ? {
		type: "orderedList",
		attrs: yh(n[0].marker),
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
function Uh(e, t, n) {
	let r = [], i = 0;
	for (; i < e.length;) {
		let a = e[i];
		if (a.indent === t) {
			let { paragraphLines: o, blockLines: s } = zh(a.contentLines), c = o.join("\n").trim(), l = [];
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
				let e = Uh(f, Math.min(...f.map((e) => e.indent)), n);
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
function Wh(e, t) {
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
var Gh = "listItem", Kh = "textStyle", qh = /^(\d+)\.\s$/;
function Jh(e) {
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
var Yh = G.create({
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
						let e = Jh(n);
						if (e) return e;
					}
					let r = e.querySelector("li");
					if (r) {
						let e = r.getAttribute("style");
						if (e) {
							let t = Jh(e);
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
		let { start: t, type: n, ...r } = e, i = U(this.options.HTMLAttributes, r);
		return t !== 1 && (i.start = t), n && n !== "1" && (i.type = n), [
			"ol",
			i,
			0
		];
	},
	markdownTokenName: "list",
	parseMarkdown: (e, t) => {
		if (e.type !== "list" || !e.ordered) return [];
		let n = e.start || 1, r = e.typeMarker, i = e.items ? Wh(e.items, t) : [], a = {};
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
			let r = e.split("\n"), [i, a] = Bh(r);
			if (i.length === 0) return;
			let o = Uh(i, i[0].indent, n);
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
		return { toggleOrderedList: () => ({ commands: e, chain: t }) => this.options.keepAttributes ? t().toggleList(this.name, this.options.itemTypeName, this.options.keepMarks).updateAttributes(Gh, this.editor.getAttributes(Kh)).run() : e.toggleList(this.name, this.options.itemTypeName, this.options.keepMarks) };
	},
	addKeyboardShortcuts() {
		return { "Mod-Shift-7": () => this.editor.commands.toggleOrderedList() };
	},
	addProseMirrorPlugins() {
		return [new k({ props: { handlePaste: (e, t) => {
			if ((t.clipboardData?.getData("text/html"))?.trim()) return !1;
			let n = t.clipboardData?.getData("text/plain");
			if (!n) return !1;
			let r = Hh(n);
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
		let e = (e, t) => (!t.attrs.type || t.attrs.type === "1") && t.childCount + t.attrs.start === +e[1], t = qd({
			find: qh,
			type: this.type,
			getAttributes: (e) => ({ start: +e[1] }),
			joinPredicate: e
		});
		return (this.options.keepMarks || this.options.keepAttributes) && (t = qd({
			find: qh,
			type: this.type,
			keepMarks: this.options.keepMarks,
			keepAttributes: this.options.keepAttributes,
			getAttributes: (e) => ({
				start: +e[1],
				...this.editor.getAttributes(Kh)
			}),
			joinPredicate: e,
			editor: this.editor
		})), [t];
	}
}), Xh = /^\s*(\[([( |x])?\])\s$/, Zh = "position:absolute;width:1px;height:1px;padding:0;margin:-1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;border:0", Qh = (e, t, n) => {
	var r;
	return (n == null || (r = n.checkboxLabel) == null ? void 0 : r.call(n, e, t)) || `Task item checkbox for ${e.textContent || "empty task item"}`;
}, $h = G.create({
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
			U(this.options.HTMLAttributes, t, { "data-type": this.name }),
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
	renderMarkdown: (e, t) => fd(e, t, `- [${e.attrs?.checked ? "x" : " "}] `),
	addExtensions() {
		return this.options.nested ? [ih(this.name, [this.options.taskListTypeName])] : [];
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
			o.style.cssText = Zh;
			let l = (e) => {
				let t = Qh(e, e.attrs.checked, this.options.a11y);
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
					let t = r.extensionManager.attributes, n = Cl(e, t), a = new Set(Object.keys(n)), o = this.options.HTMLAttributes;
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
		return [qd({
			find: Xh,
			type: this.type,
			getAttributes: (e) => ({ checked: e[e.length - 1] === "x" })
		})];
	}
}), eg = G.create({
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
			U(this.options.HTMLAttributes, e, { "data-type": this.name }),
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
				let t = ld(e, {
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
			}, i = ld(e, {
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
W.create({
	name: "listKit",
	addExtensions() {
		let e = [];
		return this.options.bulletList !== !1 && e.push(eh.configure(this.options.bulletList)), this.options.listItem !== !1 && e.push(Ch.configure(this.options.listItem)), this.options.listKeymap !== !1 && e.push(Mh.configure(this.options.listKeymap)), this.options.orderedList !== !1 && e.push(Yh.configure(this.options.orderedList)), this.options.taskItem !== !1 && e.push($h.configure(this.options.taskItem)), this.options.taskList !== !1 && e.push(eg.configure(this.options.taskList)), e;
	}
});
//#endregion
//#region node_modules/@tiptap/extension-paragraph/dist/index.js
var tg = "&nbsp;", ng = "\xA0", rg = G.create({
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
			U(this.options.HTMLAttributes, e),
			0
		];
	},
	parseMarkdown: (e, t) => {
		let n = e.tokens || [];
		if (n.length === 1 && n[0].type === "image") return t.parseChildren([n[0]]);
		let r = t.parseInline(n);
		return n.length === 1 && n[0].type === "text" && (n[0].raw === tg || n[0].text === tg || n[0].raw === ng || n[0].text === ng) && r.length === 1 && r[0].type === "text" && (r[0].text === tg || r[0].text === ng) ? t.createNode("paragraph", void 0, []) : t.createNode("paragraph", void 0, r);
	},
	renderMarkdown: (e, t, n) => {
		if (!e) return "";
		let r = Array.isArray(e.content) ? e.content : [];
		if (r.length === 0) {
			var i, a;
			let e = Array.isArray(n == null || (i = n.previousNode) == null ? void 0 : i.content) ? n.previousNode.content : [];
			return (n == null || (a = n.previousNode) == null ? void 0 : a.type) === "paragraph" && e.length === 0 ? tg : "";
		}
		return t.renderChildren(r);
	},
	addCommands() {
		return { setParagraph: () => ({ commands: e }) => e.setNode(this.name) };
	},
	addKeyboardShortcuts() {
		return { "Mod-Alt-0": () => this.editor.commands.setParagraph() };
	}
}), ig = /(?:^|\s)(~~(?!\s+~~)((?:[^~]+))~~(?!\s+~~))$/, ag = /(?:^|\s)(~~(?!\s+~~)((?:[^~]+))~~(?!\s+~~))/g, og = bd.create({
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
			U(this.options.HTMLAttributes, e),
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
		return [Wd({
			find: ig,
			type: this.type
		})];
	},
	addPasteRules() {
		return [Xd({
			find: ag,
			type: this.type
		})];
	}
}), sg = G.create({
	name: "text",
	group: "inline",
	parseMarkdown: (e) => ({
		type: "text",
		text: e.text || ""
	}),
	renderMarkdown: (e) => e.text || ""
}), cg = bd.create({
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
			U(this.options.HTMLAttributes, e),
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
function lg(e = {}) {
	return new k({ view(t) {
		return new ug(t, e);
	} });
}
var ug = class {
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
				let t = Bt(this.editorView.state.doc, e, this.editorView.dragging.slice);
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
}, dg = class e extends E {
	constructor(e) {
		super(e, e);
	}
	map(t, n) {
		let r = t.resolve(n.map(this.head));
		return e.valid(r) ? new e(r) : E.near(r);
	}
	content() {
		return d.empty;
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
		return new fg(this.anchor);
	}
	static valid(e) {
		let t = e.parent;
		if (t.inlineContent || !mg(e) || !hg(e)) return !1;
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
					if (a.isAtom && !a.isText && !O.isSelectable(a)) {
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
dg.prototype.visible = !1, dg.findFrom = dg.findGapCursorFrom, E.jsonID("gapcursor", dg);
var fg = class e {
	constructor(e) {
		this.pos = e;
	}
	map(t) {
		return new e(t.map(this.pos));
	}
	resolve(e) {
		let t = e.resolve(this.pos);
		return dg.valid(t) ? new dg(t) : E.near(t);
	}
};
function pg(e) {
	return e.isAtom || e.spec.isolating || e.spec.createGapCursor;
}
function mg(e) {
	for (let t = e.depth; t >= 0; t--) {
		let n = e.index(t), r = e.node(t);
		if (n == 0) {
			if (r.type.spec.isolating) return !0;
			continue;
		}
		for (let e = r.child(n - 1);; e = e.lastChild) {
			if (e.childCount == 0 && !e.inlineContent || pg(e.type)) return !0;
			if (e.inlineContent) return !1;
		}
	}
	return !0;
}
function hg(e) {
	for (let t = e.depth; t >= 0; t--) {
		let n = e.indexAfter(t), r = e.node(t);
		if (n == r.childCount) {
			if (r.type.spec.isolating) return !0;
			continue;
		}
		for (let e = r.child(n);; e = e.firstChild) {
			if (e.childCount == 0 && !e.inlineContent || pg(e.type)) return !0;
			if (e.inlineContent) return !1;
		}
	}
	return !0;
}
function gg() {
	return new k({ props: {
		decorations: xg,
		createSelectionBetween(e, t, n) {
			return t.pos == n.pos && dg.valid(n) ? new dg(n) : null;
		},
		handleClick: yg,
		handleKeyDown: _g,
		handleDOMEvents: { beforeinput: bg }
	} });
}
var _g = Ks({
	ArrowLeft: vg("horiz", -1),
	ArrowRight: vg("horiz", 1),
	ArrowUp: vg("vert", -1),
	ArrowDown: vg("vert", 1)
});
function vg(e, t) {
	let n = e == "vert" ? t > 0 ? "down" : "up" : t > 0 ? "right" : "left";
	return function(e, r, i) {
		let a = e.selection, o = t > 0 ? a.$to : a.$from, s = a.empty;
		if (a instanceof D) {
			if (!i.endOfTextblock(n) || o.depth == 0) return !1;
			s = !1, o = e.doc.resolve(t > 0 ? o.after() : o.before());
		}
		let c = dg.findGapCursorFrom(o, t, s);
		return c ? (r && r(e.tr.setSelection(new dg(c))), !0) : !1;
	};
}
function yg(e, t, n) {
	if (!e || !e.editable) return !1;
	let r = e.state.doc.resolve(t);
	if (!dg.valid(r)) return !1;
	let i = e.posAtCoords({
		left: n.clientX,
		top: n.clientY
	});
	return i && i.inside > -1 && O.isSelectable(e.state.doc.nodeAt(i.inside)) ? !1 : (e.dispatch(e.state.tr.setSelection(new dg(r))), !0);
}
function bg(e, t) {
	if (t.inputType != "insertCompositionText" || !(e.state.selection instanceof dg)) return !1;
	let { $from: n } = e.state.selection, r = n.parent.contentMatchAt(n.index()).findWrapping(e.state.schema.nodes.text);
	if (!r) return !1;
	let i = a.empty;
	for (let e = r.length - 1; e >= 0; e--) i = a.from(r[e].createAndFill(null, i));
	let o = e.state.tr.replace(n.pos, n.pos, new d(i, 0, 0));
	return o.setSelection(D.near(o.doc.resolve(n.pos + 1))), e.dispatch(o), !1;
}
function xg(e) {
	if (!(e.selection instanceof dg)) return null;
	let t = document.createElement("div");
	return t.className = "ProseMirror-gapcursor", L.create(e.doc, [I.widget(e.selection.head, t, { key: "gapcursor" })]);
}
//#endregion
//#region node_modules/rope-sequence/dist/index.js
var Sg = 200, X = function() {};
X.prototype.append = function(e) {
	return e.length ? (e = X.from(e), !this.length && e || e.length < Sg && this.leafAppend(e) || this.length < Sg && e.leafPrepend(this) || this.appendInner(e)) : this;
}, X.prototype.prepend = function(e) {
	return e.length ? X.from(e).append(this) : this;
}, X.prototype.appendInner = function(e) {
	return new wg(this, e);
}, X.prototype.slice = function(e, t) {
	return e === void 0 && (e = 0), t === void 0 && (t = this.length), e >= t ? X.empty : this.sliceInner(Math.max(0, e), Math.min(this.length, t));
}, X.prototype.get = function(e) {
	if (!(e < 0 || e >= this.length)) return this.getInner(e);
}, X.prototype.forEach = function(e, t, n) {
	t === void 0 && (t = 0), n === void 0 && (n = this.length), t <= n ? this.forEachInner(e, t, n, 0) : this.forEachInvertedInner(e, t, n, 0);
}, X.prototype.map = function(e, t, n) {
	t === void 0 && (t = 0), n === void 0 && (n = this.length);
	var r = [];
	return this.forEach(function(t, n) {
		return r.push(e(t, n));
	}, t, n), r;
}, X.from = function(e) {
	return e instanceof X ? e : e && e.length ? new Cg(e) : X.empty;
};
var Cg = /* @__PURE__ */ function(e) {
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
		if (this.length + e.length <= Sg) return new t(this.values.concat(e.flatten()));
	}, t.prototype.leafPrepend = function(e) {
		if (this.length + e.length <= Sg) return new t(e.flatten().concat(this.values));
	}, n.length.get = function() {
		return this.values.length;
	}, n.depth.get = function() {
		return 0;
	}, Object.defineProperties(t.prototype, n), t;
}(X);
X.empty = new Cg([]);
var wg = /* @__PURE__ */ function(e) {
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
}(X), Tg = 500, Eg = class e {
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
				u.push(new Og(t.map));
				let e = t.step.map(i.slice(a)), n;
				e && o.maybeStep(e).doc && (n = o.mapping.maps[o.mapping.maps.length - 1], l.push(new Og(n, void 0, void 0, l.length + u.length))), a--, n && i.appendMap(n, a);
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
			let r = t.steps[e].invert(t.docs[e]), l = new Og(t.mapping.maps[e], r, n), u;
			(u = c && c.merge(l)) && (l = u, e ? a.pop() : s = s.slice(0, s.length - 1)), a.push(l), n && (o++, n = void 0), i || (c = l);
		}
		let l = o - r.depth;
		return l > Ag && (s = Dg(s, l), o -= l), new e(s.append(a), o);
	}
	remapping(e, t) {
		let n = new ct();
		return this.items.forEach((t, r) => {
			let i = t.mirrorOffset != null && r - t.mirrorOffset >= e ? n.maps.length - t.mirrorOffset : void 0;
			n.appendMap(t.map, i);
		}, e, t), n;
	}
	addMaps(t) {
		return this.eventCount == 0 ? this : new e(this.items.append(t.map((e) => new Og(e))), this.eventCount);
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
				l && s++, r.push(new Og(i, o, l));
			} else r.push(new Og(i));
		}, i);
		let l = [];
		for (let e = n; e < o; e++) l.push(new Og(a.maps[e]));
		let u = this.items.slice(0, i).append(l).append(r), d = new e(u, s);
		return d.emptyItemCount() > Tg && (d = d.compress(this.items.length - r.length)), d;
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
					let c = new Og(o.invert(), t, s), l, u = i.length - 1;
					(l = i.length && i[u].merge(c)) ? i[u] = l : i.push(c);
				}
			} else e.map && r--;
		}, this.items.length, 0), new e(X.from(i.reverse()), a);
	}
};
Eg.empty = new Eg(X.empty, 0);
function Dg(e, t) {
	let n;
	return e.forEach((e, r) => {
		if (e.selection && t-- == 0) return n = r, !1;
	}), e.slice(n);
}
var Og = class e {
	constructor(e, t, n, r) {
		this.map = e, this.step = t, this.selection = n, this.mirrorOffset = r;
	}
	merge(t) {
		if (this.step && t.step && !t.selection) {
			let n = t.step.merge(this.step);
			if (n) return new e(n.getMap().invert(), n, this.selection);
		}
	}
}, kg = class {
	constructor(e, t, n, r, i) {
		this.done = e, this.undone = t, this.prevRanges = n, this.prevTime = r, this.prevComposition = i;
	}
}, Ag = 20;
function jg(e, t, n, r) {
	let i = n.getMeta(zg), a;
	if (i) return i.historyState;
	n.getMeta(Bg) && (e = new kg(e.done, e.undone, null, 0, -1));
	let o = n.getMeta("appendedTransaction");
	if (n.steps.length == 0) return e;
	if (o && o.getMeta(zg)) return o.getMeta(zg).redo ? new kg(e.done.addTransform(n, void 0, r, Rg(t)), e.undone, Ng(n.mapping.maps), e.prevTime, e.prevComposition) : new kg(e.done, e.undone.addTransform(n, void 0, r, Rg(t)), null, e.prevTime, e.prevComposition);
	if (n.getMeta("addToHistory") !== !1 && !(o && o.getMeta("addToHistory") === !1)) {
		let i = n.getMeta("composition"), a = e.prevTime == 0 || !o && e.prevComposition != i && (e.prevTime < (n.time || 0) - r.newGroupDelay || !Mg(n, e.prevRanges)), s = o ? Pg(e.prevRanges, n.mapping) : Ng(n.mapping.maps);
		return new kg(e.done.addTransform(n, a ? t.selection.getBookmark() : void 0, r, Rg(t)), Eg.empty, s, n.time, i ?? e.prevComposition);
	}
	return (a = n.getMeta("rebased")) ? new kg(e.done.rebased(n, a), e.undone.rebased(n, a), Pg(e.prevRanges, n.mapping), e.prevTime, e.prevComposition) : new kg(e.done.addMaps(n.mapping.maps), e.undone.addMaps(n.mapping.maps), Pg(e.prevRanges, n.mapping), e.prevTime, e.prevComposition);
}
function Mg(e, t) {
	if (!t) return !1;
	if (!e.docChanged) return !0;
	let n = !1;
	return e.mapping.maps[0].forEach((e, r) => {
		for (let i = 0; i < t.length; i += 2) e <= t[i + 1] && r >= t[i] && (n = !0);
	}), n;
}
function Ng(e) {
	let t = [];
	for (let n = e.length - 1; n >= 0 && t.length == 0; n--) e[n].forEach((e, n, r, i) => t.push(r, i));
	return t;
}
function Pg(e, t) {
	if (!e) return null;
	let n = [];
	for (let r = 0; r < e.length; r += 2) {
		let i = t.map(e[r], 1), a = t.map(e[r + 1], -1);
		i <= a && n.push(i, a);
	}
	return n;
}
function Fg(e, t, n) {
	let r = Rg(t), i = zg.get(t).spec.config, a = (n ? e.undone : e.done).popEvent(t, r);
	if (!a) return null;
	let o = a.selection.resolve(a.transform.doc), s = (n ? e.done : e.undone).addTransform(a.transform, t.selection.getBookmark(), i, r), c = new kg(n ? s : a.remaining, n ? a.remaining : s, null, 0, -1);
	return a.transform.setSelection(o).setMeta(zg, {
		redo: n,
		historyState: c
	});
}
var Ig = !1, Lg = null;
function Rg(e) {
	let t = e.plugins;
	if (Lg != t) {
		Ig = !1, Lg = t;
		for (let e = 0; e < t.length; e++) if (t[e].spec.historyPreserveItems) {
			Ig = !0;
			break;
		}
	}
	return Ig;
}
var zg = new A("history"), Bg = new A("closeHistory");
function Vg(e = {}) {
	return e = {
		depth: e.depth || 100,
		newGroupDelay: e.newGroupDelay || 500
	}, new k({
		key: zg,
		state: {
			init() {
				return new kg(Eg.empty, Eg.empty, null, 0, -1);
			},
			apply(t, n, r) {
				return jg(n, r, t, e);
			}
		},
		config: e,
		props: { handleDOMEvents: { beforeinput(e, t) {
			let n = t.inputType, r = n == "historyUndo" ? Ug : n == "historyRedo" ? Wg : null;
			return !r || !e.editable ? !1 : (t.preventDefault(), r(e.state, e.dispatch));
		} } }
	});
}
function Hg(e, t) {
	return (n, r) => {
		let i = zg.getState(n);
		if (!i || (e ? i.undone : i.done).eventCount == 0) return !1;
		if (r) {
			let a = Fg(i, n, e);
			a && r(t ? a.scrollIntoView() : a);
		}
		return !0;
	};
}
var Ug = Hg(!1, !0), Wg = Hg(!0, !0);
W.create({
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
		return [new k({
			key: new A("characterCount"),
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
var Gg = W.create({
	name: "dropCursor",
	addOptions() {
		return {
			color: "currentColor",
			width: 1,
			class: void 0
		};
	},
	addProseMirrorPlugins() {
		return [lg(this.options)];
	}
});
W.create({
	name: "focus",
	addOptions() {
		return {
			className: "has-focus",
			mode: "all"
		};
	},
	addProseMirrorPlugins() {
		return [new k({
			key: new A("focus"),
			props: { decorations: ({ doc: e, selection: t }) => {
				let { isEditable: n, isFocused: r } = this.editor, { anchor: i } = t, a = [];
				if (!n || !r) return L.create(e, []);
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
					a.push(I.node(t, t + e.nodeSize, { class: this.options.className }));
				}), L.create(e, a);
			} }
		})];
	}
});
var Kg = W.create({
	name: "gapCursor",
	addProseMirrorPlugins() {
		return [gg()];
	},
	extendNodeSchema(e) {
		return { allowGapCursor: H(V(e, "allowGapCursor", {
			name: e.name,
			options: e.options,
			storage: e.storage
		})) ?? null };
	}
}), qg = "placeholder", Jg = new A("tiptap__placeholder");
function Yg(e) {
	let { editor: t, placeholder: n, dataAttribute: r, pos: i, node: a, isEmptyDoc: o, hasAnchor: s, classes: { emptyNode: c, emptyEditor: l } } = e, u = [c];
	return o && u.push(l), I.node(i, i + a.nodeSize, {
		class: u.join(" "),
		[r]: typeof n == "function" ? n({
			editor: t,
			node: a,
			pos: i,
			hasAnchor: s
		}) : n
	});
}
function Xg(e, t) {
	return typeof e == "function" ? e(t) : e;
}
function Zg({ editor: e, options: t, dataAttribute: n, doc: r, selection: i, from: a, to: o }) {
	let { anchor: s } = i, c = [], l = e.isEmpty;
	return r.nodesBetween(a, o, (r, i) => {
		let a = s >= i && s <= i + r.nodeSize, o = !r.isLeaf && $l(r);
		return r.type.isTextblock && (a || !t.showOnlyCurrent) && o && c.push(Yg({
			editor: e,
			isEmptyDoc: l,
			dataAttribute: n,
			hasAnchor: a,
			placeholder: t.placeholder,
			classes: {
				emptyEditor: t.emptyEditorClass,
				emptyNode: Xg(t.emptyNodeClass, {
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
function Qg({ editor: e, options: t, dataAttribute: n, doc: r, selection: i }) {
	if (!e.isEditable && t.showOnlyWhenEditable) return null;
	let { anchor: a } = i, o = [], s = e.isEmpty;
	if (t.showOnlyCurrent && !t.includeChildren) {
		let i = r.resolve(a), c = i.depth > 0 ? i.node(1) : i.nodeAfter, l = i.depth > 0 ? i.before(1) : a;
		if (c && c.type.isTextblock && $l(c)) {
			let r = a >= l && a <= l + c.nodeSize;
			o.push(Yg({
				editor: e,
				isEmptyDoc: s,
				dataAttribute: n,
				hasAnchor: r,
				placeholder: t.placeholder,
				classes: {
					emptyEditor: t.emptyEditorClass,
					emptyNode: Xg(t.emptyNodeClass, {
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
	} else o.push(...Zg({
		editor: e,
		options: t,
		dataAttribute: n,
		doc: r,
		selection: i,
		from: 0,
		to: r.content.size
	}));
	return L.create(r, o);
}
function $g(e, t) {
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
function e_(e, t) {
	return {
		from: Math.max(0, t.from - 1),
		to: Math.min(e.content.size, t.to - 1)
	};
}
function t_(e, t, n) {
	let r = [];
	return e.forEach((e, i) => {
		let a = i, o = a + e.nodeSize, s = a + 1, c = o + 1;
		s < n && c > t && r.push({
			from: a,
			to: o
		});
	}), r;
}
function n_(e) {
	if (e.length === 0) return [];
	let t = [...e].sort((e, t) => e.from - t.from), n = [{ ...t[0] }];
	for (let e = 1; e < t.length; e += 1) {
		let r = n[n.length - 1], i = t[e];
		i.from <= r.to ? r.to = Math.max(r.to, i.to) : n.push({ ...i });
	}
	return n;
}
function r_(e, t) {
	let n = t_(e, t.from, t.to);
	return n.push(e_(e, $g(e, t.from))), t.to > t.from ? n.push(e_(e, $g(e, Math.min(t.to, e.content.size + 1) - 1))) : t.from < e.content.size + 1 && n.push(e_(e, $g(e, Math.min(t.from + 1, e.content.size)))), n;
}
function i_(e, t, n) {
	let r = [];
	if (e.docChanged) {
		let t = Bl(e);
		for (let e of t) r.push(...r_(n.doc, e.newRange));
	}
	return e.selectionSet && (r.push(e_(n.doc, $g(n.doc, e.mapping.map(t.selection.anchor)))), r.push(e_(n.doc, $g(n.doc, n.selection.anchor)))), n_(r);
}
function a_(e, t, n) {
	let r = Math.max(0, Math.min(e, n.content.size));
	return {
		from: r,
		to: Math.max(r, Math.min(t, n.content.size))
	};
}
function o_({ decorations: e, ranges: t, editor: n, options: r, dataAttribute: i, doc: a, selection: o }) {
	let s = e;
	for (let e of t) {
		let { from: t, to: c } = a_(e.from, e.to, a), l = s.find(t, c).filter((e) => e.from >= t && e.to <= c);
		l.length && (s = s.remove(l));
		let u = Zg({
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
function s_({ editor: e, options: t, dataAttribute: n }) {
	return {
		init(r, i) {
			return Qg({
				editor: e,
				options: t,
				dataAttribute: n,
				doc: i.doc,
				selection: i.selection
			}) ?? L.empty;
		},
		apply(r, i, a, o) {
			return !r.docChanged && !r.selectionSet ? i : o_({
				decorations: i.map(r.mapping, r.doc),
				ranges: i_(r, a, o),
				editor: e,
				options: t,
				dataAttribute: n,
				doc: o.doc,
				selection: o.selection
			});
		}
	};
}
function c_(e) {
	return e.replace(/\s+/g, "-").replace(/[^a-zA-Z0-9-]/g, "").replace(/^[0-9-]+/, "").replace(/^-+/, "").toLowerCase();
}
function l_({ editor: e, options: t }) {
	let n = t.dataAttribute ? `data-${c_(t.dataAttribute)}` : `data-${qg}`, r = t.showOnlyCurrent && !t.includeChildren;
	return new k({
		key: Jg,
		...r ? {} : { state: s_({
			editor: e,
			options: t,
			dataAttribute: n
		}) },
		props: { decorations: r ? ({ doc: r, selection: i }) => Qg({
			editor: e,
			options: t,
			dataAttribute: n,
			doc: r,
			selection: i
		}) : (n) => t.showOnlyWhenEditable && !e.isEditable ? L.empty : Jg.getState(n) ?? L.empty }
	});
}
W.create({
	name: "placeholder",
	addOptions() {
		return {
			emptyEditorClass: "is-editor-empty",
			emptyNodeClass: "is-empty",
			dataAttribute: qg,
			placeholder: "Write something …",
			showOnlyWhenEditable: !0,
			showOnlyCurrent: !0,
			includeChildren: !1
		};
	},
	addProseMirrorPlugins() {
		return [l_({
			editor: this.editor,
			options: this.options
		})];
	}
});
function u_(e, t) {
	return !e.selection.empty && !eu(e.selection) && t.isEditable;
}
function d_(e, t) {
	return u_(e, t) && !t.isFocused && !t.view.dragging;
}
function f_() {
	var e;
	(e = window.getSelection()) == null || e.removeAllRanges();
}
function p_(e) {
	e.focus();
}
W.create({
	name: "selection",
	addOptions() {
		return { className: "selection" };
	},
	addProseMirrorPlugins() {
		let { editor: e, options: t } = this;
		return [new k({
			key: new A("selection"),
			props: {
				decorations(n) {
					return d_(n, e) ? L.create(n.doc, [I.inline(n.selection.from, n.selection.to, { class: t.className })]) : null;
				},
				handleDOMEvents: {
					blur(t) {
						return u_(t.state, e) && f_(), !1;
					},
					focus(t) {
						return u_(t.state, e) && requestAnimationFrame(() => {
							!e.isDestroyed && t.hasFocus() && p_(t);
						}), !1;
					}
				}
			}
		})];
	}
});
function m_({ types: e, node: t }) {
	return t && Array.isArray(e) && e.includes(t.type) || t?.type === e;
}
var h_ = W.create({
	name: "trailingNode",
	addOptions() {
		return {
			node: void 0,
			notAfter: []
		};
	},
	addProseMirrorPlugins() {
		let e = new A(this.name), t = this.options.node || this.editor.schema.topNodeType.contentMatch.defaultType?.name || "paragraph", n = Object.entries(this.editor.schema.nodes).map(([, e]) => e).filter((e) => (this.options.notAfter || []).concat(t).includes(e.name));
		return [new k({
			key: e,
			appendTransaction: (n, r, i) => {
				let { doc: a, tr: o, schema: s } = i, c = e.getState(i), l = a.content.size, u = s.nodes[t];
				if (!n.some((e) => e.getMeta("skipTrailingNode")) && c) return o.insert(l, u.create());
			},
			state: {
				init: (e, t) => {
					let r = t.tr.doc.lastChild;
					return !m_({
						node: r,
						types: n
					});
				},
				apply: (e, t) => {
					if (!e.docChanged || e.getMeta("__uniqueIDTransaction")) return t;
					let r = e.doc.lastChild;
					return !m_({
						node: r,
						types: n
					});
				}
			}
		})];
	}
}), g_ = W.create({
	name: "undoRedo",
	addOptions() {
		return {
			depth: 100,
			newGroupDelay: 500
		};
	},
	addCommands() {
		return {
			undo: () => ({ state: e, dispatch: t }) => Ug(e, t),
			redo: () => ({ state: e, dispatch: t }) => Wg(e, t)
		};
	},
	addProseMirrorPlugins() {
		return [Vg(this.options)];
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
}), __ = W.create({
	name: "starterKit",
	addExtensions() {
		let e = [];
		return this.options.bold !== !1 && e.push(ff.configure(this.options.bold)), this.options.blockquote !== !1 && e.push(sf.configure(this.options.blockquote)), this.options.bulletList !== !1 && e.push(eh.configure(this.options.bulletList)), this.options.code !== !1 && e.push(hf.configure(this.options.code)), this.options.codeBlock !== !1 && e.push(yf.configure(this.options.codeBlock)), this.options.document !== !1 && e.push(bf.configure(this.options.document)), this.options.dropcursor !== !1 && e.push(Gg.configure(this.options.dropcursor)), this.options.gapcursor !== !1 && e.push(Kg.configure(this.options.gapcursor)), this.options.hardBreak !== !1 && e.push(xf.configure(this.options.hardBreak)), this.options.heading !== !1 && e.push(Sf.configure(this.options.heading)), this.options.undoRedo !== !1 && e.push(g_.configure(this.options.undoRedo)), this.options.horizontalRule !== !1 && e.push(Cf.configure(this.options.horizontalRule)), this.options.italic !== !1 && e.push(Of.configure(this.options.italic)), this.options.listItem !== !1 && e.push(Ch.configure(this.options.listItem)), this.options.listKeymap !== !1 && e.push(Mh.configure(this.options?.listKeymap)), this.options.link !== !1 && e.push(Xm.configure(this.options?.link)), this.options.orderedList !== !1 && e.push(Yh.configure(this.options.orderedList)), this.options.paragraph !== !1 && e.push(rg.configure(this.options.paragraph)), this.options.strike !== !1 && e.push(og.configure(this.options.strike)), this.options.text !== !1 && e.push(sg.configure(this.options.text)), this.options.underline !== !1 && e.push(cg.configure(this.options?.underline)), this.options.trailingNode !== !1 && e.push(h_.configure(this.options?.trailingNode)), e;
	}
}), v_ = new A("deviceAnnotations"), y_ = /* @__PURE__ */ new Set([
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
function b_(e) {
	let t = "", n = [];
	function r(e, i) {
		if (e.isText) {
			t += e.text;
			for (let t = 0; t < e.text.length; t++) n.push(i + t);
		} else e.forEach((t, n) => r(t, i + n + (e.type.name === "doc" ? 0 : 1))), y_.has(e.type.name) && (t += "\n", n.push(void 0));
	}
	return r(e, 0), {
		text: t,
		positions: n
	};
}
function x_(e, t) {
	if (!t) return null;
	let n = e.text.indexOf(t);
	if (n < 0 || e.text.indexOf(t, n + 1) >= 0) return null;
	let r = e.positions.slice(n, n + t.length).filter((e) => e !== void 0);
	return r.length ? {
		from: r[0],
		to: r[r.length - 1] + 1
	} : null;
}
function S_(e, t) {
	if (!t.length) return L.empty;
	let n = b_(e), r = [];
	for (let e of t) {
		let t = x_(n, e.quote);
		if (!t) continue;
		let i = [
			"comment",
			"todo",
			"highlight"
		].includes(e.kind) ? e.kind : "comment", a = e.status === "resolved" ? "resolved" : "open";
		r.push(I.inline(t.from, t.to, {
			class: `wa-annotation wa-annotation-${i} wa-annotation-${a}`,
			"data-annotation-id": e.id,
			title: e.content
		}));
	}
	return L.create(e, r);
}
function C_(e) {
	return W.create({
		name: "deviceAnnotations",
		addProseMirrorPlugins() {
			return [new k({
				key: v_,
				state: {
					init: () => ({
						items: [],
						decorations: L.empty
					}),
					apply(e, t) {
						let n = e.getMeta(v_) ?? t.items;
						return e.docChanged || n !== t.items ? {
							items: n,
							decorations: S_(e.doc, n)
						} : t;
					}
				},
				props: {
					decorations: (e) => v_.getState(e)?.decorations,
					handleDOMEvents: { click(t, n) {
						let r = n.target instanceof Element ? n.target.closest("[data-annotation-id]") : null;
						return r && t.dom.contains(r) && e(r.getAttribute("data-annotation-id")), !1;
					} }
				}
			})];
		}
	});
}
function w_(e, t) {
	e.view.dispatch(e.state.tr.setMeta(v_, t).setMeta("addToHistory", !1));
}
function T_(e, t) {
	let n = v_.getState(e.state)?.items.find((e) => e.id === t), r = n && x_(b_(e.state.doc), n.quote);
	return r ? (e.chain().setTextSelection(r).scrollIntoView().focus().run(), !0) : !1;
}
function E_(e) {
	let { from: t, to: n, empty: r } = e.state.selection;
	if (r) return "";
	let i = b_(e.state.doc), a = -1, o = -1;
	return i.positions.forEach((e, r) => {
		e !== void 0 && e >= t && e < n && (a < 0 && (a = r), o = r + 1);
	}), a < 0 ? "" : i.text.slice(a, o);
}
var D_ = W.create({
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
}), O_, k_;
if (typeof WeakMap < "u") {
	let e = /* @__PURE__ */ new WeakMap();
	O_ = (t) => e.get(t), k_ = (t, n) => (e.set(t, n), n);
} else {
	let e = [], t = 0;
	O_ = (t) => {
		for (let n = 0; n < e.length; n += 2) if (e[n] == t) return e[n + 1];
	}, k_ = (n, r) => (t == 10 && (t = 0), e[t++] = n, e[t++] = r);
}
var Z = class {
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
		return O_(e) || k_(e, A_(e));
	}
};
function A_(e) {
	if (e.type.spec.tableRole != "table") throw RangeError("Not a table node: " + e.type.name);
	let t = j_(e), n = e.childCount, r = [], i = 0, a = null, o = [];
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
	let s = new Z(t, n, r, a), c = !1;
	for (let e = 0; !c && e < o.length; e += 2) o[e] != null && o[e + 1] < n && (c = !0);
	return c && M_(s, o, e), s;
}
function j_(e) {
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
function M_(e, t, n) {
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
			r != null && (!c.colwidth || c.colwidth[n] != r) && ((s || (s = N_(c)))[n] = r);
		}
		s && e.problems.unshift({
			type: "colwidth mismatch",
			pos: a,
			colwidth: s
		});
	}
}
function N_(e) {
	if (e.colwidth) return e.colwidth.slice();
	let t = [];
	for (let n = 0; n < e.colspan; n++) t.push(0);
	return t;
}
function Q(e) {
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
var P_ = new A("selectingCells");
function F_(e) {
	for (let t = e.depth - 1; t > 0; t--) if (e.node(t).type.spec.tableRole == "row") return e.node(0).resolve(e.before(t + 1));
	return null;
}
function I_(e) {
	for (let t = e.depth; t > 0; t--) {
		let n = e.node(t).type.spec.tableRole;
		if (n === "cell" || n === "header_cell") return e.node(t);
	}
	return null;
}
function L_(e) {
	let t = e.selection.$head;
	for (let e = t.depth; e > 0; e--) if (t.node(e).type.spec.tableRole == "row") return !0;
	return !1;
}
function R_(e) {
	let t = e.selection;
	if ("$anchorCell" in t && t.$anchorCell) return t.$anchorCell.pos > t.$headCell.pos ? t.$anchorCell : t.$headCell;
	if ("node" in t && t.node && t.node.type.spec.tableRole == "cell") return t.$anchor;
	let n = F_(t.$head) || z_(t.$head);
	if (n) return n;
	throw RangeError(`No cell found around position ${t.head}`);
}
function z_(e) {
	for (let t = e.nodeAfter, n = e.pos; t; t = t.firstChild, n++) {
		let r = t.type.spec.tableRole;
		if (r == "cell" || r == "header_cell") return e.doc.resolve(n);
	}
	for (let t = e.nodeBefore, n = e.pos; t; t = t.lastChild, n--) {
		let r = t.type.spec.tableRole;
		if (r == "cell" || r == "header_cell") return e.doc.resolve(n - t.nodeSize);
	}
}
function B_(e) {
	return e.parent.type.spec.tableRole == "row" && !!e.nodeAfter;
}
function V_(e) {
	return e.node(0).resolve(e.pos + e.nodeAfter.nodeSize);
}
function H_(e, t) {
	return e.depth == t.depth && e.pos >= t.start(-1) && e.pos <= t.end(-1);
}
function U_(e, t, n) {
	let r = e.node(-1), i = Z.get(r), a = e.start(-1), o = i.nextCell(e.pos - a, t, n);
	return o == null ? null : e.node(0).resolve(a + o);
}
function W_(e, t, n = 1) {
	let r = {
		...e,
		colspan: e.colspan - n
	};
	return r.colwidth && (r.colwidth = r.colwidth.slice(), r.colwidth.splice(t, n), r.colwidth.some((e) => e > 0) || (r.colwidth = null)), r;
}
function G_(e, t, n = 1) {
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
function K_(e, t, n) {
	let r = Q(t.type.schema).header_cell;
	for (let i = 0; i < e.height; i++) if (t.nodeAt(e.map[n + i * e.width]).type != r) return !1;
	return !0;
}
var $ = class e extends E {
	constructor(e, t = e) {
		let n = e.node(-1), r = Z.get(n), i = e.start(-1), a = r.rectBetween(e.pos - i, t.pos - i), o = e.node(0), s = r.cellsInRect(a).filter((e) => e != t.pos - i);
		s.unshift(t.pos - i);
		let c = s.map((e) => {
			let t = n.nodeAt(e);
			if (!t) throw RangeError(`No cell with offset ${e} found`);
			let r = i + e + 1;
			return new sn(o.resolve(r), o.resolve(r + t.content.size));
		});
		super(c[0].$from, c[0].$to, c), this.$anchorCell = e, this.$headCell = t;
	}
	map(t, n) {
		let r = t.resolve(n.map(this.$anchorCell.pos)), i = t.resolve(n.map(this.$headCell.pos));
		if (B_(r) && B_(i) && H_(r, i)) {
			let t = this.$anchorCell.node(-1) != r.node(-1);
			return t && this.isRowSelection() ? e.rowSelection(r, i) : t && this.isColSelection() ? e.colSelection(r, i) : new e(r, i);
		}
		return D.between(r, i);
	}
	content() {
		let e = this.$anchorCell.node(-1), t = Z.get(e), n = this.$anchorCell.start(-1), r = t.rectBetween(this.$anchorCell.pos - n, this.$headCell.pos - n), i = {}, o = [];
		for (let n = r.top; n < r.bottom; n++) {
			let s = [];
			for (let a = n * t.width + r.left, o = r.left; o < r.right; o++, a++) {
				let n = t.map[a];
				if (i[n]) continue;
				i[n] = !0;
				let o = t.findCell(n), c = e.nodeAt(n);
				if (!c) throw RangeError(`No cell with offset ${n} found`);
				let l = r.left - o.left, u = o.right - r.right;
				if (l > 0 || u > 0) {
					let e = c.attrs;
					if (l > 0 && (e = W_(e, 0, l)), u > 0 && (e = W_(e, e.colspan - u, u)), o.left < r.left) {
						if (c = c.type.createAndFill(e), !c) throw RangeError(`Could not create cell with attrs ${JSON.stringify(e)}`);
					} else c = c.type.create(e, c.content);
				}
				if (o.top < r.top || o.bottom > r.bottom) {
					let e = {
						...c.attrs,
						rowspan: Math.min(o.bottom, r.bottom) - Math.max(o.top, r.top)
					};
					c = o.top < r.top ? c.type.createAndFill(e) : c.type.create(e, c.content);
				}
				s.push(c);
			}
			o.push(e.child(n).copy(a.from(s)));
		}
		let s = this.isColSelection() && this.isRowSelection() ? e : o;
		return new d(a.from(s), 1, 1);
	}
	replace(e, t = d.empty) {
		let n = e.steps.length, r = this.ranges;
		for (let i = 0; i < r.length; i++) {
			let { $from: a, $to: o } = r[i], s = e.mapping.slice(n);
			e.replace(s.map(a.pos), s.map(o.pos), i ? d.empty : t);
		}
		let i = E.findFrom(e.doc.resolve(e.mapping.slice(n).map(this.to)), -1);
		i && e.setSelection(i);
	}
	replaceWith(e, t) {
		this.replace(e, new d(a.from(t), 0, 0));
	}
	forEachCell(e) {
		let t = this.$anchorCell.node(-1), n = Z.get(t), r = this.$anchorCell.start(-1), i = n.cellsInRect(n.rectBetween(this.$anchorCell.pos - r, this.$headCell.pos - r));
		for (let n = 0; n < i.length; n++) e(t.nodeAt(i[n]), r + i[n]);
	}
	isColSelection() {
		let e = this.$anchorCell.index(-1), t = this.$headCell.index(-1);
		if (Math.min(e, t) > 0) return !1;
		let n = e + this.$anchorCell.nodeAfter.attrs.rowspan, r = t + this.$headCell.nodeAfter.attrs.rowspan;
		return Math.max(n, r) == this.$headCell.node(-1).childCount;
	}
	static colSelection(t, n = t) {
		let r = t.node(-1), i = Z.get(r), a = t.start(-1), o = i.findCell(t.pos - a), s = i.findCell(n.pos - a), c = t.node(0);
		return o.top <= s.top ? (o.top > 0 && (t = c.resolve(a + i.map[o.left])), s.bottom < i.height && (n = c.resolve(a + i.map[i.width * (i.height - 1) + s.right - 1]))) : (s.top > 0 && (n = c.resolve(a + i.map[s.left])), o.bottom < i.height && (t = c.resolve(a + i.map[i.width * (i.height - 1) + o.right - 1]))), new e(t, n);
	}
	isRowSelection() {
		let e = this.$anchorCell.node(-1), t = Z.get(e), n = this.$anchorCell.start(-1), r = t.colCount(this.$anchorCell.pos - n), i = t.colCount(this.$headCell.pos - n);
		if (Math.min(r, i) > 0) return !1;
		let a = r + this.$anchorCell.nodeAfter.attrs.colspan, o = i + this.$headCell.nodeAfter.attrs.colspan;
		return Math.max(a, o) == t.width;
	}
	eq(t) {
		return t instanceof e && t.$anchorCell.pos == this.$anchorCell.pos && t.$headCell.pos == this.$headCell.pos;
	}
	static rowSelection(t, n = t) {
		let r = t.node(-1), i = Z.get(r), a = t.start(-1), o = i.findCell(t.pos - a), s = i.findCell(n.pos - a), c = t.node(0);
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
		return new q_(this.$anchorCell.pos, this.$headCell.pos);
	}
};
$.prototype.visible = !1, E.jsonID("cell", $);
var q_ = class e {
	constructor(e, t) {
		this.anchor = e, this.head = t;
	}
	map(t) {
		return new e(t.map(this.anchor), t.map(this.head));
	}
	resolve(e) {
		let t = e.resolve(this.anchor), n = e.resolve(this.head);
		return t.parent.type.spec.tableRole == "row" && n.parent.type.spec.tableRole == "row" && t.index() < t.parent.childCount && n.index() < n.parent.childCount && H_(t, n) ? new $(t, n) : E.near(n, 1);
	}
};
function J_(e) {
	if (!(e.selection instanceof $)) return null;
	let t = [];
	return e.selection.forEachCell((e, n) => {
		t.push(I.node(n, n + e.nodeSize, { class: "selectedCell" }));
	}), L.create(e.doc, t);
}
function Y_({ $from: e, $to: t }) {
	if (e.pos == t.pos || e.pos < t.pos - 6) return !1;
	let n = e.pos, r = t.pos, i = e.depth;
	for (; i >= 0 && !(e.after(i + 1) < e.end(i)); i--, n++);
	for (let e = t.depth; e >= 0 && !(t.before(e + 1) > t.start(e)); e--, r--);
	return n == r && /row|table/.test(e.node(i).type.spec.tableRole);
}
function X_({ $from: e, $to: t }) {
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
function Z_(e, t, n) {
	let r = (t || e).selection, i = (t || e).doc, a, o;
	if (r instanceof O && (o = r.node.type.spec.tableRole)) {
		if (o == "cell" || o == "header_cell") a = $.create(i, r.from);
		else if (o == "row") {
			let e = i.resolve(r.from + 1);
			a = $.rowSelection(e, e);
		} else if (!n) {
			let e = Z.get(r.node), t = r.from + 1, n = t + e.map[e.width * e.height - 1];
			a = $.create(i, t + 1, n);
		}
	} else r instanceof D && Y_(r) ? a = D.create(i, r.from) : r instanceof D && X_(r) && (a = D.create(i, r.$from.start(), r.$from.end()));
	return a && (t || (t = e.tr)).setSelection(a), t;
}
var Q_ = new A("fix-tables");
function $_(e, t, n, r) {
	let i = e.childCount, a = t.childCount;
	outer: for (let o = 0, s = 0; o < a; o++) {
		let a = t.child(o);
		for (let t = s, r = Math.min(i, o + 3); t < r; t++) if (e.child(t) == a) {
			s = t + 1, n += a.nodeSize;
			continue outer;
		}
		r(a, n), s < i && e.child(s).sameMarkup(a) ? $_(e.child(s), a, n + 1, r) : a.nodesBetween(0, a.content.size, r, n + 1), n += a.nodeSize;
	}
}
function ev(e, t) {
	let n, r = (t, r) => {
		t.type.spec.tableRole == "table" && (n = tv(e, t, r, n));
	};
	return t ? t.doc != e.doc && $_(t.doc, e.doc, 0, r) : e.doc.descendants(r), n;
}
function tv(e, t, n, r) {
	let i = Z.get(t);
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
			r.setNodeMarkup(r.mapping.map(n + 1 + o.pos), null, W_(i, i.colspan - o.n, o.n));
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
				let n = Q(e.schema)[t].createAndFill();
				n && a.push(n);
			}
			let d = (c == 0 || o == c - 1) && s == c ? l + 1 : i - 1;
			r.insert(r.mapping.map(d), a);
		}
		l = i;
	}
	return r.setMeta(Q_, { fixTables: !0 });
}
function nv(e) {
	let t = e.selection, n = R_(e), r = n.node(-1), i = n.start(-1), a = Z.get(r);
	return {
		...t instanceof $ ? a.rectBetween(t.$anchorCell.pos - i, t.$headCell.pos - i) : a.findCell(n.pos - i),
		tableStart: i,
		map: a,
		table: r
	};
}
function rv(e, { map: t, tableStart: n, table: r }, i) {
	let a = i > 0 ? -1 : 0;
	K_(t, r, i + a) && (a = i == 0 || i == t.width ? null : 0);
	for (let o = 0; o < t.height; o++) {
		let s = o * t.width + i;
		if (i > 0 && i < t.width && t.map[s - 1] == t.map[s]) {
			let a = t.map[s], c = r.nodeAt(a);
			e.setNodeMarkup(e.mapping.map(n + a), null, G_(c.attrs, i - t.colCount(a))), o += c.attrs.rowspan - 1;
		} else {
			let c = a == null ? Q(r.type.schema).cell : r.nodeAt(t.map[s + a]).type, l = t.positionAt(o, i, r);
			e.insert(e.mapping.map(n + l), c.createAndFill());
		}
	}
	return e;
}
function iv(e, t) {
	if (!L_(e)) return !1;
	if (t) {
		let n = nv(e);
		t(rv(e.tr, n, n.left));
	}
	return !0;
}
function av(e, t) {
	if (!L_(e)) return !1;
	if (t) {
		let n = nv(e);
		t(rv(e.tr, n, n.right));
	}
	return !0;
}
function ov(e, { map: t, table: n, tableStart: r }, i) {
	let a = e.mapping.maps.length;
	for (let o = 0; o < t.height;) {
		let s = o * t.width + i, c = t.map[s], l = n.nodeAt(c), u = l.attrs;
		if (i > 0 && t.map[s - 1] == c || i < t.width - 1 && t.map[s + 1] == c) e.setNodeMarkup(e.mapping.slice(a).map(r + c), null, W_(u, i - t.colCount(c)));
		else {
			let t = e.mapping.slice(a).map(r + c);
			e.delete(t, t + l.nodeSize);
		}
		o += u.rowspan;
	}
}
function sv(e, t) {
	if (!L_(e)) return !1;
	if (t) {
		let n = nv(e), r = e.tr;
		if (n.left == 0 && n.right == n.map.width) return !1;
		for (let e = n.right - 1; ov(r, n, e), e != n.left; e--) {
			let e = n.tableStart ? r.doc.nodeAt(n.tableStart - 1) : r.doc;
			if (!e) throw RangeError("No table found");
			n.table = e, n.map = Z.get(e);
		}
		t(r);
	}
	return !0;
}
function cv(e, t, n) {
	let r = Q(t.type.schema).header_cell;
	for (let i = 0; i < e.width; i++) if (t.nodeAt(e.map[i + n * e.width])?.type != r) return !1;
	return !0;
}
function lv(e, { map: t, tableStart: n, table: r }, i) {
	let a = n;
	for (let e = 0; e < i; e++) a += r.child(e).nodeSize;
	let o = [], s = i > 0 ? -1 : 0;
	cv(t, r, i + s) && (s = i == 0 || i == t.height ? null : 0);
	for (let a = 0, c = t.width * i; a < t.width; a++, c++) if (i > 0 && i < t.height && t.map[c] == t.map[c - t.width]) {
		let i = t.map[c], o = r.nodeAt(i).attrs;
		e.setNodeMarkup(n + i, null, {
			...o,
			rowspan: o.rowspan + 1
		}), a += o.colspan - 1;
	} else {
		let e = (s == null ? Q(r.type.schema).cell : r.nodeAt(t.map[c + s * t.width])?.type)?.createAndFill();
		e && o.push(e);
	}
	return e.insert(a, Q(r.type.schema).row.create(null, o)), e;
}
function uv(e, t) {
	if (!L_(e)) return !1;
	if (t) {
		let n = nv(e);
		t(lv(e.tr, n, n.top));
	}
	return !0;
}
function dv(e, t) {
	if (!L_(e)) return !1;
	if (t) {
		let n = nv(e);
		t(lv(e.tr, n, n.bottom));
	}
	return !0;
}
function fv(e, { map: t, table: n, tableStart: r }, i) {
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
function pv(e, t) {
	if (!L_(e)) return !1;
	if (t) {
		let n = nv(e), r = e.tr;
		if (n.top == 0 && n.bottom == n.map.height) return !1;
		for (let e = n.bottom - 1; fv(r, n, e), e != n.top; e--) {
			let e = n.tableStart ? r.doc.nodeAt(n.tableStart - 1) : r.doc;
			if (!e) throw RangeError("No table found");
			n.table = e, n.map = Z.get(n.table);
		}
		t(r);
	}
	return !0;
}
function mv(e) {
	let t = e.content;
	return t.childCount == 1 && t.child(0).isTextblock && t.child(0).childCount == 0;
}
function hv({ width: e, height: t, map: n }, r) {
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
function gv(e, t) {
	let n = e.selection;
	if (!(n instanceof $) || n.$anchorCell.pos == n.$headCell.pos) return !1;
	let r = nv(e), { map: i } = r;
	if (hv(i, r)) return !1;
	if (t) {
		let n = e.tr, o = {}, s = a.empty, c, l;
		for (let e = r.top; e < r.bottom; e++) for (let t = r.left; t < r.right; t++) {
			let a = i.map[e * i.width + t], u = r.table.nodeAt(a);
			if (!o[a] && u) {
				if (o[a] = !0, c == null) c = a, l = u;
				else {
					mv(u) || (s = s.append(u.content));
					let e = n.mapping.map(a + r.tableStart);
					n.delete(e, e + u.nodeSize);
				}
			}
		}
		if (c == null || l == null) return !0;
		if (n.setNodeMarkup(c + r.tableStart, null, {
			...G_(l.attrs, l.attrs.colspan, r.right - r.left - l.attrs.colspan),
			rowspan: r.bottom - r.top
		}), s.size > 0) {
			let e = c + 1 + l.content.size, t = mv(l) ? c + 1 : e;
			n.replaceWith(t + r.tableStart, e + r.tableStart, s);
		}
		n.setSelection(new $(n.doc.resolve(c + r.tableStart))), t(n);
	}
	return !0;
}
function _v(e, t) {
	let n = Q(e.schema);
	return vv(({ node: e }) => n[e.type.spec.tableRole])(e, t);
}
function vv(e) {
	return (t, n) => {
		let r = t.selection, i, a;
		if (r instanceof $) {
			if (r.$anchorCell.pos != r.$headCell.pos) return !1;
			i = r.$anchorCell.nodeAfter, a = r.$anchorCell.pos;
		} else {
			if (i = I_(r.$from), !i) return !1;
			a = F_(r.$from)?.pos;
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
			let l = nv(t), u = t.tr;
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
function yv(e, t) {
	return function(n, r) {
		if (!L_(n)) return !1;
		let i = R_(n);
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
function bv(e) {
	return function(t, n) {
		if (!L_(t)) return !1;
		if (n) {
			let r = Q(t.schema), i = nv(t), a = t.tr, o = i.map.cellsInRect(e == "column" ? {
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
function xv(e, t, n) {
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
function Sv(e, t) {
	return t = t || { useDeprecatedLogic: !1 }, t.useDeprecatedLogic ? bv(e) : function(t, n) {
		if (!L_(t)) return !1;
		if (n) {
			let r = Q(t.schema), i = nv(t), a = t.tr, o = xv("row", i, r), s = xv("column", i, r), c = (e === "column" ? o : e === "row" && s) ? 1 : 0, l = e == "column" ? {
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
Sv("row", { useDeprecatedLogic: !0 }), Sv("column", { useDeprecatedLogic: !0 });
var Cv = Sv("cell", { useDeprecatedLogic: !0 });
function wv(e, t) {
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
function Tv(e) {
	return function(t, n) {
		if (!L_(t)) return !1;
		let r = wv(R_(t), e);
		if (r == null) return !1;
		if (n) {
			let e = t.doc.resolve(r);
			n(t.tr.setSelection(D.between(e, V_(e))).scrollIntoView());
		}
		return !0;
	};
}
function Ev(e, t) {
	let n = e.selection.$anchor;
	for (let r = n.depth; r > 0; r--) if (n.node(r).type.spec.tableRole == "table") return t && t(e.tr.delete(n.before(r), n.after(r)).scrollIntoView()), !0;
	return !1;
}
function Dv(e, t) {
	let n = e.selection;
	if (!(n instanceof $)) return !1;
	if (t) {
		let r = e.tr, i = Q(e.schema).cell.createAndFill().content;
		n.forEachCell((e, t) => {
			e.content.eq(i) || r.replace(r.mapping.map(t + 1), r.mapping.map(t + e.nodeSize - 1), new d(i, 0, 0));
		}), r.docChanged && t(r);
	}
	return !0;
}
function Ov(e) {
	if (e.size === 0) return null;
	let { content: t, openStart: n, openEnd: r } = e;
	for (; t.childCount == 1 && (n > 0 && r > 0 || t.child(0).type.spec.tableRole == "table");) n--, r--, t = t.child(0).content;
	let i = t.child(0), a = i.type.spec.tableRole, o = i.type.schema, s = [];
	if (a == "row") for (let e = 0; e < t.childCount; e++) {
		let i = t.child(e).content, a = e ? 0 : Math.max(0, n - 1), c = e < t.childCount - 1 ? 0 : Math.max(0, r - 1);
		(a || c) && (i = Av(Q(o).row, new d(i, a, c)).content), s.push(i);
	}
	else if (a == "cell" || a == "header_cell") s.push(n || r ? Av(Q(o).row, new d(t, n, r)).content : t);
	else return null;
	return kv(o, s);
}
function kv(e, t) {
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
	for (let i = 0; i < n.length; i++) if (i >= t.length && t.push(a.empty), n[i] < r) {
		let o = Q(e).cell.createAndFill(), s = [];
		for (let e = n[i]; e < r; e++) s.push(o);
		t[i] = t[i].append(a.from(s));
	}
	return {
		height: t.length,
		width: r,
		rows: t
	};
}
function Av(e, t) {
	let n = e.createAndFill();
	return new an(n).replace(0, n.content.size, t).doc;
}
function jv({ width: e, height: t, rows: n }, r, i) {
	if (e != r) {
		let t = [], i = [];
		for (let e = 0; e < n.length; e++) {
			let o = n[e], s = [];
			for (let n = t[e] || 0, i = 0; n < r; i++) {
				let a = o.child(i % o.childCount);
				n + a.attrs.colspan > r && (a = a.type.createChecked(W_(a.attrs, a.attrs.colspan, n + a.attrs.colspan - r), a.content)), s.push(a), n += a.attrs.colspan;
				for (let n = 1; n < a.attrs.rowspan; n++) t[e + n] = (t[e + n] || 0) + a.attrs.colspan;
			}
			i.push(a.from(s));
		}
		n = i, e = r;
	}
	if (t != i) {
		let e = [];
		for (let r = 0, o = 0; r < i; r++, o++) {
			let s = [], c = n[o % t];
			for (let e = 0; e < c.childCount; e++) {
				let t = c.child(e);
				r + t.attrs.rowspan > i && (t = t.type.create({
					...t.attrs,
					rowspan: Math.max(1, i - t.attrs.rowspan)
				}, t.content)), s.push(t);
			}
			e.push(a.from(s));
		}
		n = e, t = i;
	}
	return {
		width: e,
		height: t,
		rows: n
	};
}
function Mv(e, t, n, r, i, o, s) {
	let c = e.doc.type.schema, l = Q(c), u, d;
	if (i > t.width) for (let a = 0, o = 0; a < t.height; a++) {
		let c = n.child(a);
		o += c.nodeSize;
		let f = [], p;
		p = c.lastChild == null || c.lastChild.type == l.cell ? u || (u = l.cell.createAndFill()) : d || (d = l.header_cell.createAndFill());
		for (let e = t.width; e < i; e++) f.push(p);
		e.insert(e.mapping.slice(s).map(o - 1 + r), f);
	}
	if (o > t.height) {
		let c = [];
		for (let e = 0, r = (t.height - 1) * t.width; e < Math.max(t.width, i); e++) {
			let i = e >= t.width ? !1 : n.nodeAt(t.map[r + e]).type == l.header_cell;
			c.push(i ? d || (d = l.header_cell.createAndFill()) : u || (u = l.cell.createAndFill()));
		}
		let f = l.row.create(null, a.from(c)), p = [];
		for (let e = t.height; e < o; e++) p.push(f);
		e.insert(e.mapping.slice(s).map(r + n.nodeSize - 2), p);
	}
	return !!(u || d);
}
function Nv(e, t, n, r, i, a, o, s) {
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
function Pv(e, t, n, r, i, a, o, s) {
	if (o == 0 || o == t.width) return !1;
	let c = !1;
	for (let l = i; l < a; l++) {
		let i = l * t.width + o, a = t.map[i];
		if (t.map[i - 1] == a) {
			c = !0;
			let i = n.nodeAt(a), u = t.colCount(a), d = e.mapping.slice(s).map(a + r);
			e.setNodeMarkup(d, null, W_(i.attrs, o - u, i.attrs.colspan - (o - u))), e.insert(d + i.nodeSize, i.type.createAndFill(W_(i.attrs, 0, o - u))), l += i.attrs.rowspan - 1;
		}
	}
	return c;
}
function Fv(e, t, n, r, i) {
	let a = n ? e.doc.nodeAt(n - 1) : e.doc;
	if (!a) throw Error("No table found");
	let o = Z.get(a), { top: s, left: c } = r, l = c + i.width, u = s + i.height, f = e.tr, p = 0;
	function m() {
		if (a = n ? f.doc.nodeAt(n - 1) : f.doc, !a) throw Error("No table found");
		o = Z.get(a), p = f.mapping.maps.length;
	}
	Mv(f, o, a, n, l, u, p) && m(), Nv(f, o, a, n, c, l, s, p) && m(), Nv(f, o, a, n, c, l, u, p) && m(), Pv(f, o, a, n, s, u, c, p) && m(), Pv(f, o, a, n, s, u, l, p) && m();
	for (let e = s; e < u; e++) {
		let t = o.positionAt(e, c, a), r = o.positionAt(e, l, a);
		f.replace(f.mapping.slice(p).map(t + n), f.mapping.slice(p).map(r + n), new d(i.rows[e - s], 0, 0));
	}
	m(), f.setSelection(new $(f.doc.resolve(n + o.positionAt(s, c, a)), f.doc.resolve(n + o.positionAt(u - 1, l - 1, a)))), t(f);
}
var Iv = Ks({
	ArrowLeft: Rv("horiz", -1),
	ArrowRight: Rv("horiz", 1),
	ArrowUp: Rv("vert", -1),
	ArrowDown: Rv("vert", 1),
	"Shift-ArrowLeft": zv("horiz", -1),
	"Shift-ArrowRight": zv("horiz", 1),
	"Shift-ArrowUp": zv("vert", -1),
	"Shift-ArrowDown": zv("vert", 1),
	Backspace: Dv,
	"Mod-Backspace": Dv,
	Delete: Dv,
	"Mod-Delete": Dv
});
function Lv(e, t, n) {
	return !n.eq(e.selection) && (t && t(e.tr.setSelection(n).scrollIntoView()), !0);
}
function Rv(e, t) {
	return (n, r, i) => {
		if (!i) return !1;
		let a = n.selection;
		if (a instanceof $) return Lv(n, r, E.near(a.$headCell, t));
		if (e != "horiz" && !a.empty) return !1;
		let o = Uv(i, e, t);
		if (o == null) return !1;
		if (e == "horiz") return Lv(n, r, E.near(n.doc.resolve(a.head + t), t));
		{
			let i = n.doc.resolve(o), a = U_(i, e, t), s;
			return s = a ? E.near(a, 1) : t < 0 ? E.near(n.doc.resolve(i.before(-1)), -1) : E.near(n.doc.resolve(i.after(-1)), 1), Lv(n, r, s);
		}
	};
}
function zv(e, t) {
	return (n, r, i) => {
		if (!i) return !1;
		let a = n.selection, o;
		if (a instanceof $) o = a;
		else {
			let r = Uv(i, e, t);
			if (r == null) return !1;
			o = new $(n.doc.resolve(r));
		}
		let s = U_(o.$headCell, e, t);
		return s ? Lv(n, r, new $(o.$anchorCell, s)) : !1;
	};
}
function Bv(e, t) {
	let n = e.state.doc, r = F_(n.resolve(t));
	return r ? (e.dispatch(e.state.tr.setSelection(new $(r))), !0) : !1;
}
function Vv(e, t, n) {
	if (!L_(e.state)) return !1;
	let r = Ov(n), i = e.state.selection;
	if (i instanceof $) {
		r || (r = {
			width: 1,
			height: 1,
			rows: [a.from(Av(Q(e.state.schema).cell, n))]
		});
		let t = i.$anchorCell.node(-1), o = i.$anchorCell.start(-1), s = Z.get(t).rectBetween(i.$anchorCell.pos - o, i.$headCell.pos - o);
		return r = jv(r, s.right - s.left, s.bottom - s.top), Fv(e.state, e.dispatch, o, s, r), !0;
	}
	if (r) {
		let t = R_(e.state), n = t.start(-1);
		return Fv(e.state, e.dispatch, n, Z.get(t.node(-1)).findCell(t.pos - n), r), !0;
	}
	return !1;
}
function Hv(e, t) {
	if (t.button != 0 || t.ctrlKey || t.metaKey) return;
	let n = Wv(e, t.target), r;
	if (t.shiftKey && e.state.selection instanceof $) i(e.state.selection.$anchorCell, t), t.preventDefault();
	else if (t.shiftKey && n && (r = F_(e.state.selection.$anchor)) != null && Gv(e, t)?.pos != r.pos) i(r, t), t.preventDefault();
	else if (!n) return;
	function i(t, n) {
		let r = Gv(e, n), i = P_.getState(e.state) == null;
		if (!r || !H_(t, r)) {
			if (i) r = t;
			else return;
		}
		let a = new $(t, r);
		if (i || !e.state.selection.eq(a)) {
			let n = e.state.tr.setSelection(a);
			i && n.setMeta(P_, t.pos), e.dispatch(n);
		}
	}
	function a() {
		e.root.removeEventListener("mouseup", a), e.root.removeEventListener("dragstart", a), e.root.removeEventListener("mousemove", o), P_.getState(e.state) != null && e.dispatch(e.state.tr.setMeta(P_, -1));
	}
	function o(r) {
		let o = r, s = P_.getState(e.state), c;
		if (s != null) c = e.state.doc.resolve(s);
		else if (Wv(e, o.target) != n && (c = Gv(e, t), !c)) return a();
		c && i(c, o);
	}
	e.root.addEventListener("mouseup", a), e.root.addEventListener("dragstart", a), e.root.addEventListener("mousemove", o);
}
function Uv(e, t, n) {
	if (!(e.state.selection instanceof D)) return null;
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
function Wv(e, t) {
	for (; t && t != e.dom; t = t.parentNode) if (t.nodeName == "TD" || t.nodeName == "TH") return t;
	return null;
}
function Gv(e, t) {
	let n = e.posAtCoords({
		left: t.clientX,
		top: t.clientY
	});
	if (!n) return null;
	let { inside: r, pos: i } = n;
	return r >= 0 && F_(e.state.doc.resolve(r)) || F_(e.state.doc.resolve(i));
}
var Kv = class {
	constructor(e, t) {
		this.node = e, this.defaultCellMinWidth = t, this.dom = document.createElement("div"), this.dom.className = "tableWrapper", this.table = this.dom.appendChild(document.createElement("table")), this.table.style.setProperty("--default-cell-min-width", `${t}px`), this.colgroup = this.table.appendChild(document.createElement("colgroup")), qv(e, this.colgroup, this.table, t), this.contentDOM = this.table.appendChild(document.createElement("tbody"));
	}
	update(e) {
		return e.type == this.node.type && (this.node = e, qv(e, this.colgroup, this.table, this.defaultCellMinWidth), !0);
	}
	ignoreMutation(e) {
		return e.type == "attributes" && (e.target == this.table || this.colgroup.contains(e.target));
	}
};
function qv(e, t, n, r, i, a) {
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
var Jv = new A("tableColumnResizing");
function Yv({ handleWidth: e = 5, cellMinWidth: t = 25, defaultCellMinWidth: n = 100, View: r = Kv, lastColumnResizable: i = !0 } = {}) {
	let a = new k({
		key: Jv,
		state: {
			init(e, t) {
				var i;
				let o = (i = a.spec) == null || (i = i.props) == null ? void 0 : i.nodeViews, s = Q(t.schema).table.name;
				return r && o && (o[s] = (e, t) => new r(e, n, t)), new Xv(-1, !1);
			},
			apply(e, t) {
				return t.apply(e);
			}
		},
		props: {
			attributes: (e) => {
				let t = Jv.getState(e);
				return t && t.activeHandle > -1 ? { class: "resize-cursor" } : {};
			},
			handleDOMEvents: {
				mousemove: (t, n) => {
					Zv(t, n, e, i);
				},
				mouseleave: (e) => {
					Qv(e);
				},
				mousedown: (e, r) => {
					$v(e, r, t, n);
				}
			},
			decorations: (e) => {
				let t = Jv.getState(e);
				if (t && t.activeHandle > -1) return cy(e, t.activeHandle);
			},
			nodeViews: {}
		}
	});
	return a;
}
var Xv = class e {
	constructor(e, t) {
		this.activeHandle = e, this.dragging = t;
	}
	apply(t) {
		let n = this, r = t.getMeta(Jv);
		if (r && r.setHandle != null) return new e(r.setHandle, !1);
		if (r && r.setDragging !== void 0) return new e(n.activeHandle, r.setDragging);
		if (n.activeHandle > -1 && t.docChanged) {
			let r = t.mapping.map(n.activeHandle, -1);
			return B_(t.doc.resolve(r)) || (r = -1), new e(r, n.dragging);
		}
		return n;
	}
};
function Zv(e, t, n, r) {
	if (!e.editable) return;
	let i = Jv.getState(e.state);
	if (i && !i.dragging) {
		let a = ty(t.target), o = -1;
		if (a) {
			let { left: r, right: i } = a.getBoundingClientRect();
			t.clientX - r <= n ? o = ny(e, t, "left", n) : i - t.clientX <= n && (o = ny(e, t, "right", n));
		}
		if (o != i.activeHandle) {
			if (!r && o !== -1) {
				let t = e.state.doc.resolve(o), n = t.node(-1), r = Z.get(n), i = t.start(-1);
				if (r.colCount(t.pos - i) + t.nodeAfter.attrs.colspan - 1 == r.width - 1) return;
			}
			iy(e, o);
		}
	}
}
function Qv(e) {
	if (!e.editable) return;
	let t = Jv.getState(e.state);
	t && t.activeHandle > -1 && !t.dragging && iy(e, -1);
}
function $v(e, t, n, r) {
	if (!e.editable) return !1;
	let i = e.dom.ownerDocument.defaultView ?? window, a = Jv.getState(e.state);
	if (!a || a.activeHandle == -1 || a.dragging) return !1;
	let o = e.state.doc.nodeAt(a.activeHandle), s = ey(e, a.activeHandle, o.attrs);
	e.dispatch(e.state.tr.setMeta(Jv, { setDragging: {
		startX: t.clientX,
		startWidth: s
	} }));
	function c(t) {
		i.removeEventListener("mouseup", c), i.removeEventListener("mousemove", l);
		let r = Jv.getState(e.state);
		r?.dragging && (ay(e, r.activeHandle, ry(r.dragging, t, n)), e.dispatch(e.state.tr.setMeta(Jv, { setDragging: null })));
	}
	function l(t) {
		if (!t.which) return c(t);
		let i = Jv.getState(e.state);
		if (i && i.dragging) {
			let a = ry(i.dragging, t, n);
			oy(e, i.activeHandle, a, r);
		}
	}
	return oy(e, a.activeHandle, s, r), i.addEventListener("mouseup", c), i.addEventListener("mousemove", l), t.preventDefault(), !0;
}
function ey(e, t, { colspan: n, colwidth: r }) {
	let i = r && r[r.length - 1];
	if (i) return i;
	let a = e.domAtPos(t), o = a.node.childNodes[a.offset].offsetWidth, s = n;
	if (r) for (let e = 0; e < n; e++) r[e] && (o -= r[e], s--);
	return o / s;
}
function ty(e) {
	for (; e && e.nodeName != "TD" && e.nodeName != "TH";) e = e.classList && e.classList.contains("ProseMirror") ? null : e.parentNode;
	return e;
}
function ny(e, t, n, r) {
	let i = n == "right" ? -r : r, a = e.posAtCoords({
		left: t.clientX + i,
		top: t.clientY
	});
	if (!a) return -1;
	let { pos: o } = a, s = F_(e.state.doc.resolve(o));
	if (!s) return -1;
	if (n == "right") return s.pos;
	let c = Z.get(s.node(-1)), l = s.start(-1), u = c.map.indexOf(s.pos - l);
	return u % c.width == 0 ? -1 : l + c.map[u - 1];
}
function ry(e, t, n) {
	let r = t.clientX - e.startX;
	return Math.max(n, e.startWidth + r);
}
function iy(e, t) {
	e.dispatch(e.state.tr.setMeta(Jv, { setHandle: t }));
}
function ay(e, t, n) {
	let r = e.state.doc.resolve(t), i = r.node(-1), a = Z.get(i), o = r.start(-1), s = a.colCount(r.pos - o) + r.nodeAfter.attrs.colspan - 1, c = e.state.tr;
	for (let e = 0; e < a.height; e++) {
		let t = e * a.width + s;
		if (e && a.map[t] == a.map[t - a.width]) continue;
		let r = a.map[t], l = i.nodeAt(r).attrs, u = l.colspan == 1 ? 0 : s - a.colCount(r);
		if (l.colwidth && l.colwidth[u] == n) continue;
		let d = l.colwidth ? l.colwidth.slice() : sy(l.colspan);
		d[u] = n, c.setNodeMarkup(o + r, null, {
			...l,
			colwidth: d
		});
	}
	c.docChanged && e.dispatch(c);
}
function oy(e, t, n, r) {
	let i = e.state.doc.resolve(t), a = i.node(-1), o = i.start(-1), s = Z.get(a).colCount(i.pos - o) + i.nodeAfter.attrs.colspan - 1, c = e.domAtPos(i.start(-1)).node;
	for (; c && c.nodeName != "TABLE";) c = c.parentNode;
	c && qv(a, c.firstChild, c, r, s, n);
}
function sy(e) {
	return Array(e).fill(0);
}
function cy(e, t) {
	let n = [], r = e.doc.resolve(t), i = r.node(-1);
	if (!i) return L.empty;
	let a = Z.get(i), o = r.start(-1), s = a.colCount(r.pos - o) + r.nodeAfter.attrs.colspan - 1;
	for (let t = 0; t < a.height; t++) {
		let r = s + t * a.width;
		if ((s == a.width - 1 || a.map[r] != a.map[r + 1]) && (t == 0 || a.map[r] != a.map[r - a.width])) {
			let t = a.map[r], s = o + t + i.nodeAt(t).nodeSize - 1, c = document.createElement("div");
			c.className = "column-resize-handle", Jv.getState(e)?.dragging && n.push(I.node(o + t, o + t + i.nodeAt(t).nodeSize, { class: "column-resize-dragging" })), n.push(I.widget(s, c));
		}
	}
	return L.create(e.doc, n);
}
function ly({ allowTableNodeSelection: e = !1 } = {}) {
	return new k({
		key: P_,
		state: {
			init() {
				return null;
			},
			apply(e, t) {
				let n = e.getMeta(P_);
				if (n != null) return n == -1 ? null : n;
				if (t == null || !e.docChanged) return t;
				let { deleted: r, pos: i } = e.mapping.mapResult(t);
				return r ? null : i;
			}
		},
		props: {
			decorations: J_,
			handleDOMEvents: { mousedown: Hv },
			createSelectionBetween(e) {
				return P_.getState(e.state) == null ? null : e.state.selection;
			},
			handleTripleClick: Bv,
			handleKeyDown: Iv,
			handlePaste: Vv
		},
		appendTransaction(t, n, r) {
			return Z_(r, ev(r, n), e);
		}
	});
}
//#endregion
//#region node_modules/@tiptap/extension-table/dist/index.js
function uy(e) {
	return e === "left" || e === "right" || e === "center" ? e : null;
}
function dy(e) {
	let t = (e.style.textAlign || "").trim().toLowerCase(), n = (e.getAttribute("align") || "").trim().toLowerCase();
	return uy(t || n);
}
function fy(e) {
	return uy(e?.align);
}
function py() {
	return {
		default: null,
		parseHTML: (e) => dy(e),
		renderHTML: (e) => e.align ? { style: `text-align: ${e.align}` } : {}
	};
}
function my(e) {
	let t = e.parentElement, n = e.closest("table");
	if (!t || !n) return null;
	let r = Array.from(t.children).indexOf(e), i = n.querySelectorAll("colgroup > col")[r]?.getAttribute("width");
	return i ? [parseInt(i, 10)] : null;
}
function hy(e) {
	let t = e.getAttribute("colwidth");
	return t ? t.split(",").map((e) => parseInt(e, 10)) : my(e);
}
var gy = /[ \t\r\n\f]+/g;
function _y(e) {
	return e.children.length > 0 ? !1 : (e.textContent ?? "").replace(gy, "") === "";
}
function vy(e) {
	let t = e.createAndFill();
	if (!t) throw Error(`[tiptap error]: "${e.name}" has no default content to backfill.`);
	return t.content;
}
var yy = G.create({
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
				parseHTML: hy
			},
			align: py()
		};
	},
	tableRole: "cell",
	isolating: !0,
	parseHTML() {
		return [{
			tag: "td",
			getAttrs: (e) => _y(e) ? {} : !1,
			getContent: (e, t) => vy(t.nodes[this.name])
		}, { tag: "td" }];
	},
	renderHTML({ HTMLAttributes: e }) {
		return [
			"td",
			U(this.options.HTMLAttributes, e),
			0
		];
	}
}), by = G.create({
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
				parseHTML: hy
			},
			align: py()
		};
	},
	tableRole: "header_cell",
	isolating: !0,
	parseHTML() {
		return [{
			tag: "th",
			getAttrs: (e) => _y(e) ? {} : !1,
			getContent: (e, t) => vy(t.nodes[this.name])
		}, { tag: "th" }];
	},
	renderHTML({ HTMLAttributes: e }) {
		return [
			"th",
			U(this.options.HTMLAttributes, e),
			0
		];
	}
}), xy = G.create({
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
			U(this.options.HTMLAttributes, e),
			0
		];
	}
});
function Sy(e, t) {
	return t ? ["width", `${Math.max(t, e)}px`] : ["min-width", `${e}px`];
}
function Cy(e, t, n, r, i, a) {
	let o = 0, s = !0, c = t.firstChild, l = e.firstChild;
	if (l !== null) for (let e = 0, n = 0; e < l.childCount; e += 1) {
		let { colspan: u, colwidth: d } = l.child(e).attrs;
		for (let e = 0; e < u; e += 1, n += 1) {
			let l = i === n ? a : d && d[e], u = l ? `${l}px` : "";
			if (o += l || r, l || (s = !1), c) {
				if (c.style.width !== u) {
					let [e, t] = Sy(r, l);
					c.style.setProperty(e, t);
				}
				c = c.nextSibling;
			} else {
				let e = document.createElement("col"), [n, i] = Sy(r, l);
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
var wy = class {
	constructor(e, t, n, r = {}) {
		this.node = e, this.cellMinWidth = t, this.dom = document.createElement("div"), this.dom.className = "tableWrapper", this.table = this.dom.appendChild(document.createElement("table"));
		for (let [e, t] of Object.entries(r)) t != null && (e === "style" ? this.table.style.cssText = String(t) : this.table.setAttribute(e, String(t)));
		e.attrs.style && (this.table.style.cssText = e.attrs.style), this.colgroup = this.table.appendChild(document.createElement("colgroup")), Cy(e, this.colgroup, this.table, t), this.contentDOM = this.table.appendChild(document.createElement("tbody"));
	}
	update(e) {
		return e.type === this.node.type && (this.node = e, Cy(e, this.colgroup, this.table, this.cellMinWidth), !0);
	}
	ignoreMutation(e) {
		let t = e.target, n = this.dom.contains(t), r = this.contentDOM.contains(t);
		return !(!n || r || e.type !== "attributes" && e.type !== "childList" && e.type !== "characterData");
	}
};
function Ty(e, t, n, r) {
	let i = 0, a = !0, o = [], s = e.firstChild;
	if (!s) return {};
	for (let e = 0, c = 0; e < s.childCount; e += 1) {
		let { colspan: l, colwidth: u } = s.child(e).attrs;
		for (let e = 0; e < l; e += 1, c += 1) {
			let s = n === c ? r : u && u[e];
			i += s || t, s || (a = !1);
			let [l, d] = Sy(t, s);
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
function Ey(e, t) {
	return t ? e.createChecked(null, t) : e.createAndFill();
}
function Dy(e) {
	if (e.cached.tableNodeTypes) return e.cached.tableNodeTypes;
	let t = {};
	return Object.keys(e.nodes).forEach((n) => {
		let r = e.nodes[n];
		r.spec.tableRole && (t[r.spec.tableRole] = r);
	}), e.cached.tableNodeTypes = t, t;
}
function Oy(e, t, n, r, i) {
	let a = Dy(e), o = [], s = [];
	for (let e = 0; e < n; e += 1) {
		let e = Ey(a.cell, i);
		if (e && s.push(e), r) {
			let e = Ey(a.header_cell, i);
			e && o.push(e);
		}
	}
	let c = [];
	for (let e = 0; e < t; e += 1) c.push(a.row.createChecked(null, r && e === 0 ? o : s));
	return a.table.createChecked(null, c);
}
function ky(e) {
	return e instanceof $;
}
var Ay = ({ editor: e }) => {
	let { selection: t } = e.state;
	if (!ky(t)) return !1;
	let n = 0;
	return pl(t.ranges[0].$from, (e) => e.type.name === "table")?.node.descendants((e) => {
		if (e.type.name === "table") return !1;
		["tableCell", "tableHeader"].includes(e.type.name) && (n += 1);
	}), n === t.ranges.length && (e.commands.deleteTable(), !0);
};
function jy(e, t) {
	let n = e.mapping.map(t);
	if (pl(e.selection.$from, (e) => e.type.name === "table")?.pos === n) return;
	let r = e.doc.nodeAt(n);
	if (!r) return;
	let i = n + r.nodeSize - 1;
	e.setSelection(D.near(e.doc.resolve(i), -1));
}
function My(e) {
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
function Ny(e) {
	return e.split("\n").map((e) => !e.includes("|") || !e.includes("`") ? e : My(e)).join("\n");
}
function Py(e) {
	return (e || "").replace(/\s+/g, " ").trim();
}
function Fy(e, t, n = {}) {
	let r = n.cellLineSeparator ?? "";
	if (!e || !e.content || e.content.length === 0) return "";
	let i = [];
	e.content.forEach((e) => {
		let n = [];
		e.content && e.content.forEach((e) => {
			let i = "";
			i = e.content && Array.isArray(e.content) && e.content.length > 1 ? e.content.map((e) => t.renderChildren(e)).join(r) : e.content ? t.renderChildren(e.content) : "";
			let a = Py(i.split(r).join("\n").replace(/[ \t]*\r?\n[ \t]*/g, "<br>")), o = e.type === "tableHeader", s = fy(e.attrs);
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
var Iy = G.create({
	name: "table",
	addOptions() {
		return {
			HTMLAttributes: {},
			resizable: !1,
			renderWrapper: !1,
			handleWidth: 5,
			cellMinWidth: 25,
			View: wy,
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
		let { colgroup: n, tableWidth: r, tableMinWidth: i } = Ty(e, this.options.cellMinWidth), a = t.style;
		function o() {
			return a || (r ? `width: ${r}` : `min-width: ${i}`);
		}
		let s = [
			"table",
			U(this.options.HTMLAttributes, t, { style: o() }),
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
				let a = uy(r[n] ?? e.align), o = a ? { align: a } : {};
				i.push(t.createNode("tableHeader", o, [{
					type: "paragraph",
					content: t.parseInline(e.tokens)
				}]));
			}), n.push(t.createNode("tableRow", {}, i));
		}
		return e.rows && e.rows.forEach((e) => {
			let i = [];
			e.forEach((e, n) => {
				let a = uy(r[n] ?? e.align), o = a ? { align: a } : {};
				i.push(t.createNode("tableCell", o, [{
					type: "paragraph",
					content: t.parseInline(e.tokens)
				}]));
			}), n.push(t.createNode("tableRow", {}, i));
		}), t.createNode("table", void 0, n);
	},
	renderMarkdown: (e, t) => Fy(e, t),
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
			let s = Ny(i);
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
				let o = Oy(a.schema, e, t, n);
				if (i) {
					let e = r.selection.from + 1;
					r.replaceSelectionWith(o).scrollIntoView().setSelection(D.near(r.doc.resolve(e)));
				}
				return !0;
			},
			addColumnBefore: () => ({ state: e, dispatch: t }) => iv(e, t),
			addColumnAfter: () => ({ state: e, dispatch: t }) => av(e, t),
			deleteColumn: () => ({ state: e, dispatch: t }) => {
				let n = pl(e.selection.$from, (e) => e.type.name === "table");
				return sv(e, t && ((e) => {
					n && jy(e, n.pos), t(e);
				}));
			},
			addRowBefore: () => ({ state: e, dispatch: t }) => uv(e, t),
			addRowAfter: () => ({ state: e, dispatch: t }) => dv(e, t),
			deleteRow: () => ({ state: e, dispatch: t }) => {
				let n = pl(e.selection.$from, (e) => e.type.name === "table");
				return pv(e, t && ((e) => {
					n && jy(e, n.pos), t(e);
				}));
			},
			deleteTable: () => ({ state: e, dispatch: t }) => Ev(e, t),
			mergeCells: () => ({ state: e, dispatch: t }) => gv(e, t),
			splitCell: () => ({ state: e, dispatch: t }) => _v(e, t),
			toggleHeaderColumn: () => ({ state: e, dispatch: t }) => Sv("column")(e, t),
			toggleHeaderRow: () => ({ state: e, dispatch: t }) => Sv("row")(e, t),
			toggleHeaderCell: () => ({ state: e, dispatch: t }) => Cv(e, t),
			mergeOrSplit: () => ({ state: e, dispatch: t }) => gv(e, t) ? !0 : _v(e, t),
			setCellAttribute: (e, t) => ({ state: n, dispatch: r }) => yv(e, t)(n, r),
			goToNextCell: () => ({ state: e, dispatch: t }) => Tv(1)(e, t),
			goToPreviousCell: () => ({ state: e, dispatch: t }) => Tv(-1)(e, t),
			fixTables: () => ({ state: e, dispatch: t }) => (t && ev(e), !0),
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
			Backspace: Ay,
			"Mod-Backspace": Ay,
			Delete: Ay,
			"Mod-Delete": Ay
		};
	},
	addProseMirrorPlugins() {
		return [...this.options.resizable && this.editor.isEditable ? [Yv({
			handleWidth: this.options.handleWidth,
			cellMinWidth: this.options.cellMinWidth,
			defaultCellMinWidth: this.options.cellMinWidth,
			View: this.options.View,
			lastColumnResizable: this.options.lastColumnResizable
		})] : [], ly({ allowTableNodeSelection: this.options.allowTableNodeSelection })];
	},
	addNodeView() {
		let e = this.options.resizable && this.editor.isEditable, t = this.options.View;
		return e || !t ? null : ({ node: e, view: n, HTMLAttributes: r }) => {
			let i = U(this.options.HTMLAttributes, r);
			return new t(e, this.options.cellMinWidth, n, i);
		};
	},
	extendNodeSchema(e) {
		return { tableRole: H(V(e, "tableRole", {
			name: e.name,
			options: e.options,
			storage: e.storage
		})) };
	}
});
W.create({
	name: "tableKit",
	addExtensions() {
		let e = [];
		return this.options.table !== !1 && e.push(Iy.configure(this.options.table)), this.options.tableCell !== !1 && e.push(yy.configure(this.options.tableCell)), this.options.tableHeader !== !1 && e.push(by.configure(this.options.tableHeader)), this.options.tableRow !== !1 && e.push(xy.configure(this.options.tableRow)), e;
	}
});
//#endregion
//#region node_modules/@tiptap/extension-image/dist/index.js
var Ly = /(?:^|\s)(!\[(.+|:?)]\((\S+)(?:(?:\s+)["'](\S+)["'])?\))$/, Ry = G.create({
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
		return ["img", U(this.options.HTMLAttributes, e)];
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
			let u = U(this.options.HTMLAttributes, s);
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
			let p = new Yd({
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
					let t = Cl(e, c.extensionManager.attributes.filter((t) => t.type === e.type.name));
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
		return [Gd({
			find: Ly,
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
}), zy = 2, By = 8;
function Vy(e) {
	if (!e) return 0;
	let t = e.getAttribute?.("data-indent-level");
	if (t) {
		let e = Number.parseInt(t, 10);
		if (Number.isFinite(e)) return Math.max(0, Math.min(By, e));
	}
	let n = e.style?.marginLeft;
	if (!n) return 0;
	let r = String(n).match(/([\d.]+)/);
	if (!r) return 0;
	let i = Number.parseFloat(r[1]);
	if (!Number.isFinite(i)) return 0;
	let a = Math.round(i / zy);
	return Math.max(0, Math.min(By, a));
}
function Hy(e) {
	return Number.isFinite(e) ? Math.max(0, Math.min(By, Math.round(e))) : 0;
}
var Uy = W.create({
	name: "indent",
	addOptions() {
		return { types: ["paragraph", "heading"] };
	},
	addGlobalAttributes() {
		return [{
			types: this.options.types,
			attributes: { indentLevel: {
				default: 0,
				parseHTML: (e) => Vy(e),
				renderHTML: (e) => {
					let t = Hy(e.indentLevel);
					return t ? {
						"data-indent-level": String(t),
						style: `margin-left: ${t * zy}em;`
					} : {};
				}
			} }
		}];
	},
	addCommands() {
		let e = (e) => ({ state: t, tr: n, dispatch: r }) => {
			let { from: i, to: a, empty: o, $from: s } = t.selection, c = new Set(this.options.types ?? []), l = !1, u = (t, r) => {
				if (!t || !t.isTextblock || !c.has(t.type.name)) return;
				let i = Hy(t.attrs?.indentLevel ?? 0), a = Hy(i + e);
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
});
//#endregion
//#region src/tiptap-commands.ts
function Wy(e) {
	e && e.chain().focus().toggleBold().run();
}
function Gy(e) {
	e && e.chain().focus().toggleItalic().run();
}
function Ky(e) {
	e && e.chain().focus().setParagraph().run();
}
function qy(e, t) {
	e && e.chain().focus().setHeading({ level: t }).run();
}
function Jy(e) {
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
function Yy(e) {
	e && e.chain().focus().toggleBulletList().run();
}
function Xy(e) {
	e && e.chain().focus().toggleOrderedList().run();
}
function Zy(e) {
	e && e.chain().focus().undo().run();
}
function Qy(e) {
	e && e.chain().focus().redo().run();
}
var $y = {
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
}, eb = new Set($y.tags.map((e) => e.toUpperCase())), tb = new Set($y.nodes), nb = new Set($y.marks), rb = (e) => {
	if (/[\u0000-\u001f\u007f]/.test(e)) return !1;
	try {
		return $y.linkSchemes.includes(new URL(e.trim()).protocol.slice(0, -1));
	} catch {
		return !1;
	}
}, ib = (e) => {
	if (/[\u0000-\u001f\u007f]/.test(e)) return !1;
	if (/^data:image\/(png|jpeg|gif|webp);base64,[A-Za-z0-9+/]+={0,2}$/.test(e) || e.startsWith("/") && !e.startsWith("//") && !e.includes("\\")) return !0;
	try {
		let t = new URL(e);
		return ["http:", "https:"].includes(t.protocol) && !t.username && !t.password;
	} catch {
		return !1;
	}
};
function ab(e, t) {
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
var ob = () => [
	__.configure({
		link: {
			openOnClick: !1,
			autolink: !1,
			linkOnPaste: !1
		},
		trailingNode: !1
	}),
	D_.configure({ types: ["heading", "paragraph"] }),
	Ry,
	Uy,
	Iy.configure({
		resizable: !0,
		allowTableNodeSelection: !0
	}),
	xy,
	by,
	yy
];
function sb(e) {
	let t = document.createElement("template");
	t.innerHTML = e;
	for (let e of t.content.querySelectorAll("*")) {
		if (!eb.has(e.tagName)) throw Error("This page contains unsupported elements. Its saved source is unchanged.");
		let t = e.localName, n = [...e.children], r = $y.inlineTags;
		if (([
			"p",
			"h1",
			"h2",
			"h3",
			"h4",
			"h5",
			"h6"
		].includes(t) || r.includes(t)) && n.some((e) => !r.includes(e.localName)) || ["ul", "ol"].includes(t) && (n.some((e) => e.localName !== "li") || [...e.childNodes].some((e) => e.nodeType === Node.TEXT_NODE && e.textContent?.trim())) || t === "li" && !["ul", "ol"].includes(e.parentElement?.localName || "") || t === "pre" && (n.length !== 1 || n[0].localName !== "code" || [...e.childNodes].some((e) => e.nodeType === Node.TEXT_NODE && e.textContent)) || t === "code" && (n.length > 0 || r.includes(e.parentElement?.localName || ""))) throw Error("This page has unsupported content structure. Its saved source is unchanged.");
		if (t === "code" && e.attributes.length && e.parentElement?.localName !== "pre") throw Error("Unsupported inline code attributes.");
		if (t === "img" && !ib(e.getAttribute("src") || "")) throw Error("Unsupported image address.");
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
		for (let n of e.attributes) if (!(($y.attributes[e.localName] || []).includes(n.name) && (e.tagName !== "OL" || /^-?\d+$/.test(n.value) && Number.isSafeInteger(Number(n.value)) && Number(n.value) >= -2147483648 && Number(n.value) <= 2147483647) && (e.tagName !== "CODE" || /^language-[\w-]+$/.test(n.value)) && (!["src", "data-asset-url"].includes(n.name) || ib(n.value)) && (n.name !== "style" || ab(t, n.value)) && (n.name !== "data-indent-level" || /^[0-8]$/.test(n.value)) && (![
			"colspan",
			"rowspan",
			"width"
		].includes(n.name) || /^\d+$/.test(n.value) && Number(n.value) > 0 && Number(n.value) <= 1e4) && (n.name !== "colwidth" || n.value.split(",").every((e) => /^\d+$/.test(e) && Number(e) <= 1e4)) && (n.name !== "align" || [
			"left",
			"center",
			"right",
			"justify"
		].includes(n.value)))) throw Error("This page contains formatting this editor cannot preserve yet. Its saved source is unchanged.");
		if (e.tagName === "A" && e.hasAttribute("href") && !rb(e.getAttribute("href"))) throw Error("This page contains an unsupported link. Its saved source is unchanged.");
	}
	return e;
}
function cb(e) {
	if (!e || !tb.has(e.type)) throw Error("This legacy document contains unsupported content.");
	if (Object.keys(e).some((e) => ![
		"type",
		"attrs",
		"content",
		"marks",
		"text"
	].includes(e))) throw Error("Unsupported legacy metadata.");
	let t = $y.nodeAttributes[e.type] || [];
	if (Object.keys(e.attrs || {}).some((e) => !t.includes(e))) throw Error("Unsupported legacy attributes.");
	if (e.type === "heading" && !$y.headingLevels.includes(e.attrs?.level)) throw Error("Unsupported heading level.");
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
		if (typeof e.attrs?.src != "string" || !ib(e.attrs.src)) throw Error("Unsupported image.");
		if (e.attrs.assetUrl != null && !ib(e.attrs.assetUrl)) throw Error("Unsupported image asset URL.");
		if (e.attrs.width != null && (!/^\d+$/.test(String(e.attrs.width)) || Number(e.attrs.width) < 1 || Number(e.attrs.width) > 1e4)) throw Error("Unsupported image width.");
	}
	for (let t of e.marks || []) {
		if (!nb.has(t.type)) throw Error("Unsupported legacy formatting.");
		if (Object.keys(t).some((e) => !["type", "attrs"].includes(e))) throw Error("Unsupported legacy mark metadata.");
		if (Object.keys(t.attrs || {}).some((e) => t.type !== "link" || !$y.attributes.a.includes(e))) throw Error("Unsupported legacy formatting attributes.");
		if (t.type === "link" && !rb(t.attrs?.href || "")) throw Error("Unsupported legacy link.");
	}
	for (let t of e.content || []) cb(t);
}
function lb(e, t) {
	if (t === "Html") {
		let t = document.createElement("div");
		t.innerHTML = sb(e);
		let n = Me.fromSchema(Ml(ob())).parse(t, { preserveWhitespace: "full" });
		return n.check(), n.toJSON();
	}
	if (t === "LegacyJson") {
		let t = JSON.parse(e);
		if (t.type !== "doc") throw Error("The legacy JSON is not a TipTap document.");
		return cb(t), Ml(ob()).nodeFromJSON(t).check(), t;
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
function ub(e, t, n, r, i = !1) {
	let a = lb(t, n), o = document.createElement("div");
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
	let _ = 0, v = !1, y = null, b = {
		from: 1,
		to: 1
	}, x = {
		from: 1,
		to: 1
	}, S = 0, ee = [], te = (e, ...t) => {
		v || r.invokeMethodAsync(e, ...t).catch(() => {
			v || (u.textContent = "The editor could not notify the app. Use Save before leaving this page.");
		});
	}, ne = () => {
		C.isEditable && (d.hidden = !0, b = {
			from: C.state.selection.from,
			to: C.state.selection.to
		}, l.value = C.getAttributes("link").href || "", c.hidden = !1, l.focus());
	}, re = (e) => {
		C.isEditable && (x = {
			from: C.state.selection.from,
			to: C.state.selection.to
		}, S = _, c.hidden = !0, e ? (d.hidden = !0, g.value = "", g.click()) : (f.value = "", p.value = "", d.hidden = !1, f.focus()));
	};
	function ie(e, t = "", n = C.state.selection) {
		return v || !C.isEditable ? !1 : (e = e.trim(), ib(e) ? C.getHTML().length + e.length + t.length * 6 + 200 > 5e5 ? (u.textContent = "This image would exceed the page size limit. Choose a smaller image or use an image URL.", !1) : (C.chain().focus().setTextSelection({
			from: n.from,
			to: n.to
		}).setImage({
			src: e,
			alt: t
		}).run(), d.hidden = !0, u.textContent = "", !0) : (u.textContent = "Use an http or https image address, or choose a PNG, JPEG, GIF or WebP file.", !1));
	}
	d.addEventListener("submit", (e) => {
		if (e.preventDefault(), _ !== S) {
			u.textContent = "The writing changed. Open Image URL again to choose where to insert the image.";
			return;
		}
		ie(f.value, p.value, x);
	});
	let ae = () => {
		d.hidden = !0, C.commands.focus();
	};
	h.addEventListener("click", ae), d.addEventListener("keydown", (e) => {
		e.key === "Escape" && (e.preventDefault(), ae());
	}), g.addEventListener("change", async () => {
		let e = g.files?.[0];
		if (!e || v) return;
		let t = { ...x }, n = S;
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
			if (v) return;
			if (_ !== n) throw Error("The writing changed. Choose the image again to set its insertion point.");
			let o = "";
			for (let e of r) o += String.fromCharCode(e);
			ie(`data:${a};base64,${btoa(o)}`, e.name, t);
		} catch (e) {
			v || (u.textContent = e instanceof Error ? e.message : "The image could not be opened.");
		}
	});
	let oe = W.create({
		name: "deviceShortcuts",
		addKeyboardShortcuts() {
			return {
				"Mod-s": () => (te("OnSaveRequested"), !0),
				"Mod-k": () => (ne(), !0)
			};
		}
	}), se = !1, C = new Ud({
		element: s,
		extensions: [
			...ob(),
			oe,
			C_((e) => te("OnAnnotationClicked", e))
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
					sb(n);
				} catch {
					return t.preventDefault(), e.dispatch(e.state.tr.insertText(t.clipboardData?.getData("text/plain") || "")), u.textContent = "Pasted as plain text because the source has unsupported formatting.", !0;
				}
				return !1;
			},
			handleDrop: () => !0
		},
		onUpdate: () => {
			y = null, _++, te("OnContentChanged", C.getHTML(), _);
		},
		onSelectionUpdate: () => {
			let e = C.state.selection;
			e.empty || (y = {
				from: e.from,
				to: e.to
			});
		},
		onTransaction: () => {
			i && se && ce();
			for (let e of ee) e.active && e.button.setAttribute("aria-pressed", String(e.active())), e.button.disabled = !C.isEditable || (e.enabled ? !e.enabled() : !1);
		}
	});
	function ce() {
		let e = C.isEditable, t = C.isActive("heading") ? C.getAttributes("heading").level : null;
		te("OnFormattingChanged", {
			isBold: C.isActive("bold"),
			isItalic: C.isActive("italic"),
			isLink: C.isActive("link"),
			isStrike: C.isActive("strike"),
			isCode: C.isActive("code"),
			canBold: e && C.can().toggleBold(),
			canItalic: e && C.can().toggleItalic(),
			canStrike: e && C.can().toggleStrike(),
			canCode: e && C.can().toggleCode(),
			canApplyHeading: e && !C.isActive("codeBlock"),
			canToggleList: e,
			canBlockquote: e,
			canHorizontalRule: e && C.can().setHorizontalRule(),
			canInsertTable: e && C.can().insertTable({
				rows: 3,
				cols: 3,
				withHeaderRow: !0
			}),
			canInsertImage: e && !C.isActive("codeBlock"),
			isInTable: C.isActive("table"),
			isHeaderCell: C.isActive("tableHeader"),
			canAddTableRowBefore: e && C.can().addRowBefore(),
			canAddTableRowAfter: e && C.can().addRowAfter(),
			canDeleteTableRow: e && C.can().deleteRow(),
			canAddTableColumnBefore: e && C.can().addColumnBefore(),
			canAddTableColumnAfter: e && C.can().addColumnAfter(),
			canDeleteTableColumn: e && C.can().deleteColumn(),
			canToggleTableHeaderRow: e && C.can().toggleHeaderRow(),
			canToggleTableHeaderColumn: e && C.can().toggleHeaderColumn(),
			canMergeTableCells: e && C.can().mergeCells(),
			canSplitTableCell: e && C.can().splitCell(),
			canDeleteTable: e && C.can().deleteTable(),
			canAlign: e && C.can().setTextAlign("left"),
			canIncreaseIndent: e && C.can().increaseIndent(),
			canDecreaseIndent: e && C.can().decreaseIndent(),
			textAlign: C.getAttributes(t ? "heading" : "paragraph").textAlign || "left",
			blockType: t ? `heading:${t}` : "paragraph"
		}, e && C.can().undo(), e && C.can().redo());
	}
	se = !0, i && ce();
	function w(e, t, n, r) {
		let i = document.createElement("button");
		i.type = "button", i.textContent = e, i.addEventListener("mousedown", (e) => e.preventDefault()), i.addEventListener("click", () => {
			C.isEditable && t();
		}), n && i.setAttribute("aria-pressed", String(n())), r && (i.disabled = !r()), o.append(i), ee.push({
			button: i,
			active: n,
			enabled: r
		});
	}
	w("Paragraph", () => Ky(C), () => C.isActive("paragraph"));
	for (let e of $y.headingLevels) w(`Heading ${e}`, () => qy(C, e), () => C.isActive("heading", { level: e }));
	w("Bold", () => Wy(C), () => C.isActive("bold")), w("Italic", () => Gy(C), () => C.isActive("italic")), w("Bullets", () => Yy(C), () => C.isActive("bulletList")), w("Numbered list", () => Xy(C), () => C.isActive("orderedList")), w("Quote", () => Jy(C), () => C.isActive("blockquote")), w("Link", ne, () => C.isActive("link")), w("Undo", () => Zy(C), void 0, () => C.can().undo()), w("Redo", () => Qy(C), void 0, () => C.can().redo()), c.append(l);
	let le = (e, t) => {
		let n = document.createElement("button");
		n.type = "button", n.textContent = e, n.addEventListener("click", t), c.append(n);
	}, ue = () => {
		if (!rb(l.value)) {
			u.textContent = "Use an http, https or mailto address.";
			return;
		}
		C.chain().focus().setTextSelection(b).extendMarkRange("link").setLink({ href: l.value.trim() }).run(), c.hidden = !0, u.textContent = "";
	};
	return le("Apply link", ue), le("Remove link", () => {
		C.chain().focus().setTextSelection(b).extendMarkRange("link").unsetLink().run(), c.hidden = !0;
	}), le("Cancel", () => {
		c.hidden = !0, C.commands.focus();
	}), l.addEventListener("keydown", (e) => {
		e.key === "Enter" && (e.preventDefault(), ue()), e.key === "Escape" && (c.hidden = !0, C.commands.focus());
	}), {
		setAnnotations: (e) => w_(C, e),
		navigateToAnnotation: (e) => T_(C, e),
		selectedText: () => E_(C),
		navigateToText(e) {
			if (!e || e.length > 1e3) return !1;
			let t = "", n = [], r = null;
			C.state.doc.descendants((e, i, a) => {
				if (e.isText && e.text) {
					r && a !== r && (t += "\n", n.push(i));
					for (let t = 0; t < e.text.length; t++) n.push(i + t);
					t += e.text, r = a;
				} else e.type.name === "hardBreak" && (t += "\n", n.push(i));
			});
			let i = t.indexOf(e);
			return i < 0 ? !1 : (C.chain().setTextSelection({
				from: n[i],
				to: n[i + e.length - 1] + 1
			}).scrollIntoView().focus().run(), !0);
		},
		command(e, t) {
			if (C.isEditable) switch (e) {
				case "bold":
					Wy(C);
					break;
				case "italic":
					Gy(C);
					break;
				case "bulletList":
					Yy(C);
					break;
				case "orderedList":
					Xy(C);
					break;
				case "blockquote":
					Jy(C);
					break;
				case "paragraph":
					Ky(C);
					break;
				case "heading":
					t && $y.headingLevels.includes(t) && qy(C, t);
					break;
				case "link":
					ne();
					break;
				case "undo":
					Zy(C);
					break;
				case "redo":
					Qy(C);
					break;
				case "strike":
					C.chain().focus().toggleStrike().run();
					break;
				case "code":
					C.chain().focus().toggleCode().run();
					break;
				case "horizontalRule":
					C.chain().focus().setHorizontalRule().run();
					break;
				case "table":
					C.chain().focus().insertTable({
						rows: 3,
						cols: 3,
						withHeaderRow: !0
					}).run();
					break;
				case "image":
					re(!0);
					break;
				case "imageUrl":
					re(!1);
					break;
				case "align:left":
				case "align:center":
				case "align:right":
					C.chain().focus().setTextAlign(e.slice(6)).run();
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
					C.chain().focus()[e]().run();
					break;
				default: throw Error("Unsupported editor command.");
			}
		},
		insertImage: ie,
		setEditable(t) {
			C.setEditable(t, !1), i && ce(), e.querySelectorAll("button,input").forEach((e) => {
				e.disabled = !t;
			});
			for (let e of ee) e.button.disabled = !t || (e.enabled ? !e.enabled() : !1);
		},
		snapshot: () => ({
			html: C.getHTML(),
			version: _
		}),
		captureAi(e = !1, t = !1) {
			let n = C.state.doc, r = C.state.selection.empty ? t ? null : y : C.state.selection, i = e ? 0 : r?.from ?? C.state.selection.from, a = e ? n.content.size : r?.to ?? C.state.selection.to, o = (e, t) => n.textBetween(e, t, "\n", "\n"), s = o(i, a), c = o(0, i).length;
			return {
				html: C.getHTML(),
				plainText: o(0, n.content.size),
				selectedText: s,
				selectionStart: c,
				selectionEnd: c + s.length,
				from: i,
				to: a,
				version: _
			};
		},
		applyAi(e, t, n, r, i, a) {
			if (!C.isEditable || C.getHTML() !== e) throw Error("The writing changed after the preview. Run the action again.");
			if (!i.trim()) throw Error("The proposed text is empty.");
			let o = C.state.doc;
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
			if (!C.commands.insertContentAt(c, s, {
				updateSelection: !0,
				errorOnInvalidContent: !0
			})) throw Error("The proposal could not be applied. Your writing is unchanged.");
			return y = null, {
				html: C.getHTML(),
				version: _
			};
		},
		restoreAi(e, t) {
			if (!C.isEditable || C.getHTML() !== e) throw Error("The writing changed after AI was applied. Restore the original as a separate copy instead.");
			return sb(t), C.commands.setContent(lb(t, "Html"), {
				emitUpdate: !0,
				errorOnInvalidContent: !0
			}), {
				html: C.getHTML(),
				version: _
			};
		},
		setContent(e) {
			if (sb(e), e === C.getHTML()) return;
			let t = C.state.selection;
			C.commands.setContent(lb(e, "Html"), {
				emitUpdate: !1,
				errorOnInvalidContent: !0
			});
			let n = C.state.doc.content.size;
			C.commands.setTextSelection({
				from: Math.min(t.from, n),
				to: Math.min(t.to, n)
			});
		},
		destroy() {
			v = !0, C.destroy(), e.replaceChildren();
		}
	};
}
//#endregion
export { ub as create };
