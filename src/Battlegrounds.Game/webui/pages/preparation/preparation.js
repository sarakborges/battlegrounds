import { appendChildren } from '../../core/template.js';
import { createButton } from '../../atoms/button/button.js';
import { createHorizontalStack } from '../../atoms/horizontal-stack/horizontal-stack.js';
import { createModalDialog } from '../../atoms/modal-dialog/modal-dialog.js';
import { createEndPreparationControl } from '../../molecules/end-preparation-control/end-preparation-control.js';
import { createLeaderHud } from '../../organisms/leader-hud/leader-hud.js';
import { createOfferRow } from '../../organisms/offer-row/offer-row.js';
import { createOpponentRail } from '../../organisms/opponent-rail/opponent-rail.js';
import { createPlayerField } from '../../organisms/player-field/player-field.js';
import { createPlayerReserve } from '../../organisms/player-reserve/player-reserve.js';
import { createPreparationControls } from '../../organisms/preparation-controls/preparation-controls.js';
import { createResourceCounter } from '../../molecules/resource-counter/resource-counter.js';
import { createPreparationTemplate } from '../../templates/preparation/preparation.js';
import { bindPreparationDrag } from '../../interactions/preparation-drag.js';
import {
  boardArtUrl,
  cosmeticsForState,
  leaderArtUrl,
  preparationHost,
  preparationHostArtUrl
} from '../../theme/cosmetics.js';
import {
  createChoiceToken,
  createOfferToken,
  createPlayerFieldUnitToken,
  createPlayerReserveToken,
  createTargetUnitToken
} from './preparation-tokens.js';

async function createOverlay(state, cosmetics) {
  const labels = state.labels ?? {};
  const pending = state.pendingChoice;

  if (pending) {
    const tokens = [];
    for (const option of pending.options ?? []) {
      tokens.push(await createChoiceToken(pending.kind, option, cosmetics));
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
    targets.push(await createTargetUnitToken(candidate, cosmetics));
  }

  return createModalDialog({
    eyebrow: 'Target',
    title: `Choose target · ${interaction.zone}`,
    actions: [cancel],
    body: [await createHorizontalStack({ children: targets })],
    variant: 'target'
  });
}

export async function createPreparationPage(state) {
  const element = await createPreparationTemplate();
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

  const host = preparationHost(cosmetics);
  appendChildren(element.querySelector('[data-slot="controls"]'), [
    await createPreparationControls({
      labels,
      participant: human,
      blocked,
      hostName: host.name || host.id || '',
      hostArt: await preparationHostArtUrl(cosmetics)
    })
  ]);

  const offerTokens = [];
  for (const entry of state.offer ?? []) {
    const canAcquire = !blocked &&
      reserveCount < (limits.reserveCapacity ?? Number.POSITIVE_INFINITY) &&
      (human.resource ?? 0) >= (entry.cost ?? Number.POSITIVE_INFINITY);
    offerTokens.push(await createOfferToken(entry, { cosmetics, blocked, canAcquire }));
  }
  appendChildren(element.querySelector('[data-slot="offer"]'), [
    await createOfferRow({ tokens: offerTokens })
  ]);

  const fieldTokens = [];
  for (const unit of state.field ?? []) {
    fieldTokens.push(await createPlayerFieldUnitToken(unit, { cosmetics, blocked }));
  }
  appendChildren(element.querySelector('[data-slot="field"]'), [
    await createPlayerField({ tokens: fieldTokens })
  ]);

  appendChildren(element.querySelector('[data-slot="leader"]'), [
    await createLeaderHud({
      labels,
      participant: human,
      power: human.powerInfo,
      leaderId: humanPlayer?.leaderId ?? '',
      leaderName: humanPlayer?.leader ?? '—',
      leaderDescription: humanPlayer?.leaderDescription ?? '',
      leaderArt: await leaderArtUrl(cosmetics, humanPlayer?.leaderId),
      blocked
    })
  ]);

  appendChildren(element.querySelector('[data-slot="resource"]'), [
    await createResourceCounter({
      value: human.resource ?? 0,
      maximum: human.resourceMaximum ?? human.resource ?? 0,
      label: labels.resource ?? 'Resource'
    })
  ]);

  const reserveTokens = [];
  for (const entry of state.reserve ?? []) {
    reserveTokens.push(await createPlayerReserveToken(entry, {
      cosmetics,
      blocked,
      canDeployUnit: canDeployReserveUnit
    }));
  }
  appendChildren(element.querySelector('[data-slot="reserve"]'), [
    await createPlayerReserve({ tokens: reserveTokens })
  ]);

  appendChildren(element.querySelector('[data-slot="end-preparation"]'), [
    await createEndPreparationControl({
      label: labels.endPreparation ?? 'Ready',
      blocked,
      canAct: state.canAct,
      currentParticipantId: state.currentPreparationPlayerId
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
