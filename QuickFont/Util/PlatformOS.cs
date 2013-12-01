using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QuickFont.Util {
    [Flags]
    public enum PlatformOS : int {
        None = 0,
        Windows = 1 << 0,
        Unix = 1 << 1,
        MacOS = 1 << 2
    }

}
