﻿#region License
//
// The Open Toolkit Library License
//
// Copyright (c) 2006 - 2009 the Open Toolkit library, except where noted.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights to 
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//
#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

using OpenTK.Platform;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace OpenTK
{
    /// <summary>
    /// Defines a UserControl with OpenGL rendering capabilities.
    /// </summary>
    public partial class GLControl : UserControl
    {
        IGraphicsContext context;
        IGLControl implementation;
        GraphicsMode format;
        IWindowInfo window_info;
        int major, minor;
        GraphicsContextFlags flags;

        #region --- Constructors ---

        /// <summary>
        /// Constructs a new GLControl.
        /// </summary>
        public GLControl()
            : this(GraphicsMode.Default)
        { }

        /// <summary>
        /// Constructs a new GLControl with the specified GraphicsMode.
        /// </summary>
        /// <param name="mode">The OpenTK.Graphics.GraphicsMode of the control.</param>
        public GLControl(GraphicsMode mode)
            : this(mode, 1, 0, GraphicsContextFlags.Default)
        { }

        /// <summary>
        /// Constructs a new GLControl with the specified GraphicsMode.
        /// </summary>
        /// <param name="mode">The OpenTK.Graphics.GraphicsMode of the control.</param>
        /// <param name="major">The major version for the OpenGL GraphicsContext.</param>
        /// <param name="minor">The minor version for the OpenGL GraphicsContext.</param>
        /// <param name="flags">The GraphicsContextFlags for the OpenGL GraphicsContext.</param>
        public GLControl(GraphicsMode mode, int major, int minor, GraphicsContextFlags flags)
        {
            SetStyle(ControlStyles.Opaque, true);
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            DoubleBuffered = false;

            InitializeComponent();

            this.format = mode;
            this.major = major;
            this.minor = minor;
            this.flags = flags;

            // On Windows, you first need to create the window, then set the pixel format.
            // On X11, you first need to select the visual, then create the window.
            // On OSX, the pixel format needs to be selected before the GL context.
            // Right now, pixel formats/visuals are selected during context creation. In the future,
            // it would be better to decouple selection from context creation, which will allow us
            // to clean up this hacky code. The best option is to do this along with multisampling
            // support.
            if (DesignMode)
                implementation = new DummyGLControl();
            else
                implementation = new GLControlFactory().CreateGLControl(mode, this);

            this.CreateControl();
        }

        #endregion

        #region --- Protected Methods ---

        /// <summary>Raises the HandleCreated event.</summary>
        /// <param name="e">Not used.</param>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            this.Context = implementation.CreateContext(major, minor, flags);

            this.window_info = implementation.WindowInfo;
            this.MakeCurrent();
            ((IGraphicsContextInternal)this.Context).LoadAll();
        }

        /// <summary>Raises the HandleDestroyed event.</summary>
        /// <param name="e">Not used.</param>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            if (this.Context != null)
            {
                this.Context.Dispose();
                this.Context = null;
            }
            this.window_info.Dispose();
            this.window_info = null;
        }

        /// <summary>
        /// Raises the System.Windows.Forms.Control.Paint event.
        /// </summary>
        /// <param name="e">A System.Windows.Forms.PaintEventArgs that contains the event data.</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            if (DesignMode)
                e.Graphics.Clear(BackColor);

            base.OnPaint(e);
        }

        /// <summary>
        /// Raises the Resize event.
        /// </summary>
        /// <param name="e">A System.EventArgs that contains the event data.</param>
        protected override void OnResize(EventArgs e)
        {
            if (context != null)
                context.Update(window_info);

            base.OnResize(e);
        }

        /// <summary>
        /// Raises the ParentChanged event.
        /// </summary>
        /// <param name="e">A System.EventArgs that contains the event data.</param>
        protected override void OnParentChanged(EventArgs e)
        {
            if (context != null)
                context.Update(window_info);

            base.OnParentChanged(e);
        }

        #endregion

        #region --- Public Methods ---

        #region public void SwapBuffers()

        /// <summary>
        /// Swaps the front and back buffers, presenting the rendered scene to the screen.
        /// </summary>
        public void SwapBuffers()
        {
            Context.SwapBuffers();
        }

        #endregion

        #region public void MakeCurrent()

        /// <summary>
        /// Makes the underlying this GLControl current in the calling thread.
        /// All OpenGL commands issued are hereafter interpreted by this GLControl.
        /// </summary>
        public void MakeCurrent()
        {
            this.Context.MakeCurrent(this.window_info);
        }

        #endregion

        #region public bool IsIdle

        /// <summary>
        /// Gets a value indicating whether the current thread contains pending system messages.
        /// </summary>
        [Browsable(false)]
        public bool IsIdle
        {
            get { return implementation.IsIdle; }
        }

        #endregion

        #region public IGraphicsContext Context

        /// <summary>
        /// Gets an interface to the underlying GraphicsContext used by this GLControl.
        /// </summary>
        [Browsable(false)]
        public IGraphicsContext Context
        {
            get { return context; }
            private set { context = value; }
        }

        #endregion

        #region public float AspectRatio

        /// <summary>
        /// Gets the aspect ratio of this GLControl.
        /// </summary>
        [Description("The aspect ratio of the client area of this GLControl.")]
        public float AspectRatio
        {
            get
            {
                return this.ClientSize.Width / (float)this.ClientSize.Height;
            }
        }

        #endregion

        #region public bool VSync

        /// <summary>
        /// Gets or sets a value indicating whether vsync is active for this GLControl.
        /// </summary>
        [Description("Indicates whether GLControl updates are synced to the monitor's refresh.")]
        public bool VSync
        {
            get
            {
                if (Context != null)
                    return Context.VSync;
                return false;
            }
            set
            {
                if (Context != null)
                    Context.VSync = value;
            }
        }

        #endregion

        #region public GraphicsMode GraphicsMode

        /// <summary>
        /// Gets the GraphicsMode of the GraphicsContext attached to this GLControl.
        /// </summary>
        /// <remarks>
        /// To change the GraphicsMode, you must destroy and recreate the GLControl.
        /// </remarks>
        public GraphicsMode GraphicsMode
        {
            get { return Context.GraphicsMode; }
        }

        #endregion

        #region public Bitmap GrabScreenshot()

        /// <summary>Grabs a screenshot of the frontbuffer contents.</summary>
        /// <returns>A System.Drawing.Bitmap, containing the contents of the frontbuffer.</returns>
        /// <exception cref="OpenTK.Graphics.GraphicsContextException">
        /// Occurs when no OpenTK.Graphics.GraphicsContext is current in the calling thread.
        /// </exception>
        [Obsolete("This method will be removed. Please provide your own code to capture framebuffer contents.")]
        public Bitmap GrabScreenshot()
        {
            Bitmap bmp = new Bitmap(this.ClientSize.Width, this.ClientSize.Height);
            System.Drawing.Imaging.BitmapData data =
                bmp.LockBits(this.ClientRectangle, System.Drawing.Imaging.ImageLockMode.WriteOnly,
                             System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            GL.ReadPixels(0, 0, this.ClientSize.Width, this.ClientSize.Height, PixelFormat.Bgr, PixelType.UnsignedByte,
                          data.Scan0);
            bmp.UnlockBits(data);
            bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
            return bmp;
        }

        #endregion

        #endregion
    }
}