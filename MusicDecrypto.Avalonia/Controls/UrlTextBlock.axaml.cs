using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using MusicDecrypto.Avalonia.Helpers;

namespace MusicDecrypto.Avalonia.Controls;

public partial class UrlTextBlock : TemplatedControl
{
    private StackPanel? _layoutRoot;
    private bool _isPressed;

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<OptionsDisplayItem, string>(nameof(Text));
    public static readonly StyledProperty<string> HrefProperty =
        AvaloniaProperty.Register<OptionsDisplayItem, string>(nameof(Href));

    public string _text = string.Empty;
    public string Text
    {
        get => _text;
        set => SetAndRaise(TextProperty, ref _text, value);
    }

    public string Href
    {
        get => GetValue(HrefProperty);
        set => SetValue(HrefProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _layoutRoot = e.NameScope.Find<StackPanel>("LayoutRoot");
        _layoutRoot.PointerPressed += OnPointerPressed;
        _layoutRoot.PointerReleased += OnPointerReleased;
        _layoutRoot.PointerCaptureLost += OnPointerCaptureLost;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
        {
            _isPressed = true;
            PseudoClasses.Set(":pressed", true);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var pt = e.GetCurrentPoint(this);
        if (_isPressed && pt.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
        {
            _isPressed = false;
            PseudoClasses.Set(":pressed", false);

            UrlHelper.OpenLink(Href);
        }
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isPressed = false;
        PseudoClasses.Set(":pressed", false);
    }
}
