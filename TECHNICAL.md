# Portfolio — technical notes

Developer reference for the codebase. For how to use the app, see the [README](README.md).

The app is built with [Avalonia](https://avaloniaui.net/) on .NET 10, with one shared codebase and
a thin head per platform: Windows/macOS/Linux, Android, iOS and the browser (WebAssembly).

## Building and running

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

A XAML error does *not* fail `dotnet build`: it is reported, the build still says "succeeded", and
the resulting assembly throws at startup. `./build.sh <project>` wraps the build and surfaces those
diagnostics.

## Storage

| Head | Store | Location |
|---|---|---|
| Desktop | SQLite (`Portfolio.db`) | `%LOCALAPPDATA%\Portfolio` on Windows, the equivalent per-user app-data folder elsewhere |
| Android / iOS | SQLite (`Portfolio.db`) | The app's private data folder |
| Browser | JSON document | `localStorage`; seeded with a three-year demo portfolio on first visit |

## Project layout

| Project | What it is |
|---|---|
| `Portfolio.Domain` | Models and the analytics engine. No UI, no storage. |
| `Portfolio.Charts` | Reusable Avalonia chart components. No reference to the domain. |
| `MVVM.Core` | Shell plumbing: navigation stores, the layout store, the chrome window. |
| `ModalControl` | The modal overlay control. |
| `Portfolio.Storage` | JSON persistence, backup import/export and the demo data. |
| `Portfolio.EntityFramework` | SQLite persistence. |
| `Portfolio` | The app: screens, view models, theme. |
| `Portfolio.Desktop` / `.Android` / `.iOS` / `.Browser` | Platform heads. |
| `Portfolio.Test` | Unit and integration tests. |
| `branding/` | The app icon's SVG source. |

## Domain model

- **Platform** → **FinancialItem** (a "holding" in the UI) → **Entry**.
- An `Entry` holds non-negative amounts in named fields: `Deposit`, `Withdrawal`, `Investment`,
  `PrincipalRepayment`, `Interest`, `Bonus`, `Tax`, `Fee`, `DefaultLoss`, and an optional
  `MarketValue`. The direction of a movement is implied by the field it is in.
- A `MarketValue` anchors the holding's estimated value until the next one.
- `InvestmentType` is the role a holding plays (`Grow`, `Compound`, `Hold`, `Buffer`, `Shrink`,
  `Exit`, `Other`), not an asset class.

## Analytics

`IPortfolioNode` is implemented by `PortfolioRoot`, `Platform`, `FinancialItem` and `GroupNode`
(e.g. "everything of type Grow"). Every analytics function takes a node and a `DateRange`:

```csharp
var stats = node.Describe(DateRange.Year(2025));
var xirr  = node.XirrFor(DateRange.LastMonths(12, today));
var split = node.DistributionByChild(PortfolioMetric.NetEarnings);
```

`AnalysisViewModel` drives the dashboard, the statistics screen and every drill-down by pointing
the same code at a different node.

Reported figures: balance, cash, invested, estimated value, unrealised gain, deposits, withdrawals,
interest, bonuses, taxes, fees, write-offs, XIRR, simple return, cost ratio, yield on deposits,
share of parent, maximum drawdown, best/worst/average month, positive-month streak, concentration
(Herfindahl) and largest share. Series: value vs. paid in, estimated value, cumulative earnings,
drawdown, per-period flows and a stacked income-and-cost breakdown, plus a per-period return matrix
and a side-by-side table for any node's children.

Calculation rules:

- **XIRR** uses only flows that cross the portfolio boundary (deposits and withdrawals), bracketed
  by the opening and closing value. It returns `null` when it is undefined.
- **Write-offs** (`DefaultLoss`) reduce both net earnings and the balance, so
  `Cash = TotalBalance − Invested` always holds.

## Charts library

`Portfolio.Charts` takes dates, labels and numbers and has no dependency on the domain.

```csharp
var chart = new TimeSeriesChartViewModel { Title = "Balance" };
chart.SetSeries([
    new TimeSeries("Value",  points,   SeriesStyle.Area()),
    new TimeSeries("Paid in", deposits, SeriesStyle.Reference()),
]);
```

Register the templates once:

```xml
<Application.DataTemplates>
    <charts:ChartDataTemplates />
</Application.DataTemplates>
```

After that, `<ContentControl Content="{Binding AnyChart}" />` renders a chart anywhere.

Available components: `TimeSeriesChartViewModel`, `CategoryChartViewModel` (grouped or stacked,
vertical or horizontal), `DistributionChartViewModel` (donut plus list), `SparklineViewModel`,
`MetricTile` / `MetricTileGroup`, `DataTableViewModel` (sortable) and a `ChartCard` frame.

- `ChartScroll.PassThrough` lets the page scroll while the pointer is over a chart; Ctrl+wheel
  still zooms the chart.
- The palette has light and dark sets. Colour follows the entity, not its rank, and more than eight
  series fold into "Other".

`Portfolio/Services/AnalyticsPresenter.cs` is the only place where domain data is turned into
chart view models.

## Layout and navigation

- A `LayoutStore`, fed by the shell's measured size, drives column counts and chart heights.
- `NavigationViewModel` is presented as a side rail on wide layouts and a five-icon bottom bar on
  compact ones.
- Editors open as modals on wide layouts and as full pages on compact ones.
- `NavigationStore` keeps a back stack; `App.TryGoBack()` lets the Android back button unwind it
  before leaving the app.
- `AppCulture` resolves a specific culture at startup (browsers often report only a language such
  as `es`).

## Backup and sync

- Export writes a `PortfolioSnapshot` JSON file (`portfolio-<date>-<device>.json`) with the writer
  device and timestamp. Types are written by name; a file from a newer format is refused.
- Import replaces all data on the target device; nothing is merged.
- Each device stores a fingerprint of its data at its last export or import, so an import can warn
  about unsynced local changes, never-exported data, or an older file.
- Before an import, the current data is saved as a restore point. Restoring swaps the two.
- `ReplaceAllAsync` runs in a single SQLite transaction (or a single document write in the browser).
- In the browser, an unreadable stored document is copied to `portfolio.snapshot.v1.damaged` and
  left in place until the first real change.

Backup files match `portfolio-[0-9]*.json` in `.gitignore` so real data is never committed.

## Pitfalls

None of these are caught by the compiler.

- **Change handlers fire during construction.** Screens do their first refresh from
  `CompleteInitialisation()` at the end of the constructor, and handlers call `RefreshIfReady()`,
  which also guards against re-entrancy.
- **A `DataTemplate` with the wrong `x:DataType` is silently skipped** and Avalonia falls back to
  `ToString()`. If a template renders as text, check its `DataType` first.
- **Button classes must style hover and pressed states**, and set `TextElement.Foreground` as well
  as `Background`, or Fluent's pointer-over setters override both.
- **Heading rows must use the same column definitions as data rows**, including fixed widths for
  trailing button columns.
- **`ItemsPanelTemplate` cannot use compiled bindings.** Use `ReflectionBinding`, which does not
  support `#ElementName` or `((Type)DataContext)` casts; expose what the panel needs on its own
  data context (see `HoldingGroupViewModel.Columns`).
