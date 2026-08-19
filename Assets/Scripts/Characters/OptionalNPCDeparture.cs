using UnityEngine;

/// <summary>
/// Turns an NPC journal discovery into a one-time departure: the NPC follows a
/// configured fixed route, reveals optional rewards, then leaves the scene.
/// </summary>
[RequireComponent(typeof(NPC), typeof(NPCFixedRoute))]
public class OptionalNPCDeparture : MonoBehaviour
{
    private const string DepartureWorldFlagPrefix = "npc_departed:";

    [SerializeField] private string departureRouteId;
    [SerializeField] private GameObject[] revealAfterDeparture = new GameObject[0];
    [SerializeField] private bool hideNPCAfterDeparture = true;

    private NPC npc;
    private NPCFixedRoute fixedRoute;
    private bool departureStarted;

    private void Awake()
    {
        npc = GetComponent<NPC>();
        fixedRoute = GetComponent<NPCFixedRoute>();
    }

    private void OnEnable()
    {
        NPC.OnNPCInteracted += HandleNPCInteracted;
        fixedRoute.RouteCompleted += HandleRouteCompleted;
    }

    private void Start()
    {
        SetRewardsActive(false);

        if (SaveGameManager.HasActiveChapterWorldFlag(GetDepartureWorldFlag()))
            ApplyCompletedState();
    }

    private void OnDisable()
    {
        NPC.OnNPCInteracted -= HandleNPCInteracted;
        fixedRoute.RouteCompleted -= HandleRouteCompleted;
    }

    private void HandleNPCInteracted(NPCInfoSO interactedNPC)
    {
        if (departureStarted || interactedNPC == null || interactedNPC.NpcID != npc.NpcID)
            return;

        if (!fixedRoute.TryBeginRoute(departureRouteId))
        {
            Debug.LogError(
                $"{gameObject.name} could not begin optional departure route '{departureRouteId}'.",
                this);
            return;
        }

        departureStarted = true;
        npc.SetInteractionEnabled(false);
    }

    private void HandleRouteCompleted()
    {
        if (!departureStarted)
            return;

        SaveGameManager.RecordActiveChapterWorldFlag(GetDepartureWorldFlag());
        ApplyCompletedState();
    }

    private string GetDepartureWorldFlag()
    {
        return DepartureWorldFlagPrefix + npc.NpcID;
    }

    private void ApplyCompletedState()
    {
        SetRewardsActive(true);
        npc.SetInteractionEnabled(false);

        if (hideNPCAfterDeparture)
            gameObject.SetActive(false);
    }

    private void SetRewardsActive(bool active)
    {
        foreach (GameObject reward in revealAfterDeparture)
        {
            if (reward != null)
                reward.SetActive(active);
        }
    }
}
