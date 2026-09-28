# Cosmetic assets

Cosmetic images are presentation-only PNG files owned by the mod.

Conventions:

- `assets/cosmetics/leaders/<leader-id>/<skin-id>.png`
- `assets/cosmetics/shopkeepers/<shopkeeper-id>/<skin-id>.png`

`presentation/cosmetics.json` chooses the active shopkeeper/skin and may override a leader skin by leader id. Leaders not listed there implicitly request the `base` skin.

Resolution is intentionally soft-fail:

1. requested `<skin-id>.png`;
2. `base.png` when the requested skin is not `base`;
3. for leaders only, the legacy `assets/presentation.json` portrait slot;
4. no image.

Missing or unreadable cosmetic PNGs must never invalidate gameplay or crash the mod bootstrap.
