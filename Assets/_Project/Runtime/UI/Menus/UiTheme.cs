#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace Uno.UI.Menus
{
    public static class UiTheme
    {
        public static readonly Color Crimson = new Color(0.906f, 0.114f, 0.212f, 1f);
        public static readonly Color RoyalBlue = new Color(0f, 0.4f, 1f, 1f);
        public static readonly Color Emerald = new Color(0f, 0.722f, 0.396f, 1f);
        public static readonly Color Golden = new Color(1f, 0.784f, 0f, 1f);
        public static readonly Color Slate = new Color(0.078f, 0.09f, 0.122f, 0.94f);
        public static readonly Color SlateSoft = new Color(0.12f, 0.14f, 0.2f, 0.92f);
        public static readonly Color Felt = new Color(0.071f, 0.247f, 0.157f, 1f);
        public static readonly Color TextMuted = new Color(0.627f, 0.682f, 0.753f, 1f);

        private static Font? _font;

        public static Font Font
        {
            get
            {
                if (_font != null)
                {
                    return _font;
                }

                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf")
                        ?? Font.CreateDynamicFontFromOSFont("Arial", 28);
                return _font;
            }
        }

        public static GameObject Panel(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        public static Text Label(string name, Transform parent, string value, int size, FontStyle style, TextAnchor anchor, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = Font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button Button(string name, Transform parent, string label, Color color, int fontSize = 28)
        {
            GameObject go = Panel(name, parent, color);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
            button.colors = colors;
            Text text = Label("Label", go.transform, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            Stretch(text.rectTransform);
            text.raycastTarget = false;
            return button;
        }

        public static InputField Input(string name, Transform parent, string placeholder)
        {
            GameObject go = Panel(name, parent, new Color(0.08f, 0.1f, 0.14f, 1f));
            Image img = go.GetComponent<Image>();
            InputField field = go.AddComponent<InputField>();

            Text text = Label("Text", go.transform, string.Empty, 26, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            Stretch(text.rectTransform, 18f, 8f, 18f, 8f);
            text.raycastTarget = false;

            Text ph = Label("Placeholder", go.transform, placeholder, 24, FontStyle.Italic, TextAnchor.MiddleLeft, TextMuted);
            Stretch(ph.rectTransform, 18f, 8f, 18f, 8f);
            ph.raycastTarget = false;

            field.textComponent = text;
            field.placeholder = ph;
            field.targetGraphic = img;
            field.caretColor = Golden;
            return field;
        }

        public static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void SetAnchors(RectTransform rt, float xmin, float ymin, float xmax, float ymax)
        {
            rt.anchorMin = new Vector2(xmin, ymin);
            rt.anchorMax = new Vector2(xmax, ymax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static Canvas CreateOverlayCanvas(string name, int sortOrder)
        {
            GameObject canvasObj = new GameObject(name);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            return canvas;
        }
    }
}
