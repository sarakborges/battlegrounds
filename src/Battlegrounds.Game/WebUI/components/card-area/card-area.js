import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createHorizontalStack } from '../../design-system/horizontal-stack/horizontal-stack.js';

const templateUrl = new URL('./card-area.html', import.meta.url);
useStyle(new URL('../../design-system/panel/panel.css', import.meta.url));
useStyle(new URL('./card-area.css', import.meta.url));

const roles = {
  offer: 'panel.tavern',
  field: 'panel.board',
  reserve: 'panel.reserve'
};

export async function createCardArea({
  variant = 'offer',
  children = [],
  dropKind = null,
  themeRole = null
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.dataset.variant = variant;
  element.dataset.themeRole = themeRole ?? roles[variant] ?? 'panel';
  if (dropKind) element.dataset.dropKind = dropKind;

  const cards = await createHorizontalStack({
    children,
    variant,
    className: 'card-area__cards'
  });
  appendChildren(element.querySelector('[data-slot="content"]'), [cards]);
  return element;
}
