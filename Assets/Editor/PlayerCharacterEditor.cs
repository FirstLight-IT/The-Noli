using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerCharacter))]
public class PlayerCharacterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Sprite sprite = GetPreviewSprite();
        if (sprite == null || sprite.texture == null)
            return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Editor Preview", EditorStyles.boldLabel);
        Rect previewArea = GUILayoutUtility.GetRect(10f, 180f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(previewArea, new Color(0.12f, 0.12f, 0.12f, 1f));
        DrawSprite(previewArea, sprite);
    }

    public override bool HasPreviewGUI()
    {
        return GetPreviewSprite() != null;
    }

    public override void OnPreviewGUI(Rect previewArea, GUIStyle background)
    {
        Sprite sprite = GetPreviewSprite();
        if (sprite == null || sprite.texture == null)
            return;

        DrawSprite(previewArea, sprite);
    }

    private static void DrawSprite(Rect previewArea, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        Rect textureRect = sprite.textureRect;
        Rect uv = new Rect(
            textureRect.x / sprite.texture.width,
            textureRect.y / sprite.texture.height,
            textureRect.width / sprite.texture.width,
            textureRect.height / sprite.texture.height);

        float spriteAspect = textureRect.width / textureRect.height;
        Rect drawArea = FitAspect(previewArea, spriteAspect);

        GUI.DrawTextureWithTexCoords(drawArea, sprite.texture, uv, true);
    }

    private Sprite GetPreviewSprite()
    {
        PlayerCharacter player = (PlayerCharacter)target;
        SpriteRenderer renderer = player.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer != null && renderer.sprite != null)
            return renderer.sprite;

        NPCInfoSO character = serializedObject.FindProperty("currentCharacter")?.objectReferenceValue as NPCInfoSO;
        RuntimeAnimatorController controller = character != null ? character.PlayerAnimatorController : null;
        if (controller == null)
            return null;

        foreach (AnimationClip clip in controller.animationClips)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                foreach (ObjectReferenceKeyframe keyframe in keyframes)
                {
                    if (keyframe.value is Sprite sprite)
                        return sprite;
                }
            }
        }

        return null;
    }

    private static Rect FitAspect(Rect area, float aspect)
    {
        float width = area.width;
        float height = width / aspect;

        if (height > area.height)
        {
            height = area.height;
            width = height * aspect;
        }

        return new Rect(
            area.x + (area.width - width) * 0.5f,
            area.y + (area.height - height) * 0.5f,
            width,
            height);
    }
}
