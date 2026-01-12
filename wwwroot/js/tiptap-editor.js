import { Editor, Extension } from "https://esm.sh/@tiptap/core@2.1.13?bundle";
import StarterKit from "https://esm.sh/@tiptap/starter-kit@2.1.13?bundle";
import TextStyle from "https://esm.sh/@tiptap/extension-text-style@2.1.13?bundle";
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
    toggleOrderedList,
    setFontSize
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

const fontSizePresets = [12, 14, 16, 18, 24, 32];

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

function parseFontSize(value) {
    if (value === null || value === undefined) {
        return null;
    }

    const match = String(value).match(/(\d+(\.\d+)?)/);
    if (!match) {
        return null;
    }

    const parsed = Number(match[1]);
    return Number.isFinite(parsed) ? parsed : null;
}

function adjustFontSize(editor, direction) {
    const attributes = editor.getAttributes("textStyle") ?? {};
    const currentSize = parseFontSize(attributes.fontSize) ?? 16;
    let index = fontSizePresets.indexOf(currentSize);
    if (index === -1) {
        index = fontSizePresets.indexOf(16);
    }

    if (direction > 0 && index < fontSizePresets.length - 1) {
        index += 1;
    } else if (direction < 0 && index > 0) {
        index -= 1;
    }

    setFontSize(editor, fontSizePresets[index]);
}

function focusFontFamilySelect() {
    const select = document.getElementById("fontFamilySelect");
    if (select) {
        select.focus();
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
        blockType: getBlockType(editor),
        fontFamily: fontFamilyResult.mixed ? null : (fontFamilyResult.value ?? ""),
        fontSize: fontSizeResult.mixed ? null : normalizeFontSize(fontSizeResult.value)
    };
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
                    "Mod-Shift-.": () => {
                        adjustFontSize(this.editor, 1);
                        return true;
                    },
                    "Mod-Shift-,": () => {
                        adjustFontSize(this.editor, -1);
                        return true;
                    },
                    "Mod-Shift-f": () => {
                        focusFontFamilySelect();
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
            }
        });

        editor.__interopState = interopState;

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

    destroy: function (editor) {
        if (editor && editor.__interopState) {
            editor.__interopState.enabled = false;
        }
        editor.destroy();
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
