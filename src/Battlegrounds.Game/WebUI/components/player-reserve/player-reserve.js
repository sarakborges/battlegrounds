import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createHorizontalStack } from '../../design-system/horizontal-stack/horizontal-stack.js';

const templateUrl = new URL('./player-reserve.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./player-reserve.css', import.meta.url));

export async function createPlayerReserve({ cards = [] } = {}) {
  const element = await cloneTemplate(templateUrl);
  appendChildren(element.querySelector('[data-slot="content"]'), [
    await createHorizontalStack({ children: cards, className: 'player-reserve__cards' })
  ]);
  return element;
}
