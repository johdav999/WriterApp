const registrations = new Map();

export function registerEditorSaveEvents(key, dotNetRef) {
  unregisterEditorSaveEvents(key);
  const pending = new Set();
  let registration;
  const invoke = method => {
    if (pending.has(method)) return;
    pending.add(method);
    // A blur can occur while Blazor changes focus during a render batch. Defer interop
    // until that batch releases the WASM heap, and never call a replaced/disposed editor.
    queueMicrotask(() => {
      if (registrations.get(key) !== registration) { pending.delete(method); return; }
      Promise.resolve().then(() => registrations.get(key) === registration ? dotNetRef.invokeMethodAsync(method) : undefined)
        .catch(error => console.warn('Editor lifecycle save was unavailable.', error))
        .finally(() => pending.delete(method));
    });
  };

  const onWindowBlur = event => {
    if (event.target === window) invoke("OnWindowBlurred");
  };

  const onVisibilityChange = () => {
    if (document.visibilityState === "hidden") {
      invoke("OnDocumentHidden");
    }
  };

  const onPageHide = () => {
    invoke("OnPageHide");
  };

  window.addEventListener("blur", onWindowBlur);
  document.addEventListener("visibilitychange", onVisibilityChange, true);
  window.addEventListener("pagehide", onPageHide, true);

  registration = {
    onWindowBlur,
    onVisibilityChange,
    onPageHide
  };
  registrations.set(key, registration);
}

export function unregisterEditorSaveEvents(key) {
  const registration = registrations.get(key);
  if (!registration) {
    return;
  }

  window.removeEventListener("blur", registration.onWindowBlur);
  document.removeEventListener("visibilitychange", registration.onVisibilityChange, true);
  window.removeEventListener("pagehide", registration.onPageHide, true);
  registrations.delete(key);
}
