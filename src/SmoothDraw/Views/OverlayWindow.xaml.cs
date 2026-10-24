using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SmoothDraw.Models;
using SmoothDraw.Services;

namespace SmoothDraw.Views;

public partial class OverlayWindow : Window
{
    private IntPtr _hwnd;
    private bool _isDrawMode;
    private bool _isOverToolbar; // tracks if overlay is currently click-through for toolbar
    private AnnotationTool _currentTool = AnnotationTool.None;
    private readonly AnnotationSettings _settings;
    private LaserService? _laserService;
    private StopwatchService? _stopwatchService;
    private ConfettiService? _confettiService;
    private ToastWindow? _toast;
    private ToolbarWindow? _toolbar;
    private readonly System.Windows.Threading.DispatcherTimer _toolbarHitTimer;

    // Draw mode catches input with this near-invisible fill (alpha 1): a layered window
    // takes input only where its pixels aren't fully transparent. The fill has a hole where
    // the toolbar is, so a finger tap there reaches the toolbar. A finger, unlike the
    // mouse, doesn't hover first for OnToolbarHitCheck to notice.
    private readonly System.Windows.Shapes.Path _hitLayer = new()
    {
        Fill = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
        IsHitTestVisible = false
    };
    private bool _catchInput;

    // Current drawing color
    private Color _currentColor;

    // Shape drawing state
    private Point _shapeStartPoint;
    private Shape? _shapePreview;
    private Polygon? _arrowPreviewHead; // with an arrow's preview (its shaft): its head
    private Point _shapeEndPoint;
    private bool _isDrawingShape;

    // Select/Move state
    private UIElement? _draggedElement;
    private Point _dragStartPoint;
    private Point _dragLastPoint;

    // Arrow pairs: Line ↔ Polygon (arrowhead) — move both together. Never emptied, not
    // even by Clear: an arrow brought back by undo must still move as one piece.
    private readonly Dictionary<UIElement, UIElement> _arrowPairs = new();

    // Undo / redo (Services/DrawingHistory.cs). The screen and each page of the board (F5)
    // keep their own: the ones not on show wait in _screen / _pages with their drawings.
    private DrawingHistory _history = new();

    // The board (F5): pages over the screen, each with its own drawings. What is not on show
    // waits here: the screen's drawings while the board is up, and every page but the one on show.
    private sealed class Surface
    {
        public List<Stroke> Strokes = new();
        public List<UIElement> Shapes = new();
        public DrawingHistory History = new();
    }
    private Surface _screen;
    private readonly List<Surface> _pages = new();
    private int _page; // 0-based: the board's page on show, or the one it opens on
    private bool _boardOn;
    private bool _drawingBeforeBoard;

    // The thinking timer (F6), made on its first use
    private CountdownHud? _countdown;
    // The taskbar's room on the overlay's monitor (DIP), so a timer at the bottom sits above it
    private Thickness _workInset;
    // Selected ink's points before a move or resize, until it has happened
    private List<(Stroke Stroke, StylusPointCollection Before)>? _transformBefore;

    // Text tool state
    private TextBox? _activeTextBox;

    // Colour, thickness and text size — what you picked last, remembered in pen.json
    private readonly PenState _pen;
    private double TextSize => PenState.TextSizes[_pen.TextSize];
    private double PenWidth => _settings.PenSize * PenState.ThicknessScale[_pen.Thickness];

    public OverlayWindow() : this(new AnnotationSettings()) { }

    public OverlayWindow(AnnotationSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        ((Grid)Content).Children.Insert(0, _hitLayer);
        SizeChanged += (_, _) => UpdateHitLayer();
        _pen = PenStateService.Load(settings);
        _currentColor = InkPalette.Get(_pen.Color);

        DrawCanvas.StrokeCollected += OnStrokeCollected;
        DrawCanvas.StrokeErasing += OnStrokeErasing;
        DrawCanvas.SelectionMoving += (_, _) => RememberSelectionPoints();
        DrawCanvas.SelectionResizing += (_, _) => RememberSelectionPoints();
        DrawCanvas.SelectionMoved += (_, _) => RecordSelectionTransform();
        DrawCanvas.SelectionResized += (_, _) => RecordSelectionTransform();
        // An eraser drag is one undo step: it ends when the canvas lets go of the mouse or pen
        DrawCanvas.AddHandler(Mouse.LostMouseCaptureEvent, new System.Windows.Input.MouseEventHandler((_, _) => _history.FlushGroup()), true);
        DrawCanvas.AddHandler(Stylus.LostStylusCaptureEvent, new StylusEventHandler((_, _) => _history.FlushGroup()), true);
        DrawCanvas.IgnoreTouchNearPen = _settings.IgnoreTouchNearPen;
        // A finger draws here, so no press-and-hold right-click ring, no flick gestures
        // (a quick stroke would turn into "back"), no tap circles in the recording
        Stylus.SetIsPressAndHoldEnabled(this, false);
        Stylus.SetIsFlicksEnabled(this, false);
        Stylus.SetIsTapFeedbackEnabled(this, false);

        // Shape canvas mouse events
        ShapeCanvas.MouseLeftButtonDown += OnShapeMouseDown;
        ShapeCanvas.MouseMove += OnShapeMouseMove;
        ShapeCanvas.MouseLeftButtonUp += OnShapeMouseUp;

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;

        _confettiService = new ConfettiService(ConfettiCanvas);

        _toast = new ToastWindow();
        _toast.Show();

        _toolbar = new ToolbarWindow();
        _toolbar.ToolSelected += OnToolbarToolSelected;
        _toolbar.ColorSelected += SetColor;
        _toolbar.ClearRequested += ClearAllStrokes;
        _toolbar.TextSizeCycled += CycleTextSize;
        _toolbar.ThicknessCycled += CycleThickness;
        _toolbar.UndoRequested += () => Undo();
        _toolbar.RedoRequested += () => Redo();
        _toolbar.ConfettiRequested += TriggerConfetti;
        _toolbar.PageRequested += TurnPage;
        _toolbar.CountdownRequested += StartCountdown;
        // It starts on what you picked last
        _toolbar.SetActiveColor(_pen.Color);
        _toolbar.SetThickness(_pen.Thickness);
        _toolbar.SetTextSize(_pen.TextSize);
        // Every history reports; the toolbar shows the one on show
        _history.Changed += ShowUndoRedo;
        _screen = NewSurface();
        _pages.Add(NewSurface());
        try { Board.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.BoardColor)); }
        catch { App.Log($"BoardColor '{_settings.BoardColor}' is not a colour: the default board"); }
        // ✕ stops drawing, exactly like the toolbar's mouse button. It used to quit the
        // whole app, which left the OBS remote's drawing buttons grey (twice on the laptop,
        // 2026-09-27). Quitting is the tray icon's "Quit".
        _toolbar.CloseRequested += () => OnToolbarToolSelected(AnnotationTool.None);
        // Keep the hit layer's hole on the toolbar as it's dragged, collapsed, shown or hidden
        _toolbar.LocationChanged += (_, _) => UpdateHitLayer();
        _toolbar.SizeChanged += (_, _) => UpdateHitLayer();
        _toolbar.IsVisibleChanged += (_, _) => UpdateHitLayer();
        _toolbar.Show();
        // Shown once so it lays out and positions itself, then the dot (or nothing) until you draw
        Dispatcher.BeginInvoke(() => { if (!_isDrawMode) ShowIdleToolbar(); },
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // Timer to detect when cursor is over toolbar and pass clicks through
        _toolbarHitTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _toolbarHitTimer.Tick += OnToolbarHitCheck;
    }

    // No hover on the top edge brings the bar (2026-10-18 to 2026-10-24): Omri found it
    // annoying, and a bar dragged down from there jumped back up (CHANGELOG). With
    // HideToolbarWhenIdle the bar shows only while drawing, where it was last dragged.

    private void OnToolbarToolSelected(AnnotationTool tool)
    {
        if (tool == AnnotationTool.None)
        {
            ExitDrawMode();
            ShowModeIndicator("MOUSE");
        }
        else
        {
            _toolbar?.ExpandFromMinimal();
            EnterDrawMode();
            SetTool(tool);
            ShowModeIndicator(tool.ToString().ToUpper());
        }
    }

    public void InitializeServices(LaserService laserService, StopwatchService stopwatchService)
    {
        _laserService = laserService;
        _stopwatchService = stopwatchService;
        _stopwatchService.TimeUpdated += time => Dispatcher.Invoke(() => TimerText.Text = time);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        OverlayService.SetClickThrough(_hwnd);
        OverlayService.HideFromAltTab(_hwnd);
        OverlayService.SetNoActivate(_hwnd);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    // --- Draw Mode Toggle (Cycle: Pen -> Highlighter -> Eraser -> Off) ---

    public void ToggleDrawMode()
    {
        switch (_currentTool)
        {
            case AnnotationTool.None:
                EnterDrawMode();
                SetTool(AnnotationTool.Pen);
                ShowModeIndicator("PEN");
                break;
            case AnnotationTool.Pen:
                SetTool(AnnotationTool.Highlighter);
                ShowModeIndicator("HIGHLIGHTER");
                break;
            case AnnotationTool.Highlighter:
                SetTool(AnnotationTool.Eraser);
                ShowModeIndicator("ERASER");
                break;
            default: // Eraser, Laser, Arrow, Rectangle, Circle, Text -> Off
                ExitDrawMode();
                ShowModeIndicator("CLICK-THROUGH");
                break;
        }
    }

    // --- Tool Number Shortcuts (Ctrl+0-8) ---

    public void SelectToolByNumber(int num)
    {
        var tool = num switch
        {
            0 => AnnotationTool.None,
            1 => AnnotationTool.Pen,
            2 => AnnotationTool.Highlighter,
            3 => AnnotationTool.Laser,
            4 => AnnotationTool.Eraser,
            5 => AnnotationTool.Arrow,
            6 => AnnotationTool.Rectangle,
            7 => AnnotationTool.Circle,
            8 => AnnotationTool.Text,
            _ => _currentTool
        };
        OnToolbarToolSelected(tool);
    }

    // --- Paste image from clipboard ---

    public void PasteImageFromClipboard()
    {
        if (!System.Windows.Clipboard.ContainsImage()) return;

        var bmp = System.Windows.Clipboard.GetImage();
        if (bmp == null) return;

        // Enter draw mode so the overlay is visible/interactive
        if (!_isDrawMode)
        {
            _toolbar?.ExpandFromMinimal();
            EnterDrawMode();
            SetTool(AnnotationTool.Select);
        }

        // Place image at screen center
        double screenW = ActualWidth;
        double screenH = ActualHeight;
        double imgW = Math.Min(bmp.PixelWidth, screenW * 0.6);
        double imgH = bmp.PixelHeight * (imgW / bmp.PixelWidth);

        var img = new System.Windows.Controls.Image
        {
            Source = bmp,
            Width = imgW,
            Height = imgH,
            IsHitTestVisible = true
        };

        double left = (screenW - imgW) / 2;
        double top = (screenH - imgH) / 2;
        Canvas.SetLeft(img, left);
        Canvas.SetTop(img, top);
        ShapeCanvas.Children.Add(img);
        _history.Push(new ElementsAdded(ShapeCanvas.Children, new() { (img, ShapeCanvas.Children.IndexOf(img)) }));

        ShowModeIndicator("IMAGE PASTED");
    }

    // --- Shape Tool Toggles ---

    public void ToggleArrow()
    {
        if (_currentTool == AnnotationTool.Arrow)
        {
            ExitDrawMode();
            ShowModeIndicator("CLICK-THROUGH");
        }
        else
        {
            EnterDrawMode();
            SetTool(AnnotationTool.Arrow);
            ShowModeIndicator("ARROW");
        }
    }

    public void ToggleRectangle()
    {
        if (_currentTool == AnnotationTool.Rectangle)
        {
            ExitDrawMode();
            ShowModeIndicator("CLICK-THROUGH");
        }
        else
        {
            EnterDrawMode();
            SetTool(AnnotationTool.Rectangle);
            ShowModeIndicator("RECTANGLE");
        }
    }

    public void ToggleCircle()
    {
        if (_currentTool == AnnotationTool.Circle)
        {
            ExitDrawMode();
            ShowModeIndicator("CLICK-THROUGH");
        }
        else
        {
            EnterDrawMode();
            SetTool(AnnotationTool.Circle);
            ShowModeIndicator("CIRCLE");
        }
    }

    public void ToggleText()
    {
        if (_currentTool == AnnotationTool.Text)
        {
            CommitActiveTextBox();
            ExitDrawMode();
            ShowModeIndicator("CLICK-THROUGH");
        }
        else
        {
            EnterDrawMode();
            SetTool(AnnotationTool.Text);
            ShowModeIndicator("TEXT");
        }
    }

    // F11 again = back to the mouse. It used to switch to the pen, which kept the screen
    // covered — pressing F11 to get out left you stuck in drawing mode.
    public void ToggleLaser() =>
        OnToolbarToolSelected(_currentTool == AnnotationTool.Laser ? AnnotationTool.None : AnnotationTool.Laser);

    /// <summary>Esc: finish the text being typed, otherwise stop drawing. False when not
    /// drawing, so the key goes on to the app underneath.</summary>
    public bool HandleEscape()
    {
        // _isDrawMode with no tool = confetti borrowing the overlay; nothing to stop
        if (!_isDrawMode || _currentTool == AnnotationTool.None) return false;
        if (_activeTextBox != null)
            CommitActiveTextBox();
        else
            OnToolbarToolSelected(AnnotationTool.None);
        return true;
    }

    // --- Color Switching ---

    public void SetColor(int index)
    {
        if (index < 1 || index > InkPalette.Colors.Length) return;
        _currentColor = InkPalette.Get(index);
        _pen.Color = index;
        PenStateService.Save(_pen);
        _toolbar?.SetActiveColor(index);

        // Update current tool's drawing attributes and cursor
        if (_currentTool is AnnotationTool.Pen or AnnotationTool.Laser)
        {
            var attrs = DrawCanvas.DefaultDrawingAttributes.Clone();
            attrs.Color = _currentColor;
            DrawCanvas.DefaultDrawingAttributes = attrs;
            DrawCanvas.Cursor = CreateCircleCursor(_currentColor, attrs.Width);
        }
        // Keep laser fade color in sync
        _laserService?.SetBaseColor(_currentColor);

        ShowModeIndicator(InkPalette.Names[index - 1].ToUpper());
    }

    // --- Timer Controls ---

    public void ToggleTimer()
    {
        _stopwatchService?.ToggleStartPause();
        TimerContainer.Visibility = Visibility.Visible;
        ShowModeIndicator(_stopwatchService?.IsRunning == true ? "TIMER STARTED" : "TIMER PAUSED");
    }

    public void ToggleTimerVisibility()
    {
        TimerContainer.Visibility = TimerContainer.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    // --- The thinking timer (F6 = 10 s, Shift+F6 = 30 s, Ctrl+F6 = 60 s; the pad's ⏳ keys) ---

    public bool CountdownRunning => _countdown?.IsRunning == true;
    public int CountdownSeconds => _countdown?.Seconds ?? 0;

    /// <summary>Starts a countdown; the same length again while it runs stops it, another
    /// length starts over with that one. Works whether you draw or not: the overlay is
    /// click-through then, so the timer sits over the apps without taking a click.</summary>
    public void StartCountdown(int seconds)
    {
        if (_countdown == null)
        {
            _countdown = new CountdownHud(Math.Clamp(_settings.CountdownSize, 80, 600));
            CountdownHost.Children.Add(_countdown);
            _countdown.Ended += () => _toolbar?.SetCountdown(0);
        }
        if (_countdown.IsRunning && _countdown.Seconds == seconds)
        {
            _countdown.Cancel();
            App.Log($"Countdown {seconds}s: stopped early");
            ShowModeIndicator("TIMER OFF");
            return;
        }
        // On the screen you are working on. Not while drawing (the overlay is there already),
        // nor over drawings left on the screen, which would move with the overlay.
        if (!_isDrawMode && !_countdown.IsRunning && DrawCanvas.Strokes.Count == 0 && ShapeCanvas.Children.Count == 0)
            RepositionToCurrentMonitor();
        PlaceCountdown();
        _countdown.Start(seconds);
        _toolbar?.SetCountdown(seconds);
        App.Log($"Countdown {seconds}s: started");
        ShowModeIndicator($"⏳ {seconds}");
    }

    private void PlaceCountdown()
    {
        if (_countdown == null) return;
        const double m = 36; // from the screen's edge (above the taskbar at the bottom)
        var w = _workInset;
        string pos = (_settings.CountdownPosition ?? "").Trim().ToLowerInvariant();
        bool top = !pos.StartsWith("bottom"), center = pos == "center";
        _countdown.VerticalAlignment = center ? VerticalAlignment.Center : top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        _countdown.HorizontalAlignment = center || pos.EndsWith("center") ? System.Windows.HorizontalAlignment.Center
            : pos.EndsWith("left") ? System.Windows.HorizontalAlignment.Left : System.Windows.HorizontalAlignment.Right;
        _countdown.Margin = center ? new Thickness(0)
            : new Thickness(w.Left + m, top ? w.Top + m : 0, w.Right + m, top ? 0 : w.Bottom + m);
    }

    // --- The board (F5): an empty page over the screen, with its own drawings ---

    public bool IsBoardOn => _boardOn;

    /// <summary>F5: up = the page covers the screen and the pen is on (the board's own
    /// drawings from last time come back); again = the screen and its drawings come back,
    /// and drawing stops if it was off before. Stopping drawing (Esc, the mouse button, F9's
    /// cycle) also takes the board down: a page you cannot draw on would only hide the screen.</summary>
    public void ToggleBoard()
    {
        if (_boardOn)
        {
            HideBoard();
            if (!_drawingBeforeBoard) ExitDrawMode();
            ShowModeIndicator("SCREEN");
            return;
        }
        _drawingBeforeBoard = _isDrawMode && _currentTool != AnnotationTool.None;
        _screen = SwapSurface(_pages[_page]);
        _boardOn = true;
        Board.Visibility = Visibility.Visible;
        if (!_drawingBeforeBoard)
        {
            _toolbar?.ExpandFromMinimal();
            EnterDrawMode();
            SetTool(AnnotationTool.Pen);
        }
        ShowPage();
        App.Log($"Board up, page {_page + 1}/{_pages.Count}");
        ShowModeIndicator(_pages.Count > 1 ? $"BOARD {_page + 1}/{_pages.Count}" : "BOARD");
    }

    private void HideBoard()
    {
        if (!_boardOn) return;
        _boardOn = false;
        _pages[_page] = SwapSurface(_screen);
        Board.Visibility = Visibility.Collapsed;
        ShowPage();
        App.Log("Board down");
    }

    // --- The board's pages (PageUp / PageDown / ← / →, the toolbar's ◀ ▶) ---

    public int BoardPage => _page + 1;
    public int BoardPageCount => _pages.Count;

    /// <summary>Flips to the board's next (+1) or previous (-1) page, each with its own
    /// drawings and undo. Past the last page is a new, empty one — but not from an empty
    /// page, so pressing on never piles up blank pages. Only while the board is up.</summary>
    public void TurnPage(int dir)
    {
        if (!_boardOn) return;
        int to = _page + Math.Sign(dir);
        if (to < 0)
        {
            ShowModeIndicator("FIRST PAGE");
            return;
        }
        if (to == _pages.Count)
        {
            if (OnShowIsEmpty())
            {
                ShowModeIndicator("EMPTY PAGE");
                return;
            }
            _pages.Add(NewSurface());
        }
        _pages[_page] = SwapSurface(_pages[to]);
        _page = to;
        ShowPage();
        App.Log($"Board page {_page + 1}/{_pages.Count}");
        ShowModeIndicator($"PAGE {_page + 1}/{_pages.Count}");
    }

    private void ShowPage() => _toolbar?.SetPage(_boardOn ? _page + 1 : 0, _pages.Count);

    private bool OnShowIsEmpty() =>
        ShapeCanvas.Children.Count == 0 && DrawCanvas.Strokes.All(s => _laserService?.IsLaserStroke(s) == true);

    private Surface NewSurface()
    {
        var s = new Surface();
        s.History.Changed += ShowUndoRedo;
        return s;
    }

    /// <summary>Puts away what is on show (its drawings and its undo history) and brings out
    /// <paramref name="incoming"/>; returns what was put away. Anything under way is finished
    /// or dropped first, as Clear does; the laser's strokes go (they were fading anyway).</summary>
    private Surface SwapSurface(Surface incoming)
    {
        CommitActiveTextBox();
        DropShapePreview();
        _draggedElement = null;
        if (ShapeCanvas.IsMouseCaptured) ShapeCanvas.ReleaseMouseCapture();
        ClearInkSelection();
        _history.FlushGroup();

        var strokes = DrawCanvas.Strokes;
        var shown = new Surface
        {
            Strokes = strokes.Where(s => _laserService?.IsLaserStroke(s) != true).ToList(),
            Shapes = ShapeCanvas.Children.Cast<UIElement>().ToList(),
            History = _history
        };
        strokes.Clear();
        ShapeCanvas.Children.Clear();
        _laserService?.ClearTracking();

        // The same collections stay (the history's steps point at them); only what is in them changes
        foreach (var s in incoming.Strokes) strokes.Add(s);
        foreach (var el in incoming.Shapes)
        {
            ShapeCanvas.Children.Add(el);
            if (_currentTool == AnnotationTool.Select) el.IsHitTestVisible = true;
        }
        _history = incoming.History;
        ShowUndoRedo();
        return shown;
    }

    private void ShowUndoRedo() => _toolbar?.SetUndoRedo(_history.UndoCount > 0, _history.RedoCount > 0);

    // --- Clear ---

    /// <summary>Toolbar, F10, pipe. Undoable: one step brings everything back (not the
    /// laser, which was fading anyway). Nothing to clear = nothing recorded.</summary>
    public void ClearAllStrokes()
    {
        // A text being typed is finished properly first: removed with the rest, it used to
        // stay "active" and keep the mouse
        CommitActiveTextBox();
        // A shape or a drag under way is dropped
        DropShapePreview();
        _draggedElement = null;
        if (ShapeCanvas.IsMouseCaptured) ShapeCanvas.ReleaseMouseCapture();
        ClearInkSelection();
        _history.FlushGroup();

        var strokes = DrawCanvas.Strokes;
        var ink = strokes.Select((stroke, i) => (stroke, i))
            .Where(x => _laserService?.IsLaserStroke(x.stroke) != true).ToList();
        var shapes = ShapeCanvas.Children.Cast<UIElement>().Select((element, i) => (element, i)).ToList();
        strokes.Clear();
        ShapeCanvas.Children.Clear();
        _laserService?.ClearTracking();
        if (ink.Count + shapes.Count > 0)
            _history.Push(new Cleared(strokes, ink, ShapeCanvas.Children, shapes));
        ShowModeIndicator("CLEARED");
    }

    // --- Undo / redo (toolbar, Ctrl+Z / Ctrl+Y, pipe) ---

    public int UndoCount => _history.UndoCount;
    public int RedoCount => _history.RedoCount;

    public bool Undo() => StepHistory(undo: true);
    public bool Redo() => StepHistory(undo: false);

    private bool StepHistory(bool undo)
    {
        string what = undo ? "UNDO" : "REDO";
        // Mid-gesture (a stroke, a shape or a drag under way) its step isn't there yet
        if (_isDrawingShape || _draggedElement != null || Mouse.Captured != null || Stylus.Captured != null)
        {
            App.Log($"{what} ignored: in the middle of a stroke, shape or drag");
            return false;
        }
        // From the toolbar or the pipe (the keys leave a text box its own Ctrl+Z)
        CommitActiveTextBox();
        // A selection would go on pointing at strokes that are no longer there
        ClearInkSelection();
        bool done;
        try
        {
            done = undo ? _history.Undo() : _history.Redo();
        }
        catch (Exception ex)
        {
            App.Log($"{what} FAILED: {ex}");
            return false;
        }
        ShowModeIndicator(done ? what : $"NOTHING TO {what}");
        return done;
    }

    private void ClearInkSelection()
    {
        if (DrawCanvas.GetSelectedStrokes().Count > 0) DrawCanvas.Select(new StrokeCollection());
    }

    // --- Recording what changes (the other steps are recorded where they happen) ---

    /// <summary>The eraser (and the pen's eraser end) takes strokes one by one; a whole drag
    /// is one undo step. Laser strokes are left alone: they fade by themselves.</summary>
    private void OnStrokeErasing(object? sender, InkCanvasStrokeErasingEventArgs e)
    {
        if (_laserService?.IsLaserStroke(e.Stroke) == true)
        {
            e.Cancel = true;
            return;
        }
        int index = DrawCanvas.Strokes.IndexOf(e.Stroke);
        if (index >= 0) _history.AddToGroup(new InkRemoved(DrawCanvas.Strokes, e.Stroke, index));
    }

    private void RememberSelectionPoints() =>
        _transformBefore = DrawCanvas.GetSelectedStrokes().Select(s => (s, s.StylusPoints.Clone())).ToList();

    private void RecordSelectionTransform()
    {
        var before = _transformBefore;
        _transformBefore = null;
        if (before is not { Count: > 0 }) return;
        var items = before.Where(b => DrawCanvas.Strokes.Contains(b.Stroke))
            .Select(b => (b.Stroke, b.Before, After: b.Stroke.StylusPoints.Clone())).ToList();
        if (items.Count > 0) _history.Push(new InkTransformed(items));
    }

    // --- Hit layer (see _hitLayer) ---

    private void SetCatchInput(bool on)
    {
        _catchInput = on;
        UpdateHitLayer();
    }

    private void UpdateHitLayer()
    {
        if (!_catchInput) { _hitLayer.Data = null; return; }
        Geometry fill = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
        // Holes: the toolbar, and the OBS remote (its "stop drawing" button)
        _passThroughHole = PassThroughRect();
        foreach (var hole in new[] { ToolbarRect(), _passThroughHole })
            if (hole is Rect r)
                fill = new CombinedGeometry(GeometryCombineMode.Exclude, fill, new RectangleGeometry(r));
        _hitLayer.Data = fill;
    }

    // Where the remote's hole was cut; the hit timer re-cuts when the remote moves
    private Rect? _passThroughHole;

    /// <summary>The toolbar's area in this window's coordinates, or null while it's hidden.</summary>
    private Rect? ToolbarRect()
    {
        // What you see of it (the bar or the dot), not its shadow room
        if (_toolbar is not { IsVisible: true } || PresentationSource.FromVisual(this) == null
            || _toolbar.VisibleScreenRect() is not Rect r)
            return null;
        return new Rect(PointFromScreen(r.TopLeft), PointFromScreen(r.BottomRight));
    }

    // --- Internal Helpers ---

    private void EnterDrawMode()
    {
        if (!_isDrawMode)
        {
            _isDrawMode = true;
            _isOverToolbar = false;
            RepositionToCurrentMonitor();
            SetCatchInput(true);
            OverlayService.RemoveClickThrough(_hwnd);
            if (_toolbar != null)
            {
                _toolbar.ExpandFromMinimal();
                _toolbar.Show();
            }
            _toolbar?.RaiseAboveOverlay();
            RaisePassThroughWindow();
            _toolbarHitTimer.Start();
        }
    }

    // --- State and on/off for the control line (Start menu, pen button) ---

    public AnnotationTool CurrentTool => _currentTool;
    public bool IsDrawMode => _isDrawMode;
    /// <summary>A text box is being typed in (it has the keyboard: KeyboardHookService.IsTyping).</summary>
    public bool IsTyping => _activeTextBox != null;
    public int ColorIndex => _pen.Color;
    public int ThicknessIndex => _pen.Thickness;

    /// <summary>The pipe's "dot X Y": a new home for the dot (tests).</summary>
    public string MoveDotHome(int x, int y) => _toolbar?.MoveDotHome(x, y).ToString() ?? "";

    /// <summary>The pipe's "snapshot": PNGs of the toolbar, which no screenshot can show.</summary>
    public List<string> RenderToolbarSnapshots(string dir) => _toolbar?.RenderSnapshots(dir) ?? new List<string>();

    /// <summary>Drawing on (pen) or off — a plain switch, unlike F9's cycle. From the
    /// laser it goes to the pen: the laser has its own switch (F11).</summary>
    public void ToggleDrawOnOff() =>
        SelectToolByNumber(_isDrawMode && _currentTool != AnnotationTool.Laser ? 0 : 1);

    private void ExitDrawMode()
    {
        HideBoard();
        _isDrawMode = false;
        _isOverToolbar = false;
        _toolbarHitTimer.Stop();
        SetTool(AnnotationTool.None);
        SetCatchInput(false);
        OverlayService.SetClickThrough(_hwnd);
        ShowIdleToolbar();
        // Stay on current monitor so annotations remain visible and positioned correctly
    }

    /// <summary>Not drawing: the toolbar shrinks to its dot (hidden from recordings, like
    /// the toolbar), or with HideToolbarWhenIdle it goes away completely.</summary>
    private void ShowIdleToolbar()
    {
        if (_toolbar == null) return;
        if (_settings.HideToolbarWhenIdle)
        {
            _toolbar.Hide();
            return;
        }
        _toolbar.CollapseToMinimal();
        _toolbar.Show();
    }

    private void RepositionToCurrentMonitor()
    {
        Native.User32.GetCursorPos(out var pt);
        var hMonitor = Native.User32.MonitorFromPoint(pt, Native.User32.MONITOR_DEFAULTTONEAREST);

        var mi = new Native.User32.MONITORINFO();
        mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(mi);

        if (Native.User32.GetMonitorInfo(hMonitor, ref mi))
        {
            // Convert screen pixels to WPF device-independent units
            var source = PresentationSource.FromVisual(this);
            double dpiX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
            double dpiY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

            Left = mi.rcMonitor.Left * dpiX;
            Top = mi.rcMonitor.Top * dpiY;
            Width = (mi.rcMonitor.Right - mi.rcMonitor.Left) * dpiX;
            Height = (mi.rcMonitor.Bottom - mi.rcMonitor.Top) * dpiY;
            _workInset = new Thickness(
                (mi.rcWork.Left - mi.rcMonitor.Left) * dpiX, (mi.rcWork.Top - mi.rcMonitor.Top) * dpiY,
                (mi.rcMonitor.Right - mi.rcWork.Right) * dpiX, (mi.rcMonitor.Bottom - mi.rcWork.Bottom) * dpiY);
        }
    }

    // The pass-through window (the OBS dashboard's remote), looked up by title at most
    // once a second — it can be opened, closed or moved at any time
    private IntPtr _passThroughHwnd;
    private DateTime _passThroughLookup = DateTime.MinValue;

    private IntPtr PassThroughWindow()
    {
        if (string.IsNullOrEmpty(_settings.PassThroughWindowTitle)) return IntPtr.Zero;
        if (DateTime.UtcNow - _passThroughLookup > TimeSpan.FromSeconds(1))
        {
            _passThroughHwnd = Native.User32.FindWindow(null, _settings.PassThroughWindowTitle);
            _passThroughLookup = DateTime.UtcNow;
        }
        return _passThroughHwnd;
    }

    private bool CursorOverPassThrough(Native.User32.POINT pt)
    {
        var hwnd = PassThroughWindow();
        if (hwnd == IntPtr.Zero || !Native.User32.IsWindowVisible(hwnd) || Native.User32.IsIconic(hwnd))
            return false;
        // Both sides are Win32 pixels of this process (GetCursorPos and GetWindowRect agree)
        return Native.User32.GetWindowRect(hwnd, out var r)
               && pt.X >= r.Left && pt.X < r.Right && pt.Y >= r.Top && pt.Y < r.Bottom;
    }

    private void RaisePassThroughWindow()
    {
        var hwnd = PassThroughWindow();
        if (hwnd != IntPtr.Zero) OverlayService.RaiseToTop(hwnd);
    }

    /// <summary>The pass-through window's area in this window's coordinates, or null
    /// while it's closed or minimized.</summary>
    private Rect? PassThroughRect()
    {
        var hwnd = PassThroughWindow();
        if (hwnd == IntPtr.Zero || !Native.User32.IsWindowVisible(hwnd) || Native.User32.IsIconic(hwnd)
            || !Native.User32.GetWindowRect(hwnd, out var r) || PresentationSource.FromVisual(this) == null)
            return null;
        return new Rect(PointFromScreen(new Point(r.Left, r.Top)), PointFromScreen(new Point(r.Right, r.Bottom)));
    }

    private void OnToolbarHitCheck(object? sender, EventArgs e)
    {
        if (!_isDrawMode || _toolbar == null) return;

        // The remote opened, closed or moved: move the hit layer's hole with it
        if (PassThroughRect() != _passThroughHole) UpdateHitLayer();

        Native.User32.GetCursorPos(out var pt);
        var toolbarBounds = _toolbar.GetScreenBounds();
        bool overToolbar = toolbarBounds.Contains(pt.X, pt.Y);
        bool overRemote = !overToolbar && CursorOverPassThrough(pt);
        bool cursorOverToolbar = overToolbar || overRemote;

        if (cursorOverToolbar && !_isOverToolbar)
        {
            // Cursor entered toolbar (or remote) area - let clicks pass through to it
            _isOverToolbar = true;
            OverlayService.SetClickThrough(_hwnd);
            if (overRemote) RaisePassThroughWindow();
            else _toolbar.RaiseAboveOverlay();
        }
        else if (!cursorOverToolbar && _isOverToolbar)
        {
            // Cursor left toolbar area - resume capturing for drawing
            _isOverToolbar = false;
            OverlayService.RemoveClickThrough(_hwnd);
        }
    }

    private bool IsShapeTool(AnnotationTool tool) =>
        tool is AnnotationTool.Arrow or AnnotationTool.Rectangle or AnnotationTool.Circle or AnnotationTool.Text or AnnotationTool.Select;

    private void SetTool(AnnotationTool tool)
    {
        // Clean up previous tool state
        CommitActiveTextBox();
        _history.FlushGroup();

        _currentTool = tool;
        _toolbar?.SetActiveTool(tool);

        if (IsShapeTool(tool))
        {
            _laserService?.Stop();

            if (tool == AnnotationTool.Select)
            {
                // Select mode: InkCanvas handles stroke selection, ShapeCanvas handles shape dragging
                DrawCanvas.EditingMode = InkCanvasEditingMode.Select;
                ShapeCanvas.IsHitTestVisible = true;
                DrawCanvas.Cursor = Cursors.Hand;
                ShapeCanvas.Cursor = Cursors.Hand;
                // Make shape children hit-testable for dragging
                foreach (UIElement child in ShapeCanvas.Children)
                    child.IsHitTestVisible = true;
            }
            else
            {
                // Shape drawing tools: disable InkCanvas, enable ShapeCanvas
                DrawCanvas.EditingMode = InkCanvasEditingMode.None;
                ShapeCanvas.IsHitTestVisible = true;
                DrawCanvas.Cursor = tool == AnnotationTool.Text ? Cursors.IBeam : Cursors.Cross;
                ShapeCanvas.Cursor = tool == AnnotationTool.Text ? Cursors.IBeam : Cursors.Cross;
            }
        }
        else
        {
            // Ink tools: enable InkCanvas, disable ShapeCanvas
            ShapeCanvas.IsHitTestVisible = false;

            switch (tool)
            {
                case AnnotationTool.None:
                    DrawCanvas.EditingMode = InkCanvasEditingMode.None;
                    DrawCanvas.Cursor = Cursors.Arrow;
                    _laserService?.Stop();
                    break;

                case AnnotationTool.Pen:
                    DrawCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    DrawCanvas.DefaultDrawingAttributes = new DrawingAttributes
                    {
                        Color = _currentColor,
                        Width = PenWidth,
                        Height = PenWidth,
                        FitToCurve = true,
                        StylusTip = StylusTip.Ellipse,
                        IgnorePressure = false
                    };
                    DrawCanvas.UseCustomCursor = true;
                    DrawCanvas.Cursor = CreateCircleCursor(_currentColor, PenWidth);
                    _laserService?.Stop();
                    break;

                case AnnotationTool.Highlighter:
                    DrawCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    DrawCanvas.DefaultDrawingAttributes = new DrawingAttributes
                    {
                        Color = Color.FromArgb(100, 255, 255, 0),
                        Width = _settings.HighlighterSize,
                        Height = _settings.HighlighterSize,
                        FitToCurve = true,
                        StylusTip = StylusTip.Rectangle,
                        IgnorePressure = true,
                        IsHighlighter = true
                    };
                    DrawCanvas.UseCustomCursor = true;
                    DrawCanvas.Cursor = CreateCircleCursor(Color.FromRgb(255, 255, 0), _settings.HighlighterSize);
                    _laserService?.Stop();
                    break;

                case AnnotationTool.Eraser:
                    DrawCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                    DrawCanvas.UseCustomCursor = true;
                    DrawCanvas.Cursor = CreateCircleCursor(Color.FromRgb(200, 200, 200), _settings.EraserSize);
                    _laserService?.Stop();
                    break;

                case AnnotationTool.Laser:
                    DrawCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    // Single bright stroke - wider for natural glow look, no separate glow layer
                    double laserWidth = _settings.LaserSize * 2;
                    DrawCanvas.DefaultDrawingAttributes = new DrawingAttributes
                    {
                        Color = Color.FromArgb(200, _currentColor.R, _currentColor.G, _currentColor.B),
                        Width = laserWidth,
                        Height = laserWidth,
                        FitToCurve = true,
                        StylusTip = StylusTip.Ellipse,
                        IgnorePressure = true
                    };
                    DrawCanvas.UseCustomCursor = true;
                    DrawCanvas.Cursor = CreateCircleCursor(_currentColor, laserWidth);
                    _laserService?.SetBaseColor(_currentColor);
                    _laserService?.Start();
                    break;
            }
        }
    }

    // --- InkCanvas Stroke Collected ---

    private void OnStrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        if (_currentTool == AnnotationTool.Laser)
        {
            _laserService?.RegisterStroke(e.Stroke); // never undoable: it fades
            return;
        }

        var strokes = DrawCanvas.Strokes;
        int idx = strokes.IndexOf(e.Stroke);
        if (idx < 0) return;
        var added = new List<(Stroke Stroke, int Index)>();
        if (_currentTool == AnnotationTool.Pen)
        {
            // Add subtle shadow under pen strokes for a polished look
            var shadowAttrs = new DrawingAttributes
            {
                Color = Color.FromArgb(40, 0, 0, 0),
                Width = e.Stroke.DrawingAttributes.Width + 4,
                Height = e.Stroke.DrawingAttributes.Height + 4,
                FitToCurve = true,
                StylusTip = StylusTip.Ellipse,
                IgnorePressure = true
            };
            var shadow = new System.Windows.Ink.Stroke(e.Stroke.StylusPoints.Clone(), shadowAttrs);
            strokes.Insert(idx, shadow);
            // One undo step: the stroke and its shadow
            added.Add((shadow, idx));
            added.Add((e.Stroke, idx + 1));
        }
        else
        {
            added.Add((e.Stroke, idx)); // the highlighter
        }
        _history.Push(new InkAdded(strokes, added));
    }

    // --- Shape Drawing (Arrow, Rectangle, Circle) ---

    /// <summary>A mouse event WPF made from a touch while the pen is near: a palm
    /// (see PenInkCanvas). Only the down is filtered. Moves and ups act only on a shape or
    /// drag that a down started, and filtering an up could leave the mouse captured.</summary>
    private bool IsFromTouch(MouseEventArgs e) =>
        e.StylusDevice?.TabletDevice?.Type == TabletDeviceType.Touch && DrawCanvas.PenIsNear;

    private void OnShapeMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsFromTouch(e)) { e.Handled = true; return; }

        if (_currentTool == AnnotationTool.Text)
        {
            PlaceTextBox(e.GetPosition(ShapeCanvas));
            return;
        }

        // Select mode: try to grab an existing shape, or pass through to InkCanvas
        if (_currentTool == AnnotationTool.Select)
        {
            var pos = e.GetPosition(ShapeCanvas);

            // Find the closest shape within a generous hit radius
            UIElement? bestElement = null;
            double bestDist = 15; // max hit distance in pixels

            foreach (UIElement child in ShapeCanvas.Children)
            {
                if (child is TextBox) continue; // skip active text boxes
                double dist = GetDistanceToElement(child, pos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestElement = child;
                }
            }

            if (bestElement != null)
            {
                _draggedElement = bestElement;
                _dragStartPoint = pos;
                _dragLastPoint = pos;
                ShapeCanvas.CaptureMouse();
                e.Handled = true;
                return;
            }

            // No shape hit - temporarily disable ShapeCanvas so InkCanvas Select mode works
            ShapeCanvas.IsHitTestVisible = false;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_currentTool == AnnotationTool.Select)
                    ShapeCanvas.IsHitTestVisible = true;
            }), System.Windows.Threading.DispatcherPriority.Input);
            return;
        }

        if (!IsShapeTool(_currentTool) || _currentTool == AnnotationTool.Text) return;

        _shapeStartPoint = e.GetPosition(ShapeCanvas);
        _shapeEndPoint = _shapeStartPoint;
        _isDrawingShape = true;

        var brush = new SolidColorBrush(_currentColor);

        _shapePreview = _currentTool switch
        {
            AnnotationTool.Arrow => new Line
            {
                Stroke = brush,
                StrokeThickness = PenWidth,
                Effect = CreateShapeShadow()
            },
            AnnotationTool.Rectangle => new System.Windows.Shapes.Rectangle
            {
                Stroke = brush,
                StrokeThickness = PenWidth,
                Fill = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), // Near-transparent but hittable
                RadiusX = 4,
                RadiusY = 4,
                Effect = CreateShapeShadow()
            },
            AnnotationTool.Circle => new Ellipse
            {
                Stroke = brush,
                StrokeThickness = PenWidth,
                Fill = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), // Near-transparent but hittable
                Effect = CreateShapeShadow()
            },
            _ => null
        };

        if (_shapePreview != null)
        {
            if (_shapePreview is not Line)
            {
                Canvas.SetLeft(_shapePreview, _shapeStartPoint.X);
                Canvas.SetTop(_shapePreview, _shapeStartPoint.Y);
            }
            ShapeCanvas.Children.Add(_shapePreview);
            if (_shapePreview is Line shaft)
            {
                // The head is there from the start, so the preview is the arrow it will be
                _arrowPreviewHead = new Polygon { Fill = brush, Stroke = brush, Effect = CreateShapeShadow() };
                ArrowShape.Place(shaft, _arrowPreviewHead, _shapeStartPoint, _shapeStartPoint, shaft.StrokeThickness);
                ShapeCanvas.Children.Add(_arrowPreviewHead);
            }
            ShapeCanvas.CaptureMouse();
        }
    }

    private void OnShapeMouseMove(object sender, MouseEventArgs e)
    {
        // Handle dragging in Select mode
        if (_draggedElement != null)
        {
            var pos = e.GetPosition(ShapeCanvas);
            double dx = pos.X - _dragLastPoint.X;
            double dy = pos.Y - _dragLastPoint.Y;
            MoveElement(_draggedElement, dx, dy);
            // If this is part of an arrow pair (Line + arrowhead), move the partner too
            if (_arrowPairs.TryGetValue(_draggedElement, out var partner))
                MoveElement(partner, dx, dy);
            _dragLastPoint = pos;
            return;
        }

        if (!_isDrawingShape || _shapePreview == null) return;

        var currentPoint = e.GetPosition(ShapeCanvas);
        _shapeEndPoint = currentPoint;

        switch (_shapePreview)
        {
            case Line line when _arrowPreviewHead != null:
                ArrowShape.Place(line, _arrowPreviewHead, _shapeStartPoint, currentPoint, line.StrokeThickness);
                break;
            case System.Windows.Shapes.Rectangle rect:
                UpdateRectShape(rect, _shapeStartPoint, currentPoint);
                break;
            case Ellipse ellipse:
                UpdateRectShape(ellipse, _shapeStartPoint, currentPoint);
                break;
        }
    }

    private void OnShapeMouseUp(object sender, MouseButtonEventArgs e)
    {
        // End dragging in Select mode: one undo step, the whole way from where it started
        if (_draggedElement != null)
        {
            var moved = _draggedElement;
            _draggedElement = null;
            ShapeCanvas.ReleaseMouseCapture();
            double dx = _dragLastPoint.X - _dragStartPoint.X, dy = _dragLastPoint.Y - _dragStartPoint.Y;
            if (Math.Abs(dx) + Math.Abs(dy) > 0.5)
            {
                var elements = new List<UIElement> { moved };
                if (_arrowPairs.TryGetValue(moved, out var partner)) elements.Add(partner);
                _history.Push(new ElementsMoved(elements, dx, dy, MoveElement));
            }
            return;
        }

        if (!_isDrawingShape) return;

        _isDrawingShape = false;
        ShapeCanvas.ReleaseMouseCapture();

        var shape = _shapePreview;
        var head = _arrowPreviewHead;
        _shapePreview = null;
        _arrowPreviewHead = null;
        if (shape == null) return;

        // A click rather than a shape: nothing to keep, nothing to undo
        if (ShapeExtent(shape) < 3)
        {
            ShapeCanvas.Children.Remove(shape);
            if (head != null) ShapeCanvas.Children.Remove(head);
            return;
        }

        var added = new List<(UIElement Element, int Index)> { (shape, ShapeCanvas.Children.IndexOf(shape)) };
        if (head != null)
        {
            added.Add((head, ShapeCanvas.Children.IndexOf(head)));
            // Register bidirectional pair so Select tool moves both together
            _arrowPairs[shape] = head;
            _arrowPairs[head] = shape;
        }
        _history.Push(new ElementsAdded(ShapeCanvas.Children, added));
    }

    /// <summary>A shape (or an arrow, with its head) being drawn is dropped: Clear, the board's swap.</summary>
    private void DropShapePreview()
    {
        if (_isDrawingShape)
        {
            if (_shapePreview != null) ShapeCanvas.Children.Remove(_shapePreview);
            if (_arrowPreviewHead != null) ShapeCanvas.Children.Remove(_arrowPreviewHead);
        }
        _shapePreview = null;
        _arrowPreviewHead = null;
        _isDrawingShape = false;
    }

    /// <summary>How far a shape reaches: an arrow's length (to its tip, not the shaft's end), a
    /// box's larger side.</summary>
    private double ShapeExtent(Shape shape)
    {
        if (shape is Line) return (_shapeEndPoint - _shapeStartPoint).Length;
        double w = double.IsNaN(shape.Width) ? 0 : shape.Width, h = double.IsNaN(shape.Height) ? 0 : shape.Height;
        return Math.Max(w, h);
    }

    private static void UpdateRectShape(Shape shape, Point start, Point current)
    {
        double x = Math.Min(start.X, current.X);
        double y = Math.Min(start.Y, current.Y);
        double w = Math.Abs(current.X - start.X);
        double h = Math.Abs(current.Y - start.Y);

        Canvas.SetLeft(shape, x);
        Canvas.SetTop(shape, y);
        shape.Width = Math.Max(w, 1);
        shape.Height = Math.Max(h, 1);
    }

    private static void MoveElement(UIElement el, double dx, double dy)
    {
        if (el is Line line)
        {
            line.X1 += dx; line.Y1 += dy;
            line.X2 += dx; line.Y2 += dy;
        }
        else if (el is Polygon poly)
        {
            var pts = new PointCollection();
            foreach (var p in poly.Points)
                pts.Add(new Point(p.X + dx, p.Y + dy));
            poly.Points = pts;
        }
        else
        {
            double left = Canvas.GetLeft(el);
            double top = Canvas.GetTop(el);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            Canvas.SetLeft(el, left + dx);
            Canvas.SetTop(el, top + dy);
        }
    }

    private static double GetDistanceToElement(UIElement el, Point pos)
    {
        if (el is Line line)
        {
            return DistanceToLineSegment(pos, new Point(line.X1, line.Y1), new Point(line.X2, line.Y2));
        }
        else if (el is Polygon poly && poly.Points.Count > 0)
        {
            // Check distance to polygon edges + interior
            var bounds = new Rect(
                poly.Points.Min(p => p.X), poly.Points.Min(p => p.Y),
                poly.Points.Max(p => p.X) - poly.Points.Min(p => p.X),
                poly.Points.Max(p => p.Y) - poly.Points.Min(p => p.Y));
            if (bounds.Contains(pos)) return 0;
            double min = double.MaxValue;
            for (int i = 0; i < poly.Points.Count; i++)
            {
                var a = poly.Points[i];
                var b = poly.Points[(i + 1) % poly.Points.Count];
                min = Math.Min(min, DistanceToLineSegment(pos, a, b));
            }
            return min;
        }
        else
        {
            double left = Canvas.GetLeft(el);
            double top = Canvas.GetTop(el);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            var fe = el as FrameworkElement;
            double w = fe?.Width ?? fe?.ActualWidth ?? 0;
            double h = fe?.Height ?? fe?.ActualHeight ?? 0;
            if (double.IsNaN(w)) w = fe?.ActualWidth ?? 0;
            if (double.IsNaN(h)) h = fe?.ActualHeight ?? 0;
            var bounds = new Rect(left, top, Math.Max(w, 1), Math.Max(h, 1));
            if (bounds.Contains(pos)) return 0;
            // Distance to nearest edge
            double cx = Math.Max(bounds.Left, Math.Min(pos.X, bounds.Right));
            double cy = Math.Max(bounds.Top, Math.Min(pos.Y, bounds.Bottom));
            return Math.Sqrt((pos.X - cx) * (pos.X - cx) + (pos.Y - cy) * (pos.Y - cy));
        }
    }

    private static double DistanceToLineSegment(Point p, Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 0.001) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
        double projX = a.X + t * dx, projY = a.Y + t * dy;
        return Math.Sqrt((p.X - projX) * (p.X - projX) + (p.Y - projY) * (p.Y - projY));
    }

    private static System.Windows.Media.Effects.DropShadowEffect CreateShapeShadow()
    {
        return new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 6,
            ShadowDepth = 2,
            Opacity = 0.3
        };
    }

    // --- Text Tool ---

    private void PlaceTextBox(Point position)
    {
        CommitActiveTextBox();

        _activeTextBox = new TextBox
        {
            FontSize = TextSize,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(_currentColor),
            Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
            Padding = new Thickness(6, 4, 6, 4),
            MinWidth = 80,
            AcceptsReturn = false,
            CaretBrush = new SolidColorBrush(_currentColor),
            FlowDirection = System.Windows.FlowDirection.RightToLeft,
            TextAlignment = TextAlignment.Right
        };

        Canvas.SetLeft(_activeTextBox, position.X);
        Canvas.SetTop(_activeTextBox, position.Y);
        ShapeCanvas.Children.Add(_activeTextBox);

        _activeTextBox.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter || args.Key == Key.Escape)
            {
                CommitActiveTextBox();
                args.Handled = true;
            }
        };

        _activeTextBox.Focus();
    }

    private void CommitActiveTextBox()
    {
        if (_activeTextBox == null) return;

        string text = _activeTextBox.Text;
        double left = Canvas.GetLeft(_activeTextBox);
        double top = Canvas.GetTop(_activeTextBox);
        var color = (_activeTextBox.Foreground as SolidColorBrush)?.Color ?? _currentColor;
        double fontSize = _activeTextBox.FontSize;

        ShapeCanvas.Children.Remove(_activeTextBox);

        if (!string.IsNullOrWhiteSpace(text))
        {
            // Detect Hebrew characters for RTL support
            bool hasHebrew = text.Any(c => c >= '\u0590' && c <= '\u05FF');

            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color),
                Padding = new Thickness(4, 2, 4, 2),
                IsHitTestVisible = false,
                FlowDirection = hasHebrew ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight,
                TextAlignment = hasHebrew ? TextAlignment.Right : TextAlignment.Left
            };
            textBlock.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 4,
                ShadowDepth = 1,
                Opacity = 0.7
            };
            Canvas.SetLeft(textBlock, left);
            Canvas.SetTop(textBlock, top);
            ShapeCanvas.Children.Add(textBlock);
            _history.Push(new ElementsAdded(ShapeCanvas.Children,
                new() { (textBlock, ShapeCanvas.Children.IndexOf(textBlock)) }));
        }

        _activeTextBox = null;
    }

    // --- Fun Effects ---

    public void TriggerConfetti()
    {
        // Confetti needs the overlay to render
        bool wasDrawMode = _isDrawMode;
        if (!_isDrawMode)
        {
            _isDrawMode = true;
            RepositionToCurrentMonitor();
            SetCatchInput(true);
            OverlayService.RemoveClickThrough(_hwnd);
        }

        // Use the current overlay size (single monitor), not virtual screen
        _confettiService?.FullScreenBurst(ActualWidth, ActualHeight);
        ShowModeIndicator("🎉");

        // Auto-exit after confetti settles (3 seconds)
        if (!wasDrawMode)
        {
            var exitTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            exitTimer.Tick += (_, _) =>
            {
                exitTimer.Stop();
                if (_currentTool == AnnotationTool.None)
                    ExitDrawMode();
            };
            exitTimer.Start();
        }
    }

    // --- Text Size ---

    public void CycleTextSize()
    {
        _pen.TextSize = (_pen.TextSize + 1) % PenState.TextSizes.Length;
        PenStateService.Save(_pen);
        _toolbar?.SetTextSize(_pen.TextSize);
        if (_activeTextBox != null) _activeTextBox.FontSize = TextSize; // the text being typed too
        ShowModeIndicator($"TEXT {PenState.TextSizeNames[_pen.TextSize].ToUpper()}");
    }

    // --- Thickness (pen, arrow, rectangle, circle): Thin / Medium / Thick ---

    public void CycleThickness()
    {
        _pen.Thickness = (_pen.Thickness + 1) % PenState.ThicknessScale.Length;
        PenStateService.Save(_pen);
        _toolbar?.SetThickness(_pen.Thickness);
        if (_currentTool == AnnotationTool.Pen)
        {
            var attrs = DrawCanvas.DefaultDrawingAttributes.Clone();
            attrs.Width = attrs.Height = PenWidth;
            DrawCanvas.DefaultDrawingAttributes = attrs;
            DrawCanvas.Cursor = CreateCircleCursor(_currentColor, PenWidth);
        }
        ShowModeIndicator(PenState.ThicknessNames[_pen.Thickness].ToUpper());
    }

    // --- Custom Cursor ---

    private static Cursor CreateCircleCursor(Color color, double size)
    {
        int dim = Math.Max((int)(size + 8), 24);
        int hotspot = dim / 2;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // Outer circle (white outline for visibility on any background)
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Colors.White), 2),
                new Point(hotspot, hotspot), size / 2 + 2, size / 2 + 2);
            // Inner circle (tool color)
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(color), 1.5),
                new Point(hotspot, hotspot), size / 2, size / 2);
            // Center dot
            dc.DrawEllipse(new SolidColorBrush(color), null,
                new Point(hotspot, hotspot), 1.5, 1.5);
        }

        var rtb = new RenderTargetBitmap(dim, dim, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var ms = new MemoryStream();
        encoder.Save(ms);
        ms.Position = 0;

        // Build .cur format: 22-byte header + PNG data via icon hack
        // Simpler approach: use System.Windows.Input.Cursor from stream
        return CursorFromBitmap(rtb, hotspot, hotspot);
    }

    private static Cursor CursorFromBitmap(RenderTargetBitmap bitmap, int hotX, int hotY)
    {
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // .cur file header
        bw.Write((short)0);      // Reserved
        bw.Write((short)2);      // Type: cursor
        bw.Write((short)1);      // Image count

        // Directory entry
        bw.Write((byte)width);
        bw.Write((byte)height);
        bw.Write((byte)0);       // Color count
        bw.Write((byte)0);       // Reserved
        bw.Write((short)hotX);   // Hotspot X
        bw.Write((short)hotY);   // Hotspot Y

        // We'll write size after we know it
        long sizePos = ms.Position;
        bw.Write(0);              // Placeholder for size
        bw.Write(22);             // Offset to data (header=6 + entry=16 = 22)

        long dataStart = ms.Position;

        // DIB header (BITMAPINFOHEADER)
        bw.Write(40);                 // Header size
        bw.Write(width);              // Width
        bw.Write(height * 2);         // Height (doubled for XOR + AND masks)
        bw.Write((short)1);           // Planes
        bw.Write((short)32);          // Bits per pixel
        bw.Write(0);                  // Compression (none)
        bw.Write(0);                  // Image size (can be 0 for uncompressed)
        bw.Write(0);                  // X pixels per meter
        bw.Write(0);                  // Y pixels per meter
        bw.Write(0);                  // Colors used
        bw.Write(0);                  // Important colors

        // XOR mask (BGRA, bottom-up)
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = (y * stride) + (x * 4);
                bw.Write(pixels[idx + 0]); // B
                bw.Write(pixels[idx + 1]); // G
                bw.Write(pixels[idx + 2]); // R
                bw.Write(pixels[idx + 3]); // A
            }
        }

        // AND mask (1bpp, bottom-up) - all zeros = fully visible
        int andStride = ((width + 31) / 32) * 4;
        byte[] andMask = new byte[andStride * height];
        bw.Write(andMask);

        // Write actual size
        long dataSize = ms.Position - dataStart;
        ms.Position = sizePos;
        bw.Write((int)dataSize);

        ms.Position = 0;
        return new Cursor(ms);
    }

    // --- Mode Indicator ---

    private void ShowModeIndicator(string text)
    {
        App.Log($"ShowModeIndicator: {text}");
        _toast?.ShowToast(text);
    }
}
