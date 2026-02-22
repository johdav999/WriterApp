import { Editor, Extension } from "https://esm.sh/@tiptap/core@2.1.13?bundle";
import { Plugin, PluginKey } from "https://esm.sh/prosemirror-state@1.4.3?bundle";
import { Decoration, DecorationSet } from "https://esm.sh/prosemirror-view@1.33.6?bundle";
import StarterKit from "https://esm.sh/@tiptap/starter-kit@2.1.13?bundle";
import TextStyle from "https://esm.sh/@tiptap/extension-text-style@2.1.13?bundle";
import TextAlign from "https://esm.sh/@tiptap/extension-text-align@2.1.13?bundle";
import Link from "https://esm.sh/@tiptap/extension-link@2.1.13?bundle";
import {
    toggleBold,
    toggleItalic,
    toggleStrike,
    toggleCode,
    setParagraph,
    toggleHeading,
    toggleBlockquote,
    insertHorizontalRule,
    toggleBulletList,
    toggleOrderedList
} from "/js/tiptap-commands.js";

const TextStyleWithFontSize = TextStyle.extend({
    addAttributes() {
        return {
            fontSize: {
                default: null,
                parseHTML: element => element.style.fontSize || null,
                renderHTML: attributes => {
                    if (!attributes.fontSize) {
                        return {};
                    }

                    return { style: `font-size: ${attributes.fontSize}` };
                }
            },
            fontFamily: {
                default: null,
                parseHTML: element => element.style.fontFamily || null,
                renderHTML: attributes => {
                    if (!attributes.fontFamily) {
                        return {};
                    }

                    return { style: `font-family: ${attributes.fontFamily}` };
                }
            }
        };
    }
});

const indentUnitEm = 2;
// Left indent only; right indent omitted to keep stored HTML predictable.
const indentMaxLevel = 8;

function parseIndentLevel(element) {
    if (!element) {
        return 0;
    }

    const dataValue = element.getAttribute?.("data-indent-level");
    if (dataValue) {
        const parsed = Number.parseInt(dataValue, 10);
        if (Number.isFinite(parsed)) {
            return Math.max(0, Math.min(indentMaxLevel, parsed));
        }
    }

    const styleValue = element.style?.marginLeft;
    if (!styleValue) {
        return 0;
    }

    const match = String(styleValue).match(/([\d.]+)/);
    if (!match) {
        return 0;
    }

    const parsed = Number.parseFloat(match[1]);
    if (!Number.isFinite(parsed)) {
        return 0;
    }

    const level = Math.round(parsed / indentUnitEm);
    return Math.max(0, Math.min(indentMaxLevel, level));
}

function clampIndentLevel(level) {
    if (!Number.isFinite(level)) {
        return 0;
    }

    return Math.max(0, Math.min(indentMaxLevel, Math.round(level)));
}

const IndentExtension = Extension.create({
    name: "indent",
    addOptions() {
        return {
            types: ["paragraph", "heading"]
        };
    },
    addGlobalAttributes() {
        return [
            {
                types: this.options.types,
                attributes: {
                    indentLevel: {
                        default: 0,
                        parseHTML: element => parseIndentLevel(element),
                        renderHTML: attributes => {
                            const level = clampIndentLevel(attributes.indentLevel);
                            if (!level) {
                                return {};
                            }

                            return {
                                "data-indent-level": String(level),
                                style: `margin-left: ${level * indentUnitEm}em;`
                            };
                        }
                    }
                }
            }
        ];
    },
    addCommands() {
        const updateIndent = (delta) => ({ state, tr, dispatch }) => {
            const { from, to, empty, $from } = state.selection;
            const types = new Set(this.options.types ?? []);
            let modified = false;

            const applyIndent = (node, pos) => {
                if (!node || !node.isTextblock || !types.has(node.type.name)) {
                    return;
                }

                const current = clampIndentLevel(node.attrs?.indentLevel ?? 0);
                const next = clampIndentLevel(current + delta);
                if (next === current) {
                    return;
                }

                tr.setNodeMarkup(pos, undefined, { ...node.attrs, indentLevel: next });
                modified = true;
            };

            if (empty && $from) {
                const parent = $from.parent;
                const pos = $from.before($from.depth);
                applyIndent(parent, pos);
            } else {
                const seen = new Set();
                state.doc.nodesBetween(from, to, (node, pos) => {
                    if (!node.isTextblock || !types.has(node.type.name)) {
                        return;
                    }

                    if (seen.has(pos)) {
                        return;
                    }

                    seen.add(pos);
                    applyIndent(node, pos);
                });
            }

            if (modified && dispatch) {
                dispatch(tr);
            }

            return modified;
        };

        return {
            increaseIndent: () => updateIndent(1),
            decreaseIndent: () => updateIndent(-1)
        };
    }
});

const aiDecorationsKey = new PluginKey("aiDecorations");
const qualityIssueDecorationsKey = new PluginKey("qualityIssueDecorations");
const pageGapDecorationsKey = new PluginKey("pageGapDecorations");

const AiDecorationsExtension = Extension.create({
    name: "aiDecorations",
    addProseMirrorPlugins() {
        return [
            new Plugin({
                key: aiDecorationsKey,
                state: {
                    init: () => DecorationSet.empty,
                    apply: (tr, value) => {
                        const next = tr.getMeta(aiDecorationsKey);
                        if (next) {
                            return next;
                        }

                        return value.map(tr.mapping, tr.doc);
                    }
                },
                props: {
                    decorations(state) {
                        return aiDecorationsKey.getState(state);
                    }
                }
            })
        ];
    }
});

const QualityIssueDecorationsExtension = Extension.create({
    name: "qualityIssueDecorations",
    addProseMirrorPlugins() {
        return [
            new Plugin({
                key: qualityIssueDecorationsKey,
                state: {
                    init: () => DecorationSet.empty,
                    apply: (tr, value) => {
                        const next = tr.getMeta(qualityIssueDecorationsKey);
                        if (next) {
                            return next;
                        }

                        return value.map(tr.mapping, tr.doc);
                    }
                },
                props: {
                    decorations(state) {
                        return qualityIssueDecorationsKey.getState(state);
                    }
                }
            })
        ];
    }
});

const PageGapDecorationsExtension = Extension.create({
    name: "pageGapDecorations",
    addProseMirrorPlugins() {
        return [
            new Plugin({
                key: pageGapDecorationsKey,
                state: {
                    init: () => DecorationSet.empty,
                    apply: (tr, value) => {
                        const next = tr.getMeta(pageGapDecorationsKey);
                        if (next) {
                            return next;
                        }

                        return value.map(tr.mapping, tr.doc);
                    }
                },
                props: {
                    decorations(state) {
                        return pageGapDecorationsKey.getState(state);
                    }
                }
            })
        ];
    }
});

function createInteropState(dotNetRef) {
    return { enabled: !!dotNetRef };
}

function safeInvoke(dotNetRef, interopState, method, ...args) {
    if (!dotNetRef || !interopState || !interopState.enabled) {
        return;
    }

    try {
        const result = dotNetRef.invokeMethodAsync(method, ...args);
        if (result && typeof result.catch === "function") {
            result.catch(() => {
                interopState.enabled = false;
            });
        }
    } catch (error) {
        interopState.enabled = false;
    }
}

function selectionHasNodeType(editor, nodeType) {
    const { from, to, empty } = editor.state.selection;
    if (empty) {
        return editor.isActive(nodeType);
    }

    let found = false;
    editor.state.doc.nodesBetween(from, to, node => {
        if (node.type?.name === nodeType) {
            found = true;
            return false;
        }
    });

    return found;
}

function getTextStyleAttrFromMarks(marks, attrName) {
    if (!marks) {
        return null;
    }

    const mark = marks.find(entry => entry.type?.name === "textStyle");
    if (!mark) {
        return null;
    }

    return mark.attrs ? mark.attrs[attrName] ?? null : null;
}

function getUniformTextStyleAttr(editor, attrName) {
    const { from, to, empty } = editor.state.selection;
    if (empty) {
        const attrs = editor.getAttributes("textStyle") ?? {};
        return { mixed: false, value: attrs[attrName] ?? null };
    }

    let hasValue = false;
    let currentValue = null;
    let mixed = false;

    editor.state.doc.nodesBetween(from, to, node => {
        if (!node.isText) {
            return;
        }

        const value = getTextStyleAttrFromMarks(node.marks, attrName);
        if (!hasValue) {
            currentValue = value;
            hasValue = true;
            return;
        }

        if (currentValue !== value) {
            mixed = true;
            return false;
        }
    });

    if (!hasValue) {
        currentValue = null;
    }

    return { mixed, value: currentValue };
}

function getUniformBlockAttr(editor, attrName, types) {
    const { from, to, empty } = editor.state.selection;
    const typeSet = new Set(types);

    if (empty) {
        for (let index = 0; index < types.length; index += 1) {
            const type = types[index];
            if (editor.isActive(type)) {
                const attrs = editor.getAttributes(type) ?? {};
                return { mixed: false, value: attrs[attrName] ?? null };
            }
        }

        return { mixed: false, value: null };
    }

    let hasValue = false;
    let currentValue = null;
    let mixed = false;

    editor.state.doc.nodesBetween(from, to, node => {
        if (!node.isTextblock || !typeSet.has(node.type.name)) {
            return;
        }

        const value = node.attrs ? node.attrs[attrName] ?? null : null;
        if (!hasValue) {
            currentValue = value;
            hasValue = true;
            return;
        }

        if (currentValue !== value) {
            mixed = true;
            return false;
        }
    });

    if (!hasValue) {
        currentValue = null;
    }

    return { mixed, value: currentValue };
}

function normalizeFontSize(value) {
    if (value === null || value === undefined) {
        return "";
    }

    const match = String(value).match(/(\d+(\.\d+)?)/);
    if (!match) {
        return "";
    }

    const parsed = Number(match[1]);
    return Number.isFinite(parsed) ? String(parsed) : "";
}

function buildOutline(editor) {
    const outline = [];
    editor.state.doc.descendants((node, pos) => {
        if (node.type?.name !== "heading") {
            return;
        }

        outline.push({
            text: node.textContent || "",
            level: node.attrs?.level ?? 1,
            position: pos + 1
        });
    });

    return outline;
}

function resolvePageBreakOptions(options) {
    return {
        pageHeightPx: Number(options?.pageHeightPx) || 980,
        showHorizontalRule: options?.showHorizontalRule !== false,
        gutterOffsetPx: Number(options?.gutterOffsetPx) || 28,
        pageGapPx: Number(options?.pageGapPx) || 32,
        layoutMode: options?.layoutMode || "simple",
        debug: options?.debug === true
    };
}

function getPageBreakContext(editor) {
    const view = editor?.view?.dom;
    if (!view) {
        return null;
    }

    const viewport = view.closest(".editor-viewport");
    if (!viewport) {
        return null;
    }

    const lane = view.closest(".page-lane");
    const content = view.closest(".editor-content") || view;
    const canvas = view.closest(".editor-canvas") || content || view;
    const overlayHost = lane || canvas || content || viewport;
    return { view, viewport, content, overlayHost, lane };
}

function findScrollContainer(element) {
    let current = element;
    while (current && current !== document.body) {
        const style = window.getComputedStyle(current);
        const overflowY = style?.overflowY || "";
        if ((overflowY === "auto" || overflowY === "scroll") && current.scrollHeight > current.clientHeight) {
            return current;
        }
        current = current.parentElement;
    }

    return window;
}

function ensurePageBreakOverlay(overlayHost) {
    if (!overlayHost) {
        return null;
    }

    let overlay = overlayHost.querySelector(".pagebreak-overlay");
    if (!overlay) {
        overlay = document.createElement("div");
        overlay.className = "pagebreak-overlay";
        overlayHost.appendChild(overlay);
    }

    return overlay;
}

function computePageBreaks(editor, options) {
    const ctx = getPageBreakContext(editor);
    if (!ctx) {
        return { count: 1, breaks: [], options: resolvePageBreakOptions(options), ctx: null };
    }

    const opts = resolvePageBreakOptions(options);
    const gapCount = editor?.__pageGapState?.gapCount ?? 0;
    const rawHeight = ctx.view.scrollHeight || 0;
    const contentHeight = Math.max(0, rawHeight - gapCount * opts.pageGapPx);
    const laneHeight = ctx.lane?.scrollHeight || 0;
    let measuredHeight = laneHeight || rawHeight;
    let count = Math.max(1, Math.ceil(measuredHeight / opts.pageHeightPx));

    if ((contentHeight > 0 && measuredHeight > contentHeight * 10) || count > 500) {
        console.warn("[pagination] Suspicious lane height or page count. Recomputing from content height.", {
            laneHeight: measuredHeight,
            contentHeight,
            rawHeight,
            pageCount: count
        });
        measuredHeight = contentHeight;
        count = Math.max(1, Math.ceil(measuredHeight / opts.pageHeightPx));
    }

    const viewportRect = ctx.viewport.getBoundingClientRect();
    const viewRect = ctx.view.getBoundingClientRect();
    const contentRect = ctx.content.getBoundingClientRect();
    const baseTop = viewRect.top - contentRect.top;
    const leftOffset = 0;
    const width = ctx.content.clientWidth || contentRect.width;
    const blockHeight = opts.pageHeightPx + opts.pageGapPx;

    const breaks = [];
    for (let pageIndex = 1; pageIndex <= count; pageIndex += 1) {
        const topPx = baseTop + (pageIndex - 1) * blockHeight;
        breaks.push({ pageIndex, topPx });
    }

    return { count, breaks, leftOffset, width, options: opts, ctx, contentHeight, laneHeight, rawHeight };
}

function renderPageBreakOverlay(editor, options) {
    const info = computePageBreaks(editor, options);
    const ctx = info.ctx;
    if (!ctx) {
        return info.count;
    }

    const overlay = ensurePageBreakOverlay(ctx.overlayHost);
    if (!overlay) {
        return info.count;
    }

    overlay.innerHTML = "";
    overlay.style.height = `${info.rawHeight || 0}px`;
    overlay.style.width = `${ctx.content.clientWidth || ctx.content.getBoundingClientRect().width}px`;

    const mode = info.options.layoutMode || "simple";
    const sheetHeight = Math.max(0, info.options.pageHeightPx);

    if (mode === "print") {
        info.breaks.forEach(entry => {
            const sheet = document.createElement("div");
            sheet.className = "pagebreak-sheet";
            sheet.style.top = `${entry.topPx}px`;
            sheet.style.left = `${info.leftOffset}px`;
            sheet.style.width = `${info.width}px`;
            sheet.style.height = `${sheetHeight}px`;
            overlay.appendChild(sheet);
        });
    } else {
        info.breaks.forEach(entry => {
            if (info.options.showHorizontalRule && entry.pageIndex > 1) {
                const line = document.createElement("div");
                line.className = "pagebreak-line";
                line.style.top = `${entry.topPx}px`;
                line.style.left = `${info.leftOffset}px`;
                line.style.width = `${info.width}px`;
                overlay.appendChild(line);
            }
        });
    }

    return info.count;
}

function maybeDebugEditorLayout(editor) {
    if (typeof window === "undefined" || !window.__wa_editor_debug || editor?.__waDebugLayoutLogged) {
        return;
    }

    const ctx = getPageBreakContext(editor);
    if (!ctx) {
        return;
    }

    editor.__waDebugLayoutLogged = true;

    const host = ctx.view.closest("[id^='section-editor-']");
    const shell = ctx.view.closest(".editor-shell");
    const main = ctx.view.closest(".editor-main");
    const surface = ctx.view.closest(".editor-surface");
    const statusBar = ctx.view.closest(".editor-surface")?.querySelector?.(".editor-status-bar");
    const overlay = ctx.overlayHost?.querySelector?.(".pagebreak-overlay");

    const targets = [
        { label: "shell", el: shell, color: "#2563eb" },
        { label: "main", el: main, color: "#0ea5e9" },
        { label: "surface", el: surface, color: "#16a34a" },
        { label: "viewport", el: ctx.viewport, color: "#14b8a6" },
        { label: "content", el: ctx.content, color: "#8b5cf6" },
        { label: "canvas", el: ctx.view.closest(".editor-canvas"), color: "#ec4899" },
        { label: "lane", el: ctx.lane, color: "#f97316" },
        { label: "overlay", el: overlay, color: "#ef4444" },
        { label: "section-host", el: host, color: "#f59e0b" },
        { label: "prosemirror", el: ctx.view, color: "#22c55e" },
        { label: "status-bar", el: statusBar, color: "#64748b" }
    ];

    targets.forEach(target => {
        if (!target.el) {
            return;
        }
        target.el.style.outline = `2px dashed ${target.color}`;
        target.el.style.outlineOffset = "-2px";
    });

    const diagnostics = targets
        .filter(target => target.el)
        .map(target => {
            const style = window.getComputedStyle(target.el);
            return {
                label: target.label,
                zIndex: style.zIndex,
                overflow: `${style.overflowX}/${style.overflowY}`,
                position: style.position,
                size: `${target.el.clientWidth}x${target.el.clientHeight}`
            };
        });

    console.info("[editor-debug] layout diagnostics", diagnostics);
}

function buildPageGapDecorations(editor, options) {
    const view = editor?.view;
    const ctx = getPageBreakContext(editor);
    if (!view || !ctx) {
        return { decorations: DecorationSet.empty, gapCount: 0, pageCount: 1 };
    }

    const opts = resolvePageBreakOptions(options);
    if (opts.layoutMode !== "print" || opts.pageGapPx <= 0) {
        return { decorations: DecorationSet.empty, gapCount: 0, pageCount: 1 };
    }

    const gapPx = Math.max(0, opts.pageGapPx);
    const previousGapCount = editor?.__pageGapState?.gapCount ?? 0;
    const rawHeight = ctx.view.scrollHeight || 0;
    const contentHeight = Math.max(0, rawHeight - previousGapCount * gapPx);
    const pageCount = Math.max(1, Math.ceil(contentHeight / opts.pageHeightPx));
    const blockHeight = opts.pageHeightPx + gapPx;

    const viewportRect = ctx.viewport.getBoundingClientRect();
    const viewRect = ctx.view.getBoundingClientRect();
    const contentRect = ctx.content.getBoundingClientRect();
    const baseTop = viewRect.top - contentRect.top;

    const decorations = [];
    let lastPos = -1;
    const probeX = Math.round(contentRect.left + Math.max(8, Math.min(24, contentRect.width / 2)));
    for (let pageIndex = 2; pageIndex <= pageCount; pageIndex += 1) {
        const topPx = baseTop + (pageIndex - 1) * blockHeight;
        const probeY = Math.round(contentRect.top + topPx + 2);
        const posAt = view.posAtCoords({ left: probeX, top: probeY });
        if (!posAt || typeof posAt.pos !== "number") {
            continue;
        }

        let insertPos = posAt.pos;
        try {
            const resolved = view.state.doc.resolve(insertPos);
            if (resolved.depth > 0) {
                insertPos = resolved.before(resolved.depth);
            }
        } catch (error) {
            continue;
        }

        if (insertPos <= 0 || insertPos === lastPos) {
            continue;
        }

        const gap = document.createElement("div");
        gap.className = "wa-page-gap";
        gap.setAttribute("aria-hidden", "true");
        gap.setAttribute("contenteditable", "false");
        gap.style.height = `${gapPx}px`;
        const deco = Decoration.widget(insertPos, gap, {
            key: `wa-gap-${pageIndex}`,
            side: -1,
            stopEvent: () => true,
            ignoreSelection: true
        });
        decorations.push(deco);
        lastPos = insertPos;
    }

    return {
        decorations: DecorationSet.create(view.state.doc, decorations),
        gapCount: Math.max(0, pageCount - 1),
        pageCount
    };
}

function updatePageGapDecorations(editor) {
    if (!editor?.view) {
        return;
    }

    const info = buildPageGapDecorations(editor, editor.__pageBreakState?.options);
    editor.__pageGapState = {
        gapCount: info.gapCount,
        pageCount: info.pageCount
    };

    const tr = editor.view.state.tr.setMeta(pageGapDecorationsKey, info.decorations);
    editor.view.dispatch(tr);
}

function getCurrentPageIndex(info) {
    if (!info || !info.ctx) {
        return 1;
    }

    const viewportRect = info.ctx.viewport.getBoundingClientRect();
    const centerLine = viewportRect.height / 2;
    let current = 1;

    for (let index = 0; index < info.breaks.length; index += 1) {
        if (info.breaks[index].topPx <= centerLine + 1) {
            current = info.breaks[index].pageIndex;
        }
    }

    return current;
}

function notifyPageBreakStatus(editor) {
    if (!editor || !editor.__pageBreakState) {
        return;
    }

    const info = computePageBreaks(editor, editor.__pageBreakState.options);
    const count = renderPageBreakOverlay(editor, editor.__pageBreakState.options);
    updatePageGapDecorations(editor);
    const current = getCurrentPageIndex(info);

    if (editor.__pageBreakState.options?.debug && info?.ctx) {
        const scrollContainer = editor.__pageBreakState.scrollContainer || window;
        const scrollTop = scrollContainer === window ? window.scrollY : scrollContainer.scrollTop;
        console.debug("[PageLayout]", {
            pageHeightPx: editor.__pageBreakState.options.pageHeightPx,
            pageGapPx: editor.__pageBreakState.options.pageGapPx,
            layoutMode: editor.__pageBreakState.options.layoutMode,
            pageCount: count,
            currentPage: current,
            scrollTop,
            lastBreakOffset: info.breaks[info.breaks.length - 1]?.topPx ?? 0
        });
    }

    if (editor.__pageBreakState.dotNetRef) {
        safeInvoke(editor.__pageBreakState.dotNetRef, editor.__pageBreakState.interopState, "OnPageBreakStatusChanged", count, current);
    }
}

function schedulePageBreakUpdate(editor) {
    if (!editor) {
        return;
    }

    if (!editor.__pageBreakState) {
        editor.__pageBreakState = { enabled: false, options: resolvePageBreakOptions(null) };
    }

    const state = editor.__pageBreakState;
    if (!state.enabled) {
        return;
    }

    if (state.timer) {
        clearTimeout(state.timer);
    }

    state.timer = setTimeout(() => {
        state.timer = null;
        notifyPageBreakStatus(editor);
    }, 120);
}

function getBlockType(editor) {
    const { from, to, empty } = editor.state.selection;
    if (empty) {
        for (let level = 1; level <= 6; level += 1) {
            if (editor.isActive("heading", { level })) {
                return `heading:${level}`;
            }
        }

        if (editor.isActive("paragraph")) {
            return "paragraph";
        }

        return null;
    }

    let currentType = null;
    let mixed = false;

    editor.state.doc.nodesBetween(from, to, node => {
        if (!node.isTextblock) {
            return;
        }

        let nodeType = null;
        if (node.type?.name === "heading") {
            nodeType = `heading:${node.attrs?.level ?? 1}`;
        } else if (node.type?.name === "paragraph") {
            nodeType = "paragraph";
        }

        if (!nodeType) {
            return;
        }

        if (!currentType) {
            currentType = nodeType;
            return;
        }

        if (currentType !== nodeType) {
            mixed = true;
            return false;
        }
    });

    if (mixed) {
        return null;
    }

    return currentType;
}

function buildFormattingState(editor) {
    const fontFamilyResult = getUniformTextStyleAttr(editor, "fontFamily");
    const fontSizeResult = getUniformTextStyleAttr(editor, "fontSize");
    const textAlignResult = getUniformBlockAttr(editor, "textAlign", ["paragraph", "heading"]);
    const isInCodeBlock = selectionHasNodeType(editor, "codeBlock");
    const canBold = editor.can().chain().toggleBold().run();
    const canItalic = editor.can().chain().toggleItalic().run();
    const canStrike = editor.can().chain().toggleStrike().run();
    const canCode = editor.can().chain().toggleCode().run();
    const canApplyHeading = !isInCodeBlock
        && (editor.can().chain().setParagraph().run()
            || editor.can().chain().toggleHeading({ level: 1 }).run());
    const canToggleList = !isInCodeBlock
        && (editor.can().chain().toggleBulletList().run()
            || editor.can().chain().toggleOrderedList().run());
    const canBlockquote = editor.can().chain().toggleBlockquote().run();
    const canHorizontalRule = editor.can().chain().setHorizontalRule().run();

    return {
        isBold: editor.isActive("bold"),
        isItalic: editor.isActive("italic"),
        isStrike: editor.isActive("strike"),
        isCode: editor.isActive("code"),
        canBold,
        canItalic,
        canStrike,
        canCode,
        isInCodeBlock,
        canApplyHeading,
        canToggleList,
        canBlockquote,
        canHorizontalRule,
        isLink: editor.isActive("link"),
        blockType: getBlockType(editor),
        fontFamily: fontFamilyResult.mixed ? null : (fontFamilyResult.value ?? ""),
        fontSize: fontSizeResult.mixed ? null : normalizeFontSize(fontSizeResult.value),
        textAlign: textAlignResult.mixed ? null : (textAlignResult.value ?? "left")
    };
}

function buildPlainTextSegments(doc) {
    const segments = [];
    let plainIndex = 0;
    let lastTextblock = false;

    doc.descendants((node, pos) => {
        if (node.isTextblock) {
            if (lastTextblock && plainIndex > 0) {
                plainIndex += 2;
            }

            lastTextblock = true;
        }

        if (node.isText && node.text) {
            const start = plainIndex;
            const end = plainIndex + node.text.length;
            segments.push({
                start,
                end,
                from: pos,
                to: pos + node.text.length
            });
            plainIndex = end;
        }
    });

    return segments;
}

function getEditorPlainText(editor) {
    if (!editor?.state?.doc) {
        return "";
    }

    const segments = buildPlainTextSegments(editor.state.doc);
    if (segments.length === 0) {
        return "";
    }

    const parts = [];
    let nextOffset = 0;
    segments.forEach(segment => {
        if (segment.start > nextOffset) {
            parts.push("\n\n");
            nextOffset = segment.start;
        }

        const length = Math.max(0, segment.to - segment.from);
        if (length > 0) {
            const text = editor.state.doc.textBetween(segment.from, segment.to, "", "");
            parts.push(text);
            nextOffset = segment.end;
        }
    });

    return parts.join("");
}

function mapPlainOffsetToDoc(segments, offset) {
    for (let index = 0; index < segments.length; index += 1) {
        const segment = segments[index];
        if (offset <= segment.end) {
            const delta = Math.max(0, offset - segment.start);
            return segment.from + delta;
        }
    }

    if (segments.length > 0) {
        return segments[segments.length - 1].to;
    }

    return null;
}

function buildAiDecorations(editor, ranges) {
    if (!ranges || ranges.length === 0) {
        return DecorationSet.empty;
    }

    const segments = buildPlainTextSegments(editor.state.doc);
    const decorations = [];

    ranges.forEach(range => {
        const start = Math.max(0, range.start);
        const end = Math.max(start, range.end);
        const from = mapPlainOffsetToDoc(segments, start);
        const to = mapPlainOffsetToDoc(segments, end);
        if (from === null || to === null || to <= from) {
            return;
        }

        const className = range.isActive ? "ai-edit-range is-active" : "ai-edit-range";
        decorations.push(Decoration.inline(from, to, { class: className }));
    });

    return DecorationSet.create(editor.state.doc, decorations);
}

function normalizePlainRange(from, to) {
    const safeFrom = Math.max(0, Number(from) || 0);
    const safeTo = Math.max(0, Number(to) || safeFrom);
    return {
        from: Math.min(safeFrom, safeTo),
        to: Math.max(safeFrom, safeTo)
    };
}

function mapPlainRangeToDocRange(editor, from, to) {
    const normalized = normalizePlainRange(from, to);
    const segments = buildPlainTextSegments(editor.state.doc);
    const docFrom = mapPlainOffsetToDoc(segments, normalized.from);
    const docTo = mapPlainOffsetToDoc(segments, normalized.to);
    if (docFrom === null || docTo === null || docTo <= docFrom) {
        return null;
    }

    return { from: docFrom, to: docTo };
}

function resolveRangeFromAnchorText(editor, anchorText) {
    const query = typeof anchorText === "string" ? anchorText.trim() : "";
    if (!query) {
        return null;
    }

    const plain = getEditorPlainText(editor);
    if (!plain) {
        return null;
    }

    const index = plain.toLowerCase().indexOf(query.toLowerCase());
    if (index < 0) {
        return null;
    }

    const from = index;
    const to = index + query.length;
    return { from, to };
}

function ensureQualityIssueState(editor) {
    if (!editor.__qualityIssueState) {
        editor.__qualityIssueState = {
            issues: [],
            activeIssueId: null,
            manualHighlight: null
        };
    }

    return editor.__qualityIssueState;
}

function getIssueKey(issue) {
    if (!issue) {
        return null;
    }

    return issue.issueKey ?? issue.id ?? null;
}

function normalizeIssueSeverity(value) {
    const severity = String(value || "info").toLowerCase();
    if (severity === "warning" || severity === "high") {
        return "warning";
    }

    return "info";
}

function rebuildQualityIssueDecorations(editor) {
    if (!editor || !editor.view) {
        return;
    }

    const state = ensureQualityIssueState(editor);
    const decorations = [];
    const issues = Array.isArray(state.issues) ? state.issues : [];

    issues.forEach(issue => {
        const docRange = mapPlainRangeToDocRange(editor, issue.from, issue.to);
        if (!docRange) {
            return;
        }

        const severity = normalizeIssueSeverity(issue.severity);
        const kind = String(issue.kind || "").toLowerCase();
        const classes = [
            "quality-issue",
            `quality-issue--${severity}`
        ];

        if (kind) {
            classes.push(`quality-issue--${kind}`);
        }

        const issueKey = getIssueKey(issue);
        if (state.activeIssueId && issueKey !== null && String(issueKey) === String(state.activeIssueId)) {
            classes.push("quality-issue--active");
        }

        decorations.push(Decoration.inline(docRange.from, docRange.to, { class: classes.join(" ") }));
    });

    if (state.manualHighlight && state.manualHighlight.from < state.manualHighlight.to) {
        const manualRange = mapPlainRangeToDocRange(editor, state.manualHighlight.from, state.manualHighlight.to);
        if (manualRange) {
            decorations.push(Decoration.inline(manualRange.from, manualRange.to, { class: "quality-issue quality-issue--active" }));
        }
    }

    const set = DecorationSet.create(editor.state.doc, decorations);
    editor.view.dispatch(editor.state.tr.setMeta(qualityIssueDecorationsKey, set));
}

function focusAndScrollToRange(editor, docFrom, docTo) {
    if (!editor || !editor.view) {
        return;
    }

    editor.commands.focus();
    try {
        editor.chain().focus().setTextSelection({ from: docFrom, to: docTo }).run();
    } catch (error) {
        editor.chain().focus().setTextSelection(docFrom).run();
    }

    const tr = editor.state.tr.scrollIntoView();
    editor.view.dispatch(tr);
}

window.tiptapEditor = {
    create: function (elementId, initialContent, dotNetRef) {
        const interopState = createInteropState(dotNetRef);
        const ShortcutExtension = Extension.create({
            name: "appShortcuts",
            addKeyboardShortcuts() {
                return {
                    "Mod-b": () => {
                        toggleBold(this.editor);
                        return true;
                    },
                    "Mod-i": () => {
                        toggleItalic(this.editor);
                        return true;
                    },
                    "Mod-Shift-s": () => {
                        toggleStrike(this.editor);
                        return true;
                    },
                    "Mod-e": () => {
                        toggleCode(this.editor);
                        return true;
                    },
                    "Mod-Alt-0": () => {
                        setParagraph(this.editor);
                        return true;
                    },
                    "Mod-Alt-1": () => {
                        toggleHeading(this.editor, 1);
                        return true;
                    },
                    "Mod-Alt-2": () => {
                        toggleHeading(this.editor, 2);
                        return true;
                    },
                    "Mod-Alt-3": () => {
                        toggleHeading(this.editor, 3);
                        return true;
                    },
                    "Mod-Shift-q": () => {
                        toggleBlockquote(this.editor);
                        return true;
                    },
                    "Mod-Shift-h": () => {
                        insertHorizontalRule(this.editor);
                        return true;
                    },
                    "Mod-Shift-8": () => {
                        toggleBulletList(this.editor);
                        return true;
                    },
                    "Mod-Shift-7": () => {
                        toggleOrderedList(this.editor);
                        return true;
                    },
                    "Mod-Shift-f": () => {
                        safeInvoke(dotNetRef, interopState, "OnFocusModeShortcut");
                        return true;
                    },
                    "Alt-ArrowUp": () => {
                        safeInvoke(dotNetRef, interopState, "OnPrevSectionShortcut");
                        return true;
                    },
                    "Alt-ArrowDown": () => {
                        safeInvoke(dotNetRef, interopState, "OnNextSectionShortcut");
                        return true;
                    },
                    "Mod-z": () => {
                        safeInvoke(dotNetRef, interopState, "OnUndoShortcut");
                        return true;
                    },
                    "Mod-Shift-z": () => {
                        safeInvoke(dotNetRef, interopState, "OnRedoShortcut");
                        return true;
                    },
                    "Mod-y": () => {
                        safeInvoke(dotNetRef, interopState, "OnRedoShortcut");
                        return true;
                    }
                };
            }
        });

        const editor = new Editor({
            element: document.getElementById(elementId),
            extensions: [
                StarterKit,
                TextStyleWithFontSize,
                TextAlign.configure({ types: ["heading", "paragraph"] }),
                Link.configure({ openOnClick: false }),
                IndentExtension,
                AiDecorationsExtension,
                QualityIssueDecorationsExtension,
                PageGapDecorationsExtension,
                ShortcutExtension
            ],
            content: initialContent,
            editorProps: {
                attributes: {
                    class: "ProseMirror tiptap-content",
                    spellcheck: "true",
                    style: "white-space: pre-wrap;"
                }
            },
            onUpdate({ editor }) {
                safeInvoke(dotNetRef, interopState, "OnEditorContentChanged", editor.getHTML());
                schedulePageBreakUpdate(editor);
            }
        });

        editor.__interopState = interopState;
        ensureQualityIssueState(editor);
        maybeDebugEditorLayout(editor);

        let lastFormattingState = "";
        const pushFormattingState = () => {
            if (!dotNetRef || !interopState.enabled) {
                return;
            }

            const state = buildFormattingState(editor);
            const serialized = JSON.stringify(state);
            if (serialized === lastFormattingState) {
                return;
            }

            lastFormattingState = serialized;
            safeInvoke(dotNetRef, interopState, "OnEditorFormattingChanged", state);
        };

        editor.on("selectionUpdate", pushFormattingState);
        editor.on("update", pushFormattingState);
        pushFormattingState();

        let lastSelectionState = "";
        const pushSelectionState = () => {
            if (!dotNetRef || !interopState.enabled) {
                return;
            }

            const { from, to } = editor.state.selection;
            const prefix = editor.state.doc.textBetween(0, from, " ", " ");
            const selection = editor.state.doc.textBetween(from, to, " ", " ");
            const start = prefix.length;
            const end = start + selection.length;
            const serialized = `${start}:${end}`;
            if (serialized === lastSelectionState) {
                return;
            }

            lastSelectionState = serialized;
            safeInvoke(dotNetRef, interopState, "OnEditorSelectionChanged", start, end);
        };

        editor.on("selectionUpdate", pushSelectionState);
        editor.on("update", pushSelectionState);
        pushSelectionState();

        let lastBubbleState = "";
        const pushSelectionBubble = () => {
            if (!dotNetRef || !interopState.enabled) {
                return;
            }

            const { from, to, empty } = editor.state.selection;
            if (empty) {
                if (lastBubbleState !== "hidden") {
                    lastBubbleState = "hidden";
                    safeInvoke(dotNetRef, interopState, "OnEditorSelectionBubble", 0, 0, false);
                }
                return;
            }

            const anchor = Math.round((from + to) / 2);
            let coords = null;
            try {
                coords = editor.view.coordsAtPos(anchor);
            } catch (error) {
                return;
            }

            if (!coords) {
                return;
            }

            const payload = `${coords.left}:${coords.top}`;
            if (payload === lastBubbleState) {
                return;
            }

            lastBubbleState = payload;
            safeInvoke(dotNetRef, interopState, "OnEditorSelectionBubble", coords.left, coords.top, true);
        };

        editor.on("selectionUpdate", pushSelectionBubble);
        editor.on("update", pushSelectionBubble);
        pushSelectionBubble();

        let lastOutlineState = "";
        const pushOutlineState = () => {
            if (!dotNetRef || !interopState.enabled) {
                return;
            }

            const outline = buildOutline(editor);
            const serialized = JSON.stringify(outline);
            if (serialized === lastOutlineState) {
                return;
            }

            lastOutlineState = serialized;
            safeInvoke(dotNetRef, interopState, "OnEditorOutlineChanged", outline);
        };

        editor.on("update", pushOutlineState);
        pushOutlineState();

        editor.__pageBreakState = { enabled: false, options: resolvePageBreakOptions(null) };
        const resizeHandler = () => schedulePageBreakUpdate(editor);
        window.addEventListener("resize", resizeHandler);
        editor.__pageBreakResizeHandler = resizeHandler;

        const setupScrollSync = () => {
            const editorScroll = editor.view?.dom?.closest(".editor-pane")?.querySelector(".pane-body");
            const previewScroll = document.querySelector(".preview-pane .pane-body");
            if (!editorScroll || !previewScroll) {
                return;
            }

            let isSyncing = false;
            const syncScroll = (source, target) => {
                if (isSyncing) {
                    return;
                }

                if (source.scrollHeight <= source.clientHeight || target.scrollHeight <= target.clientHeight) {
                    return;
                }

                isSyncing = true;
                const ratio = source.scrollTop / (source.scrollHeight - source.clientHeight);
                const targetMax = target.scrollHeight - target.clientHeight;
                target.scrollTop = Math.round(ratio * targetMax);
                requestAnimationFrame(() => {
                    isSyncing = false;
                });
            };

            editorScroll.addEventListener("scroll", () => syncScroll(editorScroll, previewScroll));
            previewScroll.addEventListener("scroll", () => syncScroll(previewScroll, editorScroll));
        };

        setupScrollSync();

        return editor;
    },

    setAiDecorations: function (editor, ranges) {
        if (!editor || !editor.view) {
            return;
        }

        const decorations = buildAiDecorations(editor, ranges);
        const tr = editor.state.tr.setMeta(aiDecorationsKey, decorations);
        editor.view.dispatch(tr);
    },

    setQualityIssues: function (editor, issues) {
        if (!editor || !editor.view) {
            return;
        }

        const state = ensureQualityIssueState(editor);
        state.issues = Array.isArray(issues) ? issues : [];

        if (window.__waQualityDebug && !editor.__waQualityIssueDebugLogged) {
            editor.__waQualityIssueDebugLogged = true;
            const sample = state.issues.length > 0 ? state.issues[0] : null;
            console.debug("[quality] setQualityIssues sample keys", sample ? Object.keys(sample) : []);
        }

        if (state.activeIssueId) {
            const activeExists = state.issues.some(issue => {
                const key = getIssueKey(issue);
                return key !== null && String(key) === String(state.activeIssueId);
            });
            if (window.__waQualityDebug) {
                console.debug("[quality] active match", {
                    activeIssueId: state.activeIssueId,
                    matched: activeExists
                });
            }
            if (!activeExists) {
                state.activeIssueId = null;
                state.manualHighlight = null;
            }
        }

        rebuildQualityIssueDecorations(editor);
    },

    setQualityIssuesActive: function (editor, issueId) {
        if (!editor || !editor.view) {
            return;
        }

        const state = ensureQualityIssueState(editor);
        state.activeIssueId = issueId ? String(issueId) : null;
        rebuildQualityIssueDecorations(editor);
    },

    clearQualityIssues: function (editor) {
        if (!editor || !editor.view) {
            return;
        }

        const state = ensureQualityIssueState(editor);
        state.issues = [];
        state.activeIssueId = null;
        state.manualHighlight = null;
        rebuildQualityIssueDecorations(editor);
    },

    scrollToQualityIssue: function (editor, issueId) {
        if (!editor || !editor.view || !issueId) {
            return false;
        }

        const state = ensureQualityIssueState(editor);
        const issue = (state.issues || []).find(item => {
            const key = getIssueKey(item);
            return key !== null && String(key) === String(issueId);
        });

        if (!issue) {
            return false;
        }

        return window.tiptapEditor.highlightQualityIssue(
            editor,
            issue.from,
            issue.to,
            issueId,
            issue.anchorText || null);
    },

    highlightQualityIssue: function (editor, from, to, issueId, anchorText) {
        if (!editor || !editor.view) {
            return false;
        }

        if (window.__waQualityDebug) {
            console.debug("[quality] highlight request", { issueId, from, to, anchorText });
        }

        const state = ensureQualityIssueState(editor);
        let range = normalizePlainRange(from, to);
        let mapped = range.to > range.from
            ? mapPlainRangeToDocRange(editor, range.from, range.to)
            : null;

        if (!mapped) {
            const fromAnchor = resolveRangeFromAnchorText(editor, anchorText);
            if (!fromAnchor) {
                return false;
            }

            range = normalizePlainRange(fromAnchor.from, fromAnchor.to);
            mapped = mapPlainRangeToDocRange(editor, range.from, range.to);
            if (!mapped) {
                return false;
            }
        }

        state.activeIssueId = issueId ? String(issueId) : null;
        state.manualHighlight = {
            issueKey: issueId ? String(issueId) : null,
            from: range.from,
            to: range.to
        };
        rebuildQualityIssueDecorations(editor);
        if (window.__waQualityDebug) {
            console.debug("[quality] highlight resolved", {
                issueId: state.activeIssueId,
                mappedFrom: mapped.from,
                mappedTo: mapped.to,
                decorationsRebuilt: true
            });
        }
        focusAndScrollToRange(editor, mapped.from, mapped.to);
        return true;
    },

    clearQualityIssueHighlight: function (editor, issueId) {
        if (!editor || !editor.view) {
            return;
        }

        const state = ensureQualityIssueState(editor);
        if (!issueId || String(state.activeIssueId) === String(issueId)) {
            state.activeIssueId = null;
            state.manualHighlight = null;
        }

        rebuildQualityIssueDecorations(editor);
    },

    applyQualityIssueFix: function (editor, fix) {
        if (!editor || !editor.view || !fix) {
            return false;
        }

        const kind = String(fix.kind || "").toLowerCase();
        const from = Number(fix.from);
        const to = Number(fix.to);
        if (!Number.isFinite(from) || !Number.isFinite(to)) {
            return false;
        }

        const normalized = normalizePlainRange(from, to);
        const mapped = mapPlainRangeToDocRange(editor, normalized.from, normalized.to);
        if (!mapped) {
            return false;
        }

        const text = typeof fix.text === "string" ? fix.text : "";
        const tr = editor.state.tr;

        if (kind === "replace") {
            tr.insertText(text, mapped.from, mapped.to);
        } else if (kind === "delete") {
            tr.delete(mapped.from, mapped.to);
        } else if (kind === "insert") {
            tr.insertText(text, mapped.from);
        } else {
            return false;
        }

        editor.view.dispatch(tr.scrollIntoView());
        editor.commands.focus();
        return true;
    },

    attachContextMenu: function (elementId, dotNetRef) {
        const element = document.getElementById(elementId);
        if (!element) {
            return;
        }

        if (element.__contextMenuHandler) {
            element.removeEventListener("contextmenu", element.__contextMenuHandler);
        }

        const interopState = createInteropState(dotNetRef);
        const handler = event => {
            event.preventDefault();
            safeInvoke(dotNetRef, interopState, "OnEditorContextMenu", event.clientX, event.clientY);
        };

        element.addEventListener("contextmenu", handler);
        element.__contextMenuHandler = handler;
        element.__contextMenuInteropState = interopState;
    },

    detachContextMenu: function (elementId) {
        const element = document.getElementById(elementId);
        if (!element) {
            return;
        }

        if (element.__contextMenuHandler) {
            element.removeEventListener("contextmenu", element.__contextMenuHandler);
        }

        if (element.__contextMenuInteropState) {
            element.__contextMenuInteropState.enabled = false;
        }

        element.__contextMenuHandler = null;
        element.__contextMenuInteropState = null;
    },

    prepareSectionDrag: function (event) {
        if (!event || !event.dataTransfer) {
            return;
        }

        event.dataTransfer.setData("text/plain", "section");
        event.dataTransfer.effectAllowed = "move";
        event.dataTransfer.dropEffect = "move";
    },

    setContent: function (editor, content) {
        editor.commands.setContent(content, false);
    },

    getContent: function (editor) {
        if (!editor) {
            return "";
        }

        return editor.getHTML ? editor.getHTML() : "";
    },

    setPageBreaksEnabled: function (editor, enabled, options) {
        if (!editor) {
            return 1;
        }

        if (!editor.__pageBreakState) {
            editor.__pageBreakState = { enabled: false, options: resolvePageBreakOptions(options) };
        }

        editor.__pageBreakState.enabled = !!enabled;
        editor.__pageBreakState.options = resolvePageBreakOptions(options);

        if (!enabled) {
            const ctx = getPageBreakContext(editor);
            const overlay = ctx?.overlayHost?.querySelector?.(".pagebreak-overlay");
            if (overlay) {
                overlay.innerHTML = "";
            }

            const tr = editor.view.state.tr.setMeta(pageGapDecorationsKey, DecorationSet.empty);
            editor.view.dispatch(tr);
            editor.__pageGapState = { gapCount: 0, pageCount: 1 };
            return 1;
        }

        const count = renderPageBreakOverlay(editor, editor.__pageBreakState.options);
        updatePageGapDecorations(editor);
        return count;
    },

    registerPageBreakObserver: function (editor, dotNetRef, options) {
        if (!editor) {
            return;
        }

        if (!editor.__pageBreakState) {
            editor.__pageBreakState = { enabled: false, options: resolvePageBreakOptions(options) };
        }

        editor.__pageBreakState.dotNetRef = dotNetRef;
        editor.__pageBreakState.interopState = createInteropState(dotNetRef);
        editor.__pageBreakState.options = resolvePageBreakOptions(options);
        editor.__pageBreakState.enabled = true;

        if (!editor.__pageBreakState.scrollHandler) {
            const ctx = getPageBreakContext(editor);
            const scrollContainer = ctx ? findScrollContainer(ctx.viewport) : window;
            const handler = () => schedulePageBreakUpdate(editor);
            const rafHandler = () => {
                if (editor.__pageBreakState.rafPending) {
                    return;
                }
                editor.__pageBreakState.rafPending = true;
                requestAnimationFrame(() => {
                    editor.__pageBreakState.rafPending = false;
                    handler();
                });
            };

            editor.__pageBreakState.scrollContainer = scrollContainer;
            editor.__pageBreakState.scrollHandler = rafHandler;
            if (scrollContainer === window) {
                window.addEventListener("scroll", rafHandler, { passive: true });
            } else {
                scrollContainer.addEventListener("scroll", rafHandler, { passive: true });
            }
        }

        notifyPageBreakStatus(editor);
    },

    scrollToPage: function (editor, pageIndex, options) {
        const info = computePageBreaks(editor, options);
        const ctx = info.ctx;
        if (!ctx) {
            return;
        }

        const target = Math.max(1, Math.min(info.count, pageIndex));
        const topPx = info.breaks[target - 1]?.topPx ?? 0;
        const viewportRect = ctx.viewport.getBoundingClientRect();
        const absoluteTop = window.scrollY + viewportRect.top + topPx - 80;
        window.scrollTo({ top: Math.max(0, absoluteTop), behavior: "smooth" });
    },

    destroy: function (editor) {
        if (editor && editor.__interopState) {
            editor.__interopState.enabled = false;
        }

        if (editor && editor.__pageBreakResizeHandler) {
            window.removeEventListener("resize", editor.__pageBreakResizeHandler);
            editor.__pageBreakResizeHandler = null;
        }
        if (editor && editor.__pageBreakState && editor.__pageBreakState.scrollHandler) {
            const container = editor.__pageBreakState.scrollContainer || window;
            if (container === window) {
                window.removeEventListener("scroll", editor.__pageBreakState.scrollHandler);
            } else {
                container.removeEventListener("scroll", editor.__pageBreakState.scrollHandler);
            }
            editor.__pageBreakState.scrollHandler = null;
            editor.__pageBreakState.scrollContainer = null;
        }
        editor.destroy();
    },

    notifyLayoutChanged: function () {
        if (typeof window === "undefined") {
            return;
        }

        requestAnimationFrame(() => {
            window.dispatchEvent(new Event("resize"));
        });
    }
};

if (!window.__writerAppDragInit) {
    window.__writerAppDragInit = true;
    document.addEventListener("dragstart", event => {
        let targetElement = null;
        if (event.target instanceof Element) {
            targetElement = event.target;
        } else if (event.target && event.target.parentElement) {
            targetElement = event.target.parentElement;
        }

        let draggableRoot = targetElement?.closest?.(".drag-handle");
        if (!draggableRoot && typeof event.composedPath === "function") {
            const path = event.composedPath();
            draggableRoot = path.find(entry => entry instanceof Element && entry.classList.contains("drag-handle"));
        }

        if (!draggableRoot) {
            draggableRoot = targetElement?.closest?.(".section-nav-row");
        }

        if (!draggableRoot || !draggableRoot.draggable) {
            return;
        }

        if (!event.dataTransfer) {
            return;
        }

        event.dataTransfer.setData("text/plain", "section");
        event.dataTransfer.effectAllowed = "move";
        event.dataTransfer.dropEffect = "move";
    });
}
