namespace DrawnUi.Draw
{
    public enum MeasuringStrategy
    {
        /// <summary>
        /// For different children sizes. Default.
        /// </summary>
        MeasureAll,

        /// <summary>
        /// Best for equal item sizes: only the first item is measured, every other row takes its size,
        /// and Add/Remove/Replace/Move are applied arithmetically without measuring.
        /// </summary>
        MeasureFirst,

        /// <summary>
        /// EXPERIMENTAL PREVIEW!!! Acts like MeasureAll but measures by chunks in background.
        /// </summary>
        MeasureVisible,
    }
}
