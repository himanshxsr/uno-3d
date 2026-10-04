#nullable enable

using UnityEngine;

namespace Uno.Infrastructure.Networking
{
    public static class JsonNet
    {
        public static string ToJson<T>(T value) => JsonUtility.ToJson(value);

        public static T FromJson<T>(string json) => JsonUtility.FromJson<T>(json);

        public static string Wrap(NetMessageType type, object payload) =>
            ToJson(new NetEnvelope
            {
                Type = type,
                PayloadJson = payload is string s ? s : ToJson(payload)
            });
    }
}
