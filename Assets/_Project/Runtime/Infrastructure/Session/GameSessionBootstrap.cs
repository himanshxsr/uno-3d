#nullable enable

using System;
using UnityEngine;
using Uno.Core.Session;
using Uno.Infrastructure.Networking;

namespace Uno.Infrastructure.Session
{
    /// <summary>
    /// DontDestroyOnLoad session carrier between Main Menu / Lobby / MainGame.
    /// </summary>
    public sealed class GameSessionBootstrap : MonoBehaviour
    {
        public static GameSessionBootstrap? Instance { get; private set; }

        public GameSessionConfig Config { get; private set; } =
            GameSessionConfig.CreateDefaultOfflineSeats("You");

        public UnoNetworkHost? Host { get; private set; }
        public UnoRelayHost? RelayHost { get; private set; }
        public UnoNetworkClient? Client { get; private set; }
        public int LocalSeatIndex { get; private set; }
        public bool HasPendingMatch { get; private set; }
        public bool UsesCloudRelay { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void ConfigureOffline(string playerName, int botDifficulty)
        {
            DisposeNetwork();
            Config = GameSessionConfig.CreateDefaultOfflineSeats(string.IsNullOrWhiteSpace(playerName) ? "You" : playerName.Trim());
            Config.BotDifficulty = botDifficulty;
            Config.Seed = Random.Range(1, int.MaxValue);
            LocalSeatIndex = 0;
            HasPendingMatch = true;
            UsesCloudRelay = false;
        }

        public GameSessionConfig BeginHosting(string hostName)
        {
            DisposeNetwork();
            string code = RoomCodeUtil.GenerateRoomCode();
            Config = GameSessionConfig.CreateOnlineHost(
                string.IsNullOrWhiteSpace(hostName) ? "Host" : hostName.Trim(),
                code);
            LocalSeatIndex = 0;
            HasPendingMatch = false;
            UsesCloudRelay = true;

            string relayHost = RelaySettings.Host;
            int relayPort = RelaySettings.Port;
            Config.HostAddress = relayHost;
            Config.HostPort = relayPort;

            // Prefer cloud relay; fall back to LAN listen if relay unreachable.
            RelayHost = new UnoRelayHost();
            _ = StartRelayHostAsync(relayHost, relayPort);
            return Config;
        }

        private async void StartRelayHostAsync(string relayHost, int relayPort)
        {
            bool ok = await RelayHost!.StartHostAsync(Config, relayHost, relayPort);
            if (ok)
            {
                return;
            }

            // LAN fallback
            UsesCloudRelay = false;
            RelayHost?.Dispose();
            RelayHost = null;
            Config.HostAddress = RoomCodeUtil.GetLocalIPv4();
            Config.HostPort = GameSessionPorts.DefaultTcpPort;
            Host = new UnoNetworkHost();
            Host.StartHost(Config);
            Debug.LogWarning("[UNO 3D] Cloud relay unavailable — falling back to LAN host.");
        }

        public void MarkMatchReady() => HasPendingMatch = true;

        public void ConsumePendingMatch() => HasPendingMatch = false;

        public UnoNetworkClient BeginClient()
        {
            DisposeNetwork();
            Client = new UnoNetworkClient();
            Config.Mode = GameMode.OnlineClient;
            UsesCloudRelay = true;
            return Client;
        }

        public void ApplyClientSession(JoinAcceptedMsg accepted, LobbyUpdateMsg? lobby, string localName)
        {
            Config.Mode = GameMode.OnlineClient;
            Config.RoomCode = accepted.RoomCode;
            Config.LocalPlayerName = localName;
            LocalSeatIndex = accepted.SeatIndex;
            if (lobby != null)
            {
                ApplyLobbySeats(lobby);
            }
        }

        public void ApplyLobbySeats(LobbyUpdateMsg lobby)
        {
            Config.RoomCode = lobby.RoomCode;
            Config.HostAddress = lobby.HostAddress;
            Config.HostPort = lobby.HostPort;
            if (lobby.Seats == null)
            {
                return;
            }

            for (int i = 0; i < lobby.Seats.Length && i < Config.Seats.Length; i++)
            {
                LobbySeatMsg s = lobby.Seats[i];
                Config.Seats[i].DisplayName = s.Name;
                Config.Seats[i].IsReady = s.IsReady;
                Config.Seats[i].NetworkClientId = s.ClientId;
                Config.Seats[i].Kind = s.Kind switch
                {
                    "LocalHuman" => PlayerKind.LocalHuman,
                    "RemoteHuman" => PlayerKind.RemoteHuman,
                    _ => PlayerKind.Bot
                };
                if (i == LocalSeatIndex && Config.Mode == GameMode.OnlineClient)
                {
                    Config.Seats[i].Kind = PlayerKind.LocalHuman;
                }
            }
        }

        public void ApplyStartGame(StartGameMsg start)
        {
            Config.Seed = start.Seed;
            if (start.Seats != null)
            {
                for (int i = 0; i < start.Seats.Length && i < Config.Seats.Length; i++)
                {
                    LobbySeatMsg s = start.Seats[i];
                    Config.Seats[i].DisplayName = s.Name;
                    Config.Seats[i].Kind = s.Kind switch
                    {
                        "LocalHuman" => PlayerKind.LocalHuman,
                        "RemoteHuman" => i == LocalSeatIndex ? PlayerKind.LocalHuman : PlayerKind.RemoteHuman,
                        _ => PlayerKind.Bot
                    };
                    Config.Seats[i].IsReady = true;
                }
            }

            HasPendingMatch = true;
        }

        public string GetShareString()
        {
            if (UsesCloudRelay)
            {
                // Friends only need the room code when using the public relay.
                return Config.RoomCode;
            }

            return RoomCodeUtil.BuildShareString(Config.HostAddress, Config.HostPort, Config.RoomCode);
        }

        private void Update()
        {
            Host?.Pump();
            RelayHost?.Pump();
            Client?.Pump();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            DisposeNetwork();
        }

        public void DisposeNetwork()
        {
            Host?.Dispose();
            RelayHost?.Dispose();
            Client?.Dispose();
            Host = null;
            RelayHost = null;
            Client = null;
        }

        public void FillBotsAndBeginGame(int seed)
        {
            if (RelayHost != null)
            {
                RelayHost.FillBotsAndMarkReady();
                Config.Seed = seed;
                RelayHost.BeginGame(seed);
                return;
            }

            Host?.FillBotsAndMarkReady();
            Config.Seed = seed;
            Host?.BeginGame(seed);
        }

        public LobbyUpdateMsg? BuildLobbyUpdate()
        {
            if (RelayHost != null)
            {
                return RelayHost.BuildLobbyUpdate();
            }

            return Host?.BuildLobbyUpdate();
        }

        public static GameSessionBootstrap Ensure()
        {
            if (Instance != null)
            {
                return Instance;
            }

            GameObject go = new GameObject("Game Session Bootstrap");
            return go.AddComponent<GameSessionBootstrap>();
        }
    }
}
