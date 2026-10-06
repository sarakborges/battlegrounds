import { useStyle } from '../core/template.js';
import { createActionCardPreview } from '../molecules/action-card-preview/action-card-preview.js';
import { createCharacterPortrait } from '../molecules/character-portrait/character-portrait.js';
import { createPowerTooltip } from '../molecules/power-tooltip/power-tooltip.js';
import { createUnitCardPreview } from '../molecules/unit-card-preview/unit-card-preview.js';
import { applyComponentStyles } from '../theme/theme.js';

useStyle(new URL('./hover-inspector.css', import.meta.url));

const SHOW_DELAY_MS = 90;
const HIDE_DELAY_MS = 70;
const EDGE_PADDING = 18;
const TARGET_GAP = 14;

function inspectionArt(element) {
  const image = element.querySelector?.('img:not([hidden])');
  return image?.currentSrc || image?.src || null;
}

function readInspection(element) {
  if (!element?.dataset?.inspectKind) return null;
  return {
    kind: element.dataset.inspectKind,
    placement: element.dataset.inspectPlacement ?? 'auto',
    name: element.dataset.inspectName ?? '',
    description: element.dataset.inspectDescription ?? '',
    art: inspectionArt(element),
    tier: element.dataset.inspectTier ?? null,
    attack: element.dataset.inspectAttack ?? null,
    health: element.dataset.inspectHealth ?? null,
    cost: element.dataset.inspectCost ?? null,
    frozen: element.dataset.inspectFrozen === 'true'
  };
}

async function createInspectionView(inspection) {
  switch (inspection.kind) {
    case 'unit': return createUnitCardPreview(inspection);
    case 'action': return createActionCardPreview(inspection);
    case 'leader':
      return createCharacterPortrait({
        name: inspection.name,
        art: inspection.art,
        artAlt: inspection.name,
        showName: true,
        themeRole: 'panel.leaderChoicePortrait',
        className: 'hover-leader-portrait'
      });
    case 'power': return createPowerTooltip(inspection);
    default: return null;
  }
}

function logicalRect(element, overlay) {
  const targetRect = element.getBoundingClientRect();
  const overlayRect = overlay.getBoundingClientRect();
  const scale = Number.parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--ui-scale')) || 1;
  return {
    left: (targetRect.left - overlayRect.left) / scale,
    right: (targetRect.right - overlayRect.left) / scale,
    top: (targetRect.top - overlayRect.top) / scale,
    bottom: (targetRect.bottom - overlayRect.top) / scale,
    centerX: (targetRect.left + targetRect.width / 2 - overlayRect.left) / scale,
    centerY: (targetRect.top + targetRect.height / 2 - overlayRect.top) / scale
  };
}

function clamp(value, minimum, maximum) {
  return Math.max(minimum, Math.min(value, maximum));
}

function sidePosition(targetRect, previewWidth, previewHeight, logicalWidth) {
  let left = targetRect.right + TARGET_GAP;
  if (left + previewWidth > logicalWidth - EDGE_PADDING) {
    left = targetRect.left - previewWidth - TARGET_GAP;
  }
  return { left, top: targetRect.centerY - previewHeight / 2 };
}

function directionalPosition(placement, targetRect, previewWidth, previewHeight) {
  switch (placement) {
    case 'right':
      return {
        left: targetRect.right + TARGET_GAP,
        top: targetRect.centerY - previewHeight / 2
      };
    case 'left':
      return {
        left: targetRect.left - previewWidth - TARGET_GAP,
        top: targetRect.centerY - previewHeight / 2
      };
    case 'top':
      return {
        left: targetRect.centerX - previewWidth / 2,
        top: targetRect.top - previewHeight - TARGET_GAP
      };
    case 'bottom':
      return {
        left: targetRect.centerX - previewWidth / 2,
        top: targetRect.bottom + TARGET_GAP
      };
    default:
      return null;
  }
}

function inspectionPosition(inspection, targetRect, previewWidth, previewHeight, logicalWidth, logicalHeight) {
  let position = directionalPosition(
    inspection.placement,
    targetRect,
    previewWidth,
    previewHeight
  );

  if (!position && inspection.kind === 'power') {
    position = directionalPosition('top', targetRect, previewWidth, previewHeight);
    if (position.top < EDGE_PADDING) {
      position = directionalPosition('bottom', targetRect, previewWidth, previewHeight);
    }
  }

  if (!position) {
    position = sidePosition(targetRect, previewWidth, previewHeight, logicalWidth);
  }

  if (inspection.placement === 'top' && position.top < EDGE_PADDING) {
    position = directionalPosition('bottom', targetRect, previewWidth, previewHeight);
  } else if (inspection.placement === 'bottom' && position.top + previewHeight > logicalHeight - EDGE_PADDING) {
    position = directionalPosition('top', targetRect, previewWidth, previewHeight);
  } else if (inspection.placement === 'right' && position.left + previewWidth > logicalWidth - EDGE_PADDING) {
    position = directionalPosition('left', targetRect, previewWidth, previewHeight);
  } else if (inspection.placement === 'left' && position.left < EDGE_PADDING) {
    position = directionalPosition('right', targetRect, previewWidth, previewHeight);
  }

  return {
    left: clamp(position.left, EDGE_PADDING, logicalWidth - previewWidth - EDGE_PADDING),
    top: clamp(position.top, EDGE_PADDING, logicalHeight - previewHeight - EDGE_PADDING)
  };
}

export function bindHoverInspector(root = document) {
  const overlay = document.querySelector('.ui-overlay-shell');
  if (!overlay) return null;

  const mount = document.createElement('div');
  mount.className = 'hover-inspector';
  mount.hidden = true;
  overlay.appendChild(mount);

  let activeTarget = null;
  let showTimer = null;
  let hideTimer = null;
  let renderVersion = 0;
  let theme = null;

  const clearTimers = () => {
    clearTimeout(showTimer);
    clearTimeout(hideTimer);
    showTimer = null;
    hideTimer = null;
  };

  const hide = () => {
    clearTimeout(showTimer);
    showTimer = null;
    hideTimer = setTimeout(() => {
      activeTarget = null;
      mount.classList.remove('is-visible');
      mount.hidden = true;
      mount.replaceChildren();
    }, HIDE_DELAY_MS);
  };

  const show = target => {
    const inspection = readInspection(target);
    if (!inspection) return;
    clearTimers();
    activeTarget = target;
    const version = ++renderVersion;

    showTimer = setTimeout(async () => {
      const preview = await createInspectionView(inspection);
      if (!preview) return;
      await applyComponentStyles(theme, preview);
      if (version !== renderVersion || activeTarget !== target) return;

      mount.replaceChildren(preview);
      mount.dataset.kind = inspection.kind;
      mount.hidden = false;

      const targetRect = logicalRect(target, overlay);
      const previewWidth = mount.offsetWidth;
      const previewHeight = mount.offsetHeight;
      const position = inspectionPosition(
        inspection,
        targetRect,
        previewWidth,
        previewHeight,
        overlay.clientWidth,
        overlay.clientHeight
      );

      mount.style.left = `${position.left}px`;
      mount.style.top = `${position.top}px`;
      requestAnimationFrame(() => mount.classList.add('is-visible'));
    }, SHOW_DELAY_MS);
  };

  root.addEventListener('mouseover', event => {
    const target = event.target.closest?.('[data-inspect-kind]');
    if (!target || target === activeTarget) return;
    show(target);
  });

  root.addEventListener('mouseout', event => {
    if (!activeTarget) return;
    const target = event.target.closest?.('[data-inspect-kind]');
    if (target !== activeTarget) return;
    if (event.relatedTarget && activeTarget.contains(event.relatedTarget)) return;
    hide();
  });

  root.addEventListener('focusin', event => {
    const target = event.target.closest?.('[data-inspect-kind]');
    if (target) show(target);
  });

  root.addEventListener('focusout', event => {
    if (!activeTarget) return;
    if (event.relatedTarget && activeTarget.contains(event.relatedTarget)) return;
    hide();
  });

  return {
    setTheme(nextTheme) {
      theme = nextTheme ?? null;
    }
  };
}
