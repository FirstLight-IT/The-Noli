using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class NPCMovementRestoreTests
{
    private readonly List<UnityEngine.Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            if (createdObjects[index] != null)
                UnityEngine.Object.DestroyImmediate(createdObjects[index]);
        }

        createdObjects.Clear();
    }

    [Test]
    public void CompletedMovementStep_PlacesEveryNpcAtItsRouteDestination()
    {
        NPC guevara = CreateNpcWithRoute(
            "test_restore_guevara",
            "leave_group",
            new Vector2(7f, -3f));
        NPC victorina = CreateNpcWithRoute(
            "test_restore_victorina",
            "arrive_at_party",
            new Vector2(-2f, 11f));

        GameObject stepObject = Track(new GameObject("Completed NPC Movement Step"));
        NPCMovementMissionStep step = stepObject.AddComponent<NPCMovementMissionStep>();
        SetMovementTargets(
            step,
            ("test_restore_guevara", "leave_group"),
            ("test_restore_victorina", "arrive_at_party"));

        Assert.That(step.ApplyCompletedWorldState(), Is.True);
        Assert.That(
            guevara.GetComponent<Rigidbody2D>().position,
            Is.EqualTo(new Vector2(7f, -3f)));
        Assert.That(
            victorina.GetComponent<Rigidbody2D>().position,
            Is.EqualTo(new Vector2(-2f, 11f)));
    }

    [Test]
    public void StairTrigger_UsesDirectMovementWhileNpcIsOnSlope()
    {
        GameObject stairsObject = Track(new GameObject("Test Stairs"));
        BoxCollider2D stairsCollider = stairsObject.AddComponent<BoxCollider2D>();
        stairsCollider.isTrigger = true;
        StairsTrigger stairs = stairsObject.AddComponent<StairsTrigger>();

        GameObject npcObject = Track(new GameObject("Test NPC"));
        Rigidbody2D body = npcObject.AddComponent<Rigidbody2D>();
        BoxCollider2D npcCollider = npcObject.AddComponent<BoxCollider2D>();
        NPCMover mover = npcObject.AddComponent<NPCMover>();
        SetPrivateField(mover, "body", body);
        SetPrivateField(mover, "isometricMovementOnly", true);

        GameObject destinationObject = Track(new GameObject("Slope Top"));
        destinationObject.transform.position = new Vector3(-3f, 6f, 0f);

        mover.MoveTo(destinationObject.transform);
        Assert.That(GetPrivateField<bool>(mover, "hasIsometricCorner"), Is.True);

        InvokeTrigger(stairs, "OnTriggerEnter2D", npcCollider);
        Assert.That(mover.IsOnSlope, Is.True);
        Assert.That(GetPrivateField<bool>(mover, "hasIsometricCorner"), Is.False);

        InvokeTrigger(stairs, "OnTriggerExit2D", npcCollider);
        Assert.That(mover.IsOnSlope, Is.False);
        Assert.That(GetPrivateField<bool>(mover, "hasIsometricCorner"), Is.True);
    }

    [Test]
    public void BackgroundMovementStep_KeepsInteractionDisabledUntilRouteEnds()
    {
        NPC npc = CreateNpcWithRoute(
            "test_background_movement",
            "walk_away",
            new Vector2(4f, 2f));
        NPCFixedRoute route = npc.GetComponent<NPCFixedRoute>();

        GameObject stepObject = Track(new GameObject("Background NPC Movement Step"));
        NPCMovementMissionStep step = stepObject.AddComponent<NPCMovementMissionStep>();
        SetMovementTargets(step, ("test_background_movement", "walk_away"));
        SetPrivateField(step, "disableInteractionWhileMoving", true);
        SetPrivateField(step, "waitForAllRoutesToFinish", false);

        step.Initialize("test_mission", 0);

        Assert.That(route.IsFollowingRoute, Is.True);
        Assert.That(npc.IsInteractionEnabled, Is.False);

        UnityEngine.Object.DestroyImmediate(stepObject);
        Assert.That(npc.IsInteractionEnabled, Is.False);

        route.CancelRoute();
        Assert.That(npc.IsInteractionEnabled, Is.True);
    }

    private NPC CreateNpcWithRoute(string npcId, string routeId, Vector2 destination)
    {
        NPCInfoSO npcData = Track(ScriptableObject.CreateInstance<NPCInfoSO>());
        SetPrivateField(npcData, "npcID", npcId);

        GameObject destinationObject = Track(new GameObject($"{npcId} Destination"));
        destinationObject.transform.position = destination;

        GameObject npcObject = Track(new GameObject(npcId));
        npcObject.SetActive(false);
        NPC npc = npcObject.AddComponent<NPC>();
        SetPrivateField(npc, "npcData", npcData);
        NPCFixedRoute route = npcObject.AddComponent<NPCFixedRoute>();
        SetRoute(route, routeId, destinationObject.transform);
        npcObject.SetActive(true);
        return npc;
    }

    private static void SetRoute(
        NPCFixedRoute route,
        string routeId,
        Transform destination)
    {
        Type routeType = typeof(NPCFixedRoute).GetNestedType(
            "RouteDefinition",
            BindingFlags.NonPublic);
        Assert.That(routeType, Is.Not.Null);

        object definition = Activator.CreateInstance(routeType);
        SetPrivateField(definition, "routeId", routeId);
        SetPrivateField(definition, "routePoints", new[] { destination });

        Array routes = Array.CreateInstance(routeType, 1);
        routes.SetValue(definition, 0);
        SetPrivateField(route, "routes", routes);
    }

    private static void SetMovementTargets(
        NPCMovementMissionStep step,
        params (string npcId, string routeId)[] targetValues)
    {
        Type targetType = typeof(NPCMovementMissionStep).GetNestedType(
            "MovementTarget",
            BindingFlags.NonPublic);
        Assert.That(targetType, Is.Not.Null);

        FieldInfo npcIdField = targetType.GetField(
            "npcId",
            BindingFlags.Instance | BindingFlags.Public);
        FieldInfo routeIdField = targetType.GetField(
            "routeId",
            BindingFlags.Instance | BindingFlags.Public);
        Array targets = Array.CreateInstance(targetType, targetValues.Length);

        for (int index = 0; index < targetValues.Length; index++)
        {
            object target = Activator.CreateInstance(targetType);
            npcIdField.SetValue(target, targetValues[index].npcId);
            routeIdField.SetValue(target, targetValues[index].routeId);
            targets.SetValue(target, index);
        }

        SetPrivateField(step, "movementTargets", targets);
    }

    private T Track<T>(T createdObject) where T : UnityEngine.Object
    {
        createdObjects.Add(createdObject);
        return createdObject;
    }

    private static void InvokeTrigger(StairsTrigger stairs, string methodName, Collider2D collider)
    {
        MethodInfo method = typeof(StairsTrigger).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(stairs, new object[] { collider });
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Could not find field '{fieldName}'.");
        field.SetValue(target, value);
    }
}
