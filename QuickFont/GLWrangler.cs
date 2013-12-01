using System;
using OpenTK.Graphics.OpenGL;
using QuickFont.Util;

namespace QuickFont {

    /// <summary>
    /// Wraps some GL functions which are platform dependent.
    /// </summary>
    public class GLWrangler {

        internal delegate int GLGenVertexArray();
        internal static GLGenVertexArray GenVertexArray;

        internal delegate void GLBindVertexArray(int id);
        internal static GLBindVertexArray BindVertexArray;

        internal delegate void GLDeleteVertexArray(int id);
        internal static GLDeleteVertexArray DeleteVertexArray;

		static GLWrangler() {
			if (PlatformUtils.DeterminePlatform() == PlatformOS.MacOS) {
                GenVertexArray = GL.Apple.GenVertexArray;
                BindVertexArray = GL.Apple.BindVertexArray;
                DeleteVertexArray = GL.Apple.DeleteVertexArray;
            } else {
                GenVertexArray = GL.GenVertexArray;
                BindVertexArray = GL.BindVertexArray;
                DeleteVertexArray = GL.DeleteVertexArray;
            }
        }
    }
}

