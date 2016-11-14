using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace Engine3D
{
    /// <summary>
    /// This class will only ever display a single assertion message box.
    /// Furthur assertions will be suppressed.
    /// </summary>
    public static class Assert
    {
        //private static bool hasAsserted = false;

        /// <summary>
        /// Fail an assertion.
        /// Will only ever display a single assertion message.
        /// </summary>
        /// <param name="message"></param>
        [Conditional("DEBUG")]
        public static void Fail(string message)
        {
            Contract.Requires(false); // enclosing method always produces an error
            Contract.Assert(false, message);

            //if (!hasAsserted)
            //{
            //    hasAsserted = true;
            //    Debug.Assert(false, message);
            //}
        }

        /// <summary>
        /// Will only ever display a single assertion message.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [Conditional("DEBUG")]
        public static void IsTrue(bool condition, string message)
        {
            Contract.Assert(condition, message);

            //if (!condition && !hasAsserted)
            //{
            //    hasAsserted = true;
            //    Debug.Assert(condition, message);
            //}
        }

        /// <summary>
        /// Will only ever display a single assertion message.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="message"></param>
        [Conditional("DEBUG")]
        public static void IsTrue(bool condition, string format, params object[] args)
        {
            Contract.Requires(format != null);
            Contract.Requires(args != null);
            Contract.Assert(condition, string.Format(format, args));

            //if (!condition && !hasAsserted)
            //{
            //    hasAsserted = true;
            //    Debug.Assert(condition, string.Format(format, args));
            //}
        }
    }
}