import { loadAsset } from './assets.js';

let fontGeneration = 0;
let installedFontKeys = new Set();
let fontStyleElement = null;

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

function pixelValue(value) {
  if (value == null) return null;
  const number = Number(value);
  return Number.isFinite(number) && number > 0 ? `${number}px` : null;
}

function resolveFontFamily(theme, value) {
  if (!value) return null;
  if (theme?.fonts?.[value]) return `var(--theme-font-${slug(value)})`;
  return value;
}

function ensureFontStyleElement() {
  if (fontStyleElement?.isConnected) return fontStyleElement;
  fontStyleElement = document.createElement('style');
  fontStyleElement.dataset.themeFonts = 'true';
  document.head.appendChild(fontStyleElement);
  return fontStyleElement;
}

async function applyThemeFonts(theme) {
  const root = document.documentElement;
  const generation = ++fontGeneration;
  const entries = Object.entries(theme?.fonts ?? {});
  const loaded = await Promise.all(entries.map(async ([key, font]) => {
    const relativePath = font?.relativePath ?? font?.asset ?? null;
    return [key, relativePath ? await loadAsset(relativePath) : null];
  }));

  if (generation !== fontGeneration) return;

  for (const key of installedFontKeys) {
    root.style.removeProperty(`--theme-font-${slug(key)}`);
  }

  installedFontKeys = new Set();
  const rules = [];
  for (const [key, dataUrl] of loaded) {
    if (!dataUrl) continue;
    const family = `BattlegroundsMod-${slug(key)}`;
    installedFontKeys.add(key);
    root.style.setProperty(`--theme-font-${slug(key)}`, `"${family}"`);
    rules.push(`@font-face{font-family:"${family}";src:url("${dataUrl}");font-display:swap;}`);
  }
  ensureFontStyleElement().textContent = rules.join('\n');
}

export async function applyTheme(theme, screenRole) {
  if (!theme) return;
  const root = document.documentElement;
  Object.entries(theme.colors ?? {}).forEach(([key, value]) => root.style.setProperty(`--theme-color-${slug(key)}`, value));
  Object.entries(theme.spacing ?? {}).forEach(([key, value]) => root.style.setProperty(`--theme-space-${slug(key)}`, `${value}px`));
  Object.entries(theme.radii ?? {}).forEach(([key, value]) => root.style.setProperty(`--theme-radius-${slug(key)}`, `${value}px`));
  Object.entries(theme.fontSizes ?? {}).forEach(([key, value]) => root.style.setProperty(`--theme-font-size-${slug(key)}`, `${value}px`));
  Object.entries(theme.metrics ?? {}).forEach(([key, value]) => root.style.setProperty(`--theme-metric-${slug(key)}`, String(value)));

  await applyThemeFonts(theme);

  const screen = theme.screens?.[screenRole] ?? null;
  const screenColor = resolveColor(theme, screen?.backgroundColor);
  if (screenColor) root.style.setProperty('--surface-0', screenColor);

  const screenAsset = screen?.backgroundAsset ? await loadAsset(screen.backgroundAsset) : null;
  if (screenAsset) root.style.setProperty('--theme-screen-background-image', `url("${screenAsset}")`);
  else root.style.removeProperty('--theme-screen-background-image');
}

function mergeRoleStyles(base, specific) {
  if (!base && !specific) return null;
  const merged = { ...(base ?? {}), ...(specific ?? {}) };
  const baseStates = base?.states ?? {};
  const specificStates = specific?.states ?? {};
  const stateNames = new Set([...Object.keys(baseStates), ...Object.keys(specificStates)]);
  merged.states = {};
  for (const stateName of stateNames) {
    merged.states[stateName] = {
      ...(baseStates[stateName] ?? {}),
      ...(specificStates[stateName] ?? {})
    };
  }
  return merged;
}

function roleStyle(theme, element) {
  const base = element.dataset.component ? theme.components?.[element.dataset.component] : null;
  const specific = element.dataset.themeRole ? theme.components?.[element.dataset.themeRole] : null;
  return mergeRoleStyles(base, specific);
}

function setThemeVariable(element, name, value) {
  // Base component variables are isolated from ancestor components.
  element.style.setProperty(name, value == null ? 'initial' : String(value));
}

function setThemeStateVariable(element, name, value) {
  // State variables are optional: when a state does not override a property,
  // the component CSS must fall back to the base variable.
  if (value == null) element.style.removeProperty(name);
  else element.style.setProperty(name, String(value));
}

async function applyStyleVariables(element, style, theme, stateName = null) {
  const suffix = stateName ? `-${stateName}` : '';
  const variable = property => `--theme-component${suffix}-${property}`;
  const setVariable = stateName ? setThemeStateVariable : setThemeVariable;
  const textColor = resolveColor(theme, style?.textColor);
  const backgroundColor = resolveColor(theme, style?.backgroundColor);
  const borderColor = resolveColor(theme, style?.borderColor);
  const fontSize = resolveMetric(theme.fontSizes, style?.fontSize);
  const radius = resolveMetric(theme.radii, style?.radius);
  const padX = resolveMetric(theme.spacing, style?.padding?.horizontal);
  const padY = resolveMetric(theme.spacing, style?.padding?.vertical);
  const fontFamily = resolveFontFamily(theme, style?.font);
  const [backgroundAsset, iconAsset] = await Promise.all([
    style?.backgroundAsset ? loadAsset(style.backgroundAsset) : null,
    style?.iconAsset ? loadAsset(style.iconAsset) : null
  ]);

  setVariable(element, variable('text-color'), textColor);
  setVariable(element, variable('background-color'), backgroundColor);
  setVariable(element, variable('background-image'), backgroundAsset ? `url("${backgroundAsset}")` : null);
  setVariable(element, variable('icon-image'), iconAsset ? `url("${iconAsset}")` : null);
  setVariable(element, variable('width'), pixelValue(style?.width));
  setVariable(element, variable('height'), pixelValue(style?.height));
  setVariable(element, variable('icon-width'), pixelValue(style?.iconWidth));
  setVariable(element, variable('icon-height'), pixelValue(style?.iconHeight));
  setVariable(element, variable('border-color'), borderColor);
  setVariable(element, variable('border-width'), style?.borderWidth == null ? null : `${style.borderWidth}px`);
  setVariable(element, variable('font-family'), fontFamily);
  setVariable(element, variable('font-size'), fontSize);
  setVariable(element, variable('radius'), radius);
  setVariable(element, variable('padding-x'), padX);
  setVariable(element, variable('padding-y'), padY);
  setVariable(element, variable('opacity'), style?.opacity);

  const slice = style?.slice;
  setVariable(element, variable('slice-left'), slice?.left ?? null);
  setVariable(element, variable('slice-top'), slice?.top ?? null);
  setVariable(element, variable('slice-right'), slice?.right ?? null);
  setVariable(element, variable('slice-bottom'), slice?.bottom ?? null);
  setVariable(element, variable('slice-left-width'), slice == null ? null : `${slice.left}px`);
  setVariable(element, variable('slice-top-width'), slice == null ? null : `${slice.top}px`);
  setVariable(element, variable('slice-right-width'), slice == null ? null : `${slice.right}px`);
  setVariable(element, variable('slice-bottom-width'), slice == null ? null : `${slice.bottom}px`);

  return {
    hasBackgroundAsset: !!backgroundAsset,
    hasIconAsset: !!iconAsset,
    hasSlice: !!backgroundAsset && !!slice
  };
}

async function applyResolvedComponentStyle(element, style, theme) {
  const baseResult = await applyStyleVariables(element, style, theme);
  element.dataset.themeBackgroundAsset = baseResult.hasBackgroundAsset ? 'true' : 'false';
  element.dataset.themeIconAsset = baseResult.hasIconAsset ? 'true' : 'false';
  element.dataset.themeSlicedBackground = baseResult.hasSlice ? 'true' : 'false';

  const width = pixelValue(style?.width);
  const height = pixelValue(style?.height);
  if (width) element.style.width = width;
  else element.style.removeProperty('width');
  if (height) element.style.height = height;
  else element.style.removeProperty('height');

  const stateNames = new Set([
    'hover',
    'pressed',
    'focus',
    'disabled',
    ...Object.keys(style?.states ?? {})
  ]);
  await Promise.all([...stateNames].map(stateName =>
    applyStyleVariables(element, style?.states?.[stateName] ?? null, theme, stateName)
  ));
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
