using UnityEngine;

/// <summary>
/// Turns an NPC journal discovery into a one-time departure: the NPC follows a
/// configured fixed route, reveals optional rewards, then leaves the scene.
/// </summary>
[RequireComponent(typeof(NPC), typeof(NPCFixedRoute))]
public class OptionalNPCDeparture : MonoBehaviour
{
    private const string CharacterCollection = "characters";

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
        NPC.OnNPCUnlocked += HandleNPCUnlocked;
        fixedRoute.RouteCompleted += HandleRouteCompleted;
    }

    private void Start()
    {
        SetRewardsActive(false);

        // Restore the completed state when returning to this scene during the
        // same play session. Save/load can use the same journal registry later.
        if (JournalUnlockRegistry.IsUnlocked(CharacterCollection, npc.NpcID))
            ApplyCompletedState();
    }

    private void OnDisable()
    {
        NPC.OnNPCUnlocked -= HandleNPCUnlocked;
        fixedRoute.RouteCompleted -= HandleRouteCompleted;
    }

    private void HandleNPCUnlocked(NPCInfoSO unlockedNPC)
    {
        if (departureStarted || unlockedNPC == null || unlockedNPC.NpcID != npc.NpcID)
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
        if (departureStarted)
            ApplyCompletedState();
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
