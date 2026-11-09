// One authenticated administrative bridge for every page of the public iframe.
// The public page never receives credentials or permission to call administrative APIs.
export function connect(frame, dotnet, token, expectedOrigin) {
    if (!frame || !dotnet || !/^[a-f0-9]{32}$/.test(token || '') || !expectedOrigin)
        throw new Error('Preview bridge configuration missing');
    let disposed = false;
    const send = (type, extras = {}) => {
        if (!disposed && frame.contentWindow)
            frame.contentWindow.postMessage({ type, token, ...extras }, expectedOrigin);
    };
    const onLoad = () => send('revestik-preview-enable');
    const onMessage = event => {
        if (disposed || event.origin !== expectedOrigin || event.source !== frame.contentWindow) return;
        const data = event.data;
        if (!data || data.token !== token) return;
        if (data.type === 'revestik-preview-location' && typeof data.path === 'string' &&
            data.path.length < 2048 && /^\/(es|en|de)(\/|\?|$)/.test(data.path)) {
            dotnet.invokeMethodAsync('OnPreviewLocation', data.path).catch(() => {});
        } else if (data.type === 'revestik-preview-edit' && typeof data.action === 'string' &&
            /^(hero:[123]|products|solutions|advantages|contact|product:(new|[1-9]\d*))$/.test(data.action)) {
            dotnet.invokeMethodAsync('OnPreviewEdit', data.action).catch(() => {});
        }
    };
    frame.addEventListener('load', onLoad);
    window.addEventListener('message', onMessage);
    onLoad();
    return {
        navigate(relativePath) {
            if (disposed || typeof relativePath !== 'string' ||
                !/^\/(es|en|de)(\/|$)/.test(relativePath) || relativePath.includes('?') || relativePath.includes('#')) return;
            frame.src = new URL(relativePath, expectedOrigin).href;
        },
        changeLanguage(language) {
            if (['es', 'en', 'de'].includes(language)) send('revestik-preview-language', { language });
        },
        refresh() { send('revestik-preview-refresh'); },
        dispose() {
            disposed = true;
            frame.removeEventListener('load', onLoad);
            window.removeEventListener('message', onMessage);
        }
    };
}
