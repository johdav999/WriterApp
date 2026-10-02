import { Extension } from "@tiptap/core";
import Image from "@tiptap/extension-image";

export const EditorImage = Image.extend({
    addAttributes() {
        return {
            src: { default: null },
            alt: { default: null },
            title: { default: null },
            width: {
                default: null,
                parseHTML: element => element.getAttribute("width") || null,
                renderHTML: attributes => {
                    if (!attributes.width) {
                        return {};
                    }

                    return { width: String(attributes.width) };
                }
            },
            assetUrl: {
                default: null,
                parseHTML: element => element.getAttribute("data-asset-url") || null,
                renderHTML: attributes => attributes.assetUrl ? { "data-asset-url": String(attributes.assetUrl) } : {}
            },
            assetId: {
                default: null,
                parseHTML: element => element.getAttribute("data-asset-id") || null,
                renderHTML: attributes => attributes.assetId ? { "data-asset-id": String(attributes.assetId) } : {}
            }
        };
    }
}).configure({
    inline: false,
    allowBase64: true
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

export const IndentExtension = Extension.create({
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

