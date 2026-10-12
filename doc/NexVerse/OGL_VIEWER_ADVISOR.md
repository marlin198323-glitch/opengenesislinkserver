# OGL Viewer Advisor (opt-in)

OpenGenesisLINK contains a small server-side region module that recommends the
OGL Viewer in **private owner chat** to avatars using other viewer channels.

**Release state:** Not activated. Both `bin/OpenSim.ini` and
`bin/OpenSim.ini.example` explicitly set `Enabled = false`. The module also
defaults to disabled when its section is absent. No resident will receive a
message until a server operator opts in and restarts the simulator.

## Configuration

```ini
[OGLViewerAdvisor]
    Enabled = false
    IntervalMinutes = 60
    OfficialViewerPrefixes = "OpenGenesisLINK;OGL Viewer"
    Message = "Um das volle Erlebnis von OpenGenesisLINK geniessen zu koennen, steigen Sie auf den offiziellen OGL Viewer um: https://opengenesislink.de"
```

To activate later, change `Enabled = true` on **each simulator** where this
feature should run, then restart that simulator. Do not enable until the
shipping OGL Viewer reports a matching channel at login. Check the channel
in the actual viewer build, not merely the product title.

- `IntervalMinutes`: whole minutes from 1 to 1440 (default 60). The active
  root avatar is checked at rootification and every 15 seconds thereafter.
- `OfficialViewerPrefixes`: semicolon-separated, case-insensitive prefixes
  for the client-supplied viewer channel/version. Empty prefixes match nothing.
  Only an exact name or a name followed by a separator is exempt.
- `Message`: personalized plaintext (up to 900 characters), delivered using
  `ChatTypeEnum.Owner` only to the affected client. It is not region chat.
- Avatars with missing circuit information, NPCs and child agents receive
  no messages. Repeated rootification events on the same simulator process
  do not cause another notice before the interval expires.

## Important boundaries

The per-avatar cooldown is **in memory in one simulator process**. It works
across regions hosted by that same process; restart or a teleport to a
*different simulator process* can reset that process's cooldown. A strict
grid-wide one-per-hour limit across multiple hosts requires a shared,
atomic coordinator (for example, a small authenticated ROBUST/API endpoint
with persisted timestamps). Do not treat the in-process limiter as a
cross-host guarantee. This is especially important if multiple simulator
instances are active.

Viewer channel strings are self-reported and can be spoofed. This filter
must not be used as a security, access-control or entitlement decision.

## Manual acceptance checks (in an isolated simulator)

1. Without the section and with `Enabled=false`, log in using Firestorm:
   no advisor chat.
2. Set `Enabled=true` in a test instance, then restart. Firestorm receives
   one private chat notice after becoming a root agent.
3. Rootify again/teleport to another region on that same simulator within
   60 minutes: no additional notice.
4. Remain online for an hour: a second private notice arrives no earlier
   than 60 minutes after the first (up to 15-second polling delay).
5. OGL Viewer's actual channel begins with a configured official prefix:
   zero notices, even after one hour.
6. Another viewer and an unidentified-but-valid non-OGL channel receive
   the same hourly reminder.
7. Repeat with the module disabled to verify no events or timer are active.
8. For multi-host deployments, test the known cross-process limitation or
   integrate a central limiter **before** promising global hourly behavior.

The module is compiled automatically by the recursive `NexVerse.RegionModules`
file inclusion in `prebuild.xml`; no separate project or LSL script is needed.
