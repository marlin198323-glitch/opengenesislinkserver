# OGL AssetService – federated storage implementation gates

## Implemented now

`OGLNodeObjectStore` is an isolated local, SHA-256-addressed payload primitive. It supports content-hash verification on reads, sharded paths, temporary-file staging, and non-overwriting publication. It is not connected to `OGLAssetConnector` or any ROBUST network endpoint. It is not yet a complete hardened storage service; further filesystem threat-model review, quotas, concurrency and crash-recovery validation are required.

The production connector still accepts only `Mode=compatibility` and delegates authoritative reads and writes to FSAssetConnector. Redis distributed payload reads remain disabled.

## New single-process directory prototype

`OGLAssetLocationDirectory` now persists a JSON snapshot mapping canonical asset UUIDs to SHA-256 content hashes, a primary node, replica node IDs and monotonic revisions. `Publish` rejects stale expected revisions and persists before changing in-memory state. This is a **single-process prototype only**: concurrent processes can overwrite each other, and there is no fsync of the containing directory, lease, replica-health tracking, ACL, authenticated node registration, deletion/tombstone protocol or database transaction. Do not expose it as an authoritative federated directory. It is not wired into ROBUST.

## Local staging coordination

`OGLAssetNodeCoordinator` now joins the isolated object store and JSON location directory. `Stage` writes an immutable payload before publishing a revision-checked local UUID mapping; `ReadLocal` resolves the mapping, checks local primary ownership and reads the hash-verified object. The coordinator serializes calls only within one instance. Failed mapping publications can leave orphaned payloads, deliberately retained until a safe reference-aware garbage collector exists. There is no deletion, remote transport, replication, multi-process locking, network identity verification, or production connector integration. This is **not** a federated asset service.

## Directory record validation

The directory validates primary and replica node identifiers against whitespace-padded values and rejects duplicate replica IDs. Publishing validates a defensive copy rather than modifying the caller-owned entry. These protections do not authenticate node identity, coordinate writers across processes or establish replica durability.

## Transactional SQLite location directory (prototype)

`OGLSqliteAssetLocationDirectory` introduces a separate SQLite-backed mapping with a primary-key UUID, SHA-256 hash, primary node, JSON replica list and positive revision. `Publish` uses an INSERT-with-conflict-rejection for revision 0 and a revision-guarded UPDATE for subsequent revisions, both inside a SQLite transaction. The implementation opens separate connections and therefore supports SQLite file-level coordination between processes on a supported local filesystem. It does **not** make the JSON coordinator transactional, and it is not wired into the active asset connector or ROBUST. SQLite is not an appropriate shared database over arbitrary network filesystems; PostgreSQL/MariaDB backends, authenticated node enrollment, durable write/outbox protocol, replica verification, migrations, backups, and final acceptance are still outstanding. No production mode has been enabled.

## SQLite coordinator integration

`IOGLAssetLocationDirectory` is now implemented by the JSON prototype and SQLite CAS directory. `OGLAssetNodeCoordinator` depends on this interface, allowing its staging and local read operations to use the transactional SQLite mapping. This is code-level integration only: there is no automatic configuration selection, no active ROBUST route, no remote node transport, and no replicated commit guarantee. Cross-process concurrency is guarded at the SQLite mapping layer, not across payload writes and metadata publication as one atomic transaction.

## Replica preparation primitive

`OGLAssetReplicaStaging` verifies the SHA-256 of received payload bytes before placing them in immutable local object storage. It does not register a replica as available, authenticate a remote node, perform a network transfer, or guarantee multi-replica durability. The central directory must advertise a replica only after the destination has durably stored and acknowledged the exact hash. Until an authenticated transport and transactional directory update protocol exist, replica preparation is an isolated development primitive.

## Revision-checked replica promotion

`OGLAssetReplicaCoordinator.ConfirmReplica` can promote a replica node into the directory only if the caller supplies the current asset revision and an acknowledged hash matching the current content. It rejects duplicate primary/replica identities and enforces the existing 32-replica limit. SQLite directory CAS prevents stale concurrent updates. **The acknowledgement is only a caller-provided value, not an authenticated receipt:** there is no network transport, mTLS verification, signed receipt, remote durability proof, or retry/reconciliation worker. This component must only be called by trusted in-process orchestration after independently verifying the remote node and receipt; it is not production-ready.

## Replica transport policy and receipt replay ledger

`OGLReplicaTransferPolicy` rejects non-HTTPS endpoint URIs, URI credentials/fragments, and payloads above an operator-configured size limit (maximum 1 GiB). `OGLAssetReplicaStaging` can optionally enforce this size bound before storing a replica. These are validation primitives, **not** an HTTP transport, redirect policy, mTLS setup, endpoint allowlist, streaming implementation or SSRF protection. Do not accept arbitrary URLs from remote users.

`OGLSqliteReplicaNonceLedger` persists single-use `(node_id, nonce)` records and `ConfirmSignedReplicaOnce` verifies a signed receipt before consuming its nonce and attempting directory CAS. Older promotion entry points bypass this ledger. A shared ledger, key rotation, authenticated receipt issuer, verified durable remote storage and safe nonce retention are still required before production use.

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
