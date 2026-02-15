(function () {
  const api = window.tiptapEditor;
  if (!api) {
    return;
  }
  console.info("[tiptap-patch] loaded", { version: 10 });

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

      editor.commands.focus();
      try {
        editor.chain().focus().setTextSelection({ from: clampedFrom, to: clampedTo }).run();
      } catch {
        editor.chain().focus().setTextSelection(clampedFrom).run();
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

      editor.commands.focus();
      try {
        editor.chain().focus().setTextSelection({ from: docFrom, to: docTo }).run();
      } catch {
        editor.chain().focus().setTextSelection(docFrom).run();
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
        editor.commands.focus();
        editor.chain().focus().setTextSelection(from).run();
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
          if (matches.length === 0) {
            return {
              ok: false,
              reason: "anchor_not_found",
              issueKey,
              from: range.from,
              to: range.to,
              anchorLength: anchorCandidates[0]?.length || 0
            };
          }

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

      let docFrom = Number(fix.docFrom);
      let docTo = Number(fix.docTo);
      let target = { from: Number(fix.from) || 0, to: Number(fix.to) || 0, source: "doc-captured-range" };
      let anchorMatchedAtRange = false;

      const hasCapturedDocRange = Number.isFinite(docFrom) && Number.isFinite(docTo);
      if (!hasCapturedDocRange) {
        const resolved = resolveTargetRangeFromFix(editor, fix);
        if (!resolved.ok) {
          return buildFixApplyResult(false, false, resolved.reason || "range_resolution_failed", {
            issueKey,
            from: resolved.target?.from ?? null,
            to: resolved.target?.to ?? null,
            source: resolved.target?.source ?? null
          });
        }

        target = resolved.target;
        docFrom = resolved.docFrom;
        docTo = resolved.docTo;
        anchorMatchedAtRange = !!resolved.anchorMatchedAtRange;
      } else {
        target = {
          from: Number.isFinite(Number(fix.from)) ? Number(fix.from) : 0,
          to: Number.isFinite(Number(fix.to)) ? Number(fix.to) : 0,
          source: "doc-captured-range"
        };

        if ((kind === "replace" || kind === "delete") && docTo <= docFrom) {
          return buildFixApplyResult(false, false, "captured_doc_range_invalid", {
            issueKey,
            kind,
            source: target.source,
            docFrom,
            docTo
          });
        }
      }

      const expectedText = typeof fix.expectedText === "string" ? fix.expectedText : null;
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
          return buildFixApplyResult(false, false, "doc_expected_text_mismatch", {
            issueKey,
            kind,
            source: target.source,
            docFrom,
            docTo
          });
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
          anchorMatchedAtRange
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
