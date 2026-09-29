(() => {
  const root = document.createElement('section');
  root.id = 'combat-ui';
  root.className = 'combat-ui';
  root.hidden = true;
  document.body.appendChild(root);

  let combatState = null;
  let pollTimer = null;
  let lastStepKey = '';

  function escapeHtml(value) {
    return String(value ?? '')
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#039;');
  }

  function requestState() {
    if (typeof window.sendIpcMessage !== 'function') return;
    window.sendIpcMessage(JSON.stringify({ type: 'request-state' }));
  }

  function startPolling() {
    if (pollTimer !== null) return;
    pollTimer = window.setInterval(requestState, 60);
  }

  function stopPolling() {
    if (pollTimer === null) return;
    window.clearInterval(pollTimer);
    pollTimer = null;
  }

  function highlightClass(highlight) {
    switch (highlight) {
      case '→': return 'attacker';
      case '◎': return 'target';
      case '+': return 'summoned';
      case 'Δ': return 'stats';
      case '−': return 'damaged';
      case '×': return 'destroyed';
      case '↻': return 'revived';
      case '◆': return 'triggered';
      case '◇': return 'behavior';
      default: return '';
    }
  }

  function renderUnit(unit) {
    const marker = unit.highlight ? `<span class="combat-marker">${escapeHtml(unit.highlight)}</span>` : '';
    const tier = unit.tier > 0 ? `<span class="combat-tier">T${escapeHtml(unit.tier)}</span>` : '';
    const status = unit.status ? `<span class="combat-unit-status">${escapeHtml(unit.status)}</span>` : '';
    const attack = unit.attack == null ? '—' : escapeHtml(unit.attack);

    return `
      <article class="combat-card ${highlightClass(unit.highlight)}" data-instance-id="${escapeHtml(unit.instanceId)}">
        ${marker}
        ${tier}
        <strong class="combat-card-name">${escapeHtml(unit.name)}</strong>
        <div class="combat-card-stats">
          <span><b>${attack}</b> ATK</span>
          <span><b>${escapeHtml(unit.health)}</b> HP</span>
        </div>
        ${status}
      </article>`;
  }

  function renderSide(side, position) {
    const units = side?.units ?? [];
    return `
      <section class="combat-side combat-side-${position} ${side?.human ? 'human' : ''}">
        <header class="combat-side-header">
          <span>${escapeHtml(side?.label ?? `P${side?.playerId ?? '?'}`)}</span>
          ${side?.archived ? '<small>archived</small>' : ''}
        </header>
        <div class="combat-board">
          ${units.length ? units.map(renderUnit).join('') : '<div class="combat-empty">Empty field</div>'}
        </div>
      </section>`;
  }

  function render() {
    const combat = combatState?.combat;
    if (!combat) return;

    const stepKey = `${combat.round}:${combat.eventSequence}:${combat.settlementVisible}`;
    const stepChanged = stepKey !== lastStepKey;
    lastStepKey = stepKey;

    const progress = combat.settlementVisible
      ? 'Settlement'
      : combat.eventSequence > 0
        ? `${combat.eventSequence} / ${combat.eventCount}`
        : `0 / ${combat.eventCount}`;

    root.innerHTML = `
      <div class="combat-shell ${stepChanged ? 'combat-step-changed' : ''} ${combat.settlementVisible ? 'settlement' : ''}">
        <header class="combat-topbar">
          <div>
            <span class="eyebrow">${escapeHtml(combatState.mod?.name ?? 'Battlegrounds')}</span>
            <strong>${escapeHtml(combatState.labels?.combat ?? 'Combat')}</strong>
          </div>
          <div class="combat-round">${escapeHtml(combatState.labels?.round ?? 'Round')} ${escapeHtml(combat.round)}</div>
          <div class="combat-progress">${escapeHtml(progress)}</div>
        </header>

        <main class="combat-stage">
          ${renderSide(combat.right, 'top')}

          <section class="combat-event ${combat.settlementVisible ? 'settlement' : ''}">
            <span class="combat-event-kind">${escapeHtml(combat.settlementVisible ? 'result' : (combat.eventKind ?? 'ready'))}</span>
            <strong>${escapeHtml(combat.eventText)}</strong>
          </section>

          ${renderSide(combat.left, 'bottom')}
        </main>
      </div>`;
  }

  function receive(raw) {
    try {
      const message = typeof raw === 'string' ? JSON.parse(raw) : raw;
      if (message?.type !== 'state') return;

      if (message.payload?.status === 'combat') {
        combatState = message.payload;
        root.hidden = false;
        render();
        startPolling();
        return;
      }

      combatState = null;
      root.hidden = true;
      root.replaceChildren();
      lastStepKey = '';
      stopPolling();
    } catch {
      // app.js owns user-visible IPC errors; combat rendering fails closed.
    }
  }

  if (window.ipcMessage?.addListener) {
    window.ipcMessage.addListener(receive);
  } else {
    const previous = window.onIpcMessage;
    window.onIpcMessage = raw => {
      if (typeof previous === 'function') previous(raw);
      receive(raw);
    };
  }
})();
