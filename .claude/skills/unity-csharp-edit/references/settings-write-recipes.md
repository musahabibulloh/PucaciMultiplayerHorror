# Settings Write Recipes

Use this guide when a code change also needs project or package settings updates.

## Project Settings

Use `get_project_setting` or `get_project_settings` to inspect the current state before writing.
Use `set_project_setting` only for a setting that is directly relevant to the code change.

Typical cases:

- Switching active input handling for Input System work.
- Updating editor or player settings required by new runtime code.
- Verifying existing settings before adding editor utilities or SettingsProviders.

```bash
unity-cli raw get_project_setting --json '{"path":"Player/activeInputHandler"}'
unity-cli raw get_project_settings --json '{"includePlayer":true,"includeEditor":true}'
unity-cli raw set_project_setting --json '{"path":"Player/activeInputHandler","value":"InputSystemPackage","confirmChanges":true}'
```

## Package Settings

Use package settings when a Unity package stores configuration through Settings Management rather than through your own editor script.

Typical cases:

- Code Coverage settings
- Package-specific project or user preferences
- Editor tooling that reads package configuration keys

```bash
unity-cli raw get_package_setting --json '{"package":"com.unity.testtools.codecoverage","key":"EnableCodeCoverage","scope":"project"}'
unity-cli raw set_package_setting --json '{"package":"com.unity.testtools.codecoverage","key":"EnableCodeCoverage","value":true,"scope":"project","confirmChanges":true}'
```

## Working Rules

- Read first, then write. Do not guess setting paths or keys.
- Keep the setting change in the same task as the code that depends on it.
- Prefer Unity's existing settings APIs over adding a one-off editor utility.
- Re-run the relevant compilation or tests after a settings change that affects build, editor, or runtime behavior.
