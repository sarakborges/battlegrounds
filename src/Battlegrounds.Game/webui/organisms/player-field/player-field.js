import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createHorizontalStack } from '../../atoms/horizontal-stack/horizontal-stack.js';

const templateUrl = new URL('./player-field.html', import.meta.url);
useStyle(new URL('../../atoms/panel/panel.css', import.meta.url));
useStyle(new URL('./player-field.css', import.meta.url));

export async function createPlayerField({ tokens = [] } = {}) {
  const element = await cloneTemplate(templateUrl);
  appendChildren(element.querySelector('[data-slot="content"]'), [
    await createHorizontalStack({ children: tokens, className: 'player-field__tokens' })
  ]);
  return element;
}
