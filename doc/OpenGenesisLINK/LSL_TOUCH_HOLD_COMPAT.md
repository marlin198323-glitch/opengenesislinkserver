# Stationary touch hold compatibility (OpenGenesisLINK)

## Regression

In OpenSimulator, some viewers emit `GrabUpdate` only when the mouse moves.
The old script dispatch path emitted LSL `touch()` only for each received
`GrabUpdate`. Second Life scripts that expect repeated `touch()` while a
mouse button is held stationary would not advance.

The region scene now repeats the `touch()` event approximately every
100 milliseconds while the grab remains active. Repetition is driven by
the existing region heartbeat (the actual interval depends on region FPS).
A real `GrabUpdate` still dispatches normally and postpones the next
synthetic event to reduce duplicates.

Touches are isolated per avatar, obey prim-to-root `PassTouches` routing,
stop upon `DeGrab`, remove client, scene shutdown, removed prim, or lost
touch handler, and expire after five minutes as a fail-safe against a lost
release packet. The heartbeat dispatch never grabs or moves the object.

## Manual smoke test

Use `tools/diagnostics/ogl_lsl_touch_hold_smoke.lsl` in a prim on a
region running the updated server.

1. Hold the left mouse button over the prim *without moving the pointer*
   for at least two seconds. Expect `PASS` and several `touch()` calls.
2. Perform a short click (under one second). Expect one `touch_start`
   and `touch_end`; the object need not receive a `touch()` event.
3. Hold and move the pointer. Expect touch callbacks, but no burst of
   duplicate callbacks when movement stops.
4. Repeat with two different avatars on separate objects and verify one
   avatar's release does not interrupt the other.
5. Test child-prim touching with `PassTouches` enabled and disabled.
   Events must route the same way as regular `GrabUpdate` events.
6. Delete the object, teleport, and disconnect while holding. There
   must be no remaining touch stream or exceptions in simulator logs.

The test complements CI builds; it is not a substitute for an in-viewer
runtime verification.
