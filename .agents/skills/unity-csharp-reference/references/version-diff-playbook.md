# Version Diff Playbook

Use this flow when comparing Unity API behavior across two cached versions, or when an LLM-suggested API needs to be validated against the canonical source for a specific project version.

## When to use

- The user wants to migrate a project from Unity X to Unity Y and asks which APIs changed.
- An LLM proposed `Animator.Play(stateName, layer)` but the project pins an older Unity version where that overload may not exist.
- A bug report cites a behavior change between Unity LTS versions and the team wants evidence from the source.

## Prerequisites

1. Both Unity versions are cached locally. Run `unity-cli reference status --output json` and check that the targets are listed. If not, fetch them:

   ```bash
   unity-cli reference fetch --version 2022.3.10f1 --branch 2022.3/staging --accept-license
   unity-cli reference fetch --version 2023.2.20f1 --branch 2023.2/staging --accept-license
   ```

2. The Phase 2 symbol index is generated on the first `find-symbol` or `diff` call per version; no extra command is needed.

## Symbol-only diff (default)

```bash
unity-cli reference diff --from 2022.3.10f1 --to 2023.2.20f1 --symbol UnityEngine.Animator
```

Output shape (`diffs` is an array; empty if the symbol is missing in both versions):

```json
{
  "ok": true,
  "from": "2022.3.10f1",
  "to": "2023.2.20f1",
  "diffs": [
    {
      "symbol": "UnityEngine.Animator",
      "kind": "class",
      "beforePath": "Runtime/Export/Animation/Animator.bindings.cs",
      "beforeLine": 6,
      "afterPath": "Runtime/Export/Animation/Animator.bindings.cs",
      "afterLine": 8,
      "hunks": [{ "before": ["..."], "after": ["..."], "beforeStart": 1, "afterStart": 1 }]
    }
  ]
}
```

Tips:

- The view window is 30 lines around the symbol declaration. Use `reference view` for a larger window.
- When the symbol exists in only one version, the missing side has `beforePath` / `beforeLine` (or after) as `null`. Treat that as added / removed.

## Path-range diff (opt-in)

```bash
unity-cli reference diff --from 2022.3.10f1 --to 2023.2.20f1 --path Runtime/Export/Animation --max-symbols 50
```

Returns `{added, removed, changed, truncated}`. Use this when scanning a subdirectory; raise `--max-symbols` cautiously since each `changed` entry triggers a view + line diff. `truncated: true` means the cap was hit and there may be more results.

## Cursor-driven resolve

```bash
unity-cli reference resolve-symbol-at Assets/Scripts/Player.cs --line 42 --column 18 --version 2023.2.20f1
```

Output shape:

```json
{
  "ok": true,
  "cursorPath": "Assets/Scripts/Player.cs",
  "cursorLine": 42,
  "cursorColumn": 18,
  "tokenName": "Animator",
  "candidates": [
    {
      "version": "2023.2.20f1",
      "fqn": "UnityEngine.Animator",
      "kind": "class",
      "referencePath": "Runtime/Export/Animation/Animator.bindings.cs",
      "referenceLine": 8,
      "viewExcerpt": ["..."]
    }
  ]
}
```

The CLI extracts the identifier at the cursor (alphanumeric + `_`) and returns `null` when the cursor lands on whitespace, a comment, or a string literal. When `--version` is omitted, all cached versions are scanned and `candidates` is a flat list. The `unity-cli reference resolve-symbol-at` flow is a thin wrapper around `find-symbol` + `view`; it does not touch the csharp-lsp workspace.

## Anti-patterns

- Calling `reference diff --path` over the whole `Runtime/` tree without `--max-symbols`. The default cap is 50; the result still walks both versions' indexes once.
- Trusting `tokenName` when the cursor is inside a string literal or comment. The current extractor is regex-based and does not understand C# lexing.
- Using `resolve-symbol-at` to confirm method-level behavior. Phase 2 only indexes type definitions; for member-level signatures continue with `reference grep` until Phase 2.5 lands.
