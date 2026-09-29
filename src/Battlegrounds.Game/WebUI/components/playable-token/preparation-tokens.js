import { createPlayableToken } from './playable-token.js';

const boolText = value => value ? 'true' : 'false';

export async function createTavernOfferToken(entry, { blocked = false, canAcquire = false } = {}) {
  return createPlayableToken({
    kind: entry.kind,
    id: entry.id,
    name: entry.name,
    description: entry.description,
    tier: entry.tier,
    attack: entry.attack,
    health: entry.health,
    cost: entry.cost,
    frozen: entry.frozen,
    disabled: blocked,
    location: 'offer',
    attributes: {
      'data-slot': entry.slot,
      'data-drag-kind': 'tavern-offer-token',
      'data-drag-slot': entry.slot,
      'data-drag-enabled': boolText(!blocked),
      'data-drag-valid': boolText(canAcquire)
    }
  });
}

export async function createPlayerReserveToken(entry, { blocked = false, canDeployUnit = false } = {}) {
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

  return createPlayableToken({
    kind: entry.kind,
    id: entry.id,
    name: entry.name,
    description: entry.description,
    tier: entry.tier,
    attack: entry.attack,
    health: entry.health,
    cost: entry.cost,
    action: isUnit ? null : 'play-action',
    disabled: blocked,
    location: 'reserve',
    attributes
  });
}

export async function createPlayerFieldUnitToken(unit, { blocked = false } = {}) {
  return createPlayableToken({
    kind: 'unit',
    id: unit.id,
    name: unit.name,
    description: unit.description,
    tier: unit.tier,
    attack: unit.attack,
    health: unit.health,
    disabled: blocked,
    location: 'field',
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
