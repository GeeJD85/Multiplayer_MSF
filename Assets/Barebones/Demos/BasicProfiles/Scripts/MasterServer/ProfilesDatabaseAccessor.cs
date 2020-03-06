#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

using Barebones.MasterServer;
using LiteDB;

namespace Barebones.MasterServer.Examples.BasicProfile
{
    public class ProfilesDatabaseAccessor : IProfilesDatabaseAccessor
    {
        private readonly LiteCollection<ProfileInfoData> profiles;
        private readonly LiteDatabase database;

        public ProfilesDatabaseAccessor(LiteDatabase database)
        {
            this.database = database;

            profiles = this.database.GetCollection<ProfileInfoData>("profiles");
            profiles.EnsureIndex(a => a.Username, true);
        }

        /// <summary>
        /// Get profile info from database
        /// </summary>
        /// <param name="profile"></param>
        public void RestoreProfile(ObservableServerProfile profile)
        {
            var data = FindOrCreateData(profile);
            profile.FromBytes(data.Data);
        }

        /// <summary>
        /// Update profile info in database
        /// </summary>
        /// <param name="profile"></param>
        public void UpdateProfile(ObservableServerProfile profile)
        {
            var data = FindOrCreateData(profile);
            data.Data = profile.ToBytes();
            profiles.Update(data);
        }

        /// <summary>
        /// Find profile data in database or create new data and insert them to database
        /// </summary>
        /// <param name="profile"></param>
        /// <returns></returns>
        private ProfileInfoData FindOrCreateData(ObservableServerProfile profile)
        {
            var data = profiles.FindOne(a => a.Username == profile.Username);

            if (data == null)
            {
                data = new ProfileInfoData()
                {
                    Username = profile.Username,
                    Data = profile.ToBytes()
                };

                profiles.Insert(data);
            }

            return data;
        }

        /// <summary>
        /// LiteDB profile data implementation
        /// </summary>
        private class ProfileInfoData
        {
            [BsonId]
            public string Username { get; set; }
            public byte[] Data { get; set; }
        }
    }
}

#endif