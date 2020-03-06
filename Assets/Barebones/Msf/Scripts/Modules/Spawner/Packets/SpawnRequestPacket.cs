using Barebones.Networking;
using System.Collections.Generic;

namespace Barebones.MasterServer
{
    public class SpawnRequestPacket : SerializablePacket
    {
        public int SpawnerId { get; set; }
        public int SpawnId { get; set; }
        public string SpawnCode { get; set; } = string.Empty;
        public string CustomArgs { get; set; } = string.Empty;
        public string OverrideExePath { get; set; } = string.Empty;
        public Dictionary<string, string> Properties { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(SpawnerId);
            writer.Write(SpawnId);
            writer.Write(SpawnCode);
            writer.Write(CustomArgs);
            writer.Write(OverrideExePath);
            writer.Write(Properties);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            SpawnerId = reader.ReadInt32();
            SpawnId = reader.ReadInt32();
            SpawnCode = reader.ReadString();
            CustomArgs = reader.ReadString();
            OverrideExePath = reader.ReadString();
            Properties = reader.ReadDictionary();
        }
    }
}