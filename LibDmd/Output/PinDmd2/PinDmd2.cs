﻿using System;
using System.Windows;
using System.Windows.Media.Imaging;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using NLog;
using LibDmd.Common;

namespace LibDmd.Output.PinDmd2
{
	/// <summary>
	/// Output target for PinDMD2 devices.
	/// </summary>
	/// <see cref="http://pindmd.com/"/>
	public class PinDmd2 : IFrameDestination
	{
		public bool IsAvailable { get; private set; }
		public bool IsRgb { get; } = false;

		public int Width { get; } = 128;
		public int Height { get; } = 32;

		private UsbDevice _pinDmd2Device;
		private readonly byte[] _frameBuffer;

		private static PinDmd2 _instance;
		private static readonly UsbDeviceFinder PinDmd2Finder = new UsbDeviceFinder(0x0314, 0xe457);
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private PinDmd2()
		{
			// 4 bits per pixel plus 4 init bytes
			var size = (Width * Height / 2) + 4;
			_frameBuffer = new byte[size];
			_frameBuffer[0] = 0x81;    // frame sync bytes
			_frameBuffer[1] = 0xC3;
			_frameBuffer[2] = 0xE7;
			_frameBuffer[3] = 0x0;
		}

		public void Init()
		{
			// find and open the usb device.
			_pinDmd2Device = UsbDevice.OpenUsbDevice(PinDmd2Finder);

			// if the device is open and ready
			if (_pinDmd2Device == null) {
				Logger.Debug("PinDMDv2 device not found.");
				IsAvailable = false;
				return;
			}
			if (_pinDmd2Device.Info.ProductString.Contains("pinDMD V2")) {
				Logger.Info("Found PinDMDv2 device.");
				Logger.Debug("   Manufacturer: {0}", _pinDmd2Device.Info.ManufacturerString);
				Logger.Debug("   Product:      {0}", _pinDmd2Device.Info.ProductString);
				Logger.Debug("   Serial:       {0}", _pinDmd2Device.Info.SerialString);
				Logger.Debug("   Language ID:  {0}", _pinDmd2Device.Info.CurrentCultureLangID);

			} else {
				Logger.Debug("Device found but it's not a PinDMDv2 device ({0}).", _pinDmd2Device.Info.ProductString);
				IsAvailable = false;
				return;
			}

			var usbDevice = _pinDmd2Device as IUsbDevice;
			if (!ReferenceEquals(usbDevice, null)) {
				usbDevice.SetConfiguration(1);
				usbDevice.ClaimInterface(0);
			}
			IsAvailable = true;
		}

		/// <summary>
		/// Returns the current instance of the PinDMD2 API. In any case,
		/// the instance get (re-)initialized.
		/// </summary>
		/// <returns></returns>
		public static PinDmd2 GetInstance()
		{
			if (_instance == null) {
				_instance = new PinDmd2();
			}
			_instance.Init();
			return _instance;
		}

		/// <summary>
		/// Renders an image to the display.
		/// </summary>
		/// <param name="bmp">Any bitmap</param>
		public void Render(BitmapSource bmp)
		{
			if (!IsAvailable) {
				throw new SourceNotAvailableException();
			}
			if (bmp.PixelWidth != Width || bmp.PixelHeight != Height) {
				throw new Exception($"Image must have the same dimensions as the display ({Width}x{Height}).");
			}

			var bytesPerPixel = (bmp.Format.BitsPerPixel + 7) / 8;
			var bytes = new byte[bytesPerPixel];
			var rect = new Int32Rect(0, 0, 1, 1);
			var byteIdx = 4;

			for (var y = 0; y < Height; y++) {
				for (var x = 0; x < Width; x += 8) {
					byte bd0 = 0;
					byte bd1 = 0;
					byte bd2 = 0;
					byte bd3 = 0;
					for (var v = 7; v >= 0; v--) {

						rect.X = x + v;
						rect.Y = y;
						bmp.CopyPixels(rect, bytes, bytesPerPixel, 0);

						// convert to HSL
						double hue;
						double saturation;
						double luminosity;
						ColorUtil.RgbToHsl(bytes[2], bytes[1], bytes[0], out hue, out saturation, out luminosity);

						var pixel = (byte)(luminosity * 255d);

						bd0 <<= 1;
						bd1 <<= 1;
						bd2 <<= 1;
						bd3 <<= 1;

						if ((pixel & 16) != 0) {
							bd0 |= 1;
						}
						if ((pixel & 32) != 0) {
							bd1 |= 1;
						}
						if ((pixel & 64) != 0) {
							bd2 |= 1;
						}
						if ((pixel & 128) != 0) {
							bd3 |= 1;
						}
					}
					_frameBuffer[byteIdx] = bd0;
					_frameBuffer[byteIdx + 512] = bd1;
					_frameBuffer[byteIdx + 1024] = bd2;
					_frameBuffer[byteIdx + 1536] = bd3;
					byteIdx++;
				}
			}

			var writer = _pinDmd2Device.OpenEndpointWriter(WriteEndpointID.Ep01);
			int bytesWritten;
			var error = writer.Write(_frameBuffer, 2000, out bytesWritten);
			if (error != ErrorCode.None) {
				Logger.Error("Error sending data to device: {0}", UsbDevice.LastErrorString);
				throw new Exception(UsbDevice.LastErrorString);
			}
		}

		public void Dispose()
		{
			if (_pinDmd2Device != null) {
				if (_pinDmd2Device.IsOpen) {
					var wholeUsbDevice = _pinDmd2Device as IUsbDevice;
					if (!ReferenceEquals(wholeUsbDevice, null)) {
						wholeUsbDevice.ReleaseInterface(0);
					}
					_pinDmd2Device.Close();
				}
			}
			_pinDmd2Device = null;
			UsbDevice.Exit();
		}
	}
}
