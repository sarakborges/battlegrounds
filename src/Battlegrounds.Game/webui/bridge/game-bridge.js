export function createGameBridge({ onState, onError, onAsset, onDevCssReload }) {
  let pollTimer = null;

  function send(type, payload = {}) {
    if (typeof window.sendIpcMessage !== 'function') {
      onError?.('Godot CEF IPC is not available.');
      return;
    }
    window.sendIpcMessage(JSON.stringify({ type, ...payload }));
  }

  function receive(raw) {
    try {
      const message = typeof raw === 'string' ? JSON.parse(raw) : raw;
      if (!message || typeof message.type !== 'string') return;
      if (message.type === 'state') onState?.(message.payload);
      else if (message.type === 'asset') onAsset?.(message.payload);
      else if (message.type === 'dev-css-reload') onDevCssReload?.();
      else if (message.type === 'error') onError?.(message.payload?.message ?? 'Unknown game error');
    } catch (error) {
      onError?.(`Invalid game message: ${error.message}`);
    }
  }

  function setPolling(enabled) {
    if (enabled && pollTimer === null) pollTimer = window.setInterval(() => send('request-state'), 60);
    if (!enabled && pollTimer !== null) {
      window.clearInterval(pollTimer);
      pollTimer = null;
    }
  }

  if (window.ipcMessage?.addListener) window.ipcMessage.addListener(receive);
  else window.onIpcMessage = receive;

  return { send, setPolling, requestState: () => send('request-state') };
}
