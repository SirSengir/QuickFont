using System;
using System.Collections.Generic;
using System.Text;

namespace QuickFont {

    /// <summary>
    /// The configuration used when loading a font from a qfont file.
    /// </summary>
    public class QFontLoaderConfiguration : QFontConfiguration {
        public QFontLoaderConfiguration () {
        }

        public QFontLoaderConfiguration (bool addDropShadow) {
            if (addDropShadow)
                this.ShadowConfig = new QFontShadowConfiguration ();
        }
    }
}
