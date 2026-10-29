# PlayMode Test Loop

## Default Runtime Loop

1. Check editor state.
2. Enter Play Mode or start the test run.
3. Wait until the runtime is ready.
4. Send input or capture evidence.
5. Read status and stop cleanly.

## Input Timing

- Avoid input bursts before Play Mode is active.
- Prefer `create_input_sequence` for short repeated actions.
- Re-check state after major interactions when the UI or scene should react.

## Evidence Capture

- Use screenshots for single-state proof.
- Use short video clips for motion or timing-sensitive proof.
- Prefer `maxDurationSec` on video capture for unattended runs.
