import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createHorizontalStack } from '../../design-system/horizontal-stack/horizontal-stack.js';

const templateUrl = new URL('./tavern-offer.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./tavern-offer.css', import.meta.url));

export async function createTavernOffer({ cards = [] } = {}) {
  const element = await cloneTemplate(templateUrl);
  appendChildren(element.querySelector('[data-slot="content"]'), [
    await createHorizontalStack({ children: cards, className: 'tavern-offer__cards' })
  ]);
  return element;
}
