﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace QuickFont {

    public sealed class QFontData : IDisposable {
        #region Properties

        /// <summary>
        /// Number of pages contained in the font.
        /// </summary>
        public int PageCount {
            get;
            private set;
        }

        /// <summary>
        /// Gets the char set.
        /// </summary>
        /// <value>The char set.</value>
        public char[] CharSet {
            get;
            private set;
        }

        /// <summary>
        /// Mapping from a pair of characters to a pixel offset
        /// </summary>
        public Dictionary<String, int> KerningPairs {
            get;
            internal set;
        }

        /// <summary>
        /// List of texture pages
        /// </summary>
        public TexturePage[] Pages {
            get;
            set;
        }

        /// <summary>
        /// Mapping from character to glyph index
        /// </summary>
        public Dictionary<char, QFontGlyph> CharSetMapping {
            get;
            internal set;
        }

        /// <summary>
        /// The average glyph width
        /// </summary>
        public float MeanGlyphWidth {
            get;
            private set;
        }

        /// <summary>
        /// The maximum glyph height
        /// </summary>
        public int MaxGlyphHeight {
            get;
            private set;
        }

        /// <summary>
        /// Null if no dropShadow is available
        /// </summary>
        public QFont DropShadow {
            get;
            internal set;
        }

        /// <summary>
        /// Whether the original font (from ttf) was detected to be monospaced
        /// </summary>
        public bool NaturallyMonospaced {
            get;
            internal set;
        }

        /// <summary>
        /// The font scaling due to the font being transformed to the
        /// current viewport for consistent pixel-perfect size across
        /// any resolution
        /// </summary>
        private float _scaleDueToTransformToViewport = 1.0f;

        public float ScaleDueToTransformToViewport {
            get { return _scaleDueToTransformToViewport; }
            set { _scaleDueToTransformToViewport = value; }
        }

        #endregion

        public bool IsMonospacingActive (QFontRenderOptions options) {
            return (options.Monospacing == QFontMonospacing.Natural && NaturallyMonospaced) || options.Monospacing == QFontMonospacing.Yes; 
        }

        public float GetMonoSpaceWidth (QFontRenderOptions options) {
            return (float)Math.Ceiling (1 + (1 + options.CharacterSpacing) * MeanGlyphWidth);
        }

        #region Constructors

        internal QFontData () {
        }

        public QFontData (List<string> serialized) {
            Deserialize (serialized);
        }

        #endregion

        public List<String> Serialize () {
            var data = new List<String> ();


            data.Add ("" + Pages.Length);
            data.Add ("" + CharSetMapping.Count);

            foreach (var glyphChar in CharSetMapping) {
                var chr = glyphChar.Key;
                var glyph = glyphChar.Value;

                data.Add ("" + chr + " " +
                glyph.Page + " " +
                glyph.Rect.X + " " +
                glyph.Rect.Y + " " +
                glyph.Rect.Width + " " +
                glyph.Rect.Height + " " +
                glyph.YOffset + " " +
                glyph.SuppressColouring.ToString ()

                );
            }
            return data;
        }

        public void Deserialize (List<String> input) {
            CharSetMapping = new Dictionary<char, QFontGlyph> ();
            var charSetList = new List<char> ();

            try {
                PageCount = int.Parse (input [0]);
                int glyphCount = int.Parse (input [1]);

                for (int i = 0; i < glyphCount; i++) {
                    var vals = input [2 + i].Split (' ');
                    var glyph = new QFontGlyph (int.Parse (vals [1]), new Rectangle (int.Parse (vals [2]), int.Parse (vals [3]), int.Parse (vals [4]), int.Parse (vals [5])), int.Parse (vals [6]), vals [0] [0]) {
                        SuppressColouring = vals.Length > 7 ? bool.Parse (vals [7]) : false
                    };

                    CharSetMapping.Add (vals [0] [0], glyph);
                    charSetList.Add (vals [0] [0]);
                }


            } catch (Exception e) {
                throw new Exception ("Failed to parse qfont file. Invalid format.", e);
            }

            CharSet = charSetList.ToArray ();

        }

        public void CalculateMeanWidth () {
            MeanGlyphWidth = 0f;
            foreach (var glyph in CharSetMapping)
                MeanGlyphWidth += glyph.Value.Rect.Width;

            MeanGlyphWidth /= CharSetMapping.Count;

        }

        public void CalculateMaxHeight () {
            MaxGlyphHeight = 0;
            foreach (var glyph in CharSetMapping)
                MaxGlyphHeight = Math.Max (glyph.Value.Rect.Height, MaxGlyphHeight);

        }

        public void Dispose () {
            foreach (TexturePage page in Pages)
                page.Dispose ();
        }
    }
}
