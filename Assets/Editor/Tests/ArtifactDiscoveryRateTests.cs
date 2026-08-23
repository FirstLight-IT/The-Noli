using NUnit.Framework;

public sealed class ArtifactDiscoveryRateTests
{
    [Test]
    public void AvailableArtifactTotalAllowsAZeroPercentDiscoveryRate()
    {
        ChapterAnalyticsSaveData analytics = new();

        analytics.SetArtifactsAvailable(25);

        Assert.That(analytics.artifactsUnlocked, Is.Zero);
        Assert.That(analytics.artifactsAvailable, Is.EqualTo(25));
        Assert.That(analytics.artifactDiscoveryRatePercent, Is.Zero);
    }

    [Test]
    public void RecordArtifactDiscovery_CalculatesPercentageFromAvailableArtifacts()
    {
        ChapterAnalyticsSaveData analytics = new();

        analytics.RecordArtifactDiscovery(25);
        analytics.RecordArtifactDiscovery(25);

        Assert.That(analytics.artifactsUnlocked, Is.EqualTo(2));
        Assert.That(analytics.artifactsAvailable, Is.EqualTo(25));
        Assert.That(analytics.artifactDiscoveryRatePercent, Is.EqualTo(8d));
    }

    [Test]
    public void Normalize_RecalculatesAndCapsArtifactDiscoveryRate()
    {
        ChapterAnalyticsSaveData analytics = new()
        {
            artifactsUnlocked = 30,
            artifactsAvailable = 25,
            artifactDiscoveryRatePercent = -1d
        };

        analytics.Normalize();

        Assert.That(analytics.artifactDiscoveryRatePercent, Is.EqualTo(100d));
    }

    [Test]
    public void RecordArtifactDiscovery_WithUnknownTotalKeepsRateAtZero()
    {
        ChapterAnalyticsSaveData analytics = new();

        analytics.RecordArtifactDiscovery(0);

        Assert.That(analytics.artifactsUnlocked, Is.EqualTo(1));
        Assert.That(analytics.artifactDiscoveryRatePercent, Is.Zero);
    }
}
