# Scene Inspection Playbook

## Recommended Order

1. Start with `get_scene_info` or `list_scenes`.
2. Use `get_hierarchy` to build a quick mental model.
3. Narrow with `find_gameobject` or `find_by_component`.
4. Inspect details only after the target object is clear.

## Large Scene Tactics

- Use `get_hierarchy --json '{"nameOnly":true}'` first.
- Include inactive objects in audit-style queries.
- Prefer targeted component reads over large hierarchy dumps when the user asks about a single object.

## Ambiguous Objects

- Duplicate names are common; confirm by reading details or references.
- Report the full object path or distinguishing component data when names collide.
- Use animator queries only after the correct object is identified.
