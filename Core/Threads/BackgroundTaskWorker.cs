using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Core.Threads
{
    /// <summary>
    /// Similar in concept to winform's BackgroundWorker.  If a second request comes in before current is finished, then
    /// the gets cancelled.  Finish event only fires for the latest task, all others silently cancel
    /// </summary>
    /// <remarks>
    /// Workers should should check for cancel often to avoid tasks building up
    /// </remarks>
    public class BackgroundTaskWorker<Treq, Tres> : ICancel
    {
        #region class: RunningTask

        private class RunningTask
        {
            public RunningTask(Task<Tres> task, CancellationTokenSource cancel_source)
            {
                Token = TokenGenerator.NextToken();
                CancelSource = cancel_source;
                Task = task;
            }

            public readonly long Token;
            public readonly CancellationTokenSource CancelSource;
            public readonly Task<Tres> Task;
        }

        #endregion

        #region Declaration Section

        /// <summary>
        /// Holds a link to the calling thread
        /// </summary>
        private readonly TaskScheduler _scheduler;

        private readonly Func<Treq, CancellationToken, Tres> _doWork;
        private readonly Action<Treq, Tres> _finished;
        private readonly Action<Treq, Exception> _finishedException;

        private readonly object _lock = new object();

        private readonly List<RunningTask> _running = new List<RunningTask>();
        private long? _currentToken = null;

        private readonly ICancel[] _cancelDependents;

        #endregion

        #region Constructor

        /// <summary>
        /// NOTE: This remembers what thread its instantiated from and calls the delegates on that thread (see _scheduler)
        /// </summary>
        /// <param name="doWork">Runs on a threadpool thread</param>
        /// <param name="finished">Gets called on the same thread as constructor.  If new tasks are fired off before old ones finish, the old task's finished delegate does NOT get called</param>
        /// <param name="finishedException">Made a special delegate for when exceptions happen</param>
        /// <param name="cancelDependents">These get called whenever a cancel in this class occurs</param>
        public BackgroundTaskWorker(Func<Treq, CancellationToken, Tres> doWork, Action<Treq, Tres> finished, Action<Treq, Exception> finishedException = null, IEnumerable<ICancel> cancelDependents = null)
        {
            _doWork = doWork;
            _finished = finished;
            _finishedException = finishedException;

            _scheduler = TaskScheduler.FromCurrentSynchronizationContext();

            _cancelDependents = cancelDependents?.ToArray() ?? [];
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Cancels any currently running request, then starts this request
        /// </summary>
        public void Start(Treq request)
        {
            // Create a cancel for this specific task
            var cancel_source = new CancellationTokenSource();

            // Create a worker, but not started yet
            var worker = new Task<Tres>(() =>
            {
                return _doWork(request, cancel_source.Token);
            }, cancel_source.Token);

            var latest = new RunningTask(worker, cancel_source);

            // Create a finish
            worker.ContinueWith(result =>
            {
                FinishTask(request, latest);
            }, _scheduler);

            // Store it
            lock (_lock)
            {
                foreach (ICancel other in _cancelDependents)
                    other.Cancel();

                foreach (RunningTask running in _running)
                    running.CancelSource.Cancel();

                _currentToken = latest.Token;
                _running.Add(latest);
            }

            // Start it now that it's stored
            worker.Start();
        }

        public void Cancel()
        {
            lock (_lock)
            {
                foreach (ICancel other in _cancelDependents)
                    other.Cancel();

                _currentToken = null;

                foreach (RunningTask running in _running)
                    running.CancelSource.Cancel();
            }
        }

        #endregion

        #region Private Methods

        // NOTE: this is called from the synchronized thread
        private void FinishTask(Treq request, RunningTask task)
        {
            lock (_lock)
            {
                // Remove from the list
                int index = 0;
                while (index < _running.Count)
                {
                    if (_running[index].Token == task.Token)
                        _running.RemoveAt(index);
                    else
                        index++;
                }

                // If this is an old task, then leave without notifying
                if (_currentToken == null || task.Token != _currentToken.Value)
                    return;

                // This is the latest, clear the flag
                _currentToken = null;
            }

            if (task.Task.IsCanceled)
                return;

            if (task.Task.Exception != null)
            {
                if (_finishedException != null)
                    _finishedException(request, task.Task.Exception);
            }
            else
            {
                _finished(request, task.Task.Result);
            }
        }

        #endregion
    }

    #region interface: ICancel

    public interface ICancel
    {
        void Cancel();
    }

    #endregion
}
