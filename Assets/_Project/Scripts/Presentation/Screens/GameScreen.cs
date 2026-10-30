using UnityEngine;
using UnityEngine.UI;

namespace LagCompensation.Presentation.Screens
{
    /// <summary>
    /// One of the split-screen views: a camera in its own part of the screen that shows only one
    /// version of the match (Peely's, Raven's or the server's), with its name on top.
    ///
    /// Each version of the characters lives in its own layer; this camera draws the shared world,
    /// its own version and, if ghosts are on, the ghosts of the other versions to compare.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(Camera))]   // in the editor too, so the Game view shows the same
    public sealed class GameScreen : MonoBehaviour
    {
        [Header("What it shows")]
        [Tooltip("Layers everybody sees: the arena, the crowd...")]
        [SerializeField] LayerMask world = ~0;
        [Tooltip("The layer with this screen's version of the characters.")]
        [SerializeField] LayerMask ownCharacters;
        [Tooltip("The ghosts of the other versions, shown while ghosts are on.")]
        [SerializeField] LayerMask otherGhosts;
        [SerializeField] bool showGhosts;

        [Header("Label")]
        [SerializeField] string title = "PANTALLA";
        [SerializeField] Color titleColor = Color.white;
        [SerializeField, Range(16, 96)] int fontSize = 40;

        Camera view;
        int labelLayer;   // the label's layer, once it exists
        Text flash;
        float flashLeft;   // seconds the message stays on screen

        public bool ShowGhosts
        {
            get => showGhosts;
            set { showGhosts = value; ApplyMask(); }
        }

        void OnEnable()
        {
            view = GetComponent<Camera>();
            ApplyMask();
        }

        void Start()
        {
            if (UnityEngine.Application.isPlaying) BuildLabel();
        }

        void OnValidate()
        {
            if (view != null) ApplyMask();
        }

        /// <summary>A big message in the middle of this screen for a moment (e.g. "¡IMPACTO!").</summary>
        public void Flash(string text, Color color, float seconds = 1.2f)
        {
            if (flash == null) return;
            flash.text = text;
            flash.color = color;
            flash.enabled = true;
            flashLeft = seconds;
        }

        /// <summary>Keeps the current message on screen a bit longer (e.g. while the simulation is paused).</summary>
        public void ExtendFlash(float seconds)
        {
            if (flash != null && flash.enabled) flashLeft += seconds;
        }

        void Update()
        {
            if (flash != null && flash.enabled && (flashLeft -= Time.unscaledDeltaTime) <= 0f) flash.enabled = false;
        }

        void ApplyMask() => view.cullingMask = world | ownCharacters | (showGhosts ? (int)otherGhosts : 0) | labelLayer;

        /// <summary>The name of the screen, top left. Screen Space - Camera, so the recording kit captures it.</summary>
        void BuildLabel()
        {
            var canvasObject = new GameObject($"{name} Label", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = view;
            canvas.planeDistance = view.nearClipPlane + 0.05f;
            canvasObject.layer = LayerMask.NameToLayer("UI");
            labelLayer = 1 << canvasObject.layer;
            ApplyMask();

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var label = NewText("Title", canvasObject.transform, fontSize, TextAnchor.UpperLeft, new Vector2(0f, 1f));
            label.color = titleColor;
            label.text = title;
            label.rectTransform.anchoredPosition = new Vector2(30f, -24f);

            flash = NewText("Flash", canvasObject.transform, fontSize * 2, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            flash.rectTransform.anchoredPosition = new Vector2(0f, -120f);
            flash.enabled = false;
        }

        static Text NewText(string name, Transform parent, int size, TextAnchor alignment, Vector2 anchor)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.gameObject.layer = parent.gameObject.layer;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = text.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(3f, -3f);

            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = new Vector2(1600f, size * 1.5f);
            return text;
        }
    }
}
