using System;
using System.Globalization;
using System.Windows.Data;

namespace VelocityExecutor.Converters;

public class ScrollProgressConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values.Length < 3)
		{
			return 100.0;
		}
		try
		{
			double viewportWidth = (double)values[0];
			double extentWidth = (double)values[1];
			double actualWidth = (double)values[2];
			if (extentWidth <= 0.0 || viewportWidth <= 0.0)
			{
				return actualWidth;
			}
			double ratio = viewportWidth / extentWidth;
			return Math.Max(20.0, actualWidth * ratio);
		}
		catch
		{
			return 100.0;
		}
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
