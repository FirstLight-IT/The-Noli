using System.Text;
using TMPro;
using UnityEngine;

/// <summary>Shows the active mission separately from the journal's completed history.</summary>
public class MissionJournalController : MonoBehaviour
{
    [Header("Mission Text Areas")]
    [SerializeField] private TMP_Text currentMissionText;
    [SerializeField] private TMP_Text completedMissionsText;

    private void Awake()
    {
        CreateMissingTextAreas();
    }

    private void OnEnable()
    {
        MissionController.OnMissionStatesChanged += Refresh;
        Refresh();
    }

    private void Start()
    {
        // The mission controller and journal can initialize in either order.
        Refresh();
    }

    private void OnDisable()
    {
        MissionController.OnMissionStatesChanged -= Refresh;
    }

    private void CreateMissingTextAreas()
    {
        TMP_Text template = GetComponentInChildren<TMP_Text>(true);
        if (template == null)
        {
            Debug.LogWarning("The Missions Page needs a TextMesh Pro heading to style its mission text.", this);
            return;
        }

        if (currentMissionText == null)
        {
            currentMissionText = CreateTextArea(template, "Current Mission Text", 52f);
            SetArea(currentMissionText.rectTransform, 0.12f, 0.52f, 0.88f, 0.78f);
        }

        if (completedMissionsText == null)
        {
            completedMissionsText = CreateTextArea(template, "Completed Missions Text", 38f);
            SetArea(completedMissionsText.rectTransform, 0.12f, 0.10f, 0.88f, 0.48f);
        }
    }

    private TMP_Text CreateTextArea(TMP_Text template, string objectName, float fontSize)
    {
        TMP_Text textArea = Instantiate(template, transform);
        textArea.name = objectName;
        textArea.text = string.Empty;
        textArea.fontSize = fontSize;
        textArea.fontStyle = FontStyles.Normal;
        textArea.alignment = TextAlignmentOptions.TopLeft;
        textArea.textWrappingMode = TextWrappingModes.Normal;
        textArea.raycastTarget = false;
        return textArea;
    }

    private static void SetArea(RectTransform rect, float minX, float minY, float maxX, float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void Refresh()
    {
        if (currentMissionText == null || completedMissionsText == null || MissionController.Instance == null)
            return;

        RenderCurrentMission(MissionController.Instance);
        RenderCompletedMissions(MissionController.Instance);
    }

    private void RenderCurrentMission(MissionController controller)
    {
        MissionInfoSO mission = controller.ActiveMissionInfo;
        if (mission == null)
        {
            currentMissionText.SetText(string.Empty);
            return;
        }

        StringBuilder text = new();
        text.Append("<size=115%><b>")
            .Append(EscapeRichText(mission.DisplayName))
            .Append("</b></size>");

        MissionStep[] steps = mission.MissionStepPrefabs;
        for (int i = 0; i < steps.Length; i++)
        {
            if (steps[i] != null && !steps[i].ShowAsPlayerObjective)
                continue;

            string objective = i == controller.ActiveMissionStepIndex && !string.IsNullOrWhiteSpace(controller.CurrentObjective)
                ? controller.CurrentObjective
                : steps[i] != null ? steps[i].JournalDescription : "Missing mission step";

            string line = $"- {EscapeRichText(objective)}";
            text.Append('\n').Append(i < controller.ActiveMissionStepIndex ? $"<s>{line}</s>" : line);
        }

        currentMissionText.SetText(text.ToString());
    }

    private void RenderCompletedMissions(MissionController controller)
    {
        StringBuilder text = new();

        foreach (MissionInfoSO mission in controller.MissionInfos)
        {
            if (mission == null || controller.GetMissionState(mission.MissionId) != MissionState.Finished)
                continue;

            if (text.Length > 0)
                text.Append("\n\n");

            text.Append("<s><b>").Append(EscapeRichText(mission.DisplayName)).Append("</b>");

            foreach (MissionStep step in mission.MissionStepPrefabs)
            {
                if (step != null && !step.ShowAsPlayerObjective)
                    continue;

                string objective = step != null ? step.JournalDescription : "Missing mission step";
                text.Append("\n- ").Append(EscapeRichText(objective));
            }

            text.Append("</s>");
        }

        completedMissionsText.SetText(text.ToString());
    }

    private static string EscapeRichText(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
