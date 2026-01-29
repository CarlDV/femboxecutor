using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace VelocityExecutor.Behaviors;

public static class SmoothScrollBehavior
{
	private static DispatcherTimer _scrollTimer;

	private static ScrollViewer _activeScrollViewer;

	private static double _targetOffset;

	private static double _currentVelocity;

	private static bool _isVerticalScroll;

	public static readonly DependencyProperty EnableSmoothScrollProperty = DependencyProperty.RegisterAttached("EnableSmoothScroll", typeof(bool), typeof(SmoothScrollBehavior), new PropertyMetadata(false, OnEnableSmoothScrollChanged));

	public static bool GetEnableSmoothScroll(DependencyObject obj)
	{
		return (bool)obj.GetValue(EnableSmoothScrollProperty);
	}

	public static void SetEnableSmoothScroll(DependencyObject obj, bool value)
	{
		obj.SetValue(EnableSmoothScrollProperty, value);
	}

	private static void OnEnableSmoothScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is ScrollViewer scrollViewer)
		{
			if ((bool)e.NewValue)
			{
				scrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
			}
			else
			{
				scrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
			}
		}
	}

	private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (!(sender is ScrollViewer scrollViewer))
		{
			return;
		}
		e.Handled = true;
		bool hasVertical = scrollViewer.ScrollableHeight > 0.0;
		bool hasHorizontal = scrollViewer.ScrollableWidth > 0.0;
		if (hasVertical || hasHorizontal)
		{
			_isVerticalScroll = hasVertical;
			double delta = (double)e.Delta * 0.8;
			if (_isVerticalScroll)
			{
				double currentOffset = scrollViewer.VerticalOffset;
				_targetOffset = Math.Max(0.0, Math.Min(scrollViewer.ScrollableHeight, currentOffset - delta));
			}
			else
			{
				double currentOffset2 = scrollViewer.HorizontalOffset;
				_targetOffset = Math.Max(0.0, Math.Min(scrollViewer.ScrollableWidth, currentOffset2 - delta));
			}
			_activeScrollViewer = scrollViewer;
			if (_scrollTimer == null)
			{
				_scrollTimer = new DispatcherTimer
				{
					Interval = TimeSpan.FromMilliseconds(8.333333333333334)
				};
				_scrollTimer.Tick += ScrollTimer_Tick;
			}
			if (!_scrollTimer.IsEnabled)
			{
				_scrollTimer.Start();
			}
		}
	}

	private static void ScrollTimer_Tick(object sender, EventArgs e)
	{
		if (_activeScrollViewer == null)
		{
			_scrollTimer?.Stop();
			return;
		}
		double currentOffset = (_isVerticalScroll ? _activeScrollViewer.VerticalOffset : _activeScrollViewer.HorizontalOffset);
		double difference = _targetOffset - currentOffset;
		if (Math.Abs(difference) < 0.5)
		{
			if (_isVerticalScroll)
			{
				_activeScrollViewer.ScrollToVerticalOffset(_targetOffset);
			}
			else
			{
				_activeScrollViewer.ScrollToHorizontalOffset(_targetOffset);
			}
			_scrollTimer.Stop();
			_currentVelocity = 0.0;
		}
		else
		{
			double step = difference * 0.18;
			if (_isVerticalScroll)
			{
				_activeScrollViewer.ScrollToVerticalOffset(currentOffset + step);
			}
			else
			{
				_activeScrollViewer.ScrollToHorizontalOffset(currentOffset + step);
			}
		}
	}
}
