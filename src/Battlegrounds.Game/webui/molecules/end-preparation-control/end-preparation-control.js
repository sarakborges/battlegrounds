import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createButton } from '../../atoms/button/button.js';

const templateUrl = new URL('./end-preparation-control.html', import.meta.url);
useStyle(new URL('../../atoms/button/button.css', import.meta.url));
useStyle(new URL('./end-preparation-control.css', import.meta.url));

export async function createEndPreparationControl({
  label = 'Ready',
  blocked = false,
  canAct = true,
  currentParticipantId = null
} = {}) {
  const element = await cloneTemplate(templateUrl);
  appendChildren(element.querySelector('[data-slot="button"]'), [
    await createButton({
      label,
      action: 'end-preparation',
      disabled: blocked,
      themeRole: 'button.endPreparation',
      className: 'end-preparation-control__button'
    })
  ]);

  const waiting = element.querySelector('[data-field="waiting"]');
  waiting.hidden = canAct;
  if (!canAct) {
    waiting.textContent = currentParticipantId != null ? `P${currentParticipantId}` : '…';
  }
  return element;
}
