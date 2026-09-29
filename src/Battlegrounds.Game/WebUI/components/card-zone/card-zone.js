import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createRow } from '../row/row.js';

const templateUrl = new URL('./card-zone.html', import.meta.url);
useStyle(new URL('../panel/panel.css', import.meta.url));
useStyle(new URL('./card-zone.css', import.meta.url));

const roles = {
  offer: 'panel.tavern',
  field: 'panel.board',
  reserve: 'panel.reserve'
};

export async function createCardZone({
  variant = 'offer',
  children = [],
  dropKind = null,
  themeRole = null
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.dataset.variant = variant;
  element.dataset.themeRole = themeRole ?? roles[variant] ?? 'panel';
  if (dropKind) element.dataset.dropKind = dropKind;

  const row = await createRow({
    children,
    variant,
    className: 'card-zone__row'
  });
  appendChildren(element.querySelector('[data-slot="content"]'), [row]);
  return element;
}
