using FluentAssertions;
using Portfolio.Domain.Models;
using Portfolio.Services;
using Portfolio.Storage;
using Xunit;
using static Portfolio.Test.TestData;

namespace Portfolio.Test.Integration;

/// <summary>
/// The monthly routine: export on one device, import on the other. Each test is two devices handing
/// a file across, and checks what the import is about to discard is noticed before it is.
/// </summary>
public sealed class BackupSyncTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan AMonth = TimeSpan.FromDays(30);

    private static PortfolioSnapshot Demo() => DemoData.CreateSnapshot(On(2026, 1, 1));

    private static Task AddEntryAsync(TestDevice device, decimal deposit) =>
        device.Store.AddEntryAsync(
            new Entry(0, device.Store.FinancialItems[0].Id, On(2026, 9, 15), deposit: deposit)
        );

    [Fact]
    public async Task A_backup_moves_the_whole_portfolio_to_an_empty_device()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var android = await TestDevice.StartAsync("Android", clock);

        var review = android.Backup.Review(await desktop.ExportAsync());

        review.LocalChanges.Should().Be(UnsyncedChanges.None, "an empty device has nothing to lose");
        review.NeedsCaution.Should().BeFalse();

        await android.Backup.ImportAsync(review.Backup);

        android.Fingerprint.Should().Be(desktop.Fingerprint);
        android.Backup.LastSync!.Direction.Should().Be(SyncDirection.Imported);
        android.Backup.LastSync.SourceDevice.Should().Be("Desktop");
        android.Backup.LastSync.SourceExportedAt.Should().Be(Start);

        // And it outlives a restart: a fresh repository over the same storage loads the same data.
        var reloaded = await new JsonPortfolioRepository(android.PortfolioBlob).GetPlatformsAsync();
        PortfolioTransfer.Fingerprint(reloaded).Should().Be(desktop.Fingerprint);
    }

    [Fact]
    public async Task Next_month_an_untouched_device_takes_the_new_backup_without_a_warning()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var android = await TestDevice.StartAsync("Android", clock);
        await android.Backup.ImportAsync(android.Backup.Review(await desktop.ExportAsync()).Backup);

        clock.Advance(AMonth);
        await AddEntryAsync(desktop, 250);
        var review = android.Backup.Review(await desktop.ExportAsync());

        review.IsAlreadyCurrent.Should().BeFalse();
        review.NeedsCaution.Should().BeFalse("the phone was not edited since it last synced");
    }

    [Fact]
    public async Task Changes_made_since_the_last_sync_are_flagged_before_they_are_discarded()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var android = await TestDevice.StartAsync("Android", clock);
        await android.Backup.ImportAsync(android.Backup.Review(await desktop.ExportAsync()).Backup);

        clock.Advance(AMonth);
        await AddEntryAsync(android, 99);
        await AddEntryAsync(desktop, 250);
        var review = android.Backup.Review(await desktop.ExportAsync());

        review.LocalChanges.Should().Be(UnsyncedChanges.SinceLastSync);
        review.LastSync!.At.Should().Be(Start);
        review.NeedsCaution.Should().BeTrue();
    }

    [Fact]
    public async Task Data_that_was_never_exported_is_flagged()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var android = await TestDevice.StartAsync("Android", clock, Demo());
        await AddEntryAsync(android, 10);

        var review = desktop.Backup.Review(await android.ExportAsync());

        review.LocalChanges.Should().Be(UnsyncedChanges.NeverSynced);
    }

    [Fact]
    public async Task A_backup_older_than_the_last_one_imported_is_flagged()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var android = await TestDevice.StartAsync("Android", clock);

        var september = await desktop.ExportAsync();
        clock.Advance(AMonth);
        await AddEntryAsync(desktop, 250);
        var october = await desktop.ExportAsync();
        await android.Backup.ImportAsync(android.Backup.Review(october).Backup);

        var review = android.Backup.Review(september);

        review.IsOlderThanLastSync.Should().BeTrue();
        review.LocalChanges.Should().Be(UnsyncedChanges.None);
        review.NeedsCaution.Should().BeTrue();
    }

    [Fact]
    public async Task A_backup_older_than_this_devices_own_last_export_is_flagged()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var android = await TestDevice.StartAsync("Android", clock);

        // September: the phone takes the desktop's data, then becomes the device being edited.
        await android.Backup.ImportAsync(android.Backup.Review(await desktop.ExportAsync()).Backup);
        clock.Advance(AMonth);
        await AddEntryAsync(android, 40);
        var phoneFile = await android.ExportAsync();
        await desktop.Backup.ImportAsync(desktop.Backup.Review(phoneFile).Backup);

        // November: the desktop is edited and exported again. The phone's October file is now stale.
        clock.Advance(AMonth);
        await AddEntryAsync(desktop, 300);
        await desktop.ExportAsync();

        var review = desktop.Backup.Review(phoneFile);

        review.LocalChanges.Should().Be(UnsyncedChanges.None, "the desktop has not changed since its export");
        review.IsOlderThanLastSync.Should().BeTrue("importing it would wind the desktop back");
    }

    [Fact]
    public async Task The_same_backup_twice_is_recognised_as_nothing_to_do()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var android = await TestDevice.StartAsync("Android", clock);
        var file = await desktop.ExportAsync();
        await android.Backup.ImportAsync(android.Backup.Review(file).Backup);

        android.Backup.Review(file).IsAlreadyCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task An_import_can_be_undone_and_the_undo_redone()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var android = await TestDevice.StartAsync("Android", clock, Demo());
        await AddEntryAsync(android, 10);
        var phoneFile = await android.ExportAsync();
        var before = android.Fingerprint;
        var syncBefore = android.Backup.LastSync;

        clock.Advance(TimeSpan.FromHours(1));
        await AddEntryAsync(desktop, 500);
        await android.Backup.ImportAsync(android.Backup.Review(await desktop.ExportAsync()).Backup);
        var imported = android.Fingerprint;

        await android.Backup.RestoreAsync();

        android.Fingerprint.Should().Be(before);
        android.Backup.LastSync.Should().BeEquivalentTo(syncBefore, "the undo puts the sync record back too");
        PortfolioTransfer.Fingerprint(android.Backup.RestorePoint!.Snapshot).Should().Be(imported);

        await android.Backup.RestoreAsync();

        android.Fingerprint.Should().Be(imported, "restoring again redoes the import");
        android.Backup.Review(phoneFile).IsAlreadyCurrent.Should().BeFalse();
    }

    [Fact]
    public async Task A_failed_import_changes_nothing()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var android = await TestDevice.StartAsync("Android", clock);
        await android.Store.AddPlatformAsync(new Platform(0, "Phone only"));
        var before = android.Fingerprint;
        var review = android.Backup.Review(await desktop.ExportAsync());

        android.PortfolioBlob.FailWrites = true;
        var import = () => android.Backup.ImportAsync(review.Backup);

        (await import.Should().ThrowAsync<BackupFailedException>()).WithMessage("Nothing was changed*");
        android.Fingerprint.Should().Be(before);
        android.Backup.LastSync.Should().BeNull("an import that did not happen is not recorded");

        android.PortfolioBlob.FailWrites = false;
        var reloaded = await new JsonPortfolioRepository(android.PortfolioBlob).GetPlatformsAsync();
        PortfolioTransfer.Fingerprint(reloaded).Should().Be(before);
    }

    [Fact]
    public async Task Without_a_restore_point_there_is_no_import()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var android = await TestDevice.StartAsync("Android", clock);
        await android.Store.AddPlatformAsync(new Platform(0, "Phone only"));
        var before = android.Fingerprint;
        var review = android.Backup.Review(await desktop.ExportAsync());

        android.Storage.Blob("restore-point.json").FailWrites = true;
        var import = () => android.Backup.ImportAsync(review.Backup);

        (await import.Should().ThrowAsync<BackupFailedException>()).WithMessage("*restore point*");
        android.Fingerprint.Should().Be(before);
    }

    [Fact]
    public async Task The_sync_record_and_restore_point_outlive_a_restart()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var android = await TestDevice.StartAsync("Android", clock);
        await android.Backup.ImportAsync(android.Backup.Review(await desktop.ExportAsync()).Backup);

        var restarted = new BackupService(android.Store, android.Storage, clock);
        await restarted.LoadAsync();

        restarted.LastSync.Should().BeEquivalentTo(android.Backup.LastSync);
        restarted.RestorePoint.Should().NotBeNull();
        restarted.RestorePoint!.Snapshot.Platforms.Should().BeEmpty("the phone was empty before");
    }

    [Fact]
    public async Task A_damaged_sync_record_costs_a_warning_not_the_app()
    {
        var clock = new TestClock(Start);
        var android = await TestDevice.StartAsync("Android", clock);
        await android.Storage.Open("sync.json").WriteAsync("{ not json");

        var restarted = new BackupService(android.Store, android.Storage, clock);
        var load = () => restarted.LoadAsync();

        await load.Should().NotThrowAsync();
        restarted.LastSync.Should().BeNull();
    }
}
