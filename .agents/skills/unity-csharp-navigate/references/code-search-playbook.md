# Code Search Playbook

## Index Strategy

- Run `build_index` before the first symbol-heavy query in a session.
- Run `update_index` after file edits or when the user points at a recently changed path.
- Keep searches path-scoped whenever the target area is known.

## Query Order

1. Use `read` for exact-file inspection.
2. Use `search` when you know a text pattern but not the symbol container.
3. Use `get_symbols` to inspect a file's structure.
4. Use `find_symbol` and `find_refs` when the symbol name is known.

## Large Results

- Add `pageSize` to `find_refs` for heavily used symbols.
- Narrow `search` with `path` to avoid noisy hits.
- If packages may own the behavior, check `list_packages` instead of searching only `Assets/`.
