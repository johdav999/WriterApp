const registrations = new Map();

export function registerEditorSaveEvents(key, dotNetRef) {
  unregisterEditorSaveEvents(key);

  const onWindowBlur = () => {
    dotNetRef.invokeMethodAsync("OnWindowBlurred");
  };

  const onVisibilityChange = () => {
    if (document.visibilityState === "hidden") {
      dotNetRef.invokeMethodAsync("OnDocumentHidden");
    }
  };

  const onPageHide = () => {
    dotNetRef.invokeMethodAsync("OnPageHide");
  };

  window.addEventListener("blur", onWindowBlur, true);
  document.addEventListener("visibilitychange", onVisibilityChange, true);
  window.addEventListener("pagehide", onPageHide, true);

  registrations.set(key, {
    onWindowBlur,
    onVisibilityChange,
    onPageHide
  });
}

export function unregisterEditorSaveEvents(key) {
  const registration = registrations.get(key);
  if (!registration) {
    return;
  }

  window.removeEventListener("blur", registration.onWindowBlur, true);
  document.removeEventListener("visibilitychange", registration.onVisibilityChange, true);
  window.removeEventListener("pagehide", registration.onPageHide, true);
  registrations.delete(key);
}
