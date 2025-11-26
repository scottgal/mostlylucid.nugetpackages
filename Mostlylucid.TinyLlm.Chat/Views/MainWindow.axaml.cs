using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Mostlylucid.TinyLlm.Chat.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}

/// <summary>
/// Converter to select the appropriate style class based on whether the message is from the user
/// </summary>
public class MessageStyleConverter : IValueConverter
{
    public static MessageStyleConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isUser && isUser ? "user-message" : "assistant-message";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter to select the appropriate text style class based on whether the message is from the user
/// </summary>
public class MessageTextStyleConverter : IValueConverter
{
    public static MessageTextStyleConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isUser && isUser ? "user-message-text" : "assistant-message-text";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for boolean to success color
/// </summary>
public static class BoolConverters
{
    public static FuncValueConverter<bool, IBrush> SuccessColor { get; } =
        new(b => b ? Brushes.Green : Brushes.Gray);
}
