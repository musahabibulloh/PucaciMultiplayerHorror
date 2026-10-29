# UI Test Flow

## Locate, Inspect, Interact

1. Locate the element with `find_ui_elements`.
2. Inspect it with `get_ui_element_state`.
3. Interact with the narrowest command possible.
4. Re-read state if the next step depends on the UI reaction.

## Practical Tips

- Use `namePattern` when the hierarchy is stable.
- Use `elementType` when names vary but the control kind is known.
- Include inactive elements only when the user is explicitly debugging hidden UI.

## When to Combine Skills

- Use `unity-playmode-testing` when the UI must be exercised during Play Mode transitions.
- Use scene inspection or editing skills only if the task shifts from testing to authoring the UI structure.
