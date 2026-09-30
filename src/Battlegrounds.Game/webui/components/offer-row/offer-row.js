import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./offer-row.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./offer-row.css', import.meta.url));

export async function createOfferRow({ tokens = [] } = {}) {
  const element = await cloneTemplate(templateUrl);
  appendChildren(element.querySelector('[data-slot="tokens"]'), tokens);
  return element;
}
