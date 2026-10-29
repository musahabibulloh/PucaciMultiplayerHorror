# Scene Bootstrap Patterns

## Default Flow

1. Create or load the scene.
2. Add top-level GameObjects.
3. Add required components.
4. Save the scene.

## Starter Scene Tips

- Create parent objects early when the final hierarchy matters.
- Use primitives only for quick placeholders or test fixtures.
- Save after each major batch when the scene is being built incrementally.

## Handoff Boundaries

- If the user starts tuning existing object properties, switch to `unity-gameobject-edit`.
- If the work becomes prefab-centric, switch to `unity-prefab-workflow`.
- When the scene is intended for runtime verification, follow up with `unity-playmode-testing`.
