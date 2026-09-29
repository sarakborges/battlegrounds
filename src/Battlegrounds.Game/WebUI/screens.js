(() => {
  const ui = window.BgUi = window.BgUi || {};
  const { escapeHtml } = ui;
  const C = ui.components;

  function cardMeta(entry) {
    const parts = [];
    if (entry.tier != null) parts.push(C.Badge({ text: `T${entry.tier}` }));
    if (entry.attack != null) parts.push(C.Badge({ text: `${entry.attack} ATK` }));
    if (entry.health != null) parts.push(C.Badge({ text: `${entry.health} HP` }));
    if (entry.cost != null) parts.push(C.Badge({ text: entry.cost, variant: 'accent' }));
    if (entry.frozen) parts.push(C.Badge({ text: 'Frozen', variant: 'accent' }));
    return parts.join('');
  }

  function renderLoading(state) {
    return C.Panel({
      className: 'loading-panel',
      children: `<span class="eyebrow">Battlegrounds</span><h1>Loading ${escapeHtml(state?.mod?.name ?? 'mod')}…</h1>`
    });
  }

  function renderLeaderSelection(state) {
    const labels = state.labels ?? {};
    const leaders = (state.leaders ?? []).map(leader => C.Card({
      kind: labels.leader ?? 'Leader',
      name: leader.name,
      meta: [
        C.Badge({ text: `${labels.health ?? 'Health'} ${leader.healthModifier >= 0 ? '+' : ''}${leader.healthModifier}` }),
        C.Badge({ text: `${labels.armor ?? 'Armor'} ${leader.armor}` })
      ].join(''),
      action: 'select-leader',
      variant: 'leader',
      attributes: { 'data-leader-id': leader.id }
    })).join('');

    return `
      <section class="leader-screen" data-screen="leader-selection">
        <header class="leader-copy">
          <span class="eyebrow">${escapeHtml(state.mod?.name)}</span>
          <h1>${escapeHtml(labels.chooseLeader ?? 'Choose a leader')}</h1>
          <p>Seed ${escapeHtml(state.seed)}</p>
        </header>
        <div class="leader-grid">${leaders}</div>
      </section>`;
  }

  function renderOfferCard(entry, blocked) {
    return C.Card({
      kind: entry.kind,
      name: entry.name,
      meta: cardMeta(entry),
      action: 'acquire',
      disabled: blocked,
      variant: 'offer',
      attributes: { 'data-slot': entry.slot }
    });
  }

  function renderReserveCard(state, entry, blocked) {
    const combining = state.interaction?.kind === 'combine-components' && entry.kind === 'unit';
    const action = combining ? 'toggle-combine-unit' : entry.kind === 'unit' ? 'deploy' : 'play-action';
    const disabled = combining ? !state.canAct : blocked;

    return C.Card({
      kind: entry.kind,
      name: entry.name,
      meta: cardMeta(entry),
      action,
      disabled,
      selected: entry.selectedForCombine,
      variant: 'reserve',
      attributes: {
        'data-slot': entry.slot,
        'data-unit-instance-id': entry.unitInstanceId ?? ''
      }
    });
  }

  function renderFieldCard(state, unit, blocked) {
    const combining = state.interaction?.kind === 'combine-components';
    return C.Card({
      kind: 'field',
      name: unit.name,
      meta: cardMeta(unit),
      action: combining ? 'toggle-combine-unit' : 'release',
      disabled: combining ? !state.canAct : blocked,
      selected: unit.selectedForCombine,
      variant: 'board',
      themeRole: 'card.board',
      attributes: {
        'data-slot': unit.slot,
        'data-unit-instance-id': unit.unitInstanceId
      }
    });
  }

  function renderOverlay(state) {
    const labels = state.labels ?? {};
    const pending = state.pendingChoice;

    if (pending) {
      const body = `<div class="card-row">${(pending.options ?? []).map(option => C.Card({
        kind: pending.kind,
        name: option.name,
        meta: cardMeta(option),
        action: 'resolve-choice',
        variant: 'choice',
        attributes: { 'data-option-index': option.index }
      })).join('')}</div>`;
      return C.Dialog({ eyebrow: 'Choice', title: `Choose ${pending.kind}`, body, variant: 'choice' });
    }

    const interaction = state.interaction;
    if (!interaction) return '';

    const cancel = C.Button({ label: labels.cancel ?? 'Cancel', action: 'cancel-interaction' });

    if (interaction.kind === 'combine-recipe') {
      const recipes = (interaction.recipes ?? []).map(recipe => C.Card({
        kind: `${recipe.requiredCopies} × ${recipe.source}`,
        name: recipe.name,
        meta: C.Badge({ text: `→ ${recipe.result}`, variant: 'accent' }),
        action: 'select-combine-recipe',
        variant: 'choice',
        attributes: { 'data-combine-id': recipe.id }
      })).join('') || C.Empty({ label: 'No available recipes' });

      return C.Dialog({
        eyebrow: labels.combine ?? 'Combine',
        title: 'Choose recipe',
        actions: cancel,
        body: `<div class="card-row">${recipes}</div>`,
        variant: 'combine-recipe'
      });
    }

    if (interaction.kind === 'combine-components') {
      const confirm = C.Button({
        label: labels.confirm ?? 'Confirm',
        action: 'confirm-interaction',
        variant: 'primary',
        disabled: interaction.selected !== interaction.requiredCopies
      });

      return C.Dialog({
        eyebrow: interaction.name,
        title: `Select ${interaction.requiredCopies} components · ${interaction.selected} selected`,
        body: `<div class="dialog-actions">${cancel}${confirm}</div>`,
        variant: 'combine-components'
      });
    }

    if (interaction.kind === 'target') {
      const targets = (interaction.candidates ?? []).map(candidate => C.Card({
        kind: `P${candidate.ownerId}`,
        name: candidate.name,
        meta: cardMeta(candidate),
        action: 'select-target',
        variant: 'target',
        attributes: { 'data-unit-instance-id': candidate.unitInstanceId }
      })).join('') || C.Empty({ label: 'No valid targets' });

      return C.Dialog({
        eyebrow: 'Target',
        title: `Choose target · ${interaction.zone}`,
        actions: cancel,
        body: `<div class="card-row">${targets}</div>`,
        variant: 'target'
      });
    }

    return '';
  }

  function renderMatch(state) {
    const labels = state.labels ?? {};
    const human = state.human ?? {};
    const blocked = !state.canAct || !!state.pendingChoice || !!state.interaction;

    const topbar = C.Panel({
      className: 'topbar',
      children: `
        <div class="brand"><span class="eyebrow">${escapeHtml(state.mod?.name)}</span><strong>${escapeHtml(state.phase)}</strong></div>
        <div class="round-pill">${escapeHtml(labels.round ?? 'Round')} ${escapeHtml(state.round)}</div>
        <div class="stats">
          ${C.Stat({ label: labels.health ?? 'Health', value: human.health })}
          ${C.Stat({ label: labels.armor ?? 'Armor', value: human.armor })}
          ${C.Stat({ label: labels.resource ?? 'Resource', value: human.resource })}
          ${C.Stat({ label: labels.tier ?? 'Tier', value: human.tier })}
        </div>`
    });

    const players = (state.players ?? []).map(player => C.PlayerChip({
      id: player.id,
      leader: player.leader ?? '—',
      health: player.health,
      human: player.human,
      eliminated: player.eliminated
    })).join('');

    const field = (state.field ?? []).map(unit => renderFieldCard(state, unit, blocked)).join('') || C.Empty({ label: labels.field ?? 'Field' });
    const offer = (state.offer ?? []).map(entry => renderOfferCard(entry, blocked)).join('') || C.Empty({ label: labels.offer ?? 'Offer' });
    const reserve = (state.reserve ?? []).map(entry => renderReserveCard(state, entry, blocked)).join('') || C.Empty({ label: labels.reserve ?? 'Reserve' });

    const actions = [
      C.Button({ label: labels.refresh ?? 'Refresh', action: 'refresh', disabled: blocked, themeRole: 'button.tavernRefresh' }),
      C.Button({ label: `${labels.upgrade ?? 'Upgrade'}${human.upgradeCost == null ? '' : ` · ${human.upgradeCost}`}`, action: 'upgrade', disabled: blocked || human.upgradeCost == null, themeRole: 'button.tavernUpgrade' }),
      C.Button({ label: human.offerFrozen ? (labels.unfreeze ?? 'Unfreeze') : (labels.freeze ?? 'Freeze'), action: 'toggle-freeze', disabled: blocked, themeRole: 'button.tavernFreeze' }),
      C.Button({ label: labels.usePower ?? 'Use power', action: 'use-power', disabled: blocked || !human.power, themeRole: 'button.tavernAction' }),
      C.Button({ label: labels.combine ?? 'Combine', action: 'begin-combine', disabled: blocked, themeRole: 'button.tavernAction' }),
      C.Button({ label: labels.endPreparation ?? 'End preparation', action: 'end-preparation', disabled: blocked, variant: 'primary', themeRole: 'button.primary' })
    ].join('');

    return `
      ${topbar}
      <section class="game-stage" data-screen="preparation">
        <div class="player-strip">${players}</div>
        ${C.Zone({ title: labels.field ?? 'Field', count: (state.field ?? []).length, children: field, variant: 'field' })}
        ${C.Zone({ title: labels.offer ?? 'Offer', count: (state.offer ?? []).length, children: offer, variant: 'offer' })}
        ${C.Zone({ title: labels.reserve ?? 'Reserve', count: (state.reserve ?? []).length, children: reserve, variant: 'reserve' })}
      </section>
      <footer>
        ${state.canAct ? '' : `<div class="waiting-banner">Waiting for preparation initiative${state.currentPreparationPlayerId != null ? ` · P${escapeHtml(state.currentPreparationPlayerId)}` : ''}</div>`}
        ${C.Panel({ className: 'actions', children: actions })}
      </footer>
      ${renderOverlay(state)}`;
  }

  ui.screens = { renderLoading, renderLeaderSelection, renderMatch, cardMeta };
})();
