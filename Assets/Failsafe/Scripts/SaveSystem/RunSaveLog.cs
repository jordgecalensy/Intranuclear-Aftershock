using UnityEngine;

namespace Failsafe.Scripts.SaveSystem
{
    public static class RunSaveLog
    {
        public const string Marker = "[RUN-SAVE]";
        public const string Menu = "MENU";
        public const string World = "WORLD";
        public const string Player = "PLAYER";
        public const string Enemy = "ENEMY";
        public const string DebugTools = "DEBUG";
        public const string Autosave = "AUTOSAVE";
        public const string DeathScreen = "DEATH-SCREEN";

        public static string Format(string category, string message)
        {
            string prefix = string.IsNullOrWhiteSpace(category)
                ? Marker
                : $"{Marker}[{category.Trim()}]";

            return $"{prefix} {message ?? string.Empty}";
        }

        public static void Info(
            string category,
            string message,
            Object context = null)
        {
            string formattedMessage = Format(category, message);

            if (context != null)
                Debug.Log(formattedMessage, context);
            else
                Debug.Log(formattedMessage);
        }

        public static void Warning(
            string category,
            string message,
            Object context = null)
        {
            string formattedMessage = Format(category, message);

            if (context != null)
                Debug.LogWarning(formattedMessage, context);
            else
                Debug.LogWarning(formattedMessage);
        }

        public static void Error(
            string category,
            string message,
            Object context = null)
        {
            string formattedMessage = Format(category, message);

            if (context != null)
                Debug.LogError(formattedMessage, context);
            else
                Debug.LogError(formattedMessage);
        }
    }
}
