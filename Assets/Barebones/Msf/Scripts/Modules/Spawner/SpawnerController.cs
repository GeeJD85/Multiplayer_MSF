using Barebones.Logging;
using Barebones.Networking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Barebones.MasterServer
{
    public class SpawnerController
    {
        public delegate void SpawnProcessHandler(SpawnRequestPacket packet, IIncommingMessage message);
        public delegate void KillProcessHandler(int spawnId);

        private SpawnProcessHandler spawnRequestHandler;
        private KillProcessHandler killRequestHandler;

        private static object processLock = new object();
        private static Dictionary<int, Process> processes = new Dictionary<int, Process>();

        public IClientSocket Connection { get; private set; }
        public int SpawnerId { get; set; }
        public SpawnerOptions Options { get; private set; }
        /// <summary>
        /// Settings, which are used by the default spawn handler
        /// </summary>
        public DefaultSpawnerConfig DefaultSpawnerSettings { get; private set; }

        public Logger Logger => Msf.Create.Logger(typeof(SpawnerController).Name, LogLevel.Warn);

        public SpawnerController(int spawnerId, IClientSocket connection, SpawnerOptions options)
        {
            Connection = connection;
            SpawnerId = spawnerId;
            Options = options;

            DefaultSpawnerSettings = new DefaultSpawnerConfig()
            {
                MasterIp = connection.ConnectionIp,
                MasterPort = connection.ConnectionPort,
                MachineIp = options.MachineIp,
                SpawnInBatchmode = Msf.Args.IsProvided("-batchmode")
            };

            // Add handlers
            connection.SetHandler((short)MsfMessageCodes.SpawnProcessRequest, SpawnProcessRequestHandler);
            connection.SetHandler((short)MsfMessageCodes.KillProcessRequest, KillProcessRequestHandler);
        }

        public void SetSpawnRequestHandler(SpawnProcessHandler handler)
        {
            spawnRequestHandler = handler;
        }

        public void SetKillRequestHandler(KillProcessHandler handler)
        {
            killRequestHandler = handler;
        }

        public void NotifyProcessStarted(int spawnId, int processId, string cmdArgs)
        {
            Msf.Server.Spawners.NotifyProcessStarted(spawnId, processId, cmdArgs, Connection);
        }

        public void NotifyProcessKilled(int spawnId)
        {
            Msf.Server.Spawners.NotifyProcessKilled(spawnId);
        }

        public void UpdateProcessesCount(int count)
        {
            Msf.Server.Spawners.UpdateProcessesCount(SpawnerId, count, Connection);
        }

        private void HandleSpawnRequest(SpawnRequestPacket packet, IIncommingMessage message)
        {
            if (spawnRequestHandler == null)
            {
                DefaultSpawnRequestHandler(packet, message);
                return;
            }

            spawnRequestHandler.Invoke(packet, message);
        }

        private void HandleKillSpawnedProcessRequest(int spawnId)
        {
            if (killRequestHandler == null)
            {
                DefaultKillSpawnedProcessRequestHandler(spawnId);
                return;
            }

            killRequestHandler.Invoke(spawnId);
        }

        private void SpawnProcessRequestHandler(IIncommingMessage message)
        {
            var data = message.Deserialize(new SpawnRequestPacket());

            var controller = Msf.Server.Spawners.GetController(data.SpawnerId);

            if (controller == null)
            {
                if (message.IsExpectingResponse)
                {
                    message.Respond("Couldn't find a spawn controller", ResponseStatus.NotHandled);
                }

                return;
            }

            // Pass the request to handler
            controller.HandleSpawnRequest(data, message);
        }

        private static void KillProcessRequestHandler(IIncommingMessage message)
        {
            var data = message.Deserialize(new KillSpawnedProcessPacket());

            var controller = Msf.Server.Spawners.GetController(data.SpawnerId);

            if (controller == null)
            {
                if (message.IsExpectingResponse)
                {
                    message.Respond("Couldn't find a spawn controller", ResponseStatus.NotHandled);
                }

                return;
            }

            controller.HandleKillSpawnedProcessRequest(data.SpawnId);
        }

        #region Default handlers

        public void DefaultKillSpawnedProcessRequestHandler(int spawnId)
        {
            Logger.Debug("Default kill request handler started handling a request to kill a process");

            try
            {
                Process process;

                lock (processLock)
                {
                    processes.TryGetValue(spawnId, out process);
                    processes.Remove(spawnId);
                }

                if (process != null)
                {
                    process.Kill();
                }
            }
            catch (Exception e)
            {
                Logger.Error("Got error while killing a spawned process");
                Logger.Error(e);
            }
        }

        public void DefaultSpawnRequestHandler(SpawnRequestPacket packet, IIncommingMessage message)
        {
            Logger.Debug("Default spawn handler started handling a request to spawn process");

            var controller = Msf.Server.Spawners.GetController(packet.SpawnerId);

            if (controller == null)
            {
                message.Respond("Failed to spawn a process. Spawner controller not found", ResponseStatus.Failed);
                return;
            }

            ////////////////////////////////////////////
            /// Create process args string
            var processArguments = new StringBuilder();
            processArguments.Append(" ");

            ////////////////////////////////////////////
            /// Check if we're overriding an IP to master server
            var masterIpArgument = string.IsNullOrEmpty(controller.DefaultSpawnerSettings.MasterIp) ? 
                controller.Connection.ConnectionIp : controller.DefaultSpawnerSettings.MasterIp;

            ////////////////////////////////////////////
            /// Create msater IP arg
            processArguments.Append($"{Msf.Args.Names.MasterIp} {masterIpArgument}");
            processArguments.Append(" ");

            ////////////////////////////////////////////
            /// Check if we're overriding a port to master server
            var masterPortArgument = controller.DefaultSpawnerSettings.MasterPort < 0 ?
                controller.Connection.ConnectionPort : controller.DefaultSpawnerSettings.MasterPort;

            ////////////////////////////////////////////
            /// Create master port arg
            processArguments.Append($"{Msf.Args.Names.MasterPort} {masterPortArgument}");
            processArguments.Append(" ");

            ////////////////////////////////////////////
            /// Machine Ip
            var machineIpArgument = controller.DefaultSpawnerSettings.MachineIp;

            /// Create room IP arg
            processArguments.Append($"{Msf.Args.Names.RoomIp} {machineIpArgument}");
            processArguments.Append(" ");

            ////////////////////////////////////////////
            /// Create port for room arg
            int machinePortArgument = Msf.Server.Spawners.GetAvailablePort();
            processArguments.Append($"{Msf.Args.Names.RoomPort} {machinePortArgument}");
            processArguments.Append(" ");

            ////////////////////////////////////////////
            /// Get the scene name
            var sceneNameArgument = packet.Properties.ContainsKey(MsfDictKeys.sceneName)
                ? $"{Msf.Args.Names.LoadScene} {packet.Properties[MsfDictKeys.sceneName]}" : string.Empty;

            /// Create scene name arg
            processArguments.Append(sceneNameArgument);
            processArguments.Append(" ");

            ////////////////////////////////////////////
            /// If spawn in batchmode was set and `DontSpawnInBatchmode` arg is not provided
            var spawnInBatchmodeArgument = controller.DefaultSpawnerSettings.SpawnInBatchmode && !Msf.Args.DontSpawnInBatchmode;
            processArguments.Append((spawnInBatchmodeArgument ? "-batchmode -nographics" : string.Empty));
            processArguments.Append(" ");

            ////////////////////////////////////////////
            /// Create scene name arg
            processArguments.Append((controller.DefaultSpawnerSettings.UseWebSockets ? Msf.Args.Names.UseWebSockets + " " : string.Empty));
            processArguments.Append(" ");

            ////////////////////////////////////////////
            /// Create spawn id arg
            processArguments.Append($"{Msf.Args.Names.SpawnId} {packet.SpawnId}");
            processArguments.Append(" ");

            ////////////////////////////////////////////
            /// Create spawn code arg
            processArguments.Append($"{Msf.Args.Names.SpawnCode} \"{packet.SpawnCode}\"");
            processArguments.Append(" ");

            ////////////////////////////////////////////
            /// Create destroy ui arg
            processArguments.Append((Msf.Args.DestroyUi ? Msf.Args.Names.DestroyUi + " " : string.Empty));
            processArguments.Append(" ");

            ////////////////////////////////////////////
            /// Create custom args
            processArguments.Append(packet.CustomArgs);
            processArguments.Append(" ");

            ///////////////////////////////////////////
            /// Path to executable
            var executablePath = controller.DefaultSpawnerSettings.ExecutablePath;

            if (string.IsNullOrEmpty(executablePath))
            {
                executablePath = File.Exists(Environment.GetCommandLineArgs()[0])
                    ? Environment.GetCommandLineArgs()[0]
                    : Process.GetCurrentProcess().MainModule.FileName;
            }

            // In case a path is provided with the request
            if (packet.Properties.ContainsKey(MsfDictKeys.executablePath))
            {
                executablePath = packet.Properties[MsfDictKeys.executablePath];
            }

            if (!string.IsNullOrEmpty(packet.OverrideExePath))
            {
                executablePath = packet.OverrideExePath;
            }

            /// Create info about starting process
            var startProcessInfo = new ProcessStartInfo(executablePath)
            {
                CreateNoWindow = false,
                UseShellExecute = false,
                Arguments = processArguments.ToString()
            };

            Logger.Debug("Starting process with args: " + startProcessInfo.Arguments);

            var processStarted = false;

            try
            {
                new Thread(() =>
                {
                    try
                    {
                        using (var process = Process.Start(startProcessInfo))
                        {
                            Logger.Debug("Process started. Spawn Id: " + packet.SpawnId + ", pid: " + process.Id);
                            processStarted = true;

                            lock (processLock)
                            {
                                // Save the process
                                processes[packet.SpawnId] = process;
                            }

                            var processId = process.Id;

                            // Notify server that we've successfully handled the request
                            MsfTimer.RunInMainThread(() =>
                            {
                                message.Respond(ResponseStatus.Success);
                                controller.NotifyProcessStarted(packet.SpawnId, processId, startProcessInfo.Arguments);
                            });

                            process.WaitForExit();
                        }
                    }
                    catch (Exception e)
                    {
                        if (!processStarted)
                        {
                            MsfTimer.RunInMainThread(() => { message.Respond(ResponseStatus.Failed); });
                        }

                        Logger.Error("An exception was thrown while starting a process. Make sure that you have set a correct build path. " +
                                     "We've tried to start a process at: '" + executablePath + "'. You can change it at 'SpawnerBehaviour' component");
                        Logger.Error(e);
                    }
                    finally
                    {
                        lock (processLock)
                        {
                            // Remove the process
                            processes.Remove(packet.SpawnId);
                        }

                        MsfTimer.RunInMainThread(() =>
                        {
                            // Release the port number
                            Msf.Server.Spawners.ReleasePort(machinePortArgument);

                            Logger.Debug("Notifying about killed process with spawn id: " + packet.SpawnerId);
                            controller.NotifyProcessKilled(packet.SpawnId);
                        });
                    }

                }).Start();
            }
            catch (Exception e)
            {
                message.Respond(e.Message, ResponseStatus.Error);
                Logs.Error(e);
            }
        }

        public static void KillProcessesSpawnedWithDefaultHandler()
        {
            var list = new List<Process>();

            lock (processLock)
            {
                foreach (var process in processes.Values)
                {
                    list.Add(process);
                }
            }

            foreach (var process in list)
            {
                process.Kill();
            }
        }

        public class DefaultSpawnerConfig
        {
            public string MachineIp { get; set; } = "127.0.0.1";
            public bool SpawnInBatchmode { get; set; } = Msf.Args.IsProvided("-batchmode");
            public string MasterIp { get; set; } = string.Empty;
            public int MasterPort { get; set; } = -1;
            public string ExecutablePath { get; set; } = string.Empty;
            public bool UseWebSockets { get; set; } = false;
        }

        #endregion
    }
}