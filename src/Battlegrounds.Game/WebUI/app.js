import { bindActions } from './bridge/actions.js';
import { createGameBridge } from './bridge/game-bridge.js';
import { reloadStyles } from './core/template.js';
import { initializeViewportScale } from './core/viewport-scale.js';
import { createScreen } from './screens/index.js';
import { configureAssetBridge, receiveAsset } from './theme/cosmetics.js';
import { applyComponentStyles, applyTheme } from './theme/theme.js';

const app = document.getElementById('app');
const toast = document.getElementById('toast');
let toastTimer = null;
let renderVersion = 0;

initializeViewportScale();

function showError(message) {
  toast.textContent = message;
  toast.classList.add('visible');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => toast.classList.remove('visible'), 4200);
}

function themeScreenRole(status) {
  if (status === 'mod-selection') return 'launcher';
  if (status === 'combat') return 'combat';
  return 'preparation';
}

const bridge = createGameBridge({
  onError: showError,
  onAsset: receiveAsset,
  onDevCssReload: reloadStyles,
  onState: async state => {
    const version = ++renderVersion;
    applyTheme(state?.theme, themeScreenRole(state?.status));
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

configureAssetBridge(bridge.send);
bindActions(document, bridge.send);

if (document.readyState === 'loading') {
  window.addEventListener('DOMContentLoaded', bridge.requestState, { once: true });
} else {
  bridge.requestState();
}
