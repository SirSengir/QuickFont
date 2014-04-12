using System;
using System.IO;

namespace QuickFont {

    /// <summary>
    /// Supplies the streams needed for (re-)loading a font.
    /// </summary>
    public abstract class FontResources {
        public Stream GetResource () {
            return GetResource (string.Empty);
        }

        /// <summary>
        /// Supplies a stream for the given FontLoadMethod and identifier.
        /// </summary>
        /// <value>The resources.</value>
        public abstract Stream GetResource (string ident);
    }

    /// <summary>
    /// The default implementation of FontResources.
    /// </summary>
    public sealed class FontResourcesDefault : FontResources {
        public override Stream GetResource (string ident) {
            if (string.IsNullOrEmpty (ident))
                return File.OpenRead (_path);
            else
                return File.OpenRead (_prefix + ident);
        }

        string _path;
        string _prefix;

        public FontResourcesDefault (string path) {
            _path = path;
            _prefix = path.Replace (".qfont", "").Replace (" ", "");
        }
    }
}

