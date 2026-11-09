// Session-scoped public product form drafts. No image bytes or auth material are stored.
// Each key is scoped by the authenticated administrator and public product identifier.
const MAX_CHARS = 128 * 1024;

export function readDraft(key) {
    if (typeof key !== "string" || !key.startsWith("revestik:public-product-draft:v1:")) return null;
    return sessionStorage.getItem(key);
}

export function writeDraft(key, serialized) {
    if (typeof key !== "string" || !key.startsWith("revestik:public-product-draft:v1:"))
        throw new Error("Invalid public product draft key.");
    if (typeof serialized !== "string" || serialized.length > MAX_CHARS)
        throw new Error("Public product draft exceeds local size limit.");
    sessionStorage.setItem(key, serialized);
}

export function removeDraft(key) {
    if (typeof key !== "string" || !key.startsWith("revestik:public-product-draft:v1:")) return;
    sessionStorage.removeItem(key);
}
