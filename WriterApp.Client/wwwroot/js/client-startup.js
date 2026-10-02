// Keep Blazor's integrity checks while recovering from stale HTTP cache entries.
// JavaScript runtime modules use Blazor's own module loader.
export function createBootResourceLoader(fetchResource = globalThis.fetch) {
    return function loadBootResource(type, name, defaultUri, integrity) {
        if (type === "dotnetjs") return null;

        const fetchChecked = async (uri, cache) => {
            const response = await fetchResource(uri, { cache, integrity });
            if (!response.ok) throw new Error(`Unable to load ${name} (${response.status}).`);
            return response;
        };

        return fetchChecked(defaultUri, "no-cache").catch(() => {
            // A distinct URL also avoids a cached error response in embedded browsers.
            // Retry once; never run bytes that fail the supplied integrity check.
            const separator = defaultUri.includes("?") ? "&" : "?";
            return fetchChecked(`${defaultUri}${separator}boot-retry=1`, "reload");
        });
    };
}

export async function startClient(blazor, document) {
    try {
        await blazor.start({ loadBootResource: createBootResourceLoader() });
    } catch (error) {
        console.error("Prosa client startup failed.", error);
        const root = document.getElementById("app");
        if (root?.querySelector(".loading-progress")) {
            const message = document.createElement("p");
            message.textContent = "Prosa couldn't start. Reload the page to try again.";
            message.setAttribute("role", "alert");
            root.replaceChildren(message);
        }
        const errorUi = document.getElementById("blazor-error-ui");
        if (errorUi) errorUi.style.display = "block";
    }
}

if (typeof window !== "undefined") {
    await startClient(window.Blazor, window.document);
}
