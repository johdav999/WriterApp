// Bring the response into the mobile tools viewport without changing editor focus.
export function reveal(element) {
    const target = element?.firstElementChild;
    if (target) target.scrollIntoView({ block: "nearest", behavior: "instant" });
}
