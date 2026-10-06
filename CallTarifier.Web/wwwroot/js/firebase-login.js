(() => {
  const form = document.getElementById('login-form');
  const submit = document.getElementById('login-submit');
  const toggle = document.getElementById('toggle-password');
  const passwordInput = document.getElementById('password-input');
  const emailInput = document.getElementById('Input_Email') || form?.querySelector('input[type="email"]');
  const tokenInput = document.getElementById('id-token');
  const errorBox = document.getElementById('login-error');
  const forgotBtn = document.getElementById('forgot-password');

  function setLoading(isLoading) {
    if (!submit) return;
    submit.classList.toggle('is-loading', isLoading);
    submit.toggleAttribute('disabled', isLoading);
    const label = submit.querySelector('.btn-label');
    const loading = submit.querySelector('.btn-loading');
    if (isLoading) {
      label?.setAttribute('hidden', '');
      loading?.removeAttribute('hidden');
    } else {
      loading?.setAttribute('hidden', '');
      label?.removeAttribute('hidden');
    }
  }

  function showError(message) {
    if (!errorBox) {
      window.CallTarifierToast?.show(message, 'error');
      return;
    }
    errorBox.textContent = message;
    errorBox.classList.add('form-error');
  }

  function mapFirebaseError(code) {
    switch (code) {
      case 'auth/invalid-email':
        return 'El correo no es válido.';
      case 'auth/user-disabled':
        return 'Esta cuenta está deshabilitada.';
      case 'auth/user-not-found':
      case 'auth/wrong-password':
      case 'auth/invalid-credential':
        return 'Correo o contraseña incorrectos.';
      case 'auth/too-many-requests':
        return 'Demasiados intentos. Intente más tarde.';
      case 'auth/network-request-failed':
        return 'Error de red. Verifique su conexión.';
      default:
        return 'No se pudo iniciar sesión. Intente nuevamente.';
    }
  }

  toggle?.addEventListener('click', () => {
    if (!passwordInput) return;
    const show = passwordInput.type === 'password';
    passwordInput.type = show ? 'text' : 'password';
    toggle.textContent = show ? 'Ocultar' : 'Mostrar';
  });

  if (!window.__firebaseConfig || !window.firebase) {
    showError('Firebase no está disponible en esta página.');
    return;
  }

  firebase.initializeApp(window.__firebaseConfig);
  const auth = firebase.auth();

  forgotBtn?.addEventListener('click', async () => {
    const email = (emailInput?.value || '').trim();
    if (!email) {
      showError('Ingrese su correo para recuperar la contraseña.');
      return;
    }
    try {
      await auth.sendPasswordResetEmail(email);
      window.CallTarifierToast?.show('Se envió un correo de recuperación.', 'success');
    } catch (err) {
      showError(mapFirebaseError(err?.code));
    }
  });

  form?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const email = (emailInput?.value || '').trim();
    const password = passwordInput?.value || '';

    if (!email || !password) {
      showError('Correo y contraseña son obligatorios.');
      return;
    }

    setLoading(true);
    try {
      const credential = await auth.signInWithEmailAndPassword(email, password);
      const idToken = await credential.user.getIdToken();
      if (tokenInput) tokenInput.value = idToken;
      form.submit();
    } catch (err) {
      setLoading(false);
      showError(mapFirebaseError(err?.code));
      console.error('Firebase login error', err?.code || err);
    }
  });
})();
