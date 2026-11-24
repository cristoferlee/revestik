// PUBLIC-CART-01D: public PDF is generated transiently; no PDF is stored in the application.
// WhatsApp prepares an inquiry, not a sent message or formal PDF proforma.
(() => {
  'use strict';

  const texts = {
    es: {
      cart: 'Carrito', loginRequired: 'Iniciá sesión como cliente para guardar productos en el carrito.',
      forbidden: 'Esta función requiere una cuenta de cliente público, no una cuenta administrativa.',
      unavailableService: 'No pudimos conectar con el carrito. Intentá nuevamente.',
      invalidQuantity: 'Ingresá una cantidad válida (máximo 3 decimales, hasta 10.000). La unidad de venta puede exigir números enteros.',
      conflict: 'El carrito cambió en otra pestaña o el producto ya no está disponible. Se actualizaron los datos; revisá antes de continuar.',
      addSuccess: 'Producto agregado al carrito.', updateSuccess: 'Cantidad actualizada.',
      removeSuccess: 'Producto eliminado del carrito.', unavailable: 'Producto no disponible',
      unavailableExplanation: 'Este producto ya no está publicado. Podés eliminarlo de tu carrito.',
      quantity: 'Cantidad', save: 'Guardar', remove: 'Eliminar', priceInquiry: 'Precio a consultar',
      unitPrice: 'Precio de catálogo', lineAmount: 'Subtotal orientativo',
      loading: 'Cargando tu carrito…', rateLimit: 'Demasiadas solicitudes. Intentá nuevamente en un momento.',
      guestStored: 'Producto guardado en la selección temporal de este navegador.',
      guestStorageError: 'Este navegador no permite guardar la selección temporal. Revisá la configuración de almacenamiento.',
      guestLimit: 'El carrito admite hasta 50 productos distintos.',
      guestUnavailable: 'Este producto ya no está publicado o cambió. Revisá tu selección.',
      importSuccess: 'Selección importada. Los productos repetidos conservaron las cantidades de la cuenta.',
      importPartial: 'No se pudieron importar todos los productos. Conservamos los pendientes en este navegador.',
      noWhatsapp: 'WhatsApp no está configurado para este sitio. Usá la página de contacto.',
      guestReview: 'Actualizando productos publicados antes de preparar el desglose…',
      staleWarning: 'Los datos guardados en este navegador son orientativos y se verifican antes de enviar una consulta.',
      breakdownTitle: 'Desglose orientativo', total: 'Subtotal', amountUnknown: 'Sin precio de catálogo',
      mixed: 'Se muestran subtotales por moneda; no incluyen impuestos ni envío. Sujeto a confirmación.',
      whatsappTooLong: 'La selección es extensa. Copiá el desglose y pegalo manualmente en WhatsApp.',
      copyBreakdown: 'Copiar desglose', copied: 'Desglose copiado.',
      inquiryHeader: 'Hola REVESTIK, quisiera solicitar una proforma con los siguientes productos:',
      inquiryFooter: 'Precios orientativos; agradezco confirmar disponibilidad, impuestos, entrega y precio final.',
      skippedUnavailable: 'Se omitieron productos no publicados. Revisá el carrito antes de enviar la consulta.',
      pdfReady: 'Proforma preliminar tamaño carta lista para descargar o compartir. Revestik no guarda una copia.',
      pdfUnavailable: 'No se pudo generar la proforma. Revisá el carrito y volvé a intentar.',
      pdfInvalid: 'Las cantidades o unidades cambiaron. Revisá tu selección.',
      pdfShareError: 'No se pudo compartir desde este dispositivo. Descargá el PDF y compartilo manualmente.'
    },
    en: {
      cart: 'Cart', loginRequired: 'Sign in with a customer account to save products to your cart.',
      forbidden: 'A public customer account is required. Administrative accounts cannot use this cart.',
      unavailableService: 'Unable to reach your cart. Please try again.',
      invalidQuantity: 'Enter a valid quantity (up to 3 decimal places, maximum 10,000). Your selling unit may require whole numbers.',
      conflict: 'Your cart changed in another tab or the product is no longer available. The cart has been refreshed; please review it.',
      addSuccess: 'Product added to cart.', updateSuccess: 'Quantity updated.',
      removeSuccess: 'Product removed from cart.', unavailable: 'Product unavailable',
      unavailableExplanation: 'This product is no longer published. You can remove it from your cart.',
      quantity: 'Quantity', save: 'Save', remove: 'Remove', priceInquiry: 'Price on request',
      unitPrice: 'Catalog price', lineAmount: 'Estimated subtotal',
      loading: 'Loading your cart…', rateLimit: 'Too many requests. Please try again shortly.',
      guestStored: 'Product saved to this browser’s temporary selection.',
      guestStorageError: 'This browser cannot save a temporary selection. Check storage settings.',
      guestLimit: 'The cart supports at most 50 different products.',
      guestUnavailable: 'This product is no longer published or has changed. Review your selection.',
      importSuccess: 'Selection imported. Existing products kept their account quantities.',
      importPartial: 'Not all products could be imported. Remaining items are still saved in this browser.',
      noWhatsapp: 'WhatsApp is not configured. Please use the contact page.',
      guestReview: 'Refreshing published products for your breakdown…',
      staleWarning: 'Saved browser details are indicative and checked again before sending an inquiry.',
      breakdownTitle: 'Estimated breakdown', total: 'Subtotal', amountUnknown: 'No catalog price',
      mixed: 'Subtotals are shown by currency; tax and delivery are not included. Subject to confirmation.',
      whatsappTooLong: 'This selection is lengthy. Copy the breakdown and paste it into WhatsApp.',
      copyBreakdown: 'Copy breakdown', copied: 'Breakdown copied.',
      inquiryHeader: 'Hello REVESTIK, I would like to request a quotation for these products:',
      inquiryFooter: 'Prices are indicative. Please confirm availability, taxes, delivery and final pricing.',
      skippedUnavailable: 'Unpublished products were omitted. Review your cart before making an inquiry.',
      pdfReady: 'Your preliminary quotation PDF (US Letter) is ready to download or share. Revestik does not keep a copy.',
      pdfUnavailable: 'The PDF could not be generated. Review your cart and try again.',
      pdfInvalid: 'Quantities or selling units changed. Review your selection.',
      pdfShareError: 'This device could not share the PDF. Download it and share it manually.'
    },
    de: {
      cart: 'Warenkorb', loginRequired: 'Melden Sie sich mit einem Kundenkonto an, um Produkte im Warenkorb zu speichern.',
      forbidden: 'Ein öffentliches Kundenkonto ist erforderlich. Administratorkonten können diesen Warenkorb nicht verwenden.',
      unavailableService: 'Ihr Warenkorb ist nicht erreichbar. Bitte versuchen Sie es erneut.',
      invalidQuantity: 'Geben Sie eine gültige Menge ein (bis zu 3 Dezimalstellen, höchstens 10.000). Die Verkaufseinheit kann ganze Zahlen erfordern.',
      conflict: 'Ihr Warenkorb wurde in einem anderen Tab geändert oder das Produkt ist nicht mehr verfügbar. Die Daten wurden aktualisiert.',
      addSuccess: 'Produkt zum Warenkorb hinzugefügt.', updateSuccess: 'Menge aktualisiert.',
      removeSuccess: 'Produkt aus Warenkorb entfernt.', unavailable: 'Produkt nicht verfügbar',
      unavailableExplanation: 'Dieses Produkt ist nicht mehr veröffentlicht. Sie können es aus dem Warenkorb entfernen.',
      quantity: 'Menge', save: 'Speichern', remove: 'Entfernen', priceInquiry: 'Preis auf Anfrage',
      unitPrice: 'Katalogpreis', lineAmount: 'Geschätzte Zwischensumme',
      loading: 'Warenkorb wird geladen…', rateLimit: 'Zu viele Anfragen. Versuchen Sie es in Kürze erneut.',
      guestStored: 'Das Produkt wurde in diesem Browser vorübergehend gespeichert.',
      guestStorageError: 'Dieser Browser kann die Auswahl nicht speichern. Prüfen Sie die Speichereinstellungen.',
      guestLimit: 'Der Warenkorb erlaubt höchstens 50 verschiedene Produkte.',
      guestUnavailable: 'Dieses Produkt ist nicht mehr veröffentlicht oder wurde geändert. Bitte Auswahl prüfen.',
      importSuccess: 'Auswahl importiert. Vorhandene Mengen im Konto bleiben unverändert.',
      importPartial: 'Nicht alle Produkte wurden importiert. Die übrigen bleiben in diesem Browser erhalten.',
      noWhatsapp: 'WhatsApp ist nicht konfiguriert. Bitte nutzen Sie die Kontaktseite.',
      guestReview: 'Veröffentlichte Produkte werden für die Übersicht aktualisiert…',
      staleWarning: 'Gespeicherte Browserdaten sind unverbindlich und werden vor der Anfrage geprüft.',
      breakdownTitle: 'Unverbindliche Aufstellung', total: 'Zwischensumme', amountUnknown: 'Kein Katalogpreis',
      mixed: 'Zwischensummen je Währung; ohne Steuern oder Lieferung. Bestätigung erforderlich.',
      whatsappTooLong: 'Die Auswahl ist umfangreich. Kopieren Sie die Aufstellung und fügen Sie sie in WhatsApp ein.',
      copyBreakdown: 'Aufstellung kopieren', copied: 'Aufstellung kopiert.',
      inquiryHeader: 'Hallo REVESTIK, ich bitte um eine Proforma für folgende Produkte:',
      inquiryFooter: 'Preise unverbindlich; bitte Verfügbarkeit, Steuern, Lieferung und Endpreis bestätigen.',
      skippedUnavailable: 'Unveröffentlichte Produkte wurden ausgelassen. Bitte Warenkorb vor Anfrage prüfen.',
      pdfReady: 'Das vorläufige PDF im Letter-Format kann heruntergeladen oder geteilt werden. Revestik speichert keine Kopie.',
      pdfUnavailable: 'Das PDF konnte nicht erstellt werden. Bitte Warenkorb prüfen und erneut versuchen.',
      pdfInvalid: 'Mengen oder Verkaufseinheiten haben sich geändert. Bitte Auswahl überprüfen.',
      pdfShareError: 'Das PDF konnte auf diesem Gerät nicht geteilt werden. Bitte herunterladen und manuell teilen.'
    }
  };

  const language = () => {
    const part = window.location.pathname.split('/')[1];
    return part === 'en' || part === 'de' ? part : 'es';
  };
  const t = key => texts[language()][key];
  const locale = () => ({ es: 'es-CR', en: 'en-US', de: 'de-DE' })[language()];

  // 'unknown' / 'customer' / 'guest' / 'forbidden' / 'error'.
  const sessionHint = () => find('[data-public-cart-link]')?.dataset.publicCartSession;
  let accountState = 'unknown';
  let cart = [];
  let guestCart = [];
  const GUEST_KEY = 'revestik.public.cart.guest.v1';
  const GUEST_TTL_MS = 7 * 24 * 60 * 60 * 1000;
  const GUEST_LIMIT = 50;
  let loadingPromise = null;
  let mutating = false;
  let pdfObjectUrl = null;
  let pdfFile = null;

  function clearGeneratedPdf() {
    if (pdfObjectUrl) URL.revokeObjectURL(pdfObjectUrl);
    pdfObjectUrl = null;
    pdfFile = null;
    const panel = find('[data-public-cart-pdf-result]');
    if (panel) panel.hidden = true;
    const download = find('[data-public-cart-pdf-download]');
    if (download) { download.hidden = true; download.removeAttribute('href'); }
    const share = find('[data-public-cart-pdf-share]');
    if (share) share.hidden = true;
  }


  const find = (selector, root = document) => root.querySelector(selector);
  const all = selector => [...document.querySelectorAll(selector)];
  const show = (node, visible) => { if (node) node.hidden = !visible; };
  const element = (tag, className, text) => {
    const result = document.createElement(tag);
    if (className) result.className = className;
    if (text !== undefined) result.textContent = text;
    return result;
  };
  const labeledLink = (href, text) => {
    const link = element('a', '', text);
    link.href = href;
    return link;
  };


  function positiveId(raw) {
    const value = Number(raw);
    return Number.isSafeInteger(value) && value > 0 ? value : null;
  }

  function validGuestLine(line) {
    if (!line || typeof line !== 'object' || positiveId(line.productId) === null ||
      typeof line.slug !== 'string' || !/^[a-z0-9-]{1,160}$/.test(line.slug) ||
      validQuantity(line.quantity) === null) return null;
    const translations = Array.isArray(line.translations)
      ? line.translations.filter(x => x && ['es', 'en', 'de'].includes(x.languageCode) &&
        typeof x.name === 'string' && x.name.length <= 250).slice(0, 3)
      : [];
    return {
      productId: Number(line.productId), slug: line.slug,
      quantity: Number(line.quantity), version: null, isAvailable: true,
      brand: typeof line.brand === 'string' ? line.brand.slice(0, 150) : null,
      price: Number.isFinite(line.price) && line.price >= 0 ? line.price : null,
      currency: ['CRC', 'USD'].includes(line.currency) ? line.currency : null,
      salesUnit: typeof line.salesUnit === 'string' ? line.salesUnit.slice(0, 50) : null,
      coverImageId: positiveId(line.coverImageId), translations
    };
  }

  function readGuest() {
    try {
      const raw = window.localStorage.getItem(GUEST_KEY);
      if (!raw) return [];
      const saved = JSON.parse(raw);
      if (!saved || saved.version !== 1 || !Number.isFinite(saved.expiresAt) ||
        saved.expiresAt <= Date.now() || !Array.isArray(saved.items)) {
        window.localStorage.removeItem(GUEST_KEY);
        return [];
      }
      // Fail closed on corrupt/oversized user-managed storage. Never use it as authorization.
      if (saved.items.length > GUEST_LIMIT) return [];
      const clean = saved.items.map(validGuestLine);
      if (clean.some(x => !x) || new Set(clean.map(x => x.productId)).size !== clean.length) return [];
      return clean;
    } catch { return []; }
  }

  function saveGuest(next) {
    if (next.length > GUEST_LIMIT) throw new Error(t('guestLimit'));
    try {
      // Remove key when empty; otherwise keep the original seven-day lifetime if possible.
      if (!next.length) {
        window.localStorage.removeItem(GUEST_KEY);
      } else {
        let expiresAt = Date.now() + GUEST_TTL_MS;
        try {
          const old = JSON.parse(window.localStorage.getItem(GUEST_KEY) || 'null');
          if (old?.version === 1 && Number.isFinite(old.expiresAt) && old.expiresAt > Date.now())
            expiresAt = Math.min(expiresAt, old.expiresAt);
        } catch { /* no prior expiry */ }
        window.localStorage.setItem(GUEST_KEY, JSON.stringify({ version: 1, expiresAt, items: next }));
      }
      guestCart = next;
      render();
    } catch { throw new Error(t('guestStorageError')); }
  }

  // Every guest product is read from the published public endpoint, never an admin API.
  async function publishedProduct(slug, expectedId) {
    if (!/^[a-z0-9-]{1,160}$/.test(slug || '')) throw new Error(t('guestUnavailable'));
    const response = await fetch(`/api/public/catalog/products/${encodeURIComponent(slug)}`, {
      method: 'GET', cache: 'no-store', credentials: 'same-origin', headers: { Accept: 'application/json' }
    });
    if (response.status === 404) throw new Error(t('guestUnavailable'));
    if (response.status === 429) throw new Error(t('rateLimit'));
    if (!response.ok) throw new Error(t('unavailableService'));
    const product = await response.json();
    if (!product || positiveId(product.id) !== expectedId || product.slug !== slug ||
      product.isPublished !== true) throw new Error(t('guestUnavailable'));
    return {
      productId: expectedId, slug, version: null, isAvailable: true,
      brand: product.brand ?? null, price: product.price ?? null,
      currency: product.currency ?? null, salesUnit: product.salesUnit ?? null,
      coverImageId: positiveId(Array.isArray(product.images)
        ? product.images.find(x => x?.role === 'ProductCover')?.id : null),
      translations: (Array.isArray(product.translations) ? product.translations : [])
        .filter(x => x && ['es', 'en', 'de'].includes(x.languageCode) && typeof x.name === 'string')
        .map(x => ({ languageCode: x.languageCode, name: x.name })).slice(0, 3)
    };
  }

  function notice(message, isError = false) {
    const panel = find('#public-cart-notice');
    if (!panel) return;
    const content = find('[data-public-cart-notice-text]', panel);
    if (content) content.textContent = message;
    panel.dataset.status = isError ? 'error' : 'success';
    panel.hidden = false;
  }

  function renderNavbar() {
    const total = accountState === 'guest' ? guestCart.length : accountState === 'customer' ? cart.length : 0;
    all('[data-public-cart-link]').forEach(link => {
      link.setAttribute('aria-label', total ? `${t('cart')} (${total})` : t('cart'));
      const badge = find('[data-public-cart-count]', link);
      if (badge) {
        badge.textContent = total ? String(total) : '';
        show(badge, total > 0);
      }
    });
  }

  function formatQuantity(quantity) {
    return new Intl.NumberFormat(locale(), { maximumFractionDigits: 3 }).format(quantity);
  }

  function formatMoney(price, currency) {
    if (!Number.isFinite(Number(price)) || !['CRC', 'USD'].includes(currency)) return null;
    return new Intl.NumberFormat(locale(), {
      style: 'currency', currency, minimumFractionDigits: 2, maximumFractionDigits: 2
    }).format(Number(price));
  }

  function selectName(item) {
    const translations = Array.isArray(item.translations) ? item.translations : [];
    const match = translations.find(x => x.languageCode === language()) ||
      translations.find(x => x.languageCode === 'es');
    return match?.name || item.slug || t('unavailable');
  }

  function makeRow(item) {
    const row = element('article', 'public-cart-item' + (!item.isAvailable ? ' is-unavailable' : ''));
    row.dataset.cartItem = String(item.productId);
    const photo = element('div', 'public-cart-item-photo');
    const name = item.isAvailable ? selectName(item) : t('unavailable');
    if (item.isAvailable && Number.isSafeInteger(item.coverImageId) && item.coverImageId > 0) {
      const img = element('img');
      img.src = `/media/catalog/${item.coverImageId}`;
      img.alt = '';
      img.loading = 'lazy';
      img.width = 320;
      img.height = 240;
      photo.append(img);
    } else {
      photo.textContent = 'REVESTIK';
    }
    row.append(photo);

    const main = element('div', 'public-cart-item-main');
    const title = element('h2');
    if (item.isAvailable && item.slug) {
      const prefix = ({ es: '/es/acabados/', en: '/en/finishes/', de: '/de/oberflaechen/' })[language()];
      title.append(labeledLink(prefix + encodeURIComponent(item.slug), name));
    } else {
      title.textContent = name;
    }
    main.append(title);

    if (!item.isAvailable) {
      main.append(element('p', 'public-cart-unavailable', t('unavailableExplanation')));
    } else {
      if (item.brand) main.append(element('p', '', item.brand));
      const unit = item.salesUnit ? ` / ${item.salesUnit}` : '';
      const price = item.price === null || item.price === undefined
        ? null : formatMoney(item.price, item.currency);
      main.append(element('p', '', price ? `${t('unitPrice')}: ${price}${unit}` : t('priceInquiry')));
      if (price) {
        const amount = formatMoney(Number(item.price) * Number(item.quantity), item.currency);
        if (amount) main.append(element('p', '', `${t('lineAmount')}: ${amount}`));
      }
    }
    row.append(main);

    const actions = element('div', 'public-cart-item-actions');
    if (item.isAvailable) {
      const label = element('label', 'public-cart-qty-label',
        `${t('quantity')}${item.salesUnit ? ` (${item.salesUnit})` : ''}`);
      const input = element('input', 'public-cart-qty-input');
      input.type = 'number';
      input.min = '0.001';
      input.max = '10000';
      input.step = '0.001';
      input.inputMode = 'decimal';
      input.value = String(item.quantity);
      input.id = `public-cart-qty-${item.productId}`;
      input.setAttribute('aria-label', `${t('quantity')}: ${name}`);
      label.htmlFor = input.id;
      actions.append(label, input);
    } else {
      actions.append(element('p', '', `${t('quantity')}: ${formatQuantity(item.quantity)}`));
    }

    const buttons = element('div', 'public-cart-actions-row');
    if (item.isAvailable) {
      const save = element('button', 'public-cart-secondary-button', t('save'));
      save.type = 'button';
      save.dataset.publicCartSave = String(item.productId);
      buttons.append(save);
    }
    const remove = element('button', 'public-cart-secondary-button public-cart-delete', t('remove'));
    remove.type = 'button';
    remove.dataset.publicCartRemove = String(item.productId);
    buttons.append(remove);
    actions.append(buttons);
    row.append(actions);
    return row;
  }

  function renderCartView() {
    const root = find('[data-public-cart-view]');
    if (!root) return;
    const states = {
      loading: find('[data-public-cart-loading]', root),
      empty: find('[data-public-cart-empty]', root),
      login: find('[data-public-cart-login]', root),
      forbidden: find('[data-public-cart-forbidden]', root),
      error: find('[data-public-cart-error]', root),
      results: find('[data-public-cart-results]', root),
      guestIntro: find('[data-public-cart-guest-intro]', root),
      importPanel: find('[data-public-cart-import-panel]', root)
    };
    for (const section of Object.values(states)) show(section, false);
    if (accountState === 'unknown') return show(states.loading, true);
    if (accountState === 'forbidden') return show(states.forbidden, true);
    if (accountState === 'error') return show(states.error, true);
    const guest = accountState === 'guest';
    const lines = guest ? guestCart : cart;
    if (guest) show(guestCart.length ? states.guestIntro : states.login, true);
    if (accountState === 'customer' && guestCart.length) show(states.importPanel, true);
    if (!lines.length) {
      if (!guest) show(states.empty, true);
      return;
    }
    const list = find('[data-public-cart-items]', root);
    if (list) list.replaceChildren(...lines.map(makeRow));
    show(states.results, true);
    clearGeneratedPdf();
    // Prevent showing an obsolete breakdown after any quantity change.
    const breakdown = find('[data-public-cart-breakdown]', root);
    if (breakdown) { breakdown.replaceChildren(); breakdown.hidden = true; }
    const whatsappLink = find('[data-public-cart-whatsapp-open]', root);
    if (whatsappLink) { whatsappLink.hidden = true; whatsappLink.removeAttribute('href'); }
  }

  function render() {
    renderNavbar();
    renderCartView();
  }

  async function loadCart() {
    if (loadingPromise) return loadingPromise;
    loadingPromise = (async () => {
      try {
        const response = await fetch('/api/public/cart', {
          method: 'GET', credentials: 'same-origin', cache: 'no-store',
          headers: { Accept: 'application/json' }
        });
        if (response.status === 401) { accountState = 'guest'; cart = []; }
        else if (response.status === 403) { accountState = 'forbidden'; cart = []; }
        else if (!response.ok) { accountState = 'error'; cart = []; }
        else {
          const result = await response.json();
          if (!result || !Array.isArray(result.items)) throw new Error('Invalid cart response');
          accountState = 'customer';
          cart = result.items;
        }
      } catch {
        accountState = 'error';
        cart = [];
      } finally {
        render();
      }
    })();
    try { await loadingPromise; } finally { loadingPromise = null; }
  }

  async function getCsrfToken() {
    const response = await fetch('/api/auth/csrf', {
      method: 'GET', credentials: 'same-origin', cache: 'no-store',
      headers: { Accept: 'application/json' }
    });
    if (response.status === 401) {
      accountState = 'guest'; cart = []; render();
      throw new Error(t('loginRequired'));
    }
    if (!response.ok) throw new Error(t('unavailableService'));
    const payload = await response.json();
    if (typeof payload?.requestToken !== 'string' || !payload.requestToken)
      throw new Error(t('unavailableService'));
    return payload.requestToken;
  }

  function validQuantity(raw) {
    const value = String(raw ?? '').trim();
    if (!/^\d+(\.\d{1,3})?$/.test(value)) return null;
    const number = Number(value);
    return Number.isFinite(number) && number > 0 && number <= 10000 ? number : null;
  }

  function milli(quantity) { return Math.round(Number(quantity) * 1000); }

  async function sendMutation(method, productId, quantity, version) {
    const token = await getCsrfToken();
    const path = `/api/public/cart/items/${productId}`;
    const url = method === 'DELETE'
      ? `${path}?${new URLSearchParams({ version })}`
      : path;
    const options = {
      method, credentials: 'same-origin', cache: 'no-store',
      headers: { 'X-CSRF-TOKEN': token, Accept: 'application/json' }
    };
    if (method === 'PUT') {
      options.headers['Content-Type'] = 'application/json';
      options.body = JSON.stringify({ quantity, version: version ?? null });
    }
    const response = await fetch(url, options);
    if (response.status === 401) { accountState = 'guest'; cart = []; render(); throw new Error(t('loginRequired')); }
    if (response.status === 403) { accountState = 'forbidden'; cart = []; render(); throw new Error(t('forbidden')); }
    if (response.status === 409 || response.status === 404) {
      await loadCart();
      throw new Error(t('conflict'));
    }
    if (response.status === 400) throw new Error(t('invalidQuantity'));
    if (response.status === 429) throw new Error(t('rateLimit'));
    if (!response.ok) throw new Error(t('unavailableService'));
    if (method === 'PUT') {
      const result = await response.json();
      if (!result || !Array.isArray(result.items)) throw new Error(t('unavailableService'));
      cart = result.items;
      accountState = 'customer';
      render();
    } else {
      // Successful DELETE is 204; reload for fresh versions and a correct count.
      await loadCart();
    }
  }

  async function withMutation(control, operation) {
    if (mutating) return;
    mutating = true;
    control.disabled = true;
    const root = find('[data-public-cart-view]');
    if (root) root.setAttribute('aria-busy', 'true');
    try {
      if (accountState === 'unknown' || accountState === 'error') await loadCart();
      if (accountState !== 'customer' && accountState !== 'guest') {
        notice(accountState === 'forbidden' ? t('forbidden') : t('unavailableService'), true);
        return;
      }
      await operation();
    } catch (error) {
      notice(error instanceof Error && error.message ? error.message : t('unavailableService'), true);
    } finally {
      mutating = false;
      control.disabled = false;
      if (root) root.removeAttribute('aria-busy');
    }
  }

  async function add(productId, amount, slug) {
    if (accountState === 'guest') {
      if (!slug) throw new Error(t('guestUnavailable'));
      const product = await publishedProduct(slug, productId);
      const existing = guestCart.find(line => line.productId === productId);
      const total = milli(amount) + (existing ? milli(existing.quantity) : 0);
      if (total < 1 || total > 10000000) throw new Error(t('invalidQuantity'));
      if (!existing && guestCart.length >= GUEST_LIMIT) throw new Error(t('guestLimit'));
      saveGuest([...guestCart.filter(x => x.productId !== productId), { ...product, quantity: total / 1000 }]);
      notice(t('guestStored'));
      return;
    }
    const existing = cart.find(line => line.productId === productId);
    const total = milli(amount) + (existing ? milli(existing.quantity) : 0);
    if (total < 1 || total > 10000000) throw new Error(t('invalidQuantity'));
    await sendMutation('PUT', productId, total / 1000, existing?.version ?? null);
    notice(t('addSuccess'));
  }

  async function importGuest() {
    if (accountState !== 'customer') throw new Error(t('forbidden'));
    if (!guestCart.length) return;
    // Every step is idempotent across reloads: successful imports are removed immediately.
    // Existing server lines are never overwritten. On 429/network failure, remaining lines stay local.
    for (const original of [...guestCart]) {
      const fresh = await publishedProduct(original.slug, original.productId);
      if (!cart.some(x => x.productId === fresh.productId)) {
        await sendMutation('PUT', fresh.productId, original.quantity, null);
      }
      saveGuest(guestCart.filter(x => x.productId !== original.productId));
    }
    notice(t('importSuccess'));
  }

  async function reviewedLines() {
    if (accountState === 'customer') {
      await loadCart();
      if (accountState !== 'customer') throw new Error(t('unavailableService'));
      return cart.filter(x => x.isAvailable);
    }
    if (accountState !== 'guest') throw new Error(t('forbidden'));
    const reviewed = [];
    for (const line of guestCart) {
      // Never trust client-maintained product names or prices for the inquiry.
      try {
        const fresh = await publishedProduct(line.slug, line.productId);
        reviewed.push({ ...fresh, quantity: line.quantity });
      } catch (error) {
        if (error instanceof Error && error.message === t('guestUnavailable')) continue;
        throw error;
      }
    }
    if (reviewed.length < guestCart.length) notice(t('skippedUnavailable'), true);
    return reviewed;
  }

  function breakdown(lines) {
    const totals = new Map();
    const result = [t('inquiryHeader'), ''];
    for (const [index, item] of lines.entries()) {
      const name = selectName(item).replace(/[\r\n]+/g, ' ').slice(0, 250);
      const quantity = formatQuantity(item.quantity);
      const price = item.price == null ? null : formatMoney(item.price, item.currency);
      const amount = price && formatMoney(Number(item.price) * Number(item.quantity), item.currency);
      result.push(`${index + 1}. ${name} — ${quantity}${item.salesUnit ? ' ' + item.salesUnit : ''}`);
      if (amount) {
        result.push(`   ${t('unitPrice')}: ${price} | ${t('total')}: ${amount}`);
        totals.set(item.currency, (totals.get(item.currency) || 0) + Number(item.price) * Number(item.quantity));
      } else result.push(`   ${t('amountUnknown')}`);
    }
    result.push('');
    for (const [currency, total] of totals) result.push(`${t('total')} (${currency}): ${formatMoney(total, currency)}`);
    result.push(t('mixed'), t('inquiryFooter'));
    return result.join('\n');
  }

  function drawBreakdown(message) {
    const root = find('[data-public-cart-breakdown]');
    if (!root) return;
    const title = element('h3', '', t('breakdownTitle'));
    const pre = element('pre', 'public-cart-breakdown-pre', message);
    const copy = element('button', 'public-cart-secondary-button', t('copyBreakdown'));
    copy.type = 'button';
    copy.dataset.publicCartCopy = '';
    root.replaceChildren(title, pre, copy);
    root.hidden = false;
  }

  async function showBreakdown() {
    const lines = await reviewedLines();
    if (!lines.length) throw new Error(t('guestUnavailable'));
    drawBreakdown(breakdown(lines));
  }

  async function contactWhatsApp() {
    const root = find('[data-public-cart-view]');
    const phone = root?.dataset.publicCartWhatsappNumber || '';
    if (!/^\d{8,15}$/.test(phone) || phone.startsWith('0')) throw new Error(t('noWhatsapp'));
    const lines = await reviewedLines();
    if (!lines.length) throw new Error(t('guestUnavailable'));
    const message = breakdown(lines);
    drawBreakdown(message);
    // Fetching verified product details is asynchronous. Present an explicit link
    // instead of trying to open a popup after await (often blocked by browsers).
    const open = find('[data-public-cart-whatsapp-open]', root);
    if (!open) throw new Error(t('unavailableService'));
    open.href = message.length > 1800
      ? `https://wa.me/${phone}`
      : `https://wa.me/${phone}?text=${encodeURIComponent(message)}`;
    open.hidden = false;
    if (message.length > 1800) notice(t('whatsappTooLong'), true);
  }

  async function generatePdf() {
    const lines = accountState === 'guest' ? guestCart : accountState === 'customer' ? cart : [];
    if (!lines.length || lines.some(line => !line.isAvailable)) throw new Error(t('guestUnavailable'));
    const items = lines.map(line => ({ productId: line.productId, quantity: line.quantity }));
    const response = await fetch('/api/public/cart/proforma', {
      method: 'POST', credentials: 'same-origin', cache: 'no-store',
      headers: { 'Accept': 'application/pdf', 'Content-Type': 'application/json' },
      body: JSON.stringify({ language: language(), items })
    });
    if (response.status === 429) throw new Error(t('rateLimit'));
    if (response.status === 409) throw new Error(t('guestUnavailable'));
    if (response.status === 400 || response.status === 413) throw new Error(t('pdfInvalid'));
    if (!response.ok || !response.headers.get('content-type')?.includes('application/pdf'))
      throw new Error(t('pdfUnavailable'));
    const blob = await response.blob();
    if (blob.size === 0 || blob.size > 20 * 1024 * 1024) throw new Error(t('pdfUnavailable'));
    clearGeneratedPdf();
    const date = new Date().toISOString().slice(0, 10);
    const filename = ({
      en: `REVESTIK-Preliminary-Quotation-${date}.pdf`,
      de: `REVESTIK-Unverbindliche-Proforma-${date}.pdf`,
      es: `REVESTIK-Proforma-Preliminar-${date}.pdf`
    })[language()];
    pdfFile = new File([blob], filename, { type: 'application/pdf' });
    pdfObjectUrl = URL.createObjectURL(pdfFile);
    const download = find('[data-public-cart-pdf-download]');
    const panel = find('[data-public-cart-pdf-result]');
    const ready = find('[data-public-cart-pdf-ready]');
    const share = find('[data-public-cart-pdf-share]');
    if (!panel || !download) { clearGeneratedPdf(); throw new Error(t('pdfUnavailable')); }
    download.href = pdfObjectUrl;
    download.download = filename;
    download.hidden = false;
    if (ready) ready.textContent = t('pdfReady');
    if (share) share.hidden = !(navigator.share && navigator.canShare?.({ files: [pdfFile] }));
    panel.hidden = false;
  }

  document.addEventListener('click', event => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;
    const close = target.closest('[data-public-cart-notice-close]');
    if (close) {
      const panel = find('#public-cart-notice');
      if (panel) panel.hidden = true;
      return;
    }
    const retry = target.closest('[data-public-cart-retry]');
    if (retry) { event.preventDefault(); loadCart(); return; }
    const importButton = target.closest('[data-public-cart-import]');
    if (importButton) {
      event.preventDefault();
      withMutation(importButton, async () => {
        try { await importGuest(); }
        catch (error) { throw new Error(`${t('importPartial')} ${error instanceof Error ? error.message : ''}`); }
      });
      return;
    }
    const pdfButton = target.closest('[data-public-cart-pdf]');
    if (pdfButton) { event.preventDefault(); withMutation(pdfButton, generatePdf); return; }
    const pdfShare = target.closest('[data-public-cart-pdf-share]');
    if (pdfShare) {
      event.preventDefault();
      // Must be called synchronously from user activation; not after an awaited fetch.
      if (pdfFile && navigator.canShare?.({ files: [pdfFile] })) {
        navigator.share({ files: [pdfFile], title: 'REVESTIK' })
          .catch(error => { if (error?.name !== 'AbortError') notice(t('pdfShareError'), true); });
      } else notice(t('pdfShareError'), true);
      return;
    }
    const breakdownButton = target.closest('[data-public-cart-breakdown-toggle]');
    if (breakdownButton) { event.preventDefault(); withMutation(breakdownButton, showBreakdown); return; }
    const whatsappButton = target.closest('[data-public-cart-whatsapp]');
    if (whatsappButton) { event.preventDefault(); withMutation(whatsappButton, contactWhatsApp); return; }
    const copyButton = target.closest('[data-public-cart-copy]');
    if (copyButton) {
      event.preventDefault();
      const value = find('.public-cart-breakdown-pre')?.textContent;
      if (value && navigator.clipboard?.writeText) {
        navigator.clipboard.writeText(value).then(() => notice(t('copied'))).catch(() => notice(t('unavailableService'), true));
      }
      return;
    }
    const quick = target.closest('[data-public-cart-add]');
    if (quick) {
      event.preventDefault();
      const id = positiveId(quick.dataset.productId);
      if (id !== null) withMutation(quick, () => add(id, 1, quick.dataset.productSlug));
      return;
    }
    const save = target.closest('[data-public-cart-save]');
    if (save) {
      event.preventDefault();
      const id = positiveId(save.dataset.publicCartSave);
      const line = (accountState === 'guest' ? guestCart : cart).find(item => item.productId === id);
      const row = save.closest('[data-cart-item]');
      const input = row && find('.public-cart-qty-input', row);
      const quantity = input ? validQuantity(input.value) : null;
      if (!line || !line.isAvailable) return;
      if (quantity === null) return notice(t('invalidQuantity'), true);
      withMutation(save, async () => {
        if (accountState === 'guest') {
          saveGuest(guestCart.map(x => x.productId === line.productId ? { ...x, quantity } : x));
        } else {
          await sendMutation('PUT', line.productId, quantity, line.version);
        }
        notice(t('updateSuccess'));
      });
      return;
    }
    const remove = target.closest('[data-public-cart-remove]');
    if (remove) {
      event.preventDefault();
      const id = positiveId(remove.dataset.publicCartRemove);
      const line = (accountState === 'guest' ? guestCart : cart).find(item => item.productId === id);
      if (!line) return;
      withMutation(remove, async () => {
        if (accountState === 'guest') saveGuest(guestCart.filter(x => x.productId !== id));
        else await sendMutation('DELETE', line.productId, null, line.version);
        notice(t('removeSuccess'));
      });
    }
  });

  document.addEventListener('submit', event => {
    const form = event.target instanceof Element ? event.target.closest('[data-public-cart-add-form]') : null;
    if (!form) return;
    event.preventDefault();
    const control = find('button[type="submit"]', form);
    const input = find('input[name="quantity"]', form);
    const id = positiveId(form.dataset.productId);
    const amount = input ? validQuantity(input.value) : null;
    if (id === null || !control) return;
    if (amount === null) return notice(t('invalidQuantity'), true);
    withMutation(control, () => add(id, amount, form.dataset.productSlug));
  });

  // Delegated actions survive enhanced navigation; refresh view when a new SSR page appears.
  function initialize() {
    all('[data-public-cart-activate]').forEach(control => { control.disabled = false; });
    // This SSR hint avoids unauthenticated GETs on every public page.
    // It is presentation-only: the API still authorizes every read and write.
    guestCart = readGuest();
    const hint = sessionHint();
    if (hint === 'guest' || hint === 'forbidden') {
      accountState = hint;
      cart = [];
      render();
    } else {
      accountState = 'unknown';
      render();
      loadCart();
    }
  }
  window.addEventListener('beforeunload', clearGeneratedPdf);
  window.addEventListener('storage', event => {
    if (event.key !== GUEST_KEY) return;
    guestCart = readGuest();
    render();
  });
  document.addEventListener('enhancedload', initialize);
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initialize, { once: true });
  } else {
    initialize();
  }
})();
