﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using CoinTrader.Forms.Control;
using CoinTrader.Forms.Strategies;
using CoinTrader.Common;
using CoinTrader.Forms.Event;
using CoinTrader.Common.Interface;
using CoinTrader.OKXCore.Event;
using CoinTrader.OKXCore.Monitor;
using CoinTrader.OKXCore.Okex;
using CoinTrader.OKXCore.Manager;
using CoinTrader.Strategies;
using CoinTrader.Forms.StrategiesRuntime;
using CommonTools.Coroutines;
using System.Linq;
using System.Runtime.InteropServices;
using CoinTrader.Common.Database;

namespace CoinTrader.Forms
{
    public partial class WinMain : Form, IEventListener
    {
        List<StrategyBase> fundsStrategies = new List<StrategyBase>();
        private List<OrderView> MyOrderViews = new List<OrderView>();
        private bool isInited = false;
        private bool isClosed = false;
        private readonly string RunAllMenuItem = "_RUN_ALL_";

        private string title = "";

        private bool UseSmallSwapView = false;

        public WinMain()
        {
            InitializeComponent();
            title = this.Text;

            Ticker.Start();

            this.GenerateTitle();
            // 恢复所有工作流
            var db = MysqlHelper.Instance.getDB();
            var workflows = db.Queryable<Workflow>()
                .Where(it => (it.Status == 0 || it.Status == 1))
                .ToList();

            var stategyGroups = StrategyManager.Instance.GetStrategyGroups(StrategyType.Swap);
            foreach (var workflow in workflows)
            {
                var group = stategyGroups.First(x => x.name == workflow.Strategy);
                if (group == null)
                {
                    Logger.Instance.LogError($"恢复工作流失败，策略不存在:{workflow.Status}");
                    continue;
                }
                AddView<SwapView>(workflow.Instrument, group, false);
            }
        }

        private void GenerateTitle()
        {
            var ver = Application.ProductVersion.ToString();
            string text = title + "   ver:" + ver;

            if (Config.Instance.ApiInfo.IsSimulated)
                text += "   模拟盘";
            this.Text = text;
        }

        /// <summary>
        /// 生成币种的下拉菜单
        /// </summary>
        private void ReloadCurrencyMenus()
        {
            #region 生成现货菜单

            GenerateMenuItems(mnSpots, StrategyType.Spot, OnSpotStrategyMenuItemClick, CloseAllSpot);
            #endregion

            #region 生成合约菜单

            mnSwap.Visible = AccountManager.Current.SwapOpened;

            if (AccountManager.Current.SwapOpened)
            {
                GenerateMenuItems(mnuSwapList, StrategyType.Swap, OnSwapStrategyMenuItemClick,CloseAllSwap);
            }

            #endregion
        }

        private void GenerateMenuItems(ToolStripMenuItem parentMenu,StrategyType strategyType, EventHandler  onItemClick, EventHandler closeAllCallback)
        {
            #region 生成菜单
             parentMenu.DropDownItems.Clear();

            var stategyGroups = StrategyManager.Instance.GetStrategyGroups(strategyType);

            var runAllItem = new ToolStripMenuItem("全部运行");
            parentMenu.DropDownItems.Add(runAllItem);

            foreach (var group in stategyGroups)
            {
                var subItem = new ToolStripMenuItem(group.name);
                subItem.Tag = group;
                subItem.Click += onItemClick;
                subItem.Name = RunAllMenuItem;
                runAllItem.DropDownItems.Add(subItem);
            }

            var stopAllItem = new ToolStripMenuItem("关闭所有");
            parentMenu.DropDownItems.Add(stopAllItem);
            stopAllItem.Click += closeAllCallback;

            /*
            var emulationItem = new ToolStripMenuItem("复盘测试");
            parentMenu.DropDownItems.Add(emulationItem);
            foreach (var group in stategyGroups)
            {
                var subItem = new ToolStripMenuItem(group.name);
                subItem.Tag = group;
                subItem.Click += OnClickEmulationMenuItem;
                
                emulationItem.DropDownItems.Add(subItem);
            }

            */

            parentMenu.DropDownItems.Add(new ToolStripSeparator());

            var currencies = Config.Instance.PlatformConfig.Currencies;
            foreach (string s in currencies)
            {
                if (strategyType == StrategyType.Swap)
                {
                    if (!InstrumentManager.SwapInstrument.HasInstrumentByCoin(s))
                        continue;
                }

                var item = new ToolStripMenuItem();
                item.Text = s.Trim().ToUpper();

                foreach (var group in stategyGroups)
                {
                    var subItem = new ToolStripMenuItem(group.name);
                    subItem.Tag = group;
                    subItem.Click += onItemClick;
                    subItem.Name = s;
                    item.DropDownItems.Add(subItem);
                }

                parentMenu.DropDownItems.Add(item);
            }

            #endregion
        }

        private void OnClickEmulationMenuItem(object sender, EventArgs args)
        {
            var menuItem = sender as ToolStripMenuItem;
            var group = menuItem.Tag as StrategyGroup;
            var window = new WinEmulatorGuid();
            window.SetStrategyGroup(group);
            window.Show();
        }

        private IEnumerator<IYieldInstruction> RunAllSwap(StrategyGroup group)
        {
            mnSwap.Enabled = false;
            foreach (var s in Config.Instance.PlatformConfig.Currencies)
            {
                if (InstrumentManager.SwapInstrument.HasInstrumentByCoin(s))
                {
                    string instId = $"{s}-{Config.Instance.UsdCoin}-SWAP".ToUpper();
                    if (UseSmallSwapView)
                    {
                        if (AddView<SwapViewSmall>(instId, group, true))
                        {
                            yield return null;
                        }
                        else
                        {
                            mnSwap.Enabled = true;
                            yield break;
                        }
                    }
                    else
                    {
                        if (AddView<SwapView>(instId, group, true))
                        {
                            yield return null;
                        }
                        else
                        {
                            mnSwap.Enabled = true;
                            yield break;
                        }
                    }
                }
            }

            mnSwap.Enabled = true;
        }
        private IEnumerator<IYieldInstruction> RunAllSpot(StrategyGroup group)
        {
            mnSpots.Enabled = false;
            foreach (var s in Config.Instance.PlatformConfig.Currencies)
            {
                string instId = $"{s}-{Config.Instance.UsdCoin}".ToUpper();
                if (AddView<SpotView>(instId, group, true))
                {
                    yield return null;
                }
                else
                {
                    mnSpots.Enabled = true;
                    yield break;
                }
            }

            mnSpots.Enabled = true;
        }
        private void OnSpotStrategyMenuItemClick(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            var group = item.Tag as StrategyGroup;
            var currency = item.Name;
            if (currency == RunAllMenuItem)
            {
                Coroutine.StartCoroutine(RunAllSpot(group));
                return;
            }

            string instId = $"{currency}-{Config.Instance.UsdCoin}".ToUpper();
            AddView<SpotView>(instId, group, false);
        }

        private void OnSwapStrategyMenuItemClick(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            var group = item.Tag as StrategyGroup;
            var currency = item.Name;
            if (currency == RunAllMenuItem)
            {
                Coroutine.StartCoroutine(RunAllSwap(group));
                return;
            }

            string instId = $"{currency}-{Config.Instance.UsdCoin}-SWAP".ToUpper();

            // 创建一个工作流
            var db = MysqlHelper.Instance.getDB();
            var dbId = db.Insertable(new Workflow { Instrument = instId, Strategy = group.name }).ExecuteReturnIdentity();
            Logger.Instance.LogInfo($"创建工作流:{instId}_{group.name}");

            if (UseSmallSwapView)
            {
                AddView<SwapViewSmall>(instId, group, false);
            }
            else
            {
                AddView<SwapView>(instId, group, false);
            }
        }

        private void RemoveAllView<T>() where T:UserControl
        {
            for (var i = this.pnlMarketViews.Controls.Count - 1; i >= 0; i--)
            {
                var view = pnlMarketViews.Controls[i] as T;
                if (view != null)
                {
                    view.Parent = null;
                }
            }
        }
        private bool AddView<T>(string instId,StrategyGroup strategyGroup,bool run = false) where T:UserControl,IMarketView,new()
        {
            foreach (var c in this.pnlMarketViews.Controls)
            {
                var v = c as IMarketView;

                if (v != null)
                {
                    if (string.Compare(instId, v.InstId, true) == 0)
                    {
                        MessageBox.Show($"{instId}已运行中， 请先关闭", "已存在运行中的策略", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                }
            }

            string err;
            if(!StrategyRunner.Instance.RunStrategy(instId,strategyGroup,run, out err))
            {
                MessageBox.Show(err,"错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.Instance.LogError(err);
                return false;
            }

            var view =new T() as UserControl;
            this.pnlMarketViews.Controls.Add(view);
            (view as IMarketView).SetInstId(instId);
            return true;
        }
        private void CloseAllSwap(object sender, EventArgs e)
        {
            RemoveAllView<SwapView>();
            RemoveAllView<SwapViewSmall>();
        }

        private void CloseAllSpot(object sender, EventArgs e)
        {
            RemoveAllView<SpotView>();
        }

        private void ShowFundsStrategyList()
        {
            foreach (System.Windows.Forms.Control c in this.pnlBehavior.Controls)
            {
                var view = c as StrategyView;
                c.Visible = false;
            }

            for (int i = pnlBehavior.Controls.Count; i < this.fundsStrategies.Count; i++)
            {
                var view = new StrategyView();
                pnlBehavior.Controls.Add(view);
            }

            for (int i = 0; i < this.fundsStrategies.Count; i++)
            {
                var view = pnlBehavior.Controls[i] as StrategyView;
                view.SetStrategy(this.fundsStrategies[i]);
                view.Visible = true;
            }
        }

        private void ChangeMarketType()
        {
            this.mniBank.Visible = false;
            this.mniBrowser.Visible = false;
            this.mnSwap.Visible = AccountManager.Current.SwapOpened;

            this.mnSpots.Visible = true;
            this.tabPageOrders.Hide();
        }
        private void ShowMonitorList()
        {
            foreach (System.Windows.Forms.Control c in this.pnlMonitor.Controls)
            {
                c.Visible = false;
                (c as MonitorView).monitor = null;
            }

            int mindex = 0;

            IEnumerable<MonitorBase> monitors = MonitorSchedule.Default.GetAllMonitor();

            foreach (var m in monitors)
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

        private OrderView GetOrderView(int index)
        {
            var views = this.pnlMyOrders.Controls;

            OrderView view = views.Count > index ? views[index] as OrderView : null; // new OrderView();

            if (view == null)
            {
                view = new OrderView();
                view.OnCancelled += View_OnCancelled;
                this.pnlMyOrders.Controls.Add(view);
                MyOrderViews.Add(view);
            }

            view.Visible = true;

            return view;
        }

        /// <summary>
        /// 显示订单列表
        /// </summary>
        private void ShowMyOrders()
        {
            int index = 0;
            int used = 0;

            TradeOrderManager manager = TradeOrderManager.Instance;

            manager.EachSellOrder((o) =>
            {
                var view = this.GetOrderView(index++);
                view.SetOrder(o);
                used++;
            });

            manager.EachBuyOrder((o) =>
            {
                var view = this.GetOrderView(index++);
                view.SetOrder(o);
                used++;
            });


            var views = this.pnlMyOrders.Controls;

            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                v.Visible = index > i;
            }
        }
        private void View_OnCancelled(long id)
        {
            foreach (OrderView view in this.MyOrderViews)
            {
                if (view.OrderID == id)
                {
                    view.Visible = false;
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Logger.Instance.NewLog += this.ShowLog;

            this.groupBox1.Text = string.Format("稳定币{0}", Config.Instance.UsdCoin);

            ReloadCurrencyMenus();

            MonitorSchedule.Default.AddMonotor(new TimeMonitor());

            EventCenter.Instance.AddListener(this);
            PureMVC.SendNotification(CoreEvent.SocketConnect);

            this.OnInitWindow();
        }

        private void OnInitWindow()
        {
            if (isInited)
                return;

            this.pnlMarketViews.Visible = true;
            lblLoginName.Text = Config.Instance.Account.LoginName;
            isInited = true;

            var usdx = Config.Instance.UsdCoin;
            USDXWallet.CreateInstance(usdx);

            //加载资金管理策略
            var strategyGroup = StrategyManager.Instance.GetStrategyGroups(StrategyType.Funds);

            string err;

            foreach (var g in strategyGroup)
            {
                StrategyRunner.Instance.RunStrategy(usdx, g,true, out err);

                if (!string.IsNullOrEmpty(err))
                {
                    Logger.Instance.LogError(err);
                }
            }

            fundsStrategies = StrategyRunner.Instance.GetStrategiesByInstId(usdx);

            ChangeMarketType();
        }

        private void OnConfigChanged(object obj)
        {
            ReloadCurrencyMenus();
            GenerateTitle();
        }

        private void UpdateUsdxAmountInfo()
        {
            var usdxWallet = USDXWallet.Instance;
            if (usdxWallet != null)
            {
                this.lblCTCUsdt.Text = $"可用{usdxWallet.AvailableInTrading:0.00} 冻结{usdxWallet.FrozenInTrading:0.00}";
                this.lblOTCUsdt.Text = $"可用{usdxWallet.AvailableInAccount:0.00} 冻结{usdxWallet.FrozenInAccount:0.00}";
                lblUsdtCny.Text = usdxWallet.Total.ToString("0.00");
            }
        }

        private delegate void ShowLogDelegate(Log log);

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int WM_VSCROLL = 0x115;
        private const int SB_VERT = 0x1;
        private const int SB_THUMBPOSITION = 4;
        private const int SB_BOTTOM = 7;
        private const int WM_SETREDRAW = 11;
        [DllImport("user32.dll")]
        private static extern int GetScrollPos(IntPtr hWnd, int nBar);

        private void ShowLog(Log log)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new ShowLogDelegate(ShowLog), log);
                return;
            }
            // 禁用重绘，防止控件在修改时跳动
            SendMessage(textBoxConsole.Handle, WM_SETREDRAW, 0, 0);
            // 记录当前选区
            int selectionStart = textBoxConsole.SelectionStart;
            int selectionLength = textBoxConsole.SelectionLength;
            bool isSelecting = selectionLength > 0; // 是否有选中的文本
            // 获取当前滚动条位置
            int scrollPos = GetScrollPos(textBoxConsole.Handle, SB_VERT);
            // 追加新日志
            string newLog = "\r\n" + log.ToString();
            // 处理文本超长情况，防止崩溃
            if (textBoxConsole.TextLength + newLog.Length > 32767)
            {
                string[] lines = textBoxConsole.Lines;
                int removeLines = lines.Length / 4; // 每次移除 1/4 的内容，减少性能开销
                textBoxConsole.Lines = lines.Skip(removeLines).ToArray();
            }
            // 记录鼠标点击位置
            int mouseClickPosition = textBoxConsole.SelectionStart;
            // 追加新文本
            textBoxConsole.AppendText(newLog);
            // 恢复鼠标点击时的光标位置
            textBoxConsole.SelectionStart = mouseClickPosition;

            // 如果有选中内容，恢复选区
            if (isSelecting)
            {
                textBoxConsole.SelectionStart = selectionStart;
                textBoxConsole.SelectionLength = selectionLength;
            }
            // 恢复滚动条位置
            if (!textBoxConsole.Focused)
            {
                SendMessage(textBoxConsole.Handle, WM_VSCROLL, SB_BOTTOM, 0);
            }
            else
            {
                SendMessage(textBoxConsole.Handle, WM_VSCROLL, SB_THUMBPOSITION + 0x10000 * scrollPos, 0);
            }
            // 重新启用控件重绘
            SendMessage(textBoxConsole.Handle, WM_SETREDRAW, 1, 0);
            // 强制控件重绘，刷新界面
            textBoxConsole.Invalidate();
        }


        private void buttonClearLog_Click(object sender, EventArgs e)
        {
            this.textBoxConsole.Text = string.Empty;
        }

        private void timer_state_scan_Tick(object sender, EventArgs e)
        {
            decimal money = USDXWallet.Instance != null ? USDXWallet.Instance.Total : 0;
            foreach (var c in this.pnlMarketViews.Controls)
            {
                var mv = c as IMarketView;
                money += mv.TotalAmount;
            }

            this.lblTotalMoney.Text = money.ToString("0.00");

            if (this.tabControl1.SelectedTab == tabPageOrders)
            {
                ShowMyOrders();
            }

            UpdateUsdxAmountInfo();
        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void DataView_DBClick(object sender, MouseEventArgs e)
        {

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (this.isClosed)
            {
                return;
            }

            Ticker.Stop();

            PureMVC.SendNotification(CoreEvent.SocketDisconnect);
            StrategyRunner.Instance.StopAll();

            MonitorSchedule.Default.Dispose();
            EventCenter.Instance.RemoveListener(this);
        }

        private void 账号设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var win = new WinCTCConfig();// new WinConfig();
            win.Show();
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            (new WinPassword()).ShowDialog();
        }

        private void 浏览器设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void 收付款设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }


        private void WinMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (!this.isClosed)
            {
                this.isClosed = true;
                WindowManager.Instance.CloseAll();
            }
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.tabControl1.SelectedTab == this.tabPageMonitor)
            {
                this.pnlMonitor.Controls.Clear();
            }

            if (this.tabControl1.SelectedTab == this.tabPageFunds)
            {
                this.ShowFundsStrategyList();
            }
            else if (this.tabControl1.SelectedTab == this.tabPageMonitor)
            {
                this.ShowMonitorList();
            }
        }

        public IEnumerable<EventListenItem> GetEvents()
        {
            return new EventListenItem[] {new EventListenItem(EventNames.ConfigChanged,this.OnConfigChanged)};
        }

        private void 合约查询ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WindowManager.Instance.OpenWindow<WinInstrument>();
        }

        private void 持仓管理ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WindowManager.Instance.OpenWindow<WinPosition>();

        }

        private void 合约管理ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //WindowManager.Instance.OpenWindow<WinSwapManager>();
        }

        private void mniCopyConfig_Click(object sender, EventArgs e)
        {
            WindowManager.Instance.OpenWindow<WinCopyConfig>();
        }

        private void 交叉盘ToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            WindowManager.Instance.OpenWindow<WinCross>();
        }

        private void 合约回测ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var stategyGroups = StrategyManager.Instance.GetStrategyGroups(StrategyType.Swap);
            var group = stategyGroups.First(x => x.name == "合约交易策略(随机方向)");

            var window = new WinEmulatorGuid();
            window.SetStrategyGroup(group);
            window.Show();
        }
    }
}
