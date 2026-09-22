namespace CouponManager.Web.Services;

public enum ToastKind
{
    Success,
    Error
}

public sealed record ToastMessage(Guid Id, string Text, ToastKind Kind);

public sealed class ToastService : IDisposable
{
    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(4);
    private readonly Lock _sync = new();
    private readonly List<ToastMessage> _messages = [];
    private readonly CancellationTokenSource _disposeToken = new();

    public event Action? Changed;

    public IReadOnlyList<ToastMessage> Messages
    {
        get
        {
            lock (_sync)
            {
                return [.. _messages];
            }
        }
    }

    public void Success(string message) => Show(message, ToastKind.Success);
    public void Error(string message) => Show(message, ToastKind.Error);

    public void Dismiss(Guid id)
    {
        bool removed;
        lock (_sync)
        {
            removed = _messages.RemoveAll(message => message.Id == id) > 0;
        }

        if (removed)
        {
            Changed?.Invoke();
        }
    }

    private void Show(string message, ToastKind kind)
    {
        var toast = new ToastMessage(Guid.NewGuid(), message, kind);
        lock (_sync)
        {
            _messages.Add(toast);
        }

        Changed?.Invoke();
        _ = DismissLaterAsync(toast.Id);
    }

    private async Task DismissLaterAsync(Guid id)
    {
        try
        {
            await Task.Delay(DisplayDuration, _disposeToken.Token);
            Dismiss(id);
        }
        catch (OperationCanceledException) when (_disposeToken.IsCancellationRequested)
        {
            // The circuit was disposed before the toast expired.
        }
    }

    public void Dispose()
    {
        _disposeToken.Cancel();
        _disposeToken.Dispose();
    }
}
