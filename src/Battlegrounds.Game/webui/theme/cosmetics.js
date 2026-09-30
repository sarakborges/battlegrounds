import { loadAsset } from './assets.js';

const defaults = {
  preparationHost: { id: 'default', skin: 'base', name: '', asset: null },
  board: { id: 'default', skin: 'base' },
  leaders: {},
  byIdentifier: {},
  presentationAssets: { leaders: {}, units: {}, actions: {} }
};

function safeSegment(value, fallback) {
  const text = String(value ?? fallback ?? '').trim();
  return /^[a-zA-Z0-9._-]+$/.test(text) ? text : String(fallback ?? 'default');
}

function safeAssetPath(value) {
  const path = String(value ?? '').replaceAll('\\', '/').replace(/^\/+/, '');
  if (!path.startsWith('assets/') || path.split('/').some(segment => segment === '..' || segment === '.')) return null;
  return path;
}

function assetCategory(kind) {
  if (kind === 'leader') return 'leaders';
  if (kind === 'unit') return 'units';
  if (kind === 'action') return 'actions';
  return null;
}

export function cosmeticsForState(state) {
  return { ...defaults, ...(state?.cosmetics ?? {}) };
}

export function entityArtPath(cosmetics, kind, entityId) {
  if (!entityId) return null;
  const category = assetCategory(kind);
  if (!category) return null;
  return cosmetics?.presentationAssets?.[category]?.[entityId] ?? null;
}

export function entityArtUrl(cosmetics, kind, entityId) {
  const path = entityArtPath(cosmetics, kind, entityId);
  return path ? loadAsset(path) : Promise.resolve(null);
}

export function preparationHost(cosmetics) {
  return { ...defaults.preparationHost, ...(cosmetics?.preparationHost ?? {}) };
}

export function preparationHostArtPath(cosmetics) {
  const host = preparationHost(cosmetics);
  const authoredPath = safeAssetPath(host.asset);
  if (authoredPath) return authoredPath;
  const id = safeSegment(host.id, defaults.preparationHost.id);
  const skin = safeSegment(host.skin, defaults.preparationHost.skin);
  return `assets/cosmetics/preparation-hosts/${id}/${skin}.png`;
}

export function boardArtPath(cosmetics) {
  const cosmetic = cosmetics?.board ?? defaults.board;
  const id = safeSegment(cosmetic.id, defaults.board.id);
  const skin = safeSegment(cosmetic.skin, defaults.board.skin);
  return `assets/cosmetics/boards/${id}/${skin}.png`;
}

export function leaderArtPath(cosmetics, leaderId) {
  if (!leaderId) return null;
  const authoredPath = entityArtPath(cosmetics, 'leader', leaderId);
  if (authoredPath) return authoredPath;
  const id = safeSegment(leaderId, 'unknown');
  const override = cosmetics?.leaders?.[id] ?? cosmetics?.byIdentifier?.[id];
  const cosmeticId = safeSegment(override?.id, id);
  const skin = safeSegment(override?.skin, 'base');
  return `assets/cosmetics/leaders/${cosmeticId}/${skin}.png`;
}

export function preparationHostArtUrl(cosmetics) {
  return loadAsset(preparationHostArtPath(cosmetics));
}

export function boardArtUrl(cosmetics) {
  return loadAsset(boardArtPath(cosmetics));
}

export function leaderArtUrl(cosmetics, leaderId) {
  const path = leaderArtPath(cosmetics, leaderId);
  return path ? loadAsset(path) : Promise.resolve(null);
}
