# OGL regions: flight velocity continuity, stage 1

`EntityTransferModule.CrossAgentIntoNewRegionMain` previously populated the
EventQueue `CrossRegion` viewer packet with `Vector3.Zero` for all
ordinary avatars. A seated avatar already retained its horizontal
velocity. Thus a flying avatar was handed to the viewer with **zero
velocity**, even though the destination `AgentData` contains motion
data. This can cause a sudden visible deceleration during flight.

The first-stage fix sends `agent.Velocity` for **flying avatars**,
while preserving previous default values for walking and seated avatars.
The fallback old CrossRegion packet already sends `agent.Velocity` and
remains unchanged.

This does **not** make distributed crossings perfectly invisible.
A seamless experience also requires:
- Predicting a boundary crossing from avatar position/velocity, including
  diagonal flight and varregion boundaries.
- Preparing child-agent, CAPS and destination region presence **before**
  crossing, with sufficient lead time based on velocity and RTT.
- Measuring `SimulationService.UpdateAgent` latency, physics detach/
  attach time, viewer `CrossRegion` receipt and texture/attachment catch-up.
- Keeping predictive work bounded to authorized neighboring regions;
  handling cancel/retry/teleport/rapid reversal, and hard stop on rejection.
- Performing real viewer testing for different flight speeds, diagonal
  crossings, busy sims, and inter-server transfers; comparing viewer
  frame gaps (p50/p95/p99), avatar position discontinuity and velocity
  before and after.

**Tests:** The CI static contract verifies preservation of flying
velocity and unchanged seated fallback. It is not a replacement for
real Firestorm/OGL Viewer motion tests. No claim of zero jerk is made.
