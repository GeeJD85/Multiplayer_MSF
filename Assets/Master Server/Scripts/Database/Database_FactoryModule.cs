using Barebones.MasterServer;
using System;

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using LiteDB;
#endif

namespace GW.MasterServer
{
    public class Database_FactoryModule : BaseServerModule
    {
        public HelpBox _header = new HelpBox()
        {
            Text = "This script is a custom module, which sets up database accessors for the game"
        };

        public override void Initialize(IServer server)
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                Msf.Server.DbAccessors.SetAccessor<IAccountsDatabaseAccessor>(new Accounts_DatabaseAccessor(new LiteDatabase(@"accounts.db")));
                Msf.Server.DbAccessors.SetAccessor<IProfilesDatabaseAccessor>(new Profiles_DatabaseAccessor(new LiteDatabase(@"profiles.db")));
            }
            catch (Exception e)
            {
                logger.Error("Failed to setup LiteDB");
                logger.Error(e);
            }
#endif
        }
    }
}