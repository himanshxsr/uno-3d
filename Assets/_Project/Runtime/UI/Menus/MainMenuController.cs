#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Uno.Core.Session;
using Uno.Infrastructure.Networking;
using Uno.Infrastructure.Session;

namespace Uno.UI.Menus
{
    /// <summary>
    /// Production main menu: Offline bots, Online friends (host/join/LAN), settings.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private enum Screen
        {
            Home,
            Offline,
            OnlineHub,
            HostLobby,
            JoinLobby,
            Settings
        }

        private GameSessionBootstrap _session = null!;
        private LanRoomBrowser? _browser;
        private Screen _screen = Screen.Home;

        private GameObject _home = null!;
        private GameObject _offline = null!;
        private GameObject _onlineHub = null!;
        private GameObject _hostLobby = null!;
        private GameObject _joinLobby = null!;
        private GameObject _settings = null!;

        private InputField _offlineName = null!;
        private Text _offlineDiffLabel = null!;
        private int _botDifficulty = 1;

        private InputField _onlineName = null!;
        private Text _hostCodeLabel = null!;
        private Text _hostShareLabel = null!;
        private Text _hostSeatsLabel = null!;
        private Text _hostStatusLabel = null!;

        private InputField _joinCodeOrShare = null!;
        private Text _lanRoomsLabel = null!;
        private Text _joinStatusLabel = null!;
        private Text _joinSeatsLabel = null!;
        private Button _joinReadyButton = null!;
        private Button _joinStartHint = null!;

        private Text _settingsStatus = null!;
        private bool _soundEnabled = true;

        public static MainMenuController Ensure()
        {
            MainMenuController? existing = FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                return existing;
            }

            Canvas canvas = UiTheme.CreateOverlayCanvas("UNO Main Menu Canvas", 300);
            GameObject root = canvas.gameObject;
            MainMenuController menu = root.AddComponent<MainMenuController>();
            menu.Build(root.transform);
            return menu;
        }

        private void Awake()
        {
            _session = GameSessionBootstrap.Ensure();
            if (_home == null)
            {
                Build(transform);
            }
        }

        private void Update()
        {
            _browser?.Pump();
        }

        private void OnDestroy()
        {
            _browser?.Dispose();
        }

        private void Build(Transform root)
        {
            // Atmospheric background
            GameObject bg = UiTheme.Panel("Background", root, UiTheme.Felt);
            UiTheme.Stretch(bg.GetComponent<RectTransform>());

            GameObject veil = UiTheme.Panel("Veil", root, new Color(0.05f, 0.06f, 0.1f, 0.55f));
            UiTheme.Stretch(veil.GetComponent<RectTransform>());

            Text brand = UiTheme.Label("Brand", root, "UNO 3D", 72, FontStyle.Bold, TextAnchor.MiddleCenter, UiTheme.Golden);
            UiTheme.SetAnchors(brand.rectTransform, 0.1f, 0.82f, 0.9f, 0.95f);

            Text tagline = UiTheme.Label("Tagline", root, "PLAY WITH FRIENDS  •  CHALLENGE THE BOTS", 22, FontStyle.Bold, TextAnchor.MiddleCenter, UiTheme.TextMuted);
            UiTheme.SetAnchors(tagline.rectTransform, 0.08f, 0.76f, 0.92f, 0.82f);

            _home = BuildHome(root);
            _offline = BuildOffline(root);
            _onlineHub = BuildOnlineHub(root);
            _hostLobby = BuildHostLobby(root);
            _joinLobby = BuildJoinLobby(root);
            _settings = BuildSettings(root);

            Show(Screen.Home);
        }

        private GameObject BuildHome(Transform root)
        {
            GameObject panel = UiTheme.Panel("Home", root, Color.clear);
            UiTheme.SetAnchors(panel.GetComponent<RectTransform>(), 0.12f, 0.18f, 0.88f, 0.72f);

            Button offline = UiTheme.Button("PlayOffline", panel.transform, "PLAY OFFLINE\nvs AI BOTS", UiTheme.Emerald, 30);
            UiTheme.SetAnchors(offline.GetComponent<RectTransform>(), 0.08f, 0.62f, 0.92f, 0.9f);
            offline.onClick.AddListener(() => Show(Screen.Offline));

            Button online = UiTheme.Button("PlayOnline", panel.transform, "PLAY ONLINE\nWITH FRIENDS", UiTheme.RoyalBlue, 30);
            UiTheme.SetAnchors(online.GetComponent<RectTransform>(), 0.08f, 0.32f, 0.92f, 0.58f);
            online.onClick.AddListener(() => Show(Screen.OnlineHub));

            Button settings = UiTheme.Button("Settings", panel.transform, "SETTINGS", UiTheme.SlateSoft, 26);
            UiTheme.SetAnchors(settings.GetComponent<RectTransform>(), 0.08f, 0.12f, 0.48f, 0.26f);
            settings.onClick.AddListener(() => Show(Screen.Settings));

            Button quit = UiTheme.Button("Quit", panel.transform, "QUIT", UiTheme.Crimson, 26);
            UiTheme.SetAnchors(quit.GetComponent<RectTransform>(), 0.52f, 0.12f, 0.92f, 0.26f);
            quit.onClick.AddListener(QuitApp);

            return panel;
        }

        private GameObject BuildOffline(Transform root)
        {
            GameObject panel = UiTheme.Panel("Offline", root, UiTheme.Slate);
            UiTheme.SetAnchors(panel.GetComponent<RectTransform>(), 0.08f, 0.2f, 0.92f, 0.74f);

            UiTheme.Label("Title", panel.transform, "OFFLINE MATCH", 34, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white)
                .rectTransform.anchoredPosition = new Vector2(0f, 210f);

            _offlineName = UiTheme.Input("Name", panel.transform, "Your name");
            UiTheme.SetAnchors(_offlineName.GetComponent<RectTransform>(), 0.1f, 0.62f, 0.9f, 0.74f);
            _offlineName.text = PlayerPrefs.GetString("uno.playerName", "You");

            _offlineDiffLabel = UiTheme.Label("Diff", panel.transform, "BOT DIFFICULTY: NORMAL", 24, FontStyle.Bold, TextAnchor.MiddleCenter, UiTheme.Golden);
            UiTheme.SetAnchors(_offlineDiffLabel.rectTransform, 0.1f, 0.48f, 0.9f, 0.58f);

            Button easier = UiTheme.Button("Easier", panel.transform, "EASIER", UiTheme.SlateSoft, 22);
            UiTheme.SetAnchors(easier.GetComponent<RectTransform>(), 0.1f, 0.34f, 0.48f, 0.46f);
            easier.onClick.AddListener(() =>
            {
                _botDifficulty = Mathf.Max(0, _botDifficulty - 1);
                RefreshDiffLabel();
            });

            Button harder = UiTheme.Button("Harder", panel.transform, "HARDER", UiTheme.SlateSoft, 22);
            UiTheme.SetAnchors(harder.GetComponent<RectTransform>(), 0.52f, 0.34f, 0.9f, 0.46f);
            harder.onClick.AddListener(() =>
            {
                _botDifficulty = Mathf.Min(2, _botDifficulty + 1);
                RefreshDiffLabel();
            });

            Button start = UiTheme.Button("Start", panel.transform, "START MATCH", UiTheme.Emerald, 28);
            UiTheme.SetAnchors(start.GetComponent<RectTransform>(), 0.1f, 0.14f, 0.9f, 0.28f);
            start.onClick.AddListener(StartOffline);

            Button back = UiTheme.Button("Back", panel.transform, "BACK", UiTheme.Crimson, 22);
            UiTheme.SetAnchors(back.GetComponent<RectTransform>(), 0.3f, 0.04f, 0.7f, 0.12f);
            back.onClick.AddListener(() => Show(Screen.Home));

            RefreshDiffLabel();
            return panel;
        }

        private GameObject BuildOnlineHub(Transform root)
        {
            GameObject panel = UiTheme.Panel("OnlineHub", root, UiTheme.Slate);
            UiTheme.SetAnchors(panel.GetComponent<RectTransform>(), 0.08f, 0.22f, 0.92f, 0.74f);

            UiTheme.Label("Title", panel.transform, "ONLINE WITH FRIENDS", 32, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white)
                .rectTransform.anchoredPosition = new Vector2(0f, 200f);

            _onlineName = UiTheme.Input("OnlineName", panel.transform, "Your name");
            UiTheme.SetAnchors(_onlineName.GetComponent<RectTransform>(), 0.1f, 0.62f, 0.9f, 0.74f);
            _onlineName.text = PlayerPrefs.GetString("uno.playerName", "You");

            Button host = UiTheme.Button("Host", panel.transform, "CREATE ROOM", UiTheme.RoyalBlue, 28);
            UiTheme.SetAnchors(host.GetComponent<RectTransform>(), 0.1f, 0.4f, 0.9f, 0.56f);
            host.onClick.AddListener(StartHostLobby);

            Button join = UiTheme.Button("Join", panel.transform, "JOIN ROOM", UiTheme.Emerald, 28);
            UiTheme.SetAnchors(join.GetComponent<RectTransform>(), 0.1f, 0.2f, 0.9f, 0.36f);
            join.onClick.AddListener(() =>
            {
                Show(Screen.JoinLobby);
                StartLanBrowser();
            });

            Button back = UiTheme.Button("Back", panel.transform, "BACK", UiTheme.Crimson, 22);
            UiTheme.SetAnchors(back.GetComponent<RectTransform>(), 0.3f, 0.06f, 0.7f, 0.14f);
            back.onClick.AddListener(() => Show(Screen.Home));

            return panel;
        }

        private GameObject BuildHostLobby(Transform root)
        {
            GameObject panel = UiTheme.Panel("HostLobby", root, UiTheme.Slate);
            UiTheme.SetAnchors(panel.GetComponent<RectTransform>(), 0.06f, 0.12f, 0.94f, 0.78f);

            UiTheme.Label("Title", panel.transform, "HOST LOBBY", 32, FontStyle.Bold, TextAnchor.MiddleCenter, UiTheme.Golden)
                .rectTransform.anchoredPosition = new Vector2(0f, 290f);

            _hostCodeLabel = UiTheme.Label("Code", panel.transform, "CODE: ------", 36, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            UiTheme.SetAnchors(_hostCodeLabel.rectTransform, 0.08f, 0.78f, 0.92f, 0.88f);

            _hostShareLabel = UiTheme.Label("Share", panel.transform, "Share: —", 18, FontStyle.Normal, TextAnchor.MiddleCenter, UiTheme.TextMuted);
            UiTheme.SetAnchors(_hostShareLabel.rectTransform, 0.06f, 0.7f, 0.94f, 0.78f);

            _hostSeatsLabel = UiTheme.Label("Seats", panel.transform, "Seats...", 22, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
            UiTheme.SetAnchors(_hostSeatsLabel.rectTransform, 0.1f, 0.36f, 0.9f, 0.68f);

            _hostStatusLabel = UiTheme.Label("Status", panel.transform, string.Empty, 18, FontStyle.Italic, TextAnchor.MiddleCenter, UiTheme.TextMuted);
            UiTheme.SetAnchors(_hostStatusLabel.rectTransform, 0.08f, 0.28f, 0.92f, 0.36f);

            Button copy = UiTheme.Button("Copy", panel.transform, "COPY INVITE", UiTheme.SlateSoft, 22);
            UiTheme.SetAnchors(copy.GetComponent<RectTransform>(), 0.08f, 0.18f, 0.48f, 0.27f);
            copy.onClick.AddListener(() =>
            {
                GUIUtility.systemCopyBuffer = _session.GetShareString();
                _hostStatusLabel.text = "Invite copied to clipboard.";
            });

            Button fillBots = UiTheme.Button("FillBots", panel.transform, "FILL BOTS", UiTheme.Golden, 22);
            fillBots.GetComponentInChildren<Text>().color = Color.black;
            UiTheme.SetAnchors(fillBots.GetComponent<RectTransform>(), 0.52f, 0.18f, 0.92f, 0.27f);
            fillBots.onClick.AddListener(() =>
            {
                _session.RelayHost?.FillBotsAndMarkReady();
                _session.Host?.FillBotsAndMarkReady();
                RefreshHostLobbyUi(_session.BuildLobbyUpdate());
            });

            Button start = UiTheme.Button("Start", panel.transform, "START GAME", UiTheme.Emerald, 26);
            UiTheme.SetAnchors(start.GetComponent<RectTransform>(), 0.08f, 0.08f, 0.92f, 0.16f);
            start.onClick.AddListener(HostStartGame);

            Button back = UiTheme.Button("Back", panel.transform, "LEAVE", UiTheme.Crimson, 20);
            UiTheme.SetAnchors(back.GetComponent<RectTransform>(), 0.3f, 0.01f, 0.7f, 0.07f);
            back.onClick.AddListener(() =>
            {
                _session.DisposeNetwork();
                Show(Screen.OnlineHub);
            });

            return panel;
        }

        private GameObject BuildJoinLobby(Transform root)
        {
            GameObject panel = UiTheme.Panel("JoinLobby", root, UiTheme.Slate);
            UiTheme.SetAnchors(panel.GetComponent<RectTransform>(), 0.06f, 0.1f, 0.94f, 0.78f);

            UiTheme.Label("Title", panel.transform, "JOIN FRIENDS", 32, FontStyle.Bold, TextAnchor.MiddleCenter, UiTheme.Golden)
                .rectTransform.anchoredPosition = new Vector2(0f, 300f);

            _joinCodeOrShare = UiTheme.Input("JoinInput", panel.transform, "Room code or IP:port:code");
            UiTheme.SetAnchors(_joinCodeOrShare.GetComponent<RectTransform>(), 0.08f, 0.78f, 0.92f, 0.88f);

            _lanRoomsLabel = UiTheme.Label("Lan", panel.transform, "Scanning LAN rooms...", 18, FontStyle.Normal, TextAnchor.UpperLeft, UiTheme.TextMuted);
            UiTheme.SetAnchors(_lanRoomsLabel.rectTransform, 0.08f, 0.56f, 0.92f, 0.76f);

            _joinSeatsLabel = UiTheme.Label("Seats", panel.transform, string.Empty, 20, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
            UiTheme.SetAnchors(_joinSeatsLabel.rectTransform, 0.08f, 0.34f, 0.92f, 0.54f);

            _joinStatusLabel = UiTheme.Label("Status", panel.transform, string.Empty, 18, FontStyle.Italic, TextAnchor.MiddleCenter, UiTheme.TextMuted);
            UiTheme.SetAnchors(_joinStatusLabel.rectTransform, 0.08f, 0.26f, 0.92f, 0.34f);

            Button connect = UiTheme.Button("Connect", panel.transform, "CONNECT", UiTheme.RoyalBlue, 24);
            UiTheme.SetAnchors(connect.GetComponent<RectTransform>(), 0.08f, 0.16f, 0.48f, 0.25f);
            connect.onClick.AddListener(ConnectToRoom);

            _joinReadyButton = UiTheme.Button("Ready", panel.transform, "READY", UiTheme.Emerald, 24);
            UiTheme.SetAnchors(_joinReadyButton.GetComponent<RectTransform>(), 0.52f, 0.16f, 0.92f, 0.25f);
            _joinReadyButton.onClick.AddListener(() => _session.Client?.ToggleReady());

            _joinStartHint = UiTheme.Button("Wait", panel.transform, "WAITING FOR HOST...", UiTheme.SlateSoft, 20);
            UiTheme.SetAnchors(_joinStartHint.GetComponent<RectTransform>(), 0.08f, 0.08f, 0.92f, 0.14f);
            _joinStartHint.interactable = false;

            Button back = UiTheme.Button("Back", panel.transform, "BACK", UiTheme.Crimson, 20);
            UiTheme.SetAnchors(back.GetComponent<RectTransform>(), 0.3f, 0.01f, 0.7f, 0.07f);
            back.onClick.AddListener(() =>
            {
                _browser?.Dispose();
                _browser = null;
                _session.DisposeNetwork();
                Show(Screen.OnlineHub);
            });

            return panel;
        }

        private GameObject BuildSettings(Transform root)
        {
            GameObject panel = UiTheme.Panel("Settings", root, UiTheme.Slate);
            UiTheme.SetAnchors(panel.GetComponent<RectTransform>(), 0.12f, 0.3f, 0.88f, 0.7f);

            UiTheme.Label("Title", panel.transform, "SETTINGS", 34, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white)
                .rectTransform.anchoredPosition = new Vector2(0f, 140f);

            _settingsStatus = UiTheme.Label("Status", panel.transform, "Sound: ON", 26, FontStyle.Bold, TextAnchor.MiddleCenter, UiTheme.Golden);
            UiTheme.SetAnchors(_settingsStatus.rectTransform, 0.1f, 0.45f, 0.9f, 0.6f);

            Button toggle = UiTheme.Button("Sound", panel.transform, "TOGGLE SOUND", UiTheme.RoyalBlue, 24);
            UiTheme.SetAnchors(toggle.GetComponent<RectTransform>(), 0.15f, 0.25f, 0.85f, 0.4f);
            toggle.onClick.AddListener(() =>
            {
                _soundEnabled = !_soundEnabled;
                PlayerPrefs.SetInt("uno.sound", _soundEnabled ? 1 : 0);
                AudioListener.volume = _soundEnabled ? 1f : 0f;
                _settingsStatus.text = _soundEnabled ? "Sound: ON" : "Sound: OFF";
            });

            Button back = UiTheme.Button("Back", panel.transform, "BACK", UiTheme.Crimson, 22);
            UiTheme.SetAnchors(back.GetComponent<RectTransform>(), 0.25f, 0.08f, 0.75f, 0.2f);
            back.onClick.AddListener(() => Show(Screen.Home));

            _soundEnabled = PlayerPrefs.GetInt("uno.sound", 1) == 1;
            AudioListener.volume = _soundEnabled ? 1f : 0f;
            _settingsStatus.text = _soundEnabled ? "Sound: ON" : "Sound: OFF";
            return panel;
        }

        private void Show(Screen screen)
        {
            _screen = screen;
            _home.SetActive(screen == Screen.Home);
            _offline.SetActive(screen == Screen.Offline);
            _onlineHub.SetActive(screen == Screen.OnlineHub);
            _hostLobby.SetActive(screen == Screen.HostLobby);
            _joinLobby.SetActive(screen == Screen.JoinLobby);
            _settings.SetActive(screen == Screen.Settings);
        }

        private void RefreshDiffLabel()
        {
            string label = _botDifficulty switch
            {
                0 => "EASY",
                2 => "HARD",
                _ => "NORMAL"
            };
            _offlineDiffLabel.text = $"BOT DIFFICULTY: {label}";
        }

        private void StartOffline()
        {
            string name = string.IsNullOrWhiteSpace(_offlineName.text) ? "You" : _offlineName.text.Trim();
            PlayerPrefs.SetString("uno.playerName", name);
            _session.ConfigureOffline(name, _botDifficulty);
            LaunchMatch();
        }

        private void StartHostLobby()
        {
            string name = string.IsNullOrWhiteSpace(_onlineName.text) ? "Host" : _onlineName.text.Trim();
            PlayerPrefs.SetString("uno.playerName", name);
            GameSessionConfig config = _session.BeginHosting(name);
            if (_session.RelayHost != null)
            {
                _session.RelayHost.OnLobbyChanged += RefreshHostLobbyUi;
                _session.RelayHost.OnLog += msg => _hostStatusLabel.text = msg;
            }

            if (_session.Host != null)
            {
                _session.Host.OnLobbyChanged += RefreshHostLobbyUi;
                _session.Host.OnLog += msg => _hostStatusLabel.text = msg;
            }

            _hostCodeLabel.text = $"CODE: {config.RoomCode}";
            _hostShareLabel.text = _session.UsesCloudRelay
                ? $"Friends join with CODE only → relay {RelaySettings.Host}"
                : $"Share: {_session.GetShareString()}";
            _hostStatusLabel.text = _session.UsesCloudRelay
                ? "Connecting to cloud relay..."
                : "LAN host ready.";
            RefreshHostLobbyUi(_session.BuildLobbyUpdate());
            Show(Screen.HostLobby);
        }

        private void RefreshHostLobbyUi(LobbyUpdateMsg? lobby)
        {
            if (lobby == null)
            {
                return;
            }

            _hostCodeLabel.text = $"CODE: {lobby.RoomCode}";
            _hostShareLabel.text = $"Share: {lobby.HostAddress}:{lobby.HostPort}:{lobby.RoomCode}";
            _hostSeatsLabel.text = FormatSeats(lobby.Seats);
        }

        private void HostStartGame()
        {
            if (_session.Host == null && _session.RelayHost == null)
            {
                return;
            }

            int seed = Random.Range(1, int.MaxValue);
            _session.FillBotsAndBeginGame(seed);
            _session.MarkMatchReady();
            LaunchMatch();
        }

        private void StartLanBrowser()
        {
            _browser?.Dispose();
            _browser = new LanRoomBrowser();
            _browser.OnRoomsUpdated += rooms =>
            {
                if (rooms.Count == 0)
                {
                    _lanRoomsLabel.text = "No LAN rooms yet. Ask your friend for their invite string.";
                    return;
                }

                var lines = new List<string> { "LAN ROOMS (tap Connect after pasting code):" };
                for (int i = 0; i < rooms.Count; i++)
                {
                    LanAdvertiseMsg r = rooms[i];
                    lines.Add($"• {r.RoomCode} — {r.HostName} @ {r.Address}:{r.Port} ({r.OpenSeats} open)");
                }

                _lanRoomsLabel.text = string.Join("\n", lines);
                if (string.IsNullOrWhiteSpace(_joinCodeOrShare.text) && rooms.Count > 0)
                {
                    _joinCodeOrShare.text = RoomCodeUtil.BuildShareString(rooms[0].Address, rooms[0].Port, rooms[0].RoomCode);
                }
            };
            _browser.Start();
            _lanRoomsLabel.text = "Scanning LAN rooms...";
        }

        private async void ConnectToRoom()
        {
            string name = PlayerPrefs.GetString("uno.playerName", "Guest");
            if (_onlineName != null && !string.IsNullOrWhiteSpace(_onlineName.text))
            {
                name = _onlineName.text.Trim();
                PlayerPrefs.SetString("uno.playerName", name);
            }

            if (!RoomCodeUtil.TryParseShareString(_joinCodeOrShare.text, out string address, out int port, out string code))
            {
                _joinStatusLabel.text = "Enter room CODE (cloud) or IP:port:CODE (LAN).";
                return;
            }

            // Prefer cloud relay when user entered a bare room code.
            bool bareCode = _joinCodeOrShare.text.Trim().IndexOf(':') < 0;
            if (bareCode && !string.IsNullOrEmpty(code))
            {
                address = RelaySettings.Host;
                port = RelaySettings.Port;
                PlayerPrefs.SetInt("uno.useRelay", 1);
            }
            else if (address == "127.0.0.1" && _browser != null)
            {
                foreach (LanAdvertiseMsg room in _browser.SnapshotRooms())
                {
                    if (string.Equals(room.RoomCode, code, System.StringComparison.OrdinalIgnoreCase))
                    {
                        address = room.Address;
                        port = room.Port;
                        PlayerPrefs.SetInt("uno.useRelay", 0);
                        break;
                    }
                }
            }

            UnoNetworkClient client = _session.BeginClient();
            client.OnJoined += accepted =>
            {
                _joinStatusLabel.text = $"Joined seat {accepted.SeatIndex + 1}. Press READY.";
                _session.ApplyClientSession(accepted, null, name);
            };
            client.OnJoinRejected += reason => _joinStatusLabel.text = reason;
            client.OnLobbyUpdate += lobby =>
            {
                _session.ApplyLobbySeats(lobby);
                _joinSeatsLabel.text = FormatSeats(lobby.Seats);
            };
            client.OnStartGame += start =>
            {
                _session.ApplyStartGame(start);
                _joinStatusLabel.text = "Host started — loading table...";
                LaunchMatch();
            };
            client.OnLog += msg => _joinStatusLabel.text = msg;

            _joinStatusLabel.text = "Connecting...";
            bool ok = await client.ConnectAsync(address, port, name, code);
            if (!ok)
            {
                _joinStatusLabel.text = "Could not connect. Check invite / same Wi‑Fi.";
            }
        }

        private static string FormatSeats(LobbySeatMsg[] seats)
        {
            if (seats == null || seats.Length == 0)
            {
                return "No seats";
            }

            var lines = new List<string>();
            for (int i = 0; i < seats.Length; i++)
            {
                LobbySeatMsg s = seats[i];
                string ready = s.IsReady ? "READY" : "WAIT";
                lines.Add($"{i + 1}. {s.Name}  [{s.Kind}]  {ready}");
            }

            return string.Join("\n", lines);
        }

        private void LaunchMatch()
        {
            // Prefer in-scene launch when MainGame is already open (no separate menu scene required).
            var controllerType = System.Type.GetType("Uno.Presentation.Controllers.UnoGameController, Uno.Presentation");
            Object? controller = controllerType != null ? FindAnyObjectByType(controllerType) : null;
            if (controller != null)
            {
                gameObject.SetActive(false);
                var method = controllerType!.GetMethod("BeginSessionFromMenu");
                method?.Invoke(controller, null);
                return;
            }

            if (UnityEngine.Application.CanStreamedLevelBeLoaded("MainGame"))
            {
                SceneManager.LoadScene("MainGame");
            }
            else
            {
                SceneManager.LoadScene(0);
            }
        }

        private static void QuitApp()
        {
#if UNITY_EDITOR
            System.Type? editorApp = System.Type.GetType("UnityEditor.EditorApplication, UnityEditor");
            editorApp?.GetProperty("isPlaying")?.SetValue(null, false);
#else
            UnityEngine.Application.Quit();
#endif
        }
    }
}
