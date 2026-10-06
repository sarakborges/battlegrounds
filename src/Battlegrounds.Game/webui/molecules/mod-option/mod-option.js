import { appendChildren, cloneTemplate, useStyle } from '../../core/template.js';
import { createButton } from '../../atoms/button/button.js';

const templateUrl = new URL('./mod-option.html', import.meta.url);
useStyle(new URL('../../atoms/panel/panel.css', import.meta.url));
useStyle(new URL('./mod-option.css', import.meta.url));

function formatDiagnostics(issues = []) {
  return issues
    .map(issue => `[${issue.code}] ${issue.file} ${issue.path}\n${issue.message}`)
    .join('\n\n');
}

export async function createModOption({
  directoryName,
  id,
  name,
  schemaVersion = null,
  isValid = false,
  errorCount = 0,
  issues = []
} = {}) {
  const element = await cloneTemplate(templateUrl);
  element.querySelector('[data-field="name"]').textContent = name ?? directoryName ?? '';
  element.querySelector('[data-field="id"]').textContent = id ?? directoryName ?? '';
  element.querySelector('[data-field="schema"]').textContent = schemaVersion == null
    ? 'Schema unknown'
    : `Schema ${schemaVersion}`;

  appendChildren(element.querySelector('[data-slot="action"]'), [
    await createButton({
      label: isValid ? 'Play' : `${errorCount} error${errorCount === 1 ? '' : 's'}`,
      action: isValid ? 'select-mod' : null,
      variant: isValid ? 'primary' : 'default',
      disabled: !isValid,
      attributes: { 'data-mod-directory': directoryName ?? '' }
    })
  ]);

  const diagnostics = element.querySelector('[data-field="diagnostics"]');
  diagnostics.textContent = formatDiagnostics(issues);
  diagnostics.hidden = isValid || !issues.length;
  return element;
}
