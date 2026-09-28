# Unit combine semantics

Unit combining is a neutral Preparation mechanic. Core does not know concepts such as Triple, Golden, Fusion, Upgrade, Evolution, or any fandom-specific presentation name.

## Authored model

Every combine recipe is an independent ID-addressable mod entity stored as one file:

```text
content/combines/<combine-id>.json
```

Example:

```json
{
  "id": "scout-merge",
  "name": "Scout Merge",
  "sourceUnitId": "scout",
  "requiredCopies": 3,
  "resultUnitId": "scout-merged"
}
```

The recipe owns only the mechanical relationship: one source Unit definition, an exact positive copy requirement of at least two, and one result Unit definition. The result is an ordinary independently-authored Unit file. A mod is free to call the recipe/result whatever its theme requires.

`UnitCombineCatalog` is immutable authored data. It does not own runtime Unit instances or perform mutations.

## Presentation interaction mode

Mods may choose whether the human-facing presentation exposes combine as a manual interaction or submits eligible combines automatically through the same Core command boundary.

The optional file is:

```text
presentation/interaction.json
```

Example:

```json
{
  "version": 1,
  "combineMode": "automatic"
}
```

Supported `combineMode` values are `manual` and `automatic`. If the file or property is omitted, presentation defaults to `manual`.

This setting is presentation-owned. It changes how human intent is collected, not the authoritative combine operation: Core still requires an explicit `CombineUnitsCommand` with exact instance IDs, and AI continues to use the same command boundary.

## Command boundary

UI and AI combine through the same explicit command:

```text
CombineUnitsCommand(PlayerId, UnitCombineId, UnitInstanceIds)
```

The command names the exact runtime Unit instances to consume. Core never silently chooses which copies disappear when multiple eligible copies exist.

A valid command requires:

- the recipe to exist;
- exactly `requiredCopies` distinct instance IDs;
- every selected Unit to belong to the acting player;
- every selected Unit to still be alive;
- every selected Unit to have the recipe's `sourceUnitId` definition;
- enough Reserve capacity for the resulting Unit after selected Reserve copies are removed.

Selected copies may come from Reserve, Field, or both.

## Pool ownership

Combining consumes the selected runtime instances permanently.

For each consumed Unit:

- if it still owns a physical pool copy through `PoolReturnDefinition`, that original definition is returned to the shared Unit pool exactly once;
- if it is `Generated`, no pool copy is created or returned.

The combine result is always created with `UnitInstanceOrigin.Generated`. Therefore the result itself never invents a physical pool copy, even if its definition also appears in the pool for unrelated reasons.

This is the same ownership invariant used by transform/copy mechanics: runtime identity and pool-copy ownership are separate facts.

## Resolution order

A successful combine resolves atomically in this order:

1. validate the recipe and the entire explicit input set;
2. calculate post-consumption Reserve capacity before mutating anything;
3. remove all selected instances from the player's Reserve/Field;
4. return each consumed pooled copy to the Unit pool exactly once;
5. create a fresh Generated instance of `resultUnitId`;
6. place the result in Reserve;
7. execute the result Unit's authored `onCombine` reward triggers in definition order, using that result Unit itself as the effect source;
8. resolve any consequences produced by those rewards through the existing Preparation effect runtime.

No component's `onDeath`, `onPlay`, `onSummon`, or release semantics are invoked merely because it was consumed by a combine. Combining is its own explicit lifecycle operation.

## `onCombine`

`onCombine` is a Preparation-only reward hook on the authored result Unit. It is not a combat trigger and is not a synonym for `onPlay` or `onSummon`.

The result Unit is already in Reserve when its `onCombine` effects run. The result Unit itself is the runtime effect source, so source-relative dynamic values such as `sourceStat` read the result's authored/current runtime stats rather than a synthetic proxy. Multiple authored `onCombine` triggers resolve in definition order. The hook exists for reward-style consequences such as resource changes, generated playables, pending choices, or other targetless Preparation effects that already use `GameEffectRuntime` semantics.

The result's upgraded/base stats, behaviors, tags, types, and ordinary triggers belong directly on its `UnitDefinition`; they are not synthesized from the consumed copies.

Current `onCombine` semantics intentionally do not support conditions, counters, activation limits, or UI-selected targets. Those would require a richer explicit combine-event context rather than pretending the Reserve result is a Field effect source. Content should encode the resulting Unit state directly and use `onCombine` only for the post-combine reward.

## Determinism

Combining itself consumes no RNG. Any RNG used by `onCombine` consequences comes from the same injected deterministic source as other Preparation effects.

Because callers provide exact instance IDs, replay/debug logs can reproduce which copies were consumed without relying on collection iteration order or hidden selection policy.
