using NUnit.Framework;

public sealed class ChapterWorldStateTests
{
    [Test]
    public void WorldFlags_AreAttemptScopedDeduplicatedAndNormalized()
    {
        ChapterSaveData chapter = new();

        Assert.That(chapter.AddWorldFlag(" npc_departed:tia_isabel "), Is.True);
        Assert.That(chapter.AddWorldFlag("npc_departed:tia_isabel"), Is.False);
        Assert.That(chapter.HasWorldFlag("npc_departed:tia_isabel"), Is.True);

        chapter.worldFlags.Add("npc_departed:tia_isabel");
        chapter.worldFlags.Add(string.Empty);
        chapter.Normalize();

        Assert.That(
            chapter.worldFlags,
            Is.EqualTo(new[] { "npc_departed:tia_isabel" }));
    }
}
