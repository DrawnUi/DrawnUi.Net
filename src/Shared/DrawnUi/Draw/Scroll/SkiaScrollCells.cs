namespace DrawnUi.Draw
{
    public partial class SkiaScroll
    {
        // tuning background cells measurement
        protected virtual int IncrementalMeasureAheadCount { get; set; } = 1;
        protected virtual int IncrementalMeasureBatchSize { get; set; } = 1;
        protected virtual double MeasurementTriggerDistance { get; set; } = 0;//500.0;
        private bool _incrementalMeasurementInProgress = false;


        // Check if we need more items
        protected bool? CheckForIncrementalMeasurementTrigger()
        {
            // DISABLED: incremental MeasureAdditionalItems is NOT epoch-guarded and shares no lock with the
            // epoch-guarded full background pass (IntegrateMeasuredBatch). Running it corrupts the structure
            // (two un-synchronized writers of StackStructureMeasured => cells-over-cells on scroll-up-at-
            // startup, worse under Android's separate render thread). The full pass + draw-time measure cover
            // measurement, so incremental is redundant. Proven safe by disabling it on plain CellsStack.
            return null;

            if (_incrementalMeasurementInProgress)
                return null;

            if (Content is SkiaLayout layout && layout.IsTemplated
                                             && layout.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible
                                             && layout.LastMeasuredIndex < layout.ItemsSource.Count)
            {
                // The full background measurement pass (IntegrateMeasuredBatch) is epoch-guarded and writes
                // StackStructureMeasured on its own thread. Incremental MeasureAdditionalItems is NOT guarded
                // and shares no lock with it — running both concurrently makes two threads write the same
                // structure => corruption (cells-over-cells on scroll-up-at-startup). The full pass already
                // measures everything, so incremental is redundant while it runs: skip until it finishes.
                if (layout.IsBackgroundMeasuring)
                    return false;

                var measuredEnd = layout.GetMeasuredContentEnd();

                double currentOffset = Orientation == ScrollOrientation.Vertical
                    ? -ViewportOffsetY
                    : -ViewportOffsetX;

                if (measuredEnd - currentOffset < MeasurementTriggerDistance)
                {
                    Debug.WriteLine($"[SkiaScrollCells] TRIGGERING incremental measurement: measuredEnd={measuredEnd:F1}, currentOffset={currentOffset:F1}, distance={measuredEnd - currentOffset:F1}");
                    TriggerIncrementalMeasurement(layout);
                    return true;
                }

            }

            return false;
        }

        private SemaphoreSlim _lockAdditionalMeasurement = new(1);

        private bool? _lastCheck;

        protected void TriggerIncrementalMeasurement(SkiaLayout layout)
        {
            Debug.WriteLine($"[TriggerIncrementalMeasurement] Starting background measurement batch (BatchSize: {IncrementalMeasureBatchSize}, AheadCount: {IncrementalMeasureAheadCount})");
            _incrementalMeasurementInProgress = true;

            async Task DoMeasure()
            {
                await _lockAdditionalMeasurement.WaitAsync();

                // Measure next batch of items + ahead
                int measuredCount = layout.MeasureAdditionalItems(IncrementalMeasureBatchSize, IncrementalMeasureAheadCount, RenderingScale);

                _lockAdditionalMeasurement.Release();

                _incrementalMeasurementInProgress = false;

                Debug.WriteLine($"[TriggerIncrementalMeasurement] Background measurement completed, measured {measuredCount} items");

                Update();

                if (_lastCheck == null || _lastCheck.Value)
                {
                    _lastCheck = CheckForIncrementalMeasurementTrigger();
                }
            }

            Task.Run(DoMeasure);
        }

    }
}
