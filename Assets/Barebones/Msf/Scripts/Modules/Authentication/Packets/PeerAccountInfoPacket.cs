using Barebones.Networking;
using System.Collections.Generic;

namespace Barebones.MasterServer
{
    public class PeerAccountInfoPacket : SerializablePacket
    {
        public int PeerId { get; set; }
        public string Username { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(PeerId);
            writer.Write(Username);
            writer.Write(Properties);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            PeerId = reader.ReadInt32();
            Username = reader.ReadString();
            Properties = reader.ReadDictionary();
        }

        public override string ToString()
        {
            return string.Format($"[Peer account info: Peer ID: {PeerId}, Username: {Username}, Properties: {Properties.ToReadableString()}]");
        }
    }
}