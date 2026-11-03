/* PUBLIC-GLASS-01A: progressive enhancement for explicitly selectable public photos.
   No framework dependency and no external media sources. */
(() => {
  'use strict';

  const dialog = document.getElementById('public-image-viewer');
  if (!dialog || typeof dialog.showModal !== 'function') return;

  const stage = dialog.querySelector('[data-viewer-stage]');
  const title = dialog.querySelector('[data-viewer-title]');
  const count = dialog.querySelector('[data-viewer-count]');
  const previous = dialog.querySelector('[data-viewer-prev]');
  const next = dialog.querySelector('[data-viewer-next]');
  const close = dialog.querySelector('[data-viewer-close]');
  if (!stage || !title || !count || !previous || !next || !close) return;

  const language = (document.documentElement.lang || 'es').slice(0, 2);
  const translations = {
    es: { viewer: 'Visor de fotografías', close: 'Cerrar fotografía', previous: 'Fotografía anterior', next: 'Fotografía siguiente' },
    en: { viewer: 'Photo viewer', close: 'Close photograph', previous: 'Previous photograph', next: 'Next photograph' },
    de: { viewer: 'Fotobetrachter', close: 'Foto schließen', previous: 'Vorheriges Foto', next: 'Nächstes Foto' }
  }[language] || { viewer: 'Visor de fotografías', close: 'Cerrar fotografía', previous: 'Fotografía anterior', next: 'Fotografía siguiente' };
  dialog.setAttribute('aria-label', translations.viewer);
  close.setAttribute('aria-label', translations.close);
  previous.setAttribute('aria-label', translations.previous);
  next.setAttribute('aria-label', translations.next);

  let images = [];
  let current = 0;
  let opener = null;

  function render() {
    const trigger = images[current];
    if (!trigger) return;
    const label = trigger.dataset.imageLabel || '';
    const source = trigger.dataset.fullSrc || '';
    stage.replaceChildren();

    if (source) {
      const image = document.createElement('img');
      image.src = source;
      image.alt = label;
      image.decoding = 'async';
      stage.append(image);
    } else {
      // Existing pages contain honest placeholders; never invent project photography.
      const placeholder = document.createElement('div');
      placeholder.className = 'public-image-viewer-placeholder';
      const symbol = document.createElement('span');
      symbol.className = 'public-image-viewer-placeholder-mark';
      symbol.setAttribute('aria-hidden', 'true');
      symbol.textContent = '□';
      const note = document.createElement('span');
      note.className = 'public-image-viewer-placeholder-note';
      note.textContent = trigger.dataset.placeholderNote || label;
      placeholder.append(symbol, note);
      stage.append(placeholder);
    }
    title.textContent = label;
    count.textContent = `${current + 1} / ${images.length}`;
    previous.disabled = images.length <= 1;
    next.disabled = images.length <= 1;
  }

  function move(step) {
    if (!dialog.open || images.length <= 1) return;
    current = (current + step + images.length) % images.length;
    render();
  }

  document.addEventListener('click', event => {
    const trigger = event.target.closest?.('[data-public-image]');
    if (!(trigger instanceof HTMLButtonElement) || dialog.open) return;
    const group = trigger.dataset.gallery || '';
    images = [...document.querySelectorAll('button[data-public-image]')]
      .filter(item => item.dataset.gallery === group);
    current = images.indexOf(trigger);
    if (current < 0) return;
    opener = trigger;
    render();
    dialog.showModal();
    document.body.classList.add('public-viewer-open');
    close.focus();
  });

  close.addEventListener('click', () => dialog.close());
  previous.addEventListener('click', () => move(-1));
  next.addEventListener('click', () => move(1));
  dialog.addEventListener('click', event => {
    if (event.target === dialog) dialog.close();
  });
  dialog.addEventListener('keydown', event => {
    if (event.key === 'ArrowLeft') { event.preventDefault(); move(-1); }
    if (event.key === 'ArrowRight') { event.preventDefault(); move(1); }
  });
  dialog.addEventListener('close', () => {
    document.body.classList.remove('public-viewer-open');
    if (opener?.isConnected) opener.focus();
    opener = null;
    images = [];
  });
})();
