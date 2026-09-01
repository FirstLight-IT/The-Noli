using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public sealed class RoomArea : MonoBehaviour
{
    public static event Action<string> OnPlayerEntered;

    private static readonly HashSet<RoomArea> ActiveAreas = new();

    [SerializeField] private string roomID;

    private readonly HashSet<Rigidbody2D> playerBodiesInside = new();

    public string RoomID => roomID?.Trim() ?? string.Empty;

    public static bool HasPlayerInside(string targetRoomID)
    {
        if (string.IsNullOrWhiteSpace(targetRoomID))
            return false;

        foreach (RoomArea area in ActiveAreas)
        {
            if (area != null &&
                area.playerBodiesInside.Count > 0 &&
                string.Equals(area.RoomID, targetRoomID.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasActiveArea(string targetRoomID)
    {
        if (string.IsNullOrWhiteSpace(targetRoomID))
            return false;

        foreach (RoomArea area in ActiveAreas)
        {
            if (area != null &&
                string.Equals(area.RoomID, targetRoomID.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void OnEnable()
    {
        ActiveAreas.Add(this);
    }

    private void OnDisable()
    {
        ActiveAreas.Remove(this);
        playerBodiesInside.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryGetPlayerBody(other, out Rigidbody2D playerBody) ||
            !playerBodiesInside.Add(playerBody))
        {
            return;
        }

        OnPlayerEntered?.Invoke(RoomID);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (TryGetPlayerBody(other, out Rigidbody2D playerBody))
            playerBodiesInside.Remove(playerBody);
    }

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnValidate()
    {
        roomID = roomID?.Trim();

        Collider2D areaCollider = GetComponent<Collider2D>();
        if (areaCollider != null)
            areaCollider.isTrigger = true;
    }

    private static bool TryGetPlayerBody(Collider2D other, out Rigidbody2D playerBody)
    {
        playerBody = other != null ? other.attachedRigidbody : null;
        return other != null &&
               !other.isTrigger &&
               playerBody != null &&
               playerBody.CompareTag("Player");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);

        Collider2D areaCollider = GetComponent<Collider2D>();
        if (areaCollider != null)
            Gizmos.DrawWireCube(areaCollider.bounds.center, areaCollider.bounds.size);
    }
}
