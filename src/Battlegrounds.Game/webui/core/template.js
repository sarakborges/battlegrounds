const templateCache = new Map();
const styleCache = new Set();

async function getTemplate(url) {
  const key = String(url);
  if (!templateCache.has(key)) {
    templateCache.set(key, (async () => {
      const response = await fetch(url);
      if (!response.ok && response.status !== 0) {
        throw new Error(`Unable to load UI template ${key}: ${response.status}`);
      }

      const source = await response.text();
      const document = new DOMParser().parseFromString(source, 'text/html');
      const template = document.querySelector('template');
      if (!template) throw new Error(`UI template ${key} does not contain a <template>.`);
      return template;
    })());
  }

  return templateCache.get(key);
}

export async function cloneTemplate(url) {
  const template = await getTemplate(url);
  const element = template.content.firstElementChild?.cloneNode(true);
  if (!element) throw new Error(`UI template ${url} has no root element.`);
  return element;
}

export function useStyle(url) {
  const key = String(url);
  if (styleCache.has(key)) return;
  styleCache.add(key);

  const link = document.createElement('link');
  link.rel = 'stylesheet';
  link.href = key;
  link.dataset.styleSource = key;
  document.head.appendChild(link);
}

export function reloadStyles() {
  const stamp = String(Date.now());
  const links = [...document.querySelectorAll('link[rel="stylesheet"]')];

  for (const link of links) {
    const source = link.dataset.styleSource ?? link.getAttribute('href');
    if (!source) continue;

    const url = new URL(source, document.baseURI);
    url.searchParams.set('__hot', stamp);

    const replacement = document.createElement('link');
    replacement.rel = 'stylesheet';
    replacement.href = url.href;
    replacement.dataset.styleSource = source;
    replacement.addEventListener('load', () => link.remove(), { once: true });
    replacement.addEventListener('error', () => replacement.remove(), { once: true });
    link.after(replacement);
  }
}

export function appendChildren(target, children = []) {
  for (const child of children) {
    if (child) target.appendChild(child);
  }
}

export function addClasses(element, className = '') {
  for (const name of String(className).split(/\s+/).filter(Boolean)) {
    element.classList.add(name);
  }
}

export function applyAttributes(element, attributes = {}) {
  for (const [name, value] of Object.entries(attributes)) {
    if (value === undefined || value === null || value === false) continue;
    if (value === true) element.setAttribute(name, '');
    else element.setAttribute(name, String(value));
  }
}

export function themeRole(component, variant, explicitRole) {
  if (explicitRole) return explicitRole;
  if (!variant || variant === 'default') return component;
  return `${component}.${variant}`;
}
