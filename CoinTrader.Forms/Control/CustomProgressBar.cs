using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CoinTrader.Forms.Control
{
    public partial class CustomProgressBar : UserControl
    {
        private float _minimum = 1.4f;
        private float _maximum = 2.5f;
        private float _value = 1.4f;

        public float Minimum
        {
            get => _minimum;
            set { _minimum = value; Invalidate(); }
        }

        public float Maximum
        {
            get => _maximum;
            set { _maximum = value; Invalidate(); }
        }

        public float Value
        {
            get => _value;
            set
            {
                if (Math.Abs(_value - value) > 0.0001f)
                {
                    _value = Math.Min(Math.Max(value, Minimum), Maximum);
                    Invalidate();
                }
            }
        }

        public class Marker
        {
            public float Position;
            public string TopLabel;
            public string BottomLabel;
        }

        private readonly List<Marker> _markers = new List<Marker>();
        public IReadOnlyList<Marker> Markers => _markers;

        public void SetMarkers(IEnumerable<Marker> markers)
        {
            _markers.Clear();
            _markers.AddRange(markers);
            Invalidate();
        }

        public CustomProgressBar()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
            this.Height = 60;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(this.BackColor);

            var barRect = new Rectangle(10, 25, this.Width - 20, 10);

            // 背景条
            using (var bgBrush = new SolidBrush(Color.LightGray))
                g.FillRectangle(bgBrush, barRect);

            // 当前进度条
            float percent = (Value - Minimum) / (Maximum - Minimum);
            int progressWidth = (int)(barRect.Width * percent);
            var progressRect = new Rectangle(barRect.X, barRect.Y, progressWidth, barRect.Height);

            using (var progressBrush = new SolidBrush(Color.DodgerBlue))
                g.FillRectangle(progressBrush, progressRect);

            // 绘制标记
            foreach (var marker in _markers)
            {
                if (marker.Position < Minimum || marker.Position > Maximum) continue;

                float x = barRect.X + ((marker.Position - Minimum) / (Maximum - Minimum)) * barRect.Width;

                using (var pen = new Pen(Color.Red, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                    g.DrawLine(pen, x, barRect.Top - 12, x, barRect.Bottom + 15);

                using (var textBrush = new SolidBrush(Color.Black))
                using (var font = new Font("Arial", 8))
                {
                    if (!string.IsNullOrEmpty(marker.TopLabel))
                    {
                        var size = g.MeasureString(marker.TopLabel, font);
                        float labelX = x - size.Width / 2;

                        // 保证不超出控件边界
                        if (labelX < 0) labelX = 0;
                        if (labelX + size.Width > this.Width)
                            labelX = this.Width - size.Width;

                        g.DrawString(marker.TopLabel, font, textBrush, labelX, barRect.Top - size.Height - 5);
                    }

                    if (!string.IsNullOrEmpty(marker.BottomLabel))
                    {
                        var size = g.MeasureString(marker.BottomLabel, font);
                        float labelX = x - size.Width / 2;

                        // 保证不超出控件边界
                        if (labelX < 0) labelX = 0;
                        if (labelX + size.Width > this.Width)
                            labelX = this.Width - size.Width;

                        g.DrawString(marker.BottomLabel, font, textBrush, labelX, barRect.Bottom + 5);
                    }
                }
            }


            // 当前值
            //using (var textBrush = new SolidBrush(Color.Black))
            //using (var font = new Font("Arial", 8, FontStyle.Bold))
            //{
            //    string text = $"{Value:F2}";
            //    g.DrawString(text, font, textBrush, barRect.Right + 5, barRect.Top - 2);
            //}
        }
    }
}
