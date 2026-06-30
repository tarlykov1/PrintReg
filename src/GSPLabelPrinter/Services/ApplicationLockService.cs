using System.Security.Cryptography;
using System.Text;
using GSPLabelPrinter.Utilities;

namespace GSPLabelPrinter.Services;

public sealed class ApplicationLockService : IDisposable
{
    private readonly Mutex _mutex;
    private bool _acquired;

    public ApplicationLockService(AppPaths paths)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(paths.Root))).Replace("-", string.Empty)[..16];
        _mutex = new Mutex(false, $"GSPLabelPrinter_{hash}");
    }

    public bool TryAcquire()
    {
        try
        {
            _acquired = _mutex.WaitOne(0);
            return _acquired;
        }
        catch (AbandonedMutexException)
        {
            _acquired = true;
            return true;
        }
    }

    public void Dispose()
    {
        if (_acquired)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
        }
        _mutex.Dispose();
    }
}
