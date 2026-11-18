// PUBLIC-AUTH-01C: delegated events survive enhanced SSR navigation.
(() => {
  document.addEventListener('click', async event => {
    const button = event.target instanceof Element
      ? event.target.closest('[data-public-logout]')
      : null;
    if (!button || button.disabled) return;

    const container = button.closest('.public-auth-controls');
    const feedback = container?.querySelector('[data-public-auth-feedback]');
    if (feedback) feedback.textContent = '';
    button.disabled = true;
    try {
      const tokenResponse = await fetch('/api/auth/csrf', {
        method: 'GET', credentials: 'same-origin', cache: 'no-store',
        headers: { 'Accept': 'application/json' }
      });
      if (tokenResponse.status === 401) {
        window.location.assign(window.location.pathname);
        return;
      }
      if (!tokenResponse.ok) throw new Error('Could not retrieve antiforgery token');
      const { requestToken } = await tokenResponse.json();
      if (typeof requestToken !== 'string' || !requestToken) throw new Error('Missing antiforgery token');

      const response = await fetch('/api/auth/logout', {
        method: 'POST', credentials: 'same-origin', cache: 'no-store',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': requestToken },
        body: JSON.stringify({ confirm: true })
      });
      if (response.status !== 204 && response.status !== 401)
        throw new Error('Sign-out failed');
      window.location.assign(window.location.pathname);
    } catch {
      if (feedback) feedback.textContent = button.dataset.errorText || 'Sign-out failed';
      button.disabled = false;
    }
  });
})();
