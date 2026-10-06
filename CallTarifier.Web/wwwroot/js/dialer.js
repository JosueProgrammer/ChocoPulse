window.CallTarifierDialer = (() => {
  function init(options) {
    const valid = new Set(options?.validExtensions || []);
    const display = document.getElementById('dial-display');
    const error = document.getElementById('dial-error');
    const keypad = document.getElementById('numeric-keypad');
    const items = document.querySelectorAll('.extension-item');

    function setError(message) {
      if (!error) return;
      if (!message) {
        error.hidden = true;
        error.textContent = '';
        return;
      }
      error.hidden = false;
      error.textContent = message;
    }

    function setValue(value) {
      if (display) display.value = value;
      items.forEach((item) => {
        item.classList.toggle('is-selected', item.dataset.extension === value);
      });
      setError('');
    }

    function append(key) {
      const next = `${display?.value || ''}${key}`.slice(0, 8);
      setValue(next);
    }

    keypad?.addEventListener('click', (e) => {
      const btn = e.target.closest('[data-key]');
      if (!btn) return;
      append(btn.getAttribute('data-key'));
    });

    document.getElementById('dial-backspace')?.addEventListener('click', () => {
      setValue((display?.value || '').slice(0, -1));
    });

    document.getElementById('dial-clear')?.addEventListener('click', () => setValue(''));

    items.forEach((item) => {
      item.addEventListener('click', () => {
        setValue(item.dataset.extension || '');
      });
    });

    display?.addEventListener('input', () => {
      const cleaned = (display.value || '').replace(/[^\d*#]/g, '').slice(0, 8);
      setValue(cleaned);
    });

    document.getElementById('dial-call')?.addEventListener('click', async () => {
      const extension = (display?.value || '').trim();
      if (!extension) {
        setError('Ingrese o seleccione una extensión.');
        return;
      }
      if (!valid.has(extension)) {
        setError('La extensión no existe en el directorio.');
        window.CallTarifierToast?.show('Extensión inválida', 'error');
        return;
      }

      const selected = Array.from(items).find((i) => i.dataset.extension === extension);
      const branch = selected?.dataset.branch || '';

      try {
        await window.PhoneClient?.call(extension, branch);
      } catch (err) {
        setError('No se pudo iniciar la llamada.');
        window.CallTarifierToast?.show('Error al iniciar la llamada', 'error');
        console.error(err);
      }
    });
  }

  return { init };
})();
