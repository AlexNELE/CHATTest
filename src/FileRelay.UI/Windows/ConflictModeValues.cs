using FileRelay.Core.Configuration;

namespace FileRelay.UI.Windows;

public static class ConflictModeValues
{
    public static Array All { get; } = Enum.GetValues(typeof(ConflictMode));
}
