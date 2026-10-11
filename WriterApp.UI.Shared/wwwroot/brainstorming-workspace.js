export function composerSelection(element) {
    return { start: element.selectionStart, end: element.selectionEnd, direction: element.selectionDirection || "none" };
}
export function restoreComposer(element, selection) {
    if (!element?.isConnected || element.readOnly || element.closest("[hidden]")) return;
    element.focus({ preventScroll: true });
    element.setSelectionRange(Math.min(selection.start,element.value.length),Math.min(selection.end,element.value.length),selection.direction);
}
export function voiceLifetime(receiver) {
    const stop=()=>receiver.invokeMethodAsync('BackgroundVoice').catch(()=>{});
    const hidden=()=>{if(document.hidden)stop();};
    document.addEventListener('visibilitychange',hidden);window.addEventListener('pagehide',stop);
    return {dispose(){document.removeEventListener('visibilitychange',hidden);window.removeEventListener('pagehide',stop);}};
}
