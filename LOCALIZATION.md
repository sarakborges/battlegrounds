# Mod-owned presentation localization

Presentation vocabulary, authored entity display text and UI copy belong to the selected mod. `Battlegrounds.Core` remains locale- and terminology-neutral; it owns IDs, rules, commands and deterministic simulation only.

Ownership is explicit:

```text
mod files
   |
   v
Battlegrounds.Content  -- validate/load --> ModPackage.Presentation
   |
   v
Battlegrounds.Application  -- pass through validated package
   |
   v
Battlegrounds.Game  -- choose locale + render strings
```

## Terminology

`mod.json` keeps the package's neutral presentation terminology. These values are the guaranteed fallback for concept keys such as:

- `unit` / `units`;
- `action` / `actions`;
- `leader` / `leaders`;
- `power` / `powers`;
- `health` and `armor`;
- `resource`;
- `offer`;
- `tier`;
- `reserve`;
- `field`;
- `acquire` and `release`;
- `preparation`, `combat` and `round`.

At runtime each terminology entry is available as `term.<key>`. A locale file may override those values, so one mod can render `Leader`, `Hero`, `Tamer`, `Commander`, or any other theme-specific wording without changing Core types or IDs.

## Localization layout

A mod contains:

```text
localization/
  presentation.json
  en.json
  pt-BR.json
  ...
```

`localization/presentation.json` identifies the complete fallback locale:

```json
{
  "defaultLocale": "en"
}
```

Each locale file is one flat JSON object of string keys to string values:

```json
{
  "term.leader": "Líder",
  "entity.unit.scout.name": "Batedor",
  "entity.unit.scout.description": "Uma unidade leve que devolve Energia ao entrar em campo.",
  "ui.chooseLeader": "Escolha seu {leader}",
  "ui.refresh": "Atualizar"
}
```

Locale files contain presentation data only. They do not override rules, IDs, numeric balance, effects, targeting or simulation behavior.

## Authored entity display text

Every ID-addressable authored entity has a stable presentation namespace derived from its mechanical ID:

```text
entity.leader.<leader-id>.name
entity.power.<power-id>.name
entity.unit.<unit-id>.name
entity.action.<action-id>.name
entity.behavior.<behavior-id>.name
entity.type.<type-id>.name
entity.tag.<tag-id>.name
entity.combine.<combine-id>.name
```

The same namespaces reserve an optional `description` field, for example:

```text
entity.unit.scout.description
entity.power.vital-shift.description
```

An entity's `name` in `content/.../<id>.json` remains the authored fallback. Content turns that stable authored name into an immutable presentation fallback; locales only override display text. The Core definition and ID therefore do not vary with locale and gameplay never reads localized strings.

A default-locale file does not need to duplicate every authored name. If no locale overrides `entity.unit.scout.name`, presentation falls back to the validated `UnitDefinition.Name`. Descriptions are optional and have no gameplay meaning.

`ModPresentationText.EntityName(...)` resolves a display name after locale fallback. `TryEntityDescription(...)` exposes an optional localized description without making one required for gameplay or loading.

## Validation

`ModValidator` rejects presentation packages that do not satisfy the boundary.

The manifest must provide all required terminology fallback keys. The `localization` directory and `presentation.json` are required. The configured default locale must have a matching locale file, and that default locale must define every required `ui.*` presentation string used by the current game surface.

Secondary locales may be partial. Every authored localization key/value that is present must be a non-empty string, and locale filenames use normalized locale identifiers such as `en`, `pt-BR` or `es-MX`.

Keys beginning with `entity.` must use the exact `entity.<kind>.<id>.name` or `entity.<kind>.<id>.description` shape. The kind must be one of the supported entity namespaces above and the referenced ID must exist in the corresponding content directory. Malformed keys produce `INVALID_ENTITY_LOCALIZATION_KEY`; unknown IDs produce `UNKNOWN_ENTITY_LOCALIZATION_REFERENCE`.

Validation happens before `ModPackage` is materialized, just like gameplay content validation.

## Locale resolution and fallback

`ModPresentationCatalog.Resolve(...)` is deterministic for the same validated package and requested locale.

For localized UI/entity values, resolution overlays data from least-specific fallback to most-specific selection:

```text
authored entity-name fallback / manifest term fallback
        |
        v
default locale
        |
        v
language locale, when present
        |
        v
requested exact locale
```

Locale selection itself prefers the requested exact locale, then its language locale, then the configured default locale. Missing keys in a partial secondary locale fall through one key at a time.

For example, `pt-BR` may translate `entity.unit.scout.name` while leaving `entity.unit.guard.name` absent. The scout uses the Brazilian Portuguese override and the guard keeps its authored content-file name. An unknown locale still resolves through the default locale and authored fallbacks without inventing engine-owned display strings.

## Templates

UI strings may contain named placeholders:

```json
{
  "ui.acquireEntry": "{acquire} {name} • {kind} • {tier} {tierValue} • Cost {cost}"
}
```

`ModPresentationText.Format(...)` substitutes those named values. The template controls word order and punctuation; Godot supplies runtime values such as resolved entity names, counts and terminology.

Templates are deliberately presentation-only. They never become effect expressions or gameplay scripting.

## Godot locale selection

`Main` exposes an optional `Locale` value.

- when `Locale` is non-empty, Godot requests that locale from the mod catalog;
- otherwise presentation starts from `TranslationServer.GetLocale()`;
- Content's catalog applies exact/language/default fallback plus authored entity-name fallbacks.

Godot renders the resolved `ModPresentationText`. Leader selection, offers, reserves, pending choices, targets, combines, field rows and combat playback resolve authored names by stable entity ID. Godot may format labels, buttons, prompts, summaries and replay text, but it never changes Core state based on localized text.

## Application boundary

`SinglePlayerSession.Mod` already exposes the validated `ModPackage`. Application does not choose a locale, parse templates or interpret vocabulary. Combat/session snapshots continue carrying stable IDs and immutable authored Core data; presentation may choose a different locale without changing those observations.

This keeps the framework-free session layer independent of UI language and lets different presentation clients resolve the same mod for different locales.

## Assets

Binary art, animation and audio are intentionally outside this contract. They are the next presentation concern and should be introduced through presentation metadata keyed by stable authored IDs, without adding filesystem or presentation concerns to Core.
