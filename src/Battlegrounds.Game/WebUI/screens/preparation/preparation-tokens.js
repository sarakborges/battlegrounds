import { createActionToken } from '../../components/action-token/action-token.js';
import { createUnitToken } from '../../components/unit-token/unit-token.js';
import { entityArtUrl } from '../../theme/cosmetics.js';

const boolText = value => value ? 'true' : 'false';

async function createOfferUnitToken(entry, { cosmetics, blocked, canAcquire }) {
  return createUnitToken({
    id: entry.id,
    name: entry.name,
    description: entry.description,
    art: await entityArtUrl(cosmetics, 'unit', entry.id),
    tier: entry.tier,
    attack: entry.attack,
    health: entry.health,
    frozen: entry.frozen,
    disabled: blocked,
    attributes: {
      'data-slot': entry.slot,
      'data-drag-kind': 'tavern-offer-token',
      'data-drag-slot': entry.slot,
      'data-drag-enabled': boolText(!blocked),
      'data-drag-valid': boolText(canAcquire)
    }
  });
}

async function createOfferActionToken(entry, { cosmetics, blocked, canAcquire }) {
  return createActionToken({
    id: entry.id,
    name: entry.name,
    description: entry.description,
    art: await entityArtUrl(cosmetics, 'action', entry.id),
    tier: entry.tier,
    cost: entry.cost,
    disabled: blocked,
    attributes: {
      'data-slot': entry.slot,
      'data-drag-kind': 'tavern-offer-token',
      'data-drag-slot': entry.slot,
      'data-drag-enabled': boolText(!blocked),
      'data-drag-valid': boolText(canAcquire)
    }
  });
}

export async function createTavernOfferToken(entry, options = {}) {
  return entry.kind === 'action'
    ? createOfferActionToken(entry, options)
    : createOfferUnitToken(entry, options);
}

export async function createPlayerReserveToken(entry, { cosmetics = null, blocked = false, canDeployUnit = false } = {}) {
  const attributes = {
    'data-slot': entry.slot,
    'data-unit-instance-id': entry.unitInstanceId ?? '',
    'data-inspect-placement': 'top'
  };

  if (entry.kind === 'unit') {
    attributes['data-drag-kind'] = 'player-reserve-unit';
    attributes['data-drag-slot'] = entry.slot;
    attributes['data-drag-enabled'] = boolText(!blocked);
    attributes['data-drag-valid'] = boolText(canDeployUnit);
    return createUnitToken({
      id: entry.id,
      name: entry.name,
      description: entry.description,
      art: await entityArtUrl(cosmetics, 'unit', entry.id),
      tier: entry.tier,
      attack: entry.attack,
      health: entry.health,
      disabled: blocked,
      attributes
    });
  }

  return createActionToken({
    id: entry.id,
    name: entry.name,
    description: entry.description,
    art: await entityArtUrl(cosmetics, 'action', entry.id),
    tier: entry.tier,
    cost: entry.cost,
    action: 'play-action',
    disabled: blocked,
    attributes
  });
}

export async function createPlayerFieldUnitToken(unit, { cosmetics = null, blocked = false } = {}) {
  return createUnitToken({
    id: unit.id,
    name: unit.name,
    description: unit.description,
    art: await entityArtUrl(cosmetics, 'unit', unit.id),
    tier: unit.tier,
    attack: unit.attack,
    health: unit.health,
    disabled: blocked,
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

export async function createChoiceToken(kind, option, cosmetics) {
  if (kind === 'action') {
    return createActionToken({
      id: option.id,
      name: option.name,
      description: option.description,
      art: await entityArtUrl(cosmetics, 'action', option.id),
      tier: option.tier,
      cost: option.cost,
      action: 'resolve-choice',
      attributes: { 'data-option-index': option.index }
    });
  }

  return createUnitToken({
    id: option.id,
    name: option.name,
    description: option.description,
    art: await entityArtUrl(cosmetics, 'unit', option.id),
    tier: option.tier,
    attack: option.attack,
    health: option.health,
    action: 'resolve-choice',
    attributes: { 'data-option-index': option.index }
  });
}

export async function createTargetUnitToken(candidate, cosmetics) {
  return createUnitToken({
    id: candidate.id,
    name: candidate.name,
    description: candidate.description,
    art: await entityArtUrl(cosmetics, 'unit', candidate.id),
    tier: candidate.tier,
    attack: candidate.attack,
    health: candidate.health,
    action: 'select-target',
    attributes: { 'data-unit-instance-id': candidate.unitInstanceId }
  });
}
