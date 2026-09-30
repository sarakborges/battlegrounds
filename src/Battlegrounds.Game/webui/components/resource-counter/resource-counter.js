import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./resource-counter.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./resource-counter.css', import.meta.url));

export async function createResourceCounter({ value = 0, maximum = value, label = 'Resource' } = {}) {
  const element = await cloneTemplate(templateUrl);
  const current = Math.max(0, Math.trunc(Number(value) || 0));
  const requestedMaximum = Math.max(0, Math.trunc(Number(maximum) || 0));
  const resolvedMaximum = Math.max(current, requestedMaximum);

  element.querySelector('[data-field="value"]').textContent = `${current}/${resolvedMaximum}`;

  const pips = document.createElement('span');
  pips.className = 'resource-counter__pips';
  pips.setAttribute('aria-hidden', 'true');
  for (let index = 0; index < resolvedMaximum; index += 1) {
    const pip = document.createElement('span');
    pip.className = 'resource-counter__pip';
    pip.dataset.component = 'icon';
    pip.dataset.themeRole = 'icon.resourcePip';
    if (index < current) pip.dataset.active = 'true';
    pips.append(pip);
  }
  element.append(pips);

  element.setAttribute('aria-label', `${label}: ${current}/${resolvedMaximum}`);
  element.title = `${label}: ${current}/${resolvedMaximum}`;
  return element;
}
