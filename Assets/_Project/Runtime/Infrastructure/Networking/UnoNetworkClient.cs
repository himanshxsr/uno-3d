#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Uno.Infrastructure.Networking
{
    public sealed class UnoNetworkClient : IDisposable
    {
        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private TcpClient? _tcp;
        private StreamWriter? _writer;
        private CancellationTokenSource? _cts;
        private string _clientId = string.Empty;

        public event Action<JoinAcceptedMsg>? OnJoined;
        public event Action<string>? OnJoinRejected;
        public event Action<LobbyUpdateMsg>? OnLobbyUpdate;
        public event Action<StartGameMsg>? OnStartGame;
        public event Action<GameSnapshotMsg>? OnSnapshot;
        public event Action<string>? OnLog;

        public bool IsConnected { get; private set; }
        public int AssignedSeat { get; private set; } = -1;
        public string ClientId => _clientId;

        public async Task<bool> ConnectAsync(string address, int port, string playerName, string roomCode)
        {
            Dispose();
            _cts = new CancellationTokenSource();
            _clientId = Guid.NewGuid().ToString("N");
            try
            {
                _tcp = new TcpClient();
                await _tcp.ConnectAsync(address, port);
                NetworkStream stream = _tcp.GetStream();
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                IsConnected = true;
                _ = Task.Run(() => ReadLoop(stream, _cts.Token));

                // Cloud relay hello first; LAN direct host ignores unknown Type 102 if not a relay.
                bool useRelay = !string.Equals(address, "127.0.0.1", StringComparison.Ordinal)
                                || UnityEngine.PlayerPrefs.GetInt("uno.useRelay", 1) == 1;
                if (useRelay && !string.IsNullOrEmpty(roomCode))
                {
                    Send(NetMessageType.RelayClientJoin, new JoinRequestMsg
                    {
                        PlayerName = playerName,
                        RoomCode = roomCode,
                        ClientId = _clientId
                    });
                }
                else
                {
                    Send(NetMessageType.JoinRequest, new JoinRequestMsg
                    {
                        PlayerName = playerName,
                        RoomCode = roomCode,
                        ClientId = _clientId
                    });
                }

                OnLog?.Invoke($"Connecting to {address}:{port} room {roomCode}...");
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                OnLog?.Invoke($"Connect failed: {ex.Message}");
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

        public void ToggleReady() => Send(NetMessageType.ReadyToggle, new JoinRequestMsg { ClientId = _clientId });

        public void SendCommand(PlayerCommandMsg command)
        {
            command.ClientId = _clientId;
            command.SeatIndex = AssignedSeat;
            Send(NetMessageType.PlayerCommand, command);
        }

        private async Task ReadLoop(NetworkStream stream, CancellationToken token)
        {
            var reader = new StreamReader(stream, Encoding.UTF8);
            try
            {
                while (!token.IsCancellationRequested && _tcp != null && _tcp.Connected)
                {
                    string? line = await reader.ReadLineAsync();
                    if (line == null)
                    {
                        break;
                    }

                    HandleLine(line);
                }
            }
            catch (Exception ex)
            {
                Enqueue(() => OnLog?.Invoke($"Connection lost: {ex.Message}"));
            }
            finally
            {
                IsConnected = false;
            }
        }

        private void HandleLine(string line)
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
                case NetMessageType.RelayClientJoined:
                    // Relay ack — wait for host JoinAccepted next.
                    Enqueue(() => OnLog?.Invoke("Joined relay — waiting for host seat..."));
                    break;
                case NetMessageType.JoinAccepted:
                    var accepted = JsonNet.FromJson<JoinAcceptedMsg>(env.PayloadJson);
                    AssignedSeat = accepted.SeatIndex;
                    _clientId = string.IsNullOrEmpty(accepted.ClientId) ? _clientId : accepted.ClientId;
                    Enqueue(() => OnJoined?.Invoke(accepted));
                    break;
                case NetMessageType.JoinRejected:
                    Enqueue(() => OnJoinRejected?.Invoke("Room full, bad code, or game already started."));
                    break;
                case NetMessageType.LobbyUpdate:
                    var lobby = JsonNet.FromJson<LobbyUpdateMsg>(env.PayloadJson);
                    Enqueue(() => OnLobbyUpdate?.Invoke(lobby));
                    break;
                case NetMessageType.StartGame:
                    var start = JsonNet.FromJson<StartGameMsg>(env.PayloadJson);
                    Enqueue(() => OnStartGame?.Invoke(start));
                    break;
                case NetMessageType.GameSnapshot:
                    var snap = JsonNet.FromJson<GameSnapshotMsg>(env.PayloadJson);
                    Enqueue(() => OnSnapshot?.Invoke(snap));
                    break;
            }
        }

        private void Send(NetMessageType type, object payload)
        {
            if (_writer == null)
            {
                return;
            }

            try
            {
                _writer.WriteLine(JsonNet.Wrap(type, payload));
            }
            catch (Exception ex)
            {
                Enqueue(() => OnLog?.Invoke($"Send failed: {ex.Message}"));
            }
        }

        private void Enqueue(Action action) => _mainThread.Enqueue(action);

        public void Dispose()
        {
            IsConnected = false;
            try { _cts?.Cancel(); } catch { /* ignored */ }
            try { _writer?.Dispose(); } catch { /* ignored */ }
            try { _tcp?.Close(); } catch { /* ignored */ }
            _writer = null;
            _tcp = null;
        }
    }
}
