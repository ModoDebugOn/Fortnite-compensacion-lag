using System.Collections.Generic;
using System.Globalization;
using LagCompensation.Application;
using UnityEngine;
using UnityEngine.UI;

namespace LagCompensation.Presentation.Hud
{
    /// <summary>
    /// The demo's settings on screen, so they show up in the video: ping, snapshots per second,
    /// interpolation, rewinding, rewind limit and speed. Each one with its bar, and lit up for a
    /// moment when it changes, so the viewer sees what has just been touched.
    ///
    /// It only reads the settings; the keys and sliders that change them are elsewhere. It is drawn
    /// on one screen's camera (Screen Space - Camera, so the recording kit captures it).
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [Tooltip("The camera it is drawn on: one of the split screens.")]
        [SerializeField] Camera screen;
        [Tooltip("Corner of that screen it sits in, as an anchor: (1, 1) = top right.")]
        [SerializeField] Vector2 corner = new Vector2(1f, 1f);
        [Tooltip("Size, compared with a 1080p video.")]
        [SerializeField, Range(0.5f, 2f)] float size = 1f;
        [Tooltip("Shown at the start (key H shows and hides it).")]
        [SerializeField] bool visible = true;

        [Header("Rows (decided when Play starts)")]
        [SerializeField] bool showPing = true;
        [SerializeField] bool showTickRate = true;
        [SerializeField] bool showInterpolation = true;
        [SerializeField] bool showRewinding = true;
        [SerializeField] bool showRewindLimit = true;
        [SerializeField] bool showSpeed = true;

        [Header("Colours")]
        [SerializeField] Color onColor = new Color(0.2f, 1f, 0.35f);
        [SerializeField] Color offColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField] Color barColor = new Color(0.3f, 0.8f, 1f);
        [Tooltip("Flash of a row when its value changes.")]
        [SerializeField] Color changedColor = new Color(1f, 0.88f, 0.15f);
        [Tooltip("Seconds (real time) a changed row stays lit.")]
        [SerializeField, Range(0.2f, 3f)] float litFor = 1.2f;

        // The bars go from 0 to these (the demo's slider ranges).
        const float MaxPingMs = 400f, MaxTickRate = 60f, MaxInterpolationMs = 400f, MaxRewindMs = 1000f;
        const float Width = 470f, RowHeight = 62f, Margin = 24f, Padding = 12f;

        static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
        static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.1f, 0.82f);

        /// <summary>What a row shows: its text, how full its bar is (negative = no bar) and its colour.</summary>
        readonly struct Reading
        {
            public readonly string Text;
            public readonly float Bar;
            public readonly Color Color;

            public Reading(string text, float bar, Color color)
            {
                Text = text;
                Bar = bar;
                Color = color;
            }
        }

        sealed class Row
        {
            public System.Func<Reading> Read;
            public Image Background, BarFill;
            public RectTransform BarBack;
            public Text Value;
            public string Shown;
            public float LitAt = float.NegativeInfinity;
        }

        readonly List<Row> rows = new List<Row>();
        ServerWorld world;
        GameClient viewer;
        Canvas canvas;
        CanvasScaler scaler;
        RectTransform panel;

        public bool Visible
        {
            get => visible;
            set
            {
                visible = value;
                if (canvas != null) canvas.enabled = value;
            }
        }

        /// <summary>Where the settings are read from: the server and the client whose ping is shown.</summary>
        public void Bind(ServerWorld server, GameClient client)
        {
            world = server;
            viewer = client;
        }

        void Start()
        {
            if (screen == null || world == null || viewer == null) return;
            Build();
            Visible = visible;
        }

        void LateUpdate()
        {
            if (canvas == null) return;
            scaler.scaleFactor = size * Mathf.Min(OutputWidth(), OutputHeight()) / 1080f;
            PlaceIn(corner);
            foreach (var row in rows) Refresh(row);
        }

        void PlaceIn(Vector2 anchor)
        {
            panel.anchorMin = panel.anchorMax = panel.pivot = anchor;
            panel.anchoredPosition = new Vector2(anchor.x > 0.5f ? -Margin : Margin, anchor.y > 0.5f ? -Margin : Margin);
        }

        // What the camera draws into: the game view, or the recording kit's texture while it records.
        int OutputWidth() => screen.targetTexture != null ? screen.targetTexture.width : Screen.width;
        int OutputHeight() => screen.targetTexture != null ? screen.targetTexture.height : Screen.height;

        void Refresh(Row row)
        {
            var reading = row.Read();
            if (reading.Text != row.Shown)
            {
                if (row.Shown != null) row.LitAt = Time.unscaledTime;   // not on the first frame
                row.Shown = reading.Text;
                row.Value.text = reading.Text;
            }
            row.Value.color = reading.Color;

            row.BarBack.gameObject.SetActive(reading.Bar >= 0f);
            if (reading.Bar >= 0f) row.BarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(reading.Bar), 1f);

            // Lit up when it changes, fading back to the panel's colour; the value grows a little.
            float lit = 1f - Mathf.Clamp01((Time.unscaledTime - row.LitAt) / litFor);
            row.Background.color = Color.Lerp(Color.clear, new Color(changedColor.r, changedColor.g, changedColor.b, 0.45f), lit);
            row.Value.rectTransform.localScale = Vector3.one * (1f + 0.25f * lit);
        }

        // --- The rows ---------------------------------------------------------------------------

        Reading Ping() => Milliseconds(viewer.Ping, MaxPingMs);

        Reading TickRate()
        {
            float rate = (float)(1.0 / world.Tick.Interval);
            return new Reading(rate.ToString("0", Spanish), rate / MaxTickRate, Color.white);
        }

        Reading Interpolation() => viewer.Interpolate
            ? Milliseconds(viewer.InterpolationDelay, MaxInterpolationMs)
            : new Reading("NO", 0f, offColor);

        Reading Rewinding() => world.LagCompensation
            ? new Reading("SÍ", -1f, onColor)
            : new Reading("NO", -1f, offColor);

        Reading RewindLimit() => Milliseconds(world.MaxRewind, MaxRewindMs);

        Reading Speed()
        {
            if (world.Clock.Paused) return new Reading("PAUSA", -1f, changedColor);
            double scale = world.Clock.TimeScale;
            return new Reading("×" + scale.ToString("0.##", Spanish), -1f, scale < 1.0 ? changedColor : Color.white);
        }

        static Reading Milliseconds(double seconds, float max)
        {
            float ms = (float)(seconds * 1000.0);
            return new Reading(ms.ToString("0", Spanish) + " ms", ms / max, Color.white);
        }

        // --- Building it ------------------------------------------------------------------------

        void Build()
        {
            var canvasObject = new GameObject("Settings Panel Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = screen;
            canvas.planeDistance = screen.nearClipPlane + 0.04f;   // in front of the screen's own label
            canvas.sortingOrder = 1;
            screen.cullingMask |= 1 << canvasObject.layer;
            scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var readings = new List<(string label, System.Func<Reading> read)>();
            if (showPing) readings.Add(("PING", Ping));
            if (showTickRate) readings.Add(("FOTOS POR SEGUNDO", TickRate));
            if (showInterpolation) readings.Add(("INTERPOLACIÓN", Interpolation));
            if (showRewinding) readings.Add(("REBOBINADO", Rewinding));
            if (showRewindLimit) readings.Add(("LÍMITE DE REBOBINADO", RewindLimit));
            if (showSpeed) readings.Add(("VELOCIDAD", Speed));

            panel = NewImage("Panel", canvasObject.transform, PanelColor);
            panel.sizeDelta = new Vector2(Width, readings.Count * RowHeight + 2f * Padding);
            PlaceIn(corner);

            for (int i = 0; i < readings.Count; i++)
                rows.Add(NewRow(panel, i, readings[i].label, readings[i].read));
        }

        Row NewRow(RectTransform panel, int index, string label, System.Func<Reading> read)
        {
            var area = NewImage(label, panel, Color.clear);
            area.anchorMin = new Vector2(0f, 1f);
            area.anchorMax = new Vector2(1f, 1f);
            area.pivot = new Vector2(0.5f, 1f);
            area.sizeDelta = new Vector2(0f, RowHeight);
            area.anchoredPosition = new Vector2(0f, -Padding - index * RowHeight);

            var name = NewText("Label", area, 24, TextAnchor.MiddleLeft, Color.white);
            name.text = label;
            name.rectTransform.offsetMin = new Vector2(Padding + 4f, 12f);
            name.rectTransform.offsetMax = new Vector2(-Padding, 0f);

            var value = NewText("Value", area, 32, TextAnchor.MiddleRight, Color.white);
            value.rectTransform.pivot = new Vector2(1f, 0.5f);   // grows from the right when it flashes
            value.rectTransform.offsetMin = new Vector2(Padding, 12f);
            value.rectTransform.offsetMax = new Vector2(-Padding - 8f, 0f);

            var barBack = NewImage("Bar", area, new Color(1f, 1f, 1f, 0.15f));
            barBack.anchorMin = new Vector2(0f, 0f);
            barBack.anchorMax = new Vector2(1f, 0f);
            barBack.pivot = new Vector2(0.5f, 0f);
            barBack.offsetMin = new Vector2(Padding + 4f, 6f);
            barBack.offsetMax = new Vector2(-Padding - 4f, 14f);

            var fill = NewImage("Fill", barBack, barColor);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;

            return new Row
            {
                Read = read,
                Background = area.GetComponent<Image>(),
                BarBack = barBack,
                BarFill = fill.GetComponent<Image>(),
                Value = value,
            };
        }

        static RectTransform NewImage(string name, Transform parent, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.gameObject.layer = parent.gameObject.layer;
            image.color = color;
            image.raycastTarget = false;
            return image.rectTransform;
        }

        static Text NewText(string name, Transform parent, int fontSize, TextAnchor alignment, Color color)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.gameObject.layer = parent.gameObject.layer;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = text.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            return text;
        }
    }
}
