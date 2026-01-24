using System;
using System.Windows;
using System.Windows.Controls;

namespace Integration.Behaviors;

public static class AutoScrollBehavior
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled",
            typeof(bool),
            typeof(AutoScrollBehavior),
            new PropertyMetadata(false, OnEnabledChanged));

    public static void SetEnabled(DependencyObject element, bool value)
        => element.SetValue(EnabledProperty, value);

    public static bool GetEnabled(DependencyObject element)
        => (bool)element.GetValue(EnabledProperty);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer sv) return;

        if ((bool)e.NewValue)
        {
            sv.ScrollChanged += OnScrollChanged;
            sv.Loaded += OnLoaded;
        }
        else
        {
            sv.ScrollChanged -= OnScrollChanged;
            sv.Loaded -= OnLoaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer sv)
            sv.ScrollToEnd();
    }

    private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;

        // если контент увеличился и мы "внизу" — скроллим вниз.
        // если пользователь показывает вверх (не внизу) — не мешаем.
        if (e.ExtentHeightChange <= 0) return;

        if (IsAtBottom(sv))
            sv.ScrollToEnd();
    }

    private static bool IsAtBottom(ScrollViewer sv)
    {
        // допуск на дробные значения/погрешность
        return sv.VerticalOffset >= sv.ScrollableHeight - 1.0;
    }
}