using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The page picker under a grid. It is a separate control on purpose: virtualisation is
/// the answer to a large set, so a grid that never pages never carries this.
/// </summary>
public class GridPager : TemplatedControl
{
    /// <summary>How many numbers are drawn before the run is broken by a gap.</summary>
    private const int Window = 7;

    public static readonly StyledProperty<GridRows?> RowsProperty =
        AvaloniaProperty.Register<GridPager, GridRows?>(nameof(Rows));

    public static readonly StyledProperty<IEnumerable<int>> PageSizesProperty =
        AvaloniaProperty.Register<GridPager, IEnumerable<int>>(nameof(PageSizes), [25, 50, 100, 250]);

    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<GridPager, int>(nameof(PageSize), 100, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly DirectProperty<GridPager, IReadOnlyList<GridPagerPage>> PagesProperty =
        AvaloniaProperty.RegisterDirect<GridPager, IReadOnlyList<GridPagerPage>>(nameof(Pages), pager => pager.pages);

    public static readonly DirectProperty<GridPager, string> RangeTextProperty =
        AvaloniaProperty.RegisterDirect<GridPager, string>(nameof(RangeText), pager => pager.rangeText);

    /// <summary>
    /// The widest <see cref="RangeText"/> this pager will ever hold. The theme draws it as
    /// nothing behind the real one, so the steps beside it keep their place.
    /// </summary>
    public static readonly DirectProperty<GridPager, string> RangeWidestProperty =
        AvaloniaProperty.RegisterDirect<GridPager, string>(nameof(RangeWidest), pager => pager.rangeWidest);

    public static readonly DirectProperty<GridPager, bool> CanGoBackProperty =
        AvaloniaProperty.RegisterDirect<GridPager, bool>(nameof(CanGoBack), pager => pager.canGoBack);

    public static readonly DirectProperty<GridPager, bool> CanGoForwardProperty =
        AvaloniaProperty.RegisterDirect<GridPager, bool>(nameof(CanGoForward), pager => pager.canGoForward);

    private IReadOnlyList<GridPagerPage> pages = [];
    private string rangeText = string.Empty;
    private string rangeWidest = string.Empty;
    private bool canGoBack;
    private bool canGoForward;
    private GridRows? following;

    public GridPager()
    {
        FirstCommand = new TemplateCommand(_ => Go(1));
        PreviousCommand = new TemplateCommand(_ => Go((following?.Page ?? 1) - 1));
        NextCommand = new TemplateCommand(_ => Go((following?.Page ?? 1) + 1));
        LastCommand = new TemplateCommand(_ => Go(following?.PageCount ?? 1));
        GoCommand = new TemplateCommand(page => Go(page as int? ?? 1));
    }

    /// <summary>The rows being paged. The pager reads and writes their page.</summary>
    public GridRows? Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>What the rows per page picker offers.</summary>
    public IEnumerable<int> PageSizes
    {
        get => GetValue(PageSizesProperty);
        set => SetValue(PageSizesProperty, value);
    }

    public int PageSize
    {
        get => GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    /// <summary>The numbers to draw, with a gap standing in for the pages left out.</summary>
    public IReadOnlyList<GridPagerPage> Pages => pages;

    /// <summary>Which rows the page holds, out of how many there are.</summary>
    public string RangeText => rangeText;

    /// <inheritdoc cref="RangeWidestProperty"/>
    public string RangeWidest => rangeWidest;

    public bool CanGoBack => canGoBack;

    public bool CanGoForward => canGoForward;

    public ICommand FirstCommand { get; }

    public ICommand PreviousCommand { get; }

    public ICommand NextCommand { get; }

    public ICommand LastCommand { get; }

    public ICommand GoCommand { get; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RowsProperty)
        {
            if (following is not null)
            {
                following.Rebuilt -= OnRebuilt;
            }

            following = change.GetNewValue<GridRows?>();

            if (following is not null)
            {
                following.Rebuilt += OnRebuilt;
                following.PageSize = PageSize;
            }

            Update();
        }
        else if (change.Property == PageSizeProperty && following is not null)
        {
            following.PageSize = change.GetNewValue<int>();
        }
    }

    private void Go(int page)
    {
        if (following is not null)
        {
            following.Page = page;
        }
    }

    private void OnRebuilt(object? sender, EventArgs e) => Update();

    private void Update()
    {
        var rows = following;
        var page = rows?.Page ?? 1;
        var count = rows?.PageCount ?? 1;

        SetAndRaise(PagesProperty, ref pages, Build(page, count));
        SetAndRaise(CanGoBackProperty, ref canGoBack, page > 1);
        SetAndRaise(CanGoForwardProperty, ref canGoForward, page < count);

        SetAndRaise(
            RangeTextProperty,
            ref rangeText,
            rows is null ? string.Empty : $"{rows.First:N0} to {rows.Last:N0} of {rows.Total:N0}");

        // Neither end of the range can pass the total, so the total three times over is as
        // wide as this readout ever gets.
        SetAndRaise(
            RangeWidestProperty,
            ref rangeWidest,
            rows is null ? string.Empty : $"{rows.Total:N0} to {rows.Total:N0} of {rows.Total:N0}");
    }

    /// <summary>
    /// The first page, the last page and a run around the one being shown, with a gap
    /// wherever that leaves a jump.
    /// </summary>
    private static List<GridPagerPage> Build(int page, int count)
    {
        var built = new List<GridPagerPage>();

        if (count <= Window)
        {
            for (var number = 1; number <= count; number++)
            {
                built.Add(new GridPagerPage(number, number == page));
            }

            return built;
        }

        var wanted = new SortedSet<int> { 1, count, page };

        if (page > 1)
        {
            wanted.Add(page - 1);
        }

        if (page < count)
        {
            wanted.Add(page + 1);
        }

        var last = 0;

        foreach (var number in wanted)
        {
            if (last > 0 && number > last + 1)
            {
                built.Add(GridPagerPage.Gap());
            }

            built.Add(new GridPagerPage(number, number == page));
            last = number;
        }

        return built;
    }
}
