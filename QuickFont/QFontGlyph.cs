using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace QuickFont {

    public sealed class QFontGlyph {
        /// <summary>
        /// Which texture page the glyph is on
        /// </summary>
        public int Page {
            get;
            private set;
        }

        /// <summary>
        /// The rectangle defining the glyphs position on the page
        /// </summary>
        public Rectangle Rect {
            get;
            set;
        }

        /// <summary>
        /// How far the glyph would need to be vertically offset to be vertically in line with the tallest glyph in the set of all glyphs
        /// </summary>
        public int YOffset {
            get;
            set;
        }

        /// <summary>
        /// Which character this glyph represents
        /// </summary>
        public char Character {
            get;
            private set;
        }

        public QFontGlyph (int page, Rectangle rect, int yOffset, char character) {
            Page = page;
            Rect = rect;
            YOffset = yOffset;
            Character = character;
        }
    }
}
