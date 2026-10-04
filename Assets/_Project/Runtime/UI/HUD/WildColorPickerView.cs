#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Uno.Core.Enums;

namespace Uno.UI.HUD
{
    /// <summary>
    /// Radial wild-color selection modal with four suit quadrants.
    /// </summary>
    public class WildColorPickerView : MonoBehaviour
    {
        [SerializeField] private GameObject? _root;
        [SerializeField] private Button? _redButton;
        [SerializeField] private Button? _blueButton;
        [SerializeField] private Button? _greenButton;
        [SerializeField] private Button? _yellowButton;
        [SerializeField] private TextMeshProUGUI? _titleText;

        /// <summary>
        /// Raised when the player selects a suit color.
        /// </summary>
        public event Action<CardColor>? OnColorSelected;

        private void Awake()
        {
            WireButton(_redButton, CardColor.Red);
            WireButton(_blueButton, CardColor.Blue);
            WireButton(_greenButton, CardColor.Green);
            WireButton(_yellowButton, CardColor.Yellow);
            Hide();
        }

        /// <summary>
        /// Assigns button references when the modal is built procedurally at runtime.
        /// </summary>
        public void Configure(
            GameObject root,
            Button red,
            Button blue,
            Button green,
            Button yellow,
            TextMeshProUGUI? title = null)
        {
            _root = root;
            _redButton = red;
            _blueButton = blue;
            _greenButton = green;
            _yellowButton = yellow;
            _titleText = title;

            WireButton(_redButton, CardColor.Red);
            WireButton(_blueButton, CardColor.Blue);
            WireButton(_greenButton, CardColor.Green);
            WireButton(_yellowButton, CardColor.Yellow);
        }

        public void Show()
        {
            if (_root != null)
            {
                _root.SetActive(true);
            }

            if (_titleText != null)
            {
                _titleText.text = "CHOOSE A COLOR";
            }
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private void WireButton(Button? button, CardColor color)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                OnColorSelected?.Invoke(color);
                Hide();
            });
        }
    }
}
