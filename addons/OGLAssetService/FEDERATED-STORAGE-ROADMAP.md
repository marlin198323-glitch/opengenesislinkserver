# OGL AssetService – federated storage implementation gates

## Implemented now

`OGLNodeObjectStore` is an isolated local, SHA-256-addressed payload primitive. It supports content-hash verification on reads, sharded paths, temporary-file staging, and non-overwriting publication. It is not connected to `OGLAssetConnector` or any ROBUST network endpoint. It is not yet a complete hardened storage service; further filesystem threat-model review, quotas, concurrency and crash-recovery validation are required.

The production connector still accepts only `Mode=compatibility` and delegates authoritative reads and writes to FSAssetConnector. Redis distributed payload reads remain disabled.

## Required before enabling federated mode

1. Durable metadata directory: map Asset UUID to current content hash, owning node, replicas, version and retention policy. Use a transactional database and optimistic concurrency controls.
2. Node identity and enrollment: stable node IDs, mutually authenticated transport, certificate rotation, ACLs and revocation. ROBUST must not trust caller-provided ownership claims.
3. Storage protocol: authenticated PUT/GET with bounded request sizes, streaming, integrity verification and explicit error codes. Avoid routing untrusted identifiers into filesystem paths.
4. Write transaction: persist payload to durable storage, commit authoritative metadata, then publish versioned invalidation through a durable outbox. Handle rollback and partial failure.
5. Replica policy: require sufficient durable replicas before declaring a write committed; support recovery and repair on node loss. Redis is not authoritative storage.
6. Read protocol: resolve versioned metadata, select a healthy replica, verify hash, retry another replica, and cache only version-consistent payloads.
7. Lifecycle: independent node storage process or durable volume, health reporting, backups, retention and safe garbage collection after references expire.
8. Migration: import existing FSAssets without deleting source data; reconcile SQL metadata, checksums, missing objects and duplicate UUIDs. Provide reversible cutover.
9. Final acceptance: multi-node concurrency, outages, data integrity, restore, restart persistence, load tests and rollback. Do not merge or enable federated mode until these pass.

## Operator modes

- **compatibility**: implemented, uses FSAssetConnector as authoritative storage.
- **local**: planned; local node authoritative, requires persistent UUID metadata.
- **federated**: planned; ROBUST directory and node-owned payloads with replicas.
- **hybrid**: planned; federated local storage plus durable central/object-store replicas.

No performance or completion claim for the planned modes is implied by this document.
