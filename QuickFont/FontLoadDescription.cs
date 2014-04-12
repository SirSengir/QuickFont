using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace QuickFont {

    public enum FontLoadMethod {
        FontObject,
        FontFile,
        QFontFile
    }

    /// <summary>
    /// Describes how a font was loaded so that it can be reloaded
    /// </summary>
    public sealed class FontLoadDescription {
        public FontLoadMethod Method { get; private set; }

        public FontResources Resources { get; private set; }

        public float Size { get; private set; }

        public FontStyle Style { get; private set; }

        public QFontBuilderConfiguration BuilderConfig { get; private set; }

        public float DownSampleFactor { get; private set; }

        public QFontLoaderConfiguration LoaderConfig { get; private set; }

        public FontLoadDescription (FontResources resources, float downSampleFactor, QFontLoaderConfiguration loaderConfig) {
            Method = FontLoadMethod.QFontFile;

            Resources = resources;
            DownSampleFactor = downSampleFactor;
            LoaderConfig = loaderConfig;
        }

        public FontLoadDescription (FontResources resources, float size, FontStyle style, QFontBuilderConfiguration builderConfig) {
            Method = FontLoadMethod.FontFile;

            Resources = resources;
            Size = size;
            Style = style;
            BuilderConfig = builderConfig;
        }

        public FontLoadDescription (Font font, QFontBuilderConfiguration config) {
            Method = FontLoadMethod.FontObject;
            //we don't reload fonts loaded direct from a font object...
        }
    }
}
