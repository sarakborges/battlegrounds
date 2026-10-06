export const fixtureTheme = {
  colors: {
    surface0: '#101218',
    surface1: '#171a22',
    surface2: '#202532',
    surface3: '#2b3242',
    text: '#f5f7fb',
    muted: '#a8b0c0',
    accent: '#e1b85b',
    danger: '#ef6f6c',
    line: '#343a48',
    attack: '#8f5c27',
    health: '#8f3030',
    armor: '#5d718d',
    tier1: '#7f8c8d',
    tier2: '#4c9a5f',
    tier3: '#4a7fb3',
    tier4: '#8a5db3',
    tier5: '#b77931',
    tier6: '#b34f4f'
  },
  spacing: {
    xs: 4,
    sm: 8,
    md: 12,
    lg: 18
  },
  radii: {
    sm: 8,
    md: 12,
    pill: 999
  },
  fontSizes: {
    sm: 12,
    md: 14,
    lg: 18
  },
  metrics: {},
  components: {
    button: {
      textColor: 'text',
      backgroundColor: 'surface2',
      borderColor: 'line',
      borderWidth: 1,
      radius: 'md',
      padding: { horizontal: 'md', vertical: 'sm' },
      states: {
        hover: { backgroundColor: 'surface3', borderColor: 'accent' },
        focus: { borderColor: 'accent' },
        disabled: { textColor: 'muted', opacity: 0.45 }
      }
    },
    'button.primary': {
      textColor: '#111111',
      backgroundColor: 'accent',
      borderColor: 'accent'
    },
    'button.power': {
      backgroundColor: 'surface2',
      borderColor: 'accent',
      radius: 'pill'
    },
    panel: {
      textColor: 'text',
      backgroundColor: 'surface1',
      borderColor: 'line',
      borderWidth: 1,
      radius: 'md'
    },
    'panel.attackBadge': { backgroundColor: 'attack', borderColor: '#d99b53' },
    'panel.healthBadge': { backgroundColor: 'health', borderColor: '#e46a6a' },
    'panel.armorBadge': { backgroundColor: 'armor', borderColor: '#9fb3cd' },
    icon: {
      width: 24,
      height: 24,
      radius: 'pill',
      borderWidth: 1,
      borderColor: '#ffffff55'
    },
    'icon.tier.1': { backgroundColor: 'tier1' },
    'icon.tier.2': { backgroundColor: 'tier2' },
    'icon.tier.3': { backgroundColor: 'tier3' },
    'icon.tier.4': { backgroundColor: 'tier4' },
    'icon.tier.5': { backgroundColor: 'tier5' },
    'icon.tier.6': { backgroundColor: 'tier6' }
  },
  screens: {
    preparation: { backgroundColor: 'surface0' }
  }
};
