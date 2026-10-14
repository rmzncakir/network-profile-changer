using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace NetworkProfileManager.Helpers
{
    /// <summary>
    /// Clipboard.SetText throws when another process (RDP clipboard sync, a
    /// clipboard manager) holds the clipboard open. The app has no global
    /// exception handler, so an unguarded copy button would take it down.
    /// </summary>
    public static class ClipboardHelper
    {
        /// <summary>Copies <paramref name="text"/>; false if the clipboard is busy.</summary>
        public static bool TrySetText(string text) => TrySetText(text, Clipboard.SetText);

        internal static bool TrySetText(string text, Action<string> setText)
        {
            try
            {
                setText(text);
                return true;
            }
            catch (ExternalException)   // COMException CLIPBRD_E_CANT_OPEN derives from this
            {
                return false;
            }
        }
    }
}
