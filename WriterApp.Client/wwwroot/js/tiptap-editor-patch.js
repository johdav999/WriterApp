(function () {
  const api = window.tiptapEditor;
  if (!api) {
    return;
  }
  console.info("[tiptap-patch] loaded", { version: 11 });

  const getIssueKey = (issue) => issue?.issueKey ?? issue?.id ?? null;

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

  const isTableSelectionDebugEnabled = () => {
    try {
      return window?.localStorage?.getItem("writerapp.tableSelectionDebug") === "true"
        || window?.localStorage?.getItem("writerapp.debug") === "true";
    } catch {
      return false;
    }
  };

  const getSelectionTypeName = (selection) => selection?.constructor?.name
    || selection?.jsonID
    || selection?.type
    || typeof selection;

  const isCellSelection = (selection) => !!selection
    && (selection?.constructor?.name === "CellSelection"
      || !!selection?.$anchorCell
      || !!selection?.$headCell
      || !!selection?.anchorCell
      || !!selection?.headCell);

  const isSelectionInTable = (editor, selection = editor?.state?.selection) => {
    if (!editor || !selection) {
      return false;
    }

    if (isCellSelection(selection)) {
      return true;
    }

    return editor.isActive?.("table")
      || editor.isActive?.("tableCell")
      || editor.isActive?.("tableHeader");
  };

  const isTablePointerSelectionInProgress = (editor) => {
    const pointerState = editor?.__writerPointerState;
    return !!pointerState?.isPointerDown && !!pointerState?.startedInTable;
  };

  const debugTableSelection = (editor, reason, extra = null) => {
    if (!isTableSelectionDebugEnabled()) {
      return;
    }

    const selection = editor?.state?.selection;
    try {
      console.debug("[table-selection]", {
        reason,
        selectionType: getSelectionTypeName(selection),
        from: Number(selection?.from ?? -1),
        to: Number(selection?.to ?? -1),
        empty: !!selection?.empty,
        inTable: isSelectionInTable(editor, selection),
        cellSelection: isCellSelection(selection),
        anchorCell: !!selection?.$anchorCell || !!selection?.anchorCell,
        headCell: !!selection?.$headCell || !!selection?.headCell,
        pointerDownInTable: isTablePointerSelectionInProgress(editor),
        ...(extra || {})
      });
    } catch {
    }
  };

  const applyTextSelectionSafely = (editor, selectionOrPos, reason, options = null) => {
    if (!editor?.chain || !editor?.commands) {
      return false;
    }

    const force = options?.force === true;
    const preserveCellSelection = !force && (isCellSelection(editor?.state?.selection)
      || isTablePointerSelectionInProgress(editor));

    debugTableSelection(editor, "set-text-selection", {
      requested: selectionOrPos,
      source: reason,
      force,
      preservedCellSelection: preserveCellSelection
    });

    if (preserveCellSelection) {
      return false;
    }

    editor.commands.focus();
    try {
      editor.chain().focus().setTextSelection(selectionOrPos).run();
      return true;
    } catch {
      if (typeof selectionOrPos === "number") {
        editor.chain().focus().setTextSelection(selectionOrPos).run();
        return true;
      }
    }

    return false;
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

  if (!api.getPlainText) {
    api.getPlainText = function (editor) {
      if (!editor) {
        return "";
      }

      try {
        if (editor?.state?.doc) {
          const parts = [];
          let hasTextBlock = false;
          editor.state.doc.descendants((node, pos) => {
            if (!node.isTextblock) {
              return;
            }

            if (hasTextBlock) {
              parts.push("\n\n");
            }

            hasTextBlock = true;
            parts.push(editor.state.doc.textBetween(pos + 1, pos + node.nodeSize - 1, "", ""));
          });

          return parts.join("");
        }

        if (typeof editor.getText === "function") {
          return editor.getText({ blockSeparator: "\n\n" });
        }
      } catch {
      }

      return "";
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

  if (!api.scrollToAnnotation) {
    const clampPos = (value, min, max) => {
      const numeric = Number(value);
      if (!Number.isFinite(numeric)) {
        return min;
      }

      return Math.max(min, Math.min(max, Math.round(numeric)));
    };

    const escapeCssValue = (value) => {
      if (typeof CSS !== "undefined" && CSS && typeof CSS.escape === "function") {
        return CSS.escape(value);
      }

      return String(value).replace(/[^a-zA-Z0-9\-_]/g, "\\$&");
    };

    const flashAnnotationDom = (editor, annotationId) => {
      if (!editor?.view?.dom || !annotationId) {
        return;
      }

      const selector = `[data-annotation-id="${escapeCssValue(annotationId)}"]`;
      const nodes = Array.from(editor.view.dom.querySelectorAll(selector));
      if (nodes.length === 0) {
        return;
      }

      nodes.forEach((node) => node.classList.add("wa-annotation-flash"));
      window.setTimeout(() => {
        nodes.forEach((node) => node.classList.remove("wa-annotation-flash"));
      }, 900);
    };

    api.scrollToAnnotation = function (editor, payload) {
      if (!editor?.view?.state?.doc) {
        return false;
      }

      const annotationId = String(payload?.id ?? "");
      const docSize = Number(editor.view.state.doc.content?.size ?? 0);
      if (docSize <= 1) {
        return false;
      }

      let from = Number(payload?.from);
      let to = Number(payload?.to);
      const liveAnnotations = typeof api.getAnnotations === "function"
        ? api.getAnnotations(editor)
        : [];
      if (annotationId && Array.isArray(liveAnnotations)) {
        const current = liveAnnotations.find((item) => String(item?.id ?? "") === annotationId);
        if (current && Number.isFinite(current.from) && Number.isFinite(current.to)) {
          from = Number(current.from);
          to = Number(current.to);
        }
      }

      if (!Number.isFinite(from) || !Number.isFinite(to)) {
        return false;
      }

      const clampedFrom = clampPos(Math.min(from, to), 1, docSize);
      const clampedTo = clampPos(Math.max(from, to), clampedFrom, docSize);

      const applied = applyTextSelectionSafely(
        editor,
        { from: clampedFrom, to: clampedTo },
        "scrollToAnnotation");
      if (!applied) {
        return false;
      }

      if (editor.view?.state?.tr && editor.view?.dispatch) {
        editor.view.dispatch(editor.view.state.tr.scrollIntoView());
      }

      flashAnnotationDom(editor, annotationId);
      return true;
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

  if (!api.__patchedQualityIssueLocator) {
    const originalSetQualityIssues = typeof api.setQualityIssues === "function"
      ? api.setQualityIssues.bind(api)
      : null;

    const originalHighlightQualityIssue = typeof api.highlightQualityIssue === "function"
      ? api.highlightQualityIssue.bind(api)
      : null;

    const originalClearQualityIssueHighlight = typeof api.clearQualityIssueHighlight === "function"
      ? api.clearQualityIssueHighlight.bind(api)
      : null;

    const originalClearQualityIssues = typeof api.clearQualityIssues === "function"
      ? api.clearQualityIssues.bind(api)
      : null;

    const buildPlainTextSegments = (doc) => {
      const segments = [];
      let plainIndex = 0;
      let lastTextblock = false;

      doc.descendants((node, pos) => {
        if (node.isTextblock) {
          if (lastTextblock && plainIndex > 0) {
            plainIndex += 2;
          }
          lastTextblock = true;
        }

        if (node.isText && node.text) {
          const start = plainIndex;
          const end = plainIndex + node.text.length;
          segments.push({
            start,
            end,
            from: pos,
            to: pos + node.text.length
          });
          plainIndex = end;
        }
      });

      return segments;
    };

    const getEditorPlainText = (editor) => {
      const doc = editor?.state?.doc;
      if (!doc) {
        return "";
      }

      const segments = buildPlainTextSegments(doc);
      if (segments.length === 0) {
        return "";
      }

      const parts = [];
      let nextOffset = 0;
      for (let index = 0; index < segments.length; index += 1) {
        const segment = segments[index];
        if (segment.start > nextOffset) {
          parts.push("\n\n");
          nextOffset = segment.start;
        }

        const length = Math.max(0, segment.to - segment.from);
        if (length > 0) {
          parts.push(doc.textBetween(segment.from, segment.to, "", ""));
          nextOffset = segment.end;
        }
      }

      return parts.join("");
    };

    const mapPlainOffsetToDoc = (segments, offset) => {
      for (let index = 0; index < segments.length; index += 1) {
        const segment = segments[index];
        if (offset <= segment.end) {
          const delta = Math.max(0, offset - segment.start);
          return segment.from + delta;
        }
      }

      if (segments.length > 0) {
        return segments[segments.length - 1].to;
      }

      return null;
    };

    const normalizePlainRange = (from, to) => {
      const safeFrom = Math.max(0, Number(from) || 0);
      const safeTo = Math.max(0, Number(to) || safeFrom);
      return {
        from: Math.min(safeFrom, safeTo),
        to: Math.max(safeFrom, safeTo)
      };
    };

    const focusAndScrollRange = (editor, docFrom, docTo) => {
      if (!editor?.view) {
        return false;
      }

      const applied = applyTextSelectionSafely(
        editor,
        { from: docFrom, to: docTo },
        "focusAndScrollRange");
      if (!applied) {
        return false;
      }

      const tr = editor.state.tr.scrollIntoView();
      editor.view.dispatch(tr);
      return true;
    };

    const highlightByPlainRange = (editor, from, to) => {
      const normalized = normalizePlainRange(from, to);
      if (normalized.to <= normalized.from) {
        return false;
      }

      const segments = buildPlainTextSegments(editor.state.doc);
      const docFrom = mapPlainOffsetToDoc(segments, normalized.from);
      const docTo = mapPlainOffsetToDoc(segments, normalized.to);
      if (docFrom === null || docTo === null || docTo <= docFrom) {
        return false;
      }

      return focusAndScrollRange(editor, docFrom, docTo);
    };

    const stripOuterQuotes = (value) => {
      const trimmed = String(value || "").trim();
      if (trimmed.length < 2) {
        return "";
      }

      const first = trimmed[0];
      const last = trimmed[trimmed.length - 1];
      const quotePairs = new Set([
        "\"\"",
        "''",
        "``",
        "\u201c\u201d",
        "\u2018\u2019"
      ]);
      const pair = `${first}${last}`;
      if (!quotePairs.has(pair)) {
        return "";
      }

      return trimmed.slice(1, -1).trim();
    };

    const tryFindAnchorRange = (editor, anchorText) => {
      const doc = editor?.state?.doc;
      if (!doc) {
        return null;
      }

      const raw = String(anchorText || "").trim();
      if (!raw) {
        return null;
      }

      const candidates = [raw];
      const unquoted = stripOuterQuotes(raw);
      if (unquoted && !candidates.includes(unquoted)) {
        candidates.push(unquoted);
      }

      const fullText = getEditorPlainText(editor);
      for (let index = 0; index < candidates.length; index += 1) {
        const candidate = candidates[index];
        const foundAt = fullText.indexOf(candidate);
        if (foundAt < 0) {
          continue;
        }

        return {
          from: foundAt,
          to: foundAt + candidate.length
        };
      }

      return null;
    };

    const tryLocateIssue = (editor, issueId) => {
      const issues = editor?.__qualityIssueState?.issues;
      if (!Array.isArray(issues) || !issueId) {
        return null;
      }

      return issues.find((item) => String(getIssueKey(item) || "") === String(issueId)) || null;
    };

    const buildRenderableQualityIssues = (issues, activeIssueId) => {
      const active = activeIssueId ? String(activeIssueId) : null;
      return (Array.isArray(issues) ? issues : []).map((issue) => {
        const key = getIssueKey(issue);
        const isActive = active && key !== null && String(key) === active;
        if (!isActive) {
          return issue;
        }

        const baseKind = String(issue?.kind ?? "general");
        if (baseKind.includes("quality-issue--active")) {
          return issue;
        }

        return {
          ...issue,
          kind: `${baseKind} quality-issue--active`
        };
      });
    };

    const buildAnchorCandidates = (anchorText) => {
      const raw = String(anchorText || "").trim();
      if (!raw) {
        return [];
      }

      const candidates = [raw];
      const unquoted = stripOuterQuotes(raw);
      if (unquoted && !candidates.includes(unquoted)) {
        candidates.push(unquoted);
      }

      return candidates;
    };

    const findAnchorOccurrences = (plainText, anchorCandidates) => {
      const matches = [];
      if (!plainText || !Array.isArray(anchorCandidates) || anchorCandidates.length === 0) {
        return matches;
      }

      for (let i = 0; i < anchorCandidates.length; i += 1) {
        const candidate = anchorCandidates[i];
        if (!candidate) {
          continue;
        }

        let index = 0;
        while (index <= plainText.length - candidate.length) {
          const foundAt = plainText.indexOf(candidate, index);
          if (foundAt < 0) {
            break;
          }

          matches.push({
            from: foundAt,
            to: foundAt + candidate.length,
            text: candidate
          });
          index = foundAt + Math.max(1, candidate.length);
        }
      }

      return matches;
    };

    const findBestContextMatch = (plainText, beforeAnchor, needleText, afterAnchor, targetCenter) => {
      if (!plainText) {
        return null;
      }

      const normalizedNeedle = String(needleText || "").trim();
      if (!normalizedNeedle) {
        return null;
      }

      const matches = [];
      let index = 0;
      while (index <= plainText.length - normalizedNeedle.length) {
        const foundAt = plainText.indexOf(normalizedNeedle, index);
        if (foundAt < 0) {
          break;
        }

        const foundTo = foundAt + normalizedNeedle.length;
        const beforeWindow = plainText.slice(Math.max(0, foundAt - 120), foundAt);
        const afterWindow = plainText.slice(foundTo, Math.min(plainText.length, foundTo + 120));
        const beforeText = String(beforeAnchor || "").trim();
        const afterText = String(afterAnchor || "").trim();
        const beforeScore = beforeText && beforeWindow.includes(beforeText) ? 2 : 0;
        const afterScore = afterText && afterWindow.includes(afterText) ? 2 : 0;
        const center = (foundAt + foundTo) / 2;
        const distancePenalty = Math.abs(center - Number(targetCenter || 0)) / 1000;
        const score = beforeScore + afterScore - distancePenalty;

        matches.push({
          from: foundAt,
          to: foundTo,
          score
        });

        index = foundAt + Math.max(1, normalizedNeedle.length);
      }

      if (!matches.length) {
        return null;
      }

      return matches
        .slice()
        .sort((a, b) => b.score - a.score)[0];
    };

    api.setQualityIssues = function (editor, issues) {
      if (!editor) {
        return;
      }

      const normalized = (Array.isArray(issues) ? issues : []).map((issue) => {
        const issueKey = getIssueKey(issue);
        return {
          ...issue,
          issueKey,
          id: issueKey
        };
      });

      editor.__qualityIssueState = editor.__qualityIssueState || {};
      editor.__qualityIssueState.issues = normalized;
      editor.__qualityIssueState.activeIssueId = editor.__qualityIssueState.activeIssueId ?? null;

      if (editor.__qualityIssueState.activeIssueId) {
        const activeExists = normalized.some((item) => String(getIssueKey(item) || "") === String(editor.__qualityIssueState.activeIssueId));
        if (window.__waQualityDebug === true) {
          const sample = normalized.length > 0 ? normalized[0] : null;
          console.debug("[quality] setQualityIssues sample keys", sample ? Object.keys(sample) : []);
          console.debug("[quality] active match", {
            activeIssueId: editor.__qualityIssueState.activeIssueId,
            matched: activeExists
          });
        }

        if (!activeExists) {
          editor.__qualityIssueState.activeIssueId = null;
        }
      }

      if (originalSetQualityIssues) {
        originalSetQualityIssues(
          editor,
          buildRenderableQualityIssues(normalized, editor.__qualityIssueState.activeIssueId));
      }
    };

    api.setQualityIssuesActive = function (editor, issueId) {
      if (!editor) {
        return;
      }

      editor.__qualityIssueState = editor.__qualityIssueState || {};
      editor.__qualityIssueState.activeIssueId = issueId ? String(issueId) : null;

      if (window.__waQualityDebug === true) {
        console.debug("[quality] set active", { issueId: editor.__qualityIssueState.activeIssueId });
      }

      if (originalSetQualityIssues) {
        const issues = Array.isArray(editor.__qualityIssueState.issues)
          ? editor.__qualityIssueState.issues
          : [];
        originalSetQualityIssues(
          editor,
          buildRenderableQualityIssues(issues, editor.__qualityIssueState.activeIssueId));
      }
    };

    api.clearQualityIssues = function (editor) {
      if (!editor) {
        return;
      }

      editor.__qualityIssueState = editor.__qualityIssueState || {};
      editor.__qualityIssueState.issues = [];
      editor.__qualityIssueState.activeIssueId = null;
      editor.__qualityIssueState.manualHighlight = null;

      if (originalClearQualityIssues) {
        originalClearQualityIssues(editor);
        return;
      }

      if (originalSetQualityIssues) {
        originalSetQualityIssues(editor, []);
      }
    };

    api.highlightQualityIssue = function (editor, from, to, issueId, anchorText) {
      if (window.__waQualityDebug === true) {
        console.debug("[quality] highlight request", { issueId, from, to, anchorText });
      }

      editor.__qualityIssueState = editor.__qualityIssueState || {};
      editor.__qualityIssueState.activeIssueId = issueId ? String(issueId) : null;

      if (originalHighlightQualityIssue && originalHighlightQualityIssue(editor, from, to, issueId)) {
        if (window.__waQualityDebug === true) {
          console.debug("[quality] highlight resolved", { issueId, from, to, source: "original", decorationsRebuilt: true });
        }
        return true;
      }

      if (highlightByPlainRange(editor, from, to)) {
        if (window.__waQualityDebug === true) {
          console.debug("[quality] highlight resolved", { issueId, from, to, source: "plain-range", decorationsRebuilt: false });
        }
        return true;
      }

      const issue = tryLocateIssue(editor, issueId);
      if (issue) {
        if (originalHighlightQualityIssue && originalHighlightQualityIssue(editor, issue.from, issue.to, issueId)) {
          if (window.__waQualityDebug === true) {
            console.debug("[quality] highlight resolved", { issueId, from: issue.from, to: issue.to, source: "state-original", decorationsRebuilt: true });
          }
          return true;
        }

        if (highlightByPlainRange(editor, issue.from, issue.to)) {
          if (window.__waQualityDebug === true) {
            console.debug("[quality] highlight resolved", { issueId, from: issue.from, to: issue.to, source: "state-plain-range", decorationsRebuilt: false });
          }
          return true;
        }
      }

      const resolvedAnchor = anchorText || issue?.anchorText || "";
      const range = tryFindAnchorRange(editor, resolvedAnchor);
      if (!range) {
        return false;
      }

      if (originalHighlightQualityIssue && originalHighlightQualityIssue(editor, range.from, range.to, issueId)) {
        if (window.__waQualityDebug === true) {
          console.debug("[quality] highlight resolved", { issueId, from: range.from, to: range.to, source: "anchor-original", decorationsRebuilt: true });
        }
        return true;
      }

      const anchored = highlightByPlainRange(editor, range.from, range.to);
      if (window.__waQualityDebug === true) {
        console.debug("[quality] highlight resolved", { issueId, from: range.from, to: range.to, source: anchored ? "anchor-plain-range" : "anchor-failed", decorationsRebuilt: false });
      }
      return anchored;
    };

    api.scrollToQualityIssue = function (editor, issueId) {
      if (!editor || !issueId) {
        return false;
      }

      const issue = tryLocateIssue(editor, issueId);
      if (!issue) {
        return false;
      }

      const anchorText = issue.anchorText || issue.text || null;
      return api.highlightQualityIssue(editor, issue.from, issue.to, issueId, anchorText);
    };

    api.clearQualityIssueHighlight = function (editor, issueId) {
      if (!editor) {
        return;
      }

      editor.__qualityIssueState = editor.__qualityIssueState || {};
      const activeIssueId = editor.__qualityIssueState.activeIssueId
        ? String(editor.__qualityIssueState.activeIssueId)
        : null;
      const targetIssueId = issueId ? String(issueId) : null;

      if (!targetIssueId || activeIssueId === targetIssueId) {
        editor.__qualityIssueState.activeIssueId = null;
      }

      if (window.__waQualityDebug === true) {
        console.debug("[quality] clear highlight", {
          issueId: targetIssueId,
          activeBefore: activeIssueId,
          activeAfter: editor.__qualityIssueState.activeIssueId
        });
      }

      if (originalClearQualityIssueHighlight) {
        originalClearQualityIssueHighlight(editor, issueId);
        return;
      }

      if (editor?.view?.state?.selection) {
        const { from } = editor.view.state.selection;
        applyTextSelectionSafely(editor, from, "clearQualityIssueHighlight");
      }
    };

    const buildFixApplyResult = (applied, changed, reason, extra) => {
      const result = {
        applied: !!applied,
        changed: !!changed,
        reason: reason || null,
        ...(extra || {})
      };

      if (!result.applied || !result.changed) {
        console.warn("[quality] apply fix skipped", result);
      }

      return result;
    };

    const parseOptionalNumber = (value) => {
      if (value === null || value === undefined || value === "") {
        return null;
      }

      const parsed = Number(value);
      return Number.isFinite(parsed) ? parsed : null;
    };

    const resolveTargetRangeFromFix = (editor, fix) => {
      if (!editor?.view || !fix) {
        return { ok: false, reason: "invalid_editor_or_fix" };
      }

      const kind = String(fix.kind || "").toLowerCase();
      const rawFrom = Number(fix.from);
      const rawTo = Number(fix.to);
      const range = normalizePlainRange(rawFrom, rawTo);
      const issueKey = fix.issueKey ? String(fix.issueKey) : null;
      const anchorCandidates = buildAnchorCandidates(fix.anchorText);
      const hasAnchor = anchorCandidates.length > 0;

      if (!Number.isFinite(rawFrom) || !Number.isFinite(rawTo)) {
        return { ok: false, reason: "invalid_range_numbers", issueKey };
      }

      if ((kind === "replace" || kind === "delete") && range.to <= range.from) {
        return { ok: false, reason: "invalid_replace_or_delete_range", issueKey };
      }

      const plainText = getEditorPlainText(editor);
      const max = plainText.length;
      if (range.from > max || range.to > max) {
        return { ok: false, reason: "range_out_of_bounds", issueKey, max };
      }

      const expectedCenter = (range.from + range.to) / 2;
      const mappedRangeText = plainText.slice(range.from, range.to);
      let target = { from: range.from, to: range.to, source: "mapped-range" };
      let anchorMatchedAtRange = false;
      if (hasAnchor) {
        anchorMatchedAtRange = anchorCandidates.includes(mappedRangeText);
        if (!anchorMatchedAtRange) {
          const matches = findAnchorOccurrences(plainText, anchorCandidates);
          if (matches.length > 0) {
            const selected = matches
              .slice()
              .sort((a, b) => {
                const aCenter = (a.from + a.to) / 2;
                const bCenter = (b.from + b.to) / 2;
                return Math.abs(aCenter - expectedCenter) - Math.abs(bCenter - expectedCenter);
              })[0];

            target = { from: selected.from, to: selected.to, source: matches.length === 1 ? "anchor-single" : "anchor-nearest" };
          }
        }
      }

      if (!hasAnchor && target.to <= target.from && kind !== "insert") {
        return {
          ok: false,
          reason: "resolved_range_invalid",
          issueKey,
          from: target.from,
          to: target.to
        };
      }

      const segments = buildPlainTextSegments(editor.state.doc);
      const docFrom = mapPlainOffsetToDoc(segments, target.from);
      const docTo = mapPlainOffsetToDoc(segments, target.to);
      if (docFrom === null || docTo === null || (kind !== "insert" && docTo <= docFrom)) {
        return {
          ok: false,
          reason: "doc_mapping_failed",
          issueKey,
          from: target.from,
          to: target.to
        };
      }

      return {
        ok: true,
        kind,
        issueKey,
        plainText,
        target,
        docFrom,
        docTo,
        anchorMatchedAtRange,
        expectedText: plainText.slice(target.from, target.to)
      };
    };

    api.resolvePlainRangeDetailed = function (editor, fix) {
      const resolved = resolveTargetRangeFromFix(editor, fix || {});
      if (!resolved.ok) {
        return {
          resolved: false,
          reason: resolved.reason || "range_resolution_failed",
          source: resolved.target?.source || null,
          from: resolved.target?.from ?? null,
          to: resolved.target?.to ?? null,
          docFrom: resolved.docFrom ?? null,
          docTo: resolved.docTo ?? null,
          expectedText: null
        };
      }

      return {
        resolved: true,
        reason: null,
        source: resolved.target.source,
        from: resolved.target.from,
        to: resolved.target.to,
        docFrom: resolved.docFrom,
        docTo: resolved.docTo,
        expectedText: resolved.expectedText
      };
    };

    api.applyQualityIssueFixDetailed = function (editor, fix) {
      if (!editor?.view || !fix) {
        return buildFixApplyResult(false, false, "invalid_editor_or_fix");
      }

      console.warn("[quality] apply fix detailed called", {
        kind: fix.kind,
        from: fix.from,
        to: fix.to,
        issueKey: fix.issueKey || null
      });

      const kind = String(fix.kind || "").toLowerCase();
      const issueKey = fix.issueKey ? String(fix.issueKey) : null;

      if (kind !== "replace" && kind !== "delete" && kind !== "insert") {
        return buildFixApplyResult(false, false, "unsupported_fix_kind", { issueKey, kind });
      }

      if ((kind === "replace" || kind === "insert") && typeof fix.text !== "string") {
        return buildFixApplyResult(false, false, "invalid_fix_text", { issueKey, kind });
      }

      const replacementText = kind === "delete" ? "" : String(fix.text || "");
      const plainText = getEditorPlainText(editor);

      if (window.__waQualityDebug === true) {
        console.debug("[quality] apply fix request", {
          issueKey,
          kind,
          from: fix.from,
          to: fix.to,
          text: replacementText,
          anchorText: fix.anchorText || null
        });
      }

      let docFrom = parseOptionalNumber(fix.docFrom);
      let docTo = parseOptionalNumber(fix.docTo);
      let target = { from: Number(fix.from) || 0, to: Number(fix.to) || 0, source: "doc-captured-range" };
      let anchorMatchedAtRange = false;
      const docSize = Number(editor?.state?.doc?.content?.size) || 0;
      const requiresRange = kind === "replace" || kind === "delete";

      const resolveFallbackTarget = () => {
        const resolved = resolveTargetRangeFromFix(editor, fix);
        if (!resolved.ok) {
          return buildFixApplyResult(false, false, "could_not_resolve_range", {
            issueKey,
            kind,
            source: resolved.target?.source ?? null,
            from: resolved.target?.from ?? null,
            to: resolved.target?.to ?? null,
            resolutionReason: resolved.reason || "range_resolution_failed"
          });
        }

        target = resolved.target;
        docFrom = resolved.docFrom;
        docTo = resolved.docTo;
        anchorMatchedAtRange = !!resolved.anchorMatchedAtRange;
        return null;
      };

      const hasCapturedDocRange = docFrom !== null && docTo !== null;
      if (hasCapturedDocRange) {
        const capturedLooksValid = docFrom > 0
          && docTo > 0
          && docFrom <= docSize
          && docTo <= docSize
          && (!requiresRange || docTo > docFrom);

        if (!capturedLooksValid) {
          const fallbackFailure = resolveFallbackTarget();
          if (fallbackFailure) {
            return fallbackFailure;
          }
        } else {
          target = {
            from: Number.isFinite(Number(fix.from)) ? Number(fix.from) : 0,
            to: Number.isFinite(Number(fix.to)) ? Number(fix.to) : 0,
            source: "doc-captured-range"
          };
        }
      } else {
        const fallbackFailure = resolveFallbackTarget();
        if (fallbackFailure) {
          return fallbackFailure;
        }
      }

      if (docFrom === null || docTo === null) {
        const fallbackFailure = resolveFallbackTarget();
        if (fallbackFailure) {
          return fallbackFailure;
        }
      }

      if (!Number.isFinite(docFrom) || !Number.isFinite(docTo)) {
        return buildFixApplyResult(false, false, "could_not_resolve_range", {
          issueKey,
          kind,
          source: target.source,
          from: target?.from ?? null,
          to: target?.to ?? null,
          resolutionReason: "doc_range_missing_after_resolution"
        });
      }

      if (requiresRange && docTo <= docFrom) {
        const fallbackFailure = resolveFallbackTarget();
        if (fallbackFailure) {
          return fallbackFailure;
        }
      }

      if (requiresRange && docTo <= docFrom) {
        return buildFixApplyResult(false, false, "could_not_resolve_range", {
          issueKey,
          kind,
          source: target.source,
          docFrom,
          docTo,
          resolutionReason: "resolved_doc_range_invalid"
        });
      }

      if (docFrom > docSize || docTo > docSize || docFrom < 0 || docTo < 0) {
        const fallbackFailure = resolveFallbackTarget();
        if (fallbackFailure) {
          return fallbackFailure;
        }
      }

      if (docFrom > docSize || docTo > docSize || docFrom < 0 || docTo < 0) {
        return buildFixApplyResult(false, false, "could_not_resolve_range", {
          issueKey,
          kind,
          source: target.source,
          docFrom,
          docTo,
          resolutionReason: "resolved_doc_range_out_of_bounds"
        });
      }

      docFrom = Number(docFrom);
      docTo = Number(docTo);

      if (window.__waQualityDebug === true) {
        console.debug("[quality] apply target resolved", {
          issueKey,
          kind,
          source: target.source,
          plainFrom: target.from,
          plainTo: target.to,
          docFrom,
          docTo,
          docSize
        });
      }

      if (target.source === "doc-captured-range") {
        target = {
          from: Number.isFinite(Number(fix.from)) ? Number(fix.from) : 0,
          to: Number.isFinite(Number(fix.to)) ? Number(fix.to) : 0,
          source: "doc-captured-range"
        };
      }

      const expectedText = typeof fix.expectedText === "string" ? fix.expectedText : null;
      const beforeAnchor = typeof fix.beforeAnchor === "string" ? fix.beforeAnchor : null;
      const afterAnchor = typeof fix.afterAnchor === "string" ? fix.afterAnchor : null;
      const needleText = typeof fix.needleText === "string" ? fix.needleText : null;
      const buildDocRangeFromPlain = (plainFrom, plainTo) => {
        const normalizedFrom = Number(plainFrom);
        const normalizedTo = Number(plainTo);
        if (!Number.isFinite(normalizedFrom) || !Number.isFinite(normalizedTo) || normalizedTo < normalizedFrom) {
          return null;
        }

        const segments = buildPlainTextSegments(editor.state.doc);
        const mappedFrom = mapPlainOffsetToDoc(segments, normalizedFrom);
        const mappedTo = mapPlainOffsetToDoc(segments, normalizedTo);
        if (mappedFrom === null || mappedTo === null || (kind !== "insert" && mappedTo <= mappedFrom)) {
          return null;
        }

        return { docFrom: mappedFrom, docTo: mappedTo, from: normalizedFrom, to: normalizedTo };
      };

      const tryRecoverRangeFromExpectedText = () => {
        if (!expectedText || kind === "insert") {
          return null;
        }

        const currentPlain = getEditorPlainText(editor);
        const expectedCandidates = [expectedText, ...buildAnchorCandidates(expectedText)]
          .filter((value, index, arr) => value && arr.indexOf(value) === index);
        const matches = findAnchorOccurrences(currentPlain, expectedCandidates);
        if (!matches.length) {
          if (window.__waQualityDebug === true) {
            console.debug("[quality] expected-text recovery no matches", { issueKey, expectedLength: expectedText.length });
          }
          return null;
        }

        const currentTargetCenter = (Number(target?.from) + Number(target?.to)) / 2;
        const nearest = matches
          .slice()
          .sort((a, b) => {
            const aCenter = (a.from + a.to) / 2;
            const bCenter = (b.from + b.to) / 2;
            return Math.abs(aCenter - currentTargetCenter) - Math.abs(bCenter - currentTargetCenter);
          })[0];

        const mapped = buildDocRangeFromPlain(nearest.from, nearest.to);
        if (!mapped) {
          if (window.__waQualityDebug === true) {
            console.debug("[quality] expected-text recovery mapping failed", { issueKey, from: nearest.from, to: nearest.to });
          }
          return null;
        }

        if (window.__waQualityDebug === true) {
          console.debug("[quality] expected-text recovery selected", { issueKey, matches: matches.length, from: nearest.from, to: nearest.to });
        }

        return {
          source: "expected-nearest",
          from: nearest.from,
          to: nearest.to,
          docFrom: mapped.docFrom,
          docTo: mapped.docTo
        };
      };

      const tryRecoverRangeFromAnchor = () => {
        const anchorCandidates = buildAnchorCandidates(fix.anchorText);
        if (!anchorCandidates.length) {
          return null;
        }

        const currentPlain = getEditorPlainText(editor);
        const matches = findAnchorOccurrences(currentPlain, anchorCandidates);
        if (!matches.length) {
          if (window.__waQualityDebug === true) {
            console.debug("[quality] anchor recovery no matches", { issueKey });
          }
          return null;
        }

        const currentTargetCenter = (Number(target?.from) + Number(target?.to)) / 2;
        const nearest = matches
          .slice()
          .sort((a, b) => {
            const aCenter = (a.from + a.to) / 2;
            const bCenter = (b.from + b.to) / 2;
            return Math.abs(aCenter - currentTargetCenter) - Math.abs(bCenter - currentTargetCenter);
          })[0];

        const mapped = buildDocRangeFromPlain(nearest.from, nearest.to);
        if (!mapped) {
          if (window.__waQualityDebug === true) {
            console.debug("[quality] anchor recovery mapping failed", { issueKey, from: nearest.from, to: nearest.to });
          }
          return null;
        }

        if (window.__waQualityDebug === true) {
          console.debug("[quality] anchor recovery selected", { issueKey, matches: matches.length, from: nearest.from, to: nearest.to });
        }

        return {
          source: "anchor-nearest",
          from: nearest.from,
          to: nearest.to,
          docFrom: mapped.docFrom,
          docTo: mapped.docTo
        };
      };

      const tryRecoverRangeFromContext = () => {
        if (!needleText || kind === "insert") {
          return null;
        }

        const currentPlain = getEditorPlainText(editor);
        const currentTargetCenter = (Number(target?.from) + Number(target?.to)) / 2;
        const contextual = findBestContextMatch(currentPlain, beforeAnchor, needleText, afterAnchor, currentTargetCenter);
        if (!contextual) {
          if (window.__waQualityDebug === true) {
            console.debug("[quality] contextual recovery no match", { issueKey });
          }
          return null;
        }

        const mapped = buildDocRangeFromPlain(contextual.from, contextual.to);
        if (!mapped) {
          if (window.__waQualityDebug === true) {
            console.debug("[quality] contextual recovery mapping failed", { issueKey, from: contextual.from, to: contextual.to });
          }
          return null;
        }

        if (window.__waQualityDebug === true) {
          console.debug("[quality] contextual recovery selected", { issueKey, from: contextual.from, to: contextual.to });
        }

        return {
          source: "contextual-nearest",
          from: contextual.from,
          to: contextual.to,
          docFrom: mapped.docFrom,
          docTo: mapped.docTo
        };
      };

      if (expectedText !== null && kind !== "insert") {
        const plainFrom = Number(target?.from);
        const plainTo = Number(target?.to);
        let current = null;
        if (Number.isFinite(plainFrom) && Number.isFinite(plainTo) && plainTo >= plainFrom && plainTo <= plainText.length) {
          current = plainText.slice(plainFrom, plainTo);
        } else {
          current = editor.state.doc.textBetween(docFrom, docTo, "", "");
        }

        if (current !== expectedText) {
          let recovered = null;

          const fallbackResolved = resolveTargetRangeFromFix(editor, { ...fix, docFrom: null, docTo: null });
          if (fallbackResolved.ok) {
            const fallbackCurrent = editor.state.doc.textBetween(fallbackResolved.docFrom, fallbackResolved.docTo, "", "");
            if (fallbackCurrent === expectedText) {
              recovered = {
                source: `${fallbackResolved.target.source}-retry`,
                from: fallbackResolved.target.from,
                to: fallbackResolved.target.to,
                docFrom: fallbackResolved.docFrom,
                docTo: fallbackResolved.docTo
              };
            }
          }

          if (!recovered) {
            recovered = tryRecoverRangeFromExpectedText();
          }

          if (!recovered && kind === "delete") {
            recovered = tryRecoverRangeFromAnchor();
          }

          if (!recovered) {
            recovered = tryRecoverRangeFromContext();
          }

          if (!recovered) {
            return buildFixApplyResult(false, false, "doc_expected_text_mismatch", {
              issueKey,
              kind,
              source: target.source,
              docFrom,
              docTo,
              expectedLength: expectedText?.length || 0
            });
          }

          target = {
            from: recovered.from,
            to: recovered.to,
            source: recovered.source
          };
          docFrom = recovered.docFrom;
          docTo = recovered.docTo;
        }
      }

      const tr = editor.state.tr;
      if (kind === "insert") {
        tr.insertText(replacementText, docFrom, docFrom);
      } else {
        tr.insertText(replacementText, docFrom, docTo);
      }

      if (!tr.docChanged) {
        return buildFixApplyResult(false, false, "transaction_noop", {
          issueKey,
          kind,
          from: target?.from ?? null,
          to: target?.to ?? null,
          source: target.source
        });
      }

      editor.view.dispatch(tr.scrollIntoView());
      const afterPlain = getEditorPlainText(editor);
      const changed = afterPlain !== plainText;
      if (window.__waQualityDebug === true) {
        console.debug("[quality] apply fix resolved", {
          issueKey,
          docFrom,
          docTo,
          plainFrom: target.from,
          plainTo: target.to,
          source: target.source,
          anchorMatchedAtRange,
          expectedLength: expectedText?.length || 0
        });
      }

      return buildFixApplyResult(true, changed, changed ? "applied" : "no_plain_text_change", {
        issueKey,
        kind,
        from: target.from,
        to: target.to,
        source: target.source,
        docFrom,
        docTo
      });
    };

    api.applyQualityIssueFix = function (editor, fix) {
      const result = api.applyQualityIssueFixDetailed(editor, fix);
      if (window.__waQualityDebug === true) {
        console.debug("[quality] apply fix result", result);
      }
      return !!result?.applied;
    };

    api.__patchedQualityIssueLocator = true;
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

  const applyShortcut = (editor, command, ...args) => {
    if (!editor?.commands || typeof editor.commands[command] !== "function") {
      return false;
    }

    try {
      editor.chain().focus()[command](...args).run();
      return true;
    } catch {
      return false;
    }
  };

  const handleEditorShortcut = (event, editor) => {
    if (!event || !editor || !(event.ctrlKey || event.metaKey)) {
      return false;
    }

    const key = String(event.key || "").toLowerCase();
    if (!key) {
      return false;
    }

    if ((key === "c" || key === "x" || key === "v") && !event.shiftKey && !event.altKey) {
      return false;
    }

    if (key === "b") {
      const handled = applyShortcut(editor, "toggleBold");
      if (handled) {
        event.preventDefault();
      }
      return handled;
    }

    if (key === "i") {
      const handled = applyShortcut(editor, "toggleItalic");
      if (handled) {
        event.preventDefault();
      }
      return handled;
    }

    if (key === "u" && editor?.commands && typeof editor.commands.toggleUnderline === "function") {
      const handled = applyShortcut(editor, "toggleUnderline");
      if (handled) {
        event.preventDefault();
      }
      return handled;
    }

    if (key === "k") {
      if (!editor.__dotNetRef) {
        return false;
      }
      event.preventDefault();
      safeInvoke(editor.__dotNetRef, editor.__interopState || createInteropState(editor.__dotNetRef), "OnEditorLinkShortcut");
      return true;
    }

    if (key === "z" && !event.shiftKey) {
      const handled = applyShortcut(editor, "undo");
      if (handled) {
        event.preventDefault();
      }
      return handled;
    }

    if (key === "y" || (key === "z" && event.shiftKey)) {
      const handled = applyShortcut(editor, "redo");
      if (handled) {
        event.preventDefault();
      }
      return handled;
    }

    if (key === "x" && event.shiftKey) {
      const handled = applyShortcut(editor, "toggleStrike");
      if (handled) {
        event.preventDefault();
      }
      return handled;
    }

    return false;
  };

  const attachEditorShortcuts = (editor) => {
    const root = editor?.view?.dom;
    if (!root || root.__writerShortcutHandler) {
      return;
    }

    const handler = (event) => {
      if (event?.key === "Backspace") {
        const state = getPatchedPaginationState(editor);
        if (state) {
          state.lastUserInputAt = Date.now();
          const boundaryPos = getNearestBreakPosForSelection(editor);
          if (Number.isFinite(boundaryPos)) {
            state.boundaryStableBreakPos = Number(boundaryPos);
          }
        }
      }
      handleEditorShortcut(event, editor);
    };
    root.addEventListener("keydown", handler, true);
    root.__writerShortcutHandler = handler;
  };

  const openInNewTab = (href) => {
    if (!href || typeof href !== "string") {
      return;
    }

    window.open(href, "_blank", "noopener,noreferrer");
  };

  api.openInNewTab = openInNewTab;

  const attachLinkInteractions = (editor) => {
    const root = editor?.view?.dom;
    if (!root || root.__writerLinkClickHandler) {
      return;
    }

    const clickHandler = (event) => {
      if (!event?.ctrlKey || event.button !== 0) {
        return;
      }

      const target = event.target instanceof Element ? event.target : null;
      const link = target?.closest?.("a[href]");
      if (!link || !root.contains(link)) {
        return;
      }

      event.preventDefault();
      event.stopPropagation();
      openInNewTab(link.getAttribute("href"));
    };

    root.addEventListener("click", clickHandler, true);
    root.__writerLinkClickHandler = clickHandler;
  };

  const createDebounced = (fn, waitMs) => {
    let timer = null;
    return () => {
      if (timer) {
        window.clearTimeout(timer);
      }

      timer = window.setTimeout(() => {
        timer = null;
        fn();
      }, waitMs);
    };
  };

  const PAGINATION_META_KEY = "writerPagination";
  const PAGINATION_MAX_PASSES = 10;
  const PAGINATION_IDLE_RESET_MS = 260;
  const PAGINATION_COOLDOWN_MS = 650;
  const PAGINATION_INPUT_IDLE_MS = 500;
  const PAGINATION_VIEWPORT_DELTA_PX = 2;
  const PAGINATION_OBSERVER_SUPPRESS_MS = 80;

  const normalizePageBreakOptions = (options) => ({
    pageHeightPx: Number(options?.pageHeightPx) || 980,
    showHorizontalRule: options?.showHorizontalRule !== false,
    gutterOffsetPx: Number(options?.gutterOffsetPx) || 28,
    pageGapPx: Number(options?.pageGapPx) || 32,
    layoutMode: options?.layoutMode || "simple",
    debug: options?.debug === true
  });

  const pageBreakOptionsKey = (enabled, options) => {
    const normalized = normalizePageBreakOptions(options);
    return [
      enabled ? "1" : "0",
      normalized.pageHeightPx,
      normalized.showHorizontalRule ? "1" : "0",
      normalized.gutterOffsetPx,
      normalized.pageGapPx,
      normalized.layoutMode,
      normalized.debug ? "1" : "0"
    ].join("|");
  };

  const getPatchedPaginationState = (editor) => {
    if (!editor) {
      return null;
    }

    if (!editor.__writerPaginationPatchState) {
      editor.__writerPaginationPatchState = {
        appliedKey: null,
        desiredKey: null,
        desiredEnabled: false,
        desiredOptions: null,
        rafId: 0,
        isRunning: false,
        pending: false,
        force: false,
        observerAttached: false,
        run: null,
        reentrancyLogged: false,
        cooldownUntil: 0,
        lastStopReason: null,
        lastStopTimestamp: 0,
        lastStopSignature: null,
        lastStopDocSignature: null,
        lastStopViewport: null,
        cooldownSkipLoggedAt: 0,
        suppressObserverEventsUntil: 0,
        idleTimer: 0,
        lastUserInputAt: 0,
        lastKnownDocSignature: null,
        lastKnownViewport: null,
        pendingReason: null,
        boundaryStableBreakPos: null,
        lastDocSize: 0,
        lastBreakSignature: null,
        mergeUpPending: false
      };
    }

    return editor.__writerPaginationPatchState;
  };

  const startPaginationRun = (state, reason) => {
    state.run = {
      reason,
      passCount: 0,
      lastSignature: null,
      beforeLastSignature: null,
      stopped: false,
      stopReason: null,
      warningLogged: false,
      startedAt: Date.now(),
      lastActivityAt: Date.now(),
      tallBlock: null,
      lastBreakSet: new Set(),
      breakInfoByPos: new Map(),
      breakToggleCount: new Map(),
      frozenBreaks: new Set(),
      rebuildAttempted: false,
      mergeUpBaselineBreakPos: null
    };
  };

  const getDocSignature = (editor) => {
    const state = editor?.view?.state;
    const doc = state?.doc;
    if (!doc || !state?.selection) {
      return "none";
    }

    const selection = state.selection;
    const size = Number(doc.content?.size ?? doc.nodeSize ?? 0);
    const childCount = Number(doc.childCount ?? 0);
    return `${size}:${childCount}:${selection.from}:${selection.to}`;
  };

  const getDocSize = (editor) => {
    const doc = editor?.view?.state?.doc;
    return Number(doc?.content?.size ?? doc?.nodeSize ?? 0);
  };

  const getViewportMetrics = (editor) => {
    const ctx = getEditorContext(editor);
    const root = editor?.view?.dom;
    const target = ctx?.viewport || ctx?.content || root;
    if (!(target instanceof Element)) {
      return {
        width: 0,
        height: 0,
        dpr: Math.round(Number(window.devicePixelRatio || 1) * 100) / 100
      };
    }

    const rect = target.getBoundingClientRect();
    return {
      width: Math.round(rect.width || 0),
      height: Math.round(rect.height || 0),
      dpr: Math.round(Number(window.devicePixelRatio || 1) * 100) / 100
    };
  };

  const hasMeaningfulViewportChange = (previous, next) => {
    if (!previous || !next) {
      return true;
    }

    if (Math.abs(Number(next.dpr || 0) - Number(previous.dpr || 0)) > 0.001) {
      return true;
    }

    return Math.abs(Number(next.width || 0) - Number(previous.width || 0)) > PAGINATION_VIEWPORT_DELTA_PX
      || Math.abs(Number(next.height || 0) - Number(previous.height || 0)) > PAGINATION_VIEWPORT_DELTA_PX;
  };

  const isNearEqualSignature = (left, right) => {
    if (!left || !right) {
      return false;
    }

    if (left === right) {
      return true;
    }

    const normalizePart = (part) => {
      const [rawPos, rawPx] = String(part || "").split(":");
      const pos = Number(rawPos);
      const px = Number(rawPx);
      if (!Number.isFinite(pos) || !Number.isFinite(px)) {
        return null;
      }

      return {
        pos: Math.round(pos),
        px: Math.round(px)
      };
    };

    const normalize = (signature) => String(signature)
      .split("|")
      .map((item) => normalizePart(item))
      .filter(Boolean)
      .sort((a, b) => a.pos - b.pos);

    const a = normalize(left);
    const b = normalize(right);
    if (a.length === 0 || b.length === 0 || a.length !== b.length) {
      return false;
    }

    for (let index = 0; index < a.length; index += 1) {
      if (a[index].pos !== b[index].pos) {
        return false;
      }

      if (Math.abs(a[index].px - b[index].px) > 1) {
        return false;
      }
    }

    return true;
  };

  const getNearestBreakPosForSelection = (editor) => {
    const selectionFrom = Number(editor?.view?.state?.selection?.from ?? -1);
    if (!Number.isFinite(selectionFrom) || selectionFrom < 0) {
      return null;
    }

    const breakMap = getCurrentBreakInfoMap(editor);
    let nearest = null;
    breakMap.forEach((_value, pos) => {
      if (Math.abs(pos - selectionFrom) <= 2 || Math.abs((pos + 1) - selectionFrom) <= 2) {
        nearest = pos;
      }
    });

    return nearest;
  };

  const getCurrentBreakSignature = (editor) => {
    const gap = editor?.__pageGapState;
    const last = gap?.lastGapInfo;
    const breaks = Array.isArray(last?.breaks)
      ? last.breaks
      : Array.isArray(gap?.breaks)
        ? gap.breaks
        : [];
    if (breaks.length > 0) {
      return breaks
        .map((entry) => ({
          pos: Number(entry?.pos ?? entry?.from ?? entry?.to ?? -1),
          spacerPx: Math.round(Number(entry?.spacerPx ?? entry?.height ?? 0))
        }))
        .filter((entry) => Number.isFinite(entry.pos) && entry.pos >= 0)
        .sort((left, right) => left.pos - right.pos)
        .map((entry) => {
          return `${entry.pos}:${entry.spacerPx}`;
        })
        .join("|");
    }

    const gapCount = Number(gap?.gapCount ?? 0);
    const pageCount = Number(gap?.pageCount ?? 1);
    return `${gapCount}|${pageCount}`;
  };

  const getCurrentBreakInfoMap = (editor) => {
    const gap = editor?.__pageGapState;
    const last = gap?.lastGapInfo;
    const breaks = Array.isArray(last?.breaks)
      ? last.breaks
      : Array.isArray(gap?.breaks)
        ? gap.breaks
        : [];
    const map = new Map();
    breaks.forEach((entry) => {
      const pos = Number(entry?.pos ?? entry?.from ?? entry?.to ?? -1);
      if (!Number.isFinite(pos) || pos < 0) {
        return;
      }

      const spacerPx = Math.max(1, Math.round(Number(entry?.spacerPx ?? entry?.height ?? 0) || 1));
      map.set(Math.round(pos), {
        pos: Math.round(pos),
        spacerPx,
        pageIndex: Number(entry?.pageIndex ?? 0) || 0
      });
    });
    return map;
  };

  const getFirstBreakPos = (editor) => {
    const map = getCurrentBreakInfoMap(editor);
    let first = null;
    map.forEach((_value, pos) => {
      if (!Number.isFinite(pos)) {
        return;
      }
      if (first === null || pos < first) {
        first = pos;
      }
    });
    return first;
  };

  const clearPaginationCaches = (editor, ranges = null) => {
    const pageGapState = editor?.__pageGapState;
    const cacheKeys = [
      "heightCache",
      "blockHeightCache",
      "measurementCache",
      "nodeRectCache",
      "coordsCache",
      "blockCache"
    ];

    const intersects = (key, range) => {
      if (!Number.isFinite(key)) {
        return false;
      }
      return key >= range.from && key <= range.to;
    };

    cacheKeys.forEach((cacheKey) => {
      const cache = pageGapState?.[cacheKey];
      if (cache instanceof Map) {
        if (Array.isArray(ranges) && ranges.length > 0) {
          Array.from(cache.keys()).forEach((entryKey) => {
            if (ranges.some((range) => intersects(Number(entryKey), range))) {
              cache.delete(entryKey);
            }
          });
        } else {
          cache.clear();
        }
      } else if (cache && typeof cache === "object") {
        pageGapState[cacheKey] = {};
      }
    });

    if (!pageGapState) {
      return;
    }

    if (!Array.isArray(ranges) || ranges.length === 0) {
      pageGapState.lastGapInfo = null;
      pageGapState.breaks = [];
      pageGapState.gapCount = 0;
    }
  };

  const collectChangedRanges = (tr) => {
    const ranges = [];
    if (!tr?.mapping?.maps?.length) {
      return ranges;
    }

    tr.mapping.maps.forEach((map) => {
      map.forEach((_oldFrom, _oldTo, newFrom, newTo) => {
        ranges.push({
          from: Math.max(0, Math.min(newFrom, newTo)),
          to: Math.max(newFrom, newTo)
        });
      });
    });
    return ranges;
  };

  const resetPaginationArtifacts = (editor, state, reason) => {
    if (!editor || !state || typeof state.__originalSetPageBreaksEnabled !== "function") {
      return;
    }

    state.suppressObserverEventsUntil = Date.now() + PAGINATION_OBSERVER_SUPPRESS_MS;
    state.__originalSetPageBreaksEnabled(editor, false, state.desiredOptions);
    clearPaginationCaches(editor);

    const gapState = editor.__pageGapState || {};
    editor.__pageGapState = {
      ...gapState,
      breaks: [],
      gapCount: 0,
      lastGapInfo: null,
      breakMap: new Map(),
      freezeUntil: 0
    };

    if (state.desiredOptions?.debug) {
      console.info("[pagination] rebuild-from-scratch triggered", { reason });
    }
  };

  const findTallBlockInfo = (editor, options) => {
    const view = editor?.view;
    const doc = view?.state?.doc;
    if (!view || !doc) {
      return null;
    }

    const pageHeight = Math.max(1, Number(options?.pageHeightPx) || 980);
    const safetyMargin = 24;
    const threshold = Math.max(1, pageHeight - safetyMargin);
    let found = null;
    doc.descendants((node, pos) => {
      if (found || !node?.isBlock) {
        return false;
      }

      if (node.type?.name === "paragraph" || node.type?.name === "heading") {
        return;
      }

      let domNode = null;
      try {
        domNode = view.nodeDOM(pos);
      } catch {
        domNode = null;
      }

      const element = domNode instanceof Element ? domNode : null;
      if (!element) {
        return;
      }

      const height = Math.round(element.getBoundingClientRect().height || 0);
      if (height > threshold) {
        found = {
          type: node.type?.name || "unknown",
          pos,
          height,
          pageHeight
        };
      }
    });

    return found;
  };

  const logPaginationStopWarning = (editor, run, stopReason) => {
    if (run.warningLogged) {
      return;
    }

    run.warningLogged = true;
    const gapState = editor?.__pageGapState;
    const sigA = run.beforeLastSignature;
    const sigB = run.lastSignature;
    const gapCount = Number(gapState?.gapCount ?? 0);
    console.warn("[pagination] run halted", {
      stopReason,
      passCount: run.passCount,
      gapCount,
      lastSignatures: [sigA, sigB],
      tallBlock: run.tallBlock
    });
  };

  const queuePaginationRun = (editor, reason, force = false) => {
    const state = getPatchedPaginationState(editor);
    if (!state) {
      return;
    }

    const now = Date.now();
    const docSignature = getDocSignature(editor);
    const viewport = getViewportMetrics(editor);
    const stoppedRecently = state.lastStopReason === "maxPasses" || state.lastStopReason === "oscillation";
    const withinCooldown = now < Number(state.cooldownUntil || 0);
    const docChangedSinceStop = state.lastStopDocSignature !== docSignature;
    const viewportChangedSinceStop = hasMeaningfulViewportChange(state.lastStopViewport, viewport);
    const inputIdleReached = state.lastUserInputAt > 0 && (now - state.lastUserInputAt) >= PAGINATION_INPUT_IDLE_MS;

    state.lastKnownDocSignature = docSignature;
    state.lastKnownViewport = viewport;
    state.pendingReason = reason || "update";

    if (Number(state.suppressObserverEventsUntil || 0) > now && !force) {
      return;
    }

    if (!force && stoppedRecently && !docChangedSinceStop && !viewportChangedSinceStop) {
      if (withinCooldown || !inputIdleReached) {
        if (!withinCooldown && !inputIdleReached && !state.idleTimer) {
          const wait = Math.max(32, PAGINATION_INPUT_IDLE_MS - (now - state.lastUserInputAt));
          state.idleTimer = window.setTimeout(() => {
            state.idleTimer = 0;
            queuePaginationRun(editor, "idleRetry", true);
          }, wait);
        }

        if ((now - Number(state.cooldownSkipLoggedAt || 0)) > 400) {
          state.cooldownSkipLoggedAt = now;
          console.info("[pagination] skipped due to cooldown", {
            reason,
            stopReason: state.lastStopReason,
            withinCooldown,
            inputIdleReached
          });
        }
        return;
      }
    }

    if (state.idleTimer) {
      window.clearTimeout(state.idleTimer);
      state.idleTimer = 0;
    }

    if (stoppedRecently && (force || docChangedSinceStop || viewportChangedSinceStop || inputIdleReached)) {
      state.lastStopReason = null;
      state.lastStopTimestamp = 0;
      state.lastStopSignature = null;
      state.cooldownUntil = 0;
    }

    if (!state.run || (now - state.run.lastActivityAt) > PAGINATION_IDLE_RESET_MS || state.run.stopped) {
      startPaginationRun(state, reason);
      if (Number.isFinite(state.boundaryStableBreakPos)) {
        state.run.frozenBreaks.add(Number(state.boundaryStableBreakPos));
        const boundaryInfo = getCurrentBreakInfoMap(editor).get(Number(state.boundaryStableBreakPos));
        if (boundaryInfo) {
          state.run.breakInfoByPos.set(Number(state.boundaryStableBreakPos), boundaryInfo);
        }
        state.boundaryStableBreakPos = null;
      }
    }
    state.run.lastActivityAt = now;
    state.pending = true;
    state.force = state.force || !!force;

    if (state.rafId) {
      window.cancelAnimationFrame(state.rafId);
    }

    state.rafId = window.requestAnimationFrame(() => {
      state.rafId = 0;
      runPaginationUpdate(editor);
    });
  };

  const applyFrozenBreakHints = (editor, run, options) => {
    if (!editor || !run || !(run.frozenBreaks instanceof Set) || run.frozenBreaks.size === 0) {
      return;
    }

    const gapState = editor.__pageGapState || {};
    const breakMap = gapState.breakMap instanceof Map ? gapState.breakMap : new Map();
    run.frozenBreaks.forEach((pos) => {
      const info = run.breakInfoByPos.get(pos);
      if (!info) {
        return;
      }

      breakMap.set(pos, {
        height: Math.max(1, Math.round(Number(info.spacerPx || options?.pageGapPx || 1))),
        pos,
        pageIndex: Number(info.pageIndex || 0),
        stableBoundary: true
      });
    });

    editor.__pageGapState = {
      ...gapState,
      breakMap,
      freezeUntil: Date.now() + 400
    };
  };

  const updateBoundaryStabilization = (run, editor) => {
    if (!run) {
      return;
    }

    const currentBreakInfo = getCurrentBreakInfoMap(editor);
    const nextSet = new Set(currentBreakInfo.keys());
    const previousSet = run.lastBreakSet instanceof Set ? run.lastBreakSet : new Set();
    const toggled = new Set();

    previousSet.forEach((pos) => {
      if (!nextSet.has(pos)) {
        toggled.add(pos);
      }
    });
    nextSet.forEach((pos) => {
      if (!previousSet.has(pos)) {
        toggled.add(pos);
      }
    });

    toggled.forEach((pos) => {
      const nextCount = Number(run.breakToggleCount.get(pos) || 0) + 1;
      run.breakToggleCount.set(pos, nextCount);
      if (nextCount >= 2) {
        run.frozenBreaks.add(pos);
      }
    });

    currentBreakInfo.forEach((info, pos) => run.breakInfoByPos.set(pos, info));
    run.lastBreakSet = nextSet;
  };

  const performFullRebuild = (editor, state, run, stopReason) => {
    if (!editor || !state || !run || run.rebuildAttempted || typeof state.__originalSetPageBreaksEnabled !== "function") {
      return false;
    }

    run.rebuildAttempted = true;
    resetPaginationArtifacts(editor, state, stopReason || "nonConvergent");
    if (run.tallBlock && state.desiredOptions?.debug) {
      console.info("[pagination] tall-block fallback used during rebuild", {
        nodeType: run.tallBlock.type,
        pos: run.tallBlock.pos,
        height: run.tallBlock.height,
        pageHeight: run.tallBlock.pageHeight
      });
    }

    state.suppressObserverEventsUntil = Date.now() + PAGINATION_OBSERVER_SUPPRESS_MS;
    const rebuiltCount = state.__originalSetPageBreaksEnabled(editor, true, state.desiredOptions);
    state.suppressObserverEventsUntil = Date.now() + PAGINATION_OBSERVER_SUPPRESS_MS;
    if (typeof rebuiltCount === "number" && Number.isFinite(rebuiltCount) && rebuiltCount > 0) {
      editor.__pageGapState = {
        ...(editor.__pageGapState || {}),
        pageCount: rebuiltCount
      };
    }

    run.beforeLastSignature = run.lastSignature;
    run.lastSignature = getCurrentBreakSignature(editor);
    run.stopReason = "rebuild";
    run.stopped = true;
    return true;
  };

  const runPaginationUpdate = (editor) => {
    const state = getPatchedPaginationState(editor);
    if (!state || !state.desiredEnabled || !state.desiredOptions || typeof state.__originalSetPageBreaksEnabled !== "function") {
      return;
    }

    if (state.isRunning) {
      state.pending = true;
      if (!state.reentrancyLogged) {
        state.reentrancyLogged = true;
        console.info("[pagination] re-entrancy guard: run already active, coalescing.");
      }
      return;
    }

    if (!state.run) {
      startPaginationRun(state, "update");
    }

    if (state.run?.stopped) {
      return;
    }

    const run = state.run;
    state.isRunning = true;
    state.pending = false;
    state.reentrancyLogged = false;
    const forceThisRun = state.force;
    state.force = false;

    try {
      const nextKey = pageBreakOptionsKey(state.desiredEnabled, state.desiredOptions);
      if (!forceThisRun && state.appliedKey === nextKey) {
        state.isRunning = false;
        return;
      }

      run.passCount += 1;
      if (run.passCount === 1) {
        run.mergeUpBaselineBreakPos = getFirstBreakPos(editor);
        resetPaginationArtifacts(editor, state, state.mergeUpPending ? "mergeUp" : "freshRun");
      }

      run.tallBlock = findTallBlockInfo(editor, state.desiredOptions);
      if (run.tallBlock && editor?.__pageGapState) {
        const breakMap = editor.__pageGapState.breakMap instanceof Map
          ? editor.__pageGapState.breakMap
          : new Map();
        if (!breakMap.has(run.tallBlock.pos)) {
          breakMap.set(run.tallBlock.pos, {
            height: Math.max(1, Math.round(state.desiredOptions.pageGapPx || 1)),
            pos: run.tallBlock.pos,
            pageIndex: 1,
            tallBlock: true
          });
        }
        editor.__pageGapState.breakMap = breakMap;
        editor.__pageGapState.freezeUntil = Date.now() + 300;
      }

      applyFrozenBreakHints(editor, run, state.desiredOptions);
      state.suppressObserverEventsUntil = Date.now() + PAGINATION_OBSERVER_SUPPRESS_MS;
      const count = state.__originalSetPageBreaksEnabled(editor, true, state.desiredOptions);
      state.suppressObserverEventsUntil = Date.now() + PAGINATION_OBSERVER_SUPPRESS_MS;
      state.appliedKey = nextKey;
      if (typeof count === "number" && Number.isFinite(count) && count > 0) {
        editor.__pageGapState = {
          ...(editor.__pageGapState || {}),
          pageCount: count
        };
      }

      updateBoundaryStabilization(run, editor);

      const signature = getCurrentBreakSignature(editor);
      const oscillating = run.beforeLastSignature
        && run.lastSignature
        && run.beforeLastSignature === signature
        && run.lastSignature !== signature;
      const nearEqualStable = isNearEqualSignature(run.lastSignature, signature);

      run.beforeLastSignature = run.lastSignature;
      run.lastSignature = signature;

      if (nearEqualStable) {
        run.stopped = true;
        run.stopReason = "acceptedNearEqual";
      } else if (oscillating) {
        if (!performFullRebuild(editor, state, run, "oscillation")) {
          run.stopped = true;
          run.stopReason = "oscillation";
          logPaginationStopWarning(editor, run, "oscillation");
        }
      } else if (run.passCount >= PAGINATION_MAX_PASSES) {
        if (!performFullRebuild(editor, state, run, "maxPasses")) {
          run.stopped = true;
          run.stopReason = "maxPasses";
          logPaginationStopWarning(editor, run, "maxPasses");
        }
      }

      if (state.mergeUpPending && run.stopped) {
        const nextBreakPos = getFirstBreakPos(editor);
        const baseline = Number(run.mergeUpBaselineBreakPos);
        if (Number.isFinite(baseline)
          && Number.isFinite(nextBreakPos)
          && nextBreakPos > baseline
          && state.desiredOptions?.debug) {
          console.info("[pagination] merge-up moved block", {
            fromBreakPos: baseline,
            toBreakPos: nextBreakPos
          });
        }
        state.mergeUpPending = false;
      }

      if (run.stopReason === "oscillation" || run.stopReason === "maxPasses") {
        const stoppedAt = Date.now();
        state.lastStopReason = run.stopReason;
        state.lastStopTimestamp = stoppedAt;
        state.lastStopSignature = run.lastSignature;
        state.lastStopDocSignature = getDocSignature(editor);
        state.lastStopViewport = getViewportMetrics(editor);
        state.cooldownUntil = stoppedAt + PAGINATION_COOLDOWN_MS;
      }
      state.lastBreakSignature = run.lastSignature;
    } finally {
      state.isRunning = false;
    }

    if (state.pending && !state.run?.stopped) {
      queuePaginationRun(editor, "coalesced");
    }
  };

  if (!api.__writerPaginationWrapped && typeof api.setPageBreaksEnabled === "function") {
    const originalSetPageBreaksEnabled = api.setPageBreaksEnabled.bind(api);
    const originalRegisterPageBreakObserver = typeof api.registerPageBreakObserver === "function"
      ? api.registerPageBreakObserver.bind(api)
      : null;

    api.setPageBreaksEnabled = function (editor, enabled, options) {
      if (!editor) {
        return 1;
      }

      const state = getPatchedPaginationState(editor);
      if (!state) {
        return originalSetPageBreaksEnabled(editor, enabled, options);
      }

      state.__originalSetPageBreaksEnabled = originalSetPageBreaksEnabled;
      state.desiredEnabled = !!enabled;
      state.desiredOptions = normalizePageBreakOptions(options);
      state.desiredKey = pageBreakOptionsKey(state.desiredEnabled, state.desiredOptions);
      if (!Number.isFinite(state.lastDocSize) || state.lastDocSize <= 0) {
        state.lastDocSize = getDocSize(editor);
      }

      if (!state.desiredEnabled) {
        if (state.rafId) {
          window.cancelAnimationFrame(state.rafId);
          state.rafId = 0;
        }
        if (state.idleTimer) {
          window.clearTimeout(state.idleTimer);
          state.idleTimer = 0;
        }

        state.pending = false;
        state.force = false;
        state.run = null;

        if (state.appliedKey === state.desiredKey) {
          return Number(editor?.__pageGapState?.pageCount ?? 1);
        }

        const count = originalSetPageBreaksEnabled(editor, false, state.desiredOptions);
        state.appliedKey = state.desiredKey;
        return count;
      }

      if (state.appliedKey === state.desiredKey && !state.pending && !state.isRunning) {
        return Number(editor?.__pageGapState?.pageCount ?? 1);
      }

      queuePaginationRun(editor, "setPageBreaksEnabled");
      return Number(editor?.__pageGapState?.pageCount ?? 1);
    };

    if (originalRegisterPageBreakObserver) {
      api.registerPageBreakObserver = function (editor, dotNetRef, options) {
        if (!editor) {
          return;
        }

        const state = getPatchedPaginationState(editor);
        if (!state) {
          originalRegisterPageBreakObserver(editor, dotNetRef, options);
          return;
        }

        state.desiredOptions = normalizePageBreakOptions(options);
        state.desiredEnabled = true;
        state.desiredKey = pageBreakOptionsKey(true, state.desiredOptions);

        if (!state.observerAttached) {
          originalRegisterPageBreakObserver(editor, dotNetRef, options);
          state.observerAttached = true;
        } else {
          editor.__pageBreakState = editor.__pageBreakState || {};
          editor.__pageBreakState.dotNetRef = dotNetRef;
          editor.__pageBreakState.interopState = createInteropState(dotNetRef);
          editor.__pageBreakState.options = state.desiredOptions;
          editor.__pageBreakState.enabled = true;
        }

        queuePaginationRun(editor, "registerObserver", true);
      };
    }

    api.requestPaginationReflow = function (editor, reason) {
      if (!editor) {
        return;
      }

      const state = getPatchedPaginationState(editor);
      if (!state || !state.desiredEnabled) {
        return;
      }

      queuePaginationRun(editor, reason || "reflow");
    };

    api.__writerPaginationWrapped = true;
  }

  const attachPaginationReflowObservers = (editor) => {
    if (!editor || editor.__writerReflowAttached) {
      return;
    }

    // Loop sources for non-convergent cases:
    // ResizeObserver/window resize -> notifyLayoutChanged/requestPaginationReflow -> pagination apply
    // -> spacer DOM/layout mutation -> observer callback again.
    let lastObservedViewport = getViewportMetrics(editor);
    const trigger = createDebounced(() => {
      const state = getPatchedPaginationState(editor);
      if (!state) {
        return;
      }

      const now = Date.now();
      if (Number(state.suppressObserverEventsUntil || 0) > now) {
        return;
      }

      const nextViewport = getViewportMetrics(editor);
      const viewportChanged = hasMeaningfulViewportChange(lastObservedViewport, nextViewport);
      if (!viewportChanged) {
        return;
      }

      lastObservedViewport = nextViewport;
      try {
        api.notifyLayoutChanged?.();
        api.requestPaginationReflow?.(editor, "resize");
      } catch {
      }
    }, 120);

    const root = editor?.view?.dom;
    const ctx = getEditorContext(editor);
    const observed = [root, ctx?.viewport, ctx?.content, ctx?.lane].filter(Boolean);
    const resizeObserver = typeof ResizeObserver === "function"
      ? new ResizeObserver(() => trigger())
      : null;

    if (resizeObserver) {
      observed.forEach((element) => resizeObserver.observe(element));
    }

    const onWindowResize = () => trigger();
    let lastDpr = Number(window.devicePixelRatio || 1);
    const onDprChange = () => {
      const next = Number(window.devicePixelRatio || 1);
      if (Math.abs(next - lastDpr) < 0.001) {
        return;
      }

      lastDpr = next;
      trigger();
    };

    window.addEventListener("resize", onWindowResize, { passive: true });
    window.addEventListener("resize", onDprChange, { passive: true });

    editor.__writerReflowAttached = true;
    editor.__writerReflowCleanup = () => {
      try {
        resizeObserver?.disconnect();
      } catch {
      }
      window.removeEventListener("resize", onWindowResize);
      window.removeEventListener("resize", onDprChange);
      editor.__writerReflowAttached = false;
    };
  };

  const ensureTooltips = () => {
    const roots = document.querySelectorAll(".editor-shell, .projects-page, .landing");
    roots.forEach((root) => {
      const candidates = root.querySelectorAll("button, [role='tab'], a, .project-overflow-toggle");
      candidates.forEach((element) => {
        if (element.hasAttribute("title")) {
          return;
        }

        const fromAria = element.getAttribute("aria-label");
        const text = (fromAria || element.textContent || "").trim();
        if (!text) {
          return;
        }

        element.setAttribute("title", text);
      });
    });
  };

  if (!api.__writerTooltipsInstalled) {
    ensureTooltips();
    const observer = new MutationObserver(() => ensureTooltips());
    observer.observe(document.body, { childList: true, subtree: true });
    api.__writerTooltipsInstalled = true;
  }

  if (!api.__writerContextMenuWrapped && typeof api.attachContextMenu === "function") {
    api.attachContextMenu = function (elementId, dotNetRef) {
      const element = document.getElementById(elementId);
      if (!element) {
        return;
      }

      if (element.__contextMenuHandler) {
        element.removeEventListener("contextmenu", element.__contextMenuHandler);
      }

      const interopState = createInteropState(dotNetRef);
      const handler = (event) => {
        const target = event?.target instanceof Element ? event.target : null;
        const link = target?.closest?.("a[href]");
        if (link && element.contains(link)) {
          event.preventDefault();
          safeInvoke(dotNetRef, interopState, "OnEditorLinkContextMenu", event.clientX, event.clientY, link.getAttribute("href"));
          return;
        }

        event.preventDefault();
        safeInvoke(dotNetRef, interopState, "OnEditorContextMenu", event.clientX, event.clientY);
      };

      element.addEventListener("contextmenu", handler);
      element.__contextMenuHandler = handler;
      element.__contextMenuInteropState = interopState;
    };

    api.__writerContextMenuWrapped = true;
  }

  api.openFilePicker = function (elementId) {
    const input = document.getElementById(elementId);
    if (!input || input.tagName.toLowerCase() !== "input") {
      return;
    }

    input.click();
  };

  api.getBrowserUserAgent = function () {
    try {
      return navigator?.userAgent || "";
    } catch {
      return "";
    }
  };

  if (!api.__waDebugWrapped && typeof api.create === "function") {
    const originalCreate = api.create.bind(api);
    api.create = function (...args) {
      const editor = originalCreate(...args);
      editor.__dotNetRef = args[2] || null;
      if (editor?.view && !editor.__writerPaginationDispatchWrapped) {
        const originalDispatch = editor.view.dispatch.bind(editor.view);
        editor.view.dispatch = (tr) => {
          const patchState = editor.__writerPaginationPatchState;
          const shouldMarkInternal = !!(tr?.setMeta && patchState?.isRunning);
          if (shouldMarkInternal) {
            tr.setMeta(PAGINATION_META_KEY, { internal: true });
          }

          const result = originalDispatch(tr);
          const meta = tr?.getMeta ? tr.getMeta(PAGINATION_META_KEY) : null;
          const isInternal = !!meta?.internal;
          if (patchState) {
            if (isInternal || shouldMarkInternal) {
              patchState.suppressObserverEventsUntil = Date.now() + PAGINATION_OBSERVER_SUPPRESS_MS;
            } else if (tr?.docChanged) {
              const previousDocSize = Number(patchState.lastDocSize || 0);
              const nextDocSize = getDocSize(editor);
              const changedRanges = collectChangedRanges(tr);
              clearPaginationCaches(editor, changedRanges);
              patchState.lastUserInputAt = Date.now();
              patchState.lastKnownDocSignature = getDocSignature(editor);
              patchState.lastDocSize = nextDocSize;
              if (previousDocSize > 0 && nextDocSize < previousDocSize) {
                patchState.mergeUpPending = true;
                if (patchState.desiredOptions?.debug) {
                  console.info("[pagination] merge-up requested", {
                    previousDocSize,
                    nextDocSize
                  });
                }
              }
              patchState.cooldownUntil = 0;
            }
          }

          return result;
        };
        editor.__writerOriginalDispatch = originalDispatch;
        editor.__writerPaginationDispatchWrapped = true;
      }
      maybeDebugEditorLayout(editor);
      attachEditorShortcuts(editor);
      attachLinkInteractions(editor);
      attachPaginationReflowObservers(editor);
      return editor;
    };
    api.__waDebugWrapped = true;
  }

  if (!api.__writerDestroyWrapped && typeof api.destroy === "function") {
    const originalDestroy = api.destroy.bind(api);
    api.destroy = function (editor) {
      const root = editor?.view?.dom;
      if (root?.__writerShortcutHandler) {
        root.removeEventListener("keydown", root.__writerShortcutHandler, true);
        root.__writerShortcutHandler = null;
      }
      if (root?.__writerLinkClickHandler) {
        root.removeEventListener("click", root.__writerLinkClickHandler, true);
        root.__writerLinkClickHandler = null;
      }
      if (editor?.__writerReflowCleanup) {
        editor.__writerReflowCleanup();
        editor.__writerReflowCleanup = null;
      }
      const paginationState = editor?.__writerPaginationPatchState;
      if (paginationState?.idleTimer) {
        window.clearTimeout(paginationState.idleTimer);
        paginationState.idleTimer = 0;
      }
      if (editor?.view && editor.__writerPaginationDispatchWrapped && editor.__writerOriginalDispatch) {
        editor.view.dispatch = editor.__writerOriginalDispatch;
        editor.__writerOriginalDispatch = null;
        editor.__writerPaginationDispatchWrapped = false;
      }
      originalDestroy(editor);
    };
    api.__writerDestroyWrapped = true;
  }
})();
