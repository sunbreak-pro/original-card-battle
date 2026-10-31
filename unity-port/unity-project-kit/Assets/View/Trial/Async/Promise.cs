// Promise: the JS Promise in C#, so the trial's scenes port their async flows line for line.
//
//   async Promise HoldBeat(...) { await clock.Wait(200); ... }       // an async JS function
//   Promise flying = hand.FlyDrawn(...); ... await flying; await flying;   // awaited by several (fan-out)
//   await Promise.All(a, b);  await Promise.Race(a, b);
//
// Semantics kept from JS:
// - An async method runs synchronously up to its first await (as an async JS function does).
// - `await p` never continues inline: the continuation is posted to Microtasks, even when p is already settled,
//   and runs when the queue is drained (after the clock step / input handler that settled it).
// - A promise may be awaited any number of times, before or after it settles.
// - A promise nobody settles never continues (a destroyed scene's flow stops where it stands).
// - A rejection rethrows at `await`; a rejection nobody observes goes to Microtasks.Unhandled.
//
// Engine-free (no UnityEngine), main thread only.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Trial
{
    [AsyncMethodBuilder(typeof(PromiseMethodBuilder))]
    public class Promise
    {
        private enum State { Pending, Resolved, Rejected }

        private State _state;
        private Exception _error;
        private List<Action> _waiters;
        private bool _observed;

        public Promise() { }

        public bool IsPending => _state == State.Pending;
        public bool IsResolved => _state == State.Resolved;
        public bool IsRejected => _state == State.Rejected;
        public bool IsSettled => _state != State.Pending;
        public Exception Error => _error;

        /// <summary>Promise.resolve(): an already resolved promise (awaiting it still yields to the queue).</summary>
        public static Promise Resolved()
        {
            var p = new Promise();
            p._state = State.Resolved;
            return p;
        }

        /// <summary>A promise that never settles.</summary>
        public static Promise Never() => new Promise();

        /// <summary>Promise.reject(e).</summary>
        public static Promise Rejected(Exception error)
        {
            var p = new Promise();
            p.Reject(error);
            return p;
        }

        /// <summary>Calls the thunk; a synchronous throw becomes a rejected promise, a null result a resolved one.</summary>
        public static Promise Run(Func<Promise> thunk)
        {
            try { return thunk?.Invoke() ?? Resolved(); }
            catch (Exception e) { return Rejected(e); }
        }

        /// <summary>Like Run, but a throw or a rejection is reported to Microtasks.Unhandled and the result resolves anyway (the JS scenes' safe()).</summary>
        public static Promise Safe(Func<Promise> thunk)
        {
            Promise p;
            try { p = thunk?.Invoke(); }
            catch (Exception e) { Microtasks.Report(e); return Resolved(); }
            if (p == null) return Resolved();
            var s = new PromiseSource();
            p.OnSettled(() =>
            {
                if (p.IsRejected) Microtasks.Report(p.Observe());
                s.Resolve();
            });
            return s.Promise;
        }

        internal bool TryResolveCore()
        {
            if (_state != State.Pending) return false;
            _state = State.Resolved;
            Flush();
            return true;
        }

        internal bool TryRejectCore(Exception error)
        {
            if (_state != State.Pending) return false;
            _state = State.Rejected;
            _error = error ?? new Exception("Promise rejected with null");
            bool hadWaiters = _waiters != null && _waiters.Count > 0;
            Flush();
            if (!hadWaiters)
            {
                // As the browser does at the end of a microtask checkpoint: a rejection still unobserved is reported.
                Microtasks.Post(() => { if (!_observed) Microtasks.Report(_error); });
            }
            return true;
        }

        internal void Reject(Exception error) => TryRejectCore(error);

        private void Flush()
        {
            if (_waiters == null) return;
            List<Action> w = _waiters;
            _waiters = null;
            for (int i = 0; i < w.Count; i++) Microtasks.Post(w[i]);
        }

        /// <summary>Posts `action` to the microtask queue once this promise settles (at once if it has).</summary>
        public void OnSettled(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _observed = true;
            if (_state != State.Pending) { Microtasks.Post(action); return; }
            (_waiters ??= new List<Action>()).Add(action);
        }

        /// <summary>Marks the rejection as handled and returns its error (null when not rejected).</summary>
        public Exception Observe()
        {
            _observed = true;
            return _error;
        }

        /// <summary>Throws the rejection, if any (the awaiter's GetResult).</summary>
        public void ThrowIfRejected()
        {
            _observed = true;
            if (_state == State.Rejected) throw new PromiseRejectedException(_error);
            if (_state == State.Pending) throw new InvalidOperationException("Promise: GetResult before it settled.");
        }

        public PromiseAwaiter GetAwaiter() => new PromiseAwaiter(this);

        /// <summary>Says "fire and forget" where JS calls an async function without awaiting it (silences CS4014; a rejection still reaches Microtasks.Unhandled).</summary>
        public void Forget() { }

        // ---- then / catch -----------------------------------------------------------------------

        /// <summary>.then(() => …): runs `next` after this resolves; the result settles after it. A rejection passes through.</summary>
        public Promise Then(Action next)
        {
            var s = new PromiseSource();
            OnSettled(() =>
            {
                if (IsRejected) { s.Reject(Observe()); return; }
                try { next?.Invoke(); s.Resolve(); }
                catch (Exception e) { s.Reject(e); }
            });
            return s.Promise;
        }

        /// <summary>.then(() => promise): the result settles with the promise `next` returns.</summary>
        public Promise Then(Func<Promise> next)
        {
            var s = new PromiseSource();
            OnSettled(() =>
            {
                if (IsRejected) { s.Reject(Observe()); return; }
                Promise inner;
                try { inner = next?.Invoke(); }
                catch (Exception e) { s.Reject(e); return; }
                if (inner == null) { s.Resolve(); return; }
                inner.OnSettled(() =>
                {
                    if (inner.IsRejected) s.Reject(inner.Observe());
                    else s.Resolve();
                });
            });
            return s.Promise;
        }

        /// <summary>.catch(e => …): handles a rejection; the result resolves after the handler (or passes a resolution through).</summary>
        public Promise Catch(Action<Exception> handler)
        {
            var s = new PromiseSource();
            OnSettled(() =>
            {
                if (!IsRejected) { s.Resolve(); return; }
                try { handler?.Invoke(Observe()); s.Resolve(); }
                catch (Exception e) { s.Reject(e); }
            });
            return s.Promise;
        }

        /// <summary>.finally(() => …).</summary>
        public Promise Finally(Action after)
        {
            var s = new PromiseSource();
            OnSettled(() =>
            {
                try { after?.Invoke(); }
                catch (Exception e) { s.Reject(e); return; }
                if (IsRejected) s.Reject(Observe());
                else s.Resolve();
            });
            return s.Promise;
        }

        // ---- combinators --------------------------------------------------------------------------

        /// <summary>Promise.all: resolves when every promise has resolved (null entries count as resolved); rejects with the first rejection.</summary>
        public static Promise All(params Promise[] promises) => All((IEnumerable<Promise>)promises);

        public static Promise All(IEnumerable<Promise> promises)
        {
            var s = new PromiseSource();
            var list = new List<Promise>();
            if (promises != null) foreach (Promise p in promises) if (p != null) list.Add(p);
            if (list.Count == 0) { s.Resolve(); return s.Promise; }
            int left = list.Count;
            foreach (Promise p in list)
            {
                Promise q = p;
                q.OnSettled(() =>
                {
                    if (q.IsRejected) { s.TryReject(q.Observe()); return; }
                    if (--left == 0) s.TryResolve();
                });
            }
            return s.Promise;
        }

        /// <summary>Promise.race: settles as the first of them settles (an empty race never settles, as in JS).</summary>
        public static Promise Race(params Promise[] promises) => Race((IEnumerable<Promise>)promises);

        public static Promise Race(IEnumerable<Promise> promises)
        {
            var s = new PromiseSource();
            if (promises == null) return s.Promise;
            foreach (Promise p in promises)
            {
                if (p == null) { s.TryResolve(); continue; }
                Promise q = p;
                q.OnSettled(() =>
                {
                    if (q.IsRejected) s.TryReject(q.Observe());
                    else s.TryResolve();
                });
            }
            return s.Promise;
        }
    }

    /// <summary>A Promise with a value (Promise&lt;T&gt; in JS terms). Awaiting it gives the value.</summary>
    [AsyncMethodBuilder(typeof(PromiseMethodBuilder<>))]
    public sealed class Promise<T> : Promise
    {
        private T _value;

        public T Value => _value;

        public static Promise<T> FromResult(T value)
        {
            var p = new Promise<T>();
            p.TryResolveValue(value);
            return p;
        }

        public new static Promise<T> Never() => new Promise<T>();

        public new static Promise<T> Rejected(Exception error)
        {
            var p = new Promise<T>();
            p.Reject(error);
            return p;
        }

        internal bool TryResolveValue(T value)
        {
            if (!IsPending) return false;
            _value = value;
            return TryResolveCore();
        }

        public T GetResultOrThrow()
        {
            ThrowIfRejected();
            return _value;
        }

        public new PromiseAwaiter<T> GetAwaiter() => new PromiseAwaiter<T>(this);

        /// <summary>.then(v => …).</summary>
        public Promise Then(Action<T> next)
        {
            var s = new PromiseSource();
            OnSettled(() =>
            {
                if (IsRejected) { s.Reject(Observe()); return; }
                try { next?.Invoke(_value); s.Resolve(); }
                catch (Exception e) { s.Reject(e); }
            });
            return s.Promise;
        }

        /// <summary>.then(v => u).</summary>
        public Promise<TOut> Then<TOut>(Func<T, TOut> map)
        {
            var s = new PromiseSource<TOut>();
            OnSettled(() =>
            {
                if (IsRejected) { s.Reject(Observe()); return; }
                try { s.Resolve(map(_value)); }
                catch (Exception e) { s.Reject(e); }
            });
            return s.Promise;
        }
    }

    /// <summary>new Promise((resolve, reject) => …): the side that settles.</summary>
    public sealed class PromiseSource
    {
        public PromiseSource() { Promise = new Promise(); }
        public Promise Promise { get; }
        public bool IsSettled => Promise.IsSettled;
        public void Resolve() => Promise.TryResolveCore();
        public bool TryResolve() => Promise.TryResolveCore();
        public void Reject(Exception error) => Promise.TryRejectCore(error);
        public bool TryReject(Exception error) => Promise.TryRejectCore(error);
    }

    public sealed class PromiseSource<T>
    {
        public PromiseSource() { Promise = new Promise<T>(); }
        public Promise<T> Promise { get; }
        public bool IsSettled => Promise.IsSettled;
        public void Resolve(T value) => Promise.TryResolveValue(value);
        public bool TryResolve(T value) => Promise.TryResolveValue(value);
        public void Reject(Exception error) => Promise.TryRejectCore(error);
        public bool TryReject(Exception error) => Promise.TryRejectCore(error);
    }

    /// <summary>The exception `await` throws for a rejected promise; InnerException is the rejection.</summary>
    public sealed class PromiseRejectedException : Exception
    {
        public PromiseRejectedException(Exception inner) : base(inner?.Message ?? "Promise rejected", inner) { }
    }

    public readonly struct PromiseAwaiter : ICriticalNotifyCompletion
    {
        private readonly Promise _promise;
        public PromiseAwaiter(Promise promise) { _promise = promise; }

        /// <summary>Always false: `await` yields to the microtask queue even when the promise has settled (JS semantics).</summary>
        public bool IsCompleted => false;

        public void OnCompleted(Action continuation) => _promise.OnSettled(continuation);
        public void UnsafeOnCompleted(Action continuation) => _promise.OnSettled(continuation);

        public void GetResult()
        {
            Exception e = _promise.Observe();
            if (_promise.IsRejected) throw Unwrap(e);
            if (_promise.IsPending) throw new InvalidOperationException("Promise: GetResult before it settled.");
        }

        internal static Exception Unwrap(Exception e) => e is PromiseRejectedException r && r.InnerException != null ? r.InnerException : e;
    }

    public readonly struct PromiseAwaiter<T> : ICriticalNotifyCompletion
    {
        private readonly Promise<T> _promise;
        public PromiseAwaiter(Promise<T> promise) { _promise = promise; }

        public bool IsCompleted => false;

        public void OnCompleted(Action continuation) => _promise.OnSettled(continuation);
        public void UnsafeOnCompleted(Action continuation) => _promise.OnSettled(continuation);

        public T GetResult()
        {
            Exception e = _promise.Observe();
            if (_promise.IsRejected) throw PromiseAwaiter.Unwrap(e);
            if (_promise.IsPending) throw new InvalidOperationException("Promise: GetResult before it settled.");
            return _promise.Value;
        }
    }

    // ---- async method builders ----------------------------------------------------------------------
    //
    // The state machine is boxed once, on its first incomplete await, into a Runner; every later await reuses
    // that runner (the builder inside the boxed copy holds it). The promise is created in Create(), so every
    // copy of the builder points at the same one.

    internal interface IRunner { Action MoveNext { get; } }

    internal sealed class Runner<TStateMachine> : IRunner where TStateMachine : IAsyncStateMachine
    {
        public TStateMachine StateMachine;
        private Action _moveNext;
        public Action MoveNext => _moveNext ??= () => StateMachine.MoveNext();
    }

    public struct PromiseMethodBuilder
    {
        private Promise _promise;
        private IRunner _runner;

        public static PromiseMethodBuilder Create() => new PromiseMethodBuilder { _promise = new Promise() };

        public Promise Task => _promise;

        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
            => stateMachine.MoveNext();

        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

        public void SetResult() => _promise.TryResolveCore();

        public void SetException(Exception exception) => _promise.TryRejectCore(PromiseAwaiter.Unwrap(exception));

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine
            => awaiter.OnCompleted(RunnerOf(ref stateMachine));

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine
            => awaiter.UnsafeOnCompleted(RunnerOf(ref stateMachine));

        private Action RunnerOf<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            if (_runner == null)
            {
                var runner = new Runner<TStateMachine>();
                _runner = runner;                       // set on the builder inside `stateMachine` before the copy below
                runner.StateMachine = stateMachine;     // the boxed copy carries the runner too
            }
            return _runner.MoveNext;
        }
    }

    public struct PromiseMethodBuilder<T>
    {
        private Promise<T> _promise;
        private IRunner _runner;

        public static PromiseMethodBuilder<T> Create() => new PromiseMethodBuilder<T> { _promise = new Promise<T>() };

        public Promise<T> Task => _promise;

        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
            => stateMachine.MoveNext();

        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

        public void SetResult(T result) => _promise.TryResolveValue(result);

        public void SetException(Exception exception) => _promise.TryRejectCore(PromiseAwaiter.Unwrap(exception));

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine
            => awaiter.OnCompleted(RunnerOf(ref stateMachine));

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine
            => awaiter.UnsafeOnCompleted(RunnerOf(ref stateMachine));

        private Action RunnerOf<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            if (_runner == null)
            {
                var runner = new Runner<TStateMachine>();
                _runner = runner;
                runner.StateMachine = stateMachine;
            }
            return _runner.MoveNext;
        }
    }
}
