import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./combat.html', import.meta.url);
useStyle(new URL('./combat.css', import.meta.url));

export async function createCombatTemplate() {
  return cloneTemplate(templateUrl);
}
