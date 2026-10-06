using FluentAssertions;
using MVVM.Core.Layout;
using MVVM.Core.Stores;
using Portfolio.Composition;
using Portfolio.Domain.Models;
using Portfolio.Features.Analysis;
using Portfolio.Features.Backup;
using Portfolio.Storage;
using Xunit;
using static Portfolio.Test.TestData;

namespace Portfolio.Test.Unit;

/// <summary>
/// The backup screen: picking a file only ever reviews it, cancelling anywhere changes nothing, and
/// every outcome is said out loud.
/// </summary>
public sealed class BackupViewModelTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private static (BackupViewModel Screen, FakeBackupFiles Files) Open(
        TestDevice device,
        LayoutStore? layout = null
    )
    {
        layout ??= new LayoutStore();
        var files = new FakeBackupFiles();
        var navigator = new AppNavigator(
            device.Store,
            layout,
            new NavigationStore(),
            new ModalNavigationStore(),
            device.Backup,
            files
        );

        return (new BackupViewModel(device.Store, layout, navigator, device.Backup, files), files);
    }

    private static PortfolioSnapshot Demo() => DemoData.CreateSnapshot(On(2026, 1, 1));

    [Fact]
    public async Task Exporting_saves_a_file_and_records_the_sync()
    {
        var device = await TestDevice.StartAsync("Desktop", new TestClock(Start), Demo());
        var (screen, files) = Open(device);

        await screen.ExportCommand.ExecuteAsync(null);

        files.SavedName.Should().Be("portfolio-2026-09-01-desktop.json");
        PortfolioTransfer.Read(files.SavedJson).Fingerprint.Should().Be(device.Fingerprint);
        device.Backup.LastSync!.Direction.Should().Be(SyncDirection.Exported);
        screen.StatusMessage.Should().Contain("portfolio-2026-09-01-desktop.json");
        screen.SyncStatus.Should().StartWith("Last exported");
        screen.HasUnsyncedChanges.Should().BeFalse();
    }

    [Fact]
    public async Task A_cancelled_export_records_nothing()
    {
        var device = await TestDevice.StartAsync("Desktop", new TestClock(Start), Demo());
        var (screen, files) = Open(device);
        files.CancelSave = true;

        await screen.ExportCommand.ExecuteAsync(null);

        device.Backup.LastSync.Should().BeNull();
        screen.StatusMessage.Should().BeNull();
        screen.ErrorMessage.Should().BeNull();
        screen.HasUnsyncedChanges.Should().BeTrue("the data still exists in no file");
    }

    [Fact]
    public async Task Picking_a_file_only_reviews_it_and_confirming_imports_it()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var phone = await TestDevice.StartAsync("Android", clock);
        await phone.Store.AddPlatformAsync(new Platform(0, "Phone only"));
        var before = phone.Fingerprint;
        var (screen, files) = Open(phone);
        files.FileToOpen = await desktop.ExportAsync();

        await screen.ImportCommand.ExecuteAsync(null);

        phone.Fingerprint.Should().Be(before, "nothing changes until the review is confirmed");
        screen.IsReviewing.Should().BeTrue();
        screen.ReviewSource.Should().Contain("Desktop");
        screen.HasReviewWarnings.Should().BeTrue("the phone's data was never exported");
        screen.CanExportFirst.Should().BeTrue();
        screen.ConfirmImportText.Should().Be("Replace anyway");

        await screen.ConfirmImportCommand.ExecuteAsync(null);

        phone.Fingerprint.Should().Be(desktop.Fingerprint);
        screen.IsReviewing.Should().BeFalse();
        screen.StatusMessage.Should().StartWith("Imported").And.Contain("from Desktop");
        screen.SyncStatus.Should().StartWith("Last imported").And.Contain("from Desktop's backup of");
        screen.ShowSyncNote.Should().BeTrue();
        screen.ShowSyncWarning.Should().BeFalse();
        screen.HasRestorePoint.Should().BeTrue();
    }

    [Fact]
    public async Task Cancelling_the_review_changes_nothing()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var phone = await TestDevice.StartAsync("Android", clock);
        var (screen, files) = Open(phone);
        files.FileToOpen = await desktop.ExportAsync();

        await screen.ImportCommand.ExecuteAsync(null);
        screen.CancelImportCommand.Execute(null);

        screen.IsReviewing.Should().BeFalse();
        phone.Store.Platforms.Should().BeEmpty();
        phone.Backup.LastSync.Should().BeNull();
    }

    [Fact]
    public async Task Export_first_then_import_keeps_a_file_of_what_is_replaced()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var phone = await TestDevice.StartAsync("Android", clock);
        await phone.Store.AddPlatformAsync(new Platform(0, "Phone only"));
        var before = phone.Fingerprint;
        var (screen, files) = Open(phone);
        files.FileToOpen = await desktop.ExportAsync();
        await screen.ImportCommand.ExecuteAsync(null);

        await screen.ExportThenImportCommand.ExecuteAsync(null);

        PortfolioTransfer.Read(files.SavedJson).Fingerprint.Should().Be(before);
        phone.Fingerprint.Should().Be(desktop.Fingerprint);
        screen.StatusMessage.Should().StartWith("Saved this device's data");
    }

    [Fact]
    public async Task Cancelling_the_export_first_cancels_the_import_too()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var phone = await TestDevice.StartAsync("Android", clock);
        await phone.Store.AddPlatformAsync(new Platform(0, "Phone only"));
        var before = phone.Fingerprint;
        var (screen, files) = Open(phone);
        files.FileToOpen = await desktop.ExportAsync();
        await screen.ImportCommand.ExecuteAsync(null);

        files.CancelSave = true;
        await screen.ExportThenImportCommand.ExecuteAsync(null);

        phone.Fingerprint.Should().Be(before);
        screen.IsReviewing.Should().BeTrue("the review stays open to decide again");
    }

    [Theory]
    [InlineData("{}", "not a Portfolio backup")]
    [InlineData("{ \"Version\": 99, \"Platforms\": [] }", "newer version")]
    public async Task A_file_that_cannot_be_imported_is_reported_and_changes_nothing(
        string file,
        string reason
    )
    {
        var device = await TestDevice.StartAsync("Android", new TestClock(Start), Demo());
        var before = device.Fingerprint;
        var (screen, files) = Open(device);
        files.FileToOpen = file;

        await screen.ImportCommand.ExecuteAsync(null);

        screen.ErrorMessage.Should().Contain(reason).And.EndWith("Nothing on this device was changed.");
        screen.IsReviewing.Should().BeFalse();
        device.Fingerprint.Should().Be(before);
    }

    [Fact]
    public async Task The_same_backup_again_says_there_is_nothing_to_import()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var phone = await TestDevice.StartAsync("Android", clock);
        var file = await desktop.ExportAsync();
        await phone.Backup.ImportAsync(phone.Backup.Review(file).Backup);
        var (screen, files) = Open(phone);
        files.FileToOpen = file;

        await screen.ImportCommand.ExecuteAsync(null);

        screen.IsReviewing.Should().BeFalse();
        screen.StatusMessage.Should().Contain("Nothing to import");
    }

    [Fact]
    public async Task The_restore_point_is_offered_and_puts_the_old_data_back()
    {
        var clock = new TestClock(Start);
        var desktop = await TestDevice.StartAsync("Desktop", clock, Demo());
        var phone = await TestDevice.StartAsync("Android", clock);
        await phone.Store.AddPlatformAsync(new Platform(0, "Phone only"));
        var before = phone.Fingerprint;
        await phone.Backup.ImportAsync(phone.Backup.Review(await desktop.ExportAsync()).Backup);
        var (screen, _) = Open(phone);

        screen.ShowRestorePoint.Should().BeTrue();
        screen.RestorePointSummary.Should().Contain("1 platform · 0 holdings · 0 entries");

        screen.AskRestoreCommand.Execute(null);
        screen.IsConfirmingRestore.Should().BeTrue();
        await screen.ConfirmRestoreCommand.ExecuteAsync(null);

        phone.Fingerprint.Should().Be(before);
        screen.IsConfirmingRestore.Should().BeFalse();
        screen.StatusMessage.Should().StartWith("Restored");
    }

    [Fact]
    public async Task Only_a_phone_sized_dashboard_offers_the_shortcut()
    {
        var device = await TestDevice.StartAsync("Android", new TestClock(Start), Demo());
        var layout = new LayoutStore { Width = 400 };
        var navigator = new AppNavigator(
            device.Store,
            layout,
            new NavigationStore(),
            new ModalNavigationStore(),
            device.Backup,
            new FakeBackupFiles()
        );

        var dashboard = new AnalysisViewModel(device.Store, layout, navigator, mode: AnalysisMode.Dashboard);
        var explorer = new AnalysisViewModel(device.Store, layout, navigator, mode: AnalysisMode.Explorer);

        dashboard.ShowBackupShortcut.Should().BeTrue();
        explorer.ShowBackupShortcut.Should().BeFalse();

        layout.Width = 1400;
        dashboard.ShowBackupShortcut.Should().BeFalse("the side rail carries it on wide layouts");
    }
}
