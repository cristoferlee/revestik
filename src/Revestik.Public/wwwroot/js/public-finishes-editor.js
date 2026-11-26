// Acabados editor: presentation only. The API is authoritative for every write.
// Loaded only for a server-authorized Administrator edit-mode page.
const controls = [];
let alive = false;

async function request(path, options = {}) {
    const response = await fetch(path, { credentials: 'same-origin', cache: 'no-store', ...options });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    if (response.status === 204) return null;
    return response.json();
}

async function token() {
    const result = await request('/api/auth/csrf');
    if (!result?.requestToken) throw new Error('CSRF unavailable');
    return result.requestToken;
}

async function mutate(path, method, data) {
    const headers = { 'X-CSRF-TOKEN': await token() };
    if (data !== undefined) headers['Content-Type'] = 'application/json';
    return request(path, { method, headers, ...(data === undefined ? {} : { body: JSON.stringify(data) }) });
}

function makeButton(label, className, handler) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = className;
    button.textContent = label;
    button.addEventListener('click', event => {
        event.preventDefault();
        event.stopPropagation();
        void handler();
    });
    return button;
}

function safeInvoke(dotnet, method, ...args) {
    if (!alive) return;
    void dotnet.invokeMethodAsync(method, ...args).catch(() => {
        // A disconnected circuit or an expired session cannot perform writes.
    });
}

function cardControls(card, dotnet, slug, id, featured, isPublished = true) {
    const actions = document.createElement('div');
    actions.className = 'revestik-catalog-admin-actions';
    actions.append(makeButton('✎ Editar', 'revestik-catalog-action', () => safeInvoke(dotnet, 'EditProductAsync', id)));
    actions.append(makeButton('Eliminar', 'revestik-catalog-action revestik-catalog-danger', () => safeInvoke(dotnet, 'AskDeleteProductAsync', id)));
    actions.append(makeButton(isPublished ? 'Despublicar' : 'Publicar', 'revestik-catalog-action',
        () => safeInvoke(dotnet, 'AskPublicationAsync', id)));
    if (slug) {
        actions.append(makeButton(featured.includes(slug) ? '★ Destacado' : '☆ Destacar',
            'revestik-catalog-action', () => safeInvoke(dotnet, 'ToggleFeaturedAsync', slug)));
    }
    card.append(actions);
    controls.push(actions);
}

function addHeroButtons(dotnet) {
    const hero = document.querySelector('.finishes-page .finishes-hero');
    if (!hero) return;
    const add = (target, label, field, position = 'afterend') => {
        if (!target) return;
        const button = makeButton('✎ ' + label,
            'revestik-editor-trigger revestik-finishes-hero-trigger', () => {
                const slides = Array.from(hero.querySelectorAll('[data-hero-slide]'));
                const current = hero.querySelector('[data-hero-slide].is-active');
                const slot = Math.max(1, slides.indexOf(current) + 1);
                safeInvoke(dotnet, 'BeginHeroEditAsync', slot, field);
            });
        target.insertAdjacentElement(position, button);
        controls.push(button);
    };
    add(hero.querySelector('[data-hero-heading]'), 'Editar título', 'heading');
    add(hero.querySelector('[data-hero-copy]'), 'Editar descripción', 'description');
    add(hero, 'Cambiar fotografía', 'image', 'beforeend');
}

function addProductTile(dotnet) {
    const grid = document.querySelector('.finishes-page .catalog-grid');
    if (!grid) return;
    const card = document.createElement('div');
    card.className = 'revestik-product-add-card';
    const create = makeButton('+', 'revestik-product-add-button', () => safeInvoke(dotnet, 'NewProductAsync'));
    create.setAttribute('aria-label', 'Agregar nuevo producto');
    const label = document.createElement('span');
    label.textContent = 'Agregar nuevo producto';
    create.append(label);
    card.append(create);
    grid.append(card);
    controls.push(card);
}

async function addUnpublishedControls(dotnet, featured) {
    const grid = document.querySelector('.finishes-page .catalog-grid');
    if (!grid || !alive) return;
    // Keep the administrative-only collection bounded and request it only on opt-in.
    let page = 1;
    let loaded = 0;
    while (alive && page <= 8) {
        const response = await request(`/api/admin/public-catalog/products?page=${page}&pageSize=50`);
        for (const item of response.items || []) {
            if (item.isPublished || !alive) continue;
            const card = document.createElement('article');
            card.className = 'catalog-card revestik-admin-unpublished';
            const copy = document.createElement('div');
            copy.className = 'catalog-card-copy';
            const title = document.createElement('h3');
            title.textContent = item.nameEs || item.slug;
            const status = document.createElement('p');
            status.textContent = 'No publicado · visible solo en administración';
            copy.append(title, status);
            card.append(copy);
            grid.append(card);
            controls.push(card);
            cardControls(card, dotnet, item.slug, item.id, featured, false);
        }
        loaded += (response.items || []).length;
        if (loaded >= (response.total || 0) || !(response.items || []).length) break;
        page++;
    }
}

export async function attach(dotnet) {
    detach();
    alive = true;
    const grid = document.querySelector('.finishes-page .catalog-grid');
    if (!grid) return;
    addHeroButtons(dotnet);
    let featured = [];
    try {
        const sections = await request('/api/admin/public-site/home-sections');
        featured = sections.find(x => x.key === 'products')?.featuredSlugs || [];
    } catch { /* Rendering editor actions must not depend on Home's availability. */ }
    if (!alive) return;
    for (const card of grid.querySelectorAll('.catalog-card[data-editor-product-id]')) {
        const id = Number(card.dataset.editorProductId);
        if (!Number.isSafeInteger(id) || id < 1) continue;
        const href = card.querySelector('a[href]')?.getAttribute('href');
        let slug = '';
        try { slug = decodeURIComponent(new URL(href, location.href).pathname.split('/').pop() || ''); }
        catch { /* Keep edit and delete even if a product URL is malformed. */ }
        cardControls(card, dotnet, slug, id, featured);
    }
    addProductTile(dotnet);
    const filters = document.querySelector('.finishes-page .finishes-sidebar');
    if (filters) {
        const label = document.createElement('label');
        label.className = 'revestik-catalog-admin-toggle';
        const checkbox = document.createElement('input');
        checkbox.type = 'checkbox';
        label.append(checkbox, document.createTextNode(' Mostrar no publicados'));
        checkbox.addEventListener('change', async () => {
            if (!alive) return;
            // Ignore toggle clicks while a request is already running.
            checkbox.disabled = true;
            try {
                if (checkbox.checked) await addUnpublishedControls(dotnet, featured);
                else {
                    for (const node of document.querySelectorAll('.revestik-admin-unpublished')) {
                        node.remove();
                    }
                }
            } catch { checkbox.checked = false; }
            finally { checkbox.disabled = false; }
        });
        filters.append(label);
        controls.push(label);
    }
}

export function detach() {
    alive = false;
    for (const node of controls) node.remove();
    controls.length = 0;
}

export const getCategories = () => request('/api/admin/public-catalog/categories');
export const getSubcategories = () => request('/api/admin/public-catalog/subcategories');
export const getUnits = () => request('/api/units-of-measure/?includeInactive=false');
export const getProduct = id => request(`/api/admin/public-catalog/products/${encodeURIComponent(id)}`);
export const getSections = () => request('/api/admin/public-site/home-sections');
export const getHeroSlides = () => request('/api/admin/public-site/finishes-hero');
export const saveHeroSlide = (slot, data) => {
    if (!Number.isSafeInteger(slot) || slot < 1 || slot > 3) throw new Error('Invalid slot');
    return mutate(`/api/admin/public-site/finishes-hero/${slot}`, 'PUT', data);
};

export async function stageImage(element, role = 'ProductCover') {
    const file = element?.files?.[0];
    if (!file || !file.size || file.size > 5 * 1024 * 1024 ||
        !['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) throw new Error('Invalid image');
    const body = new FormData();
    body.append('file', file, file.name);
    body.append('role', role);
    const result = await request('/api/admin/public-catalog/images/stage', {
        method: 'POST', headers: { 'X-CSRF-TOKEN': await token() }, body
    });
    return { token: result.token, name: file.name };
}
export function discardImage(tokenId) {
    if (!/^[a-f\d]{32}$/i.test(tokenId)) return Promise.resolve();
    return mutate(`/api/admin/public-catalog/images/staged/${tokenId}`, 'DELETE');
}
export const createProduct = data => mutate('/api/admin/public-catalog/products', 'POST', data);
export const saveProduct = (id, data) => mutate(`/api/admin/public-catalog/products/${id}/content`, 'PUT', data);
export const setPublication = (id, version, publish) =>
    mutate(`/api/admin/public-catalog/products/${id}/${publish ? 'publish' : 'unpublish'}`, 'POST', { version });
export const deleteProduct = (id, version) =>
    mutate(`/api/admin/public-catalog/products/${id}?version=${encodeURIComponent(version)}`, 'DELETE');
export const saveSection = (key, data) => mutate(`/api/admin/public-site/home-sections/${encodeURIComponent(key)}`, 'PUT', data);
export const refreshPage = () => location.reload();
