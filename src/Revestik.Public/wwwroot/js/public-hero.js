// HOME-01B.1: lightweight, accessible automatic carousel; no dependencies.
(() => {
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  document.querySelectorAll('[data-hero-gallery]').forEach(gallery => {
    const slides = [...gallery.querySelectorAll('[data-hero-slide]')];
    const indicators = [...gallery.querySelectorAll('[data-hero-indicator]')];
    const previous = gallery.querySelector('[data-hero-prev]');
    const next = gallery.querySelector('[data-hero-next]');
    const toggle = gallery.querySelector('[data-hero-pause]');
    const status = gallery.querySelector('[data-hero-status]');
    const pauseIcon = gallery.querySelector('[data-hero-pause-icon]');
    if (slides.length < 2 || !previous || !next || !toggle || !status) return;

    let active = 0;
    let userPaused = prefersReducedMotion.matches;
    let timer = null;
    const canRotate = () => !userPaused && !prefersReducedMotion.matches && !document.hidden && !gallery.matches(':hover') && !gallery.contains(document.activeElement);
    const render = () => {
      slides.forEach((slide, index) => {
        const selected = index === active;
        slide.classList.toggle('is-active', selected);
        slide.setAttribute('aria-hidden', String(!selected));
      });
      indicators.forEach((indicator, index) => indicator.classList.toggle('is-active', index === active));
      status.textContent = `${String(active + 1).padStart(2, '0')} / ${String(slides.length).padStart(2, '0')}`;
    };
    const updatePause = () => {
      const label = userPaused ? toggle.dataset.playLabel : toggle.dataset.pauseLabel;
      toggle.setAttribute('aria-label', label || 'Toggle slideshow');
      toggle.title = label || 'Toggle slideshow';
      toggle.setAttribute('aria-pressed', String(userPaused));
      if (pauseIcon) pauseIcon.textContent = userPaused ? '▶' : 'Ⅱ';
    };
    const stop = () => { if (timer !== null) { clearInterval(timer); timer = null; } };
    const sync = () => {
      stop();
      if (canRotate()) timer = window.setInterval(() => { active = (active + 1) % slides.length; render(); }, 6000);
    };
    const move = delta => { active = (active + delta + slides.length) % slides.length; render(); sync(); };
    previous.addEventListener('click', () => move(-1));
    next.addEventListener('click', () => move(1));
    toggle.addEventListener('click', () => { userPaused = !userPaused; updatePause(); sync(); });
    gallery.addEventListener('mouseenter', sync);
    gallery.addEventListener('mouseleave', sync);
    gallery.addEventListener('focusin', sync);
    gallery.addEventListener('focusout', () => requestAnimationFrame(sync));
    document.addEventListener('visibilitychange', sync);
    prefersReducedMotion.addEventListener('change', () => { if (prefersReducedMotion.matches) userPaused = true; updatePause(); sync(); });
    render(); updatePause(); sync();
  });
})();
