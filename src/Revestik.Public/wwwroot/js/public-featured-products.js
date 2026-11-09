// PUBLIC-03A: independent product carousels; no automatic animation or dependencies.
(() => {
  document.querySelectorAll('[data-featured-gallery]').forEach(gallery => {
    const slides = [...gallery.querySelectorAll('[data-featured-slide]')];
    const previous = gallery.querySelector('[data-featured-prev]');
    const next = gallery.querySelector('[data-featured-next]');
    const status = gallery.querySelector('[data-featured-status]');
    if (slides.length < 2 || !previous || !next || !status) return;

    let active = 0;
    const render = () => {
      slides.forEach((slide, index) => {
        const selected = index === active;
        if (selected && slide.dataset.featuredSrc) {
          slide.src = slide.dataset.featuredSrc;
          delete slide.dataset.featuredSrc;
        }
        slide.hidden = !selected;
        slide.setAttribute('aria-hidden', String(!selected));
      });
      status.textContent = `${active + 1} / ${slides.length}`;
    };
    const move = step => {
      active = (active + step + slides.length) % slides.length;
      render();
    };
    previous.addEventListener('click', () => move(-1));
    next.addEventListener('click', () => move(1));
    // The visible photograph is a normal link to the public product detail.
    // With JavaScript disabled, the first photograph and link still work.
    render();
  });
})();
