import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./loading.html', import.meta.url);
useStyle(new URL('../../atoms/panel/panel.css', import.meta.url));
useStyle(new URL('./loading.css', import.meta.url));

export async function createLoadingTemplate() {
  return cloneTemplate(templateUrl);
}
