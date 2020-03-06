using Barebones.Logging;
using Barebones.Networking;
using System.Collections.Generic;
using System.Text;

namespace Barebones.MasterServer
{
    public delegate void ClientSpawnRequestCallback(SpawnRequestController controller, string error);

    public class MsfSpawnersClient : MsfBaseClient
    {
        public delegate void AbortSpawnHandler(bool isSuccessful, string error);
        public delegate void FinalizationDataHandler(Dictionary<string, string> data, string error);

        /// <summary>
        /// List of spawn request controllers
        /// </summary>
        private Dictionary<int, SpawnRequestController> _localSpawnRequests;

        /// <summary>
        /// Create instance of <see cref="MsfSpawnersClient"/> with connection
        /// </summary>
        /// <param name="connection"></param>
        public MsfSpawnersClient(IClientSocket connection) : base(connection)
        {
            _localSpawnRequests = new Dictionary<int, SpawnRequestController>();
        }

        /// <summary>
        /// Sends a request to master server, to spawn a process with given options
        /// </summary>
        /// <param name="options"></param>
        public void RequestSpawn(Dictionary<string, string> options)
        {
            RequestSpawn(options, new Dictionary<string, string>(), string.Empty, null, Connection);
        }

        /// <summary>
        /// Sends a request to master server, to spawn a process in a given region, and with given options
        /// </summary>
        /// <param name="options"></param>
        public void RequestSpawn(Dictionary<string, string> options, string region)
        {
            RequestSpawn(options, new Dictionary<string, string>(), region, null, Connection);
        }

        /// <summary>
        /// Sends a request to master server, to spawn a process in a given region, and with given options
        /// </summary>
        /// <param name="options"></param>
        /// <param name="region"></param>
        /// <param name="callback"></param>
        public void RequestSpawn(Dictionary<string, string> options, string region, ClientSpawnRequestCallback callback)
        {
            RequestSpawn(options, new Dictionary<string, string>(), region, callback, Connection);
        }

        /// <summary>
        /// Sends a request to master server, to spawn a process in a given region, and with given options
        /// </summary>
        /// <param name="options"></param>
        /// <param name="region"></param>
        /// <param name="callback"></param>
        public void RequestSpawn(Dictionary<string, string> options, Dictionary<string, string> customArgs, string region, ClientSpawnRequestCallback callback)
        {
            RequestSpawn(options, customArgs, region, callback, Connection);
        }

        /// <summary>
        /// Sends a request to master server, to spawn a process in a given region, and with given options
        /// </summary>
        /// <param name="options"></param>
        /// <param name="customArgs"></param>
        /// <param name="region"></param>
        /// <param name="callback"></param>
        public void RequestSpawn(Dictionary<string, string> options, Dictionary<string, string> customArgs, string region, ClientSpawnRequestCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback?.Invoke(null, "Not connected");
                return;
            }

            // Create spawn request message packet
            var packet = new ClientsSpawnRequestPacket()
            {
                Options = options,
                Region = region
            };

            if (customArgs != null && customArgs.Count > 0)
            {
                var customArgsSb = new StringBuilder();

                foreach (var kvp in customArgs)
                {
                    customArgsSb.Append($"{kvp.Key} {kvp.Value} ");
                }

                packet.CustomArgs = customArgsSb.ToString();
            }
            else
            {
                packet.CustomArgs = string.Empty;
            }

            // Send request to Master Server SpawnerModule
            connection.SendMessage((short)MsfMessageCodes.ClientsSpawnRequest, packet, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(null, response.AsString("Unknown error"));
                    return;
                }

                Logs.Debug($"Room [{options[MsfDictKeys.roomName]}] was successfuly started");

                // Spawn id
                var spawnId = response.AsInt();
                var controller = new SpawnRequestController(spawnId, connection, options);

                _localSpawnRequests[controller.SpawnId] = controller;

                callback?.Invoke(controller, null);
            });
        }

        /// <summary>
        /// Sends a request to abort spawn request, which was not yet finalized
        /// </summary>
        /// <param name="spawnId"></param>
        public void AbortSpawn(int spawnId)
        {
            AbortSpawn(spawnId, null, Connection);
        }

        /// <summary>
        /// Sends a request to abort spawn request, which was not yet finalized
        /// </summary>
        public void AbortSpawn(int spawnId, AbortSpawnHandler callback)
        {
            AbortSpawn(spawnId, callback, Connection);
        }

        /// <summary>
        /// Sends a request to abort spawn request, which was not yet finalized
        /// </summary>
        public void AbortSpawn(int spawnId, AbortSpawnHandler callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback?.Invoke(false, "Not connected");
                return;
            }

            connection.SendMessage((short)MsfMessageCodes.AbortSpawnRequest, spawnId, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(false, response.AsString("Unknown error"));
                    return;
                }

                Logs.Debug($"Room process [{spawnId}] was successfuly aborted");

                callback?.Invoke(true, null);
            });
        }

        /// <summary>
        /// Retrieves data, which was given to master server by a spawned process,
        /// which was finalized
        /// </summary>
        /// <param name="spawnId"></param>
        /// <param name="callback"></param>
        public void GetFinalizationData(int spawnId, FinalizationDataHandler callback)
        {
            GetFinalizationData(spawnId, callback, Connection);
        }

        /// <summary>
        /// Retrieves data, which was given to master server by a spawned process,
        /// which was finalized
        /// </summary>
        public void GetFinalizationData(int spawnId, FinalizationDataHandler callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(null, "Not connected");
                return;
            }

            connection.SendMessage((short)MsfMessageCodes.GetSpawnFinalizationData, spawnId, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(null, response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke(new Dictionary<string, string>().FromBytes(response.AsBytes()), null);
            });
        }

        /// <summary>
        /// Retrieves a specific spawn request controller
        /// </summary>
        /// <param name="spawnId"></param>
        /// <returns></returns>
        public SpawnRequestController GetRequestController(int spawnId)
        {
            _localSpawnRequests.TryGetValue(spawnId, out SpawnRequestController controller);
            return controller;
        }

        /// <summary>
        /// Retrieves a specific spawn request controller
        /// </summary>
        /// <param name="spawnId"></param>
        /// <returns></returns>
        public bool TryGetRequestController(int spawnId, out SpawnRequestController controller)
        {
            controller = GetRequestController(spawnId);
            return controller != null;
        }
    }
}