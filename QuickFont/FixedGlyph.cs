using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace QuickFont {

    public sealed class FixedGlyph {

        public Bitmap this[float key] {
            get {
                int max = 0;
                foreach (var entry in _bitmaps) {
                    if (entry.Key > max && entry.Key <= key) {
                        max = entry.Key;
                    }
                }

                return _bitmaps[max];
            }
        }

        Dictionary<int, Bitmap> _bitmaps = new Dictionary<int, Bitmap>();

        public FixedGlyph(Bitmap initial) {
            _bitmaps[0] = initial;
        }

        public void AddBitmap(int minimumSize, Bitmap bitmap) {
            _bitmaps[minimumSize] = bitmap;
        }
    }
}
