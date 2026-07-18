using System.Collections.Generic;
using UnityEngine;

public interface IIsometricSortable
{
    MonoBehaviour SortBehaviour { get; }
    SpriteRenderer SortRenderer { get; }
    Vector2 SortAnchor { get; }
    int NaturalSortOrder { get; }
    bool DefinesSortBoundary { get; }
    bool TryGetOtherInFront(Vector2 otherAnchor, out bool otherIsInFront);
}

public static class IsometricSortingSystem
{
    private static readonly HashSet<IIsometricSortable> Participants = new();
    private static readonly List<IIsometricSortable> ActiveParticipants = new();
    private static int lastSortedFrame = -1;

    public static void Register(IIsometricSortable participant)
    {
        Participants.Add(participant);
        SortAll(true);
    }

    public static void Unregister(IIsometricSortable participant)
    {
        Participants.Remove(participant);
        SortAll(true);
    }

    public static void SortAll(bool force = false)
    {
        if (!force && lastSortedFrame == Time.frameCount)
        {
            return;
        }

        lastSortedFrame = Time.frameCount;
        ActiveParticipants.Clear();

        foreach (IIsometricSortable participant in Participants)
        {
            if (participant?.SortBehaviour != null &&
                participant.SortBehaviour.isActiveAndEnabled &&
                participant.SortRenderer != null)
            {
                ActiveParticipants.Add(participant);
            }
        }

        ActiveParticipants.Sort(CompareStableBaseline);

        HashSet<int> sortingLayerIDs = new();
        foreach (IIsometricSortable participant in ActiveParticipants)
        {
            sortingLayerIDs.Add(participant.SortRenderer.sortingLayerID);
        }

        foreach (int sortingLayerID in sortingLayerIDs)
        {
            SortLayer(sortingLayerID);
        }
    }

    private static void SortLayer(int sortingLayerID)
    {
        List<IIsometricSortable> nodes = ActiveParticipants.FindAll(
            participant => participant.SortRenderer.sortingLayerID == sortingLayerID);

        int count = nodes.Count;
        List<int>[] outgoingEdges = new List<int>[count];
        int[] incomingEdgeCounts = new int[count];

        for (int i = 0; i < count; i++)
        {
            outgoingEdges[i] = new List<int>();
        }

        for (int first = 0; first < count; first++)
        {
            for (int second = first + 1; second < count; second++)
            {
                bool secondIsInFront = IsSecondInFront(nodes[first], nodes[second]);
                int behind = secondIsInFront ? first : second;
                int inFront = secondIsInFront ? second : first;
                outgoingEdges[behind].Add(inFront);
                incomingEdgeCounts[inFront]++;
            }
        }

        List<int> backToFront = new(count);
        bool[] added = new bool[count];

        while (backToFront.Count < count)
        {
            int next = FindBackmostAvailable(nodes, incomingEdgeCounts, added, true);

            // Conflicting boundaries can form a cycle. Break it deterministically by
            // taking the remaining participant with the highest baseline anchor.
            if (next < 0)
            {
                next = FindBackmostAvailable(nodes, incomingEdgeCounts, added, false);
            }

            added[next] = true;
            backToFront.Add(next);

            foreach (int destination in outgoingEdges[next])
            {
                incomingEdgeCounts[destination]--;
            }
        }

        int previousOrder = int.MinValue;
        foreach (int nodeIndex in backToFront)
        {
            IIsometricSortable participant = nodes[nodeIndex];
            int naturalOrder = participant.NaturalSortOrder;
            int assignedOrder = previousOrder == int.MinValue
                ? naturalOrder
                : Mathf.Max(naturalOrder, previousOrder + 1);

            participant.SortRenderer.sortingOrder = assignedOrder;
            previousOrder = assignedOrder;
        }
    }

    private static bool IsSecondInFront(
        IIsometricSortable first,
        IIsometricSortable second)
    {
        if (first.DefinesSortBoundary)
        {
            if (first.TryGetOtherInFront(second.SortAnchor, out bool secondIsInFront))
            {
                return secondIsInFront;
            }
        }

        if (second.DefinesSortBoundary)
        {
            if (second.TryGetOtherInFront(first.SortAnchor, out bool firstIsInFront))
            {
                return !firstIsInFront;
            }
        }

        if (!Mathf.Approximately(first.SortAnchor.y, second.SortAnchor.y))
        {
            return second.SortAnchor.y < first.SortAnchor.y;
        }

        return second.SortBehaviour.GetHashCode() > first.SortBehaviour.GetHashCode();
    }

    private static int FindBackmostAvailable(
        List<IIsometricSortable> nodes,
        int[] incomingEdgeCounts,
        bool[] added,
        bool requireNoIncomingEdges)
    {
        int result = -1;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (added[i] || (requireNoIncomingEdges && incomingEdgeCounts[i] != 0))
            {
                continue;
            }

            if (result < 0 || CompareStableBaseline(nodes[i], nodes[result]) < 0)
            {
                result = i;
            }
        }

        return result;
    }

    private static int CompareStableBaseline(IIsometricSortable first, IIsometricSortable second)
    {
        int yComparison = second.SortAnchor.y.CompareTo(first.SortAnchor.y);
        return yComparison != 0
            ? yComparison
            : first.SortBehaviour.GetHashCode().CompareTo(second.SortBehaviour.GetHashCode());
    }
}
