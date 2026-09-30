export function inspectionAttributes({
  kind,
  id,
  name,
  description,
  tier,
  attack,
  health,
  cost,
  frozen
} = {}) {
  const attributes = {
    'data-inspect-kind': kind ?? '',
    'data-inspect-id': id ?? '',
    'data-inspect-name': name ?? ''
  };
  if (description) attributes['data-inspect-description'] = description;
  if (tier != null) attributes['data-inspect-tier'] = tier;
  if (attack != null) attributes['data-inspect-attack'] = attack;
  if (health != null) attributes['data-inspect-health'] = health;
  if (cost != null) attributes['data-inspect-cost'] = cost;
  if (frozen) attributes['data-inspect-frozen'] = 'true';
  return attributes;
}
