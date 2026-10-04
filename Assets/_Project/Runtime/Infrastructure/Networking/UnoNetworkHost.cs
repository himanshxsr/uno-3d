#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Uno.Core.Session;

namespace Uno.Infrastructure.Networking
{
    /// <summary>
    /// Host-authoritative TCP session for friends online (LAN / port-forward).
    /// </summary>
    public sealed class UnoNetworkHost : IDisposable
    {
        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private readonly Dictionary<string, ClientConn> _clients = new Dictionary<string, ClientConn>();
        private readonly object _gate = new object();
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private UdpClient? _advertiser;
        private GameSessionConfig _config = null!;
        private bool _gameStarted;

        public event Action<LobbyUpdateMsg>? OnLobbyChanged;
        public event Action<PlayerCommandMsg>? OnPlayerCommand;
        public event Action<string>? OnClientLeft;
        public event Action<string>? OnLog;

        public GameSessionConfig Config => _config;
        public bool IsRunning { get; private set; }

        public void StartHost(GameSessionConfig config)
        {
            _config = config;
            _config.Mode = GameMode.OnlineHost;
            _config.HostAddress = RoomCodeUtil.GetLocalIPv4();
            _config.HostPort = config.HostPort > 0 ? config.HostPort : GameSessionPorts.DefaultTcpPort;
            if (string.IsNullOrEmpty(_config.RoomCode))
            {
                _config.RoomCode = RoomCodeUtil.GenerateRoomCode();
            }

            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _config.HostPort);
            _listener.Start();
            IsRunning = true;
            _gameStarted = false;

            Task.Run(() => AcceptLoop(_cts.Token));
            StartAdvertiser();
            BroadcastLobby();
            OnLog?.Invoke($"Hosting room {_config.RoomCode} on {_config.HostAddress}:{_config.HostPort}");
        }

        public void Pump()
        {
            while (_mainThread.TryDequeue(out Action? action))
            {
                action?.Invoke();
            }
        }

        public void SetSeatReady(int seat, bool ready)
        {
            if (seat < 0 || seat >= _config.Seats.Length)
            {
                return;
            }

            _config.Seats[seat].IsReady = ready;
            BroadcastLobby();
        }

        public void FillBotsAndMarkReady()
        {
            _config.FillEmptySeatsWithBots();
            for (int i = 0; i < _config.Seats.Length; i++)
            {
                if (_config.Seats[i].Kind != PlayerKind.RemoteHuman || !string.IsNullOrEmpty(_config.Seats[i].NetworkClientId))
                {
                    _config.Seats[i].IsReady = true;
                }
            }

            BroadcastLobby();
        }

        public void BeginGame(int seed)
        {
            _gameStarted = true;
            _config.Seed = seed;
            var msg = new StartGameMsg
            {
                Seed = seed,
                Seats = ToLobbySeats()
            };
            Broadcast(NetMessageType.StartGame, msg);
            StopAdvertiser();
        }

        public void BroadcastSnapshot(GameSnapshotMsg snapshot)
        {
            Broadcast(NetMessageType.GameSnapshot, snapshot);
        }

        public void SendSnapshotTo(string clientId, GameSnapshotMsg snapshot)
        {
            SendTo(clientId, NetMessageType.GameSnapshot, snapshot);
        }

        public LobbyUpdateMsg BuildLobbyUpdate()
        {
            int humans = 0;
            for (int i = 0; i < _config.Seats.Length; i++)
            {
                if (_config.Seats[i].Kind == PlayerKind.LocalHuman ||
                    (_config.Seats[i].Kind == PlayerKind.RemoteHuman && !string.IsNullOrEmpty(_config.Seats[i].NetworkClientId)))
                {
                    humans++;
                }
            }

            return new LobbyUpdateMsg
            {
                RoomCode = _config.RoomCode,
                HostAddress = _config.HostAddress,
                HostPort = _config.HostPort,
                Seats = ToLobbySeats(),
                CanStart = humans >= 1
            };
        }

        public void BroadcastLobby()
        {
            LobbyUpdateMsg lobby = BuildLobbyUpdate();
            Broadcast(NetMessageType.LobbyUpdate, lobby);
            Enqueue(() => OnLobbyChanged?.Invoke(lobby));
        }

        private async Task AcceptLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _listener != null)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClient(client, token), token);
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                Enqueue(() => OnLog?.Invoke($"Host accept error: {ex.Message}"));
            }
        }

        private async Task HandleClient(TcpClient tcp, CancellationToken token)
        {
            string clientId = Guid.NewGuid().ToString("N");
            var conn = new ClientConn(clientId, tcp);
            NetworkStream stream = tcp.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);

            try
            {
                while (!token.IsCancellationRequested && tcp.Connected)
                {
                    string? line = await reader.ReadLineAsync();
                    if (line == null)
                    {
                        break;
                    }

                    ProcessLine(conn, line);
                }
            }
            catch (Exception ex)
            {
                Enqueue(() => OnLog?.Invoke($"Client {clientId} error: {ex.Message}"));
            }
            finally
            {
                RemoveClient(clientId);
                try { tcp.Close(); } catch { /* ignored */ }
            }
        }

        private void ProcessLine(ClientConn conn, string line)
        {
            NetEnvelope? env;
            try
            {
                env = JsonNet.FromJson<NetEnvelope>(line);
            }
            catch
            {
                return;
            }

            if (env == null)
            {
                return;
            }

            switch (env.Type)
            {
                case NetMessageType.JoinRequest:
                    HandleJoin(conn, JsonNet.FromJson<JoinRequestMsg>(env.PayloadJson));
                    break;
                case NetMessageType.ReadyToggle:
                    HandleReady(conn);
                    break;
                case NetMessageType.PlayerCommand:
                    var cmd = JsonNet.FromJson<PlayerCommandMsg>(env.PayloadJson);
                    cmd.ClientId = conn.Id;
                    Enqueue(() => OnPlayerCommand?.Invoke(cmd));
                    break;
                case NetMessageType.Leave:
                    RemoveClient(conn.Id);
                    break;
            }
        }

        private void HandleJoin(ClientConn conn, JoinRequestMsg req)
        {
            if (_gameStarted)
            {
                SendTo(conn, NetMessageType.JoinRejected, new JoinAcceptedMsg { ClientId = conn.Id });
                return;
            }

            if (!string.Equals(req.RoomCode, _config.RoomCode, StringComparison.OrdinalIgnoreCase))
            {
                SendTo(conn, NetMessageType.JoinRejected, new JoinAcceptedMsg { ClientId = conn.Id });
                return;
            }

            int seat = -1;
            lock (_gate)
            {
                for (int i = 1; i < _config.Seats.Length; i++)
                {
                    SeatConfig s = _config.Seats[i];
                    if (s.Kind == PlayerKind.RemoteHuman && string.IsNullOrEmpty(s.NetworkClientId))
                    {
                        seat = i;
                        s.NetworkClientId = conn.Id;
                        s.DisplayName = string.IsNullOrWhiteSpace(req.PlayerName) ? $"Player {i}" : req.PlayerName.Trim();
                        s.IsReady = false;
                        _clients[conn.Id] = conn;
                        break;
                    }
                }
            }

            if (seat < 0)
            {
                SendTo(conn, NetMessageType.JoinRejected, new JoinAcceptedMsg { ClientId = conn.Id });
                Enqueue(() => OnLog?.Invoke("Join rejected — lobby full."));
                return;
            }

            conn.SeatIndex = seat;
            var accepted = new JoinAcceptedMsg
            {
                SeatIndex = seat,
                ClientId = conn.Id,
                HostName = _config.Seats[0].DisplayName,
                RoomCode = _config.RoomCode
            };
            SendTo(conn, NetMessageType.JoinAccepted, accepted);
            BroadcastLobby();
            string joinedName = string.IsNullOrWhiteSpace(req.PlayerName) ? "Guest" : req.PlayerName.Trim();
            Enqueue(() => OnLog?.Invoke($"{joinedName} joined seat {seat}."));
        }

        private void HandleReady(ClientConn conn)
        {
            lock (_gate)
            {
                if (conn.SeatIndex < 0 || conn.SeatIndex >= _config.Seats.Length)
                {
                    return;
                }

                SeatConfig seat = _config.Seats[conn.SeatIndex];
                seat.IsReady = !seat.IsReady;
            }

            BroadcastLobby();
        }

        private void RemoveClient(string clientId)
        {
            lock (_gate)
            {
                if (_clients.Remove(clientId))
                {
                    for (int i = 0; i < _config.Seats.Length; i++)
                    {
                        if (_config.Seats[i].NetworkClientId == clientId)
                        {
                            _config.Seats[i].NetworkClientId = string.Empty;
                            _config.Seats[i].DisplayName = "Waiting...";
                            _config.Seats[i].IsReady = false;
                            _config.Seats[i].Kind = PlayerKind.RemoteHuman;
                        }
                    }
                }
            }

            BroadcastLobby();
            Enqueue(() => OnClientLeft?.Invoke(clientId));
        }

        private void Broadcast(NetMessageType type, object payload)
        {
            string line = JsonNet.Wrap(type, payload) + "\n";
            byte[] bytes = Encoding.UTF8.GetBytes(line);
            lock (_gate)
            {
                foreach (ClientConn c in _clients.Values)
                {
                    try
                    {
                        c.Stream.Write(bytes, 0, bytes.Length);
                    }
                    catch
                    {
                        // Dropped on next read.
                    }
                }
            }
        }

        private void SendTo(string clientId, NetMessageType type, object payload)
        {
            lock (_gate)
            {
                if (_clients.TryGetValue(clientId, out ClientConn? conn))
                {
                    SendTo(conn, type, payload);
                }
            }
        }

        private static void SendTo(ClientConn conn, NetMessageType type, object payload)
        {
            string line = JsonNet.Wrap(type, payload) + "\n";
            byte[] bytes = Encoding.UTF8.GetBytes(line);
            try
            {
                conn.Stream.Write(bytes, 0, bytes.Length);
            }
            catch
            {
                // ignored
            }
        }

        private LobbySeatMsg[] ToLobbySeats()
        {
            var seats = new LobbySeatMsg[_config.Seats.Length];
            for (int i = 0; i < _config.Seats.Length; i++)
            {
                SeatConfig s = _config.Seats[i];
                seats[i] = new LobbySeatMsg
                {
                    SeatIndex = i,
                    Name = s.DisplayName,
                    Kind = s.Kind.ToString(),
                    IsReady = s.IsReady,
                    ClientId = s.NetworkClientId
                };
            }

            return seats;
        }

        private void StartAdvertiser()
        {
            try
            {
                _advertiser = new UdpClient();
                _advertiser.EnableBroadcast = true;
                _ = Task.Run(async () =>
                {
                    while (_cts != null && !_cts.IsCancellationRequested && !_gameStarted)
                    {
                        var ad = new LanAdvertiseMsg
                        {
                            RoomCode = _config.RoomCode,
                            HostName = _config.Seats[0].DisplayName,
                            Address = _config.HostAddress,
                            Port = _config.HostPort,
                            OpenSeats = CountOpenSeats()
                        };
                        byte[] data = Encoding.UTF8.GetBytes(JsonNet.ToJson(ad));
                        try
                        {
                            await _advertiser.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Broadcast, GameSessionPorts.DefaultUdpAdvertisePort));
                        }
                        catch
                        {
                            // ignored
                        }

                        await Task.Delay(1000);
                    }
                });
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"LAN advertise disabled: {ex.Message}");
            }
        }

        private int CountOpenSeats()
        {
            int open = 0;
            for (int i = 1; i < _config.Seats.Length; i++)
            {
                if (_config.Seats[i].Kind == PlayerKind.RemoteHuman && string.IsNullOrEmpty(_config.Seats[i].NetworkClientId))
                {
                    open++;
                }
            }

            return open;
        }

        private void StopAdvertiser()
        {
            try { _advertiser?.Dispose(); } catch { /* ignored */ }
            _advertiser = null;
        }

        private void Enqueue(Action action) => _mainThread.Enqueue(action);

        public void Dispose()
        {
            IsRunning = false;
            _cts?.Cancel();
            StopAdvertiser();
            try { _listener?.Stop(); } catch { /* ignored */ }
            lock (_gate)
            {
                foreach (ClientConn c in _clients.Values)
                {
                    try { c.Tcp.Close(); } catch { /* ignored */ }
                }

                _clients.Clear();
            }
        }

        private sealed class ClientConn
        {
            public string Id { get; }
            public TcpClient Tcp { get; }
            public NetworkStream Stream { get; }
            public int SeatIndex { get; set; } = -1;

            public ClientConn(string id, TcpClient tcp)
            {
                Id = id;
                Tcp = tcp;
                Stream = tcp.GetStream();
            }
        }
    }
}
