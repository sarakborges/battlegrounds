import { bindActions } from './bridge/actions.js';
import { createGameBridge } from './bridge/game-bridge.js';
import { createScreen } from './screens/index.js';
import { applyComponentStyles, applyTheme } from './theme/theme.js';

const app = document.getElementById('app');
const toast = document.getElementById('toast');
let toastTimer = null;
let renderVersion = 0;

function showError(message) {
  toast.textContent = message;
  toast.classList.add('visible');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => toast.classList.remove('visible'), 4200);
}

const bridge = createGameBridge({
  onError: showError,
  onState: async state => {
    const version = ++renderVersion;
    applyTheme(state?.theme, state?.status === 'combat' ? 'combat' : 'preparation');
    bridge.setPolling(state?.status === 'combat');

    try {
      const screen = await createScreen(state);
      if (version !== renderVersion) return;
      app.replaceChildren(screen);
      applyComponentStyles(state?.theme, screen);
    } catch (error) {
      showError(`Unable to render game UI: ${error.message}`);
    }
  }
});

bindActions(document, bridge.send);
bridge.requestState();
