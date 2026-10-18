using System;
using System.IO;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Immutable replica preparation. Does not advertise replica availability,
    /// authenticate nodes or expose a remote API.
    /// </summary>
    public sealed class OGLAssetReplicaStaging
    {
        private readonly OGLNodeObjectStore objects;

        public OGLAssetReplicaStaging(OGLNodeObjectStore objects)
        {
            this.objects = objects ?? throw new ArgumentNullException(nameof(objects));
        }

        public string Prepare(byte[] payload, string expectedHash)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (String.IsNullOrWhiteSpace(expectedHash))
                throw new ArgumentException("Expected hash required.", nameof(expectedHash));
            string actual = OGLObjectCache.ContentHash(payload);
            if (!String.Equals(actual, expectedHash, StringComparison.Ordinal))
                throw new InvalidDataException("Replica SHA-256 mismatch.");
            return objects.Put(payload);
        }

        public byte[] Read(string hash) => objects.Get(hash);
    }
}
