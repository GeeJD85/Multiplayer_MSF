using System;
using System.Linq;

namespace Barebones.MasterServer
{
    public class MsfArgs
    {
        private readonly string[] _args;

        public MsfArgNames Names;

        public MsfArgs()
        {
            _args = Environment.GetCommandLineArgs();

            // Android fix
            if (_args == null)
            {
                _args = Array.Empty<string>();
            }

            Names = new MsfArgNames();

            StartMaster = IsProvided(Names.StartMaster);
            AutoConnectClient = IsProvided(Names.StartClientConnection);
            DestroyUi = IsProvided(Names.DestroyUi);

            MasterPort = ExtractValueInt(Names.MasterPort, 5000);
            MasterIp = ExtractValue(Names.MasterIp);

            RoomIp = ExtractValue(Names.RoomIp, "127.0.0.1");
            RoomPort = ExtractValueInt(Names.RoomPort, 7777);
            RoomExecutablePath = ExtractValue(Names.RoomExecutablePath);
            RoomMaxConnections = ExtractValueInt(Names.RoomMaxConnections, 1000);

            SpawnId = ExtractValueInt(Names.SpawnId);
            SpawnCode = ExtractValue(Names.SpawnCode);
            DontSpawnInBatchmode = IsProvided(Names.DontSpawnInBatchmode);
            MaxProcesses = ExtractValueInt(Names.MaxProcesses, 0);

            LoadScene = ExtractValue(Names.LoadScene);

            DbConnectionString = ExtractValue(Names.DbConnectionString);

            LobbyId = ExtractValueInt(Names.LobbyId);
            WebGl = IsProvided(Names.UseWebSockets);
        }

        public override string ToString()
        {
            return string.Join(" ", _args);
        }

        #region Arguments

        /// <summary>
        /// If true, master server should be started
        /// </summary>
        public bool StartMaster { get; private set; }

        /// <summary>
        /// If true, client will try to connect to master at start
        /// </summary>
        public bool AutoConnectClient { get; private set; }

        /// <summary>
        /// Port, which will be open on the master server
        /// </summary>
        public int MasterPort { get; private set; }

        /// <summary>
        /// Ip address to the master server
        /// </summary>
        public string MasterIp { get; private set; }

        /// <summary>
        /// Public ip of the machine, on which the process is running
        /// </summary>
        public string RoomIp { get; private set; }

        /// <summary>
        /// Port, assigned to the spawned process (most likely a game server)
        /// </summary>
        public int RoomPort { get; private set; }

        /// <summary>
        /// Max number of connections allowed
        /// </summary>
        public int RoomMaxConnections { get; private set; }

        /// <summary>
        /// Path to the executable (used by the spawner)
        /// </summary>
        public string RoomExecutablePath { get; private set; }

        /// <summary>
        /// If true, some of the Ui game objects will be destroyed.
        /// (to avoid memory leaks)
        /// </summary>
        public bool DestroyUi { get; private set; }

        /// <summary>
        /// SpawnId of the spawned process
        /// </summary>
        public int SpawnId { get; private set; }

        /// <summary>
        /// Code, which is used to ensure that there's no tampering with 
        /// spawned processes
        /// </summary>
        public string SpawnCode { get; private set; }

        /// <summary>
        /// If true, will make sure that spawned processes are not spawned in batchmode
        /// </summary>
        public bool DontSpawnInBatchmode { get; private set; }

        /// <summary>
        /// Max number of processes that can be spawned by a spawner
        /// </summary>
        public int MaxProcesses { get; private set; }

        /// <summary>
        /// Name of the scene to load
        /// </summary>
        public string LoadScene { get; private set; }

        /// <summary>
        /// Database connection string (user by some of the database implementations)
        /// </summary>
        public string DbConnectionString { get; private set; }

        /// <summary>
        /// LobbyId, which is assigned to a spawned process
        /// </summary>
        public int LobbyId { get; private set; }

        /// <summary>
        /// If true, it will be considered that we want to start server to
        /// support webgl clients
        /// </summary>
        public bool WebGl { get; private set; }

        #endregion

        #region Helper methods

        /// <summary>
        /// Extracts a string value for command line arguments provided
        /// </summary>
        /// <param name="argName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public string ExtractValue(string argName, string defaultValue = null)
        {
            if (!_args.Contains(argName))
            {
                return defaultValue;
            }

            var index = _args.ToList().FindIndex(0, a => a.Equals(argName));
            return _args[index + 1];
        }

        /// <summary>
        /// Extracts an int string value for command line arguments provided
        /// </summary>
        /// <param name="argName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public int ExtractValueInt(string argName, int defaultValue = -1)
        {
            var number = ExtractValue(argName, defaultValue.ToString());
            return Convert.ToInt32(number);
        }

        /// <summary>
        /// Check is given cmd is provided
        /// </summary>
        /// <param name="argName"></param>
        /// <returns></returns>
        public bool IsProvided(string argName)
        {
            return _args.Contains(argName);
        }

        #endregion

        public class MsfArgNames
        {
            /// <summary>
            /// Use this cmd to start master server after unity player is started
            /// </summary>
            public string StartMaster { get { return "-msfStartMaster"; } }
            /// <summary>
            /// Use this cmd to start server room spawner after unity player is started
            /// </summary>
            public string StartSpawner { get { return "-msfStartSpawner"; } }
            /// <summary>
            /// Use this cmd to start client connection to master server after unity player is started
            /// </summary>
            public string StartClientConnection { get { return "-msfStartClientConnection"; } }
            /// <summary>
            /// Use this cmd to set up master server connection port
            /// </summary>
            public string MasterPort { get { return "-msfMasterPort"; } }
            /// <summary>
            /// Use this cmd to set up master server connection IP address
            /// </summary>
            public string MasterIp { get { return "-msfMasterIp"; } }

            public string SpawnId { get { return "-msfSpawnId"; } }
            public string SpawnCode { get { return "-msfSpawnCode"; } }

            public string RoomPort { get { return "-msfRoomPort"; } }
            public string RoomIp { get { return "-msfRoomIp"; } }
            public string RoomMaxConnections { get { return "-msfRoomMaxConnections"; } }
            public string RoomExecutablePath { get { return "-msfRoomExe"; } }
            public string UseWebSockets { get { return "-msfUseWebSockets"; } }

            public string LoadScene { get { return "-msfLoadScene"; } }
            public string DbConnectionString { get { return "-msfDbConnectionString"; } }
            public string LobbyId { get { return "-msfLobbyId"; } }
            public string DontSpawnInBatchmode { get { return "-msfDontSpawnInBatchmode"; } }
            public string MaxProcesses { get { return "-msfMaxProcesses"; } }
            public string DestroyUi { get { return "-msfDestroyUi"; } }
        }
    }
}