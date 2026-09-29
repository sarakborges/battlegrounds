import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createButton } from '../../design-system/button/button.js';
import { createModOption } from '../../components/mod-option/mod-option.js';

const templateUrl = new URL('./mod-selection.html', import.meta.url);
useStyle(new URL('./mod-selection.css', import.meta.url));

export async function createModSelectionScreen(state) {
  const element = await cloneTemplate(templateUrl);
  const mods = state.mods ?? [];
  const validCount = mods.filter(mod => mod.isValid).length;

  element.querySelector('[data-field="status"]').textContent = mods.length
    ? `${mods.length} mod${mods.length === 1 ? '' : 's'} found · ${validCount} ready`
    : 'No mod packages found.';

  const discoveryError = element.querySelector('[data-field="discovery-error"]');
  discoveryError.textContent = state.discoveryError ?? '';
  discoveryError.hidden = !state.discoveryError;

  appendChildren(element.querySelector('[data-slot="actions"]'), [
    await createButton({ label: 'Refresh', action: 'refresh-mods' })
  ]);

  const options = [];
  for (const mod of mods) options.push(await createModOption(mod));
  appendChildren(element.querySelector('[data-slot="mods"]'), options);
  return element;
}
