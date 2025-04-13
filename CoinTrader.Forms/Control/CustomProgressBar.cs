using CoinTrader.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CoinTrader.Forms.Control
{
    public partial class CustomProgressBar : UserControl
    {
        private float _minimum = 0f;
        private float _maximum = 100f;
        private float _value = 0f;

        public enum ProgressDirection
        {
            LeftToRight,
            RightToLeft
        }

        /// <summary>
        /// 控制进度条的填充方向  
        /// LeftToRight 模式下：左侧为 Minimum，右侧为 Maximum，进度条从左向右填充  
        /// RightToLeft 模式下：坐标轴依然为左侧 Minimum、右侧 Maximum，但进度条填充从右向左，
        /// 即当 Value 越低时，填充越多，表示从最大值“回落”到最小值。
        /// </summary>
        public ProgressDirection Direction { get; set; } = ProgressDirection.LeftToRight;

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
                _value = Math.Min(Math.Max(value, Minimum), Maximum);
                Invalidate();
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
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
            Height = 60;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(BackColor);

            // 预留左右边距10像素，上边距25像素，绘制区域高度10像素
            var barRect = new Rectangle(10, 25, Width - 20, 10);

            // 绘制背景条
            using (var bgBrush = new SolidBrush(Color.LightGray))
                g.FillRectangle(bgBrush, barRect);

            // 计算进度条填充
            int progressWidth;
            Rectangle progressRect;

            if (Direction == ProgressDirection.LeftToRight)
            {
                // 正常模式：进度条从左向右填充
                float percent = (Value - Minimum) / (Maximum - Minimum);
                progressWidth = (int)(barRect.Width * percent);
                progressRect = new Rectangle(barRect.X, barRect.Y, progressWidth, barRect.Height);
            }
            else
            {
                // 右向左模式：进度条填充从右侧向左扩展
                // 当 Value == Maximum 时，填充宽度为0；当 Value == Minimum 时，填充满整个 barRect
                float percent = (Maximum - Value) / (Maximum - Minimum);
                progressWidth = (int)(barRect.Width * percent);
                int x = barRect.X + barRect.Width - progressWidth;
                progressRect = new Rectangle(x, barRect.Y, progressWidth, barRect.Height);
            }

            using (var progressBrush = new SolidBrush(Color.DodgerBlue))
                g.FillRectangle(progressBrush, progressRect);

            // 绘制标记（标记位置采用统一映射：左侧为 Minimum，右侧为 Maximum）
            foreach (var marker in _markers)
            {
                if (marker.Position < Minimum || marker.Position > Maximum)
                    continue;

                float normalized = (marker.Position - Minimum) / (Maximum - Minimum);
                // 无论方向如何，坐标轴保持一致
                float x = barRect.X + normalized * barRect.Width;

                using (var pen = new Pen(Color.Red, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                    g.DrawLine(pen, x, barRect.Top - 12, x, barRect.Bottom + 15);

                using (var textBrush = new SolidBrush(Color.Black))
                using (var font = new Font("Arial", 8))
                {
                    if (!string.IsNullOrEmpty(marker.TopLabel))
                    {
                        var size = g.MeasureString(marker.TopLabel, font);
                        float labelX = x - size.Width / 2;
                        if (labelX < 0) labelX = 0;
                        if (labelX + size.Width > Width)
                            labelX = Width - size.Width;
                        g.DrawString(marker.TopLabel, font, textBrush, labelX, barRect.Top - size.Height - 5);
                    }

                    if (!string.IsNullOrEmpty(marker.BottomLabel))
                    {
                        var size = g.MeasureString(marker.BottomLabel, font);
                        float labelX = x - size.Width / 2;
                        if (labelX < 0) labelX = 0;
                        if (labelX + size.Width > Width)
                            labelX = Width - size.Width;
                        g.DrawString(marker.BottomLabel, font, textBrush, labelX, barRect.Bottom + 5);
                    }
                }
            }
        }
    }
}
