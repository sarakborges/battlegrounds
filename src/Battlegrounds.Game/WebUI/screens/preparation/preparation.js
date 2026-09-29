import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createButton } from '../../design-system/button/button.js';
import { createHorizontalStack } from '../../design-system/horizontal-stack/horizontal-stack.js';
import { createModalDialog } from '../../design-system/modal-dialog/modal-dialog.js';
import { createCard } from '../../components/card/card.js';
import { createHeroCockpit } from '../../components/hero-cockpit/hero-cockpit.js';
import { createOpponentRail } from '../../components/opponent-rail/opponent-rail.js';
import { createPlayerField } from '../../components/player-field/player-field.js';
import { createPlayerReserve } from '../../components/player-reserve/player-reserve.js';
import { createTavernControls } from '../../components/tavern-controls/tavern-controls.js';
import { createTavernOffer } from '../../components/tavern-offer/tavern-offer.js';
import { createTurnRail } from '../../components/turn-rail/turn-rail.js';
import { bindPreparationDrag } from '../../interactions/preparation-drag.js';
import { boardArtUrl, cosmeticsForState, leaderArtUrl, shopkeeperArtUrl } from '../../theme/cosmetics.js';
import { createCardStatBadges } from '../shared/card-stat-badges.js';

const templateUrl = new URL('./preparation.html', import.meta.url);
useStyle(new URL('./preparation.css', import.meta.url));

const boolText = value => value ? 'true' : 'false';

async function createTavernOfferCard(entry, blocked, canAcquire) {
  return createCard({
    kind: entry.kind,
    name: entry.name,
    meta: await createCardStatBadges(entry),
    disabled: blocked,
    variant: 'offer',
    attributes: {
      'data-slot': entry.slot,
      'data-drag-kind': 'offer',
      'data-drag-slot': entry.slot,
      'data-drag-enabled': boolText(!blocked),
      'data-drag-valid': boolText(canAcquire)
    }
  });
}

async function createReserveCard(entry, blocked, canDeploy) {
  const isUnit = entry.kind === 'unit';
  const attributes = {
    'data-slot': entry.slot,
    'data-unit-instance-id': entry.unitInstanceId ?? ''
  };

  if (isUnit) {
    attributes['data-drag-kind'] = 'reserve-unit';
    attributes['data-drag-slot'] = entry.slot;
    attributes['data-drag-enabled'] = boolText(!blocked);
    attributes['data-drag-valid'] = boolText(canDeploy);
  }

  return createCard({
    kind: entry.kind,
    name: entry.name,
    meta: await createCardStatBadges(entry),
    action: isUnit ? null : 'play-action',
    disabled: blocked,
    variant: 'reserve',
    attributes
  });
}

async function createFieldUnitCard(unit, blocked) {
  return createCard({
    kind: 'field',
    name: unit.name,
    meta: await createCardStatBadges(unit),
    disabled: blocked,
    variant: 'board',
    themeRole: 'card.board',
    attributes: {
      'data-slot': unit.slot,
      'data-unit-instance-id': unit.unitInstanceId,
      'data-drag-kind': 'field',
      'data-drag-index': unit.slot,
      'data-drag-enabled': boolText(!blocked),
      'data-drag-valid': boolText(!blocked)
    }
  });
}

async function createOverlay(state) {
  const labels = state.labels ?? {};
  const pending = state.pendingChoice;

  if (pending) {
    const cards = [];
    for (const option of pending.options ?? []) {
      cards.push(await createCard({
        kind: pending.kind,
        name: option.name,
        meta: await createCardStatBadges(option),
        action: 'resolve-choice',
        variant: 'choice',
        attributes: { 'data-option-index': option.index }
      }));
    }

    return createModalDialog({
      eyebrow: 'Choice',
      title: `Choose ${pending.kind}`,
      body: [await createHorizontalStack({ children: cards })],
      variant: 'choice'
    });
  }

  const interaction = state.interaction;
  if (!interaction || interaction.kind !== 'target') return null;

  const cancel = await createButton({
    label: labels.cancel ?? 'Cancel',
    action: 'cancel-interaction'
  });

  const targets = [];
  for (const candidate of interaction.candidates ?? []) {
    targets.push(await createCard({
      kind: `P${candidate.ownerId}`,
      name: candidate.name,
      meta: await createCardStatBadges(candidate),
      action: 'select-target',
      variant: 'target',
      attributes: { 'data-unit-instance-id': candidate.unitInstanceId }
    }));
  }

  return createModalDialog({
    eyebrow: 'Target',
    title: `Choose target · ${interaction.zone}`,
    actions: [cancel],
    body: [await createHorizontalStack({ children: targets })],
    variant: 'target'
  });
}

export async function createPreparationScreen(state) {
  const element = await cloneTemplate(templateUrl);
  const labels = state.labels ?? {};
  const human = state.human ?? {};
  const blocked = !state.canAct || !!state.pendingChoice || !!state.interaction;
  const limits = state.limits ?? {};
  const reserveCount = (state.reserve ?? []).length;
  const fieldCount = (state.field ?? []).length;
  const canDeploy = !blocked &&
    fieldCount < (limits.fieldCapacity ?? Number.POSITIVE_INFINITY);
  const cosmetics = cosmeticsForState(state);
  const humanPlayer = (state.players ?? []).find(player => player.human);

  const playerViews = [];
  for (const player of state.players ?? []) {
    playerViews.push({
      id: player.id,
      leader: player.leader ?? '—',
      health: player.health,
      portrait: await leaderArtUrl(cosmetics, player.leaderId),
      human: player.human,
      eliminated: player.eliminated
    });
  }

  appendChildren(element.querySelector('[data-slot="opponents"]'), [
    await createOpponentRail({
      modName: state.mod?.name ?? '',
      roundLabel: labels.round ?? 'Round',
      round: state.round ?? '',
      players: playerViews
    })
  ]);

  const shopkeeperId = cosmetics?.shopkeeper?.id ?? 'bob';
  appendChildren(element.querySelector('[data-slot="tavern-controls"]'), [
    await createTavernControls({
      labels,
      human,
      blocked,
      shopkeeperName: shopkeeperId === 'bob' ? 'Bob' : shopkeeperId,
      shopkeeperArt: await shopkeeperArtUrl(cosmetics)
    })
  ]);

  const tavernOfferCards = [];
  for (const entry of state.offer ?? []) {
    const canAcquire = !blocked &&
      reserveCount < (limits.reserveCapacity ?? Number.POSITIVE_INFINITY) &&
      (human.resource ?? 0) >= (entry.cost ?? Number.POSITIVE_INFINITY);
    tavernOfferCards.push(await createTavernOfferCard(entry, blocked, canAcquire));
  }
  appendChildren(element.querySelector('[data-slot="offer-zone"]'), [
    await createTavernOffer({ cards: tavernOfferCards })
  ]);

  const fieldUnitCards = [];
  for (const unit of state.field ?? []) {
    fieldUnitCards.push(await createFieldUnitCard(unit, blocked));
  }
  appendChildren(element.querySelector('[data-slot="field-zone"]'), [
    await createPlayerField({ cards: fieldUnitCards })
  ]);

  appendChildren(element.querySelector('[data-slot="hero-cockpit"]'), [
    await createHeroCockpit({
      labels,
      human,
      heroName: humanPlayer?.leader ?? '—',
      heroArt: await leaderArtUrl(cosmetics, humanPlayer?.leaderId),
      blocked
    })
  ]);

  const reserveCards = [];
  for (const entry of state.reserve ?? []) {
    reserveCards.push(await createReserveCard(entry, blocked, canDeploy));
  }
  appendChildren(element.querySelector('[data-slot="reserve-zone"]'), [
    await createPlayerReserve({ cards: reserveCards })
  ]);

  appendChildren(element.querySelector('[data-slot="turn-rail"]'), [
    await createTurnRail({
      label: labels.endPreparation ?? 'Ready',
      blocked,
      canAct: state.canAct,
      currentPreparationPlayerId: state.currentPreparationPlayerId
    })
  ]);

  const boardArt = await boardArtUrl(cosmetics);
  if (boardArt) {
    element.querySelector('.table-stage')?.style.setProperty(
      '--board-art',
      `url("${boardArt}")`
    );
  }

  const overlay = await createOverlay(state);
  if (overlay) {
    appendChildren(element.querySelector('[data-slot="overlay"]'), [overlay]);
  }

  bindPreparationDrag(element);
  return element;
}
