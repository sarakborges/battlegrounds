import { appendChildren, applyAttributes, cloneTemplate, useStyle } from '../../core/template.js';
import { createButton } from '../../design-system/button/button.js';
import { inspectionAttributes } from '../../core/inspection.js';

const templateUrl = new URL('./power-button.html', import.meta.url);
useStyle(new URL('../../design-system/button/button.css', import.meta.url));
useStyle(new URL('./power-button.css', import.meta.url));

export async function createPowerButton({ power = null, label = 'Use power', blocked = false } = {}) {
  const element = await cloneTemplate(templateUrl);
  if (power?.id) {
    applyAttributes(element, {
      ...inspectionAttributes({
        kind: 'power',
        id: power.id,
        name: power.name ?? power.id,
        description: power.description,
        cost: power.cost
      }),
      'data-inspect-placement': 'top'
    });
  }

  const control = await createButton({
    label: '',
    action: 'use-power',
    disabled: blocked || !power?.id || power.activatable === false,
    themeRole: 'button.power',
    className: 'power-button__control',
    attributes: { title: label, 'aria-label': label }
  });
  appendChildren(element.querySelector('[data-slot="button"]'), [control]);

  const cost = element.querySelector('[data-field="cost"]');
  cost.textContent = power?.cost ?? '';
  cost.hidden = power?.cost == null;
  return element;
}
