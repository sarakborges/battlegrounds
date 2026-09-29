(() => {
  const ui = window.BgUi = window.BgUi || {};
  const app = document.getElementById('app');
  const toast = document.getElementById('toast');
  let state = null;
  let toastTimer = null;

  function send(type, payload = {}) {
    if (typeof window.sendIpcMessage !== 'function') {
      showError('Godot CEF IPC is not available.');
      return;
    }
    window.sendIpcMessage(JSON.stringify({ type, ...payload }));
  }

  function showError(message) {
    toast.textContent = message;
    toast.classList.add('visible');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => toast.classList.remove('visible'), 4200);
  }

  function render() {
    if (!state) return;

    if (state.status === 'leader-selection') {
      app.innerHTML = ui.screens.renderLeaderSelection(state);
    } else if (state.status === 'match' || state.status === 'finished') {
      app.innerHTML = ui.screens.renderMatch(state);
    } else if (state.status === 'combat') {
      app.replaceChildren();
    } else {
      app.innerHTML = ui.screens.renderLoading(state);
    }

    ui.theme.applyComponentStyles(state.theme, app);
  }

  function receive(raw) {
    try {
      const message = typeof raw === 'string' ? JSON.parse(raw) : raw;
      if (!message || typeof message.type !== 'string') return;

      if (message.type === 'state') {
        state = message.payload;
        ui.theme.applyTheme(state?.theme, state?.status === 'combat' ? 'combat' : 'preparation');
        render();
        window.dispatchEvent(new CustomEvent('battlegrounds:state', { detail: state }));
      } else if (message.type === 'error') {
        showError(message.payload?.message ?? 'Unknown game error');
      }
    } catch (error) {
      showError(`Invalid game message: ${error.message}`);
    }
  }

  function payloadFor(element, action) {
    switch (action) {
      case 'select-leader': return { leaderId: element.dataset.leaderId };
      case 'acquire':
      case 'deploy':
      case 'play-action':
      case 'release': return { slot: Number(element.dataset.slot) };
      case 'resolve-choice': return { optionIndex: Number(element.dataset.optionIndex) };
      case 'select-target':
      case 'toggle-combine-unit': return { unitInstanceId: Number(element.dataset.unitInstanceId) };
      case 'select-combine-recipe': return { combineId: element.dataset.combineId };
      default: return {};
    }
  }

  document.addEventListener('click', event => {
    const element = event.target.closest('[data-action]');
    if (!element || element.disabled) return;
    const action = element.dataset.action;
    send(action, payloadFor(element, action));
  });

  if (window.ipcMessage?.addListener) {
    window.ipcMessage.addListener(receive);
  } else {
    window.onIpcMessage = receive;
  }

  ui.bridge = { send, showError };
  window.addEventListener('DOMContentLoaded', () => send('request-state'));
})();
