using CoinTrader.Common;
using CoinTrader.Common.Database;
using CoinTrader.Forms.Chromium;
using CoinTrader.Forms.Strategies;
using CoinTrader.Forms.Strategies.Customer;
using CoinTrader.Forms.StrategiesRuntime;
using CoinTrader.OKXCore;
using CoinTrader.OKXCore.Entity;
using CoinTrader.OKXCore.Enum;
using CoinTrader.OKXCore.Manager;
using CoinTrader.OKXCore.Monitor;
using CoinTrader.Strategies;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CoinTrader.Forms.Control
{
    public partial class SwapView : DragbleMarketView, IMarketView
    {
        MarketDataProvider dataProvider = null;
        private InstrumentSwap instrument = null;
        private string instId = "";
        SWPFundingRateMonitor fundingRateMonitor = null;
        public string InstId
        {
            get
            {
                return instId;
            }
        }
        public decimal TotalAmount
        {
            get
            {
                var postions = PositionManager.Instance.GetPositions(this.instId);
                decimal amount = 0;
                foreach(var p in postions)
                {
                    amount += p.Upl + p.Margin;
                }

                return amount;
            }
        }

        public SwapView()
        {
            InitializeComponent();
        }

        public void SetInstId(string instId)
        {
            dataProvider = DataProviderManager.Instance.GetProvider(instId);
            this.instrument = InstrumentManager.SwapInstrument.GetInstrument(instId);
            this.lblInstrument.Text = instId;
            this.instId = instId;
            //this.lblMinSize.Text = instrument.MinSize.ToString()+"张";
            //this.lblMinAmount.Text = instrument.CtVal.ToString()+instrument.CtValCcy;
            //this.lblLever.Text = instrument.Lever.ToString();
            //this.lblFee.Text = instrument.Category.ToString();
            fundingRateMonitor=dataProvider.GetMonitor<SWPFundingRateMonitor>();
            //fundingRateMonitor = new SWPFundingRateMonitor(this.instId);
            //dataProvider.AddMonitor(fundingRateMonitor);
            RefreshStrategies();
        }

        public void RefreshStrategies()
        {
            var list = StrategyRunner.Instance.GetStrategiesByInstId(InstId);
            this.pnlBehavior.Controls.Clear();
            foreach (var strategy in list)
            {
                var view = new StrategyView();
                view.SetStrategy(strategy);
                this.pnlBehavior.Controls.Add(view);
            }
        }

        private void ShowMonitorList()
        {
            foreach (System.Windows.Forms.Control c in this.pnlMonitor.Controls)
            {
                c.Visible = false;
                (c as MonitorView).monitor = null;
            }

            int mindex = 0;
            foreach (var m in dataProvider.GetAllMonitor())
            {
                MonitorView mv;
                if (this.pnlMonitor.Controls.Count > mindex)
                {
                    mv = this.pnlMonitor.Controls[mindex] as MonitorView;
                    mv.Visible = true;
                }
                else
                {
                    mv = new MonitorView();
                    this.pnlMonitor.Controls.Add(mv);
                }

                mv.monitor = m;

                mindex++;
            }
        }

        private void Clean()
        {
            if (dataProvider != null)
            {
                DataProviderManager.Instance.ReleaseProvider(dataProvider);
                this.dataProvider = null;
            }
            StrategyRunner.Instance.StopStrategiesByInstId(InstId);
            // 标记一个工作流结束
            var db = MysqlHelper.Instance.getDB();
            db.Updateable<Workflow>()
             .SetColumns(it => it.Status == 2)
             .SetColumns(it => it.EndedAt == DateTime.Now)
             .Where(it => it.Instrument == this.instId)
             .ExecuteCommand();
            Logger.Instance.LogInfo("已结束工作流");
        }

        protected override void OnParentChanged(EventArgs e)
        {
            if (!this.dragging && this.Parent == null)
            {
                this.Clean();
            }

            base.OnParentChanged(e);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Parent = null;
        }

        private void HidePosition()
        {
            var controls = this.flowLayoutPanel1.Controls;

            for(int i = controls.Count - 1; i>0;i-- )
            {
                controls[i].Parent = null;
            }

            this.timerPosition.Enabled = false;
        }
        private void ShowPosition()
        {
            this.timerPosition.Enabled = true;
            var postions = PositionManager.Instance.GetPositions(this.instId);
            var controls = this.flowLayoutPanel1.Controls;
            int mindex = 1;
            foreach (var pos in postions)
            {
                SwapInfoView pv;
                if (this.flowLayoutPanel1.Controls.Count > mindex)
                {
                    var control = this.flowLayoutPanel1.Controls[mindex];
                    pv = control as SwapInfoView;
                    pv.Visible = true;
                }
                else
                {
                    pv = new SwapInfoView();
                    this.flowLayoutPanel1.Controls.Add(pv);
                }

                pv.SetId(pos.PosId);
                mindex++;
            }

            for (int i = mindex; i<controls.Count;i++)
            {
                var c = controls[i];
                c.Visible = false;
            }

            this.pnlEmpty.Visible = postions.Count == 0;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (this.dataProvider != null)
            {
                this.tickView1.ShowTickerPrice(dataProvider.Ask, dataProvider.Bid);
            }

            this.lblMonitor.ForeColor = this.dataProvider != null && this.dataProvider.Effective ? Color.Red : Color.Black;

            lblPostion.Visible = PositionManager.Instance.HasPosition(this.instId);
            var list = StrategyRunner.Instance.GetStrategiesByInstId(InstId);
            if (list.Count == 1)
            {
                var strategy = list[0] as SampleSwapStrategy;
                if (strategy != null)
                {
                    labelBanned.Visible = strategy.bannedTime > DateTime.Now;
                    if (strategy.MoveProfit)
                    {
                        this.labelDebug.Text = strategy.debugText;
                    }
                    else
                    {
                        this.labelDebug.Text = "";
                    }
                }
                var strategy2 = list[0] as RandomSwapStrategy;
                if (strategy2 != null)
                {
                    labelBanned.Visible = strategy2.bannedTime > DateTime.Now;
                    if (strategy2.MoveProfit)
                    {
                        this.labelDebug.Text = strategy2.debugText;
                    }
                    else
                    {
                        this.labelDebug.Text = "";
                    }
                    if (strategy2.CustomProgressBarMarkers != null)
                    {
                        this.customProgressBar.Direction = strategy2.CustomProgressBarDirection;
                        this.customProgressBar.Minimum = strategy2.CustomProgressBarMin;
                        this.customProgressBar.Maximum = strategy2.CustomProgressBarMax;
                        this.customProgressBar.Value = strategy2.CustomProgressBarValue;
                        this.customProgressBar.SetMarkers(strategy2.CustomProgressBarMarkers);
                    }
                }
            }
            //if (fundingRateMonitor != null)
            //{
            //    lblFee.Text = Math.Round( fundingRateMonitor.FundingRate.Rate * 100,3) + "%";
            //}
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.tabControl1.SelectedTab == this.tabPage3)
            {
                this.ShowMonitorList();
                this.HidePosition();
            }
            //else if (this.tabControl1.SelectedTab == this.tabPage2)
            //{
            //    this.ShowPosition();
            //    this.pnlMonitor.Controls.Clear();
            //}
            else if (this.tabControl1.SelectedTab == this.tabPage1)
            {
                this.ShowPosition();
                this.pnlMonitor.Controls.Clear();
            }
        }

        private void lblInstrument_Click(object sender, EventArgs e)
        {
            var chartWindow = WindowManager.Instance.OpenWindow<WebTradeView>();
            chartWindow.InstId = this.InstId; 
            chartWindow.Show();
        }

        private void btnStat_Click(object sender, EventArgs e)
        {
            WinSwapStat win = new WinSwapStat();
            win.SetCurrency(this.InstId);
            win.Show();
        }

        private void timerPosition_Tick(object sender, EventArgs e)
        {
            this.ShowPosition();
        }

        private void buttonInfo_Click(object sender, EventArgs e)
        {
            string info = "";
            info += $"最小下单量:{instrument.MinSize}张\n";
            info += $"最小面值:{instrument.CtVal.ToString() + instrument.CtValCcy}\n";
            info += $"最大杠杆:{instrument.Lever}\n";
            info += $"资金费率:{Math.Round(fundingRateMonitor.FundingRate.Rate * 100, 3)}%\n";
            MessageBox.Show(info);
        }

        // 测试按钮被点击
        private void buttonTest_Click(object sender, EventArgs e)
        {
            var list = StrategyRunner.Instance.GetStrategiesByInstId(InstId);
            if (list.Count == 1)
            {
                var strategy2 = list[0] as RandomSwapStrategy;
                if (strategy2 != null)
                {
                    strategy2.Test();
                }
            }
        }
    }
}
