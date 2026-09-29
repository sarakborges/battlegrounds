import { useStyle } from '../core/template.js';
import { createCardPreview } from '../components/card-preview/card-preview.js';
import { applyComponentStyles } from '../theme/theme.js';

useStyle(new URL('./hover-inspector.css', import.meta.url));

const SHOW_DELAY_MS = 110;
const HIDE_DELAY_MS = 70;
const EDGE_PADDING = 18;

function inspectionArt(element) {
  const image = element.querySelector?.('img:not([hidden])');
  return image?.currentSrc || image?.src || null;
}

function readInspection(element) {
  if (!element?.dataset?.inspectKind) return null;
  return {
    kind: element.dataset.inspectKind,
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

function logicalRect(element, overlay) {
  const targetRect = element.getBoundingClientRect();
  const overlayRect = overlay.getBoundingClientRect();
  const scale = Number.parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--ui-scale')) || 1;
  return {
    left: (targetRect.left - overlayRect.left) / scale,
    right: (targetRect.right - overlayRect.left) / scale,
    top: (targetRect.top - overlayRect.top) / scale,
    bottom: (targetRect.bottom - overlayRect.top) / scale
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
      const preview = await createCardPreview(inspection);
      await applyComponentStyles(theme, preview);
      if (version !== renderVersion || activeTarget !== target) return;

      mount.replaceChildren(preview);
      mount.hidden = false;

      const targetRect = logicalRect(target, overlay);
      const previewWidth = mount.offsetWidth;
      const previewHeight = mount.offsetHeight;
      const logicalWidth = overlay.clientWidth;
      const logicalHeight = overlay.clientHeight;

      let left = targetRect.right + 14;
      if (left + previewWidth > logicalWidth - EDGE_PADDING) {
        left = targetRect.left - previewWidth - 14;
      }
      left = Math.max(EDGE_PADDING, Math.min(left, logicalWidth - previewWidth - EDGE_PADDING));

      let top = targetRect.top - 18;
      top = Math.max(EDGE_PADDING, Math.min(top, logicalHeight - previewHeight - EDGE_PADDING));

      mount.style.left = `${left}px`;
      mount.style.top = `${top}px`;
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
