using Barebones.Networking;
using System.Collections.Generic;

namespace Barebones.MasterServer
{
    public class ClientsSpawnRequestPacket : SerializablePacket
    {
        public string Region { get; set; } = string.Empty;
        public Dictionary<string, string> Options { get; set; }
        public string CustomArgs { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Region);
            writer.Write(Options);
            writer.Write(CustomArgs);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Region = reader.ReadString();
            Options = reader.ReadDictionary();
            CustomArgs = reader.ReadString();
        }
    }
}