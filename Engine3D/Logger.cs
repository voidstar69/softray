using System.Diagnostics.Contracts;

namespace Engine3D
{
    /// <summary>
    /// TODO: does not work across threads! Silverlight errors because only UI thread can update UI components!
    /// </summary>
    public static class Logger
    {
        public delegate void LogLine(string text);
        public static event LogLine LogLineEvent;

        public static void Log(string text)
        {
            if (LogLineEvent != null)
            {
                LogLineEvent(text);
            }
        }

        public static void Log(string format, params object[] args)
        {
            Contract.Requires(format != null);
            Contract.Requires(args != null);
            if (LogLineEvent != null)
            {
                LogLineEvent(string.Format(format, args));
            }
        }
    }
}