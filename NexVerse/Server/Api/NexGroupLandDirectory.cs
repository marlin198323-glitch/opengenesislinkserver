// SPDX-License-Identifier: MPL-2.0
// Grid-wide group-owned land is authoritative in the shared region SQL store.
// This is deliberately read-only; no tables or migrations are created.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using NexVerse.Core.Economy;
using OpenMetaverse;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    internal sealed class NexGroupLandDirectory
    {
        private readonly NexLedgerSqlRuntime m_Storage;
        private readonly IGridService m_Grid;

        public NexGroupLandDirectory(
            string storageProvider,
            string connectionString,
            IGridService grid)
        {
            m_Storage = NexLedgerSqlRuntime.Resolve(
                storageProvider, connectionString);
            m_Grid = grid ??
                throw new ArgumentNullException(nameof(grid));
        }

        public IReadOnlyList<NexGroupLandEntry> List(UUID groupId, int limit = 500)
        {
            if (groupId.IsZero())
                throw new ArgumentException("A valid group UUID is required.", nameof(groupId));
            if (limit < 1 || limit > 500)
                throw new ArgumentOutOfRangeException(nameof(limit));

            using DbConnection connection = m_Storage.CreateConnection();
            connection.Open();

            using DbCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT UUID, RegionUUID, OwnerUUID, Name, Description, " +
                "Area, SalePrice, SnapshotUUID, UserLocationX, " +
                "UserLocationY, UserLocationZ " +
                "FROM land WHERE IsGroupOwned = 1 AND GroupUUID = @group " +
                "ORDER BY RegionUUID, UUID LIMIT @max";
            DbParameter groupParam = command.CreateParameter();
            groupParam.ParameterName = "@group";
            groupParam.Value = groupId.ToString();
            command.Parameters.Add(groupParam);
            DbParameter maxParam = command.CreateParameter();
            maxParam.ParameterName = "@max";
            maxParam.Value = limit;
            command.Parameters.Add(maxParam);

            List<NexGroupLandEntry> parcels = new List<NexGroupLandEntry>();
            using DbDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!UUID.TryParse(ReadString(reader, "RegionUUID"), out UUID regionId) ||
                    regionId.IsZero() ||
                    !UUID.TryParse(ReadString(reader, "UUID"), out UUID parcelId) ||
                    parcelId.IsZero())
                    continue;

                // Only include regions still registered in this grid.
                GridRegion region = m_Grid.GetRegionByUUID(UUID.Zero, regionId);
                if (region == null)
                    continue;

                UUID.TryParse(ReadString(reader, "OwnerUUID"), out UUID owner);
                UUID.TryParse(ReadString(reader, "SnapshotUUID"), out UUID snapshot);
                float x = ReadFloat(reader, "UserLocationX");
                float y = ReadFloat(reader, "UserLocationY");
                float z = ReadFloat(reader, "UserLocationZ");
                x = float.IsFinite(x) ? Math.Clamp(x, 0, Math.Max(0, region.RegionSizeX)) : 0;
                y = float.IsFinite(y) ? Math.Clamp(y, 0, Math.Max(0, region.RegionSizeY)) : 0;
                z = float.IsFinite(z) ? Math.Max(0, z) : 0;

                parcels.Add(new NexGroupLandEntry
                {
                    ParcelId = parcelId,
                    RegionId = regionId,
                    OwnerId = owner,
                    SnapshotId = snapshot,
                    Name = ReadString(reader, "Name"),
                    Description = ReadString(reader, "Description"),
                    RegionName = region.RegionName,
                    Area = Math.Max(0, ReadInt(reader, "Area")),
                    SalePrice = Math.Max(0, ReadInt(reader, "SalePrice")),
                    GlobalX = checked((uint)Math.Max(0, region.RegionLocX) + (uint)x),
                    GlobalY = checked((uint)Math.Max(0, region.RegionLocY) + (uint)y),
                    GlobalZ = (uint)Math.Min(uint.MaxValue, z)
                });
            }
            return parcels;
        }

        private static string ReadString(DbDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value || value == null ?
                string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static float ReadFloat(DbDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value || value == null ? 0 :
                Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }

        private static int ReadInt(DbDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value || value == null ? 0 :
                Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }

    internal sealed class NexGroupLandEntry
    {
        public UUID ParcelId { get; set; }
        public UUID RegionId { get; set; }
        public UUID OwnerId { get; set; }
        public UUID SnapshotId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RegionName { get; set; } = string.Empty;
        public int Area { get; set; }
        public int SalePrice { get; set; }
        public uint GlobalX { get; set; }
        public uint GlobalY { get; set; }
        public uint GlobalZ { get; set; }
    }
}
