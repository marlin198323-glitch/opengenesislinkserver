# OpenGenesisLINK fast-flight region crossing – stage 2

Before this change, `ScenePresence.CheckForSignificantMovement` allowed
child-agent / neighbour refresh only every 2 seconds and after 32 metres
of travel (or a changed view range). A flying avatar can traverse most of
a 256 m region before the next opportunity to prepare another region.

`FlightNeighbourPrefetchPolicy.NeedsPrefetch` checks:
- The avatar is **flying** and has at least **20 m/s horizontal speed**.
- The projected direction is **outward towards an X or Y region edge**.
- That edge is within a bounded **1.5-second lookahead**, minimum 32 m
  and maximum 128 m (using the actual region size; supports varregions).
- Position/velocity must be finite and currently inside the region.

Only then the simulator allows `EnableChildAgents()` at a **minimum
500 ms interval** (instead of 2000 ms), irrespective of the normal
32-metre movement trigger. Normal walking, stationary, low-speed flight
and parallel movement keep original behavior. Child-agent creation and
viewer caps still use existing signed/authorized grid protocols.

This is purely **neighbour preparation**. It does not teleport avatars
early, push a root agent through a wall, bypass parcel admission checks,
or change the authoritative crossing protocol. The already merged
stage-1 velocity fix ensures flying avatars retain momentum in the
viewer `CrossRegion` notification.

## Tests and limitations

16 executable regression scenarios cover cardinal and diagonal
directions, slow/ground/parallel movement, NaN and off-grid positions,
varregion sizes and bounded lookahead. Full CI compiles and runs these.

Actual seamless crossing still requires performance measurements on at
least two **separate simulator processes**, sim crossings with 20, 50,
100+ m/s flight, high avatar/physics load, and OGL Viewer/Firestorm
frame continuity. Changes may increase neighbour refresh work near
edges; measure CPU, latency and neighbour API calls before production.

The existing 10-second initial child-agent update deferment after
login remains untouched because the initial neighbour setup is
already performed during login. No promise of zero visible jerk is made.
