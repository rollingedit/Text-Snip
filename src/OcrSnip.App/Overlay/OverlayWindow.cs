using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using WpfPoint = System.Windows.Point;

namespace OcrSnip.App.Overlay;

public sealed class OverlayWindow : Window
{
    private readonly SelectionCanvas _canvas = new();
    private WpfPoint? _start;
    private Rect? _selection;

    private OverlayWindow()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        Cursor = System.Windows.Input.Cursors.Cross;
        Content = _canvas;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        };
        MouseRightButtonDown += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
        MouseLeftButtonDown += (_, e) =>
        {
            _start = e.GetPosition(this);
            CaptureMouse();
        };
        MouseMove += (_, e) =>
        {
            if (_start is null || !IsMouseCaptured)
            {
                return;
            }

            _selection = OverlayCoordinateMapper.Normalize(_start.Value, e.GetPosition(this));
            _canvas.Selection = _selection;
            _canvas.InvalidateVisual();
        };
        MouseLeftButtonUp += (_, e) =>
        {
            if (_start is null)
            {
                return;
            }

            ReleaseMouseCapture();
            _selection = OverlayCoordinateMapper.Normalize(_start.Value, e.GetPosition(this));
            DialogResult = _selection.Value.Width >= 4 && _selection.Value.Height >= 4;
            Close();
        };
    }

    public static Int32Rect? SelectRectangle()
    {
        var window = new OverlayWindow();
        var accepted = window.ShowDialog() == true;
        if (!accepted || window._selection is null)
        {
            return null;
        }

        var rect = window._selection.Value;
        var scaleX = Forms.Screen.PrimaryScreen?.Bounds.Width / SystemParameters.PrimaryScreenWidth ?? 1.0;
        var scaleY = Forms.Screen.PrimaryScreen?.Bounds.Height / SystemParameters.PrimaryScreenHeight ?? 1.0;
        return OverlayCoordinateMapper.ToPhysicalRect(rect, window.Left, window.Top, scaleX, scaleY);
    }
}

sealed class SelectionCanvas : FrameworkElement
{
    public Rect? Selection { get; set; }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromArgb(110, 0, 0, 0)), null, bounds);

        if (Selection is not { } selection)
        {
            return;
        }

        dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 255, 255, 255)), null, selection);
        dc.DrawRectangle(null, new System.Windows.Media.Pen(System.Windows.Media.Brushes.White, 2), selection);
    }
}
