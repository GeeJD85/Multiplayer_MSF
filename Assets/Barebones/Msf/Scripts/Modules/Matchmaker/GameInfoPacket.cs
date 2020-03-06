using Barebones.Networking;
using System.Collections.Generic;

namespace Barebones.MasterServer
{
    public class GameInfoPacket : SerializablePacket
    {
        public int Id { get; set; }
        public string Address { get; set; }
        public GameInfoType Type { get; set; }
        public string Name { get; set; }
        public bool IsPasswordProtected { get; set; }
        public int MaxPlayers { get; set; }
        public int OnlinePlayers { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        public GameInfoPacket()
        {
            Id = 0;
            Address = string.Empty;
            Name = string.Empty;
            Type = GameInfoType.Unknown;
            IsPasswordProtected = false;
            MaxPlayers = 0;
            OnlinePlayers = 0;
            Properties = new Dictionary<string, string>();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Address);
            writer.Write((int)Type);
            writer.Write(Name);

            writer.Write(IsPasswordProtected);
            writer.Write(MaxPlayers);
            writer.Write(OnlinePlayers);
            writer.Write(Properties);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Id = reader.ReadInt32();
            Address = reader.ReadString();
            Type = (GameInfoType)reader.ReadInt32();
            Name = reader.ReadString();

            IsPasswordProtected = reader.ReadBoolean();
            MaxPlayers = reader.ReadInt32();
            OnlinePlayers = reader.ReadInt32();
            Properties = reader.ReadDictionary();
        }

        public override string ToString()
        {
            return string.Format($"[GameInfo: id: {Id}, address: {Address}, players: {OnlinePlayers}/{MaxPlayers}, type: {Type}]");
        }
    }
}