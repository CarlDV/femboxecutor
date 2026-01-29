using System;
using System.Globalization;
using System.Windows.Data;

namespace VelocityExecutor.Converters;

public class ScrollOffsetConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values.Length < 4)
		{
			return 0.0;
		}
		try
		{
			double horizontalOffset = (double)values[0];
			double viewportWidth = (double)values[1];
			double extentWidth = (double)values[2];
			double actualWidth = (double)values[3];
			if (extentWidth <= viewportWidth)
			{
				return 0.0;
			}
			double ratio = viewportWidth / extentWidth;
			double num = actualWidth - actualWidth * ratio;
			double scrollRatio = horizontalOffset / (extentWidth - viewportWidth);
			return num * scrollRatio;
		}
		catch
		{
			return 0.0;
		}
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
