using System;
using System.IO;

namespace Winning.Playwright.NUnit.Internal;

internal static class EnvConfigLoader
{
    public static bool IsEnvTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
               || value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }


    public static bool IsEnvFalsy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("0", StringComparison.OrdinalIgnoreCase)
               || value.Equals("false", StringComparison.OrdinalIgnoreCase)
               || value.Equals("no", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LoadBool(string envVariableName, bool defaultValue = false)
    {
        var value = Environment.GetEnvironmentVariable(envVariableName);

        if (IsEnvTruthy(value))
            return true;
        
        if (IsEnvFalsy(value))
            return false;
        
        return defaultValue;
    }

    internal static string LoadPath(string envVariableName, string defaultValue = "./")
    {
        var value = Environment.GetEnvironmentVariable(envVariableName);

        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        // Check if the value is a valid path by extracting the full path (will throw an exception if it's not a valid path)
        var fullPath = Path.GetFullPath(value);
        
        return value;
    }
}