namespace MainCore.Services
{
    /// <summary>
    /// Holds one account's task list and run state. The list is touched by the UI thread,
    /// the account's scheduler loop and task handlers at the same time, so every access
    /// goes through <see cref="SyncRoot"/> and readers get snapshots.
    /// </summary>
    public sealed class AccountSchedulerActor(AccountId accountId, RxQueue rxQueue)
    {
        private readonly List<BaseTask> _tasks = [];
        private TaskCompletionSource _changed = NewSignal();

        /// <summary>Lock that guards the task list. Re-entrant, so callers can group several calls.</summary>
        public object SyncRoot { get; } = new();

        public bool IsExecuting { get; set; } = false;
        public StatusEnums Status { get; set; } = StatusEnums.Offline;
        public CancellationTokenSource? CancellationTokenSource { get; set; }

        /// <summary>A snapshot of the current tasks. Safe to enumerate while the list changes.</summary>
        public List<BaseTask> Tasks
        {
            get
            {
                lock (SyncRoot)
                {
                    return [.. _tasks];
                }
            }
        }

        /// <summary>
        /// Completes the next time tasks or status change. The scheduler loop waits on this
        /// (or the next due time) instead of polling.
        /// </summary>
        public Task WaitForChangeAsync() => Volatile.Read(ref _changed).Task;

        private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        private void NotifyChanged() => Interlocked.Exchange(ref _changed, NewSignal()).TrySetResult();

        private void AddTask(BaseTask task, bool first)
        {
            lock (SyncRoot)
            {
                if (first)
                {
                    var firstTask = _tasks.OrderBy(x => x.ExecuteAt).FirstOrDefault();
                    if (firstTask is not null)
                    {
                        task.ExecuteAt = firstTask.ExecuteAt.AddHours(-1);
                    }
                }

                _tasks.Add(task);
            }
            NotifyChanged();

            if (task is VillageTask villageTask)
            {
                rxQueue.Enqueue(new VillageTaskAdded(villageTask));
            }

            rxQueue.Enqueue(new TasksModified(accountId));
        }

        public void Add<T>(T task, bool first) where T : BaseTask
        {
            AddTask(task, first);
        }

        public void Remove(BaseTask task)
        {
            lock (SyncRoot)
            {
                _tasks.Remove(task);
            }
            NotifyChanged();
            rxQueue.Enqueue(new TasksModified(accountId));
        }

        public void Remove<T>() where T : AccountTask
        {
            lock (SyncRoot)
            {
                var task = _tasks.OfType<T>().FirstOrDefault(x => x.AccountId == accountId);
                if (task is null) return;

                _tasks.Remove(task);
            }
            NotifyChanged();
        }

        public void Remove<T>(VillageId villageId) where T : VillageTask
        {
            lock (SyncRoot)
            {
                var task = _tasks.OfType<T>().FirstOrDefault(x => x.AccountId == accountId && x.VillageId == villageId);
                if (task is null) return;

                _tasks.Remove(task);
            }
            NotifyChanged();
        }

        public void Clear()
        {
            lock (SyncRoot)
            {
                if (_tasks.Count == 0) return;
                _tasks.Clear();
            }
            NotifyChanged();
            rxQueue.Enqueue(new TasksModified(accountId));
        }

        public void SetStatus(StatusEnums status)
        {
            Status = status;
            NotifyChanged();
            rxQueue.Enqueue(new StatusModified(accountId, status));
        }
    }
}