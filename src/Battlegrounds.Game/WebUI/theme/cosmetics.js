const cache = new Map();

const defaults = {
  shopkeeper: { id: 'bob', skin: 'base' },
  board: { id: 'default', skin: 'base' },
  leaders: {},
  byIdentifier: {}
};

function safeSegment(value, fallback) {
  const text = String(value ?? fallback ?? '').trim();
  return /^[a-zA-Z0-9._-]+$/.test(text) ? text : String(fallback ?? 'default');
}

export function modAssetUrl(modId, path) {
  const mod = safeSegment(modId, 'example');
  const normalized = String(path ?? '').replace(/^\/+/, '');
  return `res://../../mods/${mod}/${normalized}`;
}

export async function loadCosmetics(modId) {
  const mod = safeSegment(modId, 'example');
  if (cache.has(mod)) return cache.get(mod);

  const promise = fetch(modAssetUrl(mod, 'presentation/cosmetics.json'))
    .then(response => response.ok ? response.json() : defaults)
    .then(value => ({ ...defaults, ...(value ?? {}) }))
    .catch(() => defaults);
  cache.set(mod, promise);
  return promise;
}

export function shopkeeperArtUrl(modId, cosmetics) {
  const cosmetic = cosmetics?.shopkeeper ?? defaults.shopkeeper;
  const id = safeSegment(cosmetic.id, defaults.shopkeeper.id);
  const skin = safeSegment(cosmetic.skin, defaults.shopkeeper.skin);
  return modAssetUrl(modId, `assets/cosmetics/shopkeepers/${id}/${skin}.png`);
}

export function boardArtUrl(modId, cosmetics) {
  const cosmetic = cosmetics?.board ?? defaults.board;
  const id = safeSegment(cosmetic.id, defaults.board.id);
  const skin = safeSegment(cosmetic.skin, defaults.board.skin);
  return modAssetUrl(modId, `assets/cosmetics/boards/${id}/${skin}.png`);
}

export function leaderArtUrl(modId, cosmetics, leaderId) {
  if (!leaderId) return null;
  const id = safeSegment(leaderId, 'unknown');
  const override = cosmetics?.leaders?.[id] ?? cosmetics?.byIdentifier?.[id];
  const cosmeticId = safeSegment(override?.id, id);
  const skin = safeSegment(override?.skin, 'base');
  return modAssetUrl(modId, `assets/cosmetics/leaders/${cosmeticId}/${skin}.png`);
}
