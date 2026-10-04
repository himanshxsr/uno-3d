#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Uno.Core.Session;

namespace Uno.Infrastructure.Networking
{
    /// <summary>
    /// Host session over AWS relay (outbound-only; no local port forward).
    /// </summary>
    public sealed class UnoRelayHost : IDisposable
    {
        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private readonly object _gate = new object();
        private TcpClient? _tcp;
        private StreamWriter? _writer;
        private CancellationTokenSource? _cts;
        private GameSessionConfig _config = null!;
        private bool _gameStarted;

        public event Action<LobbyUpdateMsg>? OnLobbyChanged;
        public event Action<PlayerCommandMsg>? OnPlayerCommand;
        public event Action<string>? OnClientLeft;
        public event Action<string>? OnLog;

        public GameSessionConfig Config => _config;
        public bool IsRunning { get; private set; }

        public async Task<bool> StartHostAsync(GameSessionConfig config, string relayHost, int relayPort)
        {
            Dispose();
            _config = config;
            _config.Mode = GameMode.OnlineHost;
            _config.HostAddress = relayHost;
            _config.HostPort = relayPort;
            if (string.IsNullOrEmpty(_config.RoomCode))
            {
                _config.RoomCode = RoomCodeUtil.GenerateRoomCode();
            }

            _cts = new CancellationTokenSource();
            try
            {
                _tcp = new TcpClient();
                await _tcp.ConnectAsync(relayHost, relayPort);
                NetworkStream stream = _tcp.GetStream();
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                IsRunning = true;
                _gameStarted = false;
                _ = Task.Run(() => ReadLoop(stream, _cts.Token));

                Send(NetMessageType.RelayHostCreate, new JoinRequestMsg
                {
                    RoomCode = _config.RoomCode,
                    PlayerName = _config.Seats[0].DisplayName
                });
                OnLog?.Invoke($"Connecting to relay {relayHost}:{relayPort}...");
                return true;
            }
            catch (Exception ex)
            {
                IsRunning = false;
                OnLog?.Invoke($"Relay host connect failed: {ex.Message}");
                return false;
            }
        }

        public void Pump()
        {
            while (_mainThread.TryDequeue(out Action? action))
            {
                action?.Invoke();
            }
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
            Broadcast(NetMessageType.StartGame, new StartGameMsg { Seed = seed, Seats = ToLobbySeats() });
        }

        public void BroadcastSnapshot(GameSnapshotMsg snapshot) =>
            Broadcast(NetMessageType.GameSnapshot, snapshot);

        public void SendSnapshotTo(string clientId, GameSnapshotMsg snapshot)
        {
            // Relay looks for _RelayTargetClientId inside payload JSON.
            string json = JsonNet.ToJson(snapshot);
            // Inject target field via wrapper object string replace-safe approach:
            if (json.Length > 1 && json[0] == '{')
            {
                json = "{\"_RelayTargetClientId\":\"" + clientId + "\"," + json.Substring(1);
            }

            SendRaw(NetMessageType.GameSnapshot, json);
        }

        public LobbyUpdateMsg BuildLobbyUpdate() => new LobbyUpdateMsg
        {
            RoomCode = _config.RoomCode,
            HostAddress = _config.HostAddress,
            HostPort = _config.HostPort,
            Seats = ToLobbySeats(),
            CanStart = true
        };

        public void BroadcastLobby()
        {
            LobbyUpdateMsg lobby = BuildLobbyUpdate();
            Broadcast(NetMessageType.LobbyUpdate, lobby);
            Enqueue(() => OnLobbyChanged?.Invoke(lobby));
        }

        private async Task ReadLoop(NetworkStream stream, CancellationToken token)
        {
            var reader = new StreamReader(stream, Encoding.UTF8);
            try
            {
                while (!token.IsCancellationRequested && _tcp != null && _tcp.Connected)
                {
                    string? line = await reader.ReadLineAsync();
                    if (line == null) break;
                    HandleLine(line);
                }
            }
            catch (Exception ex)
            {
                Enqueue(() => OnLog?.Invoke($"Relay disconnected: {ex.Message}"));
            }
            finally
            {
                IsRunning = false;
            }
        }

        private void HandleLine(string line)
        {
            NetEnvelope? env;
            try { env = JsonNet.FromJson<NetEnvelope>(line); }
            catch { return; }
            if (env == null) return;

            switch (env.Type)
            {
                case NetMessageType.RelayHostCreated:
                {
                    var created = JsonNet.FromJson<JoinAcceptedMsg>(env.PayloadJson);
                    // RoomCode reused field names via JoinAcceptedMsg-like JSON — parse manually.
                    try
                    {
                        var raw = JsonNet.FromJson<LobbyUpdateMsg>(env.PayloadJson);
                        if (!string.IsNullOrEmpty(raw.RoomCode))
                        {
                            _config.RoomCode = raw.RoomCode;
                        }
                    }
                    catch
                    {
                        /* ignore */
                    }

                    // Payload is { RoomCode, TcpPort }
                    string code = ExtractJsonString(env.PayloadJson, "RoomCode");
                    if (!string.IsNullOrEmpty(code))
                    {
                        _config.RoomCode = code;
                    }

                    Enqueue(() =>
                    {
                        OnLog?.Invoke($"Relay room ready: {_config.RoomCode}");
                        BroadcastLobby();
                    });
                    break;
                }
                case NetMessageType.JoinRequest:
                    HandleJoin(JsonNet.FromJson<JoinRequestMsg>(env.PayloadJson));
                    break;
                case NetMessageType.ReadyToggle:
                    // Seat ready toggled by client id — find seat
                    break;
                case NetMessageType.PlayerCommand:
                {
                    var cmd = JsonNet.FromJson<PlayerCommandMsg>(env.PayloadJson);
                    Enqueue(() => OnPlayerCommand?.Invoke(cmd));
                    break;
                }
                case NetMessageType.Leave:
                {
                    string clientId = ExtractJsonString(env.PayloadJson, "ClientId");
                    if (!string.IsNullOrEmpty(clientId))
                    {
                        RemoveClient(clientId);
                        Enqueue(() => OnClientLeft?.Invoke(clientId));
                    }
                    break;
                }
                case NetMessageType.JoinRejected:
                    Enqueue(() => OnLog?.Invoke("Relay rejected host create."));
                    break;
            }
        }

        private void HandleJoin(JoinRequestMsg req)
        {
            if (_gameStarted)
            {
                SendToClient(req.ClientId, NetMessageType.JoinRejected, new JoinAcceptedMsg { ClientId = req.ClientId });
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
                        s.NetworkClientId = req.ClientId;
                        s.DisplayName = string.IsNullOrWhiteSpace(req.PlayerName) ? $"Player {i}" : req.PlayerName.Trim();
                        s.IsReady = false;
                        break;
                    }
                }
            }

            if (seat < 0)
            {
                SendToClient(req.ClientId, NetMessageType.JoinRejected, new JoinAcceptedMsg { ClientId = req.ClientId });
                Enqueue(() => OnLog?.Invoke("Join rejected — lobby full."));
                return;
            }

            var accepted = new JoinAcceptedMsg
            {
                SeatIndex = seat,
                ClientId = req.ClientId,
                HostName = _config.Seats[0].DisplayName,
                RoomCode = _config.RoomCode
            };
            SendToClient(req.ClientId, NetMessageType.JoinAccepted, accepted);
            BroadcastLobby();
            Enqueue(() => OnLog?.Invoke($"{accepted.HostName}: {req.PlayerName} joined seat {seat}."));
        }

        private void RemoveClient(string clientId)
        {
            lock (_gate)
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

            BroadcastLobby();
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

        private void Broadcast(NetMessageType type, object payload) =>
            SendRaw(type, payload is string s ? s : JsonNet.ToJson(payload));

        private void SendToClient(string clientId, NetMessageType type, object payload)
        {
            string json = payload is string s ? s : JsonNet.ToJson(payload);
            if (json.Length > 1 && json[0] == '{')
            {
                json = "{\"_RelayTargetClientId\":\"" + clientId + "\"," + json.Substring(1);
            }

            SendRaw(type, json);
        }

        private void Send(NetMessageType type, object payload) =>
            SendRaw(type, JsonNet.ToJson(payload));

        private void SendRaw(NetMessageType type, string payloadJson)
        {
            if (_writer == null) return;
            try
            {
                _writer.WriteLine(JsonNet.ToJson(new NetEnvelope { Type = type, PayloadJson = payloadJson }));
            }
            catch (Exception ex)
            {
                Enqueue(() => OnLog?.Invoke($"Relay send failed: {ex.Message}"));
            }
        }

        private static string ExtractJsonString(string json, string key)
        {
            string needle = "\"" + key + "\":\"";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return string.Empty;
            int start = i + needle.Length;
            int end = json.IndexOf('"', start);
            return end < 0 ? string.Empty : json.Substring(start, end - start);
        }

        private void Enqueue(Action a) => _mainThread.Enqueue(a);

        public void Dispose()
        {
            IsRunning = false;
            try { _cts?.Cancel(); } catch { /* ignore */ }
            try { _writer?.Dispose(); } catch { /* ignore */ }
            try { _tcp?.Close(); } catch { /* ignore */ }
            _writer = null;
            _tcp = null;
        }
    }
}
