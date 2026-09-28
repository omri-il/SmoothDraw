using System.Windows;

namespace SmoothDraw.Views;

/// <summary>The toolbar's "this is the tool / colour in use" state, for its button styles.</summary>
public static class ToolbarProps
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.RegisterAttached(
        "IsActive", typeof(bool), typeof(ToolbarProps), new FrameworkPropertyMetadata(false));

    public static bool GetIsActive(DependencyObject d) => (bool)d.GetValue(IsActiveProperty);
    public static void SetIsActive(DependencyObject d, bool value) => d.SetValue(IsActiveProperty, value);
}
