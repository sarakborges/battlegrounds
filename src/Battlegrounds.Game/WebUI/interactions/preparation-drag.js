import { useStyle } from '../core/template.js';

useStyle(new URL('./preparation-drag.css', import.meta.url));

const DRAG_THRESHOLD = 5;

function readDragSource(element) {
  const kind = element.dataset.dragKind;
  if (!kind) return null;

  if (kind === 'player-field-unit') {
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

function isDragEnabled(element) {
  return element.dataset.disabled !== 'true' &&
    element.hasAttribute('data-drag-enabled') &&
    element.dataset.dragEnabled !== 'false';
}

function isCompatibleDropTarget(target, dragSource) {
  const dropKind = target?.dataset.dropKind;
  if (!dropKind || !dragSource) return false;
  if (dropKind === 'player-hero') return dragSource.kind === 'tavern-offer-token';
  if (dropKind === 'tavern-shopkeeper') return dragSource.kind === 'player-field-unit';
  if (dropKind === 'player-field') {
    return dragSource.kind === 'player-field-unit' || dragSource.kind === 'player-reserve-unit';
  }
  return false;
}

function isValidDrop(target, dragSource) {
  if (!isCompatibleDropTarget(target, dragSource)) return false;
  if (target.dataset.dropKind === 'player-hero') return dragSource.valid;
  if (target.dataset.dropKind === 'player-field' && dragSource.kind === 'player-reserve-unit') {
    return dragSource.valid;
  }
  return true;
}

function findPlayerFieldInsertionIndex(playerField, clientX) {
  const unitTokens = [...playerField.querySelectorAll('[data-drag-kind="player-field-unit"]')];
  for (let index = 0; index < unitTokens.length; index += 1) {
    const rect = unitTokens[index].getBoundingClientRect();
    if (clientX < rect.left + rect.width / 2) return index;
  }
  return unitTokens.length;
}

function clearDropFeedback(root) {
  for (const target of root.querySelectorAll('.is-drop-hover, .is-drop-invalid')) {
    target.classList.remove('is-drop-hover', 'is-drop-invalid');
  }
}

function dispatchPreparationAction(root, action, payload) {
  root.dispatchEvent(new CustomEvent('battlegrounds-action', {
    bubbles: true,
    detail: { action, payload }
  }));
}

function createDragGhost(sourceElement) {
  const ghost = sourceElement.cloneNode(true);
  ghost.classList.add('drag-ghost');
  ghost.removeAttribute('data-action');
  ghost.removeAttribute('data-inspect-kind');
  document.body.appendChild(ghost);
  return ghost;
}

function positionDragGhost(ghost, event) {
  ghost.style.left = `${event.clientX}px`;
  ghost.style.top = `${event.clientY}px`;
}

export function bindPreparationDrag(root) {
  let pendingDrag = null;
  let activeDrag = null;
  let capturedPointer = null;

  const releasePointer = () => {
    if (!capturedPointer) return;
    const { element, pointerId } = capturedPointer;
    if (element.hasPointerCapture?.(pointerId)) element.releasePointerCapture(pointerId);
    capturedPointer = null;
  };

  const finishDrag = event => {
    if (!activeDrag) {
      pendingDrag = null;
      releasePointer();
      return;
    }

    const hitElement = document.elementFromPoint(event.clientX, event.clientY);
    const dropTarget = hitElement?.closest?.('[data-drop-kind]');
    if (dropTarget && isValidDrop(dropTarget, activeDrag)) {
      switch (dropTarget.dataset.dropKind) {
        case 'player-hero':
          dispatchPreparationAction(root, 'acquire', { slot: activeDrag.slot });
          break;
        case 'tavern-shopkeeper':
          dispatchPreparationAction(root, 'release', { slot: activeDrag.index });
          break;
        case 'player-field': {
          const insertionIndex = findPlayerFieldInsertionIndex(dropTarget, event.clientX);
          if (activeDrag.kind === 'player-field-unit') {
            dispatchPreparationAction(root, 'reorder-field', {
              fromIndex: activeDrag.index,
              insertionIndex
            });
          } else if (activeDrag.kind === 'player-reserve-unit') {
            dispatchPreparationAction(root, 'deploy-at', {
              slot: activeDrag.slot,
              insertionIndex
            });
          }
          break;
        }
      }
    }

    activeDrag.element.classList.remove('is-dragging');
    activeDrag.ghost.remove();
    root.removeAttribute('data-drag-kind');
    clearDropFeedback(root);
    activeDrag = null;
    pendingDrag = null;
    releasePointer();
  };

  root.addEventListener('pointerdown', event => {
    if (event.button !== 0) return;
    const sourceElement = event.target.closest('[data-drag-kind]');
    if (!sourceElement || !isDragEnabled(sourceElement)) return;

    event.preventDefault();
    const dragSource = readDragSource(sourceElement);
    if (!dragSource) return;
    pendingDrag = {
      ...dragSource,
      startX: event.clientX,
      startY: event.clientY
    };

    if (sourceElement.setPointerCapture) {
      sourceElement.setPointerCapture(event.pointerId);
      capturedPointer = { element: sourceElement, pointerId: event.pointerId };
    }
  });

  root.addEventListener('pointermove', event => {
    if (pendingDrag && !activeDrag) {
      const distance = Math.hypot(
        event.clientX - pendingDrag.startX,
        event.clientY - pendingDrag.startY
      );
      if (distance < DRAG_THRESHOLD) return;

      activeDrag = {
        ...pendingDrag,
        ghost: createDragGhost(pendingDrag.element)
      };
      pendingDrag = null;
      activeDrag.element.classList.add('is-dragging');
      root.dataset.dragKind = activeDrag.kind;
    }

    if (!activeDrag) return;
    event.preventDefault();
    positionDragGhost(activeDrag.ghost, event);
    clearDropFeedback(root);

    const hitElement = document.elementFromPoint(event.clientX, event.clientY);
    const dropTarget = hitElement?.closest?.('[data-drop-kind]');
    if (!dropTarget || !isCompatibleDropTarget(dropTarget, activeDrag)) return;
    dropTarget.classList.add(isValidDrop(dropTarget, activeDrag) ? 'is-drop-hover' : 'is-drop-invalid');
  });

  root.addEventListener('pointerup', finishDrag);
  root.addEventListener('pointercancel', finishDrag);
  root.addEventListener('click', event => {
    const sourceElement = event.target.closest('[data-drag-kind]');
    if (!sourceElement || !isDragEnabled(sourceElement)) return;
    event.preventDefault();
    event.stopImmediatePropagation();
  }, true);
}
