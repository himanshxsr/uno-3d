#nullable enable

using UnityEngine;

namespace Uno.UI.Menus
{
    /// <summary>
    /// Drop on MainMenu scene (or auto-created) to build the polished menu UI.
    /// </summary>
    public sealed class MainMenuEntry : MonoBehaviour
    {
        private void Awake()
        {
            MainMenuController.Ensure();
        }
    }
}
