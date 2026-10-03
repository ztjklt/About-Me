using System;
using System.Linq;
using UnityEngine;
namespace AboutMe
{
    // Opt-in trace of actual UI callbacks; no input injection or game-state changes.
    public static class UIButtonAudit
    {
        public static bool Enabled=>Environment.GetCommandLineArgs().Contains("--button-audit")||Application.absoluteURL.Contains("buttonAudit=1");
    }
}
