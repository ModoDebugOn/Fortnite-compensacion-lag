using UnityEngine;

namespace LagCompensation.Presentation.Players
{
    /// <summary>
    /// Colours a ghost capsule (a past or remote version of a player). The colour says whose
    /// version it is: the colour of the player whose screen shows it, or white for the server.
    /// All ghosts share one material; each one only changes its tint.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(Renderer))]
    public sealed class GhostTint : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] Color color = new Color(1f, 0.9f, 0.2f, 0.45f);

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        void Apply()
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            GetComponent<Renderer>().SetPropertyBlock(block);
        }
    }
}
