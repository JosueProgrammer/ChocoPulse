/**
 * PhoneClient — JsSIP over WebRTC against Issabel (WSS).
 * Credentials from GET /api/webrtc/session (never hardcoded).
 * Library: JsSIP (browser UMD). SIP.js equivalent flow.
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
      this._speaker = true;
      this._timerId = null;
      this._elapsed = 0;
      this._sessionCfg = null;
      this._ua = null;
      this._probe = null;
      this._session = null;
      this._remoteAudio = null;
      this._connecting = null;
      this._pbxWatch = null;
      this._usingPbx = false;
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

    connect() {
      if (this._ua?.isConnected() && this._sessionCfg) {
        return Promise.resolve(true);
      }
      if (this._connecting) return this._connecting;

      this._connecting = this._connect().finally(() => {
        this._connecting = null;
      });
      return this._connecting;
    }

    async _connect() {
      try {
        this._setState(States.CONNECTING);
        console.info('WebRTC registration started');

        if (typeof JsSIP === 'undefined') {
          throw new Error('JsSIP no cargado');
        }

        const res = await fetch('/api/webrtc/session', {
          credentials: 'same-origin',
          headers: { Accept: 'application/json' }
        });

        if (!res.ok) {
          if (await this._openSocketOnly()) return true;
          const body = await res.json().catch(() => ({}));
          throw new Error(body.error || 'No se pudo abrir la sesión de tu extensión');
        }

        this._sessionCfg = await res.json();
        await this._startUa();
        await waitUntil(() => this._ua?.isConnected() || this.state === States.ERROR, 8000);
        if (!this._ua?.isConnected()) {
          if (await this._openSocketOnly()) return true;
          throw new Error('No hay conexión con la central');
        }
        return true;
      } catch (err) {
        console.error('WebRTC connect failed', err);
        this._setState(States.ERROR);
        throw err;
      }
    }

    async _startUa() {
      const cfg = this._sessionCfg;
      this._ensureRemoteAudio();

      if (this._ua) {
        try { this._ua.stop(); } catch { /* ignore */ }
      }

      const socket = new JsSIP.WebSocketInterface(cfg.webSocketUrl);
      const uri = `sip:${cfg.authorizationUsername}@${cfg.sipDomain}`;

      this._ua = new JsSIP.UA({
        sockets: [socket],
        uri,
        password: cfg.authorizationPassword,
        authorization_user: cfg.authorizationUsername,
        display_name: cfg.displayName || cfg.extension,
        session_timers: false,
        register: false,
        pcConfig: cfg.iceServersEnabled
          ? { iceServers: [{ urls: 'stun:stun.l.google.com:19302' }] }
          : { iceServers: [] }
      });

      const ua = this._ua;
      const current = () => this._ua === ua;
      ua.on('connecting', () => { if (current() && !this._usingPbx) this._setState(States.CONNECTING); });
      ua.on('connected', () => { if (current() && !this._usingPbx) this._setState(States.REGISTERED); });
      ua.on('registered', () => {
        if (!current()) return;
        this._setState(States.REGISTERED);
        console.info('WebRTC registration successful');
      });
      ua.on('unregistered', () => {
        if (current() && this.state !== States.ERROR && this.state !== States.ENDING) {
          this._setState(States.DISCONNECTED);
        }
      });
      ua.on('registrationFailed', (e) => {
        if (!current()) return;
        console.error('WebRTC registration failed', e?.cause);
        this._setState(States.ERROR);
        window.CallTarifierToast?.show('Error de registro SIP', 'error');
      });
      ua.on('disconnected', () => {
        if (current() && this.state !== States.ENDING && this.state !== States.CALLING && this.state !== States.RINGING && this.state !== States.CONNECTED) {
          this._setState(States.DISCONNECTED);
        }
      });
      ua.on('newRTCSession', (data) => { if (current()) this._onNewSession(data); });

      this._ua.start();
    }

    _openSocketOnly() {
      const url = window.__webrtcConfig?.webSocketUrl;
      if (!url) return Promise.resolve(false);
      if (this._ua?.isConnected() || this._probe?.readyState === WebSocket.OPEN) {
        if (!this._usingPbx) this._setState(States.REGISTERED);
        return Promise.resolve(true);
      }
      return new Promise((resolve) => {
        let ws;
        try {
          ws = new WebSocket(url, 'sip');
        } catch {
          resolve(false);
          return;
        }
        const timer = window.setTimeout(() => {
          try { ws.close(); } catch { /* ignore */ }
          resolve(false);
        }, 8000);
        ws.onopen = () => {
          window.clearTimeout(timer);
          this._probe = ws;
          if (!this._usingPbx) this._setState(States.REGISTERED);
          resolve(true);
        };
        ws.onerror = () => {
          window.clearTimeout(timer);
          resolve(false);
        };
      });
    }

    async call(extension, branch = '') {
      if (!extension) {
        throw new Error('Extensión requerida');
      }

      this._currentExtension = extension;
      this._currentBranch = branch;
      this._usingPbx = true;
      this._elapsed = 0;
      this._setState(States.CALLING);
      this._showActiveCall('Llamando', extension, branch);
      const timer = document.getElementById('active-call-timer');
      if (timer) timer.textContent = '00:00:00';
      console.info('Call started', extension);

      const res = await fetch('/api/calls/dial', {
        method: 'POST',
        credentials: 'same-origin',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({ extension })
      });
      const body = await res.json().catch(() => ({}));
      if (!res.ok) {
        this._usingPbx = false;
        this._stopTimer();
        this._cleanupCall();
        this._setState(this._ua?.isConnected() || this._probe ? States.REGISTERED : States.DISCONNECTED);
        throw new Error(body.error || 'No se pudo timbrar la extensión');
      }

      this._showActiveCall('Sonando', extension, branch);
      this._setState(States.RINGING);
      this._watchPbxCall();
    }

    async answer() {
      if (!this._session || !this._session.isInProgress()) return;
      this._hideIncoming();
      this._session.answer({
        mediaConstraints: { audio: true, video: false }
      });
      console.info('Call answered');
    }

    async reject() {
      if (this._session && this._session.isInProgress()) {
        try { this._session.terminate(); } catch { /* ignore */ }
      }
      this._cleanupCall();
      this._setState(States.REGISTERED);
    }

    async hangup() {
      this._setState(States.ENDING);
      this._showActiveCall('Finalizando', this._currentExtension || '—', this._currentBranch || '');
      this._stopTimer();
      this._stopPbxWatch();
      if (this._usingPbx) {
        try {
          await fetch('/api/calls/hangup', { method: 'POST', credentials: 'same-origin' });
        } catch (err) {
          console.warn('Hangup error', err);
        }
        this._usingPbx = false;
      }
      try {
        this._session?.terminate();
      } catch (err) {
        console.warn('Hangup error', err);
      }
      this._cleanupCall();
      this._setState(this._ua?.isRegistered() ? States.REGISTERED : States.DISCONNECTED);
      console.info('Call ended');
    }

    _watchPbxCall() {
      this._stopPbxWatch();
      this._pbxWatch = window.setInterval(async () => {
        try {
          const res = await fetch('/api/calls/status', { credentials: 'same-origin', headers: { Accept: 'application/json' } });
          if (!res.ok) return;
          const data = await res.json();
          if (data.state === 'ringing') {
            this._setState(States.RINGING);
            this._showActiveCall('Sonando', this._currentExtension, this._currentBranch);
          } else if (data.state === 'connected') {
            this._setState(States.CONNECTED);
            this._showActiveCall('Conectado', this._currentExtension, this._currentBranch);
            if (!this._timerId) this._startTimer();
            if (typeof data.elapsedSeconds === 'number' && data.elapsedSeconds >= this._elapsed) {
              this._elapsed = data.elapsedSeconds;
              const timer = document.getElementById('active-call-timer');
              if (timer) timer.textContent = formatHms(this._elapsed);
            }
          } else if (data.state === 'ended' || data.state === 'failed' || data.state === 'idle') {
            const failed = data.state === 'failed';
            this._stopPbxWatch();
            this._stopTimer();
            this._usingPbx = false;
            this._cleanupCall();
            this._setState(States.REGISTERED);
            if (failed && data.error) window.CallTarifierToast?.show(data.error, 'error');
          }
        } catch { /* sigue el temporizador local */ }
      }, 1000);
    }

    _stopPbxWatch() {
      if (this._pbxWatch) {
        window.clearInterval(this._pbxWatch);
        this._pbxWatch = null;
      }
    }

    setMute(muted) {
      this._muted = !!muted;
      try {
        if (this._muted) this._session?.mute({ audio: true });
        else this._session?.unmute({ audio: true });
      } catch { /* ignore */ }
      return this._muted;
    }

    toggleMute() {
      return this.setMute(!this._muted);
    }

    setSpeaker(enabled) {
      this._speaker = !!enabled;
      if (this._remoteAudio) this._remoteAudio.muted = !this._speaker;
      return this._speaker;
    }

    toggleSpeaker() {
      return this.setSpeaker(!this._speaker);
    }

    _onNewSession(data) {
      const session = data.session;
      this._session = session;

      session.on('peerconnection', (e) => this._bindPeerConnection(e.peerconnection));
      session.on('ended', () => {
        this._stopTimer();
        this._cleanupCall();
        this._setState(States.REGISTERED);
        console.info('Call ended');
      });
      session.on('failed', () => {
        this._stopTimer();
        this._cleanupCall();
        this._setState(States.REGISTERED);
      });
      session.on('accepted', () => {
        this._setState(States.CONNECTED);
        this._showActiveCall('Conectado', this._currentExtension || '—', this._currentBranch || '');
        this._startTimer();
        console.info('Call answered');
      });

      if (session.direction === 'incoming') {
        const from = session.remote_identity?.uri?.user || '—';
        this._currentExtension = from;
        this._currentBranch = '';
        this._setState(States.RINGING);

        const modal = document.getElementById('incoming-call-modal');
        const extEl = document.getElementById('incoming-ext');
        const branchEl = document.getElementById('incoming-branch');
        if (extEl) extEl.textContent = from;
        if (branchEl) branchEl.textContent = 'Llamada entrante';
        if (modal) modal.hidden = false;
      }
    }

    _bindPeerConnection(pc) {
      if (!pc || !this._remoteAudio) return;
      pc.addEventListener('track', (event) => {
        if (event.streams && event.streams[0]) {
          this._remoteAudio.srcObject = event.streams[0];
        } else {
          const stream = new MediaStream([event.track]);
          this._remoteAudio.srcObject = stream;
        }
        this._remoteAudio.muted = !this._speaker;
        this._remoteAudio.play().catch(() => { /* autoplay */ });
      });
    }

    _ensureRemoteAudio() {
      if (this._remoteAudio) return;
      let el = document.getElementById('webrtc-remote-audio');
      if (!el) {
        el = document.createElement('audio');
        el.id = 'webrtc-remote-audio';
        el.autoplay = true;
        el.playsInline = true;
        el.style.display = 'none';
        document.body.appendChild(el);
      }
      this._remoteAudio = el;
    }

    _cleanupCall() {
      this._session = null;
      this._hideActiveCall();
      this._hideIncoming();
      if (this._remoteAudio) this._remoteAudio.srcObject = null;
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
      if (el) {
        el.dataset.state = state;
        const text = el.querySelector('.status-text');
        if (text) text.textContent = labelFor(state);
      }
      const settingsState = document.getElementById('settings-phone-state');
      if (settingsState) {
        settingsState.textContent = labelFor(state);
        settingsState.className = state === 'ERROR'
          ? 'badge badge-warning'
          : (state === 'REGISTERED' || state === 'CONNECTED' ? 'badge badge-success' : 'badge badge-neutral');
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

  function formatHms(totalSeconds) {
    const h = String(Math.floor(totalSeconds / 3600)).padStart(2, '0');
    const m = String(Math.floor((totalSeconds % 3600) / 60)).padStart(2, '0');
    const s = String(totalSeconds % 60).padStart(2, '0');
    return `${h}:${m}:${s}`;
  }

  function labelFor(state) {
    const map = {
      DISCONNECTED: 'Desconectado',
      CONNECTING: 'Conectando...',
      REGISTERING: 'Registrando...',
      REGISTERED: 'Conectado',
      CALLING: 'Llamando...',
      RINGING: 'Sonando...',
      CONNECTED: 'En llamada',
      ENDING: 'Finalizando...',
      ERROR: 'Error WebRTC'
    };
    return map[state] || state;
  }

  function waitUntil(predicate, timeoutMs) {
    return new Promise((resolve) => {
      const start = Date.now();
      const id = setInterval(() => {
        if (predicate() || Date.now() - start > timeoutMs) {
          clearInterval(id);
          resolve();
        }
      }, 100);
    });
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

  if (document.body.classList.contains('app-shell')) {
    client.connect().catch((err) => {
      window.CallTarifierToast?.show(err.message || 'Error WebRTC', 'error');
    });
  }

  window.PhoneClient = client;
  window.PhoneStates = States;
})();
