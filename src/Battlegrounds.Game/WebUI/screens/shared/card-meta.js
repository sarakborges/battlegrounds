import { createBadge } from '../../components/badge/badge.js';

export async function createCardMeta(entry = {}) {
  const badges = [];
  if (entry.tier != null) badges.push(await createBadge({ text: `T${entry.tier}` }));
  if (entry.attack != null) badges.push(await createBadge({ text: `${entry.attack} ATK` }));
  if (entry.health != null) badges.push(await createBadge({ text: `${entry.health} HP` }));
  if (entry.cost != null) badges.push(await createBadge({ text: entry.cost, variant: 'accent' }));
  if (entry.frozen) badges.push(await createBadge({ text: 'Frozen', variant: 'accent' }));
  return badges;
}
