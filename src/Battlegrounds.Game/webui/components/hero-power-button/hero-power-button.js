import { appendChildren, applyAttributes, cloneTemplate, useStyle } from '../../core/template.js';
import { createButton } from '../../design-system/button/button.js';
import { inspectionAttributes } from '../../core/inspection.js';

const templateUrl = new URL('./hero-power-button.html', import.meta.url);
useStyle(new URL('../../design-system/button/button.css', import.meta.url));
useStyle(new URL('./hero-power-button.css', import.meta.url));

export async function createHeroPowerButton({ heroPower = null, label = 'Use hero power', blocked = false } = {}) {
  const element = await cloneTemplate(templateUrl);
  if (heroPower?.id) {
    applyAttributes(element, {
      ...inspectionAttributes({
        kind: 'power',
        id: heroPower.id,
        name: heroPower.name ?? heroPower.id,
        description: heroPower.description,
        cost: heroPower.cost
      }),
      'data-inspect-placement': 'top'
    });
  }

  const control = await createButton({
    label: '✦',
    action: 'use-power',
    disabled: blocked || !heroPower?.id || heroPower.activatable === false,
    themeRole: 'button.power',
    className: 'hero-power-button__control',
    attributes: { title: label }
  });
  appendChildren(element.querySelector('[data-slot="button"]'), [control]);

  const cost = element.querySelector('[data-field="cost"]');
  cost.textContent = heroPower?.cost ?? '';
  cost.hidden = heroPower?.cost == null;
  return element;
}
