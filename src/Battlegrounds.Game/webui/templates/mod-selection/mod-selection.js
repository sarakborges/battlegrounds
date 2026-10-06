import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./mod-selection.html', import.meta.url);
useStyle(new URL('./mod-selection.css', import.meta.url));

export async function createModSelectionTemplate() {
  return cloneTemplate(templateUrl);
}
