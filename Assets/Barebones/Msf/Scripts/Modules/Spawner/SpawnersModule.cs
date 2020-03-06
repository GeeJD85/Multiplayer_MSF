using Barebones.Logging;
using Barebones.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Barebones.MasterServer
{
    public class SpawnersModule : BaseServerModule
    {
        public delegate void SpawnedProcessRegistrationHandler(SpawnTask task, IPeer peer);

        private int _spawnerId = 0;
        private int _spawnTaskId = 0;

        protected Dictionary<int, RegisteredSpawner> spawnersList;
        protected Dictionary<int, SpawnTask> spawnTasksList;

        [Header("Permissions"), SerializeField, Tooltip("Minimal permission level, necessary to register a spanwer")]
        protected int createSpawnerPermissionLevel = 0;

        [Tooltip("How often spawner queues are updated"), SerializeField]
        protected float queueUpdateFrequency = 0.1f;

        [Tooltip("If true, clients will be able to request spawns"), SerializeField]
        protected bool enableClientSpawnRequests = true;

        public event Action<RegisteredSpawner> OnSpawnerRegisteredEvent;
        public event Action<RegisteredSpawner> OnSpawnerDestroyedEvent;
        public event SpawnedProcessRegistrationHandler OnSpawnedProcessRegisteredEvent;

        public override void Initialize(IServer server)
        {
            spawnersList = new Dictionary<int, RegisteredSpawner>();
            spawnTasksList = new Dictionary<int, SpawnTask>();

            // Add handlers
            server.SetHandler((short)MsfMessageCodes.RegisterSpawner, RegisterSpawnerRequestHandler);
            server.SetHandler((short)MsfMessageCodes.ClientsSpawnRequest, ClientsSpawnRequestHandler);
            server.SetHandler((short)MsfMessageCodes.RegisterSpawnedProcess, RegisterSpawnedProcessRequestHandler);
            server.SetHandler((short)MsfMessageCodes.CompleteSpawnProcess, CompleteSpawnProcessRequestHandler);
            server.SetHandler((short)MsfMessageCodes.ProcessStarted, SetProcessStartedRequestHandler);
            server.SetHandler((short)MsfMessageCodes.ProcessKilled, SetProcessKilledRequestHandler);
            server.SetHandler((short)MsfMessageCodes.AbortSpawnRequest, AbortSpawnRequestHandler);
            server.SetHandler((short)MsfMessageCodes.GetSpawnFinalizationData, GetCompletionDataRequestHandler);
            server.SetHandler((short)MsfMessageCodes.UpdateSpawnerProcessesCount, SetSpawnedProcessesCountRequestHandler);

            // Coroutines
            StartCoroutine(StartQueueUpdater());
        }

        public virtual RegisteredSpawner CreateSpawner(IPeer peer, SpawnerOptions options)
        {
            var spawner = new RegisteredSpawner(GenerateSpawnerId(), peer, options);

            Dictionary<int, RegisteredSpawner> peerSpawners = peer.GetProperty((int)MsfPropCodes.RegisteredSpawners) as Dictionary<int, RegisteredSpawner>;

            // If this is the first time registering a spawners
            if (peerSpawners == null)
            {
                // Save the dictionary
                peerSpawners = new Dictionary<int, RegisteredSpawner>();
                peer.SetProperty((int)MsfPropCodes.RegisteredSpawners, peerSpawners);

                peer.OnPeerDisconnectedEvent += OnRegisteredPeerDisconnect;
            }

            // Add a new spawner
            peerSpawners[spawner.SpawnerId] = spawner;

            // Add the spawner to a list of all spawners
            spawnersList[spawner.SpawnerId] = spawner;

            // Invoke the event
            if (OnSpawnerRegisteredEvent != null)
            {
                OnSpawnerRegisteredEvent.Invoke(spawner);
            }

            return spawner;
        }

        private void OnRegisteredPeerDisconnect(IPeer peer)
        {
            var peerSpawners = peer.GetProperty((int)MsfPropCodes.RegisteredSpawners) as Dictionary<int, RegisteredSpawner>;

            if (peerSpawners == null)
            {
                return;
            }

            // Create a copy so that we can iterate safely
            var registeredSpawners = peerSpawners.Values.ToList();

            foreach (var registeredSpawner in registeredSpawners)
            {
                DestroySpawner(registeredSpawner);
            }
        }

        public void DestroySpawner(RegisteredSpawner spawner)
        {
            var peer = spawner.Peer;

            if (peer != null)
            {
                var peerRooms = peer.GetProperty((int)MsfPropCodes.RegisteredSpawners) as Dictionary<int, RegisteredSpawner>;

                // Remove the spawner from peer
                if (peerRooms != null)
                {
                    peerRooms.Remove(spawner.SpawnerId);
                }
            }

            // Remove the spawner from all spawners
            spawnersList.Remove(spawner.SpawnerId);

            // Invoke the event
            if (OnSpawnerDestroyedEvent != null)
            {
                OnSpawnerDestroyedEvent.Invoke(spawner);
            }
        }

        public int GenerateSpawnerId()
        {
            return _spawnerId++;
        }

        public int GenerateSpawnTaskId()
        {
            return _spawnTaskId++;
        }

        public SpawnTask Spawn(Dictionary<string, string> properties)
        {
            return Spawn(properties, "", "");
        }

        public SpawnTask Spawn(Dictionary<string, string> properties, string region)
        {
            return Spawn(properties, region, "");
        }

        public virtual SpawnTask Spawn(Dictionary<string, string> properties, string region, string customArgs)
        {
            var spawners = GetFilteredSpawners(properties, region);

            if (spawners.Count < 0)
            {
                logger.Warn("No spawner was returned after filtering. " + (string.IsNullOrEmpty(region) ? "" : "Region: " + region));
                return null;
            }

            // Order from least busy server
            var orderedSpawners = spawners.OrderByDescending(s => s.CalculateFreeSlotsCount());
            var availableSpawner = orderedSpawners.FirstOrDefault(s => s.CanSpawnAnotherProcess());

            // Ignore, if all of the spawners are busy
            if (availableSpawner == null)
            {
                return null;
            }

            return Spawn(properties, customArgs, availableSpawner);
        }

        /// <summary>
        /// Requests a specific spawner to spawn a process
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="customArgs"></param>
        /// <param name="spawner"></param>
        /// <returns></returns>
        public virtual SpawnTask Spawn(Dictionary<string, string> properties, string customArgs, RegisteredSpawner spawner)
        {
            var task = new SpawnTask(GenerateSpawnTaskId(), spawner, properties, customArgs);

            spawnTasksList[task.SpawnId] = task;

            spawner.AddTaskToQueue(task);

            logger.Debug("Spawner was found, and spawn task created: " + task);

            return task;
        }

        /// <summary>
        /// Retrieves a list of spawner that can be used with given properties and region name
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        public virtual List<RegisteredSpawner> GetFilteredSpawners(Dictionary<string, string> properties, string region)
        {
            return GetSpawners(region);
        }

        public virtual List<RegisteredSpawner> GetSpawners()
        {
            return GetSpawners(null);
        }

        public virtual List<RegisteredSpawner> GetSpawners(string region)
        {
            // If region is not provided, retrieve all spawners
            if (string.IsNullOrEmpty(region))
            {
                return spawnersList.Values.ToList();
            }

            return GetSpawnersInRegion(region);
        }

        public virtual List<RegisteredSpawner> GetSpawnersInRegion(string region)
        {
            return spawnersList.Values
                .Where(s => s.Options.Region == region)
                .ToList();
        }

        /// <summary>
        /// Returns true, if peer has permissions to register a spawner
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        protected virtual bool HasCreationPermissions(IPeer peer)
        {
            var extension = peer.GetExtension<SecurityInfoPeerExtension>();

            return extension.PermissionLevel >= createSpawnerPermissionLevel;
        }

        protected virtual bool CanClientSpawn(IPeer peer, ClientsSpawnRequestPacket data)
        {
            return enableClientSpawnRequests;
        }

        protected virtual IEnumerator StartQueueUpdater()
        {
            while (true)
            {
                yield return new WaitForSeconds(queueUpdateFrequency);

                foreach (var spawner in spawnersList.Values)
                {
                    try
                    {
                        spawner.UpdateQueue();
                    }
                    catch (Exception e)
                    {
                        logger.Error(e);
                    }
                }
            }
        }

        #region Message Handlers

        protected virtual void ClientsSpawnRequestHandler(IIncommingMessage message)
        {
            var data = message.Deserialize(new ClientsSpawnRequestPacket());
            var peer = message.Peer;

            logger.Info($"Client {peer.Id} requested to spawn room {data.Options[MsfDictKeys.roomName]}");

            // Check if current request is authorized
            if (!CanClientSpawn(peer, data))
            {
                // Client can't spawn
                message.Respond("Unauthorized", ResponseStatus.Unauthorized);
                return;
            }

            // Try to find existing request to prevent new one
            SpawnTask prevRequest = peer.GetProperty((int)MsfPropCodes.ClientSpawnRequest) as SpawnTask;

            if (prevRequest != null && !prevRequest.IsDoneStartingProcess)
            {
                // Client has unfinished request
                message.Respond("You already have an active request", ResponseStatus.Failed);
                return;
            }

            // Create a new spawn task
            var task = Spawn(data.Options, data.Region, data.CustomArgs);

            // If spawn task is not created
            if (task == null)
            {
                message.Respond("All the servers are busy. Try again later".ToBytes(), ResponseStatus.Failed);
                return;
            }

            // Save spawn task requester
            task.Requester = message.Peer;

            // Save the task
            peer.SetProperty((int)MsfPropCodes.ClientSpawnRequest, task);

            // Listen to status changes
            task.OnStatusChangedEvent += (status) =>
            {
                // Send status update
                var msg = Msf.Create.Message((short)MsfMessageCodes.SpawnRequestStatusChange, new SpawnStatusUpdatePacket()
                {
                    SpawnId = task.SpawnId,
                    Status = status
                });

                message.Peer.SendMessage(msg);
            };

            message.Respond(task.SpawnId, ResponseStatus.Success);
        }

        private void AbortSpawnRequestHandler(IIncommingMessage message)
        {
            var prevRequest = message.Peer.GetProperty((int)MsfPropCodes.ClientSpawnRequest) as SpawnTask;

            if (prevRequest == null)
            {
                message.Respond("There's nothing to abort", ResponseStatus.Failed);
                return;
            }

            if (prevRequest.Status >= SpawnStatus.Finalized)
            {
                message.Respond("You can't abort a completed request", ResponseStatus.Failed);
                return;
            }

            if (prevRequest.Status <= SpawnStatus.None)
            {
                message.Respond("Already aborting", ResponseStatus.Success);
                return;
            }

            prevRequest.Abort();

            message.Respond(ResponseStatus.Success);
        }

        protected virtual void GetCompletionDataRequestHandler(IIncommingMessage message)
        {
            var spawnId = message.AsInt();

            if (!spawnTasksList.TryGetValue(spawnId, out SpawnTask task))
            {
                message.Respond("Invalid request", ResponseStatus.Failed);
                return;
            }

            if (task.Requester != message.Peer)
            {
                message.Respond("You're not the requester", ResponseStatus.Unauthorized);
                return;
            }

            if (task.FinalizationPacket == null)
            {
                message.Respond("Task has no completion data", ResponseStatus.Failed);
                return;
            }

            // Respond with data (dictionary of strings)
            message.Respond(task.FinalizationPacket.FinalizationData.ToBytes(), ResponseStatus.Success);
        }

        protected virtual void RegisterSpawnerRequestHandler(IIncommingMessage message)
        {
            if (!HasCreationPermissions(message.Peer))
            {
                message.Respond("Insufficient permissions", ResponseStatus.Unauthorized);
                return;
            }

            var options = message.Deserialize(new SpawnerOptions());

            var spawner = CreateSpawner(message.Peer, options);

            // Respond with spawner id
            message.Respond(spawner.SpawnerId, ResponseStatus.Success);
        }

        /// <summary>
        /// Handles a message from spawned process. Spawned process send this message
        /// to notify server that it was started
        /// </summary>
        /// <param name="message"></param>
        protected virtual void RegisterSpawnedProcessRequestHandler(IIncommingMessage message)
        {
            var data = message.Deserialize(new RegisterSpawnedProcessPacket());

            if (!spawnTasksList.TryGetValue(data.SpawnId, out SpawnTask task))
            {
                message.Respond("Invalid spawn task", ResponseStatus.Failed);
                logger.Error("Process tried to register to an unknown task");
                return;
            }

            if (task.UniqueCode != data.SpawnCode)
            {
                message.Respond("Unauthorized", ResponseStatus.Unauthorized);
                logger.Error("Spawned process tried to register, but failed due to mismaching unique code");
                return;
            }

            task.OnRegistered(message.Peer);

            OnSpawnedProcessRegisteredEvent?.Invoke(task, message.Peer);

            message.Respond(task.Properties.ToBytes(), ResponseStatus.Success);
        }

        protected virtual void CompleteSpawnProcessRequestHandler(IIncommingMessage message)
        {
            var data = message.Deserialize(new SpawnFinalizationPacket());

            if (spawnTasksList.TryGetValue(data.SpawnId, out SpawnTask task))
            {
                if (task.RegisteredPeer != message.Peer)
                {
                    message.Respond("Unauthorized", ResponseStatus.Unauthorized);
                    logger.Error("Spawned process tried to complete spawn task, but it's not the same peer who registered to the task");
                }
                else
                {
                    task.OnFinalized(data);
                    message.Respond(ResponseStatus.Success);
                }
            }
            else
            {
                message.Respond("Invalid spawn task", ResponseStatus.Failed);
                logger.Error("Process tried to complete to an unknown task");
            }
        }

        protected virtual void SetProcessKilledRequestHandler(IIncommingMessage message)
        {
            var spawnId = message.AsInt();

            if (spawnTasksList.TryGetValue(spawnId, out SpawnTask task))
            {
                task.OnProcessKilled();
                task.Spawner.OnProcessKilled();
            }
        }

        protected virtual void SetProcessStartedRequestHandler(IIncommingMessage message)
        {
            var spawnId = message.AsInt();

            if(spawnTasksList.TryGetValue(spawnId, out SpawnTask task))
            {
                task.OnProcessStarted();
                task.Spawner.OnProcessStarted();
            }
        }

        private void SetSpawnedProcessesCountRequestHandler(IIncommingMessage message)
        {
            var packet = message.Deserialize(new IntPairPacket());

            if (spawnersList.TryGetValue(packet.A, out RegisteredSpawner spawner))
            {
                spawner.UpdateProcessesCount(packet.B);
            }
        }

        #endregion
    }
}