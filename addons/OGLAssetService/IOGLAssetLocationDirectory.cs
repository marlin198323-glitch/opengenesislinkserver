namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Revision-checked asset location operations. Implementations must return
    /// detached records and reject stale expected revisions.
    /// </summary>
    public interface IOGLAssetLocationDirectory
    {
        OGLAssetLocationDirectory.Entry Find(string assetId);
        OGLAssetLocationDirectory.Entry Publish(
            OGLAssetLocationDirectory.Entry candidate, long expectedRevision);
    }
}
