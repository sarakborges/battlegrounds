import { createBadge } from '../../design-system/badge/badge.js';

export async function createCardBadges(entry = {}) {
  const badges = [];
  if (entry.tier != null) badges.push(await createBadge({ text: `T${entry.tier}`, variant: 'tier' }));
  if (entry.attack != null) badges.push(await createBadge({ text: entry.attack, variant: 'attack' }));
  if (entry.health != null) badges.push(await createBadge({ text: entry.health, variant: 'health' }));
  if (entry.cost != null) badges.push(await createBadge({ text: entry.cost, variant: 'accent' }));
  if (entry.frozen) badges.push(await createBadge({ text: 'Frozen', variant: 'accent' }));
  return badges;
}
