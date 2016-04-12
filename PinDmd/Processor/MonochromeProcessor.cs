﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using PixelFormat = System.Windows.Media.PixelFormat;

namespace PinDmd.Processor
{
	public class MonochromeProcessor : IProcessor
	{
		public bool Enabled { get; set; }
		public Color Color { get; set; }
		public PixelFormat PixelFormat { get; set; } = PixelFormats.Gray8;

		public BitmapSource Process(BitmapSource bmp)
		{
			var monochrome = new FormatConvertedBitmap();

			monochrome.BeginInit();
			monochrome.Source = bmp;
			monochrome.DestinationFormat = PixelFormat;
			monochrome.EndInit();

			return Color.A > 0 ? ColorShade(monochrome, Color) : monochrome;
		}

		public static BitmapSource ColorShade(BitmapSource bmp, Color color)
		{
			// convert back to rgb24
			var colored = new FormatConvertedBitmap();
			colored.BeginInit();
			colored.Source = bmp;
			colored.DestinationFormat = PixelFormats.Bgr32;
			colored.EndInit();

			var bytesPerPixel = (colored.Format.BitsPerPixel + 7) / 8;
			var stride = colored.PixelWidth * bytesPerPixel;
			var pixelBuffer = new byte[stride * colored.PixelHeight];
			var fullRect = new Int32Rect { X = 0, Y = 0, Width = colored.PixelWidth, Height = colored.PixelHeight };
			
			colored.CopyPixels(fullRect, pixelBuffer, stride, 0);

			for (var k = 0; k + 4 < pixelBuffer.Length; k += 4) {
				var blue = pixelBuffer[k] * color.ScB;
				var green = pixelBuffer[k + 1] * color.ScG;
				var red = pixelBuffer[k + 2] * color.ScR;

				if (blue < 0) { blue = 0; }
				if (green < 0) { green = 0; }
				if (red < 0) { red = 0; }

				pixelBuffer[k] = (byte)blue;
				pixelBuffer[k + 1] = (byte)green;
				pixelBuffer[k + 2] = (byte)red;
			}

			var dest = new WriteableBitmap(colored);
			dest.WritePixels(fullRect, pixelBuffer, stride, 0);

			return dest;
		}
	}
}
