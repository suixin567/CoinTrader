
using CefSharp.DevTools.Profiler;
using CoinTrader.Common.Database;
using CoinTrader.Forms.Entity;
using CoinTrader.OKXCore.Entity;
using CoinTrader.OKXCore.Enum;
using CoinTrader.OKXCore.Manager;
using System;

using System.Drawing;
 
using System.Windows.Forms;

namespace CoinTrader.Forms.Control
{
    public partial class SwapInfoView : UserControl
    {
        public SwapInfoView()
        {
            InitializeComponent();
        }

        public long Id
        {
            get; private set;
        }

        private InstrumentSwap _instrument;

        public void SetId(long id)
        {
            this.Id = id;

            var position = PositionManager.Instance.GetPosition(this.Id);

            if (position != null)
            {
                this._instrument = InstrumentManager.SwapInstrument.GetInstrument(position.InstId);
                this.lblName.Text = position.InstName;
                this.lblSide.Text = position.PosSideName;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            var position = PositionManager.Instance.GetPosition(this.Id);

            if (position != null)
            {
                this.lblAvgPx.Text = position.AvgPx.ToString(_instrument.PriceFormat);
                this.lblLiqPx.Text = position.LiqPx.ToString(_instrument.PriceFormat);
                this.lblUpl.Text = String.Format("{0:0.00} ({1:P})", position.Upl, position.UplRatio);
                this.lblUpl.ForeColor = position.Upl >= 0 ? Color.Green : Color.Red;
                this.lblMargin.Text = String.Format("{0:0.00} ({1:P})", position.Margin, position.MgnRatio);
                this.lblLever.Text = position.Lever + "x";
                this.lblPx.Text = position.MarkPx.ToString(_instrument.PriceFormat);
                this.lblAmount.Text = (position.Pos * _instrument.CtVal).ToString(_instrument.AmountFormat) + _instrument.CtValCcy;
                // 设置窗体标题
                var form = this.FindForm();
                if (form != null && form is WinPosition)
                {
                    form.Text = $"合约持仓管理 - {position.InstName} {position.PosSideName}{position.Lever}x    " + String.Format("{0:0.00} ({1:P})", position.Upl, position.UplRatio);
                }
            }
            else
            {
                //this.Parent = null;//自动移除？
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Parent = null;
        }

        private void Liquidate_Click(object sender, EventArgs e)
        {
            var win = new WinSwapLiquidate();
            win.SetId(this.Id);

            win.ShowDialog();
        }

        private void btnClose_Click_1(object sender, EventArgs e)
        {

            var ok = MessageBox.Show("确定全部平仓?", "平仓", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (ok == DialogResult.OK)
            {
                var position = PositionManager.Instance.GetPosition(this.Id);
                // 记录操作到数据库
                var db = MysqlHelper.Instance.getDB();
                var workflow = db.Queryable<Workflow>().Where(it => it.Instrument == position.InstId && it.Status == 1).First();
                var operationId = 0;
                if (workflow != null)
                {
                    Operation newOperation = new Operation()
                    {
                        WorkflowId = workflow.Id,
                        Side = (byte)PositionType.Short,
                        Status = 1
                    };
                    operationId = db.Insertable(newOperation).ExecuteReturnIdentity();
                }
                var result = PositionManager.Instance.ClosePosition(this.Id);
                if (result)
                {
                    if (workflow != null)
                    {
                        // 标记为操作成功
                        db.Updateable<Operation>()
                       .SetColumns(it => it.Des == "手动平仓")
                       .SetColumns(it => it.Profit == position.Margin * (decimal)position.UplRatio)
                       .SetColumns(it => it.Status == 1)
                       .Where(it => it.Id == operationId)
                       .ExecuteCommand();
                    }
                }
                else
                {
                    if (workflow != null)
                    {
                        // 标记为操作失败
                        db.Updateable<Operation>()
                        .SetColumns(it => it.Status == 2)
                        .Where(it => it.Id == operationId)
                        .ExecuteCommand();
                    }
                }
            }
        }

        private void btnMagrin_Click(object sender, EventArgs e)
        {
            var win = new WinMarginChange();
            win.SetId(this.Id);

            win.ShowDialog();
        }



        private void btnStop_Click(object sender, EventArgs e)
        {
            var win = new WinSwapStop();
            win.SetId(this.Id);
            win.ShowDialog();
        }
    }
}
