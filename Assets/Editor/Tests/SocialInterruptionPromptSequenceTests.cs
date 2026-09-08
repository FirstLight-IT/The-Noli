using System;
using NUnit.Framework;

public sealed class SocialInterruptionPromptSequenceTests
{
    [Test]
    public void Create_AlwaysBuildsTwoOrThreeInputs()
    {
        var random = new Random(12345);

        for (int index = 0; index < 100; index++)
        {
            int count = SocialInterruptionPromptSequence.Create(random).Count;
            Assert.That(count, Is.InRange(2, 3));
        }
    }

    [Test]
    public void Create_DoesNotRepeatTheSameInputBackToBack()
    {
        var random = new Random(54321);

        for (int sample = 0; sample < 100; sample++)
        {
            var sequence = SocialInterruptionPromptSequence.Create(random);
            for (int index = 1; index < sequence.Count; index++)
                Assert.That(sequence[index], Is.Not.EqualTo(sequence[index - 1]));
        }
    }

    [Test]
    public void Create_WhenFixedToTwo_AlwaysBuildsAKeyPair()
    {
        var random = new Random(24680);

        for (int index = 0; index < 100; index++)
            Assert.That(SocialInterruptionPromptSequence.Create(random, 2, 2), Has.Count.EqualTo(2));
    }

    [Test]
    public void IsExactChord_WithOnlyTheTwoExpectedInputs_ReturnsTrue()
    {
        var pressed = new System.Collections.Generic.HashSet<RhythmInput>
        {
            RhythmInput.Left,
            RhythmInput.Q
        };

        Assert.That(
            SocialInterruptionPromptSequence.IsExactChord(
                RhythmInput.Left, RhythmInput.Q, pressed, 2),
            Is.True);
    }

    [Test]
    public void IsExactChord_WithAnAdditionalPhysicalKey_ReturnsFalse()
    {
        var pressed = new System.Collections.Generic.HashSet<RhythmInput>
        {
            RhythmInput.Left,
            RhythmInput.Q
        };

        Assert.That(
            SocialInterruptionPromptSequence.IsExactChord(
                RhythmInput.Left, RhythmInput.Q, pressed, 3),
            Is.False);
    }
}
