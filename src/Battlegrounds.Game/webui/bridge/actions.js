function payloadFor(element, action) {
  switch (action) {
    case 'select-mod': return { directoryName: element.dataset.modDirectory };
    case 'select-leader': return { leaderId: element.dataset.leaderId };
    case 'acquire':
    case 'deploy':
    case 'play-action':
    case 'release': return { slot: Number(element.dataset.slot) };
    case 'resolve-choice': return { optionIndex: Number(element.dataset.optionIndex) };
    case 'select-target':
    case 'toggle-combine-unit': return { unitInstanceId: Number(element.dataset.unitInstanceId) };
    case 'select-combine-recipe': return { combineId: element.dataset.combineId };
    default: return {};
  }
}

function isDisabled(element) {
  return element.disabled === true ||
    element.dataset.disabled === 'true' ||
    element.getAttribute('aria-disabled') === 'true';
}

export function bindActions(root, send) {
  root.addEventListener('click', event => {
    const element = event.target.closest('[data-action]');
    if (!element || isDisabled(element)) return;
    const action = element.dataset.action;
    send(action, payloadFor(element, action));
  });

  root.addEventListener('battlegrounds-action', event => {
    const action = event.detail?.action;
    if (typeof action !== 'string' || !action) return;
    send(action, event.detail?.payload ?? {});
  });
}
