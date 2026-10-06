(() => {
  const toggle = document.getElementById('toggle-password');
  const input = document.getElementById('password-input');
  const form = document.getElementById('login-form');
  const submit = document.getElementById('login-submit');
  const forgotBtn = document.getElementById('forgot-password');

  toggle?.addEventListener('click', () => {
    if (!input) return;
    const show = input.type === 'password';
    input.type = show ? 'text' : 'password';
    toggle.textContent = show ? 'Ocultar' : 'Mostrar';
  });

  forgotBtn?.addEventListener('click', () => {
    window.CallTarifierToast?.show('En modo mock, contacte al administrador.', 'info');
  });

  form?.addEventListener('submit', () => {
    submit?.classList.add('is-loading');
    submit?.querySelector('.btn-label')?.setAttribute('hidden', '');
    submit?.querySelector('.btn-loading')?.removeAttribute('hidden');
    submit?.setAttribute('disabled', 'disabled');
  });
})();
