(() => {
  const app = document.getElementById('app');
  const toast = document.getElementById('toast');
  let state = null;
  let toastTimer = null;

  function send(type, payload = {}) {
    if (typeof window.sendIpcMessage !== 'function') {
      showError('Godot CEF IPC is not available.');
      return;
    }
    window.sendIpcMessage(JSON.stringify({ type, ...payload }));
  }

  function receive(raw) {
    try {
      const message = typeof raw === 'string' ? JSON.parse(raw) : raw;
      if (!message || typeof message.type !== 'string') return;
      if (message.type === 'state') {
        state = message.payload;
        applyTheme(state?.theme, state?.phase === 'combat' ? 'combat' : 'preparation');
        render();
      } else if (message.type === 'error') {
        showError(message.payload?.message ?? 'Unknown game error');
      }
    } catch (error) {
      showError(`Invalid game message: ${error.message}`);
    }
  }

  function showError(message) {
    toast.textContent = message;
    toast.classList.add('visible');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => toast.classList.remove('visible'), 4200);
  }

  function escapeHtml(value) {
    return String(value ?? '')
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#039;');
  }

  function slug(value) {
    return String(value).replace(/([a-z0-9])([A-Z])/g, '$1-$2').replace(/[^a-zA-Z0-9]+/g, '-').toLowerCase();
  }

  function resolveColor(theme, value) {
    if (!value) return null;
    if (value.startsWith('#') || value.startsWith('rgb') || value.startsWith('hsl')) return value;
    return theme?.colors?.[value] ?? null;
  }

  function resolveMetric(collection, value) {
    if (value == null) return null;
    if (typeof value === 'number') return `${value}px`;
    if (/^-?\d+(\.\d+)?(px|rem|em|%|vh|vw)$/.test(value)) return value;
    const resolved = collection?.[value];
    return resolved == null ? null : `${resolved}px`;
  }

  function applyTheme(theme, screenRole) {
    if (!theme) return;
    const root = document.documentElement;

    Object.entries(theme.colors ?? {}).forEach(([key, value]) => {
      root.style.setProperty(`--theme-color-${slug(key)}`, value);
    });
    Object.entries(theme.spacing ?? {}).forEach(([key, value]) => {
      root.style.setProperty(`--theme-space-${slug(key)}`, `${value}px`);
    });
    Object.entries(theme.radii ?? {}).forEach(([key, value]) => {
      root.style.setProperty(`--theme-radius-${slug(key)}`, `${value}px`);
    });
    Object.entries(theme.fontSizes ?? {}).forEach(([key, value]) => {
      root.style.setProperty(`--theme-font-size-${slug(key)}`, `${value}px`);
    });
    Object.entries(theme.metrics ?? {}).forEach(([key, value]) => {
      root.style.setProperty(`--theme-metric-${slug(key)}`, String(value));
    });

    const screen = theme.screens?.[screenRole];
    const screenColor = resolveColor(theme, screen?.backgroundColor);
    if (screenColor) root.style.setProperty('--surface-0', screenColor);
  }

  function applyRoleStyles() {
    if (!state?.theme) return;
    const theme = state.theme;
    document.querySelectorAll('[data-theme-role]').forEach(element => {
      const requestedRole = element.dataset.themeRole;
      const fallbackRole = requestedRole?.startsWith('button.') ? 'button' : requestedRole?.startsWith('card.') ? 'card' : null;
      const style = theme.components?.[requestedRole] ?? (fallbackRole ? theme.components?.[fallbackRole] : null);
      if (!style) return;

      const textColor = resolveColor(theme, style.textColor);
      const backgroundColor = resolveColor(theme, style.backgroundColor);
      const borderColor = resolveColor(theme, style.borderColor);
      const fontSize = resolveMetric(theme.fontSizes, style.fontSize);
      const radius = resolveMetric(theme.radii, style.radius);
      const padX = resolveMetric(theme.spacing, style.padding?.horizontal);
      const padY = resolveMetric(theme.spacing, style.padding?.vertical);

      if (textColor) element.style.color = textColor;
      if (backgroundColor) element.style.backgroundColor = backgroundColor;
      if (borderColor) element.style.borderColor = borderColor;
      if (style.borderWidth != null) element.style.borderWidth = `${style.borderWidth}px`;
      if (fontSize) element.style.fontSize = fontSize;
      if (radius) element.style.borderRadius = radius;
      if (padX) {
        element.style.paddingLeft = padX;
        element.style.paddingRight = padX;
      }
      if (padY) {
        element.style.paddingTop = padY;
        element.style.paddingBottom = padY;
      }
      if (style.opacity != null) element.style.opacity = String(style.opacity);
    });
  }

  function render() {
    if (!state) return;
    if (state.status === 'leader-selection') renderLeaderSelection();
    else if (state.status === 'match' || state.status === 'finished') renderMatch();
    else renderLoading();
    applyRoleStyles();
  }

  function renderLoading() {
    app.innerHTML = `
      <section class="loading-panel" data-theme-role="panel">
        <span class="eyebrow">Battlegrounds</span>
        <h1>Loading ${escapeHtml(state?.mod?.name ?? 'mod')}…</h1>
      </section>`;
  }

  function renderLeaderSelection() {
    const labels = state.labels ?? {};
    app.innerHTML = `
      <section class="leader-screen">
        <header class="leader-copy">
          <span class="eyebrow">${escapeHtml(state.mod?.name)}</span>
          <h1>${escapeHtml(labels.chooseLeader ?? 'Choose a leader')}</h1>
          <p>Seed ${escapeHtml(state.seed)}</p>
        </header>
        <div class="leader-grid">
          ${(state.leaders ?? []).map(leader => `
            <button class="card" data-theme-role="card" data-action="select-leader" data-leader-id="${escapeHtml(leader.id)}">
              <span class="kind">${escapeHtml(labels.leader ?? 'Leader')}</span>
              <strong class="name">${escapeHtml(leader.name)}</strong>
              <span class="meta">
                <span class="badge">${escapeHtml(labels.health ?? 'Health')} ${leader.healthModifier >= 0 ? '+' : ''}${escapeHtml(leader.healthModifier)}</span>
                <span class="badge">${escapeHtml(labels.armor ?? 'Armor')} ${escapeHtml(leader.armor)}</span>
              </span>
            </button>`).join('')}
        </div>
      </section>`;
    bindActions();
  }

  function renderMatch() {
    const labels = state.labels ?? {};
    const human = state.human ?? {};
    const blocked = !state.canAct || !!state.pendingChoice || !!state.interaction;

    app.innerHTML = `
      <header class="topbar panel" data-theme-role="panel">
        <div class="brand">
          <span class="eyebrow">${escapeHtml(state.mod?.name)}</span>
          <strong>${escapeHtml(state.phase)}</strong>
        </div>
        <div class="round-pill">${escapeHtml(labels.round ?? 'Round')} ${escapeHtml(state.round)}</div>
        <div class="stats">
          ${stat(labels.health ?? 'Health', human.health)}
          ${stat(labels.armor ?? 'Armor', human.armor)}
          ${stat(labels.resource ?? 'Resource', human.resource)}
          ${stat(labels.tier ?? 'Tier', human.tier)}
        </div>
      </header>

      <section class="game-stage">
        <div class="player-strip">
          ${(state.players ?? []).map(player => `
            <span class="player-chip ${player.human ? 'human' : ''} ${player.eliminated ? 'eliminated' : ''}">
              P${escapeHtml(player.id)} · ${escapeHtml(player.leader ?? '—')} · ${escapeHtml(player.health)} HP
            </span>`).join('')}
        </div>

        <section class="zone panel" data-theme-role="panel">
          <div class="zone-head"><h2>${escapeHtml(labels.field ?? 'Field')}</h2><span>${(state.field ?? []).length}</span></div>
          <div class="card-row">
            ${(state.field ?? []).length ? state.field.map(unit => unitCard(unit, 'field', blocked)).join('') : empty(labels.field ?? 'Field')}
          </div>
        </section>

        <section class="zone panel" data-theme-role="panel">
          <div class="zone-head"><h2>${escapeHtml(labels.offer ?? 'Offer')}</h2><span>${(state.offer ?? []).length}</span></div>
          <div class="card-row">
            ${(state.offer ?? []).length ? state.offer.map(entry => offerCard(entry, blocked)).join('') : empty(labels.offer ?? 'Offer')}
          </div>
        </section>

        <section class="zone panel" data-theme-role="panel">
          <div class="zone-head"><h2>${escapeHtml(labels.reserve ?? 'Reserve')}</h2><span>${(state.reserve ?? []).length}</span></div>
          <div class="card-row">
            ${(state.reserve ?? []).length ? state.reserve.map(entry => reserveCard(entry, blocked)).join('') : empty(labels.reserve ?? 'Reserve')}
          </div>
        </section>
      </section>

      <footer>
        ${state.canAct ? '' : `<div class="waiting-banner">Waiting for preparation initiative${state.currentPreparationPlayerId != null ? ` · P${escapeHtml(state.currentPreparationPlayerId)}` : ''}</div>`}
        <div class="actions panel" data-theme-role="panel">
          ${actionButton(labels.refresh ?? 'Refresh', 'refresh', blocked, 'button.tavernRefresh')}
          ${actionButton(labels.upgrade ?? 'Upgrade', 'upgrade', blocked || human.upgradeCost == null, 'button.tavernUpgrade', human.upgradeCost == null ? '' : ` · ${human.upgradeCost}`)}
          ${actionButton(human.offerFrozen ? (labels.unfreeze ?? 'Unfreeze') : (labels.freeze ?? 'Freeze'), 'toggle-freeze', blocked, 'button.tavernFreeze')}
          ${actionButton(labels.usePower ?? 'Use power', 'use-power', blocked || !human.power, 'button.tavernAction')}
          ${actionButton(labels.combine ?? 'Combine', 'begin-combine', blocked, 'button.tavernAction')}
          ${actionButton(labels.endPreparation ?? 'End preparation', 'end-preparation', blocked, 'button.primary')}
        </div>
      </footer>
      ${renderOverlay()}`;

    bindActions();
  }

  function stat(label, value) {
    return `<div class="stat"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value ?? '—')}</strong></div>`;
  }

  function empty(label) {
    return `<div class="empty">${escapeHtml(label)} · —</div>`;
  }

  function cardMeta(entry) {
    const parts = [];
    if (entry.tier != null) parts.push(`<span class="badge">T${escapeHtml(entry.tier)}</span>`);
    if (entry.attack != null) parts.push(`<span class="badge">${escapeHtml(entry.attack)} ATK</span>`);
    if (entry.health != null) parts.push(`<span class="badge">${escapeHtml(entry.health)} HP</span>`);
    if (entry.cost != null) parts.push(`<span class="badge accent">${escapeHtml(entry.cost)}</span>`);
    if (entry.frozen) parts.push('<span class="badge accent">Frozen</span>');
    return parts.join('');
  }

  function offerCard(entry, blocked) {
    return `
      <button class="card" data-theme-role="card" data-action="acquire" data-slot="${entry.slot}" ${blocked ? 'disabled' : ''}>
        <span class="kind">${escapeHtml(entry.kind)}</span>
        <strong class="name">${escapeHtml(entry.name)}</strong>
        <span class="meta">${cardMeta(entry)}</span>
      </button>`;
  }

  function reserveCard(entry, blocked) {
    const combining = state.interaction?.kind === 'combine-components' && entry.kind === 'unit';
    const action = combining ? 'toggle-combine-unit' : entry.kind === 'unit' ? 'deploy' : 'play-action';
    const disabled = combining ? !state.canAct : blocked;
    return `
      <button class="card ${entry.selectedForCombine ? 'selected' : ''}" data-theme-role="card" data-action="${action}" data-slot="${entry.slot}" data-unit-instance-id="${entry.unitInstanceId ?? ''}" ${disabled ? 'disabled' : ''}>
        <span class="kind">${escapeHtml(entry.kind)}</span>
        <strong class="name">${escapeHtml(entry.name)}</strong>
        <span class="meta">${cardMeta(entry)}</span>
      </button>`;
  }

  function unitCard(unit, zone, blocked) {
    const combining = state.interaction?.kind === 'combine-components';
    const action = combining ? 'toggle-combine-unit' : 'release';
    const disabled = combining ? !state.canAct : blocked;
    return `
      <button class="card ${unit.selectedForCombine ? 'selected' : ''}" data-theme-role="card.board" data-action="${action}" data-slot="${unit.slot}" data-unit-instance-id="${unit.unitInstanceId}" ${disabled ? 'disabled' : ''}>
        <span class="kind">${escapeHtml(zone)}</span>
        <strong class="name">${escapeHtml(unit.name)}</strong>
        <span class="meta">${cardMeta(unit)}</span>
      </button>`;
  }

  function actionButton(label, action, disabled, role, suffix = '') {
    return `<button class="action-button ${role === 'button.primary' ? 'primary' : ''}" data-theme-role="${role}" data-action="${action}" ${disabled ? 'disabled' : ''}>${escapeHtml(label)}${escapeHtml(suffix)}</button>`;
  }

  function renderOverlay() {
    const labels = state.labels ?? {};
    const pending = state.pendingChoice;
    if (pending) {
      return `
        <div class="overlay">
          <section class="dialog panel" data-theme-role="panel">
            <div class="dialog-head"><div class="dialog-copy"><span class="eyebrow">Choice</span><h2>Choose ${escapeHtml(pending.kind)}</h2></div></div>
            <div class="card-row">
              ${(pending.options ?? []).map(option => `
                <button class="card" data-theme-role="card" data-action="resolve-choice" data-option-index="${option.index}">
                  <span class="kind">${escapeHtml(pending.kind)}</span>
                  <strong class="name">${escapeHtml(option.name)}</strong>
                  <span class="meta">${cardMeta(option)}</span>
                </button>`).join('')}
            </div>
          </section>
        </div>`;
    }

    const interaction = state.interaction;
    if (!interaction) return '';

    if (interaction.kind === 'combine-recipe') {
      return `
        <div class="overlay">
          <section class="dialog panel" data-theme-role="panel">
            <div class="dialog-head">
              <div class="dialog-copy"><span class="eyebrow">${escapeHtml(labels.combine ?? 'Combine')}</span><h2>Choose recipe</h2></div>
              <button class="action-button" data-theme-role="button" data-action="cancel-interaction">${escapeHtml(labels.cancel ?? 'Cancel')}</button>
            </div>
            <div class="card-row">
              ${(interaction.recipes ?? []).length ? interaction.recipes.map(recipe => `
                <button class="card" data-theme-role="card" data-action="select-combine-recipe" data-combine-id="${escapeHtml(recipe.id)}">
                  <span class="kind">${escapeHtml(recipe.requiredCopies)} × ${escapeHtml(recipe.source)}</span>
                  <strong class="name">${escapeHtml(recipe.name)}</strong>
                  <span class="meta"><span class="badge accent">→ ${escapeHtml(recipe.result)}</span></span>
                </button>`).join('') : '<div class="empty">No available recipes</div>'}
            </div>
          </section>
        </div>`;
    }

    if (interaction.kind === 'combine-components') {
      return `
        <div class="overlay" style="pointer-events:none;background:rgba(5,7,12,.28)">
          <section class="dialog panel" data-theme-role="panel" style="pointer-events:auto;align-self:end">
            <div class="dialog-head">
              <div class="dialog-copy"><span class="eyebrow">${escapeHtml(interaction.name)}</span><h2>Select ${escapeHtml(interaction.requiredCopies)} components · ${escapeHtml(interaction.selected)} selected</h2></div>
            </div>
            <div class="dialog-actions">
              <button class="action-button" data-theme-role="button" data-action="cancel-interaction">${escapeHtml(labels.cancel ?? 'Cancel')}</button>
              <button class="action-button primary" data-theme-role="button.primary" data-action="confirm-interaction" ${interaction.selected !== interaction.requiredCopies ? 'disabled' : ''}>${escapeHtml(labels.confirm ?? 'Confirm')}</button>
            </div>
          </section>
        </div>`;
    }

    if (interaction.kind === 'target') {
      return `
        <div class="overlay">
          <section class="dialog panel" data-theme-role="panel">
            <div class="dialog-head">
              <div class="dialog-copy"><span class="eyebrow">Target</span><h2>Choose target · ${escapeHtml(interaction.zone)}</h2></div>
              <button class="action-button" data-theme-role="button" data-action="cancel-interaction">${escapeHtml(labels.cancel ?? 'Cancel')}</button>
            </div>
            <div class="card-row">
              ${(interaction.candidates ?? []).length ? interaction.candidates.map(candidate => `
                <button class="card" data-theme-role="card" data-action="select-target" data-unit-instance-id="${candidate.unitInstanceId}">
                  <span class="kind">P${escapeHtml(candidate.ownerId)}</span>
                  <strong class="name">${escapeHtml(candidate.name)}</strong>
                  <span class="meta">${cardMeta(candidate)}</span>
                </button>`).join('') : '<div class="empty">No valid targets</div>'}
            </div>
          </section>
        </div>`;
    }

    return '';
  }

  function bindActions() {
    document.querySelectorAll('[data-action]').forEach(element => {
      element.addEventListener('click', () => {
        const action = element.dataset.action;
        switch (action) {
          case 'select-leader': send(action, { leaderId: element.dataset.leaderId }); break;
          case 'acquire':
          case 'deploy':
          case 'play-action':
          case 'release': send(action, { slot: Number(element.dataset.slot) }); break;
          case 'resolve-choice': send(action, { optionIndex: Number(element.dataset.optionIndex) }); break;
          case 'select-target':
          case 'toggle-combine-unit': send(action, { unitInstanceId: Number(element.dataset.unitInstanceId) }); break;
          case 'select-combine-recipe': send(action, { combineId: element.dataset.combineId }); break;
          default: send(action); break;
        }
      });
    });
  }

  if (window.ipcMessage?.addListener) {
    window.ipcMessage.addListener(receive);
  } else {
    window.onIpcMessage = receive;
  }

  window.addEventListener('DOMContentLoaded', () => send('request-state'));
})();
