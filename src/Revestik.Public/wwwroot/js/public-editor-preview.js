// Optional read-only public preview controls. No administrative credentials or API calls.
(() => {
    if (window === window.parent) return;
    const referringOrigin = (() => {
        try { return new URL(document.referrer).origin; } catch { return null; }
    })();
    // HTTPS admin -> HTTP localhost preview is a downgrade, so browsers may
    // intentionally omit Referer. Trust only the known administrative origins.
    const authorizedEditors = new Set([
        "https://localhost:7081",
        "https://localhost:5181",
        "https://app.revestikcr.com"
    ]);

    let editorOrigin = null;
    let token = null;
    const allowed = [
        [".home-hero", [["hero:1", "Editar 1"], ["hero:2", "Editar 2"], ["hero:3", "Editar 3"]]],
        [".featured-products", [["products", "Editar productos destacados"]]],
        [".home-solutions", [["solutions", "Editar Soluciones"]]],
        [".home-advantages", [["advantages", "Editar esta sección"]]],
        [".home-contact-cta", [["contact", "Editar Contacto"]]]
    ];
    function showControls() {
        if (!document.querySelector("#revestik-editor-preview-style")) {
            const style = document.createElement("style");
            style.id = "revestik-editor-preview-style";
            style.textContent = `
                .revestik-editor-target{position:relative!important}
                .revestik-editor-controls{position:absolute;right:12px;top:12px;z-index:90;display:flex;flex-wrap:wrap;justify-content:flex-end;gap:5px;max-width:calc(100% - 24px);padding:5px;border-radius:9px;background:rgba(21,32,45,.94);box-shadow:0 4px 18px #0003}
                .revestik-editor-controls button{appearance:none;display:inline-flex;align-items:center;justify-content:center;background:#ffbd59;color:#18222d;border:1px solid #d6a036;border-radius:6px;padding:9px 11px;font:700 12px/1.3 system-ui,sans-serif;cursor:pointer;min-height:36px}
                .revestik-editor-controls button:hover{background:#ffd18b}
                .revestik-editor-controls button:focus-visible{outline:3px solid white;outline-offset:2px}
                .revestik-editor-add{border:2px dashed #aeb5be;border-radius:12px;background:#f0f1f3;color:#364452;min-height:210px;display:flex;align-items:center;justify-content:center;gap:1rem;flex-direction:column;font:600 15px system-ui;cursor:pointer}
                .revestik-editor-add span{font-size:46px;font-weight:300}
                @media(max-width:600px){.revestik-editor-controls{top:8px;right:8px;gap:4px}.revestik-editor-controls button{font-size:11px;padding:7px 9px}}
            `;
            document.head.appendChild(style);
        }
        // Catalog cards are read-only public DOM. Only the authenticated parent
        // editor can activate these controls using the established origin/token bridge.
        for (const card of document.querySelectorAll('.catalog-card[data-editor-product-id], .catalog-product-detail[data-editor-product-id]')) {
            if (card.querySelector('.revestik-editor-controls')) continue;
            const id = card.getAttribute('data-editor-product-id');
            if (!/^[1-9]\d*$/.test(id || '')) continue;
            card.classList.add('revestik-editor-target');
            const actions = document.createElement('div');
            actions.className = 'revestik-editor-controls';
            const button = document.createElement('button');
            button.type = 'button';
            button.textContent = 'Editar';
            button.setAttribute('aria-label', 'Editar producto');
            button.addEventListener('click', event => {
                event.stopPropagation(); event.preventDefault();
                if (editorOrigin && token) window.parent.postMessage({ type:'revestik-preview-edit', token, action:`product:${id}` }, editorOrigin);
            });
            actions.appendChild(button);
            card.appendChild(actions);
        }
        const grid = document.querySelector('.catalog-grid');
        if (grid && !grid.querySelector('.revestik-editor-add')) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'revestik-editor-add';
            button.innerHTML = '<span aria-hidden="true">+</span><strong>Agregar nuevo producto</strong>';
            button.setAttribute('aria-label', 'Agregar nuevo producto');
            button.addEventListener('click', event => {
                event.preventDefault(); event.stopPropagation();
                if (editorOrigin && token) window.parent.postMessage({ type:'revestik-preview-edit', token, action:'product:new' }, editorOrigin);
            });
            grid.appendChild(button);
        }
        for (const [selector, buttons] of allowed) {
            const target = document.querySelector(selector);
            if (!target || target.querySelector(".revestik-editor-controls")) continue;
            target.classList.add("revestik-editor-target");
            const controls = document.createElement("div");
            controls.className = "revestik-editor-controls";
            controls.setAttribute("aria-label", "Opciones de edición de esta sección");
            for (const [action, title] of buttons) {
                const button = document.createElement("button");
                button.type = "button";
                button.textContent = title;
                button.setAttribute("aria-label", title);
                button.addEventListener("click", event => {
                    event.stopPropagation();
                    event.preventDefault();
                    if (editorOrigin && token) window.parent.postMessage({ type: "revestik-preview-edit", token, action }, editorOrigin);
                });
                controls.appendChild(button);
            }
            target.appendChild(controls);
        }
    }
    window.addEventListener("message", event => {
        // Internal public navigation changes document.referrer to the previous public page.
        // Only enforce a referring origin if it is itself a known administrative origin.
        if (event.source !== window.parent || !authorizedEditors.has(event.origin) ||
            (referringOrigin && authorizedEditors.has(referringOrigin) && event.origin !== referringOrigin)) return;
        const data = event.data;
        if (!data || typeof data.token !== "string" || !/^[a-f0-9]{32}$/.test(data.token)) return;
        if (data.type === 'revestik-preview-enable') {
            editorOrigin = event.origin;
            token = data.token;
            showControls();
            window.parent.postMessage({
                type: 'revestik-preview-location', token,
                path: window.location.pathname + window.location.search
            }, editorOrigin);
        } else if (event.origin === editorOrigin && data.token === token &&
                   data.type === 'revestik-preview-refresh') {
            window.location.reload();
        } else if (event.origin === editorOrigin && data.token === token &&
                   data.type === 'revestik-preview-language' &&
                   ['es', 'en', 'de'].includes(data.language)) {
            const link = document.querySelector(`.languages a[lang="${data.language}"]`);
            if (link && new URL(link.href).origin === window.location.origin)
                window.location.assign(link.href);
        }
    });
})();
