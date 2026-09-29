import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createButton } from '../../design-system/button/button.js';
import { createHorizontalStack } from '../../design-system/horizontal-stack/horizontal-stack.js';
import { createModalDialog } from '../../design-system/modal-dialog/modal-dialog.js';
import { createHeroCockpit } from '../../components/hero-cockpit/hero-cockpit.js';
import { createOpponentRail } from '../../components/opponent-rail/opponent-rail.js';
import { createPlayableToken } from '../../components/playable-token/playable-token.js';
import {
  createPlayerFieldUnitToken,
  createPlayerReserveToken,
  createTavernOfferToken
} from '../../components/playable-token/preparation-tokens.js';
import { createPlayerField } from '../../components/player-field/player-field.js';
import { createPlayerReserve } from '../../components/player-reserve/player-reserve.js';
import { createTavernControls } from '../../components/tavern-controls/tavern-controls.js';
import { createTavernOffer } from '../../components/tavern-offer/tavern-offer.js';
import { createTurnRail } from '../../components/turn-rail/turn-rail.js';
import { bindPreparationDrag } from '../../interactions/preparation-drag.js';
import {
  boardArtUrl,
  cosmeticsForState,
  entityArtUrl,
  leaderArtUrl,
  shopkeeperArtUrl
} from '../../theme/cosmetics.js';

const templateUrl = new URL('./preparation.html', import.meta.url);
useStyle(new URL('./preparation.css', import.meta.url));

async function createOverlay(state, cosmetics) {
  const labels = state.labels ?? {};
  const pending = state.pendingChoice;

  if (pending) {
    const tokens = [];
    for (const option of pending.options ?? []) {
      tokens.push(await createPlayableToken({
        kind: pending.kind,
        id: option.id,
        name: option.name,
        description: option.description,
        art: await entityArtUrl(cosmetics, pending.kind, option.id),
        tier: option.tier,
        attack: option.attack,
        health: option.health,
        cost: option.cost,
        action: 'resolve-choice',
        location: 'choice',
        attributes: { 'data-option-index': option.index }
      }));
    }

    return createModalDialog({
      eyebrow: 'Choice',
      title: `Choose ${pending.kind}`,
      body: [await createHorizontalStack({ children: tokens })],
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
    targets.push(await createPlayableToken({
      kind: 'unit',
      id: candidate.id,
      name: candidate.name,
      description: candidate.description,
      art: await entityArtUrl(cosmetics, 'unit', candidate.id),
      tier: candidate.tier,
      attack: candidate.attack,
      health: candidate.health,
      action: 'select-target',
      location: 'target',
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
  const canDeployReserveUnit = !blocked &&
    fieldCount < (limits.fieldCapacity ?? Number.POSITIVE_INFINITY);
  const cosmetics = cosmeticsForState(state);
  const humanPlayer = (state.players ?? []).find(player => player.human);

  const playerViews = [];
  for (const player of state.players ?? []) {
    playerViews.push({
      id: player.id,
      leaderId: player.leaderId,
      leader: player.leader ?? '—',
      leaderDescription: player.leaderDescription ?? '',
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

  const tavernOfferTokens = [];
  for (const entry of state.offer ?? []) {
    const canAcquire = !blocked &&
      reserveCount < (limits.reserveCapacity ?? Number.POSITIVE_INFINITY) &&
      (human.resource ?? 0) >= (entry.cost ?? Number.POSITIVE_INFINITY);
    tavernOfferTokens.push(await createTavernOfferToken(entry, { cosmetics, blocked, canAcquire }));
  }
  appendChildren(element.querySelector('[data-slot="tavern-offer"]'), [
    await createTavernOffer({ tokens: tavernOfferTokens })
  ]);

  const playerFieldTokens = [];
  for (const unit of state.field ?? []) {
    playerFieldTokens.push(await createPlayerFieldUnitToken(unit, { cosmetics, blocked }));
  }
  appendChildren(element.querySelector('[data-slot="player-field"]'), [
    await createPlayerField({ tokens: playerFieldTokens })
  ]);

  appendChildren(element.querySelector('[data-slot="hero-cockpit"]'), [
    await createHeroCockpit({
      labels,
      human,
      power: human.powerInfo,
      heroId: humanPlayer?.leaderId ?? '',
      heroName: humanPlayer?.leader ?? '—',
      heroDescription: humanPlayer?.leaderDescription ?? '',
      heroArt: await leaderArtUrl(cosmetics, humanPlayer?.leaderId),
      blocked
    })
  ]);

  const playerReserveTokens = [];
  for (const entry of state.reserve ?? []) {
    playerReserveTokens.push(await createPlayerReserveToken(entry, {
      cosmetics,
      blocked,
      canDeployUnit: canDeployReserveUnit
    }));
  }
  appendChildren(element.querySelector('[data-slot="player-reserve"]'), [
    await createPlayerReserve({ tokens: playerReserveTokens })
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
    element.querySelector('.preparation-board')?.style.setProperty(
      '--board-art',
      `url("${boardArt}")`
    );
  }

  const overlay = await createOverlay(state, cosmetics);
  if (overlay) {
    appendChildren(element.querySelector('[data-slot="overlay"]'), [overlay]);
  }

  bindPreparationDrag(element);
  return element;
}
