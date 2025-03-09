namespace CoinTrader.Forms.Control
{
    partial class SwapView
    {
        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.btnClose = new System.Windows.Forms.Button();
            this.lblInstrument = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.pnlBehavior = new System.Windows.Forms.FlowLayoutPanel();
            this.lblMonitor = new System.Windows.Forms.Label();
            this.lblPostion = new System.Windows.Forms.Label();
            this.tickView1 = new CoinTrader.Forms.Control.TickView();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.pnlMonitor = new System.Windows.Forms.FlowLayoutPanel();
            this.btnStat = new System.Windows.Forms.Button();
            this.timerPosition = new System.Windows.Forms.Timer(this.components);
            this.buttonInfo = new System.Windows.Forms.Button();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.pnlEmpty = new System.Windows.Forms.Panel();
            this.label4 = new System.Windows.Forms.Label();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            this.pnlEmpty.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(307, 2);
            this.btnClose.Margin = new System.Windows.Forms.Padding(2);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(23, 19);
            this.btnClose.TabIndex = 49;
            this.btnClose.Text = "x";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // lblInstrument
            // 
            this.lblInstrument.AutoSize = true;
            this.lblInstrument.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Underline);
            this.lblInstrument.Location = new System.Drawing.Point(30, 22);
            this.lblInstrument.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblInstrument.Name = "lblInstrument";
            this.lblInstrument.Size = new System.Drawing.Size(39, 16);
            this.lblInstrument.TabIndex = 50;
            this.lblInstrument.Text = "--aa";
            this.lblInstrument.Click += new System.EventHandler(this.lblInstrument_Click);
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 200;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Location = new System.Drawing.Point(2, 2);
            this.tabControl1.Margin = new System.Windows.Forms.Padding(2);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(332, 128);
            this.tabControl1.TabIndex = 53;
            this.tabControl1.SelectedIndexChanged += new System.EventHandler(this.tabControl1_SelectedIndexChanged);
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.pnlBehavior);
            this.tabPage1.Controls.Add(this.lblMonitor);
            this.tabPage1.Controls.Add(this.lblPostion);
            this.tabPage1.Controls.Add(this.lblInstrument);
            this.tabPage1.Controls.Add(this.tickView1);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Margin = new System.Windows.Forms.Padding(2);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(2);
            this.tabPage1.Size = new System.Drawing.Size(324, 102);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "合约";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // pnlBehavior
            // 
            this.pnlBehavior.BackColor = System.Drawing.Color.Silver;
            this.pnlBehavior.Location = new System.Drawing.Point(2, 54);
            this.pnlBehavior.Margin = new System.Windows.Forms.Padding(2);
            this.pnlBehavior.Name = "pnlBehavior";
            this.pnlBehavior.Size = new System.Drawing.Size(320, 42);
            this.pnlBehavior.TabIndex = 55;
            // 
            // lblMonitor
            // 
            this.lblMonitor.AutoSize = true;
            this.lblMonitor.Location = new System.Drawing.Point(7, 25);
            this.lblMonitor.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblMonitor.Name = "lblMonitor";
            this.lblMonitor.Size = new System.Drawing.Size(17, 12);
            this.lblMonitor.TabIndex = 54;
            this.lblMonitor.Text = "❤";
            // 
            // lblPostion
            // 
            this.lblPostion.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.lblPostion.ForeColor = System.Drawing.Color.White;
            this.lblPostion.Location = new System.Drawing.Point(0, 0);
            this.lblPostion.Margin = new System.Windows.Forms.Padding(2);
            this.lblPostion.Name = "lblPostion";
            this.lblPostion.Size = new System.Drawing.Size(36, 17);
            this.lblPostion.TabIndex = 53;
            this.lblPostion.Text = "持仓";
            this.lblPostion.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblPostion.Visible = false;
            // 
            // tickView1
            // 
            this.tickView1.Location = new System.Drawing.Point(188, 5);
            this.tickView1.Margin = new System.Windows.Forms.Padding(1);
            this.tickView1.Name = "tickView1";
            this.tickView1.Size = new System.Drawing.Size(136, 46);
            this.tickView1.TabIndex = 51;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.pnlMonitor);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Margin = new System.Windows.Forms.Padding(2);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(324, 102);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "数据";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // pnlMonitor
            // 
            this.pnlMonitor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlMonitor.Location = new System.Drawing.Point(0, 0);
            this.pnlMonitor.Margin = new System.Windows.Forms.Padding(2);
            this.pnlMonitor.Name = "pnlMonitor";
            this.pnlMonitor.Size = new System.Drawing.Size(324, 102);
            this.pnlMonitor.TabIndex = 0;
            // 
            // btnStat
            // 
            this.btnStat.Location = new System.Drawing.Point(266, 1);
            this.btnStat.Margin = new System.Windows.Forms.Padding(2);
            this.btnStat.Name = "btnStat";
            this.btnStat.Size = new System.Drawing.Size(36, 20);
            this.btnStat.TabIndex = 56;
            this.btnStat.Text = "统计";
            this.btnStat.UseVisualStyleBackColor = true;
            this.btnStat.Click += new System.EventHandler(this.btnStat_Click);
            // 
            // timerPosition
            // 
            this.timerPosition.Enabled = true;
            this.timerPosition.Interval = 300;
            this.timerPosition.Tick += new System.EventHandler(this.timerPosition_Tick);
            // 
            // buttonInfo
            // 
            this.buttonInfo.Location = new System.Drawing.Point(227, -1);
            this.buttonInfo.Margin = new System.Windows.Forms.Padding(2);
            this.buttonInfo.Name = "buttonInfo";
            this.buttonInfo.Size = new System.Drawing.Size(36, 20);
            this.buttonInfo.TabIndex = 57;
            this.buttonInfo.Text = "信息";
            this.buttonInfo.UseVisualStyleBackColor = true;
            this.buttonInfo.Click += new System.EventHandler(this.buttonInfo_Click);
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.AutoScroll = true;
            this.flowLayoutPanel1.Controls.Add(this.pnlEmpty);
            this.flowLayoutPanel1.Location = new System.Drawing.Point(6, 130);
            this.flowLayoutPanel1.Margin = new System.Windows.Forms.Padding(2);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(320, 144);
            this.flowLayoutPanel1.TabIndex = 58;
            // 
            // pnlEmpty
            // 
            this.pnlEmpty.Controls.Add(this.label4);
            this.pnlEmpty.Location = new System.Drawing.Point(2, 2);
            this.pnlEmpty.Margin = new System.Windows.Forms.Padding(2);
            this.pnlEmpty.Name = "pnlEmpty";
            this.pnlEmpty.Size = new System.Drawing.Size(304, 64);
            this.pnlEmpty.TabIndex = 3;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(126, 29);
            this.label4.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(53, 12);
            this.label4.TabIndex = 2;
            this.label4.Text = "暂无持仓";
            // 
            // SwapView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.Controls.Add(this.flowLayoutPanel1);
            this.Controls.Add(this.buttonInfo);
            this.Controls.Add(this.btnStat);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.tabControl1);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "SwapView";
            this.Size = new System.Drawing.Size(332, 280);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage3.ResumeLayout(false);
            this.flowLayoutPanel1.ResumeLayout(false);
            this.pnlEmpty.ResumeLayout(false);
            this.pnlEmpty.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Label lblInstrument;
        private TickView tickView1;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.FlowLayoutPanel pnlMonitor;
        private System.Windows.Forms.Label lblPostion;
        private System.Windows.Forms.Label lblMonitor;
        private System.Windows.Forms.Button btnStat;
        private System.Windows.Forms.Timer timerPosition;
        private System.Windows.Forms.Button buttonInfo;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Panel pnlEmpty;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.FlowLayoutPanel pnlBehavior;
    }
}
