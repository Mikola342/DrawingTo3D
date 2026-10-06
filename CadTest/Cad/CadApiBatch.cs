using SolidWorks.Interop.sldworks;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    // Suppress redundant UI updates during out-of-process API sequences. This is
    // an application-scoped flag, not a saved geometry/accuracy setting.
    public IDisposable BeginApiBatch() => new ApiBatch(_swApp ?? throw new InvalidOperationException("No CAD connection."));

    private sealed class ApiBatch : IDisposable
    {
        private readonly SldWorks app;
        private readonly bool previous;
        private bool disposed;
        public ApiBatch(SldWorks app)
        {
            this.app = app; previous = app.CommandInProgress;
            if (previous) throw new InvalidOperationException("Another CAD command is already in progress.");
            app.CommandInProgress = true;
        }
        public void Dispose()
        {
            if (disposed) return;
            app.CommandInProgress = previous; disposed = true;
        }
    }
}
