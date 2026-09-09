using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Единая точка логирования для системы эффектов.
    /// </summary>
    /// <remarks>
    /// Info помечен атрибутом Conditional, поэтому в сборке компилятор вырезает не только сам вызов,
    /// но и вычисление аргументов — интерполяция строки и бокс enum'ов в билде не выполняются вообще.
    /// Чтобы вернуть информационные логи в собранную игру, добавьте символ EFFECT_VERBOSE
    /// в Player Settings -> Other Settings -> Scripting Define Symbols.
    ///
    /// Warning и Error безусловные: они сигналят о неверно настроенных ассетах,
    /// и это должно быть видно в релизе.
    /// </remarks>
    public static class EffectLog
    {
        public const string Marker = "[EFFECT]";

        public const string Status = "STATUS";
        public const string Damage = "DAMAGE";
        public const string Resistance = "RESISTANCE";
        public const string Movement = "MOVEMENT";
        public const string Feedback = "FEEDBACK";
        public const string Physics = "PHYSICS";
        public const string Parameters = "PARAMETERS";
        public const string Bundle = "BUNDLE";

        public static string Format(string category, string message)
        {
            string prefix = string.IsNullOrWhiteSpace(category)
                ? Marker
                : $"{Marker}[{category.Trim()}]";

            return $"{prefix} {message ?? string.Empty}";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("EFFECT_VERBOSE")]
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
