// PUBLIC-PROJECTS-01D: authorized contextual controls for persisted projects.
// API authorization, antiforgery, validation and concurrency remain server-side.
let inserted = [];
let active = false;

async function read(path, options = {}) {
    const response = await fetch(path, {
        credentials: 'same-origin', cache: 'no-store', ...options
    });
    if (!response.ok) {
        let message = `HTTP ${response.status}`;
        try { message = (await response.json())?.message || message; } catch { /* non-JSON error */ }
        throw new Error(message);
    }
    if (response.status === 204) return null;
    return response.json();
}

async function antiforgery() {
    const result = await read('/api/auth/csrf');
    if (!result?.requestToken) throw new Error('CSRF token unavailable');
    return result.requestToken;
}

async function write(path, method, data) {
    const headers = { 'X-CSRF-TOKEN': await antiforgery() };
    if (data !== undefined) headers['Content-Type'] = 'application/json';
    return read(path, { method, headers, ...(data === undefined ? {} : { body: JSON.stringify(data) }) });
}

function action(label, callback) {
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = label;
    button.className = 'revestik-product-add-button';
    button.addEventListener('click', e => {
        e.preventDefault();
        e.stopPropagation();
        if (!active) return;
        void callback();
    });
    return button;
}

export function attach(dotnet) {
    detach();
    active = true;
    const grid = document.querySelector('.projects-index .featured-projects-grid');
    if (!grid) return;

    for (const card of grid.querySelectorAll('.featured-project[data-editor-project-id]')) {
        const id = Number(card.dataset.editorProjectId);
        if (!Number.isSafeInteger(id) || id <= 0) continue;
        const actions = document.createElement('div');
        actions.className = 'revestik-catalog-admin-actions';
        const edit = document.createElement('button');
        edit.type = 'button';
        edit.className = 'revestik-catalog-action';
        edit.textContent = '✎ Editar proyecto';
        edit.addEventListener('click', event => {
            event.preventDefault();
            event.stopPropagation();
            if (!active) return;
            void dotnet.invokeMethodAsync('EditProjectAsync', id).catch(() => {});
        });
        actions.append(edit);
        card.append(actions);
        inserted.push(actions);
    }

    const add = document.createElement('div');
    add.className = 'revestik-product-add-card';
    const button = action('+', () =>
        dotnet.invokeMethodAsync('NewProjectAsync').catch(() => {}));
    button.setAttribute('aria-label', 'Agregar nuevo proyecto');
    const label = document.createElement('span');
    label.textContent = 'Agregar nuevo proyecto';
    button.append(label);
    add.append(button);
    grid.append(add);
    inserted.push(add);

    // Keep the manager in the page header; cards stay compact like Acabados.
    const manage = document.createElement('div');
    manage.className = 'revestik-projects-manage';
    const manageButton = document.createElement('button');
    manageButton.type = 'button';
    manageButton.className = 'revestik-catalog-action';
    manageButton.textContent = '✎ Administrar proyectos publicados y no publicados';
    manageButton.addEventListener('click', e => {
        e.preventDefault();
        if (!active) return;
        void dotnet.invokeMethodAsync('ManageProjectsAsync').catch(() => {});
    });
    manage.append(manageButton);
    const heading = document.querySelector('.projects-index .projects-index-header');
    if (heading) heading.append(manage);
    else grid.insertAdjacentElement('beforebegin', manage);
    inserted.push(manage);
}

export function detach() {
    active = false;
    for (const item of inserted) item.remove();
    inserted = [];
}

export const listProjects = page => read(`/api/admin/public-projects?page=${encodeURIComponent(page)}`);
export const getProject = id => read(`/api/admin/public-projects/${encodeURIComponent(id)}`);
export const createProject = data => write('/api/admin/public-projects', 'POST', data);
export const updateProject = (id, data) => write(`/api/admin/public-projects/${encodeURIComponent(id)}/content`, 'PUT', data);
export const publishProject = (id, version, makePublished) =>
    write(`/api/admin/public-projects/${encodeURIComponent(id)}/${makePublished ? 'publish' : 'unpublish'}`, 'POST', { version });
export const deleteProject = (id, version) =>
    write(`/api/admin/public-projects/${encodeURIComponent(id)}?version=${encodeURIComponent(version)}`, 'DELETE');

export async function stageImage(input, role) {
    const file = input?.files?.[0];
    if (!file || !file.size || file.size > 5 * 1024 * 1024 ||
        !['image/jpeg', 'image/png', 'image/webp'].includes(file.type))
        throw new Error('Seleccioná una imagen JPG, PNG o WebP de hasta 5 MB.');
    if (!['ProjectCover', 'ProjectGallery'].includes(role)) throw new Error('Invalid project image role');
    const body = new FormData();
    body.append('file', file, file.name);
    body.append('role', role);
    const upload = await read('/api/admin/public-projects/images/stage', {
        method: 'POST', headers: { 'X-CSRF-TOKEN': await antiforgery() }, body
    });
    return { token: upload.token, name: file.name, role, displayOrder: 0,
        altEs: '', altEn: '', altDe: '' };
}

export const discardImage = token => /^[a-f0-9]{32}$/i.test(token || '')
    ? write(`/api/admin/public-projects/images/staged/${token}`, 'DELETE')
    : Promise.resolve();

export function refreshPage() { window.location.reload(); }
