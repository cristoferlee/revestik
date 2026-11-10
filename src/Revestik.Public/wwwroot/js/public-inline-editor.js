// Only imported by the authenticated Inline Server editor island.
// Privileged requests reuse the existing same-origin Identity + CSRF API.
let controls = [];

async function authorizedRequest(path, options = {}) {
    const response = await fetch(path, {
        credentials: 'same-origin',
        cache: 'no-store',
        ...options
    });
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
    }
    if (response.status === 204) return null;
    return response.json();
}

async function csrf() {
    const response = await authorizedRequest('/api/auth/csrf');
    if (!response?.requestToken) throw new Error('CSRF token not available');
    return response.requestToken;
}

function addControl(target, label, field, dotnet, position = 'afterend') {
    if (!target) return;
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'revestik-editor-trigger';
    button.textContent = `✎ ${label}`;
    button.setAttribute('aria-label', `${label} en la diapositiva actual`);
    button.addEventListener('click', async () => {
        const slides = Array.from(document.querySelectorAll('[data-hero-slide]'));
        const active = document.querySelector('[data-hero-slide].is-active');
        const slot = Math.max(1, slides.indexOf(active) + 1);
        try { await dotnet.invokeMethodAsync('BeginEdit', slot, field); }
        catch { /* A disconnected editor cannot process clicks. */ }
    });
    target.insertAdjacentElement(position, button);
    controls.push(button);
}

function addSectionControl(target, label, key, field, dotnet, index = -1, position = 'afterend', modifier = '') {
    if (!target) return;
    const button = document.createElement('button');
    button.type = 'button';
    button.className = `revestik-editor-trigger${modifier ? ' ' + modifier : ''}`;
    button.textContent = `✎ ${label}`;
    button.setAttribute('aria-label', `${label} · ${key}${index >= 0 ? ` · tarjeta ${index + 1}` : ''}`);
    button.addEventListener('click', async () => {
        try { await dotnet.invokeMethodAsync('BeginSectionEdit', key, field, index); }
        catch { /* A disconnected editor cannot process clicks. */ }
    });
    target.insertAdjacentElement(position, button);
    controls.push(button);
}

function addHomeSectionControls(dotnet) {
    const sections = [
        ['.featured-products', 'products'],
        ['.home-solutions', 'solutions'],
        ['.home-advantages', 'advantages'],
        ['.home-contact-cta', 'contact']
    ];
    for (const [selector, key] of sections) {
        const section = document.querySelector(selector);
        if (!section) continue;
        addSectionControl(section.querySelector('.home-section-eyebrow, .featured-products-eyebrow'), 'Editar etiqueta', key, 'eyebrow', dotnet);
        addSectionControl(section.querySelector('h2'), 'Editar título', key, 'title', dotnet);
        // Products description is optional in SSR if there is no saved section yet.
        const description = section.querySelector(key === 'products' ? '.featured-products-header p:not(.featured-products-eyebrow)' :
            key === 'solutions' ? '.home-solutions-copy > p:not(.home-section-eyebrow)' :
            key === 'advantages' ? '.home-advantages-heading > p:not(.home-section-eyebrow)' :
            '.home-contact-cta-inner > div:first-child > p:not(.home-section-eyebrow)');
        addSectionControl(description || section.querySelector('h2'), 'Editar descripción', key, 'description', dotnet);
        if (key === 'solutions') {
            addSectionControl(section, 'Cambiar fotografía', key, 'image', dotnet, -1, 'beforeend',
                'revestik-editor-trigger--solutions-photo');
        }
    }
    for (const [index, card] of Array.from(document.querySelectorAll('.home-advantages-grid .home-advantage')).entries()) {
        if (index >= 4) break;
        addSectionControl(card.querySelector('h3'), 'Editar título', 'advantages', 'card-title', dotnet, index);
        addSectionControl(card.querySelector('p'), 'Editar descripción', 'advantages', 'card-description', dotnet, index);
    }
}

export function attach(dotnet) {
    detach();

    // Acabados reuses the public hero styles; its data is intentionally distinct.
    // Never bind Home's edit endpoints to the Acabados hero.
    const home = document.querySelector('.home-hero:not(.finishes-hero)');
    if (home) {
        addControl(document.querySelector('[data-hero-heading]'), 'Editar título', 'heading', dotnet);
        addControl(document.querySelector('[data-hero-copy]'), 'Editar descripción', 'description', dotnet);
        addControl(home, 'Cambiar fotografía', 'image', dotnet, 'beforeend');
        addHomeSectionControls(dotnet);
    }

    const bar = document.createElement('nav');
    bar.className = 'revestik-editor-toolbar';
    bar.setAttribute('aria-label', 'Modo de edición');
    const caption = document.createElement('span');
    caption.textContent = home ? 'Modo editor · Inicio' :
        document.querySelector('.finishes-page') ? 'Modo editor · Acabados' :
        'Modo editor · Controles disponibles por ahora en Inicio';
    bar.append(caption);
    if (!home) {
        const homeLink = document.createElement('a');
        homeLink.href = `/${location.pathname.split('/')[1] || 'es'}?edit=1`;
        homeLink.textContent = 'Editar Inicio';
        bar.append(homeLink);
    }
    const back = document.createElement('a');
    back.href = '/administracion/settings/public-site';
    back.textContent = 'Administración';
    const exit = document.createElement('a');
    const ordinary = new URL(location.href);
    ordinary.searchParams.set('edit', '0');
    exit.href = ordinary.pathname + ordinary.search + ordinary.hash;
    exit.textContent = 'Salir del editor';
    bar.append(back, exit);
    document.body.append(bar);
    controls.push(bar);
}

export function detach() {
    for (const control of controls) control.remove();
    controls = [];
}

export async function getSlides() {
    return authorizedRequest('/api/admin/public-site/home-hero');
}

export async function getSections() {
    return authorizedRequest('/api/admin/public-site/home-sections');
}

export async function stageImage(element) {
    const file = element?.files?.[0];
    if (!file || !file.size || file.size > 5 * 1024 * 1024 ||
        !['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
        throw new Error('Invalid image');
    }
    const body = new FormData();
    body.append('file', file, file.name);
    body.append('role', 'ProductCover');
    const result = await authorizedRequest('/api/admin/public-catalog/images/stage', {
        method: 'POST',
        headers: { 'X-CSRF-TOKEN': await csrf() },
        body
    });
    return { token: result.token, name: file.name };
}

export async function discardImage(token) {
    if (!/^[a-f\d]{32}$/i.test(token)) return;
    await authorizedRequest(`/api/admin/public-catalog/images/staged/${token}`, {
        method: 'DELETE',
        headers: { 'X-CSRF-TOKEN': await csrf() }
    });
}

export async function saveSlide(slot, data) {
    if (!Number.isInteger(slot) || slot < 1 || slot > 3)
        throw new Error('Invalid slide');
    await authorizedRequest(`/api/admin/public-site/home-hero/${slot}`, {
        method: 'PUT',
        headers: { 'X-CSRF-TOKEN': await csrf(), 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
    });
}

export async function saveSection(key, data) {
    if (!['products', 'solutions', 'advantages', 'contact'].includes(key))
        throw new Error('Invalid section');
    await authorizedRequest(`/api/admin/public-site/home-sections/${key}`, {
        method: 'PUT',
        headers: { 'X-CSRF-TOKEN': await csrf(), 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
    });
}

export function refreshPage() {
    location.reload();
}
