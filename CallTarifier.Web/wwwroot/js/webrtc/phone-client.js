/**
 * PhoneClient — abstracción SIP/WebRTC.
 * Fase 1: stub local sin credenciales ni conexión real.
 * Fase 3: conectar con Isabel PBX 4 usando configuración segura del servidor.
 */
(() => {
  const States = Object.freeze({
    DISCONNECTED: 'DISCONNECTED',
    CONNECTING: 'CONNECTING',
    REGISTERING: 'REGISTERING',
    REGISTERED: 'REGISTERED',
    CALLING: 'CALLING',
    RINGING: 'RINGING',
    CONNECTED: 'CONNECTED',
    ENDING: 'ENDING',
    ERROR: 'ERROR'
  });

  class PhoneClient {
    constructor() {
      this.state = States.DISCONNECTED;
      this._listeners = new Set();
      this._muted = false;
      this._speaker = false;
      this._timerId = null;
      this._elapsed = 0;
      this._currentExtension = null;
      this._currentBranch = null;
    }

    onStateChange(listener) {
      this._listeners.add(listener);
      return () => this._listeners.delete(listener);
    }

    getState() {
      return this.state;
    }

    async connect(_config) {
      this._setState(States.CONNECTING);
      // No real SIP connection in Phase 1.
      await delay(250);
      this._setState(States.REGISTERING);
      await delay(250);
      this._setState(States.REGISTERED);
      console.info('WebRTC registration skipped (mock mode)');
      return true;
    }

    async register(_extension, _credentials) {
      this._setState(States.REGISTERING);
      await delay(200);
      this._setState(States.REGISTERED);
      console.info('WebRTC registration successful (mock)');
    }

    async call(extension, branch = '') {
      if (!extension) {
        throw new Error('Extensión requerida');
      }

      this._currentExtension = extension;
      this._currentBranch = branch;
      this._setState(States.CALLING);
      this._showActiveCall('Llamando', extension, branch);
      console.info('Call started (mock)', extension);

      await delay(1200);
      if (this.state === States.CALLING) {
        this._setState(States.RINGING);
        this._showActiveCall('Sonando', extension, branch);
      }

      await delay(1000);
      if (this.state === States.RINGING || this.state === States.CALLING) {
        this._setState(States.CONNECTED);
        this._showActiveCall('Conectado', extension, branch);
        this._startTimer();
        console.info('Call answered (mock)', extension);
      }
    }

    async answer() {
      this._hideIncoming();
      this._setState(States.CONNECTED);
      this._showActiveCall('Conectado', this._currentExtension || '—', this._currentBranch || '');
      this._startTimer();
    }

    async reject() {
      this._hideIncoming();
      this._setState(States.REGISTERED);
    }

    async hangup() {
      this._setState(States.ENDING);
      this._showActiveCall('Finalizando', this._currentExtension || '—', this._currentBranch || '');
      this._stopTimer();
      await delay(400);
      this._hideActiveCall();
      this._setState(States.REGISTERED);
      console.info('Call ended (mock)');
    }

    setMute(muted) {
      this._muted = !!muted;
      return this._muted;
    }

    toggleMute() {
      return this.setMute(!this._muted);
    }

    setSpeaker(enabled) {
      this._speaker = !!enabled;
      return this._speaker;
    }

    toggleSpeaker() {
      return this.setSpeaker(!this._speaker);
    }

    simulateIncoming(extension, branch) {
      this._currentExtension = extension;
      this._currentBranch = branch;
      this._setState(States.RINGING);
      const modal = document.getElementById('incoming-call-modal');
      const extEl = document.getElementById('incoming-ext');
      const branchEl = document.getElementById('incoming-branch');
      if (extEl) extEl.textContent = extension;
      if (branchEl) branchEl.textContent = branch;
      if (modal) modal.hidden = false;
    }

    _setState(next) {
      this.state = next;
      this._listeners.forEach((fn) => {
        try { fn(next); } catch { /* ignore */ }
      });
      this._syncStatusUi(next);
    }

    _syncStatusUi(state) {
      const el = document.getElementById('phone-status');
      if (!el) return;
      el.dataset.state = state;
      const text = el.querySelector('.status-text');
      if (text) {
        const labels = {
          DISCONNECTED: 'Desconectado',
          CONNECTING: 'Conectando...',
          REGISTERING: 'Registrando...',
          REGISTERED: 'Registrado',
          CALLING: 'Llamando...',
          RINGING: 'Sonando...',
          CONNECTED: 'En llamada',
          ENDING: 'Finalizando...',
          ERROR: 'Error'
        };
        text.textContent = labels[state] || state;
      }

      const settingsState = document.getElementById('settings-phone-state');
      if (settingsState) {
        settingsState.textContent = labelsSafe(state);
      }
    }

    _showActiveCall(stateLabel, extension, branch) {
      const modal = document.getElementById('active-call-modal');
      const stateEl = document.getElementById('active-call-state');
      const titleEl = document.getElementById('active-call-title');
      const extEl = document.getElementById('active-call-ext');
      if (stateEl) stateEl.textContent = stateLabel;
      if (titleEl) titleEl.textContent = branch || 'Extensión';
      if (extEl) extEl.textContent = extension;
      if (modal) modal.hidden = false;
    }

    _hideActiveCall() {
      const modal = document.getElementById('active-call-modal');
      if (modal) modal.hidden = true;
      const timer = document.getElementById('active-call-timer');
      if (timer) timer.textContent = '00:00:00';
    }

    _hideIncoming() {
      const modal = document.getElementById('incoming-call-modal');
      if (modal) modal.hidden = true;
    }

    _startTimer() {
      this._stopTimer();
      this._elapsed = 0;
      this._timerId = window.setInterval(() => {
        this._elapsed += 1;
        const timer = document.getElementById('active-call-timer');
        if (timer) timer.textContent = formatHms(this._elapsed);
      }, 1000);
    }

    _stopTimer() {
      if (this._timerId) {
        window.clearInterval(this._timerId);
        this._timerId = null;
      }
    }
  }

  function delay(ms) {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }

  function formatHms(totalSeconds) {
    const h = String(Math.floor(totalSeconds / 3600)).padStart(2, '0');
    const m = String(Math.floor((totalSeconds % 3600) / 60)).padStart(2, '0');
    const s = String(totalSeconds % 60).padStart(2, '0');
    return `${h}:${m}:${s}`;
  }

  function labelsSafe(state) {
    const map = {
      DISCONNECTED: 'Desconectado',
      CONNECTING: 'Conectando...',
      REGISTERING: 'Registrando...',
      REGISTERED: 'Registrado',
      CALLING: 'Llamando...',
      RINGING: 'Sonando...',
      CONNECTED: 'En llamada',
      ENDING: 'Finalizando...',
      ERROR: 'Error'
    };
    return map[state] || state;
  }

  const client = new PhoneClient();

  document.getElementById('call-hangup')?.addEventListener('click', () => client.hangup());
  document.getElementById('call-mute')?.addEventListener('click', (e) => {
    const muted = client.toggleMute();
    e.currentTarget.textContent = muted ? 'Activar mic' : 'Silenciar';
  });
  document.getElementById('call-speaker')?.addEventListener('click', (e) => {
    const on = client.toggleSpeaker();
    e.currentTarget.textContent = on ? 'Altavoz ON' : 'Altavoz';
  });
  document.getElementById('incoming-answer')?.addEventListener('click', () => client.answer());
  document.getElementById('incoming-reject')?.addEventListener('click', () => client.reject());

  // Auto-bootstrap mock registration for authenticated shell pages.
  if (document.body.classList.contains('app-shell')) {
    client.connect({}).catch(() => client._setState(States.ERROR));
  }

  window.PhoneClient = client;
  window.PhoneStates = States;
})();
