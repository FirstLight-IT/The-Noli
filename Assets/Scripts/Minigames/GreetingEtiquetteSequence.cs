using System;
using System.Collections.Generic;

public enum GreetingEtiquetteInput
{
    Up,
    Down,
    Left,
    Right,
    Q,
    E
}

public static class GreetingEtiquetteSequence
{
    private static readonly GreetingEtiquetteInput[] Inputs =
    {
        GreetingEtiquetteInput.Up,
        GreetingEtiquetteInput.Down,
        GreetingEtiquetteInput.Left,
        GreetingEtiquetteInput.Right,
        GreetingEtiquetteInput.Q,
        GreetingEtiquetteInput.E
    };

    public static List<GreetingEtiquetteInput> Create(Random random, int length)
    {
        if (random == null)
            throw new ArgumentNullException(nameof(random));
        if (length < 1)
            throw new ArgumentOutOfRangeException(nameof(length));

        var sequence = new List<GreetingEtiquetteInput>(length);
        GreetingEtiquetteInput? previous = null;
        for (int index = 0; index < length; index++)
        {
            GreetingEtiquetteInput next;
            do
            {
                next = Inputs[random.Next(Inputs.Length)];
            }
            while (previous.HasValue && next == previous.Value);

            sequence.Add(next);
            previous = next;
        }

        return sequence;
    }

    public static int GetLengthForRound(int roundIndex, int totalRounds, int minimumLength, int maximumLength)
    {
        if (totalRounds < 1)
            throw new ArgumentOutOfRangeException(nameof(totalRounds));
        if (roundIndex < 0 || roundIndex >= totalRounds)
            throw new ArgumentOutOfRangeException(nameof(roundIndex));
        if (minimumLength < 1 || maximumLength < minimumLength)
            throw new ArgumentOutOfRangeException(nameof(minimumLength));

        return Math.Min(minimumLength + roundIndex, maximumLength);
    }
}
