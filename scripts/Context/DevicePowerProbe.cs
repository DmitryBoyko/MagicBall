using System.Globalization;
using System.Reflection;
using Godot;

namespace CrystalBall.Context;

/// <summary>
/// Godot 4.7 удалил OS.GetPowerPercentLeft / GetPowerState.
/// Зонд сначала пробует старый API через reflection, затем sysfs на Android.
/// </summary>
public static class DevicePowerProbe
{
    public readonly record struct Reading(int Percent, bool Charging, bool Known);

    public static Reading Read()
    {
        if (TryGodotOsApi(out var reading))
            return reading;
        if (TryAndroidSysfs(out reading))
            return reading;
        return new Reading(-1, false, false);
    }

    private static bool TryGodotOsApi(out Reading reading)
    {
        reading = default;
        try
        {
            var os = typeof(OS);
            var percentMethod = os.GetMethod("GetPowerPercentLeft", BindingFlags.Public | BindingFlags.Static);
            var stateMethod = os.GetMethod("GetPowerState", BindingFlags.Public | BindingFlags.Static);
            if (percentMethod == null)
                return false;

            var percent = Convert.ToInt32(percentMethod.Invoke(null, null), CultureInfo.InvariantCulture);
            var charging = false;
            if (stateMethod != null)
            {
                var state = stateMethod.Invoke(null, null);
                var name = state?.ToString() ?? string.Empty;
                charging = name.Contains("Charg", StringComparison.OrdinalIgnoreCase);
            }

            reading = new Reading(percent, charging, percent >= 0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryAndroidSysfs(out Reading reading)
    {
        reading = default;
        try
        {
            if (!OS.GetName().Equals("Android", StringComparison.OrdinalIgnoreCase))
                return false;

            var percent = ReadSysInt("/sys/class/power_supply/battery/capacity");
            var status = ReadSysText("/sys/class/power_supply/battery/status");
            if (percent < 0)
                return false;

            var charging = status.Contains("Charging", StringComparison.OrdinalIgnoreCase)
                           || status.Contains("Full", StringComparison.OrdinalIgnoreCase);
            reading = new Reading(percent, charging, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int ReadSysInt(string path)
    {
        var text = ReadSysText(path);
        return int.TryParse(text, out var value) ? value : -1;
    }

    private static string ReadSysText(string path)
    {
        if (!FileAccess.FileExists(path))
            return string.Empty;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return file?.GetAsText().Trim() ?? string.Empty;
    }
}
