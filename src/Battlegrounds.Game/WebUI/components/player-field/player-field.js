import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createHorizontalStack } from '../../design-system/horizontal-stack/horizontal-stack.js';

const templateUrl = new URL('./player-field.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./player-field.css', import.meta.url));

export async function createPlayerField({ tokens = [] } = {}) {
  const element = await cloneTemplate(templateUrl);
  appendChildren(element.querySelector('[data-slot="content"]'), [
    await createHorizontalStack({ children: tokens, className: 'player-field__tokens' })
  ]);
  return element;
}
