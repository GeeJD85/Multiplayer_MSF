using Barebones.Logging;
using Barebones.Networking;
using System;
using System.Collections.Generic;

namespace Barebones.MasterServer
{
    public class SpawnRequestController
    {
        /// <summary>
        /// Current connection
        /// </summary>
        private readonly IClientSocket connection;

        /// <summary>
        /// Current spawn id
        /// </summary>
        public int SpawnId { get; private set; }

        /// <summary>
        /// Current spawn status
        /// </summary>
        public SpawnStatus Status { get; private set; }

        /// <summary>
        /// A dictionary of options that user provided when requesting a 
        /// process to be spawned
        /// </summary>
        public Dictionary<string, string> SpawnOptions { get; private set; }

        /// <summary>
        /// Fires when spawn status changed
        /// </summary>
        public event Action<SpawnStatus> OnStatusChangedEvent;

        /// <summary>
        /// Create new <see cref="SpawnRequestController"/> instance
        /// </summary>
        /// <param name="spawnId"></param>
        /// <param name="connection"></param>
        /// <param name="spawnOptions"></param>
        public SpawnRequestController(int spawnId, IClientSocket connection, Dictionary<string, string> spawnOptions)
        {
            this.connection = connection;
            SpawnId = spawnId;
            SpawnOptions = spawnOptions;

            // Set handlers
            connection.SetHandler((short)MsfMessageCodes.SpawnRequestStatusChange, StatusUpdateHandler);
        }

        /// <summary>
        /// Abort current spawn process by Id
        /// </summary>
        public void Abort()
        {
            Msf.Client.Spawners.AbortSpawn(SpawnId);
        }

        /// <summary>
        /// Abort current spawn process by Id
        /// </summary>
        /// <param name="handler"></param>
        public void Abort(MsfSpawnersClient.AbortSpawnHandler handler)
        {
            Msf.Client.Spawners.AbortSpawn(SpawnId, handler);
        }

        /// <summary>
        /// Retrieves data, which was given to master server by a spawned process,
        /// which was finalized
        /// </summary>
        public void GetFinalizationData(MsfSpawnersClient.FinalizationDataHandler handler)
        {
            Msf.Client.Spawners.GetFinalizationData(SpawnId, handler, connection);
        }

        /// <summary>
        /// Fires when new status received
        /// </summary>
        /// <param name="message"></param>
        private static void StatusUpdateHandler(IIncommingMessage message)
        {
            var data = message.Deserialize(new SpawnStatusUpdatePacket());

            if(Msf.Client.Spawners.TryGetRequestController(data.SpawnId, out SpawnRequestController controller))
            {
                controller.Status = data.Status;
                controller.OnStatusChangedEvent?.Invoke(data.Status);
            }
        }
    }
}