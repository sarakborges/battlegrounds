const DRAG_THRESHOLD = 5;

function sourceFrom(element) {
  const kind = element.dataset.dragKind;
  if (!kind) return null;

  if (kind === 'field') {
    return {
      kind,
      index: Number(element.dataset.dragIndex),
      valid: element.dataset.dragValid !== 'false',
      element
    };
  }

  return {
    kind,
    slot: Number(element.dataset.dragSlot),
    valid: element.dataset.dragValid !== 'false',
    element
  };
}

function compatible(target, drag) {
  const kind = target?.dataset.dropKind;
  if (!kind || !drag) return false;
  if (kind === 'hero') return drag.kind === 'offer';
  if (kind === 'shopkeeper') return drag.kind === 'field';
  if (kind === 'field-surface') return drag.kind === 'field' || drag.kind === 'reserve-unit';
  return false;
}

function valid(target, drag) {
  if (!compatible(target, drag)) return false;
  if (target.dataset.dropKind === 'hero') return drag.valid;
  if (target.dataset.dropKind === 'field-surface' && drag.kind === 'reserve-unit') return drag.valid;
  return true;
}

function insertionIndex(fieldSurface, clientX) {
  const cards = [...fieldSurface.querySelectorAll('[data-drag-kind="field"]')];
  for (let index = 0; index < cards.length; index += 1) {
    const rect = cards[index].getBoundingClientRect();
    if (clientX < rect.left + rect.width / 2) return index;
  }
  return cards.length;
}

function clearFeedback(root) {
  for (const target of root.querySelectorAll('.is-drop-hover, .is-drop-invalid')) {
    target.classList.remove('is-drop-hover', 'is-drop-invalid');
  }
  for (const slot of root.querySelectorAll('.drop-slot.is-drop-preview')) {
    slot.classList.remove('is-drop-preview');
  }
}

function dispatch(root, action, payload) {
  root.dispatchEvent(new CustomEvent('battlegrounds-action', {
    bubbles: true,
    detail: { action, payload }
  }));
}

function makeGhost(source) {
  const ghost = source.cloneNode(true);
  ghost.classList.add('drag-ghost');
  ghost.removeAttribute('data-action');
  document.body.appendChild(ghost);
  return ghost;
}

function moveGhost(ghost, event) {
  ghost.style.left = `${event.clientX}px`;
  ghost.style.top = `${event.clientY}px`;
}

export function bindPreparationDrag(root) {
  let pending = null;
  let drag = null;
  let lastDragEnd = 0;

  const finish = event => {
    if (!drag) {
      pending = null;
      return;
    }

    const hit = document.elementFromPoint(event.clientX, event.clientY);
    const target = hit?.closest?.('[data-drop-kind]');
    if (target && valid(target, drag)) {
      switch (target.dataset.dropKind) {
        case 'hero':
          dispatch(root, 'acquire', { slot: drag.slot });
          break;
        case 'shopkeeper':
          dispatch(root, 'release', { slot: drag.index });
          break;
        case 'field-surface': {
          const index = insertionIndex(target, event.clientX);
          if (drag.kind === 'field') dispatch(root, 'reorder-field', { fromIndex: drag.index, insertionIndex: index });
          else if (drag.kind === 'reserve-unit') dispatch(root, 'deploy-at', { slot: drag.slot, insertionIndex: index });
          break;
        }
      }
    }

    lastDragEnd = performance.now();
    drag.element.classList.remove('is-dragging');
    drag.ghost.remove();
    root.removeAttribute('data-drag-kind');
    clearFeedback(root);
    drag = null;
    pending = null;
  };

  root.addEventListener('pointerdown', event => {
    if (event.button !== 0) return;
    const element = event.target.closest('[data-drag-kind]');
    if (!element || element.disabled || element.dataset.dragEnabled !== 'true') return;

    const source = sourceFrom(element);
    if (!source) return;
    pending = { ...source, startX: event.clientX, startY: event.clientY };
  });

  root.addEventListener('pointermove', event => {
    if (pending && !drag) {
      if (Math.hypot(event.clientX - pending.startX, event.clientY - pending.startY) < DRAG_THRESHOLD) return;
      drag = { ...pending, ghost: makeGhost(pending.element) };
      pending = null;
      drag.element.classList.add('is-dragging');
      root.dataset.dragKind = drag.kind;
    }

    if (!drag) return;
    event.preventDefault();
    moveGhost(drag.ghost, event);
    clearFeedback(root);

    const hit = document.elementFromPoint(event.clientX, event.clientY);
    const target = hit?.closest?.('[data-drop-kind]');
    if (!target || !compatible(target, drag)) return;

    const isValid = valid(target, drag);
    target.classList.add(isValid ? 'is-drop-hover' : 'is-drop-invalid');
    if (target.dataset.dropKind === 'field-surface') {
      const index = insertionIndex(target, event.clientX);
      target.querySelector(`.drop-slot[data-insertion-index="${index}"]`)?.classList.add('is-drop-preview');
    }
  });

  root.addEventListener('pointerup', finish);
  root.addEventListener('pointercancel', finish);
  root.addEventListener('click', event => {
    if (performance.now() - lastDragEnd > 250) return;
    event.preventDefault();
    event.stopImmediatePropagation();
  }, true);
}
