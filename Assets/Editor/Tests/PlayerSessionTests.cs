using NUnit.Framework;

public sealed class PlayerSessionTests
{
    [SetUp]
    public void SetUp()
    {
        PlayerSession.ReturnToGuest();
    }

    [TearDown]
    public void TearDown()
    {
        PlayerSession.ReturnToGuest();
    }

    [Test]
    public void NoSignedInAccount_IsAnOfflineGuest()
    {
        Assert.That(PlayerSession.IsGuest, Is.True);
        Assert.That(PlayerSession.IsSignedIn, Is.False);
        Assert.That(PlayerSession.AccountId, Is.Empty);
        Assert.That(PlayerSession.CanUseOnlineAccountFeatures, Is.False);
        Assert.That(PlayerSession.CanSubmitGlobalAnalytics, Is.False);
    }

    [Test]
    public void PendingTeacher_ReceivesPlayerAccess()
    {
        AccountProfile teacher = CreateProfile(AccountRole.Teacher);
        teacher.teacherVerificationStatus = TeacherVerificationStatus.Pending;

        Assert.That(
            PlayerSession.TryBeginAccountSession(teacher, out string error),
            Is.True,
            error);
        Assert.That(PlayerSession.EffectiveRole, Is.EqualTo(AccountRole.Player));
    }

    [Test]
    public void VerifiedTeacher_ReceivesTeacherAccess()
    {
        AccountProfile teacher = CreateProfile(AccountRole.Teacher);
        teacher.teacherVerificationStatus = TeacherVerificationStatus.Verified;

        Assert.That(
            PlayerSession.TryBeginAccountSession(teacher, out string error),
            Is.True,
            error);
        Assert.That(PlayerSession.EffectiveRole, Is.EqualTo(AccountRole.Teacher));
    }

    [Test]
    public void SessionRequiresAPermanentAccountId()
    {
        AccountProfile profile = CreateProfile(AccountRole.Player);
        profile.accountId = string.Empty;

        Assert.That(
            PlayerSession.TryBeginAccountSession(profile, out string error),
            Is.False);
        Assert.That(error, Does.Contain("Account ID"));
        Assert.That(PlayerSession.IsGuest, Is.True);
    }

    [Test]
    public void SigningOutReturnsToGuest()
    {
        Assert.That(
            PlayerSession.TryBeginAccountSession(
                CreateProfile(AccountRole.Player),
                out string error),
            Is.True,
            error);

        PlayerSession.ReturnToGuest();

        Assert.That(PlayerSession.IsGuest, Is.True);
        Assert.That(PlayerSession.CurrentAccount, Is.Null);
    }

    [Test]
    public void CurrentSaveDirectoryChangesWithTheSessionAccountId()
    {
        string persistentDataPath = "device-data";
        string guestDirectory = SaveStorageScope.GetCurrentSaveDirectory(persistentDataPath);

        Assert.That(
            PlayerSession.TryBeginAccountSession(
                CreateProfile(AccountRole.Player),
                out string error),
            Is.True,
            error);
        string accountDirectory = SaveStorageScope.GetCurrentSaveDirectory(persistentDataPath);

        Assert.That(accountDirectory, Is.Not.EqualTo(guestDirectory));
        Assert.That(accountDirectory, Does.Contain(
            System.IO.Path.Combine("Saves", "Accounts")));
    }

    private static AccountProfile CreateProfile(AccountRole role)
    {
        return new AccountProfile
        {
            accountId = "account-123",
            username = "meep",
            inGameName = "Meep",
            role = role
        };
    }
}
