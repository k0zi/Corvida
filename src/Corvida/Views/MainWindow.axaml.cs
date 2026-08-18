using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using SukiUI.Controls;

namespace Corvida.Views;

public partial class MainWindow : SukiWindow
{
    private const double EdgeThickness = 6;
    private const double FallbackMinWidth = 480;
    private const double FallbackMinHeight = 360;

    private static readonly Cursor SizeNorthSouthCursor = new(StandardCursorType.SizeNorthSouth);
    private static readonly Cursor SizeWestEastCursor = new(StandardCursorType.SizeWestEast);
    private static readonly Cursor SizeNwSeCursor = new(StandardCursorType.TopLeftCorner);
    private static readonly Cursor SizeNeSwCursor = new(StandardCursorType.TopRightCorner);

    private WindowEdge? _resizeEdge;
    private PixelPoint _resizeStartScreenPos;
    private Size _resizeStartSize;
    private PixelPoint _resizeStartPosition;

    public MainWindow()
    {
        InitializeComponent();

        // Avalonia's BeginResizeDrag() (used by SukiWindow's own edge grips) relies on the
        // window manager honoring _NET_WM_MOVERESIZE, which is unreliable on Ubuntu/GNOME
        // (see AvaloniaUI/Avalonia#14291). Resizing is implemented manually here instead,
        // by hit-testing the pointer against the window bounds and driving Width/Height/
        // Position directly, so it works regardless of WM support for interactive resize.
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnWindowPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnWindowPointerReleased, RoutingStrategies.Tunnel);
    }

    private WindowEdge? HitTestEdge(Point pos)
    {
        if (!CanResize || WindowState != WindowState.Normal)
            return null;

        var left = pos.X <= EdgeThickness;
        var right = pos.X >= Bounds.Width - EdgeThickness;
        var top = pos.Y <= EdgeThickness;
        var bottom = pos.Y >= Bounds.Height - EdgeThickness;

        if (top && left) return WindowEdge.NorthWest;
        if (top && right) return WindowEdge.NorthEast;
        if (bottom && left) return WindowEdge.SouthWest;
        if (bottom && right) return WindowEdge.SouthEast;
        if (top) return WindowEdge.North;
        if (bottom) return WindowEdge.South;
        if (left) return WindowEdge.West;
        if (right) return WindowEdge.East;
        return null;
    }

    private static Cursor? CursorFor(WindowEdge? edge) => edge switch
    {
        WindowEdge.North or WindowEdge.South => SizeNorthSouthCursor,
        WindowEdge.West or WindowEdge.East => SizeWestEastCursor,
        WindowEdge.NorthWest or WindowEdge.SouthEast => SizeNwSeCursor,
        WindowEdge.NorthEast or WindowEdge.SouthWest => SizeNeSwCursor,
        _ => null,
    };

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var edge = HitTestEdge(e.GetPosition(this));
        if (edge is null)
            return;

        _resizeEdge = edge;
        _resizeStartScreenPos = this.PointToScreen(e.GetPosition(this));
        _resizeStartSize = new Size(Width, Height);
        _resizeStartPosition = Position;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);

        if (_resizeEdge is not { } edge)
        {
            Cursor = CursorFor(HitTestEdge(pos)) ?? Cursor.Default;
            return;
        }

        var scaling = RenderScaling;
        var screenPos = this.PointToScreen(pos);
        var dx = (screenPos.X - _resizeStartScreenPos.X) / scaling;
        var dy = (screenPos.Y - _resizeStartScreenPos.Y) / scaling;

        var width = _resizeStartSize.Width;
        var height = _resizeStartSize.Height;
        var posX = _resizeStartPosition.X;
        var posY = _resizeStartPosition.Y;

        if (edge is WindowEdge.East or WindowEdge.NorthEast or WindowEdge.SouthEast)
            width += dx;
        if (edge is WindowEdge.West or WindowEdge.NorthWest or WindowEdge.SouthWest)
        {
            width -= dx;
            posX = _resizeStartPosition.X + (screenPos.X - _resizeStartScreenPos.X);
        }
        if (edge is WindowEdge.South or WindowEdge.SouthEast or WindowEdge.SouthWest)
            height += dy;
        if (edge is WindowEdge.North or WindowEdge.NorthEast or WindowEdge.NorthWest)
        {
            height -= dy;
            posY = _resizeStartPosition.Y + (screenPos.Y - _resizeStartScreenPos.Y);
        }

        var minWidth = MinWidth > 0 ? MinWidth : FallbackMinWidth;
        var minHeight = MinHeight > 0 ? MinHeight : FallbackMinHeight;

        Width = Math.Max(width, minWidth);
        Height = Math.Max(height, minHeight);
        Position = new PixelPoint(posX, posY);
        e.Handled = true;
    }

    private void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizeEdge is null)
            return;

        _resizeEdge = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }
}
