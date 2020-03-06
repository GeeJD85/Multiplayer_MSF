using Aevien.Utilities;
using Barebones.Networking;
using System;
using System.Collections.Generic;

namespace Barebones.MasterServer
{
    public class AccountInfoPacket : SerializablePacket
    {
        public string Username { get; private set; }
        public string Email { get; private set; }
        public bool IsAdmin { get; private set; }
        public bool IsGuest { get; private set; }
        public bool IsEmailConfirmed { get; private set; }
        public Dictionary<string, string> Properties { get; private set; }

        public AccountInfoPacket() { }

        public AccountInfoPacket(IAccountInfoData account)
        {
            Username = account.Username;
            Email = account.Email;
            IsAdmin = account.IsAdmin;
            IsGuest = account.IsGuest;
            IsEmailConfirmed = account.IsEmailConfirmed;
            Properties = account.Properties;
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Username);
            writer.Write(Email);
            writer.Write(IsAdmin);
            writer.Write(IsGuest);
            writer.Write(IsEmailConfirmed);
            writer.Write(Properties);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Username = reader.ReadString();
            Email = reader.ReadString();
            IsAdmin = reader.ReadBoolean();
            IsGuest = reader.ReadBoolean();
            IsEmailConfirmed = reader.ReadBoolean();
            Properties = reader.ReadDictionary();
        }
    }
}