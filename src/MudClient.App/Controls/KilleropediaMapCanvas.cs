using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MudClient.App.Controls;

/// <summary>
/// Draws only the visible source rectangle of a full-resolution Killeropedia map.
/// This avoids transforming and compositing a multi-megapixel visual on every pan.
/// </summary>
public sealed class KilleropediaMapCanvas : Control
{
    private IImage? _source;
    private double _scale = 1;
    private Vector _offset;

    public IImage? Source
    {
        get => _source;
        set
        {
            if (ReferenceEquals(_source, value))
            {
                return;
            }

            _source = value;
            InvalidateVisual();
        }
    }

    public void SetView(double scale, Vector offset)
    {
        if (Math.Abs(_scale - scale) < double.Epsilon && _offset == offset)
        {
            return;
        }

        _scale = scale;
        _offset = offset;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_source is null || _scale <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var sourceRect = CalculateVisibleSourceRect(_source.Size, Bounds.Size, _scale, _offset);
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            return;
        }

        var destinationRect = new Rect(
            _offset.X + sourceRect.X * _scale,
            _offset.Y + sourceRect.Y * _scale,
            sourceRect.Width * _scale,
            sourceRect.Height * _scale);
        context.DrawImage(_source, sourceRect, destinationRect);
    }

    internal static Rect CalculateVisibleSourceRect(
        Size imageSize,
        Size viewportSize,
        double scale,
        Vector offset)
    {
        if (scale <= 0 || imageSize.Width <= 0 || imageSize.Height <= 0
            || viewportSize.Width <= 0 || viewportSize.Height <= 0)
        {
            return default;
        }

        var requested = new Rect(
            -offset.X / scale,
            -offset.Y / scale,
            viewportSize.Width / scale,
            viewportSize.Height / scale);
        return new Rect(imageSize).Intersect(requested);
    }
}
