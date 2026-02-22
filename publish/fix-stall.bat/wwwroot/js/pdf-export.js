window.printHtmlAsPdf = function (html) {
    const frame = document.createElement("iframe");
    frame.style.position = "fixed";
    frame.style.right = "0";
    frame.style.bottom = "0";
    frame.style.width = "0";
    frame.style.height = "0";
    frame.style.border = "0";

    document.body.appendChild(frame);

    const doc = frame.contentDocument || frame.contentWindow.document;
    doc.open();
    doc.write(html);
    doc.close();

    frame.contentWindow.focus();
    frame.contentWindow.print();

    setTimeout(() => {
        document.body.removeChild(frame);
    }, 1000);
};
