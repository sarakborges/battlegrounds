const assetCache = new Map();
const pendingAssets = new Map();
let sendAssetRequest = null;

function normalizeAssetPath(path) {
  const normalized = String(path ?? '').replace(/\\/g, '/').replace(/^\/+/, '');
  if (!normalized.startsWith('assets/') || normalized.split('/').includes('..')) return null;
  return normalized;
}

export function configureAssetBridge(send) {
  sendAssetRequest = send;
}

export function receiveAsset(payload = {}) {
  const path = normalizeAssetPath(payload.path);
  if (!path) return;

  const pending = pendingAssets.get(path);
  if (!pending) return;

  pendingAssets.delete(path);
  const value = typeof payload.dataUrl === 'string' && payload.dataUrl.length > 0 ? payload.dataUrl : null;
  assetCache.set(path, value);
  pending.resolve(value);
}

export function loadAsset(path) {
  const normalized = normalizeAssetPath(path);
  if (!normalized) return Promise.resolve(null);
  if (assetCache.has(normalized)) return Promise.resolve(assetCache.get(normalized));
  if (pendingAssets.has(normalized)) return pendingAssets.get(normalized).promise;
  if (typeof sendAssetRequest !== 'function') return Promise.resolve(null);

  let resolve;
  const promise = new Promise(done => { resolve = done; });
  pendingAssets.set(normalized, { promise, resolve });
  sendAssetRequest('request-asset', { path: normalized });
  return promise;
}
