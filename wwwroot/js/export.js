export function downloadFile(base64Data, mimeType, fileName) {
    if (!base64Data) {
        return;
    }

    const binary = atob(base64Data);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }

    const blob = new Blob([bytes], { type: mimeType || "application/octet-stream" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName || "document";
    link.style.display = "none";
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
}
<<<<<<< HEAD

export function printHtmlAsPdf(html) {
    if (!html) {
        return;
    }

    const printWindow = window.open("", "_blank");
    if (!printWindow) {
        return;
    }

    printWindow.document.open();
    printWindow.document.write(html);
    printWindow.document.close();

    const triggerPrint = () => {
        printWindow.focus();
        printWindow.print();
    };

    if (printWindow.document.readyState === "complete") {
        setTimeout(triggerPrint, 50);
    } else {
        printWindow.onload = () => setTimeout(triggerPrint, 50);
    }
}

if (!window.writerExport) {
    window.writerExport = {};
}

window.writerExport.printHtmlAsPdf = printHtmlAsPdf;
=======
>>>>>>> ebb7526 (Implemented export of md and html)
