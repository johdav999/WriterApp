const handlers = new WeakMap();
export function attach(list) {
    const handler = event => {
        if (event.target.getAttribute('role') === 'tab' &&
            ['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) event.preventDefault();
    };
    list.addEventListener('keydown', handler);
    handlers.set(list, handler);
}
export function detach(list) {
    const handler = handlers.get(list);
    if (handler) list.removeEventListener('keydown', handler);
    handlers.delete(list);
}
