# Portfolio Claude

A personal investment ledger: you record what moved, and it works out what that means. Built on
Avalonia and running on Windows, macOS, Linux, Android, iOS and in the browser from one codebase.

This is a rewrite of `../Portfolio` aimed at four specific problems: charts that could not be reused,
statistics that only ever covered the whole portfolio, list screens with no way to find anything, and
a mobile and browser story that had never actually been run.

## Running it

```bash
dotnet run --project Portfolio.Desktop
```

```bash
dotnet run --project Portfolio.Browser
```

```bash
dotnet build Portfolio.Android -t:Install
```

```bash
dotnet test
```

Desktop, Android and iOS keep a SQLite file in the platform's per-user application data folder. The
browser keeps a JSON document in `localStorage` and seeds a three-year demo portfolio on first visit,
so there is something to look at straight away.

> **A note on the Avalonia build:** a XAML error does *not* fail `dotnet build` — it is reported and
> the build still says "succeeded", leaving an assembly with no compiled XAML that throws at startup.
> `./build.sh <project>` wraps the build and surfaces those diagnostics.

## Layout

| Project | What it is |
|---|---|
| `Portfolio.Domain` | Models and the analytics engine. No UI, no storage. |
| `Portfolio.Charts` | **Reusable** Avalonia chart components. No reference to the domain. |
| `MVVM.Core` | Shell plumbing: navigation stores, the layout store, the chrome window. |
| `ModalControl` | The modal overlay control. |
| `Portfolio.Storage` | JSON persistence plus the demo data. |
| `Portfolio.EntityFramework` | SQLite persistence. |
| `Portfolio` | The app: screens, view models, theme. |
| `Portfolio.Desktop` / `.Android` / `.iOS` / `.Browser` | Platform heads, a dozen lines each. |
| `Portfolio.Test` | Unit and integration tests. |

## 1. The charts are a library now

`Portfolio.Charts` has **no reference to `Portfolio.Domain`**. It takes dates, labels and numbers, so
it can be dropped into any other Avalonia app:

```csharp
var chart = new TimeSeriesChartViewModel { Title = "Balance" };
chart.SetSeries([
    new TimeSeries("Value",  points,   SeriesStyle.Area()),
    new TimeSeries("Paid in", deposits, SeriesStyle.Reference()),
]);
```

```xml
<Application.DataTemplates>
    <charts:ChartDataTemplates />
</Application.DataTemplates>
```

That one registration is all the wiring there is; after it, `<ContentControl Content="{Binding
AnyChart}" />` renders a chart anywhere — a page, a list row, a tooltip.

What is in the box: `TimeSeriesChartViewModel`, `CategoryChartViewModel` (grouped or stacked, vertical
or horizontal), `DistributionChartViewModel` (donut plus its accessible list), `SparklineViewModel`,
`MetricTile`/`MetricTileGroup`, `DataTableViewModel` (sortable), and a `ChartCard` frame.

Two details worth knowing:

- **`ChartScroll.PassThrough`** fixes the scroll trap where a chart under the pointer stops the page
  from scrolling. Ctrl still zooms the chart.
- **The palette** has selected light and dark sets, validated for colour-vision separation and
  contrast against this app's actual surfaces. Colour follows the entity rather than its rank, so
  filtering a list does not repaint the survivors, and past eight series the remainder folds into
  "Other" instead of reusing the first colour.

`Portfolio/Services/AnalyticsPresenter.cs` is the single seam between the domain and the charts.

## 2. Statistics for any scope

`IPortfolioNode` is implemented by `PortfolioRoot`, `Platform`, `FinancialItem` and `GroupNode` (for
"everything of type Grow"). Every analytics function takes a node and a `DateRange`:

```csharp
var stats = node.Describe(DateRange.Year(2025));
var xirr  = node.XirrFor(DateRange.LastMonths(12, today));
var split = node.DistributionByChild(PortfolioMetric.NetEarnings);
```

So one screen — `AnalysisViewModel` — serves the dashboard, the statistics explorer and every
drill-down. Picking a platform or a single holding in the scope picker runs the same code against a
different node.

Beyond the original figures it now reports estimated value, unrealised gain, simple return, cost
ratio, yield on deposits, share of parent, maximum drawdown, best and worst month, average month,
positive-month streak, concentration (Herfindahl) and largest share. New series: estimated value,
cumulative earnings, drawdown, per-period flows, and a stacked income-and-cost breakdown. Plus a
per-period return matrix and a side-by-side table, both for any node's children.

### Two corrections to the maths

Numbers will differ from the old app, deliberately:

- **XIRR.** The original fed interest, bonuses, taxes and fees in as cash flows *and* counted them in
  the terminal value, double-counting them and badly understating returns. A return calculation takes
  only flows that cross the portfolio boundary — deposits and withdrawals — bracketed by the opening
  and closing value. A holding that earns 10 % a year now reports 10 %.
- **Write-offs.** `DefaultLoss` used to reduce invested capital only, leaving the balance untouched,
  which silently turned lost money into cash. It now reduces net earnings and the balance, so the
  identity `Cash = TotalBalance − Invested` holds and a default costs what it costs.

XIRR also returns `null` rather than `0` when it is undefined, because `0` reads as "it broke even",
which is a different and more misleading claim than "there is not enough here to say".

## 3. The list screens

Entries, holdings and platforms each get: search, filters (period, platform, holding, movement kind,
investment type), sorting with direction, grouping, a card ⇄ table toggle, totals for the filtered set
rather than the whole portfolio, and paged loading for long histories. Rows carry a sparkline and open
their own statistics. Delete confirmations name what else will go, because these deletes cascade.

An entry can hold several movements at once — a deposit that was invested the same day, interest net
of tax — so rather than pretending it has one "type", each row shows a labelled chip per movement it
actually contains.

The entry editor previews the effect on balance, invested capital and earnings as you type, and warns
about the mistakes that are easy to make and hard to spot later: future dates, withdrawing more than
the available cash, repaying more capital than is invested.

## 4. Android and the browser

- **Layout follows the measured viewport, not the operating system.** A `LayoutStore` fed by the
  shell's own size drives column counts and chart heights, so a narrow desktop window behaves like a
  phone and a desktop browser gets the desktop layout. The old `OperatingSystem.IsAndroid()` checks
  could not get either case right.
- **One navigation view model, two presentations** — a side rail when there is room, a bottom bar of
  five icons when there is not.
- **Editors are modals on wide layouts and full pages on compact ones**, so Android's back gesture
  does the right thing. `NavigationStore` keeps a real back stack of factories, and
  `App.TryGoBack()` lets the system back button unwind it before leaving the app.
- **The browser head works at all.** It previously never set up storage and would have crashed on the
  first screen; one repository interface now makes that a compile error. SQLite in WebAssembly needs a
  native build and a mounted filesystem to survive a reload, so this head uses a JSON document in
  `localStorage` instead.
- **Culture.** Browsers report a language, not a region — `navigator.language` is often just `es` —
  and a neutral culture has no currency, so every amount rendered with the generic currency sign.
  `AppCulture` resolves a specific culture at startup.
- Safe-area insets, 44 px minimum touch targets, and tables that scroll sideways instead of squeezing.

## 5. Backup and sync

**Backup & sync** sits in the side rail, or behind the sync icon on a phone's dashboard. Export writes
the whole portfolio to one JSON file (`portfolio-2026-10-06-desktop.json`); import on another device
replaces everything there. Moving the file across — Drive, email, a cable — is left to you, which is
what keeps this working the same way on every platform with no server.

- **The file is the browser's own storage format**, `PortfolioSnapshot`, plus who wrote it and when.
  Types are written by name, and a newer format is refused rather than half-read.
- **Nothing merges.** Each device keeps a fingerprint of its data from its last export or import, so
  an import can say what it is about to throw away — changes made since that sync, data that was never
  exported, or a file older than one already seen — before anything happens.
- **Every import is undoable.** The data it replaces is saved as a restore point first, and restoring
  swaps the two, so an undo can be undone as well.
- **All or nothing.** `ReplaceAllAsync` runs in one SQLite transaction, or one document write in the
  browser; a failed import leaves the old portfolio exactly as it was.
- **A stored document that cannot be read is no longer lost.** The browser head used to replace a
  damaged document with demo data the moment it opened; it now copies it to
  `portfolio.snapshot.v1.damaged` and leaves it in place until the first real change.

## Two traps worth knowing about

Both of these cost a crash and a broken screen, and neither is caught by the compiler:

- **A property assigned in a constructor raises its change handler.** If that handler refreshes the
  screen, the refresh runs while later fields are still null. Screens therefore do their first
  refresh from `CompleteInitialisation()` as the last line of the constructor, and handlers call
  `RefreshIfReady()`, which also guards re-entrancy — a refresh rebuilds the filter lists, which
  reassigns their selected items, which raises the handlers again.
- **A `DataTemplate` whose `x:DataType` does not match the item is not applied at all.** Avalonia
  falls back to `ToString()`, so the screen fills with `DisplayRow { Key = … }` rather than failing.
  If a template renders as text, check its `DataType` before anything else.
- **Styling a button's resting state is not enough.** Fluent's own
  `Button:pointerover /template/ ContentPresenter` setters replace the background *and* the text
  colour, so a class that defines only its normal appearance loses both the moment the pointer
  arrives. Every button class here states its hover and pressed appearance, and sets
  `TextElement.Foreground` alongside `Background` — the content presenter carries the text colour,
  not the button.
- **A heading row has to be laid out exactly like a data row.** Spreading headings evenly while the
  rows use a star column plus fixed ones puts every heading over the wrong column, and a trailing
  `Auto` column that is empty in the heading and full of buttons in the rows does the same thing more
  subtly. Pin that column to a width.
- **An `ItemsPanelTemplate` cannot use compiled bindings**, so its panel needs a `ReflectionBinding` —
  and the reflection parser does not understand `#ElementName` or `((Type)DataContext)` casts. It
  throws when the panel is realised, which may be only at one breakpoint on one screen: the card
  layouts here crashed on Android and nowhere else, because desktop never left table mode. Give the
  panel what it needs on its own data context instead; `HoldingGroupViewModel.Columns` exists for
  exactly this.

## Status

Everything builds with zero warnings and 99 tests pass, including screen-construction tests that
reproduce the crash above. The desktop and browser heads have been run and checked, and backup and
sync has been exercised end to end in the browser. The Android and iOS heads compile but have not
been run on a device.
