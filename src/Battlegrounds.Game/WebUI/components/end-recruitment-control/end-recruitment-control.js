import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createButton } from '../../design-system/button/button.js';

const templateUrl = new URL('./end-recruitment-control.html', import.meta.url);
useStyle(new URL('../../design-system/button/button.css', import.meta.url));
useStyle(new URL('./end-recruitment-control.css', import.meta.url));

export async function createEndRecruitmentControl({
  label = 'Ready',
  blocked = false,
  canAct = true,
  currentPreparationPlayerId = null
} = {}) {
  const element = await cloneTemplate(templateUrl);
  appendChildren(element.querySelector('[data-slot="button"]'), [
    await createButton({
      label,
      action: 'end-preparation',
      disabled: blocked,
      variant: 'primary',
      themeRole: 'button.primary',
      className: 'end-recruitment-control__button'
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
