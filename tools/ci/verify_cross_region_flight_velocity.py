#!/usr/bin/env python3
"""Regression contract for preserving flying avatar velocity during CrossRegion."""
from pathlib import Path

code=(Path(__file__).resolve().parents[2] /
      "OpenSim/Region/CoreModules/Framework/EntityTransfer/EntityTransferModule.cs").read_text()
block=code.split("public bool CrossAgentIntoNewRegionMain(",1)[1].split(
    "private void CrossAgentToNewRegionCompleted(",1)[0]
must=[
"Vector3 vel2 = Vector3.Zero;",
"if((agent.m_crossingFlags & 2) != 0)",
"vel2 = new Vector3(agent.Velocity.X, agent.Velocity.Y, 0);",
"else if (isFlying)",
"vel2 = agent.Velocity;",
"m_eqModule.CrossRegion(",
"neighbourRegion.RegionHandle, pos, vel2",
]
for part in must:
    assert part in block, part
assert block.index("else if (isFlying)") < block.index("m_eqModule.CrossRegion(")
assert "agent.CopyTo(cAgent,true);" in block
assert "m_scene.SimulationService.UpdateAgent(neighbourRegion, cAgent, ctx)" in block
print("Flight crossing velocity hand-off: OK")
