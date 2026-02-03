(function () {
  const api = window.tiptapEditor;
  if (!api) {
    return;
  }

  const createInteropState = (dotNetRef) => ({ enabled: !!dotNetRef });
  const safeInvoke = (dotNetRef, interopState, method, ...args) => {
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
    } catch {
      interopState.enabled = false;
    }
  };

  if (!api.getSelectionDocRange) {
    api.getSelectionDocRange = function (editor) {
      if (!editor?.state?.selection) {
        return null;
      }

      const { from, to } = editor.state.selection;
      return { from, to };
    };
  }

  if (!api.getSelectionText) {
    api.getSelectionText = function (editor) {
      if (!editor?.state?.selection) {
        return "";
      }

      const { from, to } = editor.state.selection;
      return editor.state.doc.textBetween(from, to, " ", " ");
    };
  }

  if (!api.scrollToElement) {
    api.scrollToElement = function (elementId) {
      if (!elementId) {
        return;
      }

      const element = document.getElementById(elementId);
      if (!element) {
        return;
      }

      element.scrollIntoView({ behavior: "smooth", block: "center" });
    };
  }

  if (!api.attachAnnotationClicks) {
    api.attachAnnotationClicks = function (editor, dotNetRef) {
      if (!editor?.view?.dom || !dotNetRef) {
        return;
      }

      const root = editor.view.dom;
      if (root.__annotationClickHandler) {
        root.removeEventListener("click", root.__annotationClickHandler);
      }

      const interopState = createInteropState(dotNetRef);
      const handler = (event) => {
        const target = event?.target instanceof HTMLElement ? event.target : null;
        if (!target) {
          return;
        }

        const element = target.closest?.("[data-annotation-id]");
        if (!element) {
          return;
        }

        const id = element.getAttribute("data-annotation-id");
        if (!id) {
          return;
        }

        safeInvoke(dotNetRef, interopState, "OnAnnotationClicked", id);
      };

      root.addEventListener("click", handler);
      root.__annotationClickHandler = handler;
      root.__annotationClickInteropState = interopState;
    };
  }

  if (!api.detachAnnotationClicks) {
    api.detachAnnotationClicks = function (editor) {
      const root = editor?.view?.dom;
      if (!root) {
        return;
      }

      if (root.__annotationClickHandler) {
        root.removeEventListener("click", root.__annotationClickHandler);
      }

      if (root.__annotationClickInteropState) {
        root.__annotationClickInteropState.enabled = false;
      }

      root.__annotationClickHandler = null;
      root.__annotationClickInteropState = null;
    };
  }

  if (!api.__patchedGetAnnotations) {
    const originalGetAnnotations = api.getAnnotations;
    api.getAnnotations = function (editor) {
      const items = typeof originalGetAnnotations === "function" ? originalGetAnnotations(editor) : [];
      const doc = editor?.view?.state?.doc;
      if (!doc || !Array.isArray(items)) {
        return items;
      }

      return items.map((item) => {
        if (item && typeof item.text === "string") {
          return item;
        }
        if (!item || !Number.isFinite(item.from) || !Number.isFinite(item.to)) {
          return item;
        }
        return {
          ...item,
          text: doc.textBetween(item.from, item.to, " ", " ")
        };
      });
    };
    api.__patchedGetAnnotations = true;
  }

  if (!api.setAnnotations) {
    api.setAnnotations = function (editor, items) {
      if (!editor || !editor.view) {
        return;
      }

      const normalized = Array.isArray(items) ? items : [];
      const tr = editor.view.state.tr.setMeta("annotationDecorations", {
        decorations: editor.view.state.tr.doc ? null : null,
        items: normalized
      });
      editor.view.dispatch(tr);
    };
  }

  if (!api.clearAnnotations) {
    api.clearAnnotations = function (editor) {
      if (!editor || !editor.view) {
        return;
      }

      const tr = editor.view.state.tr.setMeta("annotationDecorations", {
        decorations: null,
        items: []
      });
      editor.view.dispatch(tr);
    };
  }
})();
