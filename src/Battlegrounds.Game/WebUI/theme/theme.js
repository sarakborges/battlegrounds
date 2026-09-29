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
  if (/^-?\d+(\.\d+)?(px|rem|em|%|vh|vw)$/.test(value)) return value;
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

export function applyComponentStyles(theme, root = document) {
  if (!theme) return;
  const elements = [...(root.matches?.('[data-component]') ? [root] : []), ...root.querySelectorAll('[data-component]')];
  for (const element of elements) {
    const style = roleStyle(theme, element);
    if (!style) continue;
    const textColor = resolveColor(theme, style.textColor);
    const backgroundColor = resolveColor(theme, style.backgroundColor);
    const borderColor = resolveColor(theme, style.borderColor);
    const fontSize = resolveMetric(theme.fontSizes, style.fontSize);
    const radius = resolveMetric(theme.radii, style.radius);
    const padX = resolveMetric(theme.spacing, style.padding?.horizontal);
    const padY = resolveMetric(theme.spacing, style.padding?.vertical);
    if (textColor) element.style.color = textColor;
    if (backgroundColor) element.style.backgroundColor = backgroundColor;
    if (borderColor) element.style.borderColor = borderColor;
    if (style.borderWidth != null) element.style.borderWidth = `${style.borderWidth}px`;
    if (fontSize) element.style.fontSize = fontSize;
    if (radius) element.style.borderRadius = radius;
    if (padX) { element.style.paddingLeft = padX; element.style.paddingRight = padX; }
    if (padY) { element.style.paddingTop = padY; element.style.paddingBottom = padY; }
    if (style.opacity != null) element.style.opacity = String(style.opacity);
  }
}
