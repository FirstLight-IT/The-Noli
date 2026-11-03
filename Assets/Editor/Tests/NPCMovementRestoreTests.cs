using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
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
    public void CompletedMovementStep_AppliesRouteArrivalFacing()
    {
        NPC npc = CreateNpcWithRoute(
            "test_restore_facing",
            "enter_room",
            new Vector2(3f, 4f),
            NPCFacingDirection.North);
        NPCMover mover = npc.GetComponent<NPCMover>();
        NPCFixedRoute route = npc.GetComponent<NPCFixedRoute>();

        Assert.That(mover.RestingFacing, Is.EqualTo(Vector2.down));
        Assert.That(route.TryApplyCompletedRoute("enter_room"), Is.True);
        Assert.That(mover.RestingFacing, Is.EqualTo(Vector2.up));
        Assert.That(mover.CurrentFacing, Is.EqualTo(Vector2.up));
    }

    [Test]
    public void FinishedFixedRoute_AppliesRouteArrivalFacing()
    {
        NPC npc = CreateNpcWithRoute(
            "test_finished_facing",
            "cross_room",
            new Vector2(5f, 2f),
            NPCFacingDirection.West);
        NPCFixedRoute route = npc.GetComponent<NPCFixedRoute>();
        NPCMover mover = npc.GetComponent<NPCMover>();

        Assert.That(route.TryBeginRoute("cross_room"), Is.True);
        InvokePrivate(route, "CompleteRoute");

        Assert.That(route.IsFollowingRoute, Is.False);
        Assert.That(mover.RestingFacing, Is.EqualTo(Vector2.left));
    }

    [Test]
    public void NearbyPlayer_OverridesIdleFacingButRestoresItOnExit()
    {
        GameObject npcObject = Track(new GameObject("Facing NPC"));
        NPCMover mover = InitializeMover(npcObject);
        GameObject player = Track(new GameObject("Player"));
        player.transform.position = new Vector3(2f, 0f, 0f);

        mover.SetRestingFacing(NPCFacingDirection.North);
        Assert.That(mover.CurrentFacing, Is.EqualTo(Vector2.up));

        mover.SetNearbyPlayer(player.transform);
        Assert.That(mover.CurrentFacing, Is.EqualTo(Vector2.right));

        mover.ClearNearbyPlayer(player.transform);
        Assert.That(mover.CurrentFacing, Is.EqualTo(Vector2.up));
    }

    [Test]
    public void NearbyPlayer_SlightlyOffCentreInFrontStillFacesSouth()
    {
        GameObject npcObject = Track(new GameObject("Facing NPC"));
        NPCMover mover = InitializeMover(npcObject);
        GameObject player = Track(new GameObject("Player"));
        player.transform.position = new Vector3(0.45f, -0.9f, 0f);

        mover.SetNearbyPlayer(player.transform);

        Assert.That(mover.CurrentFacing, Is.EqualTo(Vector2.down));
    }

    [Test]
    public void NearbyPlayer_StaysInCurrentDirectionNearSectorBoundary()
    {
        GameObject npcObject = Track(new GameObject("Facing NPC"));
        NPCMover mover = InitializeMover(npcObject);
        GameObject player = Track(new GameObject("Player"));
        player.transform.position = new Vector3(0.5f, 0.75f, 0f);

        mover.SetNearbyPlayer(player.transform);
        Assert.That(mover.CurrentFacing, Is.EqualTo(Vector2.up));

        player.transform.position = new Vector3(0.5f, 0.65f, 0f);
        InvokePrivate(mover, "Update");
        Assert.That(mover.CurrentFacing, Is.EqualTo(Vector2.up));

        player.transform.position = new Vector3(0.85f, 0.5f, 0f);
        InvokePrivate(mover, "Update");
        Assert.That(mover.CurrentFacing, Is.EqualTo(Vector2.one));
    }

    [Test]
    public void WalkingAnimation_UsesOneExactDirectionalClip()
    {
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animators/Player.controller");
        Assert.That(controller, Is.Not.Null);

        GameObject npcObject = Track(new GameObject("Animated NPC"));
        GameObject rendererObject = Track(new GameObject("Renderer"));
        rendererObject.transform.SetParent(npcObject.transform, false);
        SpriteRenderer spriteRenderer = rendererObject.AddComponent<SpriteRenderer>();
        spriteRenderer.flipX = true; // A mirrored scene override used to reverse side-facing sprites.
        Animator animator = rendererObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        NPCMover mover = InitializeMover(npcObject);
        Assert.That(spriteRenderer.flipX, Is.False);

        MethodInfo updateAnimation = typeof(NPCMover).GetMethod(
            "UpdateAnimation", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(updateAnimation, Is.Not.Null);
        updateAnimation.Invoke(mover, new object[] { new Vector2(0.9f, -0.5f) });

        Assert.That(mover.CurrentFacing, Is.EqualTo(new Vector2(1f, -1f)));
        Assert.That(animator.GetBool("isWalking"), Is.True);
        Assert.That(animator.GetFloat("inputX"), Is.EqualTo(1f));
        Assert.That(animator.GetFloat("inputY"), Is.EqualTo(-1f));
        Assert.That(animator.GetFloat("lastInputX"), Is.EqualTo(1f));
        Assert.That(animator.GetFloat("lastInputY"), Is.EqualTo(-1f));

        AnimationClip southEastWalk = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Animators/animations/Ibarra-Walking/PH-walk-SE.anim");
        Assert.That(southEastWalk, Is.Not.Null);
        EditorCurveBinding walkSpriteBinding = AnimationUtility.GetObjectReferenceCurveBindings(southEastWalk)[0];
        Sprite expectedWalkingSprite = AnimationUtility.GetObjectReferenceCurve(southEastWalk, walkSpriteBinding)[0]
            .value as Sprite;
        animator.Update(0f);
        Assert.That(spriteRenderer.sprite, Is.EqualTo(expectedWalkingSprite));
        Assert.That(spriteRenderer.flipX, Is.False);
    }

    [Test]
    public void NearbyPlayer_InFrontSelectsOnlySouthIdleClipAndRestoresDefault()
    {
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animators/Player.controller");
        Assert.That(controller, Is.Not.Null);

        GameObject npcObject = Track(new GameObject("Animated NPC"));
        GameObject rendererObject = Track(new GameObject("Renderer"));
        rendererObject.transform.SetParent(npcObject.transform, false);
        SpriteRenderer spriteRenderer = rendererObject.AddComponent<SpriteRenderer>();
        Animator animator = rendererObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        NPCMover mover = InitializeMover(npcObject);
        mover.SetRestingFacing(NPCFacingDirection.North);

        GameObject player = Track(new GameObject("Player Feet"));
        player.transform.position = new Vector3(0.7f, -0.9f, 0f);
        mover.SetNearbyPlayer(player.transform);

        Assert.That(mover.CurrentFacing, Is.EqualTo(Vector2.down));
        Assert.That(animator.GetBool("isWalking"), Is.False);
        Assert.That(animator.GetFloat("inputX"), Is.Zero);
        Assert.That(animator.GetFloat("inputY"), Is.EqualTo(-1f));
        Assert.That(animator.GetFloat("lastInputX"), Is.Zero);
        Assert.That(animator.GetFloat("lastInputY"), Is.EqualTo(-1f));

        AnimationClip southIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Animators/animations/Ibarra-Idle/PH-Idle-S.anim");
        Assert.That(southIdle, Is.Not.Null);
        EditorCurveBinding spriteBinding = AnimationUtility.GetObjectReferenceCurveBindings(southIdle)[0];
        Sprite expectedSouthSprite = AnimationUtility.GetObjectReferenceCurve(southIdle, spriteBinding)[0]
            .value as Sprite;
        animator.Update(0f);
        Assert.That(spriteRenderer.sprite, Is.EqualTo(expectedSouthSprite));

        mover.ClearNearbyPlayer(player.transform);
        Assert.That(mover.CurrentFacing, Is.EqualTo(Vector2.up));
        Assert.That(animator.GetFloat("lastInputY"), Is.EqualTo(1f));
    }

    [TestCase(2f, 0f, 1f, 0f, "E")]
    [TestCase(-2f, 0f, -1f, 0f, "W")]
    [TestCase(1.4f, 0.8f, 1f, 1f, "NE")]
    [TestCase(-1.4f, 0.8f, -1f, 1f, "NW")]
    [TestCase(1.4f, -0.8f, 1f, -1f, "SE")]
    [TestCase(-1.4f, -0.8f, -1f, -1f, "SW")]
    public void NearbyPlayer_OnSideSelectsMatchingIdleSprite(
        float playerX,
        float playerY,
        float facingX,
        float facingY,
        string clipDirection)
    {
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animators/Player.controller");
        Assert.That(controller, Is.Not.Null);

        GameObject npcObject = Track(new GameObject("Animated NPC"));
        GameObject rendererObject = Track(new GameObject("Renderer"));
        rendererObject.transform.SetParent(npcObject.transform, false);
        SpriteRenderer spriteRenderer = rendererObject.AddComponent<SpriteRenderer>();
        spriteRenderer.flipX = true;
        Animator animator = rendererObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        NPCMover mover = InitializeMover(npcObject);
        Assert.That(spriteRenderer.flipX, Is.False);

        GameObject player = Track(new GameObject("Player Feet"));
        player.transform.position = new Vector3(playerX, playerY, 0f);
        mover.SetNearbyPlayer(player.transform);

        Vector2 expectedFacing = new Vector2(facingX, facingY);
        Assert.That(mover.CurrentFacing, Is.EqualTo(expectedFacing));
        Assert.That(animator.GetFloat("inputX"), Is.EqualTo(facingX));
        Assert.That(animator.GetFloat("inputY"), Is.EqualTo(facingY));
        Assert.That(animator.GetFloat("lastInputX"), Is.EqualTo(facingX));
        Assert.That(animator.GetFloat("lastInputY"), Is.EqualTo(facingY));

        AnimationClip expectedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            $"Assets/Animators/animations/Ibarra-Idle/PH-Idle-{clipDirection}.anim");
        Assert.That(expectedClip, Is.Not.Null);
        EditorCurveBinding spriteBinding = AnimationUtility.GetObjectReferenceCurveBindings(expectedClip)[0];
        Sprite expectedSprite = AnimationUtility.GetObjectReferenceCurve(expectedClip, spriteBinding)[0]
            .value as Sprite;
        animator.Update(0f);
        Assert.That(spriteRenderer.sprite, Is.EqualTo(expectedSprite));
        Assert.That(spriteRenderer.flipX, Is.False);
    }

    [Test]
    public void InteractionDetector_UsesSpriteFeetInsteadOfOffsetDetectorForFacing()
    {
        GameObject npcObject = Track(new GameObject("Facing NPC"));
        NPCMover mover = InitializeMover(npcObject);
        CircleCollider2D npcCollider = npcObject.AddComponent<CircleCollider2D>();

        GameObject player = Track(new GameObject("Player"));
        player.transform.position = new Vector3(0f, 0.5f, 0f);
        player.AddComponent<Rigidbody2D>();
        GameObject spriteFeet = Track(new GameObject("Sprite Feet"));
        spriteFeet.transform.SetParent(player.transform, false);
        spriteFeet.transform.localPosition = new Vector3(0f, -1f, 0f);
        spriteFeet.AddComponent<Animator>();
        GameObject detectorObject = Track(new GameObject("Offset Detector"));
        detectorObject.transform.SetParent(player.transform, false);
        detectorObject.transform.localPosition = new Vector3(0f, 2f, 0f);
        InteractionDetector detector = detectorObject.AddComponent<InteractionDetector>();
        InvokePrivate(detector, "Awake");

        InvokeTrigger(detector, "OnTriggerEnter2D", npcCollider);

        Assert.That(mover.CurrentFacing, Is.EqualTo(Vector2.down));
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

    private NPC CreateNpcWithRoute(
        string npcId,
        string routeId,
        Vector2 destination,
        NPCFacingDirection? arrivalFacing = null)
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
        SetRoute(route, routeId, destinationObject.transform, arrivalFacing);
        InitializeMover(npcObject.GetComponent<NPCMover>());
        InvokePrivate(npc, "Awake");
        InvokePrivate(route, "Awake");
        npcObject.SetActive(true);
        InvokePrivate(npc, "OnEnable");
        InvokePrivate(route, "OnEnable");
        return npc;
    }

    private static NPCMover InitializeMover(GameObject npcObject)
    {
        return InitializeMover(npcObject.AddComponent<NPCMover>());
    }

    private static NPCMover InitializeMover(NPCMover mover)
    {
        // Edit Mode does not invoke ordinary MonoBehaviour Awake callbacks.
        InvokePrivate(mover, "Awake");
        return mover;
    }

    private static void SetRoute(
        NPCFixedRoute route,
        string routeId,
        Transform destination,
        NPCFacingDirection? arrivalFacing = null)
    {
        Type routeType = typeof(NPCFixedRoute).GetNestedType(
            "RouteDefinition",
            BindingFlags.NonPublic);
        Assert.That(routeType, Is.Not.Null);

        object definition = Activator.CreateInstance(routeType);
        SetPrivateField(definition, "routeId", routeId);
        SetPrivateField(definition, "routePoints", new[] { destination });
        if (arrivalFacing.HasValue)
        {
            SetPrivateField(definition, "overrideArrivalFacing", true);
            SetPrivateField(definition, "arrivalFacing", arrivalFacing.Value);
        }

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

    private static void InvokeTrigger(object target, string methodName, Collider2D collider)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(target, new object[] { collider });
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(target, null);
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
