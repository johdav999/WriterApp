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

  if (!api.setQualityIssues) {
    api.setQualityIssues = function () {
      return;
    };
  }

  if (!api.clearQualityIssues) {
    api.clearQualityIssues = function () {
      return;
    };
  }

  const getEditorContext = (editor) => {
    const view = editor?.view?.dom;
    if (!view) {
      return null;
    }

    const viewport = view.closest(".editor-viewport");
    if (!viewport) {
      return null;
    }

    const lane = view.closest(".page-lane");
    const content = view.closest(".editor-content") || view;
    const canvas = view.closest(".editor-canvas") || content || view;
    const overlayHost = lane || canvas || content || viewport;
    return { view, viewport, content, overlayHost, lane };
  };

  const maybeDebugEditorLayout = (editor) => {
    if (typeof window === "undefined" || !window.__wa_editor_debug || editor?.__waDebugLayoutLogged) {
      return;
    }

    const ctx = getEditorContext(editor);
    if (!ctx) {
      return;
    }

    editor.__waDebugLayoutLogged = true;

    const host = ctx.view.closest("[id^='section-editor-']");
    const shell = ctx.view.closest(".editor-shell");
    const main = ctx.view.closest(".editor-main");
    const surface = ctx.view.closest(".editor-surface");
    const statusBar = ctx.view.closest(".editor-surface")?.querySelector?.(".editor-status-bar");
    const overlay = ctx.overlayHost?.querySelector?.(".pagebreak-overlay");

    const targets = [
      { label: "shell", el: shell, color: "#2563eb" },
      { label: "main", el: main, color: "#0ea5e9" },
      { label: "surface", el: surface, color: "#16a34a" },
      { label: "viewport", el: ctx.viewport, color: "#14b8a6" },
      { label: "content", el: ctx.content, color: "#8b5cf6" },
      { label: "canvas", el: ctx.view.closest(".editor-canvas"), color: "#ec4899" },
      { label: "lane", el: ctx.lane, color: "#f97316" },
      { label: "overlay", el: overlay, color: "#ef4444" },
      { label: "section-host", el: host, color: "#f59e0b" },
      { label: "prosemirror", el: ctx.view, color: "#22c55e" },
      { label: "status-bar", el: statusBar, color: "#64748b" }
    ];

    targets.forEach((target) => {
      if (!target.el) {
        return;
      }
      target.el.style.outline = `2px dashed ${target.color}`;
      target.el.style.outlineOffset = "-2px";
    });

    const diagnostics = targets
      .filter((target) => target.el)
      .map((target) => {
        const style = window.getComputedStyle(target.el);
        return {
          label: target.label,
          zIndex: style.zIndex,
          overflow: `${style.overflowX}/${style.overflowY}`,
          position: style.position,
          size: `${target.el.clientWidth}x${target.el.clientHeight}`
        };
      });

    console.info("[editor-debug] layout diagnostics", diagnostics);
  };

  if (!api.__waDebugWrapped && typeof api.create === "function") {
    const originalCreate = api.create.bind(api);
    api.create = function (...args) {
      const editor = originalCreate(...args);
      maybeDebugEditorLayout(editor);
      return editor;
    };
    api.__waDebugWrapped = true;
  }
})();
