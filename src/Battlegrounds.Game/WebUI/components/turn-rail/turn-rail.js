import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createButton } from '../button/button.js';

const templateUrl = new URL('./turn-rail.html', import.meta.url);
useStyle(new URL('../panel/panel.css', import.meta.url));
useStyle(new URL('./turn-rail.css', import.meta.url));

export async function createTurnRail({
  label = 'Ready',
  blocked = false,
  canAct = true,
  currentPreparationPlayerId = null
} = {}) {
  const element = await cloneTemplate(templateUrl);
  appendChildren(element.querySelector('[data-slot="ready"]'), [
    await createButton({
      label,
      action: 'end-preparation',
      disabled: blocked,
      variant: 'primary',
      themeRole: 'button.primary',
      className: 'turn-rail__ready-button'
    })
  ]);

  const waiting = element.querySelector('[data-field="waiting"]');
  waiting.hidden = canAct;
  if (!canAct) {
    waiting.textContent = currentPreparationPlayerId != null
      ? `P${currentPreparationPlayerId}`
      : '…';
  }

  return element;
}
