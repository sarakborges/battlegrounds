import { loadAsset } from './assets.js';

function slug(value) {
  return String(value).replace(/([a-z0-9])([A-Z])/g, '$1-$2').replace(/[^a-zA-Z0-9]+/g, '-').toLowerCase();
}

export function resolveColor(theme, value) {
  if (!value) return null;
  if (value.startsWith('#') || value.startsWith('rgb') || value.startsWith('hsl') || value === 'transparent') return value;
  return theme?.colors?.[value] ?? null;
}

export function resolveMetric(collection, value) {
  if (value == null) return null;
  if (typeof value === 'number') return `${value}px`;
  if (/^-?\d+(\.\d+)?(px|rem|em|%)$/.test(value)) return value;
  const resolved = collection?.[value];
  return resolved == null ? null : `${resolved}px`;
}

export function applyTheme(theme, screenRole) {
  if (!theme) return;
  const root = document.documentElement;
  Object.entries(theme.colors ?? {}).forEach(([key, value]) => root.style.setProperty(`--theme-color-${slug(key)}`, value));
  Object.entries(theme.spacing ?? {}).forEach(([key, value]) => root.style.setProperty(`--theme-space-${slug(key)}`, `${value}px`));
  Object.entries(theme.radii ?? {}).forEach(([key, value]) => root.style.setProperty(`--theme-radius-${slug(key)}`, `${value}px`));
  Object.entries(theme.fontSizes ?? {}).forEach(([key, value]) => root.style.setProperty(`--theme-font-size-${slug(key)}`, `${value}px`));
  Object.entries(theme.metrics ?? {}).forEach(([key, value]) => root.style.setProperty(`--theme-metric-${slug(key)}`, String(value)));
  const screenColor = resolveColor(theme, theme.screens?.[screenRole]?.backgroundColor);
  if (screenColor) root.style.setProperty('--surface-0', screenColor);
}

function roleStyle(theme, element) {
  const base = element.dataset.component ? theme.components?.[element.dataset.component] : null;
  const specific = element.dataset.themeRole ? theme.components?.[element.dataset.themeRole] : null;
  return base || specific ? { ...(base ?? {}), ...(specific ?? {}) } : null;
}

function setThemeVariable(element, name, value) {
  if (value == null) element.style.removeProperty(name);
  else element.style.setProperty(name, String(value));
}

async function applyResolvedComponentStyle(element, style, theme) {
  const textColor = resolveColor(theme, style?.textColor);
  const backgroundColor = resolveColor(theme, style?.backgroundColor);
  const borderColor = resolveColor(theme, style?.borderColor);
  const fontSize = resolveMetric(theme.fontSizes, style?.fontSize);
  const radius = resolveMetric(theme.radii, style?.radius);
  const padX = resolveMetric(theme.spacing, style?.padding?.horizontal);
  const padY = resolveMetric(theme.spacing, style?.padding?.vertical);
  const backgroundAsset = style?.backgroundAsset ? await loadAsset(style.backgroundAsset) : null;

  setThemeVariable(element, '--theme-component-text-color', textColor);
  setThemeVariable(element, '--theme-component-background-color', backgroundColor);
  setThemeVariable(element, '--theme-component-background-image', backgroundAsset ? `url("${backgroundAsset}")` : null);
  setThemeVariable(element, '--theme-component-border-color', borderColor);
  setThemeVariable(
    element,
    '--theme-component-border-width',
    style?.borderWidth == null ? null : `${style.borderWidth}px`
  );
  setThemeVariable(element, '--theme-component-font-size', fontSize);
  setThemeVariable(element, '--theme-component-radius', radius);
  setThemeVariable(element, '--theme-component-padding-x', padX);
  setThemeVariable(element, '--theme-component-padding-y', padY);
  setThemeVariable(element, '--theme-component-opacity', style?.opacity);
  setThemeVariable(element, '--theme-component-slice-left', style?.slice?.left);
  setThemeVariable(element, '--theme-component-slice-top', style?.slice?.top);
  setThemeVariable(element, '--theme-component-slice-right', style?.slice?.right);
  setThemeVariable(element, '--theme-component-slice-bottom', style?.slice?.bottom);
}

export async function applyComponentStyles(theme, root = document) {
  if (!theme) return;
  const elements = [
    ...(root.matches?.('[data-component]') ? [root] : []),
    ...root.querySelectorAll('[data-component]')
  ];

  await Promise.all(elements.map(element =>
    applyResolvedComponentStyle(element, roleStyle(theme, element), theme)
  ));
}
