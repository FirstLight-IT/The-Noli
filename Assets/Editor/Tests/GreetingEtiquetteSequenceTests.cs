using System;
using NUnit.Framework;

public sealed class GreetingEtiquetteSequenceTests
{
    [Test]
    public void GetLengthForRound_GrowsFromThreeToSixAcrossFiveRounds()
    {
        int[] expected = { 3, 4, 5, 6, 6 };

        for (int roundIndex = 0; roundIndex < expected.Length; roundIndex++)
        {
            Assert.That(
                GreetingEtiquetteSequence.GetLengthForRound(roundIndex, 5, 3, 6),
                Is.EqualTo(expected[roundIndex]));
        }
    }

    [Test]
    public void Create_ReturnsTheRequestedNumberOfInputs()
    {
        var random = new Random(12345);

        for (int length = 3; length <= 6; length++)
            Assert.That(GreetingEtiquetteSequence.Create(random, length), Has.Count.EqualTo(length));
    }

    [Test]
    public void Create_DoesNotRepeatTheSameInputBackToBack()
    {
        var random = new Random(54321);

        for (int sample = 0; sample < 100; sample++)
        {
            var sequence = GreetingEtiquetteSequence.Create(random, 6);
            for (int index = 1; index < sequence.Count; index++)
                Assert.That(sequence[index], Is.Not.EqualTo(sequence[index - 1]));
        }
    }
}
