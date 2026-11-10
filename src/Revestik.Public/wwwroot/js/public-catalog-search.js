// Progressive enhancement for Acabados; the existing SSR GET form remains the fallback.
(() => {
    const form = document.querySelector('.finishes-page .catalog-search');
    const input = form?.querySelector('input[name="search"]');
    if (!form || !input) return;

    const locale = location.pathname.split('/')[1];
    const root = locale === 'en' ? '/en/finishes' : locale === 'de' ? '/de/oberflaechen' : '/es/acabados';
    const suggestions = document.createElement('div');
    suggestions.className = 'finishes-suggestions';
    suggestions.id = 'finishes-search-suggestions';
    suggestions.setAttribute('role', 'listbox');
    suggestions.setAttribute('aria-label', 'Sugerencias de productos');
    suggestions.hidden = true;
    form.append(suggestions);
    input.setAttribute('aria-controls', suggestions.id);
    input.setAttribute('aria-autocomplete', 'list');
    let controller = null;
    let timer = null;
    let selected = -1;
    let options = [];
    const close = () => {
        suggestions.hidden = true;
        suggestions.replaceChildren();
        selected = -1;
        options = [];
        input.removeAttribute('aria-activedescendant');
    };
    function focusOption(index) {
        selected = index;
        options.forEach((option, i) => option.classList.toggle('is-selected', i === index));
        if (index >= 0) input.setAttribute('aria-activedescendant', options[index].id);
        else input.removeAttribute('aria-activedescendant');
    }
    function show(items) {
        close();
        if (!items.length || document.activeElement !== input) return;
        items.forEach((item, i) => {
            if (!/^[a-z0-9-]{1,160}$/.test(item.slug)) return;
            const option = document.createElement('a');
            option.id = `finishes-suggestion-${i}`;
            option.setAttribute('role', 'option');
            option.setAttribute('aria-selected', 'false');
            option.href = `${root}/${encodeURIComponent(item.slug)}`;
            option.className = 'finishes-suggestion';
            const translation = item.translations?.find(x => x.languageCode === locale) ||
                item.translations?.find(x => x.languageCode === 'es');
            option.textContent = translation?.name || item.slug;
            suggestions.append(option);
            options.push(option);
        });
        suggestions.hidden = options.length === 0;
    }
    input.addEventListener('input', () => {
        clearTimeout(timer);
        if (controller) controller.abort();
        const text = input.value.trim();
        if (!text || text.length > 100) { close(); return; }
        const categoryId = form.querySelector('input[name="categoryId"]')?.value;
        const subcategoryId = form.querySelector('input[name="subcategoryId"]')?.value;
        timer = setTimeout(async () => {
            controller = new AbortController();
            const current = controller;
            try {
                const params = new URLSearchParams({ search: text, page: '1', pageSize: '6' });
                if (categoryId) params.set('categoryId', categoryId);
                if (subcategoryId) params.set('subcategoryId', subcategoryId);
                const response = await fetch(`/api/public/catalog/products?${params}`, {
                    method: 'GET', credentials: 'omit', cache: 'no-store', signal: current.signal
                });
                if (!response.ok) { close(); return; }
                const data = await response.json();
                if (current === controller && input.value.trim() === text) show(data.items || []);
            } catch (error) {
                if (error?.name !== 'AbortError') close();
            }
        }, 300);
    });
    input.addEventListener('keydown', event => {
        if (event.key === 'Escape') { close(); return; }
        if (suggestions.hidden || !options.length) return;
        if (event.key === 'ArrowDown') { event.preventDefault(); focusOption((selected + 1) % options.length); }
        if (event.key === 'ArrowUp') { event.preventDefault(); focusOption((selected + options.length - 1) % options.length); }
        if (event.key === 'Enter' && selected >= 0) {
            event.preventDefault();
            window.location.assign(options[selected].href);
        }
    });
    input.addEventListener('blur', () => window.setTimeout(close, 175));
    document.addEventListener('visibilitychange', () => { if (document.hidden) close(); });
})();
