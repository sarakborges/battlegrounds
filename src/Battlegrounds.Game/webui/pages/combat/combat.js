import { appendChildren } from '../../core/template.js';
import { createCombatPlayerHero } from '../../organisms/combat-player-hero/combat-player-hero.js';
import { createCombatUnitToken } from '../../organisms/combat-unit-token/combat-unit-token.js';
import { createOpponentRail } from '../../organisms/opponent-rail/opponent-rail.js';
import { createCombatTemplate } from '../../templates/combat/combat.js';
import {
  boardArtUrl,
  cosmeticsForState,
  entityArtUrl,
  leaderArtUrl
} from '../../theme/cosmetics.js';

let lastStepKey = '';

function orientCombatSides(combat) {
  if (combat?.left?.human) return { player: combat.left, opponent: combat.right };
  if (combat?.right?.human) return { player: combat.right, opponent: combat.left };
  return { player: combat?.left ?? null, opponent: combat?.right ?? null };
}

async function createCombatBoardTokens(side, cosmetics, attackDirection) {
  const tokens = [];
  for (const unit of side?.units ?? []) {
    tokens.push(await createCombatUnitToken({
      unit,
      art: await entityArtUrl(cosmetics, 'unit', unit.unitId),
      attackDirection
    }));
  }
  return tokens;
}

async function createCombatPlayerViews(state, cosmetics) {
  const views = [];
  for (const player of state.players ?? []) {
    views.push({
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
  return views;
}

export async function createCombatPage(state) {
  const element = await createCombatTemplate();
  const combat = state.combat;
  if (!combat) return element;

  const scene = element.querySelector('.combat-board-scene');
  const stepKey = `${combat.round}:${combat.eventSequence}:${combat.settlementVisible}`;
  scene.classList.toggle('combat-step-changed', stepKey !== lastStepKey);
  lastStepKey = stepKey;

  element.querySelector('[data-field="round-label"]').textContent = state.labels?.round ?? 'Round';
  element.querySelector('[data-field="round"]').textContent = combat.round;
  element.querySelector('[data-field="progress"]').textContent = combat.settlementVisible
    ? ''
    : `${combat.eventSequence > 0 ? combat.eventSequence : 0} / ${combat.eventCount}`;

  const eventContainer = element.querySelector('[data-field="event-container"]');
  eventContainer.classList.toggle('settlement', !!combat.settlementVisible);
  eventContainer.hidden = !combat.settlementVisible && !combat.eventText;
  element.querySelector('[data-field="event-kind"]').textContent = combat.settlementVisible
    ? 'result'
    : (combat.eventKind ?? '');
  element.querySelector('[data-field="event-text"]').textContent = combat.eventText ?? '';

  const cosmetics = cosmeticsForState(state);
  const { player, opponent } = orientCombatSides(combat);

  appendChildren(element.querySelector('[data-slot="opponent-board"]'),
    await createCombatBoardTokens(opponent, cosmetics, 'down'));
  appendChildren(element.querySelector('[data-slot="player-board"]'),
    await createCombatBoardTokens(player, cosmetics, 'up'));

  if (opponent) {
    appendChildren(element.querySelector('[data-slot="opponent-hero"]'), [
      await createCombatPlayerHero({
        side: opponent,
        art: await leaderArtUrl(cosmetics, opponent.leaderId),
        placement: 'top'
      })
    ]);
  }

  if (player) {
    appendChildren(element.querySelector('[data-slot="player-hero"]'), [
      await createCombatPlayerHero({
        side: player,
        art: await leaderArtUrl(cosmetics, player.leaderId),
        placement: 'bottom'
      })
    ]);
  }

  appendChildren(element.querySelector('[data-slot="opponents"]'), [
    await createOpponentRail({
      modName: state.mod?.name ?? '',
      roundLabel: state.labels?.round ?? 'Round',
      round: combat.round ?? '',
      players: await createCombatPlayerViews(state, cosmetics)
    })
  ]);

  const boardArt = await boardArtUrl(cosmetics);
  if (boardArt) scene.style.setProperty('--board-art', `url("${boardArt}")`);

  return element;
}

export function resetCombatPage() {
  lastStepKey = '';
}
