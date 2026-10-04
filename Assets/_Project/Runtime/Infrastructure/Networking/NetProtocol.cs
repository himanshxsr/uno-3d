#nullable enable

using System;
using Uno.Core.Enums;

namespace Uno.Infrastructure.Networking
{
    public enum NetMessageType
    {
        JoinRequest = 1,
        JoinAccepted = 2,
        JoinRejected = 3,
        LobbyUpdate = 4,
        ReadyToggle = 5,
        StartGame = 6,
        PlayerCommand = 7,
        GameSnapshot = 8,
        Chat = 9,
        Leave = 10,
        Ping = 11,
        // Cloud relay handshake (AWS)
        RelayHostCreate = 100,
        RelayHostCreated = 101,
        RelayClientJoin = 102,
        RelayClientJoined = 103
    }

    public enum PlayerCommandType
    {
        PlayCard = 1,
        DrawCard = 2,
        SelectColor = 3,
        CallUno = 4,
        PassAfterDraw = 5
    }

    [Serializable]
    public sealed class NetEnvelope
    {
        public NetMessageType Type;
        public string PayloadJson = string.Empty;
    }

    [Serializable]
    public sealed class JoinRequestMsg
    {
        public string PlayerName = "Guest";
        public string RoomCode = string.Empty;
        public string ClientId = string.Empty;
    }

    [Serializable]
    public sealed class JoinAcceptedMsg
    {
        public int SeatIndex;
        public string ClientId = string.Empty;
        public string HostName = string.Empty;
        public string RoomCode = string.Empty;
    }

    [Serializable]
    public sealed class LobbySeatMsg
    {
        public int SeatIndex;
        public string Name = string.Empty;
        public string Kind = "Bot";
        public bool IsReady;
        public string ClientId = string.Empty;
    }

    [Serializable]
    public sealed class LobbyUpdateMsg
    {
        public string RoomCode = string.Empty;
        public string HostAddress = string.Empty;
        public int HostPort;
        public LobbySeatMsg[] Seats = Array.Empty<LobbySeatMsg>();
        public bool CanStart;
    }

    [Serializable]
    public sealed class StartGameMsg
    {
        public int Seed;
        public LobbySeatMsg[] Seats = Array.Empty<LobbySeatMsg>();
    }

    [Serializable]
    public sealed class PlayerCommandMsg
    {
        public string ClientId = string.Empty;
        public int SeatIndex;
        public PlayerCommandType Command;
        public string CardId = string.Empty;
        public CardColor Color;
    }

    [Serializable]
    public sealed class CardDto
    {
        public string Id = string.Empty;
        public CardColor Color;
        public CardType Type;
        public int Value;
    }

    [Serializable]
    public sealed class GameSnapshotMsg
    {
        public int LocalSeat;
        public string[] Names = Array.Empty<string>();
        public string[] Kinds = Array.Empty<string>();
        public int[] HandCounts = Array.Empty<int>();
        public CardDto[] MyHand = Array.Empty<CardDto>();
        public bool HasTopDiscard;
        public CardDto TopDiscard = new CardDto();
        public int DrawCount;
        public int DiscardCount;
        public CardColor ActiveColor;
        public int CurrentSeat;
        public string Direction = "Clockwise";
        public float TimerSeconds = 15f;
        public string Log = string.Empty;
        public bool ShowColorPicker;
        public bool ShowUnoButton;
        public bool RoundEnded;
        public int WinnerSeat = -1;
        public int Score;
        public bool InputEnabled;
        public string TurnMessage = string.Empty;
    }

    [Serializable]
    public sealed class LanAdvertiseMsg
    {
        public string RoomCode = string.Empty;
        public string HostName = string.Empty;
        public string Address = string.Empty;
        public int Port = 17777;
        public int OpenSeats = 3;
    }
}
