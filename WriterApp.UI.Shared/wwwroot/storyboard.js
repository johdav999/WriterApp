export function scrollToElement(id) {
    document.getElementById(id)?.scrollIntoView({ behavior: "smooth", block: "nearest", inline: "nearest" });
}
export function focusAndSelectInput(input) {
    input?.focus();
    input?.select();
}
