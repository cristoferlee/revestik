// Connect a trusted administrative Blazor editor to its public read-only preview.
export function connect(frame, dotnet, token, expectedOrigin) {
    if (!frame || !dotnet || !token || !expectedOrigin) throw new Error("Preview bridge configuration missing");
    let disposed = false;
    const sendHandshake = () => {
        if (disposed || !frame.contentWindow) return;
        frame.contentWindow.postMessage({ type: "revestik-preview-enable", token }, expectedOrigin);
    };
    const onMessage = event => {
        if (disposed || event.origin !== expectedOrigin || event.source !== frame.contentWindow) return;
        const data = event.data;
        if (!data || data.type !== "revestik-preview-edit" || data.token !== token || typeof data.action !== "string") return;
        if (!/^(hero:[123]|products|solutions|advantages|contact)$/.test(data.action)) return;
        dotnet.invokeMethodAsync("OnPreviewEdit", data.action).catch(() => {});
    };
    frame.addEventListener("load", sendHandshake);
    window.addEventListener("message", onMessage);
    sendHandshake();
    return {
        dispose() {
            disposed = true;
            frame.removeEventListener("load", sendHandshake);
            window.removeEventListener("message", onMessage);
        }
    };
}
