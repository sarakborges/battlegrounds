import { cloneTemplate, useStyle } from '../../core/template.js';

const templateUrl = new URL('./drop-slot.html', import.meta.url);
useStyle(new URL('./drop-slot.css', import.meta.url));

export async function createDropSlot({ insertionIndex = 0 } = {}) {
  const element = await cloneTemplate(templateUrl);
  element.dataset.insertionIndex = String(insertionIndex);
  return element;
}
