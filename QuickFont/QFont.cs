using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Imaging;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using System.Runtime.InteropServices;
using System.IO;

namespace QuickFont {

    public sealed class QFont : IDisposable {
        Stack<QFontRenderOptions> optionsStack = new Stack<QFontRenderOptions> ();

        public QFontData FontData {
            get;
            private set;
        }

        private FontLoadDescription fontLoadDescription;

        public QFontRenderOptions Options {
            get {

                if (optionsStack.Count == 0) {
                    optionsStack.Push (new QFontRenderOptions ());
                }

                return optionsStack.Peek (); 
            }
            set { //not sure if we should even allow this...
                optionsStack.Pop ();
                optionsStack.Push (value);
            }
        }

        #region Constructors and font builders

        private QFont () {
        }

        internal QFont (QFontData fontData) {
            this.FontData = fontData;
        }

        public QFont (FontLoadDescription loadDescription) {
            fontLoadDescription = loadDescription;
            if (fontLoadDescription.Method == FontLoadMethod.FontFile)
                LoadQFontFromFontFile (fontLoadDescription);
            else if (fontLoadDescription.Method == FontLoadMethod.QFontFile)
                LoadQFontFromQFontFile (fontLoadDescription);
            else
                throw new SystemException ("Direct loading is not supported for load method: " + fontLoadDescription.Method);
        }

        public QFont (Font font)
			: this (font, null) {
        }

        public QFont (Font font, QFontBuilderConfiguration config) {
            if (config == null)
                config = new QFontBuilderConfiguration ();

            FontData = BuildFont (font, config, null);

            if (config.ShadowConfig != null)
                Options.DropShadowActive = true;
        }

        public QFont (string fileName, float size)
			: this (fileName, size, FontStyle.Regular, null) {
        }

        public QFont (string fileName, float size, FontStyle style)
			: this (fileName, size, style, null) {
        }

        public QFont (string fileName, float size, QFontBuilderConfiguration config)
			: this (fileName, size, FontStyle.Regular, config) {
        }

        public QFont (string fileName, float size, FontStyle style, QFontBuilderConfiguration config) {
            fontLoadDescription = new FontLoadDescription (new FontResourcesDefault (fileName), size, style, config);
            LoadQFontFromFontFile (fontLoadDescription);
        }

        #endregion

        #region Redbuilding and Loading

        private void LoadQFontFromFontFile (FontLoadDescription loadDescription) {
            QFontBuilderConfiguration config = loadDescription.BuilderConfig;
            float size = loadDescription.Size;
            FontStyle style = loadDescription.Style;

            if (config == null)
                config = new QFontBuilderConfiguration ();

            //TransformViewport? transToVp = null;
            float fontScale = 1f;
            //if (config.TransformToCurrentOrthogProjection)
            //    transToVp = OrthogonalTransform (out fontScale);

            //dont move this into a separate method - it needs to stay in scope!
            PrivateFontCollection pfc = new PrivateFontCollection ();

            // Marshall the font data and load it from memory.
            using (Stream stream = loadDescription.Resources.GetResource ()) {
                int streamlength = (int)stream.Length;
                byte[] fontdata = new byte[streamlength];
                stream.Read (fontdata, 0, streamlength);
                stream.Close ();

                IntPtr marshalled = Marshal.AllocCoTaskMem (streamlength);
                Marshal.Copy (fontdata, 0, marshalled, streamlength);

                pfc.AddMemoryFont (marshalled, streamlength);

                Marshal.FreeCoTaskMem (marshalled);
            }
            var fontFamily = pfc.Families [0];

            if (!fontFamily.IsStyleAvailable (style))
                throw new ArgumentException ("Chosen font file does not support style: " + style);


            Font font = new Font (fontFamily, size * fontScale * config.SuperSampleLevels, style);
            //var font = ObtainFont(fileName, size * fontScale * config.SuperSampleLevels, style)
            FontData = BuildFont (font, config, null);
            FontData.ScaleDueToTransformToViewport = fontScale;
            font.Dispose ();

            if (config.ShadowConfig != null)
                Options.DropShadowActive = true;
            //if (transToVp != null)
            //    Options.TransformToViewport = transToVp;
        }

        private void LoadQFontFromQFontFile (FontLoadDescription loadDescription) {
            QFontLoaderConfiguration loaderConfig = loadDescription.LoaderConfig;
            float downSampleFactor = loadDescription.DownSampleFactor;


            if (loaderConfig == null)
                loaderConfig = new QFontLoaderConfiguration ();

            //TransformViewport? transToVp = null;
            float fontScale = 1f;
            //if (loaderConfig.TransformToCurrentOrthogProjection)
            //    transToVp = OrthogonalTransform (out fontScale);

            FontData = Builder.LoadQFontDataFromFile (loadDescription, downSampleFactor * fontScale, loaderConfig);
            FontData.ScaleDueToTransformToViewport = fontScale;

            if (loaderConfig.ShadowConfig != null)
                Options.DropShadowActive = true;
            //if (transToVp != null)
            //    Options.TransformToViewport = transToVp;
        }

        public static QFont FromQFontFile (string filePath) {
            return FromQFontFile (filePath, 1.0f, null);
        }

        public static QFont FromQFontFile (string filePath, QFontLoaderConfiguration loaderConfig) {
            return FromQFontFile (filePath, 1.0f, loaderConfig);
        }

        public static QFont FromQFontFile (string filePath, float downSampleFactor) {
            return FromQFontFile (filePath, downSampleFactor, null);
        }

        public static QFont FromQFontFile (string filePath, float downSampleFactor, QFontLoaderConfiguration loaderConfig) {
            QFont qfont = new QFont ();
            qfont.fontLoadDescription = new FontLoadDescription (new FontResourcesDefault (filePath), downSampleFactor, loaderConfig);
            qfont.LoadQFontFromQFontFile (qfont.fontLoadDescription);
            return qfont;
        }

        private static QFontData BuildFont (Font font, QFontBuilderConfiguration config, string saveName) {
            Builder builder = new Builder (font, config);
            return builder.BuildFontData (saveName);
        }

        #endregion

        #region QFont File Creation

        public static void CreateTextureFontFiles (Font font, string newFontName) {
            CreateTextureFontFiles (font, newFontName, new QFontBuilderConfiguration ());
        }

        public static void CreateTextureFontFiles (Font font, string newFontName, QFontBuilderConfiguration config) {
            var fontData = BuildFont (font, config, newFontName);
            Builder.SaveQFontDataToFile (fontData, newFontName);
        }

        public static void CreateTextureFontFiles (string fileName, float size, string newFontName) {
            CreateTextureFontFiles (fileName, size, FontStyle.Regular, null, newFontName);
        }

        public static void CreateTextureFontFiles (string fileName, float size, FontStyle style, string newFontName) {
            CreateTextureFontFiles (fileName, size, style, null, newFontName);
        }

        public static void CreateTextureFontFiles (string fileName, float size, QFontBuilderConfiguration config, string newFontName) {
            CreateTextureFontFiles (fileName, size, FontStyle.Regular, config, newFontName);
        }

        public static void CreateTextureFontFiles (string fileName, float size, FontStyle style, QFontBuilderConfiguration config, string newFontName) {

            QFontData fontData = null;
            if (config == null)
                config = new QFontBuilderConfiguration ();


            //dont move this into a separate method - it needs to stay in scope!
            PrivateFontCollection pfc = new PrivateFontCollection ();
            pfc.AddFontFile (fileName);
            var fontFamily = pfc.Families [0];

            if (!fontFamily.IsStyleAvailable (style))
                throw new ArgumentException ("Font file: " + fileName + " does not support style: " + style);

            var font = new Font (fontFamily, size * config.SuperSampleLevels, style);
            //var font = ObtainFont(fileName, size * config.SuperSampleLevels, style);
            try {
                fontData = BuildFont (font, config, newFontName);
            } finally {
                if (font != null)
                    font.Dispose ();
            }

            Builder.SaveQFontDataToFile (fontData, newFontName);

        }

        #endregion

        public float LineSpacing {
            get { return (float)Math.Ceiling (FontData.MaxGlyphHeight * Options.LineSpacing); }
        }

        public bool IsMonospacingActive {
            get { return FontData.IsMonospacingActive (Options); }
        }

        public float MonoSpaceWidth {
            get { return FontData.GetMonoSpaceWidth (Options); }
        }

        /// <summary>
        /// Dispose of the QFont data.
        /// </summary>
        public void Dispose () {
            FontData.Dispose ();
        }
    }
}
