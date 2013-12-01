using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Runtime.InteropServices;

namespace QuickFont.Util {
	public sealed class PlatformUtils {

		#region Platform
		/// <summary>
		/// Attempts to detect the platform the application is currently running on.
		/// </summary>
		/// <remarks>>Will take into account Mono's bizarre decision to continue identifying MacOS as Unix.</remarks>
		/// <returns>The platform.</returns>
		public static PlatformOS DeterminePlatform () {
			switch (Environment.OSVersion.Platform) {
			case PlatformID.Unix:
				return IsRunningOnMac() ? PlatformOS.MacOS : PlatformOS.Unix;
			case PlatformID.MacOSX:
				return PlatformOS.MacOS;
			default:
				return PlatformOS.Windows;
			}
		}

		private static bool IsRunningOnMac () {
			IntPtr buf = IntPtr.Zero;
			try {
				buf = Marshal.AllocHGlobal (8192);
				if (uname (buf) == 0) {
					string os = Marshal.PtrToStringAnsi (buf);
					if (os == "Darwin")
						return true;
				}
			} catch {
			} finally {
				if (buf != IntPtr.Zero)
					Marshal.FreeHGlobal (buf);
			}
			return false;
		}

		[DllImport ("libc")]
		static extern int uname (IntPtr buf);
		#endregion

	}
}
