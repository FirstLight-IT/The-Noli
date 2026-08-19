using System;
using System.Collections.Generic;
using UnityEngine;

public class NPCMovementMissionStep : MissionStep
{
    [Serializable]
    private struct MovementTarget
    {
        [SerializeField] public string npcId;
        [SerializeField] public string routeId;
    }

    [Header("NPC Movement Step")]
    [SerializeField] private MovementTarget[] movementTargets = new MovementTarget[0];
    [SerializeField] private bool disableInteractionWhileMoving = true;

    [Header("Completion")]
    [Tooltip("When disabled, the step completes as soon as every route starts. The NPCs continue moving in the background.")]
    [SerializeField] private bool waitForAllRoutesToFinish = true;

    private readonly Dictionary<NPCFixedRoute, Action> routeCompletedHandlers = new();
    private readonly HashSet<NPCFixedRoute> pendingRoutes = new();
    private readonly Dictionary<NPC, bool> previousInteractionStates = new();

    void OnDisable()
    {
        UnsubscribeFromRoutes();
        RestoreInteractionStates();
    }

    public bool ApplyCompletedWorldState()
    {
        if (movementTargets == null || movementTargets.Length == 0)
            return false;

        bool appliedEveryTarget = true;

        foreach (MovementTarget target in movementTargets)
        {
            if (string.IsNullOrWhiteSpace(target.npcId) ||
                string.IsNullOrWhiteSpace(target.routeId))
            {
                appliedEveryTarget = false;
                continue;
            }

            if (!NPC.TryGetById(target.npcId, out NPC npc))
            {
                Debug.LogWarning(
                    $"Could not restore completed movement for NPC '{target.npcId}' because it is not active.",
                    this);
                appliedEveryTarget = false;
                continue;
            }

            if (!npc.TryGetComponent(out NPCFixedRoute route) ||
                !route.TryApplyCompletedRoute(target.routeId))
            {
                Debug.LogWarning(
                    $"Could not restore completed route '{target.routeId}' for NPC '{target.npcId}'.",
                    npc);
                appliedEveryTarget = false;
            }
        }

        return appliedEveryTarget;
    }

    protected override void OnStepActivated()
    {
        if (movementTargets == null || movementTargets.Length == 0)
        {
            FailStep("NPC movement step has no movement targets.");
            return;
        }

        HashSet<string> uniqueNpcIds = new();

        Dictionary<NPCFixedRoute, string> requestedRouteIds = new();

        foreach (MovementTarget target in movementTargets)
        {
            if (string.IsNullOrWhiteSpace(target.npcId))
            {
                FailStep("Every NPC movement target needs an NPC ID.");
                return;
            }

            if (string.IsNullOrWhiteSpace(target.routeId))
            {
                FailStep($"NPC movement target '{target.npcId}' needs a route ID.");
                return;
            }

            if (!uniqueNpcIds.Add(target.npcId))
            {
                FailStep($"NPC movement step contains duplicate NPC ID '{target.npcId}'.");
                return;
            }

            if (!NPC.TryGetById(target.npcId, out NPC npc))
            {
                FailStep($"Could not find an active NPC with ID '{target.npcId}'.");
                return;
            }

            if (!npc.TryGetComponent(out NPCFixedRoute route))
            {
                FailStep($"NPC '{target.npcId}' needs an NPC Fixed Route component.");
                return;
            }

            if (!route.HasConfiguredRoute(target.routeId))
            {
                FailStep($"NPC '{target.npcId}' has no configured fixed route with ID '{target.routeId}'.");
                return;
            }

            pendingRoutes.Add(route);
            requestedRouteIds.Add(route, target.routeId);

            if (disableInteractionWhileMoving)
                previousInteractionStates.Add(npc, npc.IsInteractionEnabled);
        }

        SetTargetInteractionEnabled(false);

        if (waitForAllRoutesToFinish)
        {
            foreach (NPCFixedRoute route in pendingRoutes)
            {
                NPCFixedRoute capturedRoute = route;
                Action handler = () => HandleRouteCompleted(capturedRoute);
                routeCompletedHandlers.Add(route, handler);
                route.RouteCompleted += handler;
            }
        }

        foreach (NPCFixedRoute route in new List<NPCFixedRoute>(pendingRoutes))
        {
            if (!route.TryBeginRoute(requestedRouteIds[route]))
            {
                CancelPendingRoutes();
                UnsubscribeFromRoutes();
                RestoreInteractionStates();
                FailStep($"Could not start the fixed route on '{route.gameObject.name}'.");
                return;
            }
        }

        if (!waitForAllRoutesToFinish)
        {
            RestoreInteractionStates();
            FinishStep();
        }
    }

    private void HandleRouteCompleted(NPCFixedRoute completedRoute)
    {
        if (!pendingRoutes.Remove(completedRoute))
            return;

        if (routeCompletedHandlers.Remove(completedRoute, out Action handler))
            completedRoute.RouteCompleted -= handler;

        if (pendingRoutes.Count == 0)
        {
            RestoreInteractionStates();
            FinishStep();
        }
    }

    private void CancelPendingRoutes()
    {
        foreach (NPCFixedRoute route in pendingRoutes)
            route.CancelRoute();
    }

    private void UnsubscribeFromRoutes()
    {
        foreach (KeyValuePair<NPCFixedRoute, Action> entry in routeCompletedHandlers)
        {
            if (entry.Key != null)
                entry.Key.RouteCompleted -= entry.Value;
        }

        routeCompletedHandlers.Clear();
        pendingRoutes.Clear();
    }

    private void SetTargetInteractionEnabled(bool enabled)
    {
        foreach (NPC npc in previousInteractionStates.Keys)
        {
            if (npc != null)
                npc.SetInteractionEnabled(enabled);
        }
    }

    private void RestoreInteractionStates()
    {
        foreach (KeyValuePair<NPC, bool> entry in previousInteractionStates)
        {
            if (entry.Key != null)
                entry.Key.SetInteractionEnabled(entry.Value);
        }

        previousInteractionStates.Clear();
    }
}
