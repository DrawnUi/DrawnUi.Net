namespace DrawnUi.Draw
{
    public interface IAfterEffectDelete : IDisposable
    {
        /// <summary>
        /// For faster scanning of anims of same type
        /// </summary>
        public string TypeId { get; }
        /// <summary>
        /// Called when drawing parent control frame
        /// </summary>
        /// <param name="control"></param>
        /// <param name="canvas"></param>
        /// <param name="scale"></param>
        public void Render(IDrawnBase control, SkiaDrawingContext context, double scale);
        public Task Start(uint length, Easing easing);
        public void Stop();
        public bool IsRunning { get; set; }
        public bool WasStopped { get; set; }
        public int Repeat { get; set; }

        public event EventHandler OnUpdated;

        public event EventHandler OnFinished;
    }
}