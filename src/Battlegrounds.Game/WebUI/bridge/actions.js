function payloadFor(element, action) {
  switch (action) {
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

export function bindActions(root, send) {
  root.addEventListener('click', event => {
    const element = event.target.closest('[data-action]');
    if (!element || element.disabled) return;
    const action = element.dataset.action;
    send(action, payloadFor(element, action));
  });
}
