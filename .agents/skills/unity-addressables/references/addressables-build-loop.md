# Addressables Build Loop

## Recommended Order

1. List or inspect current groups.
2. Apply group and entry changes.
3. Run `addressables_analyze`.
4. Review whether `fix_issues` is appropriate.
5. Build content.
6. Use `clean_build` after large structural changes.

## Risk Notes

- Group moves can change downstream bundle layout.
- `fix_issues` may alter more than one group or entry.
- Clean builds are slower but safer after major reorganization.

## Handoff Boundary

- Use `unity-asset-management` for general asset dependency questions outside Addressables.
- Use this skill only when the workflow is clearly about Addressables content or build steps.
