import { createCard } from './card.js';
import { createCardBadges } from './card-badges.js';

const boolText = value => value ? 'true' : 'false';

export async function createTavernOfferCard(entry, { blocked = false, canAcquire = false } = {}) {
  return createCard({
    kind: entry.kind,
    name: entry.name,
    badges: await createCardBadges(entry),
    disabled: blocked,
    variant: 'offer',
    attributes: {
      'data-slot': entry.slot,
      'data-drag-kind': 'tavern-offer-card',
      'data-drag-slot': entry.slot,
      'data-drag-enabled': boolText(!blocked),
      'data-drag-valid': boolText(canAcquire)
    }
  });
}

export async function createPlayerReserveCard(entry, { blocked = false, canDeployUnit = false } = {}) {
  const isUnit = entry.kind === 'unit';
  const attributes = {
    'data-slot': entry.slot,
    'data-unit-instance-id': entry.unitInstanceId ?? ''
  };

  if (isUnit) {
    attributes['data-drag-kind'] = 'player-reserve-unit';
    attributes['data-drag-slot'] = entry.slot;
    attributes['data-drag-enabled'] = boolText(!blocked);
    attributes['data-drag-valid'] = boolText(canDeployUnit);
  }

  return createCard({
    kind: entry.kind,
    name: entry.name,
    badges: await createCardBadges(entry),
    action: isUnit ? null : 'play-action',
    disabled: blocked,
    variant: 'reserve',
    attributes
  });
}

export async function createPlayerFieldUnitCard(unit, { blocked = false } = {}) {
  return createCard({
    kind: 'unit',
    name: unit.name,
    badges: await createCardBadges(unit),
    disabled: blocked,
    variant: 'board',
    themeRole: 'card.board',
    attributes: {
      'data-slot': unit.slot,
      'data-unit-instance-id': unit.unitInstanceId,
      'data-drag-kind': 'player-field-unit',
      'data-drag-index': unit.slot,
      'data-drag-enabled': boolText(!blocked),
      'data-drag-valid': boolText(!blocked)
    }
  });
}
