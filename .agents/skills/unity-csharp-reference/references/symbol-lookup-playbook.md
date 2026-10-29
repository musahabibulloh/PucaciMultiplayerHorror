# Symbol Lookup Playbook

The canonical reference → navigate → edit workflow when validating LLM-suggested Unity code.

## When to Reach Here

- The user (or an LLM) proposed a Unity API call and you need to confirm the exact signature or behavior.
- An LLM suggested code that compiles but you suspect it relies on a different overload, attribute, or version-specific behavior.
- The user is comparing two Unity versions and needs API-level evidence.

## Workflow

### 1. Locate the symbol in the official source

```bash
unity-cli reference find-symbol --name Animator --kind class
unity-cli reference find-symbol --name AssetDatabase --kind class --namespace UnityEditor
unity-cli reference grep "void Play\(" --file-glob "Animator*.cs" --context 2
unity-cli reference search "AssetDatabase.Refresh" --max-results 10
```

`find-symbol` reads (and lazily builds) a per-version index of type definitions and returns `{ path, name, kind, line, namespace?, container?, fqn? }`. Use it when the type name is already known; the index is regenerated incrementally based on file size and mtime, so repeated lookups within the same Unity version are O(1) over the index. Fall back to `grep` when the pattern targets methods, properties, or free text — those are intentionally outside the Phase 2 index scope.

### 2. Read the relevant span

```bash
unity-cli reference view Runtime/Export/Animation/Animator.bindings.cs --start-line 100 --max-lines 60
```

Use the line number from `grep` to anchor a tight `--start-line` / `--max-lines` window. Avoid dumping entire files: the LLM context budget is finite and the surrounding code is rarely needed.

### 3. Cross-check against the project

Switch to [unity-csharp-navigate](../../unity-csharp-navigate/SKILL.md) for project sources:

```bash
unity-cli raw find_refs --json '{"name":"Animator.Play","scope":"assets"}'
```

This catches callers that depend on the exact overload you just confirmed.

### 4. Apply the change

Hand the validated signature to [unity-csharp-edit](../../unity-csharp-edit/SKILL.md) for the write step:

```bash
unity-cli raw apply_csharp_edits --json '{ ... }'
```

Keep the original `reference view` excerpt in the conversation context so the editor can quote the canonical implementation when justifying the change.

## Anti-Patterns

- Skipping the reference lookup and trusting the LLM-suggested API name verbatim.
- Reading the entire `Runtime/Export` tree instead of using `grep --file-glob` to narrow the scope.
- Caching multiple versions you do not actively compare; rely on `unity-cli reference clean --keep 1` to control disk usage.
- Mixing project sources and reference sources in the same write tool call; the reference cache is read-only on purpose.
