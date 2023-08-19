using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace TextBoxBorderColor
{
    public class MyTextBox : TextBox
    {
        const int WM_NCPAINT = 0x85;
        const uint RDW_INVALIDATE = 0x1;
        const uint RDW_IUPDATENOW = 0x100;
        const uint RDW_FRAME = 0x400;
        [Flags]
        public enum BP_BUFFERFORMAT
        {
            CompatibleBitmap,
            DIB,
            TopDownDIB,
            TopDownMonoDIB
        }
        public enum BPPF : uint
        {
            Erase = 1,
            NoClip = 2,
            NonClient = 4
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            internal int left;

            internal int top;

            internal int right;

            internal int bottom;

            internal int Height
            {
                get
                {
                    return bottom - top;
                }
            }

            internal Point Location
            {
                get
                {
                    return new Point(left, top);
                }
            }
            internal Size Size
            {
                get
                {
                    return new Size(Width, Height);
                }
            }
            internal int Width
            {
                get
                {
                    return right - left;
                }
            }

            internal RECT(int l, int t, int r, int b)
            {
                left = l;
                top = t;
                right = r;
                bottom = b;
            }

            public static RECT FromRectangle(Rectangle r)
            {
                return new RECT(r.Left, r.Top, r.Right, r.Bottom);
            }



            internal Rectangle ToRectangle()
            {
                return new Rectangle(left, top, (right - left), (bottom - top));
            }

            public bool IsEmpty => left == 0 & top == 0 & right == 0 & bottom == 0;

            public static RECT FromXYWH(int x, int y, int width, int height)
            {
                return new RECT(x, y, x + width, y + height);
            }

            internal RECT GetRtlRect(int width)
            {
                return new RECT(width - Width - left, top, width - left, bottom);
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct BP_PAINTPARAMS : IDisposable
        {
            private int cbSize;
            public BPPF Flags;
            private IntPtr prcExclude;
            private IntPtr pBlendFunction;

            public BP_PAINTPARAMS(BPPF flags)
            {
                cbSize = Marshal.SizeOf(typeof(BP_PAINTPARAMS));
                Flags = flags;
                prcExclude = pBlendFunction = IntPtr.Zero;
            }



            [StructLayout(LayoutKind.Sequential)]
            public struct BLENDFUNCTION
            {
                public byte BlendOp;
                public byte BlendFlags;
                public byte SourceConstantAlpha;
                public byte AlphaFormat;

                public BLENDFUNCTION(byte op, byte flags, byte alpha, byte format)
                {
                    BlendOp = op;
                    BlendFlags = flags;
                    SourceConstantAlpha = alpha;
                    AlphaFormat = format;
                }
            }
            public Rectangle Exclude
            {
                get { return (Rectangle)Marshal.PtrToStructure(prcExclude, typeof(RECT)); }
                set
                {
                    if (prcExclude == IntPtr.Zero)
                        prcExclude = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(RECT)));
                    Marshal.StructureToPtr(value, prcExclude, false);
                }
            }

            public BLENDFUNCTION BlendFunction
            {
                get { return (BLENDFUNCTION)Marshal.PtrToStructure(pBlendFunction, typeof(BLENDFUNCTION)); }
                set
                {
                    if (pBlendFunction == IntPtr.Zero)
                        pBlendFunction = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(BLENDFUNCTION)));
                    Marshal.StructureToPtr(value, pBlendFunction, false);
                }
            }

            public void Dispose()
            {
                if (prcExclude != IntPtr.Zero) Marshal.FreeHGlobal(prcExclude);
                if (pBlendFunction != IntPtr.Zero) Marshal.FreeHGlobal(pBlendFunction);
            }
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetWindowDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll")]
        static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprc, IntPtr hrgn, uint flags);
        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("User32.dll", SetLastError = true)]
        static extern int GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern int OffsetRect(ref RECT lpRect, int x, int y);
        [DllImport("uxtheme.dll", SetLastError = true)]
        static extern IntPtr BeginBufferedPaint(IntPtr hdc, ref RECT prcTarget, BP_BUFFERFORMAT dwFormat, ref BP_PAINTPARAMS pPaintParams, out IntPtr phdc);
       
        [DllImport("gdi32.dll")]
        static extern int ExcludeClipRect(IntPtr hdc, int left, int top, int right, int bottom);
        [DllImport("uxtheme.dll")]
        static extern IntPtr EndBufferedPaint(IntPtr hBufferedPaint, bool fUpdateTarget);
        [DllImport("uxtheme.dll", SetLastError = true)]
        [PreserveSig]
        static extern IntPtr BufferedPaintInit();

        [DllImport("uxtheme.dll", SetLastError = true)]
        [PreserveSig]
        static extern IntPtr BufferedPaintUnInit();
        Color borderColor = Color.Blue;
        public Color BorderColor
        {
            get { return borderColor; }
            set
            {
                borderColor = value;
                RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, RDW_FRAME | RDW_IUPDATENOW | RDW_INVALIDATE);
            }
        }
        
        protected override void WndProc(ref Message m)
        {

            if (m.Msg == WM_NCPAINT && BorderColor != Color.Transparent &&
                BorderStyle == System.Windows.Forms.BorderStyle.Fixed3D)
            {
                var hdc = GetWindowDC(Handle);
                var pPaintParams = new BP_PAINTPARAMS(BPPF.Erase | BPPF.NoClip | BPPF.NonClient);
                RECT rWindow, rClient;
                GetWindowRect(m.HWnd, out rWindow);
                GetClientRect(m.HWnd, out rClient);
                OffsetRect(ref rWindow, -rWindow.left, -rWindow.top);
                OffsetRect(ref rClient, 2, 2);
                ExcludeClipRect(hdc, rClient.left, rClient.top, rClient.right, rClient.bottom);
                IntPtr memdc;
                BufferedPaintInit();
                var hbuff = BeginBufferedPaint(hdc, ref rWindow, BP_BUFFERFORMAT.TopDownDIB, ref pPaintParams, out memdc);
                if (memdc != IntPtr.Zero)
                {
                    using (var g = Graphics.FromHdcInternal(memdc))
                    {
                        using (var b = new Pen(BackColor))
                            g.DrawRectangle(b, new Rectangle(1, 1, Width - 3, Height - 3));
                        using (var p = new Pen(BorderColor))
                            g.DrawRectangle(p, new Rectangle(0, 0, Width - 1, Height - 1));


                    }
                    EndBufferedPaint(hbuff, true);
                }
                else
                {

                    using (var g = Graphics.FromHdcInternal(hdc))
                    {
                        using (var b = new Pen(BackColor))
                            g.DrawRectangle(b, new Rectangle(1, 1, Width - 3, Height - 3));
                        using (var p = new Pen(BorderColor))
                            g.DrawRectangle(p, new Rectangle(0, 0, Width - 1, Height - 1));

                    }
                }
                BufferedPaintUnInit();
                ReleaseDC(Handle, hdc);
            }
            else
                base.WndProc(ref m);
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero,
                   RDW_FRAME | RDW_IUPDATENOW | RDW_INVALIDATE);
        }
    }
}
