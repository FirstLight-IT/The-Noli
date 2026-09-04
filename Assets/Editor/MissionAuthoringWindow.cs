using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class MissionAuthoringWindow : EditorWindow
{
    private const string MenuPath = "Tools/The Noli/Mission Group Builder";
    private const string LegacyMenuPath = "Tools/The Noli/Mission Builder";
    private const string Chapter1MissionsJsonPath = "Assets/JSON Files/Missions/Missions.json";
    private const string Chapter1ObjectivesJsonPath = "Assets/JSON Files/Missions/MissionObjectives.json";
    private const string ChapterMissionJsonRoot = "Assets/JSON Files/Missions";
    private const string MissionAssetsRoot = "Assets/ScriptableObjects/Missions";
    private const string MissionStepsRoot = "Assets/Prefabs/Mission Steps";
    private const string Chapter2Path = "Assets/ScriptableObjects/Chapters/Chapter 2.asset";
    private const string DraftRelativePath = "UserSettings/TheNoli/MissionGroupBuilderDraft.json";
    private const double DraftSaveDelaySeconds = 0.5d;

    private enum StepKind
    {
        Artifact,
        Conversation,
        InspectArtifacts,
        MeetCharacters,
        NPCMovement,
        EnterRoom,
        SpeakToAmbientNPCs
    }

    [Serializable]
    private sealed class CharacterTargetDraft
    {
        public string npcId = string.Empty;
        public string displayName = string.Empty;
    }

    [Serializable]
    private sealed class MovementTargetDraft
    {
        public string npcId = string.Empty;
        public string routeId = string.Empty;
    }

    [Serializable]
    private sealed class StepDraft
    {
        public bool expanded = true;
        public StepKind kind = StepKind.EnterRoom;
        public bool showAsPlayerObjective = true;
        public string objectiveEnglish = string.Empty;
        public string objectiveFilipino = string.Empty;
        public ArtifactInfoSO targetArtifact;
        public string targetNpcId = string.Empty;
        public TextAsset conversationJson;
        public string roomId = string.Empty;
        public int requiredCount = 1;
        public List<CharacterTargetDraft> characters = new();
        public List<MovementTargetDraft> movementTargets = new();
        public bool disableInteractionWhileMoving = true;
        public bool waitForAllRoutesToFinish = true;
        public AmbientNPCTag requiredTag = AmbientNPCTag.Girl;
    }

    [Serializable]
    private sealed class MissionGroupDraftFile
    {
        public int version = 2;
        public string chapterGuid = string.Empty;
        public string missionNameEnglish = string.Empty;
        public string missionNameFilipino = string.Empty;
        public string missionId = string.Empty;
        public string lastGeneratedMissionId = string.Empty;
        public string prerequisiteGuid = string.Empty;
        public bool autoStartWhenAvailable = true;
        public bool makeChapterStartingMission;
        public List<StepDraftFile> steps = new();
    }

    [Serializable]
    private sealed class StepDraftFile
    {
        public bool expanded = true;
        public StepKind kind = StepKind.EnterRoom;
        public bool showAsPlayerObjective = true;
        public string objectiveEnglish = string.Empty;
        public string objectiveFilipino = string.Empty;
        public string targetArtifactGuid = string.Empty;
        public string targetNpcId = string.Empty;
        public string conversationJsonGuid = string.Empty;
        public string roomId = string.Empty;
        public int requiredCount = 1;
        public List<CharacterTargetDraft> characters = new();
        public List<MovementTargetDraft> movementTargets = new();
        public bool disableInteractionWhileMoving = true;
        public bool waitForAllRoutesToFinish = true;
        public AmbientNPCTag requiredTag = AmbientNPCTag.Girl;
    }

    [Serializable]
    private sealed class MissionFile
    {
        public int schemaVersion = 1;
        public string defaultLanguageCode = "en";
        public List<MissionJsonEntry> missions = new();
    }

    [Serializable]
    private sealed class ObjectiveFile
    {
        public int schemaVersion = 1;
        public string defaultLanguageCode = "en";
        public List<MissionObjectiveJsonEntry> objectives = new();
    }

    [SerializeField] private ChapterDataSO chapter;
    [SerializeField] private string missionNameEnglish = string.Empty;
    [SerializeField] private string missionNameFilipino = string.Empty;
    [SerializeField] private string missionId = string.Empty;
    [SerializeField] private string lastGeneratedMissionId = string.Empty;
    [SerializeField] private MissionInfoSO prerequisite;
    [SerializeField] private bool autoStartWhenAvailable = true;
    [SerializeField] private bool makeChapterStartingMission;
    [SerializeField] private List<StepDraft> steps = new();
    [SerializeField] private Vector2 scrollPosition;

    [NonSerialized] private bool draftDirty;
    [NonSerialized] private double draftSaveAt;
    [NonSerialized] private string draftStatus = string.Empty;

    [MenuItem(MenuPath)]
    public static void Open()
    {
        MissionAuthoringWindow window = GetWindow<MissionAuthoringWindow>();
        window.titleContent = new GUIContent("Mission Groups");
        window.minSize = new Vector2(500f, 650f);
        window.Show();
    }

    [MenuItem(LegacyMenuPath, false, 1001)]
    private static void OpenLegacyPath()
    {
        Open();
    }

    private void OnEnable()
    {
        bool restoredDraft = TryLoadDraft(out string loadError);

        if (!restoredDraft && chapter == null)
        {
            chapter = AssetDatabase.LoadAssetAtPath<ChapterDataSO>(Chapter2Path);
            makeChapterStartingMission = chapter != null && chapter.StartingMission == null;
        }

        if (steps == null || steps.Count == 0)
            steps = new List<StepDraft> { CreateStepDraft(StepKind.EnterRoom) };

        if (!string.IsNullOrWhiteSpace(loadError))
            draftStatus = loadError;
    }

    private void OnDisable()
    {
        if (draftDirty)
            SaveDraft();
    }

    private void Update()
    {
        if (!draftDirty || EditorApplication.timeSinceStartup < draftSaveAt)
            return;

        SaveDraft();
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("Mission Group Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "A mission group contains one or more ordered step prefabs. Steps advance in order. " +
            "The Mission Complete graphic appears only after the final step in this group finishes.",
            MessageType.Info);

        DrawDraftControls();

        DrawChapterField();
        DrawMissionGroupFields();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"Ordered Mission Steps ({steps.Count})", EditorStyles.boldLabel);
        for (int index = 0; index < steps.Count; index++)
        {
            if (DrawStep(index))
                break;
        }

        if (GUILayout.Button("+ Add Mission Step", GUILayout.Height(28f)))
            ShowAddStepMenu();

        EditorGUILayout.Space(10f);
        using (new EditorGUI.DisabledScope(!CanCreate()))
        {
            if (GUILayout.Button("Create Mission Group and Add to Chapter", GUILayout.Height(38f)))
                CreateMissionGroup();
        }

        if (!CanCreate())
        {
            EditorGUILayout.HelpBox(
                "Choose a chapter and provide a group name, mission ID, at least one step, and an English objective for every step.",
                MessageType.Warning);
        }

        EditorGUILayout.EndScrollView();

        if (EditorGUI.EndChangeCheck())
            MarkDraftDirty();
    }

    private void DrawDraftControls()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "This unfinished mission group is saved automatically. You can close this window or Unity " +
            "and continue later without creating the mission.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Draft Now"))
            SaveDraft(showConfirmation: true);

        if (GUILayout.Button("Reload Saved Draft"))
        {
            if (!TryLoadDraft(out string error))
                draftStatus = string.IsNullOrWhiteSpace(error) ? "No saved mission draft was found." : error;
            else
                draftStatus = "Saved mission draft reloaded.";

            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrWhiteSpace(draftStatus))
            EditorGUILayout.LabelField(draftStatus, EditorStyles.miniLabel);
    }

    private void DrawChapterField()
    {
        ChapterDataSO selectedChapter = (ChapterDataSO)EditorGUILayout.ObjectField(
            "Chapter",
            chapter,
            typeof(ChapterDataSO),
            false);
        if (selectedChapter == chapter)
            return;

        chapter = selectedChapter;
        prerequisite = null;
        makeChapterStartingMission = chapter != null && chapter.StartingMission == null;
    }

    private void DrawMissionGroupFields()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Mission Group", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        string updatedName = EditorGUILayout.TextField("Name (English)", missionNameEnglish);
        if (EditorGUI.EndChangeCheck())
        {
            missionNameEnglish = updatedName;
            string generated = Slugify(missionNameEnglish);
            if (string.IsNullOrWhiteSpace(missionId) || missionId == lastGeneratedMissionId)
                missionId = generated;
            lastGeneratedMissionId = generated;
        }

        missionNameFilipino = EditorGUILayout.TextField(
            "Name (Filipino, optional)",
            missionNameFilipino);
        missionId = EditorGUILayout.TextField("Mission ID", missionId).Trim();
        prerequisite = (MissionInfoSO)EditorGUILayout.ObjectField(
            "Prerequisite Group",
            prerequisite,
            typeof(MissionInfoSO),
            false);
        autoStartWhenAvailable = EditorGUILayout.Toggle(
            "Auto Start When Available",
            autoStartWhenAvailable);
        makeChapterStartingMission = EditorGUILayout.Toggle(
            "Chapter Starting Group",
            makeChapterStartingMission);
    }

    private bool DrawStep(int index)
    {
        StepDraft step = steps[index];
        bool listChanged = false;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        string stepTitle = $"Step {index + 1}: {ObjectNames.NicifyVariableName(step.kind.ToString())}";
        step.expanded = EditorGUILayout.Foldout(step.expanded, stepTitle, true);

        using (new EditorGUI.DisabledScope(index == 0))
        {
            if (GUILayout.Button("Up", GUILayout.Width(36f)))
            {
                (steps[index - 1], steps[index]) = (steps[index], steps[index - 1]);
                listChanged = true;
            }
        }

        using (new EditorGUI.DisabledScope(index >= steps.Count - 1))
        {
            if (GUILayout.Button("Down", GUILayout.Width(44f)))
            {
                (steps[index + 1], steps[index]) = (steps[index], steps[index + 1]);
                listChanged = true;
            }
        }

        using (new EditorGUI.DisabledScope(steps.Count <= 1))
        {
            if (GUILayout.Button("Remove", GUILayout.Width(62f)))
            {
                steps.RemoveAt(index);
                listChanged = true;
            }
        }
        EditorGUILayout.EndHorizontal();

        if (step.expanded && !listChanged)
        {
            StepKind selectedKind = (StepKind)EditorGUILayout.EnumPopup("Step Type", step.kind);
            if (selectedKind != step.kind)
                steps[index] = step = CreateStepDraft(selectedKind);

            step.showAsPlayerObjective = EditorGUILayout.Toggle(
                "Show As Player Objective",
                step.showAsPlayerObjective);
            if (step.showAsPlayerObjective)
            {
                step.objectiveEnglish = EditorGUILayout.TextField(
                    "Objective (English)",
                    step.objectiveEnglish);
                step.objectiveFilipino = EditorGUILayout.TextField(
                    "Objective (Filipino, optional)",
                    step.objectiveFilipino);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "This story action still runs in order, but it will not appear on the HUD, " +
                    "in the journal, or in the objective JSON.",
                    MessageType.None);
            }
            DrawStepSpecificFields(step);
        }

        EditorGUILayout.EndVertical();

        if (listChanged)
        {
            MarkDraftDirty();
            GUIUtility.ExitGUI();
            return true;
        }

        return false;
    }

    private void DrawStepSpecificFields(StepDraft step)
    {
        switch (step.kind)
        {
            case StepKind.Artifact:
                step.targetArtifact = (ArtifactInfoSO)EditorGUILayout.ObjectField(
                    "Target Artifact",
                    step.targetArtifact,
                    typeof(ArtifactInfoSO),
                    false);
                break;
            case StepKind.Conversation:
                step.targetNpcId = EditorGUILayout.TextField("Target NPC ID", step.targetNpcId).Trim();
                step.conversationJson = (TextAsset)EditorGUILayout.ObjectField(
                    "Conversation JSON",
                    step.conversationJson,
                    typeof(TextAsset),
                    false);
                EditorGUILayout.HelpBox(
                    "Conversation dialogue remains a separate JSON asset. The builder validates its ID, " +
                    "language blocks, lines, speaker assets, and Mansion Speaker Registry before linking it.",
                    MessageType.None);
                break;
            case StepKind.InspectArtifacts:
                step.roomId = EditorGUILayout.TextField("Room ID", step.roomId).Trim();
                step.requiredCount = EditorGUILayout.IntField("Required Artifact Count", step.requiredCount);
                break;
            case StepKind.MeetCharacters:
                DrawCharacterTargets(step.characters);
                break;
            case StepKind.NPCMovement:
                DrawMovementTargets(step.movementTargets);
                step.disableInteractionWhileMoving = EditorGUILayout.Toggle(
                    "Disable Interaction While Moving",
                    step.disableInteractionWhileMoving);
                step.waitForAllRoutesToFinish = EditorGUILayout.Toggle(
                    "Wait For All Routes",
                    step.waitForAllRoutesToFinish);
                break;
            case StepKind.EnterRoom:
                step.roomId = EditorGUILayout.TextField("Target Room ID", step.roomId).Trim();
                break;
            case StepKind.SpeakToAmbientNPCs:
                step.requiredTag = (AmbientNPCTag)EditorGUILayout.EnumFlagsField(
                    "Required NPC Tag",
                    step.requiredTag);
                step.requiredCount = EditorGUILayout.IntField("Required Unique NPCs", step.requiredCount);
                break;
        }
    }

    private void DrawCharacterTargets(List<CharacterTargetDraft> targets)
    {
        EditorGUILayout.LabelField("Character Targets", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("NPC ID", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Journal Display Name", EditorStyles.miniLabel);
        GUILayout.Space(32f);
        EditorGUILayout.EndHorizontal();

        for (int index = 0; index < targets.Count; index++)
        {
            EditorGUILayout.BeginHorizontal();
            targets[index].npcId = EditorGUILayout.TextField(targets[index].npcId);
            targets[index].displayName = EditorGUILayout.TextField(targets[index].displayName);
            if (GUILayout.Button("X", GUILayout.Width(28f)))
            {
                targets.RemoveAt(index);
                MarkDraftDirty();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Add Character Target"))
        {
            targets.Add(new CharacterTargetDraft());
            MarkDraftDirty();
        }
    }

    private void DrawMovementTargets(List<MovementTargetDraft> targets)
    {
        EditorGUILayout.LabelField("Movement Targets", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("NPC ID", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Fixed Route ID", EditorStyles.miniLabel);
        GUILayout.Space(32f);
        EditorGUILayout.EndHorizontal();

        for (int index = 0; index < targets.Count; index++)
        {
            EditorGUILayout.BeginHorizontal();
            targets[index].npcId = EditorGUILayout.TextField(targets[index].npcId);
            targets[index].routeId = EditorGUILayout.TextField(targets[index].routeId);
            if (GUILayout.Button("X", GUILayout.Width(28f)))
            {
                targets.RemoveAt(index);
                MarkDraftDirty();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Add Movement Target"))
        {
            targets.Add(new MovementTargetDraft());
            MarkDraftDirty();
        }
    }

    private void ShowAddStepMenu()
    {
        GenericMenu menu = new();
        foreach (StepKind kind in Enum.GetValues(typeof(StepKind)))
        {
            StepKind capturedKind = kind;
            menu.AddItem(
                new GUIContent(ObjectNames.NicifyVariableName(kind.ToString())),
                false,
                () =>
                {
                    steps.Add(CreateStepDraft(capturedKind));
                    MarkDraftDirty();
                    Repaint();
                });
        }
        menu.ShowAsContext();
    }

    private static StepDraft CreateStepDraft(StepKind kind)
    {
        StepDraft step = new() { kind = kind };
        switch (kind)
        {
            case StepKind.InspectArtifacts:
                step.requiredCount = 5;
                break;
            case StepKind.MeetCharacters:
                step.characters.Add(new CharacterTargetDraft());
                break;
            case StepKind.NPCMovement:
                step.movementTargets.Add(new MovementTargetDraft());
                break;
            case StepKind.SpeakToAmbientNPCs:
                step.requiredCount = 3;
                break;
        }
        return step;
    }

    private bool CanCreate()
    {
        return chapter != null &&
               !string.IsNullOrWhiteSpace(missionNameEnglish) &&
               !string.IsNullOrWhiteSpace(missionId) &&
               steps != null &&
               steps.Count > 0 &&
               steps.All(step => step != null &&
                                 (!step.showAsPlayerObjective ||
                                  !string.IsNullOrWhiteSpace(step.objectiveEnglish)));
    }

    private void CreateMissionGroup()
    {
        missionId = Slugify(missionId);
        List<string> objectiveIds = Enumerable.Range(0, steps.Count)
            .Select(index => steps[index].showAsPlayerObjective
                ? $"{missionId}_step_{index + 1}"
                : string.Empty)
            .ToList();
        string missionsJsonPath = ResolveChapterJsonPath("Missions.json");
        string objectivesJsonPath = ResolveChapterJsonPath("MissionObjectives.json");

        if (!File.Exists(ToAbsolutePath(missionsJsonPath)) ||
            !File.Exists(ToAbsolutePath(objectivesJsonPath)))
        {
            EditorUtility.DisplayDialog(
                "Mission Group Builder",
                $"{chapter.name} does not have its mission JSON files yet. Expected them under " +
                $"'{ChapterMissionJsonRoot}/{chapter.name}'.",
                "OK");
            return;
        }

        if (!ValidateInput(objectiveIds, missionsJsonPath, objectivesJsonPath, out string error))
        {
            if (!string.IsNullOrWhiteSpace(error))
                EditorUtility.DisplayDialog("Mission Group Builder", error, "OK");
            return;
        }

        string chapterFolderName = SanitizeFileName(chapter.name);
        string missionFolder = $"{MissionAssetsRoot}/{chapterFolderName}";
        string stepFolder = $"{MissionStepsRoot}/{chapterFolderName}";
        EnsureFolder(MissionAssetsRoot, chapterFolderName);
        EnsureFolder(MissionStepsRoot, chapterFolderName);

        string safeMissionName = SanitizeFileName(missionNameEnglish);
        string missionAssetPath = $"{missionFolder}/{safeMissionName}.asset";
        List<string> stepPrefabPaths = steps
            .Select((step, index) =>
                $"{stepFolder}/{safeMissionName} {index + 1:D2} {ObjectNames.NicifyVariableName(step.kind.ToString())}.prefab")
            .ToList();

        IEnumerable<string> plannedPaths = new[] { missionAssetPath }.Concat(stepPrefabPaths);
        string occupiedPath = plannedPaths.FirstOrDefault(path =>
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null);
        if (!string.IsNullOrWhiteSpace(occupiedPath))
        {
            EditorUtility.DisplayDialog(
                "Mission Group Builder",
                $"An asset already exists at '{occupiedPath}'. Change the English mission name.",
                "OK");
            return;
        }

        string originalMissionsJson = File.ReadAllText(ToAbsolutePath(missionsJsonPath));
        string originalObjectivesJson = File.ReadAllText(ToAbsolutePath(objectivesJsonPath));
        List<string> createdAssetPaths = new();

        try
        {
            AppendLocalizedText(objectiveIds, missionsJsonPath, objectivesJsonPath);
            AssetDatabase.ImportAsset(missionsJsonPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(objectivesJsonPath, ImportAssetOptions.ForceSynchronousImport);
            MissionLocalizationJson.ClearCache();
            MissionObjectiveLocalizationJson.ClearCache();

            TextAsset missionsJson = AssetDatabase.LoadAssetAtPath<TextAsset>(missionsJsonPath);
            TextAsset objectivesJson = AssetDatabase.LoadAssetAtPath<TextAsset>(objectivesJsonPath);
            List<MissionStep> stepPrefabs = new();

            for (int index = 0; index < steps.Count; index++)
            {
                MissionStep stepPrefab = CreateStepPrefab(
                    steps[index],
                    stepPrefabPaths[index],
                    objectivesJson,
                    objectiveIds[index],
                    index);
                stepPrefabs.Add(stepPrefab);
                createdAssetPaths.Add(stepPrefabPaths[index]);
            }

            MissionInfoSO mission = CreateMissionAsset(missionAssetPath, missionsJson, stepPrefabs);
            createdAssetPaths.Add(missionAssetPath);
            AddMissionToChapter(mission);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = mission;
            EditorGUIUtility.PingObject(mission);

            EditorUtility.DisplayDialog(
                "Mission Group Created",
                $"Created '{missionNameEnglish}' with {steps.Count} ordered step(s) and added it to {chapter.name}.",
                "OK");
            PrepareForNextMission(mission);
        }
        catch (Exception exception)
        {
            foreach (string createdPath in createdAssetPaths.AsEnumerable().Reverse())
                AssetDatabase.DeleteAsset(createdPath);

            File.WriteAllText(ToAbsolutePath(missionsJsonPath), originalMissionsJson, new UTF8Encoding(false));
            File.WriteAllText(ToAbsolutePath(objectivesJsonPath), originalObjectivesJson, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(missionsJsonPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(objectivesJsonPath, ImportAssetOptions.ForceSynchronousImport);
            MissionLocalizationJson.ClearCache();
            MissionObjectiveLocalizationJson.ClearCache();

            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Mission Group Builder",
                $"Nothing was kept because creation failed. Check the Console for details.\n\n{exception.Message}",
                "OK");
        }
    }

    private bool ValidateInput(
        IReadOnlyList<string> objectiveIds,
        string missionsJsonPath,
        string objectivesJsonPath,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(missionId))
        {
            error = "Mission ID must contain at least one letter or number.";
            return false;
        }

        if (prerequisite != null && !chapter.Missions.Contains(prerequisite))
        {
            error = "The prerequisite group is not registered in the selected chapter.";
            return false;
        }

        for (int index = 0; index < steps.Count; index++)
        {
            if (!ValidateStep(steps[index], index, out error))
                return false;
        }

        MissionFile missionFile = ReadJson<MissionFile>(missionsJsonPath);
        missionFile.missions ??= new List<MissionJsonEntry>();
        if (missionFile.missions.Any(entry =>
                entry != null && string.Equals(entry.missionId, missionId, StringComparison.Ordinal)))
        {
            error = $"Mission ID '{missionId}' already exists.";
            return false;
        }

        ObjectiveFile objectiveFile = ReadJson<ObjectiveFile>(objectivesJsonPath);
        objectiveFile.objectives ??= new List<MissionObjectiveJsonEntry>();
        foreach (string objectiveId in objectiveIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (objectiveFile.objectives.Any(entry =>
                    entry != null && string.Equals(entry.objectiveId, objectiveId, StringComparison.Ordinal)))
            {
                error = $"Objective ID '{objectiveId}' already exists.";
                return false;
            }
        }

        if (makeChapterStartingMission &&
            chapter.StartingMission != null &&
            !EditorUtility.DisplayDialog(
                "Replace Starting Mission Group?",
                $"{chapter.name} already starts with '{chapter.StartingMission.name}'. Replace it?",
                "Replace",
                "Cancel"))
        {
            error = string.Empty;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateStep(StepDraft step, int index, out string error)
    {
        string prefix = $"Step {index + 1} ({ObjectNames.NicifyVariableName(step.kind.ToString())})";
        if (step.showAsPlayerObjective && string.IsNullOrWhiteSpace(step.objectiveEnglish))
        {
            error = $"{prefix} needs an English objective.";
            return false;
        }

        switch (step.kind)
        {
            case StepKind.Artifact when step.targetArtifact == null:
                error = $"{prefix} needs a target Artifact Info asset.";
                return false;
            case StepKind.Conversation when
                string.IsNullOrWhiteSpace(step.targetNpcId) || step.conversationJson == null:
                error = $"{prefix} needs a target NPC ID and conversation JSON.";
                return false;
            case StepKind.Conversation:
                if (!ValidateConversationStep(step, prefix, out error))
                    return false;
                break;
            case StepKind.InspectArtifacts when
                string.IsNullOrWhiteSpace(step.roomId) || step.requiredCount < 1:
                error = $"{prefix} needs a room ID and a required count of at least one.";
                return false;
            case StepKind.MeetCharacters:
                if (!ValidateCharacterTargets(step.characters, prefix, out error))
                    return false;
                break;
            case StepKind.NPCMovement:
                if (!ValidateMovementTargets(step.movementTargets, prefix, out error))
                    return false;
                break;
            case StepKind.EnterRoom when string.IsNullOrWhiteSpace(step.roomId):
                error = $"{prefix} needs a target room ID.";
                return false;
            case StepKind.SpeakToAmbientNPCs when
                step.requiredTag == AmbientNPCTag.None || step.requiredCount < 1:
                error = $"{prefix} needs an ambient NPC tag and a required count of at least one.";
                return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateCharacterTargets(
        IReadOnlyCollection<CharacterTargetDraft> targets,
        string prefix,
        out string error)
    {
        if (targets == null || targets.Count == 0 ||
            targets.Any(target => target == null ||
                                  string.IsNullOrWhiteSpace(target.npcId) ||
                                  string.IsNullOrWhiteSpace(target.displayName)))
        {
            error = $"{prefix} needs at least one character with an NPC ID and journal display name.";
            return false;
        }

        if (targets.Select(target => target.npcId.Trim()).Distinct(StringComparer.Ordinal).Count() != targets.Count)
        {
            error = $"{prefix} contains a duplicate NPC ID.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateMovementTargets(
        IReadOnlyCollection<MovementTargetDraft> targets,
        string prefix,
        out string error)
    {
        if (targets == null || targets.Count == 0 ||
            targets.Any(target => target == null ||
                                  string.IsNullOrWhiteSpace(target.npcId) ||
                                  string.IsNullOrWhiteSpace(target.routeId)))
        {
            error = $"{prefix} needs at least one NPC ID and fixed route ID pair.";
            return false;
        }

        if (targets.Select(target => target.npcId.Trim()).Distinct(StringComparer.Ordinal).Count() != targets.Count)
        {
            error = $"{prefix} contains a duplicate NPC ID.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateConversationStep(
        StepDraft step,
        string prefix,
        out string error)
    {
        string conversationPath = AssetDatabase.GetAssetPath(step.conversationJson);
        if (string.IsNullOrWhiteSpace(conversationPath) ||
            !conversationPath.StartsWith("Assets/JSON Files/Conversations/", StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(conversationPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            error = $"{prefix} must reference a JSON asset inside Assets/JSON Files/Conversations.";
            return false;
        }

        Conversation conversation;
        try
        {
            conversation = JsonUtility.FromJson<Conversation>(step.conversationJson.text);
        }
        catch (Exception exception)
        {
            error = $"{prefix} has malformed conversation JSON: {exception.Message}";
            return false;
        }

        if (conversation == null || string.IsNullOrWhiteSpace(conversation.conversationId))
        {
            error = $"{prefix} conversation JSON needs a conversationId.";
            return false;
        }

        if (conversation.languages == null || conversation.languages.Count == 0)
        {
            error = $"{prefix} conversation '{conversation.conversationId}' needs at least one language block.";
            return false;
        }

        HashSet<string> languageCodes = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> speakerIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (ConversationLanguageContent language in conversation.languages)
        {
            if (language == null ||
                string.IsNullOrWhiteSpace(language.languageCode) ||
                !languageCodes.Add(language.languageCode.Trim()))
            {
                error = $"{prefix} conversation '{conversation.conversationId}' contains an empty or duplicate language code.";
                return false;
            }

            if (language.lines == null || language.lines.Count == 0)
            {
                error = $"{prefix} conversation '{conversation.conversationId}' has no lines for '{language.languageCode}'.";
                return false;
            }

            foreach (DialogueLine line in language.lines)
            {
                if (line == null ||
                    string.IsNullOrWhiteSpace(line.speakerId) ||
                    string.IsNullOrWhiteSpace(line.text))
                {
                    error = $"{prefix} conversation '{conversation.conversationId}' contains an empty speaker ID or dialogue line.";
                    return false;
                }

                speakerIds.Add(line.speakerId.Trim());
            }
        }

        if (string.IsNullOrWhiteSpace(conversation.defaultLanguageCode) ||
            !languageCodes.Contains(conversation.defaultLanguageCode.Trim()))
        {
            error = $"{prefix} conversation '{conversation.conversationId}' has no usable default language block.";
            return false;
        }

        if (!speakerIds.Contains(step.targetNpcId.Trim()))
        {
            error = $"{prefix} target NPC '{step.targetNpcId}' never speaks in conversation '{conversation.conversationId}'.";
            return false;
        }

        if (!ValidateUniqueConversationId(conversationPath, conversation.conversationId, out error))
            return false;

        Dictionary<string, NPCInfoSO> npcAssets = FindNpcAssetsById(out string duplicateNpcId);
        if (!string.IsNullOrWhiteSpace(duplicateNpcId))
        {
            error = $"The project contains duplicate primary NPC ID '{duplicateNpcId}'.";
            return false;
        }

        string unknownSpeaker = speakerIds.FirstOrDefault(id => !npcAssets.ContainsKey(id));
        if (!string.IsNullOrWhiteSpace(unknownSpeaker))
        {
            error = $"{prefix} conversation '{conversation.conversationId}' references unknown NPC asset ID '{unknownSpeaker}'.";
            return false;
        }

        SpeakerRegistry registry = UnityEngine.Object.FindObjectsByType<SpeakerRegistry>(
                FindObjectsInactive.Include)
            .FirstOrDefault(candidate =>
                candidate != null && candidate.gameObject.scene.IsValid() && candidate.gameObject.scene.isLoaded);
        if (registry == null)
        {
            error = $"Open Mansion before creating {prefix}, so its Speaker Registry can be validated.";
            return false;
        }

        HashSet<string> registeredSpeakerIds = GetRegisteredSpeakerIds(registry);
        string unregisteredSpeaker = speakerIds.FirstOrDefault(id => !registeredSpeakerIds.Contains(id));
        if (!string.IsNullOrWhiteSpace(unregisteredSpeaker))
        {
            error = $"{prefix} speaker '{unregisteredSpeaker}' is not in Mansion's Speaker Registry. " +
                    "Register that NPC Info asset before creating the group.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateUniqueConversationId(
        string selectedAssetPath,
        string conversationId,
        out string error)
    {
        foreach (string guid in AssetDatabase.FindAssets(
                     "t:TextAsset",
                     new[] { "Assets/JSON Files/Conversations" }))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(assetPath, selectedAssetPath, StringComparison.Ordinal))
                continue;

            TextAsset candidate = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.text))
                continue;

            Conversation parsed;
            try
            {
                parsed = JsonUtility.FromJson<Conversation>(candidate.text);
            }
            catch
            {
                continue;
            }

            if (parsed != null &&
                string.Equals(parsed.conversationId, conversationId, StringComparison.Ordinal))
            {
                error = $"Conversation ID '{conversationId}' is already used by '{assetPath}'.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static Dictionary<string, NPCInfoSO> FindNpcAssetsById(out string duplicateNpcId)
    {
        Dictionary<string, NPCInfoSO> byId = new(StringComparer.OrdinalIgnoreCase);
        duplicateNpcId = string.Empty;

        foreach (string guid in AssetDatabase.FindAssets("t:NPCInfoSO"))
        {
            NPCInfoSO npc = AssetDatabase.LoadAssetAtPath<NPCInfoSO>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (npc == null || string.IsNullOrWhiteSpace(npc.NpcID))
                continue;

            if (!byId.TryAdd(npc.NpcID.Trim(), npc))
            {
                duplicateNpcId = npc.NpcID.Trim();
                return byId;
            }
        }

        return byId;
    }

    private static HashSet<string> GetRegisteredSpeakerIds(SpeakerRegistry registry)
    {
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        SerializedObject serializedRegistry = new(registry);
        SerializedProperty speakers = serializedRegistry.FindProperty("allSpeakers");
        if (speakers == null)
            return ids;

        for (int index = 0; index < speakers.arraySize; index++)
        {
            NPCInfoSO npc = speakers.GetArrayElementAtIndex(index).objectReferenceValue as NPCInfoSO;
            if (npc != null && !string.IsNullOrWhiteSpace(npc.NpcID))
                ids.Add(npc.NpcID.Trim());
        }

        return ids;
    }

    private void AppendLocalizedText(
        IReadOnlyList<string> objectiveIds,
        string missionsJsonPath,
        string objectivesJsonPath)
    {
        MissionFile missionFile = ReadJson<MissionFile>(missionsJsonPath);
        missionFile.missions ??= new List<MissionJsonEntry>();
        MissionJsonEntry missionEntry = new() { missionId = missionId };
        missionEntry.languages.Add(new MissionLanguageContent
        {
            languageCode = "en",
            displayName = missionNameEnglish.Trim()
        });
        if (!string.IsNullOrWhiteSpace(missionNameFilipino))
        {
            missionEntry.languages.Add(new MissionLanguageContent
            {
                languageCode = "fil",
                displayName = missionNameFilipino.Trim()
            });
        }
        missionFile.missions.Add(missionEntry);

        ObjectiveFile objectiveFile = ReadJson<ObjectiveFile>(objectivesJsonPath);
        objectiveFile.objectives ??= new List<MissionObjectiveJsonEntry>();
        List<MissionObjectiveJsonEntry> newObjectiveEntries = new();
        for (int index = 0; index < steps.Count; index++)
        {
            StepDraft step = steps[index];
            if (!step.showAsPlayerObjective)
                continue;

            MissionObjectiveJsonEntry objectiveEntry = new() { objectiveId = objectiveIds[index] };
            objectiveEntry.languages.Add(new MissionObjectiveLanguageContent
            {
                languageCode = "en",
                description = step.objectiveEnglish.Trim()
            });
            if (!string.IsNullOrWhiteSpace(step.objectiveFilipino))
            {
                objectiveEntry.languages.Add(new MissionObjectiveLanguageContent
                {
                    languageCode = "fil",
                    description = step.objectiveFilipino.Trim()
                });
            }
            objectiveFile.objectives.Add(objectiveEntry);
            newObjectiveEntries.Add(objectiveEntry);
        }

        AppendJsonArrayEntries(
            missionsJsonPath,
            "missions",
            new[] { JsonUtility.ToJson(missionEntry, true) });
        AppendJsonArrayEntries(
            objectivesJsonPath,
            "objectives",
            newObjectiveEntries.Select(entry => JsonUtility.ToJson(entry, true)));
    }

    private MissionStep CreateStepPrefab(
        StepDraft draft,
        string prefabPath,
        TextAsset objectivesJson,
        string objectiveId,
        int index)
    {
        string typeName = ObjectNames.NicifyVariableName(draft.kind.ToString());
        GameObject stepObject = new($"{missionNameEnglish.Trim()} {index + 1:D2} {typeName}");
        try
        {
            MissionStep step = AddStepComponent(stepObject, draft.kind);
            SerializedObject serializedStep = new(step);
            serializedStep.FindProperty("showAsPlayerObjective").boolValue = draft.showAsPlayerObjective;
            serializedStep.FindProperty("localizedObjectiveJson").objectReferenceValue =
                draft.showAsPlayerObjective ? objectivesJson : null;
            serializedStep.FindProperty("objectiveId").stringValue =
                draft.showAsPlayerObjective ? objectiveId : string.Empty;
            serializedStep.FindProperty("objectiveDescription").stringValue =
                draft.showAsPlayerObjective ? draft.objectiveEnglish.Trim() : string.Empty;
            ConfigureStep(serializedStep, draft);
            serializedStep.ApplyModifiedPropertiesWithoutUndo();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(stepObject, prefabPath);
            if (savedPrefab == null)
                throw new InvalidOperationException($"Unity could not save step prefab '{prefabPath}'.");
            return savedPrefab.GetComponent<MissionStep>();
        }
        finally
        {
            DestroyImmediate(stepObject);
        }
    }

    private static MissionStep AddStepComponent(GameObject stepObject, StepKind kind)
    {
        return kind switch
        {
            StepKind.Artifact => stepObject.AddComponent<ArtifactMissionStep>(),
            StepKind.Conversation => stepObject.AddComponent<ConversationMissionStep>(),
            StepKind.InspectArtifacts => stepObject.AddComponent<InspectArtifactsMissionStep>(),
            StepKind.MeetCharacters => stepObject.AddComponent<MeetCharactersMissionStep>(),
            StepKind.NPCMovement => stepObject.AddComponent<NPCMovementMissionStep>(),
            StepKind.EnterRoom => stepObject.AddComponent<EnterRoomMissionStep>(),
            StepKind.SpeakToAmbientNPCs => stepObject.AddComponent<SpeakToAmbientNPCsMissionStep>(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static void ConfigureStep(SerializedObject serializedStep, StepDraft draft)
    {
        switch (draft.kind)
        {
            case StepKind.Artifact:
                serializedStep.FindProperty("targetArtifactInfo").objectReferenceValue = draft.targetArtifact;
                break;
            case StepKind.Conversation:
                serializedStep.FindProperty("targetNPCId").stringValue = draft.targetNpcId.Trim();
                serializedStep.FindProperty("conversationJson").objectReferenceValue = draft.conversationJson;
                break;
            case StepKind.InspectArtifacts:
                serializedStep.FindProperty("roomID").stringValue = draft.roomId.Trim();
                serializedStep.FindProperty("requiredArtifactCount").intValue = draft.requiredCount;
                break;
            case StepKind.MeetCharacters:
                ConfigureCharacterTargets(serializedStep.FindProperty("characters"), draft.characters);
                break;
            case StepKind.NPCMovement:
                ConfigureMovementTargets(serializedStep.FindProperty("movementTargets"), draft.movementTargets);
                serializedStep.FindProperty("disableInteractionWhileMoving").boolValue =
                    draft.disableInteractionWhileMoving;
                serializedStep.FindProperty("waitForAllRoutesToFinish").boolValue =
                    draft.waitForAllRoutesToFinish;
                break;
            case StepKind.EnterRoom:
                serializedStep.FindProperty("targetRoomID").stringValue = draft.roomId.Trim();
                break;
            case StepKind.SpeakToAmbientNPCs:
                serializedStep.FindProperty("requiredTag").intValue = (int)draft.requiredTag;
                serializedStep.FindProperty("requiredUniqueCount").intValue = draft.requiredCount;
                break;
        }
    }

    private static void ConfigureCharacterTargets(
        SerializedProperty property,
        IReadOnlyList<CharacterTargetDraft> targets)
    {
        property.arraySize = targets.Count;
        for (int index = 0; index < targets.Count; index++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("npcId").stringValue = targets[index].npcId.Trim();
            element.FindPropertyRelative("displayName").stringValue = targets[index].displayName.Trim();
        }
    }

    private static void ConfigureMovementTargets(
        SerializedProperty property,
        IReadOnlyList<MovementTargetDraft> targets)
    {
        property.arraySize = targets.Count;
        for (int index = 0; index < targets.Count; index++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("npcId").stringValue = targets[index].npcId.Trim();
            element.FindPropertyRelative("routeId").stringValue = targets[index].routeId.Trim();
        }
    }

    private MissionInfoSO CreateMissionAsset(
        string assetPath,
        TextAsset missionsJson,
        IReadOnlyList<MissionStep> stepPrefabs)
    {
        MissionInfoSO mission = CreateInstance<MissionInfoSO>();
        SerializedObject serializedMission = new(mission);
        serializedMission.FindProperty("localizedDataJson").objectReferenceValue = missionsJson;
        serializedMission.FindProperty("missionId").stringValue = missionId;
        SerializedProperty prerequisites = serializedMission.FindProperty("prerequisites");
        prerequisites.arraySize = prerequisite != null ? 1 : 0;
        if (prerequisite != null)
            prerequisites.GetArrayElementAtIndex(0).objectReferenceValue = prerequisite;
        serializedMission.FindProperty("autoStartWhenAvailable").boolValue = autoStartWhenAvailable;
        SerializedProperty stepArray = serializedMission.FindProperty("missionStepPrefabs");
        stepArray.arraySize = stepPrefabs.Count;
        for (int index = 0; index < stepPrefabs.Count; index++)
            stepArray.GetArrayElementAtIndex(index).objectReferenceValue = stepPrefabs[index];
        serializedMission.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(mission, assetPath);
        return mission;
    }

    private void AddMissionToChapter(MissionInfoSO mission)
    {
        Undo.RecordObject(chapter, $"Add {missionNameEnglish} mission group");
        SerializedObject serializedChapter = new(chapter);
        SerializedProperty missions = serializedChapter.FindProperty("missions");
        missions.arraySize++;
        missions.GetArrayElementAtIndex(missions.arraySize - 1).objectReferenceValue = mission;

        if (makeChapterStartingMission)
            serializedChapter.FindProperty("startingMission").objectReferenceValue = mission;

        serializedChapter.ApplyModifiedProperties();
        EditorUtility.SetDirty(chapter);
    }

    private void PrepareForNextMission(MissionInfoSO createdMission)
    {
        prerequisite = createdMission;
        makeChapterStartingMission = false;
        missionNameEnglish = string.Empty;
        missionNameFilipino = string.Empty;
        missionId = string.Empty;
        lastGeneratedMissionId = string.Empty;
        steps = new List<StepDraft> { CreateStepDraft(StepKind.EnterRoom) };
        SaveDraft();
        Repaint();
    }

    private void MarkDraftDirty()
    {
        draftDirty = true;
        draftSaveAt = EditorApplication.timeSinceStartup + DraftSaveDelaySeconds;
        draftStatus = "Saving draft...";
    }

    private void SaveDraft(bool showConfirmation = false)
    {
        try
        {
            string absolutePath = GetDraftAbsolutePath();
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            MissionGroupDraftFile draft = CaptureDraft();
            File.WriteAllText(
                absolutePath,
                JsonUtility.ToJson(draft, true),
                new UTF8Encoding(false));

            draftDirty = false;
            draftStatus = $"Draft saved at {DateTime.Now:HH:mm:ss}.";

            if (showConfirmation)
            {
                EditorUtility.DisplayDialog(
                    "Mission Draft Saved",
                    "Your unfinished mission group is saved. You can safely close this window or Unity.",
                    "OK");
            }
        }
        catch (Exception exception)
        {
            draftStatus = $"Could not save draft: {exception.Message}";
            Debug.LogError($"Mission Group Builder could not save its draft: {exception}");
        }
    }

    private bool TryLoadDraft(out string error)
    {
        error = string.Empty;
        string absolutePath = GetDraftAbsolutePath();
        if (!File.Exists(absolutePath))
            return false;

        try
        {
            MissionGroupDraftFile draft = JsonUtility.FromJson<MissionGroupDraftFile>(
                File.ReadAllText(absolutePath));
            if (draft == null || draft.version < 1 || draft.version > 2)
            {
                error = "The saved mission draft has an unsupported format.";
                return false;
            }

            if (draft.version == 1)
            {
                foreach (StepDraftFile savedStep in draft.steps ?? new List<StepDraftFile>())
                {
                    if (savedStep != null)
                        savedStep.showAsPlayerObjective = true;
                }
            }

            ApplyDraft(draft);
            draftDirty = false;
            draftStatus = "Saved mission draft restored.";
            return true;
        }
        catch (Exception exception)
        {
            error = $"Could not load saved draft: {exception.Message}";
            Debug.LogError($"Mission Group Builder could not load its draft: {exception}");
            return false;
        }
    }

    private MissionGroupDraftFile CaptureDraft()
    {
        MissionGroupDraftFile draft = new()
        {
            chapterGuid = GetAssetGuid(chapter),
            missionNameEnglish = missionNameEnglish,
            missionNameFilipino = missionNameFilipino,
            missionId = missionId,
            lastGeneratedMissionId = lastGeneratedMissionId,
            prerequisiteGuid = GetAssetGuid(prerequisite),
            autoStartWhenAvailable = autoStartWhenAvailable,
            makeChapterStartingMission = makeChapterStartingMission
        };

        foreach (StepDraft step in steps ?? new List<StepDraft>())
        {
            if (step == null)
                continue;

            draft.steps.Add(new StepDraftFile
            {
                expanded = step.expanded,
                kind = step.kind,
                showAsPlayerObjective = step.showAsPlayerObjective,
                objectiveEnglish = step.objectiveEnglish,
                objectiveFilipino = step.objectiveFilipino,
                targetArtifactGuid = GetAssetGuid(step.targetArtifact),
                targetNpcId = step.targetNpcId,
                conversationJsonGuid = GetAssetGuid(step.conversationJson),
                roomId = step.roomId,
                requiredCount = step.requiredCount,
                characters = step.characters ?? new List<CharacterTargetDraft>(),
                movementTargets = step.movementTargets ?? new List<MovementTargetDraft>(),
                disableInteractionWhileMoving = step.disableInteractionWhileMoving,
                waitForAllRoutesToFinish = step.waitForAllRoutesToFinish,
                requiredTag = step.requiredTag
            });
        }

        return draft;
    }

    private void ApplyDraft(MissionGroupDraftFile draft)
    {
        chapter = LoadAssetByGuid<ChapterDataSO>(draft.chapterGuid);
        missionNameEnglish = draft.missionNameEnglish ?? string.Empty;
        missionNameFilipino = draft.missionNameFilipino ?? string.Empty;
        missionId = draft.missionId ?? string.Empty;
        lastGeneratedMissionId = draft.lastGeneratedMissionId ?? string.Empty;
        prerequisite = LoadAssetByGuid<MissionInfoSO>(draft.prerequisiteGuid);
        autoStartWhenAvailable = draft.autoStartWhenAvailable;
        makeChapterStartingMission = draft.makeChapterStartingMission;
        steps = new List<StepDraft>();

        foreach (StepDraftFile savedStep in draft.steps ?? new List<StepDraftFile>())
        {
            if (savedStep == null)
                continue;

            steps.Add(new StepDraft
            {
                expanded = savedStep.expanded,
                kind = savedStep.kind,
                showAsPlayerObjective = savedStep.showAsPlayerObjective,
                objectiveEnglish = savedStep.objectiveEnglish ?? string.Empty,
                objectiveFilipino = savedStep.objectiveFilipino ?? string.Empty,
                targetArtifact = LoadAssetByGuid<ArtifactInfoSO>(savedStep.targetArtifactGuid),
                targetNpcId = savedStep.targetNpcId ?? string.Empty,
                conversationJson = LoadAssetByGuid<TextAsset>(savedStep.conversationJsonGuid),
                roomId = savedStep.roomId ?? string.Empty,
                requiredCount = savedStep.requiredCount,
                characters = savedStep.characters ?? new List<CharacterTargetDraft>(),
                movementTargets = savedStep.movementTargets ?? new List<MovementTargetDraft>(),
                disableInteractionWhileMoving = savedStep.disableInteractionWhileMoving,
                waitForAllRoutesToFinish = savedStep.waitForAllRoutesToFinish,
                requiredTag = savedStep.requiredTag
            });
        }

        if (steps.Count == 0)
            steps.Add(CreateStepDraft(StepKind.EnterRoom));
    }

    private static string GetAssetGuid(UnityEngine.Object asset)
    {
        if (asset == null)
            return string.Empty;

        string assetPath = AssetDatabase.GetAssetPath(asset);
        return string.IsNullOrWhiteSpace(assetPath)
            ? string.Empty
            : AssetDatabase.AssetPathToGUID(assetPath);
    }

    private static T LoadAssetByGuid<T>(string guid) where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(guid))
            return null;

        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrWhiteSpace(assetPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<T>(assetPath);
    }

    private static string GetDraftAbsolutePath()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
        return Path.Combine(
            projectRoot,
            DraftRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static T ReadJson<T>(string assetPath) where T : class, new()
    {
        string absolutePath = ToAbsolutePath(assetPath);
        if (!File.Exists(absolutePath))
            return new T();

        T parsed = JsonUtility.FromJson<T>(File.ReadAllText(absolutePath));
        return parsed ?? new T();
    }

    private static void AppendJsonArrayEntries(
        string assetPath,
        string propertyName,
        IEnumerable<string> serializedEntries)
    {
        string absolutePath = ToAbsolutePath(assetPath);
        string original = File.ReadAllText(absolutePath);
        string propertyToken = $"\"{propertyName}\"";
        int propertyIndex = original.IndexOf(propertyToken, StringComparison.Ordinal);
        int arrayStart = propertyIndex >= 0 ? original.IndexOf('[', propertyIndex + propertyToken.Length) : -1;
        int arrayEnd = arrayStart >= 0 ? FindMatchingArrayEnd(original, arrayStart) : -1;
        if (propertyIndex < 0 || arrayStart < 0 || arrayEnd < 0)
            throw new InvalidOperationException($"Could not find JSON array '{propertyName}' in '{assetPath}'.");

        string lineEnding = original.Contains("\r\n") ? "\r\n" : "\n";
        int propertyLineStart = original.LastIndexOf('\n', propertyIndex);
        propertyLineStart = propertyLineStart < 0 ? 0 : propertyLineStart + 1;
        string propertyIndent = original.Substring(propertyLineStart, propertyIndex - propertyLineStart);
        string entryIndent = propertyIndent + "  ";
        List<string> entries = serializedEntries
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => IndentJson(entry, entryIndent, lineEnding))
            .ToList();
        if (entries.Count == 0)
            return;

        string existingContent = original.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
        string trimmedExisting = existingContent.TrimEnd();
        string replacement = string.IsNullOrWhiteSpace(trimmedExisting)
            ? lineEnding + string.Join("," + lineEnding, entries) + lineEnding + propertyIndent
            : trimmedExisting + "," + lineEnding + string.Join("," + lineEnding, entries) + lineEnding + propertyIndent;

        string updated = original.Substring(0, arrayStart + 1) +
                         replacement +
                         original.Substring(arrayEnd);
        File.WriteAllText(absolutePath, updated, new UTF8Encoding(false));
    }

    private static int FindMatchingArrayEnd(string json, int arrayStart)
    {
        int depth = 0;
        bool inString = false;
        bool escaped = false;
        for (int index = arrayStart; index < json.Length; index++)
        {
            char character = json[index];
            if (inString)
            {
                if (escaped)
                    escaped = false;
                else if (character == '\\')
                    escaped = true;
                else if (character == '"')
                    inString = false;
                continue;
            }

            if (character == '"')
            {
                inString = true;
                continue;
            }

            if (character == '[')
                depth++;
            else if (character == ']' && --depth == 0)
                return index;
        }

        return -1;
    }

    private static string IndentJson(string json, string indent, string lineEnding)
    {
        string normalized = json.Replace("\r\n", "\n").Replace('\r', '\n');
        return string.Join(
            lineEnding,
            normalized.Split('\n').Select(line => indent + line));
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new InvalidOperationException("Could not resolve the Unity project root.");

        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private string ResolveChapterJsonPath(string fileName)
    {
        if (chapter != null &&
            string.Equals(chapter.ChapterId, "chapter_1", StringComparison.Ordinal))
        {
            return string.Equals(fileName, "Missions.json", StringComparison.Ordinal)
                ? Chapter1MissionsJsonPath
                : Chapter1ObjectivesJsonPath;
        }

        return $"{ChapterMissionJsonRoot}/{SanitizeFileName(chapter?.name ?? string.Empty)}/{fileName}";
    }

    private static void EnsureFolder(string parentPath, string childName)
    {
        string folderPath = $"{parentPath}/{childName}";
        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder(parentPath, childName);
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        StringBuilder result = new();
        bool previousWasSeparator = false;
        foreach (char character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                result.Append(character);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && result.Length > 0)
            {
                result.Append('_');
                previousWasSeparator = true;
            }
        }

        return result.ToString().Trim('_');
    }

    private static string SanitizeFileName(string value)
    {
        HashSet<char> invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        string sanitized = new(value
            .Trim()
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "New Mission" : sanitized;
    }
}
