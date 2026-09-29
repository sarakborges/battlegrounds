const defaults = {
  shopkeeper: { id: 'bob', skin: 'base' },
  board: { id: 'default', skin: 'base' },
  leaders: {},
  byIdentifier: {}
};

const assetCache = new Map();
const pendingAssets = new Map();
let sendAssetRequest = null;

function safeSegment(value, fallback) {
  const text = String(value ?? fallback ?? '').trim();
  return /^[a-zA-Z0-9._-]+$/.test(text) ? text : String(fallback ?? 'default');
}

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

export function cosmeticsForState(state) {
  return { ...defaults, ...(state?.cosmetics ?? {}) };
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

export function shopkeeperArtPath(cosmetics) {
  const cosmetic = cosmetics?.shopkeeper ?? defaults.shopkeeper;
  const id = safeSegment(cosmetic.id, defaults.shopkeeper.id);
  const skin = safeSegment(cosmetic.skin, defaults.shopkeeper.skin);
  return `assets/cosmetics/shopkeepers/${id}/${skin}.png`;
}

export function boardArtPath(cosmetics) {
  const cosmetic = cosmetics?.board ?? defaults.board;
  const id = safeSegment(cosmetic.id, defaults.board.id);
  const skin = safeSegment(cosmetic.skin, defaults.board.skin);
  return `assets/cosmetics/boards/${id}/${skin}.png`;
}

export function leaderArtPath(cosmetics, leaderId) {
  if (!leaderId) return null;
  const id = safeSegment(leaderId, 'unknown');
  const override = cosmetics?.leaders?.[id] ?? cosmetics?.byIdentifier?.[id];
  const cosmeticId = safeSegment(override?.id, id);
  const skin = safeSegment(override?.skin, 'base');
  return `assets/cosmetics/leaders/${cosmeticId}/${skin}.png`;
}

export function shopkeeperArtUrl(cosmetics) {
  return loadAsset(shopkeeperArtPath(cosmetics));
}

export function boardArtUrl(cosmetics) {
  return loadAsset(boardArtPath(cosmetics));
}

export function leaderArtUrl(cosmetics, leaderId) {
  const path = leaderArtPath(cosmetics, leaderId);
  return path ? loadAsset(path) : Promise.resolve(null);
}
