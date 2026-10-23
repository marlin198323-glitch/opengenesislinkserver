# OpenGenesisLINK OGLVoice 2.0 — dedicated voice.opengenesislink.de vServer

**Status: Deployment scaffolding; not an installed or production-validated Voice system.**
The C# OGLVoice region discovery and Firestorm CAPS adapters already exist in the main server.
This deployment supplies a separate **Pion WebRTC media bridge** and **LiveKit SFU**. It does
not install or modify ROBUST, and it must not be mistaken for a voice-enabled production release
until tested with real Firestorm viewers.

## Request and media paths

1. Simulator `[NexVerseNodeAgent]` polls its trusted **ROBUST** listener at
   `/internal/oglvoice/v1/provider`. This is **not a media-server endpoint**.
   HTTP 404 means ROBUST is not publishing that path (often `[OGLVoice] Enabled=false`),
   or the NodeAgent `PeerUrl` points to the wrong HTTP listener/reverse proxy.
2. ROBUST returns an HMAC-signed provider descriptor with the Firestorm MediaGatewayUrl.
3. Firestorm sends voice LLSD through the simulator CAPS. Simulator validates authenticated
   avatar session, region/parcel voice permission, and Hypergrid admission.
4. Simulator signs a media exchange using its NexBus node shared key. Caddy on
   `voice.opengenesislink.de` permits only trusted simulator IPs to
   `POST /internal/webrtc/v1/exchange`; the bridge checks node ACL/HMAC/nonces.
5. The bridge terminates Firestorm Pion WebRTC and feeds Opus into LiveKit. LiveKit and
   Pion media packets are **UDP**, not Web traffic; Caddy cannot proxy their UDP ICE ports.

## Debian 13 dedicated host: installation steps

Prerequisites: a DNS A/AAAA record for `voice.opengenesislink.de` on this **new vServer**,
a public reachable IP, firewall control, Go version required by `go.mod`, `libopus-dev`,
`pkg-config`, `libopus0`, Docker Compose and Caddy. Use recent security-patched releases.
For the media bridge, prefer direct host deployment rather than bridged Docker NAT.

```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl git pkg-config libopus-dev libopus0 caddy docker.io docker-compose-plugin
getent group oglvoice >/dev/null || sudo groupadd --system oglvoice
id -u oglvoice >/dev/null 2>&1 || sudo useradd --system --gid oglvoice --no-create-home --shell /usr/sbin/nologin oglvoice
sudo install -d -m 0750 -o oglvoice -g oglvoice /opt/oglvoice /etc/oglvoice
if [ -d /opt/oglvoice/source/.git ]; then
  git -C /opt/oglvoice/source pull --ff-only origin main
else
  git clone https://github.com/opengenesislink/opengenesislinkserver.git /opt/oglvoice/source
fi
cd /opt/oglvoice/source/services/oglvoice-media
sudo bash deploy/install-go-toolchain.sh
/opt/oglvoice/toolchains/go1.26.10/bin/go version
/opt/oglvoice/toolchains/go1.26.10/bin/go build -tags nolibopusfile -trimpath -o /tmp/oglvoice-media .
sudo install -m 0755 -o root -g root /tmp/oglvoice-media /opt/oglvoice/oglvoice-media
```

**Go version / v39227 build failure:** `go 1.26.8` in `go.mod` is valid for
modern Go and is the module's *minimum* version. Older Debian-packaged `go` binaries
can reject patch versions with `invalid go version ... must match format 1.23`.
Do **not** downgrade `go.mod` or dependencies. The script installs the official
Go **1.26.10** Linux x86-64/ARM64 release to
`/opt/oglvoice/toolchains/go1.26.10` and verifies its Go-published SHA-256
before extracting it. It does **not** replace `/usr/bin/go` or `/usr/local/go`;
the explicit binary path ensures the correct toolchain is used. Unsupported
architectures and checksum mismatches abort without changing an existing Go install.

Copy `deploy/livekit.yaml.example` to `/etc/oglvoice/livekit.yaml`; choose a strong
LiveKit API key and random API secret (at least 32 bytes). Put **exactly the same**
key and secret in `/etc/oglvoice/media.env` (based on `deploy/media.env.example`).
Keep the existing grid tenant `nexverse` for Stadt NexVerse (or use separate tenant IDs for other grids). Replace NodeId and the **existing** NexBus shared key with your actual simulator values.
For multiple simulators add a separate `nodes` entry per NodeId, using each node's
real shared key. Never expose a LiveKit signing secret to a viewer.

```bash
sudo chown root:oglvoice /etc/oglvoice/livekit.yaml
sudo chmod 0640 /etc/oglvoice/livekit.yaml
sudo chown root:oglvoice /etc/oglvoice/media.env
sudo chmod 0640 /etc/oglvoice/media.env
```

Copy `deploy/compose.yaml` to `/opt/oglvoice/compose.yaml`, then create
`/opt/oglvoice/.env` with `LIVEKIT_TAG=<a specifically tested LiveKit release>`.
Do not use `latest` for production; verify image version compatibility and pin it.
`sudo docker compose -f /opt/oglvoice/compose.yaml up -d` starts LiveKit with
host networking and the local YAML configuration.

Install `deploy/oglvoice-media.service` at `/etc/systemd/system/oglvoice-media.service`:
```bash
sudo systemctl daemon-reload
sudo systemctl enable --now oglvoice-media
sudo systemctl status oglvoice-media
curl -fsS http://127.0.0.1:19098/healthz
```

Replace `REPLACE_WITH_TRUSTED_SIMULATOR_IP_OR_CIDR` in `deploy/Caddyfile.example`
with the **actual** simulator public source address or VPN CIDR. Use individual IPs or
the restricted WireGuard subnet, not an Internet-wide CIDR. If Caddy is behind another
proxy, ensure its `remote_ip` evaluation refers to the actual authenticated origin.
Copy to `/etc/caddy/Caddyfile`, run `caddy validate --config /etc/caddy/Caddyfile`
and reload Caddy. TLS and DNS must resolve correctly before enabling Firestorm.

### Firewall: different independent transports

| Endpoint | TCP | UDP | Access |
| --- | --- | --- | --- |
| Caddy HTTPS/WSS + ACME | 443, 80 | — | Public |
| Pion WebRTC Firestorm | — | 40000–40999 | Public |
| LiveKit ICE TCP | 7881 | — | Public |
| LiveKit ICE UDP multiplex | — | 7882 | Public |
| LiveKit embedded TURN/UDP | — | 3478 | Public |
| LiveKit HTTP API | 7880 | — | Local/proxy only |
| OGL media bridge HTTP | 19098 | — | Loopback only |

Expose UDP ICE directly on the **voice vServer**, not through the ROBUST HTTP server.
Pion's port range is configurable by `OGLVOICE_WEBRTC_PORT_MIN/MAX` and is fail-closed
for invalid ranges; the example allocates 1000 ports. Larger deployments need capacity
and port-exhaustion testing. NAT requires correct public ICE candidates. TURN/TLS on 443
needs further deployment design (often a dedicated TURN hostname/certificate); UDP TURN
alone does **not** guarantee connectivity across restrictive enterprise firewalls.

## ROBUST and simulator setup

On the grid ROBUST's **active** `Robust.HG.ini` or `Robust.ini` (not only the
`.example` file), set:

```ini
[OGLVoice]
    Enabled = true
    Mode = "GridAuthority"
    ServiceUrl = "https://voice.opengenesislink.de"
    TenantId = "nexverse"
    IncludeHypergridGuests = true
    EnableFirestormGateway = true
    MediaGatewayUrl = "https://voice.opengenesislink.de/internal/webrtc/v1/exchange"
    EnableSessionAuthority = false
```

`EnableSessionAuthority` is a separate optional ROBUST LiveKit-token endpoint
and is not necessary to activate the external Go Media Bridge; LiveKit signing
keys remain on the media vServer. With `Mode=GridManaged` the simulator discovers
the provider from ROBUST using `[NexVerseNodeAgent]` (not from the voice hostname).
Set its `PeerUrl` to the **reachable actual ROBUST listener** and its NodeId/SharedKey
to match the media ACL. Restart ROBUST so the discovery handler is registered;
restart simulator(s) as required to register viewer CAPS.

**Before enabling the gateway**, verify `/healthz`, HTTPS routing, the bridge logs,
LiveKit process, TLS certificates, and the private trusted ROBUST discovery.
A non-signed public `curl` request to the ROBUST discovery path should return
401 (not 404) once the route is actually registered. A GET to the media exchange
returns 403 via Caddy (only trusted POSTs are proxied); this is by design.
Do **not** treat a HTTP 200 at `/healthz` as proof of audio.

### End-to-end acceptance

Run two unmodified Firestorm OpenSim WebRTC-enabled viewers with avatars on one
voice-enabled parcel. Test bidirectional Opus audio, mixed simultaneous speakers,
distance/panning, mic mute/PTT, on-screen speaking indicators, region crossing,
Hypergrid access, logout/reconnect and unauthorized NodeId rejection.
Check LiveKit and bridge logs plus UDP connection tracking under load.
Never claim productive WebRTC voice solely from a passing CI build.

### Troubleshooting the historical HTTP 404

The simulator logs the first HTTP 404 when ROBUST does not register provider discovery;
further identical responses are quiet until the HTTP status changes. This only
reduces log noise — it **does not** enable voice or mask a changing error state.
Inspect active ROBUST `[OGLVoice]`, NodeAgent `PeerUrl`, proxy routing and the
listening host/port. HTTP 401 means the correct handler is present but authentication
or HMAC key verification failed; do not disable authentication to silence the log.
