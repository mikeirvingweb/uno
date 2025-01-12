﻿#nullable enable

using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Core;

namespace Uno.Helpers;

/// <summary>
/// Handles completion of deferrals. The deferred action is completed when all deferral objects that were taken have called Complete().
/// </summary>
/// <typeparam name="T"></typeparam>
internal class DeferralManager<T>
{
	private readonly Func<DeferralCompletedHandler, T> _deferralFactory;
	private readonly TaskCompletionSource<object?> _allDeferralsCompletedCompletionSource = new();

	/// <summary>
	/// Start the count at 1, this ensures the deferral won't be completed until all subscribers to the corresponding event have had a
	/// chance to take out a deferral object.
	/// </summary>
	private int _deferralsCount = 1;

	public DeferralManager(Func<DeferralCompletedHandler, T> deferralFactory)
	{
		_deferralFactory = deferralFactory ?? throw new ArgumentNullException(nameof(deferralFactory));
	}

	internal event EventHandler? Completed;

	internal bool CompletedSynchronously { get; set; }

	public T GetDeferral()
	{
		_deferralsCount++;
		var isCompleted = false;
		return _deferralFactory(OnDeferralCompleted);

		void OnDeferralCompleted()
		{
#if !__WASM__ // Disable check on WASM until threading is supported
			CoreDispatcher.CheckThreadAccess();
#endif
			if (isCompleted)
			{
				throw new InvalidOperationException("Deferral already completed.");
			}

			isCompleted = true;
			DeferralCompleted();
		}
	}

	/// <summary>
	/// This marks the deferral as ready for completion.
	/// Must be called after the related event finished invoking.
	/// In case the operation is not deferred, it will also synchronously raise
	/// the Completed event.
	/// </summary>
	/// <returns>A value indicating whether the deferral completed synchronously.</returns>
	internal bool EventRaiseCompleted()
	{
		CompletedSynchronously = DeferralCompleted();
		return CompletedSynchronously;
	}

	internal Task WhenAllCompletedAsync() => _allDeferralsCompletedCompletionSource.Task;

	private bool DeferralCompleted()
	{
		_deferralsCount--;
		if (_deferralsCount <= 0)
		{
			Completed?.Invoke(this, EventArgs.Empty);
			_allDeferralsCompletedCompletionSource.TrySetResult(null);
			return true;
		}

		return false;
	}
}
