declare global {
    interface Window {
        tiptapEditor?: { create: (elementId: string, initialContent: string, dotNetRef: any) => any };
        __writer_disable_mask_pagination?: boolean;
        __writer_pagination_debug?: { enabled?: boolean; last?: any };
    }
}
import { Editor, Extension } from "@tiptap/core";
import { Plugin, PluginKey } from "prosemirror-state";
import { Decoration, DecorationSet } from "prosemirror-view";
import StarterKit from "@tiptap/starter-kit";
import TextStyle from "@tiptap/extension-text-style";
import TextAlign from "@tiptap/extension-text-align";
import Link from "@tiptap/extension-link";
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
} from "./tiptap-commands";

if (typeof window !== "undefined" && window.__writer_disable_mask_pagination === undefined) {
    window.__writer_disable_mask_pagination = false;
}

if (typeof window !== "undefined" && window.__writer_pagination_debug === undefined) {
    window.__writer_pagination_debug = { enabled: false };
}

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
const pageGapDecorationsKey = new PluginKey("pageGapDecorations");
const headingNumberDecorationsKey = new PluginKey("headingNumberDecorations");
const WA_LAYOUT_META = "wa_layout_tx";
const WA_HEADING_NUMBERING_REBUILD = "wa_heading_numbering_rebuild";

function isWriterDebugEnabled() {
    try {
        return window?.localStorage?.getItem("writerapp.debug") === "true";
    } catch {
        return false;
    }
}

function debugHeading(stage, payload) {
    if (!isWriterDebugEnabled()) {
        return;
    }
    try {
        console.log("[heading-numbering]", { stage, ...payload });
    } catch {
    }
}

// Debug: localStorage.setItem("writerapp.debug","true"); location.reload();
// Disable: localStorage.removeItem("writerapp.debug"); location.reload();

function hashStringFNV1a(value) {
    if (!value) {
        return "0";
    }
    let hash = 0x811c9dc5;
    for (let index = 0; index < value.length; index += 1) {
        hash ^= value.charCodeAt(index);
        hash = (hash * 0x01000193) >>> 0;
    }
    return hash.toString(16);
}

function getHeadingDocSummary(editor) {
    const view = editor?.view;
    if (!view) {
        return null;
    }

    const nodeTypeCounts = {
        heading: 0,
        paragraph: 0,
        bulletList: 0,
        orderedList: 0,
        blockquote: 0,
        codeBlock: 0,
        hardBreak: 0
    };
    const headingLevelsCounts = { 1: 0, 2: 0, 3: 0, 4: 0, 5: 0, 6: 0 };
    const firstHeadingSamples = [];

    view.state.doc.descendants((node, pos) => {
        const type = node.type?.name;
        if (type && Object.prototype.hasOwnProperty.call(nodeTypeCounts, type)) {
            nodeTypeCounts[type] += 1;
        }

        if (type === "heading") {
            const level = Math.max(1, Math.min(6, Number(node.attrs?.level ?? 1)));
            headingLevelsCounts[level] += 1;
            if (firstHeadingSamples.length < 3) {
                const text = (node.textContent || "").slice(0, 40);
                firstHeadingSamples.push({ level, textSnippet: text, pos });
            }
        }
    });

    const docSize = view.state.doc.content.size;
    const textLength = view.state.doc.textBetween(0, docSize, "\n", "\n").length;
    return {
        docSize,
        textLength,
        nodeTypeCounts,
        headingLevelsCounts,
        firstHeadingSamples
    };
}

const AiDecorationsExtension = Extension.create({
    name: "aiDecorations",
    addProseMirrorPlugins() {
        return [
            new Plugin({
                key: aiDecorationsKey,
                state: {
                    init: () => DecorationSet.empty,
                    apply: (tr, value) => {
                        const current = value ?? DecorationSet.empty;
                        const next = tr.getMeta(aiDecorationsKey);
                        if (next) {
                            return next;
                        }

                        return current.map(tr.mapping, tr.doc);
                    }
                },
                props: {
                    decorations(state) {
                        return aiDecorationsKey.getState(state) ?? DecorationSet.empty;
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
                        const current = value ?? DecorationSet.empty;
                        const next = tr.getMeta(pageGapDecorationsKey);
                        if (next) {
                            return next;
                        }

                        return current.map(tr.mapping, tr.doc);
                    }
                },
                props: {
                    decorations(state) {
                        return pageGapDecorationsKey.getState(state) ?? DecorationSet.empty;
                    }
                }
            })
        ];
    }
});

const HeadingNumberingExtension = Extension.create({
    name: "headingNumbering",
    addProseMirrorPlugins() {
        const editor = this.editor;
        return [
            new Plugin({
                key: headingNumberDecorationsKey,
                state: {
                    init: () => DecorationSet.empty,
                    apply: (tr, value) => {
                        const current = value ?? DecorationSet.empty;
                        const next = tr.getMeta(headingNumberDecorationsKey);
                        if (next) {
                            return next;
                        }

                        return current.map(tr.mapping, tr.doc);
                    }
                },
                props: {
                    decorations(state) {
                        return headingNumberDecorationsKey.getState(state) ?? DecorationSet.empty;
                    }
                },
                view(view) {
                    const scheduler = createHeadingNumberingScheduler(editor, view);
                    if (editor) {
                        editor.__headingNumberingScheduler = scheduler;
                    }
                    return {
                        destroy() {
                            if (scheduler?.destroy) {
                                scheduler.destroy();
                            }
                            if (editor && editor.__headingNumberingScheduler === scheduler) {
                                editor.__headingNumberingScheduler = null;
                            }
                        }
                    };
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
    const content = lane || view.closest(".editor-content") || view;
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

function getCssNumber(style, name, fallback) {
    if (!style) {
        return fallback;
    }

    const raw = style.getPropertyValue(name);
    if (!raw) {
        return fallback;
    }

    const parsed = Number.parseFloat(raw);
    return Number.isFinite(parsed) ? parsed : fallback;
}

function resolvePaginationMetrics(ctx, options) {
    const opts = resolvePageBreakOptions(options);
    const host = ctx?.lane || ctx?.overlayHost || ctx?.content;
    const hostStyle = host ? window.getComputedStyle(host) : null;
    const shell = host?.closest?.(".editor-shell");
    const shellStyle = shell ? window.getComputedStyle(shell) : hostStyle;

    // Single source of truth for scaled page geometry (overlay + spacers must use the same values).
    const scale = getCssNumber(shellStyle, "--editor-font-scale", 1);
    const pageWidth = getCssNumber(shellStyle, "--page-width-px", opts.pageWidthPx ?? 760) * scale;
    const pageHeight = getCssNumber(shellStyle, "--page-height-px", opts.pageHeightPx ?? 980) * scale;
    const pageGap = getCssNumber(shellStyle, "--page-gap-px", opts.pageGapPx ?? 32) * scale;
    const padY = getCssNumber(shellStyle, "--page-padding-y", 24) * scale;
    const padX = getCssNumber(shellStyle, "--page-padding-x", 20) * scale;
    const band = pageHeight + pageGap;
    const contentHeight = Math.max(0, pageHeight - padY * 2);

    return {
        scale,
        pageWidth,
        pageHeight,
        pageGap,
        padY,
        padX,
        band,
        contentHeight,
        options: opts
    };
}

function toLaneLocalY(ctx, coords) {
    if (!ctx?.lane || !coords) {
        return coords?.top ?? 0;
    }

    const laneRect = ctx.lane.getBoundingClientRect();
    return coords.top - laneRect.top;
}

function ensurePaginationDebugOverlay(ctx) {
    const lane = ctx?.lane;
    if (!lane) {
        return null;
    }

    let overlay = lane.querySelector(".wa-pagination-debug");
    if (!overlay) {
        overlay = document.createElement("div");
        overlay.className = "wa-pagination-debug";
        overlay.setAttribute("aria-hidden", "true");
        lane.appendChild(overlay);
    }

    return overlay;
}

function renderPaginationDebug(ctx, info, gapInfo, debugMeta) {
    const state = window.__writer_pagination_debug;
    const metrics = info?.metrics;
    const lane = ctx?.lane;
    if (!lane || !metrics) {
        return;
    }

    const overlay = ensurePaginationDebugOverlay(ctx);
    if (!overlay) {
        return;
    }

    if (!state?.enabled) {
        overlay.innerHTML = "";
        overlay.style.display = "none";
        return;
    }

    overlay.style.display = "block";
    overlay.innerHTML = "";
    if (debugMeta) {
        const insertEps = debugMeta.insertEpsPx ?? "n/a";
        const removeEps = debugMeta.removeEpsPx ?? "n/a";
        const reason = debugMeta.reason ?? "update";
        const layout = debugMeta.lastLayoutTx ? "layout" : "content";
        overlay.setAttribute("data-label", `insert=${insertEps} remove=${removeEps} reason=${reason} lastTx=${layout}`);
    }

    const band = metrics.band;
    const pageCount = info?.count ?? 1;
    const scrollContainer = findScrollContainer(ctx.viewport);
    const scrollTop = gapInfo?.scrollTop ?? (scrollContainer === window ? window.scrollY : scrollContainer.scrollTop);
    const laneTop = gapInfo?.laneTop ?? ctx.lane?.getBoundingClientRect?.().top ?? 0;

    for (let pageIndex = 0; pageIndex < pageCount; pageIndex += 1) {
        const pageTop = pageIndex * band;
        const contentStart = pageTop + metrics.padY;
        const contentEnd = contentStart + metrics.contentHeight;
        const pageEnd = pageTop + metrics.pageHeight;
        const bandEnd = pageTop + band;

        const pageStartLine = document.createElement("div");
        pageStartLine.className = "wa-debug-line wa-debug-page-start";
        pageStartLine.style.top = `${pageTop}px`;
        pageStartLine.setAttribute("data-label", `p${pageIndex + 1} start`);
        overlay.appendChild(pageStartLine);

        const contentStartLine = document.createElement("div");
        contentStartLine.className = "wa-debug-line wa-debug-content-start";
        contentStartLine.style.top = `${contentStart}px`;
        contentStartLine.setAttribute("data-label", `p${pageIndex + 1} content start`);
        overlay.appendChild(contentStartLine);

        const contentEndLine = document.createElement("div");
        contentEndLine.className = "wa-debug-line wa-debug-content-end";
        contentEndLine.style.top = `${contentEnd}px`;
        contentEndLine.setAttribute("data-label", `p${pageIndex + 1} content end`);
        overlay.appendChild(contentEndLine);

        if (metrics.pageGap > 0) {
            const gapStartLine = document.createElement("div");
            gapStartLine.className = "wa-debug-line wa-debug-gap-start";
            gapStartLine.style.top = `${pageEnd}px`;
            gapStartLine.setAttribute("data-label", `p${pageIndex + 1} gap start`);
            overlay.appendChild(gapStartLine);

            const gapBand = document.createElement("div");
            gapBand.className = "wa-debug-gap";
            gapBand.style.top = `${pageEnd}px`;
            gapBand.style.height = `${metrics.pageGap}px`;
            overlay.appendChild(gapBand);

            const gapEndLine = document.createElement("div");
            gapEndLine.className = "wa-debug-line wa-debug-gap-end";
            gapEndLine.style.top = `${bandEnd}px`;
            gapEndLine.setAttribute("data-label", `p${pageIndex + 1} gap end`);
            overlay.appendChild(gapEndLine);
        }
    }

    if (gapInfo?.breaks?.length) {
        gapInfo.breaks.forEach(entry => {
            const marker = document.createElement("div");
            marker.className = "wa-debug-break";
            marker.style.top = `${entry.blockTop}px`;
            marker.setAttribute("data-label", `p${entry.pageIndex + 1} ${Math.round(entry.spacerPx)}px @${entry.pos}`);
            overlay.appendChild(marker);
        });
    }

    if (gapInfo?.tallBreaks?.length) {
        gapInfo.tallBreaks.forEach(entry => {
            const marker = document.createElement("div");
            marker.className = "wa-debug-break wa-debug-tall-break";
            marker.style.top = `${entry.yAtSplit}px`;
            marker.setAttribute("data-label", `p${entry.pageIndex + 1} ${Math.round(entry.spacerPx)}px @${entry.pos}`);
            overlay.appendChild(marker);
        });
    }

    const label = document.createElement("div");
    label.className = "wa-debug-label";
    const headingNumbers = debugMeta?.headingNumbers ? "on" : "off";
    label.textContent = `pageHeight=${Math.round(metrics.pageHeight)} padY=${Math.round(metrics.padY)} printable=${Math.round(metrics.contentHeight)} gap=${Math.round(metrics.pageGap)} band=${Math.round(metrics.band)} | scrollTop=${Math.round(scrollTop)} laneTop=${Math.round(laneTop)} | headingNumbers=${headingNumbers}`;
    overlay.appendChild(label);

    window.__writer_pagination_debug = {
        ...(state ?? {}),
        last: {
            metrics,
            pageCount,
            breaks: gapInfo?.breaks ?? [],
            tallBreaks: gapInfo?.tallBreaks ?? [],
            scrollTop,
            laneTop,
            boundaries: Array.from({ length: pageCount }, (_, index) => {
                const pageTop = index * band;
                const contentStart = pageTop + metrics.padY;
                const contentEnd = contentStart + metrics.contentHeight;
                const pageEnd = pageTop + metrics.pageHeight;
                return { pageIndex: index, pageTop, contentStart, contentEnd, pageEnd };
            })
        }
    };
}

function computePageBreaks(editor, options) {
    const ctx = getPageBreakContext(editor);
    if (!ctx) {
        return { count: 1, breaks: [], options: resolvePageBreakOptions(options), ctx: null };
    }

    const metrics = resolvePaginationMetrics(ctx, options);
    const rawHeight = ctx.view.scrollHeight || 0;
    const count = Math.max(1, Math.ceil((rawHeight + metrics.pageGap) / metrics.band));

    const baseTop = 0;
    const leftOffset = 0;
    const width = metrics.pageWidth;
    const breaks = [];
    for (let pageIndex = 1; pageIndex <= count; pageIndex += 1) {
        const topPx = baseTop + (pageIndex - 1) * metrics.band;
        breaks.push({ pageIndex, topPx });
    }

    return { count, breaks, leftOffset, width, options: metrics.options, ctx, baseTop, metrics };
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
    const overlayHeight = info.metrics
        ? Math.max(ctx.view.scrollHeight || 0, info.count * info.metrics.band - info.metrics.pageGap)
        : (ctx.view.scrollHeight || 0);
    const overlayWidth = info.metrics?.pageWidth
        ?? (ctx.content.clientWidth || ctx.content.getBoundingClientRect().width);
    overlay.style.height = `${overlayHeight}px`;
    overlay.style.width = `${overlayWidth}px`;
    if (ctx.overlayHost) {
        ctx.overlayHost.style.setProperty("--lane-height", `${overlayHeight}px`);
    }

    const mode = info.options.layoutMode || "simple";
    const sheetHeight = Math.max(0, info.metrics?.pageHeight ?? info.options.pageHeightPx);

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

function getHeadingNumberingSettingsKey(editor) {
    const enabled = editor?.__headingNumberingEnabled !== false;
    const scope = editor?.__headingNumberingScope ?? "document";
    const prefix = Array.isArray(editor?.__headingNumberingPrefix)
        ? editor.__headingNumberingPrefix
        : [0, 0, 0, 0, 0, 0, 0];
    const normalized = [];
    for (let index = 1; index <= 6; index += 1) {
        normalized.push(Number(prefix[index]) || 0);
    }
    return `${enabled ? "1" : "0"}:${scope}:${normalized.join(",")}`;
}

function buildHeadingNumberSignature(docSize, headingCount, firstPos, lastPos) {
    const safeFirst = Number.isFinite(firstPos) ? firstPos : 0;
    const safeLast = Number.isFinite(lastPos) ? lastPos : 0;
    return `${docSize}:${headingCount}:${safeFirst}:${safeLast}`;
}

function countHeadingsInDoc(doc) {
    if (!doc) {
        return 0;
    }

    let count = 0;
    doc.descendants(node => {
        if (node.type?.name === "heading") {
            count += 1;
        }
    });
    return count;
}

function ensureHeadingNumberingState(editor) {
    if (!editor) {
        return null;
    }

    if (!editor.__headingNumberingState) {
        editor.__headingNumberingState = {
            lastGoodDecorations: DecorationSet.empty,
            lastSignature: null,
            lastSettingsKey: null,
            pendingRebuild: false,
            pendingReason: null,
            pendingTraceId: null,
            pendingFrameId: null
        };
    }

    return editor.__headingNumberingState;
}

function buildHeadingNumberDecorations(editor, state) {
    const view = editor?.view;
    const doc = state?.doc;
    if (!view || !doc) {
        return {
            decorations: DecorationSet.empty,
            headingCount: 0,
            decorationsCreated: 0,
            samples: [],
            headingLevelsCounts: { 1: 0, 2: 0, 3: 0, 4: 0, 5: 0, 6: 0 },
            signature: buildHeadingNumberSignature(0, 0, 0, 0)
        };
    }

    const numberingEnabled = editor.__headingNumberingEnabled !== false;
    const decorations = [];
    const prefix = Array.isArray(editor.__headingNumberingPrefix)
        ? editor.__headingNumberingPrefix
        : [0, 0, 0, 0, 0, 0, 0];
    const counters = [0, 0, 0, 0, 0, 0, 0];
    const samples = [];
    let headingCount = 0;
    let firstHeadingPos = null;
    let lastHeadingPos = null;
    const headingLevelsCounts = { 1: 0, 2: 0, 3: 0, 4: 0, 5: 0, 6: 0 };
    for (let index = 1; index <= 6; index += 1) {
        counters[index] = Number(prefix[index]) || 0;
    }

    doc.descendants((node, pos) => {
        if (node.type?.name !== "heading") {
            return;
        }

        headingCount += 1;
        if (firstHeadingPos === null) {
            firstHeadingPos = pos;
        }
        lastHeadingPos = pos;

        const level = Math.max(1, Math.min(6, Number(node.attrs?.level ?? 1)));
        headingLevelsCounts[level] += 1;

        if (!numberingEnabled) {
            return;
        }

        counters[level] += 1;
        for (let index = level + 1; index <= 6; index += 1) {
            counters[index] = 0;
        }

        // Render a fixed 3-part version using H1/H2/H3 counters.
        const part1 = counters[1] || 0;
        const part2 = counters[2] || 0;
        const part3 = counters[3] || 0;
        const label = `${part1}.${part2}.${part3}`;
        if (samples.length < 3) {
            samples.push(label);
        }

        const marker = document.createElement("span");
        marker.className = "wa-heading-number";
        marker.setAttribute("data-number", label);
        marker.setAttribute("contenteditable", "false");
        marker.setAttribute("draggable", "false");
        marker.textContent = label;

        const deco = Decoration.widget(pos + 1, marker, {
            key: `wa-heading-number:${pos}`,
            side: -1,
            stopEvent: () => false,
            ignoreSelection: true
        });
        decorations.push(deco);
    });

    const signature = buildHeadingNumberSignature(doc.content.size, headingCount, firstHeadingPos, lastHeadingPos);

    return {
        decorations: DecorationSet.create(doc, decorations),
        headingCount,
        decorationsCreated: decorations.length,
        samples,
        headingLevelsCounts,
        signature
    };
}

function runHeadingNumberingRebuild(editor, view, reason, traceId) {
    if (!editor || !view) {
        return;
    }

    const headingState = ensureHeadingNumberingState(editor);
    if (!headingState) {
        return;
    }

    const state = view.state;
    const settingsKey = getHeadingNumberingSettingsKey(editor);
    const result = buildHeadingNumberDecorations(editor, state);
    const signature = result.signature;
    const effectiveTraceId = traceId ?? editor.__headingNumberingTraceId;

    if (headingState.lastSignature === signature && headingState.lastSettingsKey === settingsKey) {
        debugHeading("REBUILD_SKIPPED_NO_CHANGE", { traceId: effectiveTraceId, signature });
        return;
    }

    const numberingEnabled = editor.__headingNumberingEnabled !== false;
    let decorationsToApply = result.decorations;
    let keptPrevious = false;
    if (numberingEnabled && result.headingCount > 0 && result.decorationsCreated === 0) {
        decorationsToApply = headingState.lastGoodDecorations ?? DecorationSet.empty;
        keptPrevious = true;
        debugHeading("REBUILD_GUARD_KEEP_PREVIOUS", {
            traceId: effectiveTraceId,
            reason: reason ?? "unknown",
            headingsFound: result.headingCount,
            decorationsCreated: result.decorationsCreated
        });
    }

    const tr = state.tr.setMeta(headingNumberDecorationsKey, decorationsToApply);
    tr.setMeta(WA_LAYOUT_META, true);
    editor.__headingNumberingApplying = true;
    try {
        view.dispatch(tr);
    } finally {
        editor.__headingNumberingApplying = false;
    }

    if (numberingEnabled && result.headingCount > 0 && !keptPrevious) {
        headingState.lastGoodDecorations = decorationsToApply;
    }

    headingState.lastSignature = signature;
    headingState.lastSettingsKey = settingsKey;

    debugHeading("REBUILD_DONE", {
        traceId: effectiveTraceId,
        reason: reason ?? "unknown",
        enabled: numberingEnabled,
        scope: editor.__headingNumberingScope ?? "document",
        headingCount: result.headingCount,
        decorationsCreated: result.decorationsCreated,
        samples: result.samples,
        headingLevelsCounts: result.headingLevelsCounts,
        prefixCountersUsed: (editor.__headingNumberingPrefix ?? []).slice(1, 4)
    });

    requestAnimationFrame(() => {
        const domCount = editor?.view?.dom?.querySelectorAll?.(".wa-heading-number")?.length ?? 0;
        const headingCount = countHeadingsInDoc(view.state.doc);
        debugHeading("DOM_VERIFY", { traceId: effectiveTraceId, domCount, headingCount });
    });
}

function createHeadingNumberingScheduler(editor, view) {
    const headingState = ensureHeadingNumberingState(editor);
    if (!headingState) {
        return null;
    }

    const scheduleRebuild = (reason, traceId) => {
        const nextReason = reason ?? "unknown";
        const nextTraceId = traceId ?? editor?.__headingNumberingTraceId;
        headingState.pendingReason = nextReason;
        headingState.pendingTraceId = nextTraceId;
        debugHeading("REBUILD_SCHEDULED", { traceId: nextTraceId, reason: nextReason });
        if (headingState.pendingRebuild) {
            return;
        }

        headingState.pendingRebuild = true;
        const schedule = typeof requestAnimationFrame === "function"
            ? requestAnimationFrame
            : (callback) => queueMicrotask(callback);
        const frameId = schedule(() => {
            headingState.pendingRebuild = false;
            headingState.pendingFrameId = null;
            const pendingReason = headingState.pendingReason;
            const pendingTraceId = headingState.pendingTraceId;
            headingState.pendingReason = null;
            headingState.pendingTraceId = null;
            runHeadingNumberingRebuild(editor, view, pendingReason, pendingTraceId);
        });
        headingState.pendingFrameId = typeof frameId === "number" ? frameId : null;
    };

    const destroy = () => {
        if (headingState.pendingFrameId !== null && typeof cancelAnimationFrame === "function") {
            cancelAnimationFrame(headingState.pendingFrameId);
        }
        headingState.pendingRebuild = false;
        headingState.pendingFrameId = null;
        headingState.pendingReason = null;
        headingState.pendingTraceId = null;
    };

    return { requestRebuild: scheduleRebuild, destroy };
}

function requestHeadingNumberingRebuild(editor, reason) {
    if (!editor?.view) {
        return;
    }

    const scheduler = editor.__headingNumberingScheduler;
    if (scheduler?.requestRebuild) {
        scheduler.requestRebuild(reason, editor.__headingNumberingTraceId);
        return;
    }

    runHeadingNumberingRebuild(editor, editor.view, reason, editor.__headingNumberingTraceId);
}

function buildPageGapDecorations(editor, options) {
    const view = editor?.view;
    const ctx = getPageBreakContext(editor);
    if (!view || !ctx) {
        return { decorations: DecorationSet.empty, gapCount: 0, pageCount: 1 };
    }

    const metrics = resolvePaginationMetrics(ctx, options);
    if (metrics.options.layoutMode !== "print" || metrics.pageGap <= 0) {
        return { decorations: DecorationSet.empty, gapCount: 0, pageCount: 1 };
    }

    const decorations = [];
    const breaks = [];
    const tallBreaks = [];
    const prevBreakMap = editor?.__pageGapState?.breakMap;
    const nextBreakMap = new Map();
    let lastPos = -1;
    let warned = false;
    const scrollContainer = findScrollContainer(ctx.viewport);
    const scrollTop = scrollContainer === window ? window.scrollY : scrollContainer.scrollTop;
    const band = metrics.band;
    const printableHeight = metrics.contentHeight;
    const contentStartOffset = metrics.padY;

    if (window.__writer_pagination_debug?.enabled && !editor.__pageGapState?.loggedCoords) {
        const samplePos = Math.min(2, view.state.doc.content.size);
        try {
            const sampleCoords = view.coordsAtPos(samplePos);
            const laneRect = ctx.lane?.getBoundingClientRect?.();
            console.debug("[pagination] coords sample", {
                pos: samplePos,
                coordsTop: sampleCoords?.top,
                laneTop: laneRect?.top ?? 0
            });
        } catch {
        }

        if (!editor.__pageGapState) {
            editor.__pageGapState = { gapCount: 0, pageCount: 0, pass: 0 };
        }
        editor.__pageGapState.loggedCoords = true;
    }

    // Quantized measurements + asymmetric hysteresis to avoid subpixel jitter and oscillation.
    const INSERT_EPS_PX = 6;
    const REMOVE_EPS_PX = 18;
    const MIN_GAP_PX = 2;

    const root = view.dom;
    if (root && !root.style.position) {
        root.style.position = "relative";
    }

    const blockElements = root
        ? Array.from(root.children).filter(element => {
            if (!(element instanceof HTMLElement)) {
                return false;
            }
            if (element.classList.contains("wa-page-gap") || element.classList.contains("wa-pagination-debug")) {
                return false;
            }
            if (element.getAttribute("aria-hidden") === "true") {
                return false;
            }
            return true;
        })
        : [];

    const getTextblockRangeAtPos = (pos) => {
        const doc = view.state.doc;
        if (pos <= 0 || pos > doc.content.size) {
            return null;
        }

        const resolved = doc.resolve(pos);
        for (let depth = resolved.depth; depth >= 0; depth -= 1) {
            const node = resolved.node(depth);
            if (node?.isTextblock) {
                return {
                    from: resolved.start(depth),
                    to: resolved.end(depth)
                };
            }
        }

        return null;
    };

    const getLaneYAtPos = (pos) => {
        if (pos <= 0 || pos > view.state.doc.content.size) {
            return null;
        }

        try {
            const coords = view.coordsAtPos(pos);
            return toLaneLocalY(ctx, coords);
        } catch (error) {
            return null;
        }
    };

    const findSplitPos = (from, to, thresholdY) => {
        const startY = getLaneYAtPos(from);
        const endY = getLaneYAtPos(to);
        if (startY === null || endY === null) {
            return null;
        }

        if (endY < thresholdY - 0.5) {
            return null;
        }

        if (startY >= thresholdY) {
            return from;
        }

        let low = from;
        let high = to;
        while (low < high) {
            const mid = Math.floor((low + high) / 2);
            const midY = getLaneYAtPos(mid);
            if (midY === null) {
                return null;
            }

            if (midY >= thresholdY) {
                high = mid;
            } else {
                low = mid + 1;
            }
        }

        const finalY = getLaneYAtPos(low);
        if (finalY === null || finalY < thresholdY) {
            return null;
        }

        return low;
    };

    blockElements.forEach(element => {
        const top = Math.round(element.offsetTop);
        const bottom = Math.round(top + element.offsetHeight);
        const blockHeight = Math.max(0, bottom - top);
        let pos = view.posAtDOM(element, 0);
        pos = Math.max(1, Math.min(pos, view.state.doc.content.size));
        if (pos <= 0 || pos === lastPos) {
            return;
        }

        if (blockHeight <= printableHeight) {
            const yInBand = Math.round(((top % band) + band) % band);
            const yInContent = Math.round(yInBand - contentStartOffset);
            const pageIndex = Math.floor(Math.max(0, top) / band);
            const pageStart = pageIndex * band;
            const contentStart = pageStart + contentStartOffset;
            const contentEnd = Math.round(contentStart + printableHeight);
            const nextPageContentStart = Math.round(pageStart + band + contentStartOffset);

            // Sticky gaps: keep prior breaks unless they clearly fit by REMOVE_EPS_PX.
            const key = `tb:${pos}`;
            const prevGap = prevBreakMap?.get(key);
            const overflow = bottom - contentEnd;
            const shouldInsert = overflow > INSERT_EPS_PX;
            const shouldRemove = overflow < -REMOVE_EPS_PX;
            if (!prevGap && !shouldInsert) {
                return;
            }
            if (prevGap && shouldRemove) {
                return;
            }

            let spacerPx = Math.round(Math.max(0, nextPageContentStart - top));
            spacerPx = Math.max(0, Math.min(spacerPx, band));
            if (prevGap) {
                const prevHeight = prevGap.height ?? prevGap.spacerPx ?? 0;
                if (Math.abs(spacerPx - prevHeight) <= 2 || spacerPx < MIN_GAP_PX) {
                    spacerPx = prevHeight;
                }
            }

            if (spacerPx < MIN_GAP_PX) {
                return;
            }

            const gap = document.createElement("span");
            gap.className = "wa-page-gap";
            gap.setAttribute("aria-hidden", "true");
            gap.setAttribute("contenteditable", "false");
            gap.style.display = "block";
            gap.style.height = `${spacerPx}px`;
            gap.style.width = "100%";
            gap.style.pointerEvents = "none";
            gap.style.userSelect = "none";

            const deco = Decoration.widget(pos, gap, {
                key: `wa-gap:${pos}`,
                side: 0,
                stopEvent: () => false,
                ignoreSelection: true
            });
            decorations.push(deco);
            breaks.push({
                pos,
                blockTop: top,
                blockBottom: bottom,
                pageIndex,
                spacerPx,
                yInBand,
                yInContent,
                blockHeight
            });
            nextBreakMap.set(key, { height: spacerPx, pos, pageIndex });
            lastPos = pos;
            return;
        }

        const range = getTextblockRangeAtPos(pos);
        if (!range) {
            return;
        }

        if (!warned) {
            console.warn("[pagination] tall block; using coordsAtPos fallback for splits.");
            warned = true;
        }

        const scanFrom = Math.min(range.from, view.state.doc.content.size);
        const scanTo = Math.min(range.to, view.state.doc.content.size);
        if (scanTo <= scanFrom) {
            return;
        }

        const scanTop = getLaneYAtPos(scanFrom);
        if (scanTop === null) {
            return;
        }

        const pageIndex = Math.floor(Math.max(0, scanTop) / band);
        const pageStart = pageIndex * band;
        const contentStart = pageStart + contentStartOffset;
        const contentEnd = contentStart + printableHeight;
        const splitPos = findSplitPos(scanFrom, scanTo, contentEnd);
        if (splitPos === null || splitPos >= scanTo || splitPos === lastPos) {
            return;
        }

        const yAtSplit = getLaneYAtPos(splitPos);
        if (yAtSplit === null) {
            return;
        }

        const yInBand = Math.round(((yAtSplit % band) + band) % band);
        let spacerPx = (printableHeight - (yInBand - contentStartOffset)) + metrics.pageGap;
        spacerPx = Math.round(Math.max(0, Math.min(spacerPx, band)));
        if (spacerPx < MIN_GAP_PX) {
            return;
        }

        const gap = document.createElement("span");
        gap.className = "wa-page-gap";
        gap.setAttribute("aria-hidden", "true");
        gap.setAttribute("contenteditable", "false");
        gap.style.display = "block";
        gap.style.height = `${spacerPx}px`;
        gap.style.width = "100%";
        gap.style.pointerEvents = "none";
        gap.style.userSelect = "none";

        const deco = Decoration.widget(splitPos, gap, {
            key: `wa-gap:${splitPos}`,
            side: 0,
            stopEvent: () => false,
            ignoreSelection: true
        });
        decorations.push(deco);
        tallBreaks.push({
            pos: splitPos,
            yAtSplit,
            pageIndex,
            spacerPx
        });
        lastPos = splitPos;
    });

    const rawHeight = ctx.view.scrollHeight || 0;
    const pageCount = Math.max(1, Math.ceil((rawHeight + metrics.pageGap) / band));
    const signature = [...breaks, ...tallBreaks]
        .map(entry => `${entry.pageIndex}:${entry.pos}:${Math.round(entry.spacerPx)}`)
        .join("|");
    if (!window.__writer_pagination_debug) {
        window.__writer_pagination_debug = { enabled: false };
    }
    window.__writer_pagination_debug.last = {
        metrics,
        breaks,
        tallBreaks,
        pageCount,
        scrollTop,
        laneTop: ctx.lane?.getBoundingClientRect?.().top ?? 0
    };

    return {
        decorations: DecorationSet.create(view.state.doc, decorations),
        gapCount: Math.max(0, breaks.length + tallBreaks.length),
        pageCount,
        breaks,
        tallBreaks,
        metrics,
        ctx,
        signature,
        scrollTop,
        breakMap: nextBreakMap,
        insertEpsPx: INSERT_EPS_PX,
        removeEpsPx: REMOVE_EPS_PX,
        minGapPx: MIN_GAP_PX
    };
}

function updatePageGapDecorations(editor) {
    if (!editor?.view) {
        return null;
    }

    const info = buildPageGapDecorations(editor, editor.__pageBreakState?.options);
    if (!editor.__pageGapState) {
        editor.__pageGapState = { gapCount: 0, pageCount: 0, pass: 0 };
    }

    const now = Date.now();
    if (editor.__pageGapState.freezeUntil && now < editor.__pageGapState.freezeUntil) {
        return editor.__pageGapState.lastGapInfo ?? info;
    }

    const MAX_PASSES = 8;
    const signatureChanged = !!info?.signature && editor.__pageGapState.lastSignature !== info.signature;
    if (signatureChanged) {
        const sigHistory = [...(editor.__pageGapState.sigHistory ?? []), info.signature].slice(-8);
        const lastFour = sigHistory.slice(-4);
        editor.__pageGapState.sigHistory = sigHistory;
        if (lastFour.length === 4 && lastFour[0] === lastFour[2] && lastFour[1] === lastFour[3]) {
            if (!editor.__pageGapState.oscillationWarned) {
                editor.__pageGapState.oscillationWarned = true;
                console.warn("[pagination] detected oscillation; freezing signature");
            }
            editor.__pageGapState.freezeUntil = now + 300;
            return editor.__pageGapState.lastGapInfo ?? info;
        }

        editor.__pageGapState.lastSignature = info.signature;
        editor.__pageGapState.pass = (editor.__pageGapState.pass ?? 0) + 1;
        console.info(`[pagination] pass ${editor.__pageGapState.pass} signature=${info.signature} gaps=${info.gapCount}`);
        if (editor.__pageGapState.pass < MAX_PASSES) {
            if (!editor.__pageGapState.pendingReflow) {
                editor.__pageGapState.pendingReflow = true;
                requestAnimationFrame(() => {
                    editor.__pageGapState.pendingReflow = false;
                    schedulePageBreakUpdate(editor);
                });
            }
        } else if (!editor.__pageGapState.warned) {
            editor.__pageGapState.warned = true;
            const lastFour = (editor.__pageGapState.sigHistory ?? []).slice(-4);
            const sampleBreaks = (info.breaks ?? []).slice(0, 3).map(entry => ({
                pos: entry.pos,
                spacerPx: Math.round(entry.spacerPx),
                blockTop: Math.round(entry.blockTop)
            }));
            console.warn(`[pagination] spacer layout did not converge after ${MAX_PASSES} passes.`, {
                lastSignatures: lastFour,
                gapCount: info.gapCount,
                sampleBreaks
            });
        }
    } else {
        editor.__pageGapState.pass = 0;
    }

    editor.__pageGapState = {
        gapCount: info.gapCount,
        pageCount: info.pageCount,
        lastSignature: editor.__pageGapState.lastSignature,
        pass: editor.__pageGapState.pass,
        pendingReflow: editor.__pageGapState.pendingReflow,
        warned: editor.__pageGapState.warned,
        breakMap: info.breakMap ?? editor.__pageGapState.breakMap,
        sigHistory: editor.__pageGapState.sigHistory,
        freezeUntil: editor.__pageGapState.freezeUntil,
        oscillationWarned: editor.__pageGapState.oscillationWarned,
        lastGapInfo: info
    };

    const tr = editor.view.state.tr.setMeta(pageGapDecorationsKey, info.decorations);
    tr.setMeta(WA_LAYOUT_META, true);
    editor.__paginationApplying = true;
    try {
        editor.view.dispatch(tr);
    } finally {
        editor.__paginationApplying = false;
    }
    return info;
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

function notifyPageBreakStatus(editor, reason) {
    if (!editor || !editor.__pageBreakState) {
        return;
    }

    const info = computePageBreaks(editor, editor.__pageBreakState.options);
    const count = renderPageBreakOverlay(editor, editor.__pageBreakState.options);
    const gapInfo = reason === "scroll" ? null : updatePageGapDecorations(editor);
    if (info?.ctx) {
        const fallbackGapInfo = gapInfo ?? editor.__pageGapState?.lastGapInfo;
        const debugMeta = {
            insertEpsPx: gapInfo?.insertEpsPx ?? fallbackGapInfo?.insertEpsPx,
            removeEpsPx: gapInfo?.removeEpsPx ?? fallbackGapInfo?.removeEpsPx,
            reason: reason ?? "update",
            lastLayoutTx: !!editor.__waLastWasLayoutTx,
            headingNumbers: editor.__headingNumberingEnabled !== false
        };
        renderPaginationDebug(info.ctx, info, fallbackGapInfo, debugMeta);
    }
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

function schedulePageBreakUpdate(editor, reason) {
    if (!editor) {
        return;
    }
    if (editor.__paginationApplying) {
        return;
    }
    if (editor.__waLastWasLayoutTx) {
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
        notifyPageBreakStatus(editor, reason);
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
                plainIndex += 1;
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

function hasExtension(editor, name) {
    const extensions = editor?.extensionManager?.extensions ?? [];
    return extensions.some(extension => extension?.name === name);
}

window.tiptapEditor = {
    create: function (elementId, initialContent, dotNetRef) {
        const interopState = createInteropState(dotNetRef);
        console.info("[tiptap] mask pagination disabled:", window.__writer_disable_mask_pagination);
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

        let editor;
        try {
            console.info("[tiptap] create editor", { elementId });
            console.info("[pagination] real pagination enabled");
            debugHeading("INIT", { debug: isWriterDebugEnabled() });
            const extensions = [
                StarterKit,
                TextStyleWithFontSize,
                TextAlign.configure({ types: ["heading", "paragraph"] }),
                Link.configure({ openOnClick: false }),
                IndentExtension,
                AiDecorationsExtension,
                PageGapDecorationsExtension,
                HeadingNumberingExtension,
                ShortcutExtension
            ];
            editor = new Editor({
                element: document.getElementById(elementId),
                extensions,
            content: initialContent,
            editorProps: {
                attributes: {
                    class: "ProseMirror tiptap-content",
                    spellcheck: "true",
                    style: "white-space: pre-wrap;"
                }
            },
            onTransaction({ editor, transaction }) {
                if (transaction?.getMeta?.(WA_LAYOUT_META)) {
                    editor.__waLastWasLayoutTx = true;
                    requestAnimationFrame(() => {
                        editor.__waLastWasLayoutTx = false;
                    });
                    return;
                }

                if ((transaction?.docChanged || transaction?.getMeta?.(WA_HEADING_NUMBERING_REBUILD))
                    && !editor.__headingNumberingApplying) {
                    const reason = transaction?.docChanged ? "docChanged" : "forceRebuild";
                    requestHeadingNumberingRebuild(editor, reason);
                }
            },
            onUpdate({ editor }) {
                if (editor.__waLastWasLayoutTx) {
                    return;
                }
                safeInvoke(dotNetRef, interopState, "OnEditorContentChanged", editor.getHTML());
                schedulePageBreakUpdate(editor);
            }
        });
        } catch (error) {
            console.error("[tiptap] editor creation failed", error);
            throw error;
        }

        console.log("[pagination] extensions:", editor.extensionManager.extensions.map(extension => extension.name));

        try {
            const emptySet = DecorationSet.empty;
            console.info("[tiptap] prosemirror diagnostics", {
                decorationSetType: DecorationSet?.name,
                decorationSetHasLocalsInner: typeof DecorationSet?.prototype?.localsInner === "function",
                emptyDecorationSetType: emptySet?.constructor?.name,
                emptyHasLocalsInner: typeof emptySet?.localsInner === "function",
                viewConstructor: editor?.view?.constructor?.name,
                viewHasDocView: !!editor?.view?.docView
            });
        } catch {
        }

        editor.__interopState = interopState;
        if (editor.__headingNumberingEnabled === undefined) {
            editor.__headingNumberingEnabled = true;
        }
        if (!Array.isArray(editor.__headingNumberingPrefix)) {
            editor.__headingNumberingPrefix = [0, 0, 0, 0, 0, 0, 0];
        }
        if (!editor.__headingNumberingScope) {
            editor.__headingNumberingScope = "document";
        }
        requestHeadingNumberingRebuild(editor, "init");

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
        const traceId = editor?.__headingNumberingTraceId;
        const pageId = editor?.__headingNumberingPageId;
        const contentLength = typeof content === "string" ? content.length : 0;
        const contentHash = typeof content === "string" ? hashStringFNV1a(content) : "0";
        debugHeading("SET_CONTENT_START", { traceId, pageId, contentLength, contentHash });
        editor.commands.setContent(content, false);
        const summary = getHeadingDocSummary(editor);
        debugHeading("SET_CONTENT_DONE", { traceId, pageId, summary });
        if (summary) {
            debugHeading("DOC_SUMMARY", {
                traceId,
                pageId,
                elementId: editor?.view?.dom?.id,
                ...summary
            });
        }
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

        const shouldUseGaps = editor.__pageBreakState.options.layoutMode === "print"
            && editor.__pageBreakState.options.pageGapPx > 0;
        if (shouldUseGaps && !hasExtension(editor, "pageGapDecorations")) {
            console.error("[pagination] PageGapDecorationsExtension missing — gaps will not render");
        }

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

    setHeadingNumberingEnabled: function (editor, enabled) {
        if (!editor) {
            return;
        }

        editor.__headingNumberingEnabled = enabled !== false;
        debugHeading("TOGGLE", { traceId: editor.__headingNumberingTraceId, enabled: editor.__headingNumberingEnabled });
        requestHeadingNumberingRebuild(editor, "toggle");
    },

    setHeadingNumberingPrefix: function (editor, counters) {
        if (!editor) {
            return;
        }

        const prefix = Array.isArray(counters) ? counters : [];
        const normalized = [0, 0, 0, 0, 0, 0, 0];
        for (let index = 1; index <= 6; index += 1) {
            const value = Number(prefix[index]) || 0;
            normalized[index] = Math.max(0, value);
        }
        editor.__headingNumberingPrefix = normalized;
        debugHeading("PREFIX_SET", { traceId: editor.__headingNumberingTraceId, counters: normalized.slice(1) });
        requestHeadingNumberingRebuild(editor, "prefix");
    },

    setHeadingNumberingContext: function (editor, context) {
        if (!editor) {
            return;
        }

        const enabled = context?.enabled !== false;
        editor.__headingNumberingEnabled = enabled;
        editor.__headingNumberingTraceId = context?.traceId;
        editor.__headingNumberingPageId = context?.pageId;
        if (context?.scope) {
            editor.__headingNumberingScope = context.scope;
        }
        if (Array.isArray(context?.prefixCounters)) {
            const normalized = [0, 0, 0, 0, 0, 0, 0];
            for (let index = 1; index <= 6; index += 1) {
                const value = Number(context.prefixCounters[index]) || 0;
                normalized[index] = Math.max(0, value);
            }
            editor.__headingNumberingPrefix = normalized;
        }

        debugHeading("CONTEXT_SET", {
            traceId: editor.__headingNumberingTraceId,
            enabled: editor.__headingNumberingEnabled,
            scope: editor.__headingNumberingScope ?? "document",
            counters: editor.__headingNumberingPrefix?.slice(1),
            pageId: editor.__headingNumberingPageId
        });
        requestHeadingNumberingRebuild(editor, "context");
    },

    setHeadingNumberingTraceId: function (editor, traceId) {
        if (!editor) {
            return;
        }

        editor.__headingNumberingTraceId = traceId;
        debugHeading("TRACE_SET", { traceId });
    },

    forceHeadingNumberingRebuild: function (editor, traceId, reason) {
        if (!editor) {
            return;
        }

        editor.__headingNumberingTraceId = traceId ?? editor.__headingNumberingTraceId;
        debugHeading("FORCE_REBUILD", { traceId: editor.__headingNumberingTraceId, reason });
        const tr = editor.view.state.tr.setMeta(WA_HEADING_NUMBERING_REBUILD, true);
        editor.view.dispatch(tr);
    },

    debugLog: function (stage, payload) {
        debugHeading(stage, payload ?? {});
    },

    saveDebugPageBackup: function (pageId, content, hash) {
        if (!isWriterDebugEnabled()) {
            return;
        }
        try {
            const key = `writerapp.pagebackup.${pageId}`;
            const payload = {
                pageId,
                hash,
                content,
                savedAt: new Date().toISOString()
            };
            window.localStorage.setItem(key, JSON.stringify(payload));
            debugHeading("BACKUP_SAVED", { pageId, hash });
        } catch {
        }
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

        const shouldUseGaps = editor.__pageBreakState.options.layoutMode === "print"
            && editor.__pageBreakState.options.pageGapPx > 0;
        if (shouldUseGaps && !hasExtension(editor, "pageGapDecorations")) {
            console.error("[pagination] PageGapDecorationsExtension missing — gaps will not render");
        }

        if (!editor.__pageBreakState.scrollHandler) {
            const ctx = getPageBreakContext(editor);
            const scrollContainer = ctx ? findScrollContainer(ctx.viewport) : window;
            const handler = () => schedulePageBreakUpdate(editor, "scroll");
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

console.log('tiptap bundle loaded', !!(window as any).tiptapEditor);

