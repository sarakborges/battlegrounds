const assetCache = new Map();
const pendingAssets = new Map();
const chunkedAssets = new Map();
const requestQueue = [];
let activeRequest = null;
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

  if (payload.chunked === true && Number.isInteger(payload.chunkCount)) {
    chunkedAssets.set(path, {
      mimeType: typeof payload.mimeType === 'string' ? payload.mimeType : 'application/octet-stream',
      chunks: new Array(payload.chunkCount),
      received: 0
    });
    return;
  }

  if (Number.isInteger(payload.chunkIndex) && typeof payload.chunkData === 'string') {
    const state = chunkedAssets.get(path);
    if (!state || payload.chunkIndex < 0 || payload.chunkIndex >= state.chunks.length) return;
    if (state.chunks[payload.chunkIndex] === undefined) {
      state.chunks[payload.chunkIndex] = payload.chunkData;
      state.received += 1;
    }
    if (state.received !== state.chunks.length) return;

    chunkedAssets.delete(path);
    pendingAssets.delete(path);
    const value = `data:${state.mimeType};base64,${state.chunks.join('')}`;
    assetCache.set(path, value);
    pending.resolve(value);
    finishAssetRequest(path);
    return;
  }

  pendingAssets.delete(path);
  const value = typeof payload.dataUrl === 'string' && payload.dataUrl.length > 0 ? payload.dataUrl : null;
  assetCache.set(path, value);
  pending.resolve(value);
  finishAssetRequest(path);
}

function finishAssetRequest(path) {
  if (activeRequest !== path) return;
  activeRequest = null;
  pumpAssetQueue();
}

function pumpAssetQueue() {
  if (activeRequest || requestQueue.length === 0 || typeof sendAssetRequest !== 'function') return;
  const path = requestQueue.shift();
  activeRequest = path;
  sendAssetRequest('request-asset', { path });
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
  requestQueue.push(normalized);
  pumpAssetQueue();
  return promise;
}
