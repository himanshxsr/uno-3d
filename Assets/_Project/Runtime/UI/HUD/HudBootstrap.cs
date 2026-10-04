#nullable enable

using UnityEngine;
using UnityEngine.UI;
using Uno.Core.Enums;

namespace Uno.UI.HUD
{
    /// <summary>
    /// Builds a complete portrait UNO HUD from scratch using Unity UI.Text (always visible).
    /// Destroys any previous incomplete canvases before rebuilding.
    /// </summary>
    public static class HudBootstrap
    {
        private static Font? _font;

        public static UnoHudView EnsureHud()
        {
            EnsureEventSystem();
            DestroyOldHud();

            GameObject canvasObj = new GameObject("UNO 3D HUD Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            UnoHudView hud = canvasObj.AddComponent<UnoHudView>();
            Font font = ResolveFont();

            // ===== TOP BANNER =====
            GameObject banner = Panel("TopBanner", canvasObj.transform, new Color(0.08f, 0.1f, 0.16f, 0.95f));
            StretchTop(banner.GetComponent<RectTransform>(), 150f);

            Image badge = Image("ActiveColorBadge", banner.transform, new Color(0.906f, 0.114f, 0.212f, 1f));
            RectTransform badgeRt = badge.rectTransform;
            badgeRt.anchorMin = new Vector2(0f, 0.5f);
            badgeRt.anchorMax = new Vector2(0f, 0.5f);
            badgeRt.pivot = new Vector2(0f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(24f, 10f);
            badgeRt.sizeDelta = new Vector2(44f, 44f);

            Text turnText = Label("TurnText", banner.transform, "YOUR TURN", 34, FontStyle.Bold, TextAnchor.MiddleCenter, font);
            SetAnchors(turnText.rectTransform, 0.08f, 0.4f, 0.92f, 0.95f);
            turnText.color = new Color(1f, 0.85f, 0.15f, 1f);

            Text directionText = Label("DirectionText", banner.transform, "DIR: CLOCKWISE", 20, FontStyle.Bold, TextAnchor.MiddleLeft, font);
            SetAnchors(directionText.rectTransform, 0.08f, 0.08f, 0.55f, 0.4f);
            directionText.color = new Color(0.8f, 0.88f, 1f, 1f);

            Text timerText = Label("TimerText", banner.transform, "15s", 28, FontStyle.Bold, TextAnchor.MiddleRight, font);
            SetAnchors(timerText.rectTransform, 0.7f, 0.08f, 0.96f, 0.4f);

            // ===== OPPONENT PILLS =====
            Text leftBot = Pill("LeftBotPill", canvasObj.transform, "SARAH\n7 cards", font, new Vector2(0.02f, 0.55f), new Vector2(0.28f, 0.68f));
            Text topBot = Pill("TopBotPill", canvasObj.transform, "ALEX\n7 cards", font, new Vector2(0.35f, 0.72f), new Vector2(0.65f, 0.84f));
            Text rightBot = Pill("RightBotPill", canvasObj.transform, "DAVID\n7 cards", font, new Vector2(0.72f, 0.55f), new Vector2(0.98f, 0.68f));

            // ===== DISCARD PREVIEW (readable dump-deck card) =====
            GameObject discardPanel = Panel("DiscardPreview", canvasObj.transform, new Color(0.05f, 0.06f, 0.1f, 0.82f));
            SetAnchors(discardPanel.GetComponent<RectTransform>(), 0.58f, 0.38f, 0.96f, 0.58f);

            Text discardTitle = Label("DiscardTitle", discardPanel.transform, "DUMP DECK", 18, FontStyle.Bold, TextAnchor.UpperCenter, font);
            SetAnchors(discardTitle.rectTransform, 0.05f, 0.72f, 0.95f, 0.96f);
            discardTitle.color = new Color(1f, 0.9f, 0.35f, 1f);

            Image discardCardImage = Image("DiscardCardImage", discardPanel.transform, new Color(0.906f, 0.114f, 0.212f, 1f));
            SetAnchors(discardCardImage.rectTransform, 0.08f, 0.08f, 0.42f, 0.7f);

            Text discardCardLabel = Label("DiscardCardLabel", discardPanel.transform, "RED 7", 28, FontStyle.Bold, TextAnchor.MiddleLeft, font);
            SetAnchors(discardCardLabel.rectTransform, 0.46f, 0.2f, 0.96f, 0.7f);
            discardCardLabel.color = Color.white;

            Text pileCount = Label("PileCount", canvasObj.transform, "DRAW 80   DISCARD 1", 22, FontStyle.Bold, TextAnchor.MiddleCenter, font);
            SetAnchors(pileCount.rectTransform, 0.04f, 0.38f, 0.42f, 0.44f);
            pileCount.color = new Color(1f, 1f, 1f, 0.95f);

            Text logText = Label("LogText", canvasObj.transform, "Welcome to UNO 3D", 20, FontStyle.Normal, TextAnchor.MiddleCenter, font);
            SetAnchors(logText.rectTransform, 0.08f, 0.2f, 0.92f, 0.25f);
            logText.color = new Color(0.9f, 0.95f, 1f, 0.95f);

            // ===== BOTTOM ACTION BAR =====
            GameObject actionBar = Panel("ActionBar", canvasObj.transform, new Color(0.06f, 0.07f, 0.1f, 0.88f));
            RectTransform actionRt = actionBar.GetComponent<RectTransform>();
            actionRt.anchorMin = new Vector2(0f, 0f);
            actionRt.anchorMax = new Vector2(1f, 0f);
            actionRt.pivot = new Vector2(0.5f, 0f);
            actionRt.sizeDelta = new Vector2(0f, 170f);

            Button menuBtn = BigButton("MenuButton", actionBar.transform, "MENU", new Color(0.25f, 0.28f, 0.36f, 1f), font);
            SetAnchors(menuBtn.GetComponent<RectTransform>(), 0.04f, 0.18f, 0.22f, 0.82f);
            menuBtn.onClick.AddListener(() => hud.RequestMainMenu());

            Button unoBtn = BigButton("UnoButton", actionBar.transform, "UNO!", new Color(0.95f, 0.15f, 0.2f, 1f), font);
            SetAnchors(unoBtn.GetComponent<RectTransform>(), 0.24f, 0.18f, 0.48f, 0.82f);

            Button drawBtn = BigButton("DrawButton", actionBar.transform, "DRAW", new Color(0.12f, 0.5f, 1f, 1f), font);
            SetAnchors(drawBtn.GetComponent<RectTransform>(), 0.64f, 0.18f, 0.96f, 0.82f);
            Text drawLabel = drawBtn.GetComponentInChildren<Text>();

            // ===== ROUND END MODAL =====
            GameObject modal = Panel("RoundEndModal", canvasObj.transform, new Color(0f, 0f, 0f, 0.7f));
            StretchFull(modal.GetComponent<RectTransform>());

            GameObject panel = Panel("Panel", modal.transform, new Color(0.1f, 0.12f, 0.18f, 1f));
            RectTransform panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(480f, 360f);

            Text winner = Label("WinnerText", panel.transform, "YOU WIN!", 40, FontStyle.Bold, TextAnchor.MiddleCenter, font);
            winner.rectTransform.anchoredPosition = new Vector2(0f, 80f);
            winner.rectTransform.sizeDelta = new Vector2(420f, 60f);

            Text score = Label("ScoreText", panel.transform, "+0 PTS", 30, FontStyle.Bold, TextAnchor.MiddleCenter, font);
            score.rectTransform.anchoredPosition = new Vector2(0f, 20f);
            score.rectTransform.sizeDelta = new Vector2(420f, 50f);
            score.color = new Color(1f, 0.8f, 0.1f, 1f);

            Button next = BigButton("NextRoundButton", panel.transform, "NEXT ROUND", new Color(0f, 0.72f, 0.4f, 1f), font);
            RectTransform nextRt = next.GetComponent<RectTransform>();
            nextRt.anchorMin = new Vector2(0.5f, 0f);
            nextRt.anchorMax = new Vector2(0.5f, 0f);
            nextRt.pivot = new Vector2(0.5f, 0f);
            nextRt.anchoredPosition = new Vector2(-110f, 36f);
            nextRt.sizeDelta = new Vector2(200f, 64f);

            Button menuFromEnd = BigButton("MainMenuButton", panel.transform, "MAIN MENU", new Color(0.12f, 0.5f, 1f, 1f), font);
            RectTransform menuEndRt = menuFromEnd.GetComponent<RectTransform>();
            menuEndRt.anchorMin = new Vector2(0.5f, 0f);
            menuEndRt.anchorMax = new Vector2(0.5f, 0f);
            menuEndRt.pivot = new Vector2(0.5f, 0f);
            menuEndRt.anchoredPosition = new Vector2(110f, 36f);
            menuEndRt.sizeDelta = new Vector2(200f, 64f);
            menuFromEnd.onClick.AddListener(() => hud.RequestMainMenu());
            modal.SetActive(false);

            // ===== COLOR PICKER =====
            GameObject picker = Panel("ColorPicker", canvasObj.transform, new Color(0f, 0f, 0f, 0.65f));
            StretchFull(picker.GetComponent<RectTransform>());

            Text pickerTitle = Label("PickerTitle", picker.transform, "CHOOSE A COLOR", 32, FontStyle.Bold, TextAnchor.MiddleCenter, font);
            pickerTitle.rectTransform.anchoredPosition = new Vector2(0f, 220f);
            pickerTitle.rectTransform.sizeDelta = new Vector2(500f, 50f);

            Button red = ColorButton(picker.transform, "Red", CardColor.Red, new Vector2(-90f, 70f), font);
            Button blue = ColorButton(picker.transform, "Blue", CardColor.Blue, new Vector2(90f, 70f), font);
            Button yellow = ColorButton(picker.transform, "Yellow", CardColor.Yellow, new Vector2(90f, -70f), font);
            Button green = ColorButton(picker.transform, "Green", CardColor.Green, new Vector2(-90f, -70f), font);
            picker.SetActive(false);

            hud.BindRefs(
                turnText, directionText, timerText, logText, badge,
                leftBot, topBot, rightBot, pileCount,
                unoBtn, drawBtn, drawLabel,
                modal, winner, score, next,
                picker, red, blue, green, yellow,
                discardCardImage, discardCardLabel);

            Debug.Log("[UNO 3D] Full portrait HUD rebuilt with discard preview.");
            return hud;
        }

        private static void DestroyOldHud()
        {
            UnoHudView[] existing = Object.FindObjectsByType<UnoHudView>(FindObjectsInactive.Include);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] == null)
                {
                    continue;
                }

                existing[i].gameObject.SetActive(false);
                if (UnityEngine.Application.isPlaying)
                {
                    Object.Destroy(existing[i].gameObject);
                }
                else
                {
                    Object.DestroyImmediate(existing[i].gameObject);
                }
            }

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] == null || !canvases[i].name.Contains("UNO 3D HUD"))
                {
                    continue;
                }

                canvases[i].gameObject.SetActive(false);
                if (UnityEngine.Application.isPlaying)
                {
                    Object.Destroy(canvases[i].gameObject);
                }
                else
                {
                    Object.DestroyImmediate(canvases[i].gameObject);
                }
            }
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            {
                return;
            }

            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private static Font ResolveFont()
        {
            if (_font != null)
            {
                return _font;
            }

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            if (_font == null)
            {
                _font = Font.CreateDynamicFontFromOSFont("Arial", 28);
            }

            return _font;
        }

        private static GameObject Panel(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Image Image(string name, Transform parent, Color color)
        {
            GameObject go = Panel(name, parent, color);
            return go.GetComponent<Image>();
        }

        private static Text Label(string name, Transform parent, string value, int size, FontStyle style, TextAnchor anchor, Font font)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Text Pill(string name, Transform parent, string value, Font font, Vector2 min, Vector2 max)
        {
            GameObject go = Panel(name, parent, new Color(0.1f, 0.12f, 0.18f, 0.92f));
            SetAnchors(go.GetComponent<RectTransform>(), min.x, min.y, max.x, max.y);
            Text text = Label("Label", go.transform, value, 22, FontStyle.Bold, TextAnchor.MiddleCenter, font);
            StretchFull(text.rectTransform);
            return text;
        }

        private static Button BigButton(string name, Transform parent, string label, Color color, Font font)
        {
            GameObject go = Panel(name, parent, color);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            Text text = Label("Label", go.transform, label, 30, FontStyle.Bold, TextAnchor.MiddleCenter, font);
            StretchFull(text.rectTransform);
            text.raycastTarget = false;
            return button;
        }

        private static Button ColorButton(Transform parent, string name, CardColor color, Vector2 pos, Font font)
        {
            Color fill = color switch
            {
                CardColor.Red => new Color(0.906f, 0.114f, 0.212f, 1f),
                CardColor.Blue => new Color(0f, 0.4f, 1f, 1f),
                CardColor.Green => new Color(0f, 0.722f, 0.396f, 1f),
                CardColor.Yellow => new Color(1f, 0.784f, 0f, 1f),
                _ => Color.gray
            };

            Button button = BigButton(name, parent, name.ToUpperInvariant(), fill, font);
            RectTransform rt = button.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(150f, 150f);
            return button;
        }

        private static void StretchTop(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetAnchors(RectTransform rt, float xmin, float ymin, float xmax, float ymax)
        {
            rt.anchorMin = new Vector2(xmin, ymin);
            rt.anchorMax = new Vector2(xmax, ymax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
