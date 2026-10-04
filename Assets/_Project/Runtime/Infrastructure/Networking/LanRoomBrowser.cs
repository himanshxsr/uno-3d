#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Uno.Infrastructure.Networking
{
    public sealed class LanRoomBrowser : IDisposable
    {
        private readonly ConcurrentDictionary<string, LanAdvertiseMsg> _rooms = new ConcurrentDictionary<string, LanAdvertiseMsg>();
        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private UdpClient? _udp;
        private CancellationTokenSource? _cts;

        public event Action<IReadOnlyList<LanAdvertiseMsg>>? OnRoomsUpdated;

        public void Start()
        {
            Dispose();
            _cts = new CancellationTokenSource();
            _udp = new UdpClient(GameSessionPorts.DefaultUdpAdvertisePort);
            _udp.EnableBroadcast = true;
            _ = Task.Run(() => ListenLoop(_cts.Token));
        }

        public void Pump()
        {
            while (_mainThread.TryDequeue(out Action? action))
            {
                action?.Invoke();
            }
        }

        public IReadOnlyList<LanAdvertiseMsg> SnapshotRooms()
        {
            return new List<LanAdvertiseMsg>(_rooms.Values);
        }

        private async Task ListenLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _udp != null)
                {
                    UdpReceiveResult result = await _udp.ReceiveAsync();
                    string json = Encoding.UTF8.GetString(result.Buffer);
                    try
                    {
                        LanAdvertiseMsg ad = JsonNet.FromJson<LanAdvertiseMsg>(json);
                        if (string.IsNullOrEmpty(ad.RoomCode))
                        {
                            continue;
                        }

                        if (string.IsNullOrEmpty(ad.Address))
                        {
                            ad.Address = result.RemoteEndPoint.Address.ToString();
                        }

                        _rooms[ad.RoomCode] = ad;
                        var list = new List<LanAdvertiseMsg>(_rooms.Values);
                        _mainThread.Enqueue(() => OnRoomsUpdated?.Invoke(list));
                    }
                    catch
                    {
                        // ignore malformed
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch
            {
                // ignored
            }
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { /* ignored */ }
            try { _udp?.Dispose(); } catch { /* ignored */ }
            _udp = null;
            _rooms.Clear();
        }
    }
}
