import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./preparation.html', import.meta.url);
useStyle(new URL('./preparation.css', import.meta.url));

export async function createPreparationTemplate() {
  return cloneTemplate(templateUrl);
}
