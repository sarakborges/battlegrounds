import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createHorizontalStack } from '../../atoms/horizontal-stack/horizontal-stack.js';

const templateUrl = new URL('./player-reserve.html', import.meta.url);
useStyle(new URL('../../atoms/panel/panel.css', import.meta.url));
useStyle(new URL('./player-reserve.css', import.meta.url));

export async function createPlayerReserve({ tokens = [] } = {}) {
  const element = await cloneTemplate(templateUrl);
  appendChildren(element.querySelector('[data-slot="content"]'), [
    await createHorizontalStack({ children: tokens, className: 'player-reserve__tokens' })
  ]);
  return element;
}
