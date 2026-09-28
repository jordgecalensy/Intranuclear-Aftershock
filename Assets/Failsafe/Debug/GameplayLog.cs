using System.Diagnostics;
using UnityEngine;

namespace Failsafe.Debugging
{
    /// <summary>
    /// Optional gameplay tracing. Calls and their arguments are omitted unless
    /// FAILSAFE_VERBOSE_LOGS is explicitly enabled, including in the Editor.
    /// Warnings, errors and explicitly requested reports should use Unity Debug directly.
    /// </summary>
    public static class GameplayLog
    {
        [Conditional("FAILSAFE_VERBOSE_LOGS")]
        public static void Trace(object message, Object context = null)
        {
            UnityEngine.Debug.Log(message, context);
        }
    }
}
