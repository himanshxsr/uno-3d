#nullable enable

namespace Uno.Infrastructure.Networking
{
    /// <summary>
    /// Default public relay endpoint. Override via PlayerPrefs key "uno.relayHost".
    /// </summary>
    public static class RelaySettings
    {
        public const string PrefHostKey = "uno.relayHost";
        public const string PrefPortKey = "uno.relayPort";

        /// <summary>
        /// Placeholder until DNS is pointed; updated after AWS deploy.
        /// </summary>
        public const string DefaultHost = "127.0.0.1";
        public const int DefaultPort = GameSessionPorts.DefaultTcpPort;

        public static string Host =>
            UnityEngine.PlayerPrefs.GetString(PrefHostKey, DefaultHost);

        public static int Port =>
            UnityEngine.PlayerPrefs.GetInt(PrefPortKey, DefaultPort);

        public static void SetEndpoint(string host, int port)
        {
            UnityEngine.PlayerPrefs.SetString(PrefHostKey, host);
            UnityEngine.PlayerPrefs.SetInt(PrefPortKey, port);
            UnityEngine.PlayerPrefs.Save();
        }
    }
}
