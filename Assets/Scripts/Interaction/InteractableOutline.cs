using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class InteractableOutline : MonoBehaviour
{
    private const string OutlineShaderName = "The Noli/Interactable Sprite Outline";

    [Header("Outline Appearance")]
    [SerializeField] private Color outlineColor = new(1f, 0.82f, 0.2f, 1f);
    [SerializeField, Range(0.5f, 8f)] private float thickness = 3f;

    [Header("Optional Renderer Override")]
    [Tooltip("Leave empty to outline the enabled Sprite Renderers under this object automatically.")]
    [SerializeField] private SpriteRenderer[] targetRenderers;

    private readonly List<OutlineRendererPair> outlineRenderers = new();
    private Material outlineMaterial;
    private bool isHighlighted;

    private void Awake()
    {
        BuildOutlineRenderers();
    }

    private void LateUpdate()
    {
        if (!isHighlighted)
            return;

        foreach (OutlineRendererPair pair in outlineRenderers)
            pair.Sync();
    }

    private void OnDisable()
    {
        SetHighlighted(false);
    }

    private void OnDestroy()
    {
        if (outlineMaterial != null)
            Destroy(outlineMaterial);
    }

    public void SetHighlighted(bool highlighted)
    {
        isHighlighted = highlighted;

        foreach (OutlineRendererPair pair in outlineRenderers)
        {
            pair.Sync();
            pair.Outline.enabled = highlighted && pair.Source.enabled;
        }
    }

    private void BuildOutlineRenderers()
    {
        Shader outlineShader = Shader.Find(OutlineShaderName);
        if (outlineShader == null)
        {
            Debug.LogError($"Could not find shader '{OutlineShaderName}'.", this);
            enabled = false;
            return;
        }

        outlineMaterial = new Material(outlineShader)
        {
            name = $"{gameObject.name} Interaction Outline (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        outlineMaterial.SetColor("_OutlineColor", outlineColor);
        outlineMaterial.SetFloat("_OutlineThickness", thickness);

        SpriteRenderer[] sources = targetRenderers != null && targetRenderers.Length > 0
            ? targetRenderers
            : GetComponentsInChildren<SpriteRenderer>(false);

        foreach (SpriteRenderer source in sources)
        {
            if (source == null || source.gameObject.name.EndsWith(" (Interaction Outline)"))
                continue;

            GameObject outlineObject = new($"{source.gameObject.name} (Interaction Outline)");
            outlineObject.hideFlags = HideFlags.HideInHierarchy;
            outlineObject.transform.SetParent(source.transform, false);

            SpriteRenderer outline = outlineObject.AddComponent<SpriteRenderer>();
            outline.sharedMaterial = outlineMaterial;
            outline.enabled = false;

            OutlineRendererPair pair = new(source, outline);
            pair.Sync();
            outlineRenderers.Add(pair);
        }
    }

    private sealed class OutlineRendererPair
    {
        public SpriteRenderer Source { get; }
        public SpriteRenderer Outline { get; }

        public OutlineRendererPair(SpriteRenderer source, SpriteRenderer outline)
        {
            Source = source;
            Outline = outline;
        }

        public void Sync()
        {
            if (Source == null || Outline == null)
                return;

            Outline.sprite = Source.sprite;
            Outline.flipX = Source.flipX;
            Outline.flipY = Source.flipY;
            Outline.drawMode = Source.drawMode;
            Outline.size = Source.size;
            Outline.maskInteraction = Source.maskInteraction;
            Outline.sortingLayerID = Source.sortingLayerID;
            Outline.sortingOrder = Source.sortingOrder;
        }
    }
}
