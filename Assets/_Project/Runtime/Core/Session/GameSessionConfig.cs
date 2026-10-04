#nullable enable

using System;

namespace Uno.Core.Session
{
    [Serializable]
    public sealed class SeatConfig
    {
        public int SeatIndex;
        public string DisplayName = "Player";
        public PlayerKind Kind = PlayerKind.Bot;
        public string NetworkClientId = string.Empty;
        public bool IsReady;
    }

    [Serializable]
    public sealed class GameSessionConfig
    {
        public const int MaxSeats = 4;
        public const int DefaultPort = 17777;

        public GameMode Mode = GameMode.OfflineBots;
        public string LocalPlayerName = "You";
        public string RoomCode = string.Empty;
        public string HostAddress = "127.0.0.1";
        public int HostPort = DefaultPort;
        public int BotDifficulty = 1; // 0 easy, 1 normal, 2 hard
        public int Seed;
        public SeatConfig[] Seats = Array.Empty<SeatConfig>();

        public static GameSessionConfig CreateDefaultOfflineSeats(string localName)
        {
            return new GameSessionConfig
            {
                Mode = GameMode.OfflineBots,
                LocalPlayerName = localName,
                Seed = Environment.TickCount,
                Seats = new[]
                {
                    new SeatConfig { SeatIndex = 0, DisplayName = localName, Kind = PlayerKind.LocalHuman, IsReady = true },
                    new SeatConfig { SeatIndex = 1, DisplayName = "Sarah", Kind = PlayerKind.Bot, IsReady = true },
                    new SeatConfig { SeatIndex = 2, DisplayName = "Alex", Kind = PlayerKind.Bot, IsReady = true },
                    new SeatConfig { SeatIndex = 3, DisplayName = "David", Kind = PlayerKind.Bot, IsReady = true }
                }
            };
        }

        public static GameSessionConfig CreateOnlineHost(string hostName, string roomCode)
        {
            GameSessionConfig config = CreateDefaultOfflineSeats(hostName);
            config.Mode = GameMode.OnlineHost;
            config.RoomCode = roomCode;
            config.Seats[1].Kind = PlayerKind.RemoteHuman;
            config.Seats[1].DisplayName = "Waiting...";
            config.Seats[1].IsReady = false;
            config.Seats[2].Kind = PlayerKind.RemoteHuman;
            config.Seats[2].DisplayName = "Waiting...";
            config.Seats[2].IsReady = false;
            config.Seats[3].Kind = PlayerKind.RemoteHuman;
            config.Seats[3].DisplayName = "Waiting...";
            config.Seats[3].IsReady = false;
            return config;
        }

        public string GetSeatName(int seat) =>
            seat >= 0 && seat < Seats.Length ? Seats[seat].DisplayName : $"P{seat}";

        public PlayerKind GetSeatKind(int seat) =>
            seat >= 0 && seat < Seats.Length ? Seats[seat].Kind : PlayerKind.Bot;

        public void FillEmptySeatsWithBots()
        {
            string[] botNames = { "Sarah", "Alex", "David", "Riley" };
            for (int i = 1; i < Seats.Length; i++)
            {
                SeatConfig seat = Seats[i];
                if (seat.Kind == PlayerKind.RemoteHuman && string.IsNullOrEmpty(seat.NetworkClientId))
                {
                    seat.Kind = PlayerKind.Bot;
                    seat.DisplayName = botNames[(i - 1) % botNames.Length];
                    seat.IsReady = true;
                }
            }
        }
    }
}
