using System;
using System.Collections.Generic;

public enum RhythmInput
{
    Up,
    Down,
    Left,
    Right,
    Q,
    E
}

public static class SocialInterruptionPromptSequence
{
    private static readonly RhythmInput[] Inputs =
    {
        RhythmInput.Up,
        RhythmInput.Down,
        RhythmInput.Left,
        RhythmInput.Right,
        RhythmInput.Q,
        RhythmInput.E
    };

    public static List<RhythmInput> Create(Random random, int minimumLength = 2, int maximumLength = 3)
    {
        if (random == null)
            throw new ArgumentNullException(nameof(random));
        if (minimumLength < 1 || maximumLength < minimumLength)
            throw new ArgumentOutOfRangeException(nameof(minimumLength));

        int length = random.Next(minimumLength, maximumLength + 1);
        var sequence = new List<RhythmInput>(length);
        RhythmInput? previous = null;

        for (int index = 0; index < length; index++)
        {
            RhythmInput next;
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

    public static bool IsExactChord(
        RhythmInput firstExpected,
        RhythmInput secondExpected,
        ICollection<RhythmInput> uniquePressedInputs,
        int physicalPressCount)
    {
        return uniquePressedInputs != null &&
               physicalPressCount == 2 &&
               uniquePressedInputs.Count == 2 &&
               uniquePressedInputs.Contains(firstExpected) &&
               uniquePressedInputs.Contains(secondExpected);
    }
}
