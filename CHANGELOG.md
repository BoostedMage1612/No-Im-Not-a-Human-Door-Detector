# Changelog

## v0.1.3

First GitHub publication of the working package delivered on October 23, 2026.

- Move the panel to the top-right corner.
- Follow window/resolution changes and scale down for narrow windows.
- Support in-place upgrades from the known 0.1.0–0.1.2 packages.
- Preserve the original tested binary and installer ZIP.

## v0.1.2

- Use the game's inherited `DialogManager` field instead of an unused field.
- Display the verdict while knocking and through the peephole.
- Retry temporary display-state failures without invalidating the assigned identity.
- Recover across tutorial/scene transitions and clear stale encounters.
- Add native display lifecycle regression coverage.

## v0.1.1

- Correct native object handles to pointer-sized values for this Unity build.
- Add native handle boundary tests and startup self-checks.
- Expand diagnostic collection and support an in-place upgrade.

## v0.1.0

- Initial Door Detector implementation, identity reservation and admission checks.
- Superseded because of the corrected native-handle crash. Use the latest release.
