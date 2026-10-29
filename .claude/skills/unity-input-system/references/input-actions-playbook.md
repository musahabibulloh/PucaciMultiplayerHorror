# Input Actions Playbook

## Authoring Order

1. Identify the target `.inputactions` asset.
2. Create or select the action map.
3. Add actions.
4. Add bindings or composites.
5. Add control schemes if needed.
6. Analyze the asset.

## Binding Guidance

- Create the action before adding its bindings.
- Use composite bindings when a single action needs structured directional input.
- Remove obsolete bindings before replacing them wholesale.

## Validation

- Run `analyze_input_actions_asset` after structural changes.
- Separate asset authoring from runtime validation.
- Use `unity-playmode-testing` for actual input simulation after the asset change lands.
