# Prefab Edit Mode

## Safe Sequence

1. Create or open the prefab asset.
2. Make the scoped changes in prefab edit mode.
3. Save the prefab.
4. Exit prefab mode before switching contexts.

## Scene vs Asset

- `modify_prefab` targets the asset.
- `instantiate_prefab` creates a scene instance.
- If the user wants to tweak only one placed instance, use `unity-gameobject-edit` instead.

## Common Pitfalls

- Unsaved prefab edits are lost on exit.
- Remaining in prefab mode can confuse later scene operations.
- Instantiation placement should use explicit transform values when the exact spawn position matters.
