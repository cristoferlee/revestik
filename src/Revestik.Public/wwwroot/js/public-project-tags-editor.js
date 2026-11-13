// PUBLIC-PROJECTS-01C2. The server validates all coordinates, versions, product visibility and CSRF.
let bound = null;

async function read(path, options = {}) {
    const response = await fetch(path, { credentials: 'same-origin', cache: 'no-store', ...options });
    if (!response.ok) {
        let reason = `HTTP ${response.status}`;
        try { reason = (await response.json())?.message || reason; } catch { /* no JSON body */ }
        throw new Error(reason);
    }
    if (response.status === 204) return null;
    return response.json();
}
async function write(path, method, payload) {
    const csrf = await read('/api/auth/csrf');
    if (!csrf?.requestToken) throw new Error('CSRF token unavailable');
    const headers = { 'X-CSRF-TOKEN': csrf.requestToken };
    if (payload !== undefined) headers['Content-Type'] = 'application/json';
    return read(path, { method, headers, ...(payload === undefined ? {} : { body: JSON.stringify(payload) }) });
}
export const listTags = imageId => read(`/api/admin/public-projects/images/${encodeURIComponent(imageId)}/tags`);
export const searchProducts = search => read(`/api/admin/public-projects/products?search=${encodeURIComponent(search)}`);
export const createTag = (imageId, payload) =>
    write(`/api/admin/public-projects/images/${encodeURIComponent(imageId)}/tags`, 'POST', payload);
export const updateTag = (imageId, tagId, payload) =>
    write(`/api/admin/public-projects/images/${encodeURIComponent(imageId)}/tags/${encodeURIComponent(tagId)}`, 'PUT', payload);
export const deleteTag = (imageId, tagId, version) =>
    write(`/api/admin/public-projects/images/${encodeURIComponent(imageId)}/tags/${encodeURIComponent(tagId)}?version=${encodeURIComponent(version)}`, 'DELETE');

export function attach(stage, dotnet) {
    detach();
    if (!stage || !dotnet) return;
    let pointer = null;
    const location = event => {
        const r = stage.getBoundingClientRect();
        return {
            x: Math.max(0, Math.min(1, (event.clientX - r.left) / r.width)),
            y: Math.max(0, Math.min(1, (event.clientY - r.top) / r.height))
        };
    };
    const preview = (part, coords) => {
        const handle = stage.querySelector(`[data-tag-drag="${part}"]`);
        if (!handle) return;
        handle.style.left = `${coords.x * 100}%`;
        handle.style.top = `${coords.y * 100}%`;
        const line = stage.querySelector('[data-tag-draft-line]');
        if (line) {
            line.setAttribute(part === 'anchor' ? 'x1' : 'x2', `${coords.x * 100}`);
            line.setAttribute(part === 'anchor' ? 'y1' : 'y2', `${coords.y * 100}`);
        }
    };
    const down = event => {
        if (event.button !== 0 || pointer !== null) return;
        if (event.target.closest('[data-tag-edit]')) return;
        if (!stage.querySelector('[data-tag-drag="anchor"]')) return;
        const target = event.target.closest('[data-tag-drag]');
        const part = target?.dataset?.tagDrag === 'label' ? 'label' : 'anchor';
        pointer = { id: event.pointerId, part };
        event.preventDefault();
        stage.setPointerCapture(event.pointerId);
        preview(part, location(event));
    };
    const move = event => {
        if (pointer?.id !== event.pointerId) return;
        preview(pointer.part, location(event));
    };
    const up = event => {
        if (pointer?.id !== event.pointerId) return;
        const { part } = pointer;
        const point = location(event);
        preview(part, point);
        pointer = null;
        if (stage.hasPointerCapture(event.pointerId)) stage.releasePointerCapture(event.pointerId);
        // Save to the Blazor draft once per gesture, not on every pointer movement.
        void dotnet.invokeMethodAsync('SetTagPosition', part, point.x, point.y).catch(() => {});
    };
    const cancel = event => {
        if (pointer?.id !== event.pointerId) return;
        pointer = null;
    };
    stage.addEventListener('pointerdown', down);
    stage.addEventListener('pointermove', move);
    stage.addEventListener('pointerup', up);
    stage.addEventListener('pointercancel', cancel);
    bound = () => {
        stage.removeEventListener('pointerdown', down);
        stage.removeEventListener('pointermove', move);
        stage.removeEventListener('pointerup', up);
        stage.removeEventListener('pointercancel', cancel);
        pointer = null;
    };
}
export function detach() { if (bound) { bound(); bound = null; } }
