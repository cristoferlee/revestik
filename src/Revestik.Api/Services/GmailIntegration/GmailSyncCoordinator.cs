namespace Revestik.Api.Services.GmailIntegration;

internal class GmailSyncCoordinator
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private int isSyncing;

    public bool IsSyncing => Volatile.Read(ref isSyncing) == 1;

    public bool TryEnter(out IDisposable? lease)
    {
        if (!gate.Wait(0))
        {
            lease = null;
            return false;
        }

        Volatile.Write(ref isSyncing, 1);
        lease = new Lease(this);
        return true;
    }

    private void Exit()
    {
        Volatile.Write(ref isSyncing, 0);
        gate.Release();
    }

    private sealed class Lease(GmailSyncCoordinator owner) : IDisposable
    {
        private GmailSyncCoordinator? owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref owner, null)?.Exit();
        }
    }
}

internal sealed class BankVoucherGmailSyncCoordinator : GmailSyncCoordinator
{
}
