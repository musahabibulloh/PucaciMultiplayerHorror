# Component Edit Safety

## Before Mutating

- Confirm the full `gameObjectPath`.
- List or inspect current components when the target component type may be duplicated.
- Call out destructive edits before applying them.

## Field Updates

- `set_component_field` uses dot-separated `fieldPath` values for nested fields.
- Prefer field-level updates when a full `modify_component` payload would be noisy.
- Keep values explicit for vectors, colors, and nested structs.

## Destructive Changes

- Remove or delete one target at a time unless the user asked for a bulk cleanup.
- Save the scene after destructive changes.
- If the user wants to change the prefab asset instead of the scene instance, switch to `unity-prefab-workflow`.
