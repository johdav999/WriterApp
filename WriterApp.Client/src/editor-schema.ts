import StarterKit from "@tiptap/starter-kit";
import TextAlign from "@tiptap/extension-text-align";
import Link from "@tiptap/extension-link";
import { OrderedList } from "@tiptap/extension-list";
import { Table, TableRow, TableHeader, TableCell } from "@tiptap/extension-table";
import { EditorImage, IndentExtension } from "./editor-rich-extensions";

const ContractLink = Link.extend({ addAttributes() {
    const attributes = { ...this.parent?.() };
    // Tiptap 3.31 adds title; contract v2 does not permit it. Refuse title in source validation.
    delete attributes.title;
    return attributes;
} }).configure({ openOnClick: false, autolink: false, linkOnPaste: false });
const ContractOrderedList = OrderedList.extend({ addAttributes() {
    const attributes = { ...this.parent?.() };
    delete attributes.type;
    return attributes;
} });
const alignmentAttributes = { align: { default: null,
    parseHTML: (element: HTMLElement) => element.getAttribute('align') || element.style.textAlign || null,
    renderHTML: (attributes: any) => attributes.align ? { align: attributes.align } : {} } };
const ContractTableCell = TableCell.extend({ addAttributes() { return { ...this.parent?.(), ...alignmentAttributes }; } });
const ContractTableHeader = TableHeader.extend({ addAttributes() { return { ...this.parent?.(), ...alignmentAttributes }; } });

/** The contract-v2 content schema. Presentation-only web extensions stay outside shared content. */
export const richExtensions = (collaborative = false) => [
    StarterKit.configure({ undoRedo: collaborative ? false : {},
        link: false, orderedList: false, trailingNode: false }), ContractLink, ContractOrderedList,
    TextAlign.configure({ types: ["heading", "paragraph"] }), EditorImage, IndentExtension,
    Table.configure({ resizable: true, allowTableNodeSelection: true }), TableRow, ContractTableHeader, ContractTableCell
];
