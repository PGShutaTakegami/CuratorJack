using System;
using System.Collections.Generic;
using NUnit.Framework;

public class AchievementBookTests
{
    static readonly DateTime Now = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Local);

    static AchievementBook Create(AchievementProgress progress = null)
    {
        var definitions = new List<AchievementDefinition>
        {
            new AchievementDefinition("normal", false),
            new AchievementDefinition("secret", true),
        };
        return new AchievementBook(definitions, progress ?? new AchievementProgress(), () => Now);
    }

    [Test]
    public void Unlock_NewAchievement_RaisesEventOnceAndRecordsTime()
    {
        AchievementBook book = Create();
        var raised = new List<string>();
        book.Unlocked += definition => raised.Add(definition.Id);

        Assert.IsTrue(book.Unlock("normal"));
        Assert.IsFalse(book.Unlock("normal"));

        CollectionAssert.AreEqual(new[] { "normal" }, raised);
        Assert.IsTrue(book.TryGetUnlockedAt("normal", out DateTime unlockedAt));
        Assert.AreEqual(Now, unlockedAt);
        Assert.AreEqual(1, book.UnlockedCount);
    }

    [Test]
    public void Unlock_UnknownId_IsIgnored()
    {
        AchievementBook book = Create();
        bool raised = false;
        book.Unlocked += _ => raised = true;

        Assert.IsFalse(book.Unlock("missing"));
        Assert.IsFalse(raised);
        Assert.AreEqual(0, book.UnlockedCount);
    }

    [Test]
    public void Progress_JsonRoundTrip_KeepsUnlockedAchievements()
    {
        AchievementBook book = Create();
        book.Unlock("secret");

        AchievementBook restored = Create(AchievementProgress.FromJson(book.Progress.ToJson()));

        Assert.IsTrue(restored.IsUnlocked("secret"));
        Assert.IsFalse(restored.IsUnlocked("normal"));
        Assert.IsTrue(restored.TryGetUnlockedAt("secret", out DateTime unlockedAt));
        Assert.AreEqual(Now, unlockedAt);
    }

    [Test]
    public void Progress_FromBrokenJson_StartsEmpty()
    {
        Assert.IsFalse(AchievementProgress.FromJson("{not json").IsUnlocked("normal"));
        Assert.IsFalse(AchievementProgress.FromJson(string.Empty).IsUnlocked("normal"));
    }

    [Test]
    public void Progress_StoresUnlockTimeAsUnixSeconds()
    {
        var progress = new AchievementProgress();
        progress.TryUnlock("normal", Now);

        StringAssert.Contains($"\"unlockedAtUnix\":{new DateTimeOffset(Now).ToUnixTimeSeconds()}", progress.ToJson());
    }

    [Test]
    public void SteamApiName_FallsBackToIdWhenEmpty()
    {
        Assert.AreEqual("ACH_FIRST_WIN", new AchievementDefinition("first_win", false).SteamApiName);
        Assert.AreEqual("CUSTOM_NAME", new AchievementDefinition("first_win", false, "CUSTOM_NAME").SteamApiName);
    }

    [Test]
    public void PlatformRelay_SendsPastUnlocksOnRegisterAndForwardsNewOnes()
    {
        AchievementBook book = Create();
        var relay = new AchievementPlatformRelay();
        relay.Bind(book);
        book.Unlock("secret");

        var platform = new RecordingPlatform();
        relay.Register(platform);
        CollectionAssert.AreEqual(new[] { "ACH_SECRET" }, platform.Unlocked);

        book.Unlock("normal");
        CollectionAssert.AreEqual(new[] { "ACH_SECRET", "ACH_NORMAL" }, platform.Unlocked);
    }

    sealed class RecordingPlatform : IAchievementPlatform
    {
        public readonly List<string> Unlocked = new List<string>();

        public void Unlock(AchievementDefinition definition) => Unlocked.Add(definition.SteamApiName);
    }
}
