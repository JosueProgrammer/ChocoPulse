window.CallTarifierToast = (() => {
  function show(message, type = 'info', timeoutMs = 3500) {
    const host = document.getElementById('toast-host');
    if (!host || !message) return;

    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.setAttribute('role', 'status');
    toast.textContent = message;
    host.appendChild(toast);

    window.setTimeout(() => {
      toast.remove();
    }, timeoutMs);
  }

  return { show };
})();
