using System;
using Aevien.Utilities;
using Barebones.Logging;
using Barebones.MasterServer;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    public class BaseSpawnerBehaviour : Singleton<BaseSpawnerBehaviour>
    {
        protected SpawnerController spawnerController;
        protected Logging.Logger logger;

        [SerializeField]
        private HelpBox headerEditor = new HelpBox()
        {
            Text = "This creates and registers a spawner, which can spawn " +
                   "game servers and other processes",
            Type = HelpBoxType.Info
        };

        [SerializeField]
        private HelpBox headerWarn = new HelpBox()
        {
            Text = $"It will start ONLY if '{Msf.Args.Names.StartSpawner}' argument is found, or if StartSpawner() is called manually from your scripts",
            Type = HelpBoxType.Warning
        };

        [Header("General"), SerializeField, Tooltip("Log level of this script's logger")]
        protected LogLevel logLevel = LogLevel.Info;
        [SerializeField, Tooltip("Log level of internal SpawnerController logger")]
        protected LogLevel spawnerLogLevel = LogLevel.Warn;

        [Header("Spawner Default Options"), SerializeField, Tooltip("Default IP address")]
        protected string machineIp = "127.0.0.1";
        [SerializeField, Tooltip("Default path to executable file")]
        protected string executableFilePath = "";
        [SerializeField, Tooltip("Use this to set whether or not to spawn room/server in headless mode.")]
        protected bool spawnInBatchmode = false;
        [SerializeField, Tooltip("Max number of rooms/server SpawnerController can run")]
        protected int maxProcesses = 5;
        [SerializeField, Tooltip("Use this to set whether or not to spawn room/server for browser games. This feature works only if game server uses websocket transport for connections")]
        protected bool spawnWebSocketServers = false;

        [Header("Runtime Settings"), SerializeField, Tooltip("If true, kills all spawned processes when master server quits")]
        protected bool killProcessesWhenAppQuits = true;

        [Header("Running in Editor"), SerializeField, Tooltip("If true, when running in editor, spawner server will start automatically (after connecting to master)")]
        protected bool autoStartInEditor = true;
        [SerializeField, Tooltip("If true, and if running in editor, path to executable will be overriden, and a value from 'exePathFromEditor' will be used.")]
        protected bool overrideExePathInEditor = true;
        [SerializeField, Tooltip("Path to the executable to be spawned as server")]
        protected string exePathFromEditor = "C:/Please set your own path";

        /// <summary>
        /// Check if spawner is ready to create rooms/servers
        /// </summary>
        public bool IsSpawnerStarted { get; protected set; } = false;

        protected override void Awake()
        {
            base.Awake();

            logger = Msf.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;
        }

        protected virtual void Start()
        {
            // Subscribe to connection event
            Msf.Connection.AddConnectionListener(OnConnectedToMasterEventHandler, true);
            Msf.Connection.AddDisconnectionListener(OnDisconnectedFromMasterEventHandler, true);
        }

        protected virtual void OnApplicationQuit()
        {
            if (killProcessesWhenAppQuits)
            {
                SpawnerController.KillProcessesSpawnedWithDefaultHandler();
            }
        }

        protected virtual void OnDestroy()
        {
            // Remove listener
            Msf.Connection.RemoveConnectionListener(OnConnectedToMasterEventHandler);
            Msf.Connection.RemoveDisconnectionListener(OnDisconnectedFromMasterEventHandler);
        }

        protected virtual void OnConnectedToMasterEventHandler()
        {
            // If we want to start a spawner (cmd argument was found)
            if (Msf.Args.IsProvided(Msf.Args.Names.StartSpawner))
            {
                StartSpawner();
            }
            else if (autoStartInEditor && Msf.Runtime.IsEditor)
            {
                StartSpawner();
            }
        }

        protected virtual void OnDisconnectedFromMasterEventHandler()
        {
            SpawnerController.KillProcessesSpawnedWithDefaultHandler();
        }

        public virtual void StartSpawner()
        {
            if (!Msf.Connection.IsConnected)
            {
                logger.Error("Spawner cannot be started because of the lack of connection to the master.");
                return;
            }

            // In case we went from one scene to another, but we've already started the spawner
            if (IsSpawnerStarted)
            {
                return;
            }

            IsSpawnerStarted = true;

            var spawnerOptions = new SpawnerOptions
            {
                // If MaxProcesses count defined in cmd args
                MaxProcesses = Msf.Args.IsProvided(Msf.Args.Names.MaxProcesses) ? Msf.Args.MaxProcesses : maxProcesses
            };

            // If we're running in editor, and we want to override the executable path
            if (Msf.Runtime.IsEditor && overrideExePathInEditor)
            {
                executableFilePath = exePathFromEditor;
            }

            logger.Info("Registering as a spawner with options: \n" + spawnerOptions);

            // 1. Register the spawner
            Msf.Server.Spawners.RegisterSpawner(spawnerOptions, (spawnerController, error) =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    logger.Error($"Failed to create spawner: {error}");
                    return;
                }

                this.spawnerController = spawnerController;
                this.spawnerController.Logger.LogLevel = spawnerLogLevel;

                spawnerController.DefaultSpawnerSettings.UseWebSockets = Msf.Args.IsProvided(Msf.Args.Names.UseWebSockets)
                    ? Msf.Args.WebGl
                    : spawnWebSocketServers;

                // Set to run in batchmode
                if (spawnInBatchmode && !Msf.Args.DontSpawnInBatchmode)
                {
                    spawnerController.DefaultSpawnerSettings.SpawnInBatchmode = true;
                }

                // 2. Set the default executable path
                spawnerController.DefaultSpawnerSettings.ExecutablePath = Msf.Args.IsProvided(Msf.Args.Names.RoomExecutablePath) ?
                    Msf.Args.RoomExecutablePath : executableFilePath;

                // 3. Set the machine IP
                spawnerController.DefaultSpawnerSettings.MachineIp = Msf.Args.IsProvided(Msf.Args.Names.RoomIp) ?
                    Msf.Args.RoomIp : machineIp;

                // 4. (Optional) Set the method which does the spawning, if you want to
                // fully control how processes are spawned
                spawnerController.SetSpawnRequestHandler(SpawnRequestHandler);

                // 5. (Optional) Set the method, which kills processes when kill request is received
                spawnerController.SetKillRequestHandler(KillRequestHandler);

                logger.Info("Spawner successfully created. Id: " + spawnerController.SpawnerId);
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="spawnid"></param>
        private void KillRequestHandler(int spawnid)
        {
            spawnerController.DefaultKillSpawnedProcessRequestHandler(spawnid);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="message"></param>
        protected virtual void SpawnRequestHandler(SpawnRequestPacket packet, IIncommingMessage message)
        {
            spawnerController.DefaultSpawnRequestHandler(packet, message);
        }
    }
}