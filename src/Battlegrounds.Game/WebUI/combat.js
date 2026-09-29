(() => {
  const ui = window.BgUi = window.BgUi || {};
  const C = ui.components;
  const { escapeHtml } = ui;

  const root = document.createElement('section');
  root.id = 'combat-ui';
  root.className = 'combat-ui';
  root.hidden = true;
  document.body.appendChild(root);

  let combatState = null;
  let pollTimer = null;
  let lastStepKey = '';

  function requestState() {
    ui.bridge?.send('request-state');
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
    return C.CombatCard({
      name: unit.name,
      attack: unit.attack,
      health: unit.health,
      tier: unit.tier,
      status: unit.status,
      marker: unit.highlight,
      highlight: highlightClass(unit.highlight),
      instanceId: unit.instanceId
    });
  }

  function renderSide(side, position) {
    const units = side?.units ?? [];
    return `
      <section class="combat-side combat-side-${position} ${side?.human ? 'human' : ''}" data-component="combat-side" data-variant="${escapeHtml(position)}">
        <header class="combat-side-header">
          <span>${escapeHtml(side?.label ?? `P${side?.playerId ?? '?'}`)}</span>
          ${side?.archived ? '<small>archived</small>' : ''}
        </header>
        <div class="combat-board">
          ${units.length ? units.map(renderUnit).join('') : C.Empty({ label: 'Empty field' })}
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
      <div class="combat-shell ${stepChanged ? 'combat-step-changed' : ''} ${combat.settlementVisible ? 'settlement' : ''}" data-screen="combat">
        ${C.Panel({
          className: 'combat-topbar',
          children: `
            <div><span class="eyebrow">${escapeHtml(combatState.mod?.name ?? 'Battlegrounds')}</span><strong>${escapeHtml(combatState.labels?.combat ?? 'Combat')}</strong></div>
            <div class="combat-round">${escapeHtml(combatState.labels?.round ?? 'Round')} ${escapeHtml(combat.round)}</div>
            <div class="combat-progress">${escapeHtml(progress)}</div>`
        })}
        <main class="combat-stage">
          ${renderSide(combat.right, 'top')}
          <section class="combat-event ${combat.settlementVisible ? 'settlement' : ''}" data-component="combat-event">
            <span class="combat-event-kind">${escapeHtml(combat.settlementVisible ? 'result' : (combat.eventKind ?? 'ready'))}</span>
            <strong>${escapeHtml(combat.eventText)}</strong>
          </section>
          ${renderSide(combat.left, 'bottom')}
        </main>
      </div>`;

    ui.theme.applyComponentStyles(combatState.theme, root);
  }

  window.addEventListener('battlegrounds:state', event => {
    const nextState = event.detail;
    if (nextState?.status === 'combat') {
      combatState = nextState;
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
  });
})();
