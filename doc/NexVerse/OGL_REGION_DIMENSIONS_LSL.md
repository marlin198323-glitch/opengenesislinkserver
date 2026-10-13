# Native OGL LSL: `oglGetRegionDimensions()`

OpenGenesisLINK adds a parameterless LSL function:

```lsl
vector dimensions = oglGetRegionDimensions();
```

The function reads the **current simulator scene's configured dimensions**, in metres, and returns `<X, Y, Z>` as an LSL vector.

- `X`: configured horizontal width, e.g. 256 or 1024 metres.
- `Y`: configured horizontal length, e.g. 256 or 1024 metres.
- `Z`: `RegionInfo.RegionSizeZ`, the scene's configured vertical extent, commonly 4096 metres in the default simulator settings.

For example, a 1024 × 1024 metre varregion with the default configured Z extent returns `<1024.0, 1024.0, 4096.0>`. The Z component **does not** mean terrain height at a position, the highest building, or the maximum altitude supported by every viewer or physics engine. Use `llGround()` for terrain height at an XY offset.

The function belongs to the native LSL API and is therefore independent of the OSSL `AllowOSFunctions` switch and OSSL threat-level permissions. The existing `osGetRegionSize()` remains unchanged and continues to report Z = 0, preserving compatibility.

This is an **OpenGenesisLINK extension**; unmodified Second Life and stock OpenSimulator do not support the function. Viewer-side highlighting/autocomplete may require separate updates, but server-side script compilation uses the registered API.

## Test

Run the structural regression check:

```sh
python3 tools/ci/verify_ogl_region_dimensions.py
```

Load `tools/lsl/ogl_region_dimensions_smoke.lsl` into a prim and confirm that it reports the expected X/Y dimensions and a positive Z value on both a standard region and a configured varregion. The CI check verifies source registration and legacy behaviour; an in-world smoke test is required for runtime verification.
