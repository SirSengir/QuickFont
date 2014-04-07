using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Imaging;
using System.IO;
using OpenTK.Graphics.OpenGL;

namespace QuickFont {
	/// <summary>
	/// Class for building a Quick Font, given a Font
	/// and a configuration object.
	/// </summary>
	class Builder {

		private string charSet;
		private QFontBuilderConfiguration config;
		private Font font;

		public Builder (Font font, QFontBuilderConfiguration config) {
			this.charSet = config.CharSet;
			this.config = config;
			this.font = font;
            
		}

		private static Dictionary<char, QFontGlyph> CreateCharGlyphMapping (QFontGlyph[] glyphs) {
			Dictionary<char, QFontGlyph> dict = new Dictionary<char, QFontGlyph> ();
			for (int i = 0; i < glyphs.Length; i++)
				dict.Add (glyphs [i].Character, glyphs [i]);

			return dict;
		}

		//these do not affect the actual width of glyphs (we measure widths pixel-perfectly ourselves), but is used to detect whether a font is monospaced
		private List<SizeF> GetGlyphSizes (Font font) {
			Bitmap bmp = new Bitmap (512, 512, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
			Graphics graph = Graphics.FromImage (bmp);
			List<SizeF> sizes = new List<SizeF> ();

			for (int i = 0; i < charSet.Length; i++) {
				var charSize = graph.MeasureString ("" + charSet [i], font);
				sizes.Add (new SizeF (charSize.Width, charSize.Height));
			}

			graph.Dispose ();
			bmp.Dispose ();

			return sizes;
		}

		private SizeF GetMaxGlyphSize (List<SizeF> sizes) {
			SizeF maxSize = new SizeF (0f, 0f);
			for (int i = 0; i < charSet.Length; i++) {
				if (sizes [i].Width > maxSize.Width)
					maxSize.Width = sizes [i].Width;

				if (sizes [i].Height > maxSize.Height)
					maxSize.Height = sizes [i].Height;
			}

			return maxSize;
		}

		private SizeF GetMinGlyphSize (List<SizeF> sizes) {
			SizeF minSize = new SizeF (float.MaxValue, float.MaxValue);
			for (int i = 0; i < charSet.Length; i++) {
				if (sizes [i].Width < minSize.Width)
					minSize.Width = sizes [i].Width;

				if (sizes [i].Height < minSize.Height)
					minSize.Height = sizes [i].Height;
			}

			return minSize;
		}

		/// <summary>
		/// Returns true if all glyph widths are within 5% of each other
		/// </summary>
		/// <param name="sizes"></param>
		/// <returns></returns>
		private bool IsMonospaced (List<SizeF> sizes) {
			var min = GetMinGlyphSize (sizes);
			var max = GetMaxGlyphSize (sizes);

			if (max.Width - min.Width < max.Width * 0.05f)
				return true;

			return false;
		}

		//The initial bitmap is simply a long thin strip of all glyphs in a row
		private Bitmap CreateInitialBitmap (Font font, SizeF maxSize, int initialMargin, out QFontGlyph[] glyphs, TextGenerationRenderHint renderHint) {
			glyphs = new QFontGlyph[charSet.Length];

			int spacing = (int)Math.Ceiling (maxSize.Width) + 2 * initialMargin;
			Bitmap bmp = new Bitmap (spacing * charSet.Length, (int)Math.Ceiling (maxSize.Height) + 2 * initialMargin, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
			Graphics graph = Graphics.FromImage (bmp);

			switch (renderHint) {
			case TextGenerationRenderHint.SizeDependent: 
				graph.TextRenderingHint = font.Size <= 12.0f ? TextRenderingHint.ClearTypeGridFit : TextRenderingHint.AntiAlias; 
				break;
			case TextGenerationRenderHint.AntiAlias: 
				graph.TextRenderingHint = TextRenderingHint.AntiAlias; 
				break;
			case TextGenerationRenderHint.AntiAliasGridFit: 
				graph.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; 
				break;
			case TextGenerationRenderHint.ClearTypeGridFit:
				graph.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
				break;
			case TextGenerationRenderHint.SystemDefault:
				graph.TextRenderingHint = TextRenderingHint.SystemDefault;
				break;
			}

			int xOffset = initialMargin;
			for (int i = 0; i < charSet.Length; i++) {
				graph.DrawString ("" + charSet [i], font, Brushes.White, xOffset, initialMargin);
				var charSize = graph.MeasureString ("" + charSet [i], font);
				glyphs [i] = new QFontGlyph (0, new Rectangle (xOffset - initialMargin, 0, (int)charSize.Width + initialMargin * 2, (int)charSize.Height + initialMargin * 2), 0, charSet [i]);
				xOffset += (int)charSize.Width + initialMargin * 2;
			}

			graph.Flush ();
			graph.Dispose ();

			return bmp;
		}

		private delegate bool EmptyDel (byte[] data, int stride, int x, int y);

		private static void RetargetGlyphRectangleInwards (Bitmap bitmap, BitmapData bitmapData, QFontGlyph glyph, bool setYOffset, byte alphaTolerance) {
			int startX, endX;
			int startY, endY;

			Rectangle rect = glyph.Rect;

			// Marshal the bitmap data
			int size = bitmapData.Height * bitmapData.Stride;
			byte[] marshalled = new byte[size];
			System.Runtime.InteropServices.Marshal.Copy (bitmapData.Scan0, marshalled, 0, size);

			// Set the delegate to use for empty pixel search.
			EmptyDel emptyPix;
			if (bitmapData.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
				emptyPix = delegate(byte[] data, int stride, int x, int y) {
					return QBitmap.EmptyAlphaPixel (marshalled, bitmapData.Stride, x, y, alphaTolerance);
				};
			else
				emptyPix = delegate(byte[] data, int stride, int x, int y) {
					return QBitmap.EmptyPixel (marshalled, bitmapData.Stride, x, y);
				};

			for (startX = rect.X; startX < bitmapData.Width; startX++)
				for (int j = rect.Y; j < rect.Y + rect.Height; j++)
					if (!emptyPix (marshalled, bitmapData.Stride, startX, j))
						goto Done1;
			Done1:

			for (endX = rect.X + rect.Width; endX >= 0; endX--)
				for (int j = rect.Y; j < rect.Y + rect.Height; j++)
					if (!emptyPix (marshalled, bitmapData.Stride, endX, j))
						goto Done2;
			Done2:

			for (startY = rect.Y; startY < bitmapData.Height; startY++)
				for (int i = startX; i < endX; i++)
					if (!emptyPix (marshalled, bitmapData.Stride, i, startY))
						goto Done3;
                            
			Done3:

			for (endY = rect.Y + rect.Height; endY >= 0; endY--)
				for (int i = startX; i < endX; i++)
					if (!emptyPix (marshalled, bitmapData.Stride, i, endY))
						goto Done4;
			Done4:
			;

			if (endY < startY)
				startY = endY = rect.Y;

			if (endX < startX)
				startX = endX = rect.X;

			glyph.Rect = new Rectangle (startX, startY, endX - startX + 1, endY - startY + 1);

			if (setYOffset)
				glyph.YOffset = glyph.Rect.Y;

		}

		private static void RetargetGlyphRectangleOutwards (BitmapData bitmapData, QFontGlyph glyph, bool setYOffset, byte alphaTolerance) {
			int startX, endX;
			int startY, endY;

			Rectangle rect = glyph.Rect;

			// Marshal the bitmap data
			int size = bitmapData.Height * bitmapData.Stride;
			byte[] marshalled = new byte[size];
			System.Runtime.InteropServices.Marshal.Copy (bitmapData.Scan0, marshalled, 0, size);

			// Set the delegate to use for empty pixel search.
			EmptyDel emptyPix;
			if (bitmapData.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
				emptyPix = delegate(byte[] data, int stride, int x, int y) {
					return QBitmap.EmptyAlphaPixel (marshalled, bitmapData.Stride, x, y, alphaTolerance);
				};
			else
				emptyPix = delegate(byte[] data, int stride, int x, int y) {
					return QBitmap.EmptyPixel (marshalled, bitmapData.Stride, x, y);
				};


			for (startX = rect.X; startX >= 0; startX--) {
				bool foundPix = false;
				for (int j = rect.Y; j <= rect.Y + rect.Height; j++) {
					if (!emptyPix (marshalled, bitmapData.Stride, startX, j)) {
						foundPix = true;
						break;
					}
				}

				if (!foundPix) {
					startX++;
					break;
				}
			}


			for (endX = rect.X + rect.Width; endX < bitmapData.Width; endX++) {
				bool foundPix = false;
				for (int j = rect.Y; j <= rect.Y + rect.Height; j++) {
					if (!emptyPix (marshalled, bitmapData.Stride, endX, j)) {
						foundPix = true;
						break; 
					}
				}

				if (!foundPix) {
					endX--;
					break;
				}
			}

			for (startY = rect.Y; startY >= 0; startY--) {
				bool foundPix = false;
				for (int i = startX; i <= endX; i++) {
					if (!emptyPix (marshalled, bitmapData.Stride, i, startY)) {
						foundPix = true;
						break;
					}
				}

				if (!foundPix) {
					startY++;
					break;
				}
			}

			for (endY = rect.Y + rect.Height; endY < bitmapData.Height; endY++) {
				bool foundPix = false;
				for (int i = startX; i <= endX; i++) {
					if (!emptyPix (marshalled, bitmapData.Stride, i, endY)) {
						foundPix = true;
						break;
					}
				}

				if (!foundPix) {
					endY--;
					break;
				}
			}

			glyph.Rect = new Rectangle (startX, startY, endX - startX + 1, endY - startY + 1);

			if (setYOffset)
				glyph.YOffset = glyph.Rect.Y;
		}

		private static List<QBitmap> GenerateBitmapSheetsAndRepack (QFontGlyph[] sourceGlyphs, BitmapData[] sourceBitmaps, int destSheetWidth, int destSheetHeight, out QFontGlyph[] destGlyphs, int destMargin, bool usePowerOfTwo) {
			List<QBitmap> pages = new List<QBitmap> ();
			destGlyphs = new QFontGlyph[sourceGlyphs.Length];

			QBitmap currentPage = null;


			int maxY = 0;
			foreach (var glph in sourceGlyphs)
				maxY = Math.Max (glph.Rect.Height, maxY);


			int finalPageIndex = 0;
			int finalPageRequiredWidth = 0;
			int finalPageRequiredHeight = 0;

            
			for (int k = 0; k < 2; k++) {
				bool pre = k == 0;  //first iteration is simply to determine the required size of the final page, so that we can crop it in advance


				int xPos = 0;
				int yPos = 0;
				int maxYInRow = 0;
				int totalTries = 0;

				for (int i = 0; i < sourceGlyphs.Length; i++) {


					if (!pre && currentPage == null) {

						if (finalPageIndex == pages.Count) {
							int width = Math.Min (destSheetWidth, usePowerOfTwo ? PowerOfTwo (finalPageRequiredWidth) : finalPageRequiredWidth);
							int height = Math.Min (destSheetHeight, usePowerOfTwo ? PowerOfTwo (finalPageRequiredHeight) : finalPageRequiredHeight);

							currentPage = new QBitmap (new Bitmap (width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb));
							currentPage.Clear32 (255, 255, 255, 0); //clear to white, but totally transparent
						} else {
							currentPage = new QBitmap (new Bitmap (destSheetWidth, destSheetHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb));
							currentPage.Clear32 (255, 255, 255, 0); //clear to white, but totally transparent
						}
						pages.Add (currentPage);

					}
                


					totalTries++;

					if (totalTries > 10 * sourceGlyphs.Length)
						throw new Exception ("Failed to fit font into texture pages");


					Rectangle rect = sourceGlyphs [i].Rect;

					if (xPos + rect.Width + 2 * destMargin <= destSheetWidth && yPos + rect.Height + 2 * destMargin <= destSheetHeight) {
						if (!pre) {
							//add to page
							if (sourceBitmaps [sourceGlyphs [i].Page].PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
								QBitmap.Blit (sourceBitmaps [sourceGlyphs [i].Page], currentPage.bitmapData, rect.X, rect.Y, rect.Width, rect.Height, xPos + destMargin, yPos + destMargin);
							else
								QBitmap.BlitMask (sourceBitmaps [sourceGlyphs [i].Page], currentPage.bitmapData, rect.X, rect.Y, rect.Width, rect.Height, xPos + destMargin, yPos + destMargin);

							destGlyphs [i] = new QFontGlyph (pages.Count - 1, new Rectangle (xPos + destMargin, yPos + destMargin, rect.Width, rect.Height), sourceGlyphs [i].YOffset, sourceGlyphs [i].Character);
						} else {
							finalPageRequiredWidth = Math.Max (finalPageRequiredWidth, xPos + rect.Width + 2 * destMargin);
							finalPageRequiredHeight = Math.Max (finalPageRequiredHeight, yPos + rect.Height + 2 * destMargin);
						}


						xPos += rect.Width + 2 * destMargin;
						maxYInRow = Math.Max (maxYInRow, rect.Height);

						continue;
					}


					if (xPos + rect.Width + 2 * destMargin > destSheetWidth) {
						i--;

						yPos += maxYInRow + 2 * destMargin;
						xPos = 0;

						if (yPos + maxY + 2 * destMargin > destSheetHeight) {
							yPos = 0;

							if (!pre) {
								currentPage = null;
							} else {
								finalPageRequiredWidth = 0;
								finalPageRequiredHeight = 0;
								finalPageIndex++;
							}
						}
						continue;
					}

				}

			}

			return pages;
		}

		public QFontData BuildFontData () {
			return BuildFontData (null);
		}

		public QFontData BuildFontData (string saveName) {
			if (config.ForcePowerOfTwo && config.SuperSampleLevels != PowerOfTwo (config.SuperSampleLevels)) {
				throw new ArgumentOutOfRangeException ("SuperSampleLevels must be a power of two when using ForcePowerOfTwo.");
			}

			if (config.SuperSampleLevels <= 0 || config.SuperSampleLevels > 8) {
				throw new ArgumentOutOfRangeException ("SuperSampleLevels = [" + config.SuperSampleLevels + "] is an unsupported value. Please use values in the range [1,8]"); 
			}

			int margin = 2; //margin in initial bitmap (don't bother to make configurable - likely to cause confusion
			int pageWidth = config.PageWidth * config.SuperSampleLevels; //texture page width
			int pageHeight = config.PageHeight * config.SuperSampleLevels; //texture page height
			bool usePowerOfTwo = config.ForcePowerOfTwo;
			int glyphMargin = config.GlyphMargin * config.SuperSampleLevels;

			QFontGlyph[] initialGlyphs;
			List<SizeF> sizes = GetGlyphSizes (font);
			SizeF maxSize = GetMaxGlyphSize (sizes);
			Bitmap initialBmp = CreateInitialBitmap (font, maxSize, margin, out initialGlyphs, config.TextGenerationRenderHint);
			BitmapData initialBitmapData = initialBmp.LockBits (new Rectangle (0, 0, initialBmp.Width, initialBmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

			int minYOffset = int.MaxValue;
			foreach (QFontGlyph glyph in initialGlyphs) {
				RetargetGlyphRectangleInwards (initialBmp, initialBitmapData, glyph, true, config.KerningConfig.alphaEmptyPixelTolerance);
				minYOffset = Math.Min (minYOffset, glyph.YOffset);
			}
			minYOffset--; //give one pixel of breathing room?

			foreach (QFontGlyph glyph in initialGlyphs)
				glyph.YOffset -= minYOffset;
           
			QFontGlyph[] glyphs; 
			var bitmapPages = GenerateBitmapSheetsAndRepack (initialGlyphs, new BitmapData[1] { initialBitmapData }, pageWidth, pageHeight, out glyphs, glyphMargin, usePowerOfTwo);

			initialBmp.UnlockBits (initialBitmapData);
			initialBmp.Dispose ();

			if (config.SuperSampleLevels != 1) {
				ScaleSheetsAndGlyphs (bitmapPages, glyphs, 1.0f / config.SuperSampleLevels);
				RetargetAllGlyphs (bitmapPages, glyphs, config.KerningConfig.alphaEmptyPixelTolerance);
			}

			//create list of texture pages
			List<TexturePage> pages = new List<TexturePage> ();
			foreach (QBitmap page in bitmapPages)
				pages.Add (new TexturePage (page.bitmapData));

			var fontData = new QFontData ();
			fontData.CharSetMapping = CreateCharGlyphMapping (glyphs);
			fontData.Pages = pages.ToArray ();
			fontData.CalculateMeanWidth ();
			fontData.CalculateMaxHeight ();
			fontData.KerningPairs = KerningCalculator.CalculateKerning (charSet.ToCharArray (), glyphs, bitmapPages, config.KerningConfig);
			fontData.NaturallyMonospaced = IsMonospaced (sizes);

			if (saveName != null) {
				if (bitmapPages.Count == 1)
					bitmapPages [0].bitmap.Save (saveName + ".png", System.Drawing.Imaging.ImageFormat.Png);
				else {
					for (int i = 0; i < bitmapPages.Count; i++)
						bitmapPages [i].bitmap.Save (saveName + "_sheet_" + i + ".png", System.Drawing.Imaging.ImageFormat.Png);
				}
			}


			if (config.ShadowConfig != null)
				fontData.DropShadow = BuildDropShadow (bitmapPages, glyphs, config.ShadowConfig, charSet.ToCharArray (), config.KerningConfig.alphaEmptyPixelTolerance);

			foreach (QBitmap page in bitmapPages)
				page.Free ();

			//validate glyphs
			var intercept = FirstIntercept (fontData.CharSetMapping);
			if (intercept != null)
				throw new Exception ("Failed to create glyph set. Glyphs '" + intercept [0] + "' and '" + intercept [1] + "' were overlapping. This is could be due to an error in the font, or a bug in Graphics.MeasureString().");

            SetGLData(fontData);
			return fontData;

		}

		private static QFont BuildDropShadow (List<QBitmap> sourceFontSheets, QFontGlyph[] sourceFontGlyphs, QFontShadowConfiguration shadowConfig, char[] charSet, byte alphaTolerance) {

			QFontGlyph[] newGlyphs;

			List<BitmapData> sourceBitmapData = new List<BitmapData> ();
			foreach (QBitmap sourceSheet in sourceFontSheets)
				sourceBitmapData.Add (sourceSheet.bitmapData);
            
			List<QBitmap> bitmapSheets = GenerateBitmapSheetsAndRepack (sourceFontGlyphs, sourceBitmapData.ToArray (), shadowConfig.PageWidth, shadowConfig.PageHeight, out newGlyphs, shadowConfig.GlyphMargin + shadowConfig.blurRadius * 3, shadowConfig.ForcePowerOfTwo);

			//scale up in case we wanted bigger/smaller shadows
			if (shadowConfig.Scale != 1.0f)
				ScaleSheetsAndGlyphs (bitmapSheets, newGlyphs, shadowConfig.Scale); //no point in retargeting yet, since we will do it after blur

			//blacken and blur
			foreach (QBitmap bitmapSheet in bitmapSheets) {
				bitmapSheet.Colour32 (0, 0, 0);
				bitmapSheet.BlurAlpha (shadowConfig.blurRadius, shadowConfig.blurPasses);

			}

			//retarget after blur and scale
			RetargetAllGlyphs (bitmapSheets, newGlyphs, alphaTolerance);

			//create list of texture pages
			List<TexturePage> newTextureSheets = new List<TexturePage> ();
			foreach (QBitmap page in bitmapSheets)
				newTextureSheets.Add (new TexturePage (page.bitmapData));

			QFontData fontData = new QFontData ();
            //fontData.CharSet = charSet;
			fontData.CharSetMapping = new Dictionary<char, QFontGlyph> ();
			for (int i = 0; i < charSet.Length; i++)
				fontData.CharSetMapping.Add (charSet [i], newGlyphs [i]);

			fontData.Pages = newTextureSheets.ToArray ();
			fontData.CalculateMeanWidth ();
			fontData.CalculateMaxHeight ();

			foreach (QBitmap sheet in bitmapSheets)
				sheet.Free ();

            SetGLData(fontData);
			return new QFont (fontData);
		}

		private static void ScaleSheetsAndGlyphs (List<QBitmap> pages, QFontGlyph[] glyphs, float scale) {
			foreach (QBitmap page in pages)
				page.DownScale32 ((int)(page.bitmap.Width * scale), (int)(page.bitmap.Height * scale));

			foreach (QFontGlyph glyph in glyphs) {
				glyph.Rect = new Rectangle ((int)(glyph.Rect.X * scale), (int)(glyph.Rect.Y * scale), (int)(glyph.Rect.Width * scale), (int)(glyph.Rect.Height * scale));
				glyph.YOffset = (int)(glyph.YOffset * scale);
  
			}
		}

		private static void RetargetAllGlyphs (List<QBitmap> pages, QFontGlyph[] glyphs, byte alphaTolerance) {
			foreach (var glyph in glyphs)
				RetargetGlyphRectangleOutwards (pages [glyph.Page].bitmapData, glyph, false, alphaTolerance);
		}

		public static void SaveQFontDataToFile (QFontData data, string filePath) {
			List<string> lines = data.Serialize ();
			StreamWriter writer = new StreamWriter (filePath + ".qfont");
			foreach (string line in lines)
				writer.WriteLine (line);
            
			writer.Close ();

		}

		public static void CreateBitmapPerGlyph (QFontGlyph[] sourceGlyphs, QBitmap[] sourceBitmaps, out QFontGlyph[]  destGlyphs, out QBitmap[] destBitmaps) {
			destBitmaps = new QBitmap[sourceGlyphs.Length];
			destGlyphs = new QFontGlyph[sourceGlyphs.Length];
			for (int i = 0; i < sourceGlyphs.Length; i++) {
				QFontGlyph sg = sourceGlyphs [i];
				destGlyphs [i] = new QFontGlyph (i, new Rectangle (0, 0, sg.Rect.Width, sg.Rect.Height), sg.YOffset, sg.Character);
				destBitmaps [i] = new QBitmap (new Bitmap (sg.Rect.Width, sg.Rect.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb));
				QBitmap.Blit (sourceBitmaps [sg.Page].bitmapData, destBitmaps [i].bitmapData, sg.Rect, 0, 0);
			}
		}

		/// <summary>
		/// Loads the QFontData from a qfont file.
		/// </summary>
		/// <returns>The Q font data from file.</returns>
		/// <param name="filePath">File path.</param>
		/// <param name="downSampleFactor">Down sample factor.</param>
		/// <param name="loaderConfig">Loader config.</param>
		public static QFontData LoadQFontDataFromFile (FontLoadDescription loadDescription, float downSampleFactor, QFontLoaderConfiguration loaderConfig) {
			List<String> lines = new List<String> ();
			StreamReader reader = new StreamReader (loadDescription.Resources.GetResource());
			string line;
			while ((line = reader.ReadLine ()) != null)
				lines.Add (line);
			reader.Close ();

			QFontData data = new QFontData ();
			data.Deserialize (lines);

			List<Stream> bitmapStreams = new List<Stream> ();

			if (data.PageCount == 1) {
				bitmapStreams.Add (loadDescription.Resources.GetResource (".png"));
			} else {
				for (int i = 0; i < data.PageCount; i++)
					bitmapStreams.Add (loadDescription.Resources.GetResource ("_sheet_" + i));
			}

			QFontData processed = LoadQFontDataFromFile (data, bitmapStreams, downSampleFactor, loaderConfig);
			foreach (Stream stream in bitmapStreams)
				stream.Close ();

			return processed;
		}

		public static QFontData LoadQFontDataFromFile (QFontData data, IList<Stream> bitmapStreams, float downSampleFactor, QFontLoaderConfiguration loaderConfig) {

			List<QBitmap> bitmapPages = new List<QBitmap> ();
			foreach(Stream stream in bitmapStreams)
				bitmapPages.Add(new QBitmap(stream));

			foreach (QFontGlyph glyph in data.CharSetMapping.Values)
				RetargetGlyphRectangleOutwards (bitmapPages [glyph.Page].bitmapData, glyph, false, loaderConfig.KerningConfig.alphaEmptyPixelTolerance);
 
			char[] intercept = FirstIntercept (data.CharSetMapping);
			if (intercept != null) {
				throw new Exception ("Failed to load font from file. Glyphs '" + intercept [0] + "' and '" + intercept [1] + "' were overlapping. If you are texturing your font without locking pixel opacity, then consider using a larger glyph margin. This can be done by setting QFontBuilderConfiguration myQfontBuilderConfig.GlyphMargin, and passing it into CreateTextureFontFiles.");
			}

			if (downSampleFactor > 1.0f) {
				foreach (QBitmap page in bitmapPages)
					page.DownScale32 ((int)(page.bitmap.Width * downSampleFactor), (int)(page.bitmap.Height * downSampleFactor));

				foreach (QFontGlyph glyph in data.CharSetMapping.Values) {

					glyph.Rect = new Rectangle ((int)(glyph.Rect.X * downSampleFactor),
						(int)(glyph.Rect.Y * downSampleFactor),
						(int)(glyph.Rect.Width * downSampleFactor),
						(int)(glyph.Rect.Height * downSampleFactor));
					glyph.YOffset = (int)(glyph.YOffset * downSampleFactor);
				}
			} else if (downSampleFactor < 1.0f) {
				// If we were simply to shrink the entire texture, then at some point we will make glyphs overlap, breaking the font.
				// For this reason it is necessary to copy every glyph to a separate bitmap, and then shrink each bitmap individually.
				QFontGlyph[] shrunkGlyphs;
				QBitmap[] shrunkBitmapsPerGlyph;
				CreateBitmapPerGlyph (Helper.ToArray (data.CharSetMapping.Values), bitmapPages.ToArray (), out shrunkGlyphs, out shrunkBitmapsPerGlyph);
                    
				//shrink each bitmap
				for (int i = 0; i < shrunkGlyphs.Length; i++) {   
					QBitmap bmp = shrunkBitmapsPerGlyph [i];
					bmp.DownScale32 (Math.Max ((int)(bmp.bitmap.Width * downSampleFactor), 1), Math.Max ((int)(bmp.bitmap.Height * downSampleFactor), 1));
					shrunkGlyphs [i].Rect = new Rectangle (0, 0, bmp.bitmap.Width, bmp.bitmap.Height);
					shrunkGlyphs [i].YOffset = (int)(shrunkGlyphs [i].YOffset * downSampleFactor);
				}

				BitmapData[] shrunkBitmapData = new BitmapData[shrunkBitmapsPerGlyph.Length];
				for (int i = 0; i < shrunkBitmapsPerGlyph.Length; i++) {
					shrunkBitmapData [i] = shrunkBitmapsPerGlyph [i].bitmapData;
				}

				//use roughly the same number of pages as before..
				int newWidth = (int)(bitmapPages [0].bitmap.Width * (0.1f + downSampleFactor));
				int newHeight = (int)(bitmapPages [0].bitmap.Height * (0.1f + downSampleFactor));

				//free old bitmap pages since we are about to chuck them away
				for (int i = 0; i < bitmapPages.Count; i++)
					bitmapPages [i].Free ();

				QFontGlyph[] shrunkRepackedGlyphs;
				bitmapPages = GenerateBitmapSheetsAndRepack (shrunkGlyphs, shrunkBitmapData, newWidth, newHeight, out shrunkRepackedGlyphs, 4, false);
				data.CharSetMapping = CreateCharGlyphMapping (shrunkRepackedGlyphs);

				foreach (QBitmap bmp in shrunkBitmapsPerGlyph)
					bmp.Free ();

			}

			data.Pages = new TexturePage[bitmapPages.Count];
			for (int i = 0; i < bitmapPages.Count; i++)
				data.Pages [i] = new TexturePage (bitmapPages [i].bitmapData);


			if (downSampleFactor != 1.0f) {
				foreach (QFontGlyph glyph in data.CharSetMapping.Values)
					RetargetGlyphRectangleOutwards (bitmapPages [glyph.Page].bitmapData, glyph, false, loaderConfig.KerningConfig.alphaEmptyPixelTolerance);
 

				intercept = FirstIntercept (data.CharSetMapping);
				if (intercept != null) {
					throw new Exception ("Failed to load font from file. Glyphs '" + intercept [0] + "' and '" + intercept [1] + "' were overlapping. This occurred only after resizing your texture font, implying that there is a bug in QFont. ");
				}
			}

			List<QFontGlyph> glyphList = new List<QFontGlyph> ();

			foreach (char c in data.CharSet)
				glyphList.Add (data.CharSetMapping [c]);

			if (loaderConfig.ShadowConfig != null)
				data.DropShadow = BuildDropShadow (bitmapPages, glyphList.ToArray (), loaderConfig.ShadowConfig, Helper.ToArray (data.CharSet), loaderConfig.KerningConfig.alphaEmptyPixelTolerance);

			data.KerningPairs = KerningCalculator.CalculateKerning (Helper.ToArray (data.CharSet), glyphList.ToArray (), bitmapPages, loaderConfig.KerningConfig);
            
			data.CalculateMeanWidth ();
			data.CalculateMaxHeight ();

			for (int i = 0; i < bitmapPages.Count; i++)
				bitmapPages [i].Free ();

			SetGLData (data);
			return data;
		}

		const int FLOATS_PER_GLYPH = 8;
		private static void SetGLData(QFontData data) {

			// Create array of floats representing vertices and UV coords.
			float[] dataVertex = new float[FLOATS_PER_GLYPH*data.CharSetMapping.Count];
			float[] dataTexture = new float[FLOATS_PER_GLYPH*data.CharSetMapping.Count];

            int index = 0;
			foreach(KeyValuePair<char, QFontGlyph> glyph in data.CharSetMapping) {
				glyph.Value.GLIndex = index;
				SetGLVertexData (data, glyph.Value, dataVertex);
				SetGLTextureData (data, glyph.Value, dataTexture);
                index++;
			}

			data.VbaId = GLWrangler.GenVertexArray ();
			int vboVertex = GL.GenBuffer();
			int vboTexture = GL.GenBuffer ();

			GLWrangler.BindVertexArray (data.VbaId);

			// Populate the vertex buffer.
			GL.BindBuffer(BufferTarget.ArrayBuffer, vboVertex);
			GL.BufferData<float> (BufferTarget.ArrayBuffer, (IntPtr)(dataVertex.Length * sizeof(float)), dataVertex, BufferUsageHint.StaticDraw);
			GL.EnableClientState(ArrayCap.VertexArray);
			GL.VertexPointer(2, VertexPointerType.Float, 0, IntPtr.Zero);

			// Populate the UV buffer
			GL.BindBuffer (BufferTarget.ArrayBuffer, vboTexture);
			GL.EnableClientState(ArrayCap.TextureCoordArray);
			GL.BufferData<float> (BufferTarget.ArrayBuffer, (IntPtr)(dataTexture.Length * sizeof(float)), dataTexture, BufferUsageHint.StaticDraw);
			GL.TexCoordPointer(2, TexCoordPointerType.Float, 0, IntPtr.Zero);

			// Reset
			GL.BindBuffer (BufferTarget.ArrayBuffer, 0); // Unbind the buffer.
			GLWrangler.BindVertexArray (0);
		}

		private static void SetGLVertexData(QFontData data, QFontGlyph glyph, float[] buffer) {

			int offset = FLOATS_PER_GLYPH*glyph.GLIndex;

			// Vertices
			buffer [offset + 0] = 0;
			buffer [offset + 1] = glyph.YOffset;

			buffer [offset + 2] = 0;
			buffer [offset + 3] = glyph.YOffset + glyph.Rect.Height;

			buffer [offset + 4] = glyph.Rect.Width;
			buffer [offset + 5] = glyph.YOffset + glyph.Rect.Height;

			buffer [offset + 6] = glyph.Rect.Width;
			buffer [offset + 7] = glyph.YOffset;

		}

		private static void SetGLTextureData(QFontData data, QFontGlyph glyph, float[] buffer) {

			int offset = FLOATS_PER_GLYPH*glyph.GLIndex;
			TexturePage sheet = data.Pages [glyph.Page];

			// UV
			float left = (float)(glyph.Rect.X) / sheet.Width;
			float right = (float)(glyph.Rect.X + glyph.Rect.Width) / sheet.Width;
			float top = (float)(glyph.Rect.Y) / sheet.Height;
			float bottom = (float)(glyph.Rect.Y + glyph.Rect.Height) / sheet.Height;

			buffer[offset + 0] = left;
			buffer[offset + 1] = top;

			buffer[offset + 2] = left;
			buffer[offset + 3] = bottom;

			buffer[offset + 4] = right;
			buffer[offset + 5] = bottom;

			buffer[offset + 6] = right;
			buffer[offset + 7] = top;

		}

		private static char[] FirstIntercept (Dictionary<char,QFontGlyph> charSet) {
			char[] keys = Helper.ToArray (charSet.Keys);

			for (int i = 0; i < keys.Length; i++) {
				for (int j = i + 1; j < keys.Length; j++) {
					if (charSet [keys [i]].Page == charSet [keys [j]].Page && RectangleIntersect (charSet [keys [i]].Rect, charSet [keys [j]].Rect)) {
						return new char[2] { keys [i], keys [j] };
					}

				}
			}
			return null;
		}

		private static bool RectangleIntersect (Rectangle r1, Rectangle r2) {
			return (r1.X < r2.X + r2.Width && r1.X + r1.Width > r2.X &&
			r1.Y < r2.Y + r2.Height && r1.Y + r1.Height > r2.Y);

		}

		/// <summary>
		/// Returns the power of 2 that is closest to x, but not smaller than x.
		/// </summary>
		private static int PowerOfTwo (int x) {
			int shifts = 0;
			uint val = (uint)x;

			if (x < 0)
				return 0;

			while (val > 0) {
				val = val >> 1;
				shifts++;
			}

			val = (uint)1 << (shifts - 1);
			if (val < x) {
				val = val << 1;
			}

			return (int)val;
		}
	}
}
