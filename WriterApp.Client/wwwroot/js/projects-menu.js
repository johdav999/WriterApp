window.writerProjectsMenu = window.writerProjectsMenu || {
    getAnchorLayout: function (element) {
        if (!element || typeof element.getBoundingClientRect !== "function") {
            return null;
        }

        const rect = element.getBoundingClientRect();
        return {
            left: rect.left,
            top: rect.top,
            right: rect.right,
            bottom: rect.bottom,
            width: rect.width,
            height: rect.height,
            viewportWidth: window.innerWidth || document.documentElement.clientWidth || 0,
            viewportHeight: window.innerHeight || document.documentElement.clientHeight || 0
        };
    },
    getViewport: function () {
        return {
            width: window.innerWidth || document.documentElement.clientWidth || 0,
            height: window.innerHeight || document.documentElement.clientHeight || 0
        };
    }
};
