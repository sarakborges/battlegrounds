import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./leader-selection.html', import.meta.url);
useStyle(new URL('./leader-selection.css', import.meta.url));

export async function createLeaderSelectionTemplate() {
  return cloneTemplate(templateUrl);
}
