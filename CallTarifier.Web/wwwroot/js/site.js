(() => {
  const menuToggle = document.getElementById('menu-toggle');
  const sidebar = document.getElementById('sidebar');
  const backdrop = document.getElementById('sidebar-backdrop');
  const closeBtn = document.getElementById('sidebar-close');

  function openSidebar() {
    sidebar?.classList.add('is-open');
    if (backdrop) backdrop.hidden = false;
  }

  function closeSidebar() {
    sidebar?.classList.remove('is-open');
    if (backdrop) backdrop.hidden = true;
  }

  menuToggle?.addEventListener('click', openSidebar);
  closeBtn?.addEventListener('click', closeSidebar);
  backdrop?.addEventListener('click', closeSidebar);

  document.querySelectorAll('[data-toast]').forEach((el) => {
    el.addEventListener('click', () => {
      window.CallTarifierToast?.show(el.getAttribute('data-toast') || '', 'info');
    });
  });
})();
