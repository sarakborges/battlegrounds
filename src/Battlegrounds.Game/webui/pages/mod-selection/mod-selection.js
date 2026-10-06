import { appendChildren } from '../../core/template.js';
import { createButton } from '../../atoms/button/button.js';
import { createModOption } from '../../molecules/mod-option/mod-option.js';
import { createModSelectionTemplate } from '../../templates/mod-selection/mod-selection.js';

export async function createModSelectionPage(state) {
  const element = await createModSelectionTemplate();
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
