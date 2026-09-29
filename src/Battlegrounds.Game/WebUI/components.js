(() => {
  const ui = window.BgUi = window.BgUi || {};

  function escapeHtml(value) {
    return String(value ?? '')
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#039;');
  }

  function attrs(values = {}) {
    return Object.entries(values)
      .filter(([, value]) => value !== undefined && value !== null && value !== false)
      .map(([key, value]) => value === true ? key : `${key}="${escapeHtml(value)}"`)
      .join(' ');
  }

  function role(component, variant, explicitRole) {
    if (explicitRole) return explicitRole;
    if (!variant || variant === 'default') return component;
    return `${component}.${variant}`;
  }

  function Panel({ children = '', className = '', variant = 'default', themeRole = null, attributes = {} } = {}) {
    return `<section class="panel ${escapeHtml(className)}" data-component="panel" data-variant="${escapeHtml(variant)}" data-theme-role="${escapeHtml(role('panel', variant, themeRole))}" ${attrs(attributes)}>${children}</section>`;
  }

  function Button({ label = '', action = null, variant = 'default', themeRole = null, disabled = false, className = '', attributes = {} } = {}) {
    return `<button class="action-button ${variant === 'primary' ? 'primary' : ''} ${escapeHtml(className)}" data-component="button" data-variant="${escapeHtml(variant)}" data-theme-role="${escapeHtml(role('button', variant, themeRole))}" ${action ? `data-action="${escapeHtml(action)}"` : ''} ${disabled ? 'disabled' : ''} ${attrs(attributes)}>${escapeHtml(label)}</button>`;
  }

  function Badge({ text = '', variant = 'default', className = '' } = {}) {
    return `<span class="badge ${variant !== 'default' ? escapeHtml(variant) : ''} ${escapeHtml(className)}" data-component="badge" data-variant="${escapeHtml(variant)}">${escapeHtml(text)}</span>`;
  }

  function Card({ kind = '', name = '', meta = '', action = null, variant = 'default', themeRole = null, disabled = false, selected = false, className = '', attributes = {} } = {}) {
    return `
      <button class="card ${selected ? 'selected' : ''} ${escapeHtml(className)}" data-component="card" data-variant="${escapeHtml(variant)}" data-theme-role="${escapeHtml(role('card', variant, themeRole))}" ${action ? `data-action="${escapeHtml(action)}"` : ''} ${disabled ? 'disabled' : ''} ${attrs(attributes)}>
        <span class="kind">${escapeHtml(kind)}</span>
        <strong class="name">${escapeHtml(name)}</strong>
        <span class="meta">${meta}</span>
      </button>`;
  }

  function CombatCard({ name = '', attack = '—', health = '—', tier = null, status = '', marker = '', highlight = '', instanceId = '' } = {}) {
    return `
      <article class="combat-card ${escapeHtml(highlight)}" data-component="combat-card" data-variant="${escapeHtml(highlight || 'default')}" data-instance-id="${escapeHtml(instanceId)}">
        ${marker ? `<span class="combat-marker">${escapeHtml(marker)}</span>` : ''}
        ${tier > 0 ? `<span class="combat-tier">T${escapeHtml(tier)}</span>` : ''}
        <strong class="combat-card-name">${escapeHtml(name)}</strong>
        <div class="combat-card-stats">
          <span><b>${escapeHtml(attack ?? '—')}</b> ATK</span>
          <span><b>${escapeHtml(health ?? '—')}</b> HP</span>
        </div>
        ${status ? `<span class="combat-unit-status">${escapeHtml(status)}</span>` : ''}
      </article>`;
  }

  function Stat({ label = '', value = '—' } = {}) {
    return `<div class="stat" data-component="stat"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value ?? '—')}</strong></div>`;
  }

  function PlayerChip({ id = '', leader = '—', health = '—', human = false, eliminated = false } = {}) {
    return `<span class="player-chip ${human ? 'human' : ''} ${eliminated ? 'eliminated' : ''}" data-component="player-chip" data-variant="${human ? 'human' : eliminated ? 'eliminated' : 'default'}">P${escapeHtml(id)} · ${escapeHtml(leader)} · ${escapeHtml(health)} HP</span>`;
  }

  function Empty({ label = '' } = {}) {
    return `<div class="empty" data-component="empty">${escapeHtml(label)} · —</div>`;
  }

  function Zone({ title = '', count = 0, children = '', variant = 'default', themeRole = 'panel' } = {}) {
    return Panel({
      className: `zone zone-${variant}`,
      themeRole,
      children: `<div class="zone-head"><h2>${escapeHtml(title)}</h2><span>${escapeHtml(count)}</span></div><div class="card-row">${children}</div>`
    });
  }

  function Dialog({ eyebrow = '', title = '', body = '', actions = '', variant = 'default' } = {}) {
    return `
      <div class="overlay overlay-${escapeHtml(variant)}" data-component="overlay" data-variant="${escapeHtml(variant)}">
        ${Panel({
          className: `dialog dialog-${variant}`,
          children: `<div class="dialog-head"><div class="dialog-copy"><span class="eyebrow">${escapeHtml(eyebrow)}</span><h2>${escapeHtml(title)}</h2></div>${actions}</div>${body}`
        })}
      </div>`;
  }

  ui.escapeHtml = escapeHtml;
  ui.attrs = attrs;
  ui.components = { Panel, Button, Badge, Card, CombatCard, Stat, PlayerChip, Empty, Zone, Dialog };
})();
