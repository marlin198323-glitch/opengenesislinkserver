# FSAssets staging cutover and rollback gate

**Status:** staging procedure only. No automated production cutover. Do not use this document as authorization to switch a live ROBUST service.

## Preconditions

1. Record the source server, exact database engine/version, database name, configured FSAssets `Realm`, `BaseDirectory`, `SpoolDirectory`, and `UseOsgridFormat`. Identify every ROBUST and simulator process that can write assets.
2. Schedule downtime. Stop **all** asset writers. Confirm no active writes and drain the FSAssets spool using the existing service's supported shutdown/drain procedure. Do not treat an undrained spool as an immutable FSAssets snapshot.
3. Create an immutable, timestamped copy of the complete FSAssets directory **and** a consistent SQL database backup. Keep the original source untouched. Document backup paths, snapshot timestamp, permissions and checksums.
4. Restore the SQL backup into a **separate staging database** using native engine tooling (`mariadb-dump`/`mysql` or `pg_dump`/`pg_restore`). The UUID/hash TSV audit is *not* a database backup and cannot restore names, descriptions, flags or timestamps.
5. Export UUID/hash from that staging database using [EXPORT.md](EXPORT.md). Capture an independent `COUNT(*)` from the **same restored database**.
6. Run `--verify-metadata ... --expected-rows <count>` against the **copied** FSAssets snapshot. Require zero invalid rows, duplicate IDs, conflicts, missing files and hash mismatches. Record JSON and CLI exit code.
7. Copy files to a separate destination with the migration CLI in dry-run mode first, then `--copy`. Run the metadata audit again against the destination and compare counts and hashes.
8. Start a staging ROBUST/AssetService instance with isolated network endpoints and credentials, pointing to the restored staging database and copied FSAssets destination. Test representative assets through `Get`, `GetMetadata`, and `GetData`, including shared hashes and different asset types. Verify correct flags and metadata. Record results.
9. Only after a successful, reviewed staging run should an operator prepare a separate production change request, rollback window and explicit go/no-go decision. No code in this addon currently performs the live switchover.

## Abort conditions

Stop before cutover on any unexpected SQL row-count difference, failed database restore, FSAssets spool backlog, SHA-256 mismatch, invalid/duplicate UUID, missing asset, filesystem permission error, failed staging readback, unexpected asset metadata, or Redis invalidation failure.

## Rollback contract (manual until automated and exercised)

- Preserve the original database backup, original FSAssets directory, old service configuration and deployed binaries until after a defined acceptance period.
- Stop writes before any rollback; never point two live writers at the same asset database/spool.
- Restore the **original** database and original filesystem snapshot as a matched pair; restore the old service config and binaries.
- Reopen the service only after `Get`/`GetMetadata`/`GetData` checks and a metadata audit succeed on the restored original state.
- **Any writes after the snapshot require an explicit reconciliation plan**; simply restoring the snapshot discards them. If this cannot be guaranteed, do not attempt a live cutover.

## Evidence checklist

- [ ] Consistent source DB and filesystem snapshots retained
- [ ] Source writer and spool drain verified
- [ ] Native SQL backup restored successfully into staging
- [ ] Full schema and row counts validated
- [ ] Offline UUID/hash audit passed on source snapshot and destination copy
- [ ] Staging service readback passed for all supported asset types
- [ ] Rollback exercised against a disposable staging environment
- [ ] Rollback window, ownership and post-cutover write policy approved

Current CI includes disposable native MariaDB and PostgreSQL SQL backup/restore fixtures, provider-specific metadata comparisons and file/hash auditing. These fixtures **do not** validate the production schema, spool consistency, staging service readback or a production rollback.
