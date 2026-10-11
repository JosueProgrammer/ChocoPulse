(() => {
  const form = document.getElementById('login-form');
  const submit = document.getElementById('login-submit');
  const googleBtn = document.getElementById('google-login');
  const toggle = document.getElementById('toggle-password');
  const passwordInput = document.getElementById('password-input');
  const emailInput = document.getElementById('Input_Email') || form?.querySelector('input[type="email"]');
  const tokenInput = document.getElementById('id-token');
  const errorBox = document.getElementById('login-error');
  const forgotBtn = document.getElementById('forgot-password');

  function setLoading(isLoading, which = 'email') {
    const btn = which === 'google' ? googleBtn : submit;
    if (!btn) return;
    btn.classList.toggle('is-loading', isLoading);
    btn.toggleAttribute('disabled', isLoading);
    if (which === 'email' && submit) {
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
    if (googleBtn) googleBtn.toggleAttribute('disabled', isLoading);
    if (submit) submit.toggleAttribute('disabled', isLoading);
  }

  function showError(message) {
    if (!errorBox) {
      if (message) window.CallTarifierToast?.show(message, 'error');
      return;
    }
    errorBox.textContent = message || '';
    if (message) errorBox.classList.add('form-error');
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
      case 'auth/popup-closed-by-user':
        return 'Inicio con Google cancelado.';
      case 'auth/popup-blocked':
        return 'El navegador bloqueó la ventana de Google. Permita ventanas emergentes.';
      case 'auth/cancelled-popup-request':
        return 'Se canceló el inicio con Google.';
      case 'auth/account-exists-with-different-credential':
        return 'Ya existe una cuenta con este correo usando otro método.';
      case 'auth/unauthorized-domain':
        return 'Este dominio no está autorizado en Firebase (Authorized domains).';
      default:
        return 'No se pudo iniciar sesión. Intente nuevamente.';
    }
  }

  async function completeLogin(user) {
    const idToken = await user.getIdToken();
    if (tokenInput) tokenInput.value = idToken;
    // Avoid re-triggering the submit listener: use native submit.
    HTMLFormElement.prototype.submit.call(form);
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
  const googleProvider = new firebase.auth.GoogleAuthProvider();
  googleProvider.setCustomParameters({ prompt: 'select_account' });

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

  googleBtn?.addEventListener('click', async () => {
    setLoading(true, 'google');
    showError('');
    try {
      const result = await auth.signInWithPopup(googleProvider);
      await completeLogin(result.user);
    } catch (err) {
      // Fallback if popup is blocked
      if (err?.code === 'auth/popup-blocked') {
        try {
          await auth.signInWithRedirect(googleProvider);
          return;
        } catch (redirectErr) {
          setLoading(false, 'google');
          showError(mapFirebaseError(redirectErr?.code));
          console.error('Google redirect error', redirectErr?.code || redirectErr);
          return;
        }
      }
      setLoading(false, 'google');
      showError(mapFirebaseError(err?.code));
      console.error('Google login error', err?.code || err);
    }
  });

  // Complete redirect flow (if popup was blocked previously)
  auth.getRedirectResult()
    .then(async (result) => {
      if (result?.user) {
        setLoading(true, 'google');
        await completeLogin(result.user);
      }
    })
    .catch((err) => {
      showError(mapFirebaseError(err?.code));
      console.error('Google redirect result error', err?.code || err);
    });

  form?.addEventListener('submit', async (event) => {
    // Native post after we set IdToken should not be intercepted again.
    if (tokenInput?.value) {
      return;
    }

    event.preventDefault();
    const email = (emailInput?.value || '').trim();
    const password = passwordInput?.value || '';

    if (!email || !password) {
      showError('Correo y contraseña son obligatorios.');
      return;
    }

    setLoading(true, 'email');
    try {
      const credential = await auth.signInWithEmailAndPassword(email, password);
      await completeLogin(credential.user);
    } catch (err) {
      setLoading(false, 'email');
      showError(mapFirebaseError(err?.code));
      console.error('Firebase login error', err?.code || err);
    }
  });
})();
