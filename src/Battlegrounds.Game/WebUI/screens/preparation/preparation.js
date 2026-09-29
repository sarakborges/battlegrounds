import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createButton } from '../../components/button/button.js';
import { createCard } from '../../components/card/card.js';
import { createDialog } from '../../components/dialog/dialog.js';
import { createDropSlot } from '../../components/drop-slot/drop-slot.js';
import { createEmpty } from '../../components/empty/empty.js';
import { createPlayerChip } from '../../components/player-chip/player-chip.js';
import { createRow } from '../../components/row/row.js';
import { createStat } from '../../components/stat/stat.js';
import { createZone } from '../../components/zone/zone.js';
import { createBadge } from '../../components/badge/badge.js';
import { bindPreparationDrag } from '../../interactions/preparation-drag.js';
import { createCardMeta } from '../shared/card-meta.js';

const templateUrl = new URL('./preparation.html', import.meta.url);
useStyle(new URL('../../components/panel/panel.css', import.meta.url));
useStyle(new URL('./preparation.css', import.meta.url));

async function createOfferCard(entry, blocked, canAcquire) {
  return createCard({
    kind: entry.kind,
    name: entry.name,
    meta: await createCardMeta(entry),
    action: 'acquire',
    disabled: blocked,
    variant: 'offer',
    attributes: {
      'data-slot': entry.slot,
      'data-drag-kind': 'offer',
      'data-drag-slot': entry.slot,
      'data-drag-enabled': !blocked,
      'data-drag-valid': canAcquire
    }
  });
}

async function createReserveCard(state, entry, blocked, canDeploy) {
  const combining = state.interaction?.kind === 'combine-components' && entry.kind === 'unit';
  const action = combining ? 'toggle-combine-unit' : entry.kind === 'unit' ? 'deploy' : 'play-action';
  const attributes = { 'data-slot': entry.slot, 'data-unit-instance-id': entry.unitInstanceId ?? '' };
  if (entry.kind === 'unit' && !combining) {
    attributes['data-drag-kind'] = 'reserve-unit';
    attributes['data-drag-slot'] = entry.slot;
    attributes['data-drag-enabled'] = !blocked;
    attributes['data-drag-valid'] = canDeploy;
  }

  return createCard({
    kind: entry.kind,
    name: entry.name,
    meta: await createCardMeta(entry),
    action,
    disabled: combining ? !state.canAct : blocked,
    selected: entry.selectedForCombine,
    variant: 'reserve',
    attributes
  });
}

async function createFieldCard(state, unit, blocked) {
  const combining = state.interaction?.kind === 'combine-components';
  const attributes = { 'data-slot': unit.slot, 'data-unit-instance-id': unit.unitInstanceId };
  if (!combining) {
    attributes['data-drag-kind'] = 'field';
    attributes['data-drag-index'] = unit.slot;
    attributes['data-drag-enabled'] = !blocked;
    attributes['data-drag-valid'] = !blocked;
  }

  return createCard({
    kind: 'field',
    name: unit.name,
    meta: await createCardMeta(unit),
    action: combining ? 'toggle-combine-unit' : 'release',
    disabled: combining ? !state.canAct : blocked,
    selected: unit.selectedForCombine,
    variant: 'board',
    themeRole: 'card.board',
    attributes
  });
}

async function createOverlay(state) {
  const labels = state.labels ?? {};
  const pending = state.pendingChoice;

  if (pending) {
    const cards = [];
    for (const option of pending.options ?? []) {
      cards.push(await createCard({ kind: pending.kind, name: option.name, meta: await createCardMeta(option), action: 'resolve-choice', variant: 'choice', attributes: { 'data-option-index': option.index } }));
    }
    return createDialog({ eyebrow: 'Choice', title: `Choose ${pending.kind}`, body: [await createRow({ children: cards })], variant: 'choice' });
  }

  const interaction = state.interaction;
  if (!interaction) return null;
  const cancel = await createButton({ label: labels.cancel ?? 'Cancel', action: 'cancel-interaction' });

  if (interaction.kind === 'combine-recipe') {
    const recipes = [];
    for (const recipe of interaction.recipes ?? []) {
      recipes.push(await createCard({
        kind: `${recipe.requiredCopies} × ${recipe.source}`,
        name: recipe.name,
        meta: [await createBadge({ text: `→ ${recipe.result}`, variant: 'accent' })],
        action: 'select-combine-recipe',
        variant: 'choice',
        attributes: { 'data-combine-id': recipe.id }
      }));
    }
    if (!recipes.length) recipes.push(await createEmpty({ label: 'No available recipes' }));
    return createDialog({ eyebrow: labels.combine ?? 'Combine', title: 'Choose recipe', actions: [cancel], body: [await createRow({ children: recipes })], variant: 'combine-recipe' });
  }

  if (interaction.kind === 'combine-components') {
    const confirm = await createButton({ label: labels.confirm ?? 'Confirm', action: 'confirm-interaction', variant: 'primary', disabled: interaction.selected !== interaction.requiredCopies });
    return createDialog({
      eyebrow: interaction.name,
      title: `Select ${interaction.requiredCopies} components · ${interaction.selected} selected`,
      body: [await createRow({ children: [cancel, confirm], variant: 'actions' })],
      variant: 'combine-components'
    });
  }

  if (interaction.kind === 'target') {
    const targets = [];
    for (const candidate of interaction.candidates ?? []) {
      targets.push(await createCard({ kind: `P${candidate.ownerId}`, name: candidate.name, meta: await createCardMeta(candidate), action: 'select-target', variant: 'target', attributes: { 'data-unit-instance-id': candidate.unitInstanceId } }));
    }
    if (!targets.length) targets.push(await createEmpty({ label: 'No valid targets' }));
    return createDialog({ eyebrow: 'Target', title: `Choose target · ${interaction.zone}`, actions: [cancel], body: [await createRow({ children: targets })], variant: 'target' });
  }

  return null;
}

export async function createPreparationScreen(state) {
  const element = await cloneTemplate(templateUrl);
  const labels = state.labels ?? {};
  const human = state.human ?? {};
  const blocked = !state.canAct || !!state.pendingChoice || !!state.interaction;
  const limits = state.limits ?? {};
  const reserveCount = (state.reserve ?? []).length;
  const fieldCount = (state.field ?? []).length;
  const canDeploy = !blocked && fieldCount < (limits.fieldCapacity ?? Number.POSITIVE_INFINITY);

  element.querySelector('[data-field="mod-name"]').textContent = state.mod?.name ?? '';
  element.querySelector('[data-field="phase"]').textContent = state.phase ?? '';
  element.querySelector('[data-field="round-label"]').textContent = labels.round ?? 'Round';
  element.querySelector('[data-field="round"]').textContent = state.round ?? '';
  element.querySelector('[data-field="field-label"]').textContent = labels.field ?? 'Field';
  element.querySelector('[data-field="leader-label"]').textContent = labels.leader ?? 'Leader';
  element.querySelector('[data-field="hero-name"]').textContent = (state.players ?? []).find(player => player.human)?.leader ?? '—';

  const stats = [
    await createStat({ label: labels.health ?? 'Health', value: human.health }),
    await createStat({ label: labels.armor ?? 'Armor', value: human.armor }),
    await createStat({ label: labels.resource ?? 'Resource', value: human.resource }),
    await createStat({ label: labels.tier ?? 'Tier', value: human.tier })
  ];
  appendChildren(element.querySelector('[data-slot="stats"]'), stats);

  const players = [];
  for (const player of state.players ?? []) {
    players.push(await createPlayerChip({ id: player.id, leader: player.leader ?? '—', health: player.health, human: player.human, eliminated: player.eliminated }));
  }
  appendChildren(element.querySelector('[data-slot="players"]'), players);

  const field = [await createDropSlot({ insertionIndex: 0 })];
  for (const unit of state.field ?? []) {
    field.push(await createFieldCard(state, unit, blocked));
    field.push(await createDropSlot({ insertionIndex: unit.slot + 1 }));
  }
  if (!(state.field ?? []).length) field.push(await createEmpty({ label: labels.field ?? 'Field' }));

  const offer = [];
  for (const entry of state.offer ?? []) {
    const canAcquire = !blocked &&
      reserveCount < (limits.reserveCapacity ?? Number.POSITIVE_INFINITY) &&
      (human.resource ?? 0) >= (entry.cost ?? Number.POSITIVE_INFINITY);
    offer.push(await createOfferCard(entry, blocked, canAcquire));
  }
  if (!offer.length) offer.push(await createEmpty({ label: labels.offer ?? 'Offer' }));

  const reserve = [];
  for (const entry of state.reserve ?? []) reserve.push(await createReserveCard(state, entry, blocked, canDeploy));
  if (!reserve.length) reserve.push(await createEmpty({ label: labels.reserve ?? 'Reserve' }));

  const fieldZone = await createZone({ title: labels.field ?? 'Field', count: fieldCount, children: field, variant: 'field' });
  fieldZone.dataset.dropKind = 'field-surface';
  appendChildren(element.querySelector('[data-slot="field"]'), [fieldZone]);
  appendChildren(element.querySelector('[data-slot="offer"]'), [await createZone({ title: labels.offer ?? 'Offer', count: (state.offer ?? []).length, children: offer, variant: 'offer' })]);
  appendChildren(element.querySelector('[data-slot="reserve"]'), [await createZone({ title: labels.reserve ?? 'Reserve', count: reserveCount, children: reserve, variant: 'reserve' })]);

  const actions = [
    await createButton({ label: labels.refresh ?? 'Refresh', action: 'refresh', disabled: blocked, themeRole: 'button.tavernRefresh' }),
    await createButton({ label: `${labels.upgrade ?? 'Upgrade'}${human.upgradeCost == null ? '' : ` · ${human.upgradeCost}`}`, action: 'upgrade', disabled: blocked || human.upgradeCost == null, themeRole: 'button.tavernUpgrade' }),
    await createButton({ label: human.offerFrozen ? (labels.unfreeze ?? 'Unfreeze') : (labels.freeze ?? 'Freeze'), action: 'toggle-freeze', disabled: blocked, themeRole: 'button.tavernFreeze' }),
    await createButton({ label: labels.usePower ?? 'Use power', action: 'use-power', disabled: blocked || !human.power, themeRole: 'button.tavernAction' }),
    await createButton({ label: labels.combine ?? 'Combine', action: 'begin-combine', disabled: blocked, themeRole: 'button.tavernAction' }),
    await createButton({ label: labels.endPreparation ?? 'End preparation', action: 'end-preparation', disabled: blocked, variant: 'primary', themeRole: 'button.primary' })
  ];
  appendChildren(element.querySelector('[data-slot="actions"]'), actions);

  const waiting = element.querySelector('[data-field="waiting"]');
  waiting.hidden = state.canAct;
  if (!state.canAct) waiting.textContent = `Waiting for preparation initiative${state.currentPreparationPlayerId != null ? ` · P${state.currentPreparationPlayerId}` : ''}`;

  const overlay = await createOverlay(state);
  if (overlay) appendChildren(element.querySelector('[data-slot="overlay"]'), [overlay]);

  bindPreparationDrag(element);
  return element;
}
