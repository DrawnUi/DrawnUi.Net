namespace DrawnUi
{
    /// <summary>
    /// Allocation-free twin of AppoMobi.Specials <c>IsEither</c> for code inside DrawnUi. Declared in the
    /// <c>DrawnUi</c> namespace, so every call in DrawnUi.Draw, DrawnUi.Controls and the other DrawnUi namespaces
    /// binds here before the AppoMobi.Specials using: the arguments go in a span on the stack instead of a new
    /// string[] per call. Property-change handlers run it for X and Y of every drawn control each frame while
    /// content scrolls.
    /// </summary>
    internal static class StringMatchExtensions
    {
        /// <summary>
        /// True when <paramref name="value"/> equals one of <paramref name="values"/> (ordinal); false for a null
        /// value, as AppoMobi.Specials returns.
        /// </summary>
        public static bool IsEither(this string value, params ReadOnlySpan<string> values)
        {
            if (value == null)
                return false;

            foreach (var candidate in values)
            {
                if (string.Equals(value, candidate, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
