# Asset Safety

## Inspection First

- Read asset info before changing import settings.
- Run dependency analysis before moving or deleting shared assets.
- Refresh the Asset Database after external file operations.

## Material Updates

- Create a new material when the user wants an isolated asset.
- Modify an existing material only when shared usage is understood.
- Keep material property payloads focused on the fields that actually change.

## Import Settings

- Treat import settings as project-wide behavior for that asset path.
- Change only the relevant keys in the `settings` object.
- Re-check the asset after a large import adjustment if the workflow depends on the new metadata.
