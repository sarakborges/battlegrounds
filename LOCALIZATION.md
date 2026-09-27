# Mod-owned presentation localization

Presentation vocabulary and UI copy belong to the selected mod. `Battlegrounds.Core` remains locale- and terminology-neutral; it owns IDs, rules, commands and deterministic simulation only.

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
  "ui.chooseLeader": "Escolha seu {leader}",
  "ui.refresh": "Atualizar"
}
```

Locale files contain presentation data only. They do not override rules, IDs, numeric balance, effects, targeting or simulation behavior.

## Validation

`ModValidator` rejects presentation packages that do not satisfy the boundary.

The manifest must provide all required terminology fallback keys. The `localization` directory and `presentation.json` are required. The configured default locale must have a matching locale file, and that default locale must define every required `ui.*` presentation string used by the current game surface.

Secondary locales may be partial. Every authored localization key/value that is present must be a non-empty string, and locale filenames use normalized locale identifiers such as `en`, `pt-BR` or `es-MX`.

Validation happens before `ModPackage` is materialized, just like gameplay content validation.

## Locale resolution and fallback

`ModPresentationCatalog.Resolve(...)` is deterministic for the same validated package and requested locale.

Resolution uses:

```text
requested exact locale
        |
        v
language locale, when present
        |
        v
default locale
        |
        v
manifest terminology fallback for term.*
```

For example, `es-MX` uses `es-MX` when present, otherwise `es` when present, otherwise the mod's default locale. Missing keys in a partial secondary locale fall through to the default locale one key at a time.

An unknown locale therefore does not make presentation unusable and does not invent engine-owned English strings.

## Templates

UI strings may contain named placeholders:

```json
{
  "ui.acquireEntry": "{acquire} {name} • {kind} • {tier} {tierValue} • Cost {cost}"
}
```

`ModPresentationText.Format(...)` substitutes those named values. The template controls word order and punctuation; Godot supplies runtime values such as names, counts and terminology.

Templates are deliberately presentation-only. They never become effect expressions or gameplay scripting.

## Godot locale selection

`Main` exposes an optional `Locale` value.

- when `Locale` is non-empty, Godot requests that locale from the mod catalog;
- otherwise presentation starts from `TranslationServer.GetLocale()`;
- Content's catalog applies exact/language/default fallback.

Godot renders the resolved `ModPresentationText`. It may format labels, buttons, prompts, summaries and replay text, but it never changes Core state based on localized text.

## Application boundary

`SinglePlayerSession.Mod` already exposes the validated `ModPackage`. Application does not choose a locale, parse templates or interpret vocabulary. This keeps the framework-free session layer independent of UI language and lets different presentation clients resolve the same mod for different locales.

## Authored entity names

This slice localizes generic vocabulary and UI templates. Authored `UnitDefinition.Name`, `ActionDefinition.Name`, `LeaderDefinition.Name`, `PowerDefinition.Name`, behavior/type/tag names and combine names are still the single display strings authored in their content files.

The next presentation/content slice should give those authored entities stable localization keys so their display names and future descriptions can vary by locale without changing IDs or gameplay definitions.

## Assets

Binary art, animation and audio are intentionally outside this contract. They should be introduced through presentation metadata after localized authored entity display text, without adding filesystem or presentation concerns to Core.
