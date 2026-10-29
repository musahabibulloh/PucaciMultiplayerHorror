# Unity Implementation Guidelines

Use this guide before changing Unity C# that interacts with serialization, bindings, editor-only APIs, or settings.

## Preserve Serialization And Bindings

- Treat `[SerializeField]` names, serialized property paths, and asset-backed data as compatibility surfaces.
- Before renaming a field or property, check whether scenes, prefabs, ScriptableObjects, UXML bindings, custom inspectors, or reflection-based lookups depend on the current name.
- Prefer a targeted refactor over a broad rewrite when serialized compatibility matters.

## Respect Editor And Runtime Boundaries

- Keep `UnityEditor` usage inside editor-only assemblies or `Editor/` folders.
- Do not pull editor-only APIs into runtime code paths.
- Preserve existing asmdef boundaries and test assembly layout when adding or moving code.

## Keep Null Handling Intentional

- Add `null` guards only when the value can legitimately be absent at runtime, the type is nullable by contract, or the boundary accepts external input.
- Do not add speculative `null` checks around required components, guaranteed callback parameters, or established serialized references.
- Prefer stronger invariants such as `RequireComponent`, serialized configuration, setup methods, or clearer ownership over repetitive guard code.

## Choose The Right Write Shape

- Use symbol edits for localized behavior changes that should preserve the current file shape.
- Use file-level writes when the structure is changing, when a binding-aware rewrite is easier to reason about, or when `namePath` targeting is unstable.
- Use multi-file writes when interface, implementation, tests, and settings-adjacent code must stay in sync.

## Validate The Unity-Specific Surface

- Check compile diagnostics after non-trivial changes.
- When bindings or serialized names are involved, review the affected files and settings before declaring the change complete.
