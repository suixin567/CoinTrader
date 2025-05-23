﻿/*
 * 这是一个示例文件，
 * 放在外置的StrategiesCustomized文件夹下
 * 每次重启的时候就会动态加载StrategiesCustomized文件夹下的 .cs和.csx文件
 * 这个文件里的代码是动态编译，修改后重启生效。
*/
using System;
using System.Collections.Generic;
using CoinTrader.Common.Classes;
using CoinTrader.OKXCore.Entity;
using CoinTrader.OKXCore.Enum;
using CoinTrader.Common;
using CoinTrader.Strategies;
using CoinTrader.Common.Database;
using CoinTrader.Strategies.Runtime;
using static CoinTrader.Forms.Control.CustomProgressBar;
using CoinTrader.Forms.Control;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;
using MySqlX.XDevAPI.Common;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace CoinTrader.Forms.Strategies.Customer
{

    [Strategy(Name = "合约交易策略(随机方向)")]
    internal class RandomSwapStrategy : SwapStrategyBase
    {
        [StrategyParameter(Name = "仓位大小(USD)", Min = 1, Max = 100000000, Intro = "按稳定币计价的仓位大小")]
        public decimal PositionSizeUsd { get; set; }

        private uint lever = 1;
        [StrategyParameter(Name = "杠杆倍数", Min = 1, Max = 150)]
        public uint Lever
        {
            get
            {
                return lever;
            }
            set
            {
                lever = value;
                resetLever = true;
            }
        }
    
        [StrategyParameter(Name = "仓位模式")]
        public SwapMarginMode Mode{get;set;}

        [StrategyParameter(Name = "趋势")]
        public Direction DirectionType{get;set; }

        [StrategyParameter(Name = "K线采样数",Min = 1,Max = 200)]
        public int KLinSample{get; set;}

        [StrategyParameter(Name = "波动幅度(%)", Min = .01, Max = 1000, Intro = "触发涨跌幅")]
        public float Range{ get;set;}

        [StrategyParameter(Name = "K线满足数", Min = 1, Max = 200,Intro = "满足多少个符合涨跌幅的K线")]
        public int KLineCount{ get;set; }

        CandleGranularity _candle;
        [StrategyParameter(Name = "K线周期")]
        public CandleGranularity OpenCandle
        {
            get { return _candle; }
            set
            {
                if (_candle != value)
                {
                    UnloadCandle(this._candle);
                    _candle = value;
                }
            }
        }

        [StrategyParameter(Name = "止损(%)", Min = .1, Max = 100)]
        public float StopLoss
        {
            get; set;
        }

        [StrategyParameter(Name = "止盈(%)", Min = .1, Max = 100)]
        public float StopSurplus
        {
            get; set;
        }

        [StrategyParameter(Name = "移动止盈")]
        public bool MoveProfit
        {
            get;set;
        }

        [StrategyParameter(Name = "移动止盈回调幅度(%)",Dependent = "MoveProfit",DependentValue = true, Min = .1,Max =100)]
        public float RetracementPercent
        {
            get; set;
        }

        [StrategyParameter(Name = "开启补仓")]
        public bool OpenAppend
        {
            get; set;
        }

        [StrategyParameter(Name = "最大补仓次数", Min = 1, Max = 100, Dependent = "OpenAppend", DependentValue = true, Intro = "亏损幅度到达幅度后总共进行几次补仓")]
        public decimal AppendTimes
        {
            get; set;
        }

        [StrategyParameter(Name = "补仓亏损幅度(%)", Min = 0.1, Max = 100, Dependent = "OpenAppend", DependentValue = true, Intro = "当前仓位亏损幅度达到多少的时候进行一次补仓")]
        public decimal AppendLoss
        {
            get; set;
        }

        /// <summary>
        /// 是否需要重新设置杠杆倍数
        /// </summary>
        private bool resetLever = true;

        /// <summary>
        /// 触发移动止盈的最后最高（最低）价格记录
        /// </summary>
        public decimal lastTriggerPrice = 0;
        public DateTime bannedTime;
        public string debugText;
        /// <summary>
        /// 交易冷却（主要是等待同步数据)
        /// </summary>
        private float delay = 2.0f;

        public CustomProgressBar.ProgressDirection CustomProgressBarDirection;
        public float CustomProgressBarMin;
        public float CustomProgressBarMax;
        public float CustomProgressBarValue;
        public Marker[] CustomProgressBarMarkers;

        public RandomSwapStrategy()
        {
            this.KLinSample = 10;
            this.KLineCount = 5;
            this.StopLoss = 10;
            this.StopSurplus = 10;
            this.Range = 1;
            this.RetracementPercent = 1;
            this.lever = 10;
            this.PositionSizeUsd = 500;
            this.AppendLoss = 10;
            this.AppendTimes = 1;
            this._candle = CandleGranularity.M15;
        }

        /// <summary>
        /// 初始化函数， 传入instId
        /// </summary>
        /// <param name="instId">代表这个策略将在那个交易品种上运行</param>
        /// <returns>成功返回true，失败返回false</returns>
        public override bool Init(string instId)
        {
            //基类初始化成功
            if (base.Init(instId))
            {
                if (runtime is SwapStrategyEmulatorRuntime) {
                    delay = 0;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 每次收到服务器端报价的时候就会执行这个函数
        /// </summary>
        protected override void  OnTick()
        {
            this.Executing = false;
            if (!Effective)//数据不正常
                return;
 
            IList<Position> postions = GetPositions(); //获取当前币种的所有的持仓
            var coinAmount = GetPostionSize(Ask, Bid);//获取每次下单的数量

            Position pos = postions.Count > 0 ? postions[0] : null;

            Message = string.Format("仓位大小 {0}{1}", coinAmount.ToString(instrument.AmountFormat), instrument.CtValCcy);

            if (pos == null) //没有持仓
            {
                PositionType side;
                if (coinAmount > 0 && CanOpen(Ask, Bid, out side, out string des) && !isBanned()) //判断是否可以开仓
                {
                    this.Executing = true;
                    MoveProfit = false; //新仓位禁止移动止盈
                    lastTriggerPrice = 0;//清零移动止盈标记价格
                    if (resetLever) //重置杠杆倍数
                    {
                        SetLever(side, Mode, lever);
                        resetLever = false;
                    }
                    // 记录操作到数据库
                    var db = MysqlHelper.Instance.getDB();
                    var workflow = db.Queryable<Workflow>().Where(it => it.Instrument == InstId && (it.Status == 0 || it.Status == 1)).First();
                    Operation newOperation = new Operation()
                    {
                        WorkflowId = workflow.Id,
                        Side = (byte)side,
                        Des = "准备开仓 side:" + side.ToString(),
                    };
                    var operationId = db.Insertable(newOperation).ExecuteReturnIdentity();
                    if (CreatePosition(side, coinAmount, Mode) > 0)//判断是否下单成功
                    {
                        string des2 = des + $" {Lever}x 市价:{GetClosePrice(side, Ask, Bid)}";
                        // 标记为操作成功
                        db.Updateable<Operation>()
                       .SetColumns(it => it.Des == des2)
                       .SetColumns(it => it.Status == 1)
                       .Where(it => it.Id == operationId)
                       .ExecuteCommand();
                        // 标记工作流为工作状态
                        db.Updateable<Workflow>()
                         .SetColumns(it => it.Status == 1)
                         .Where(it => it.Id == workflow.Id)
                         .ExecuteCommand();
                    }
                    else {
                        // 标记为操作失败
                        db.Updateable<Operation>()
                        .SetColumns(it => it.Status == 2)
                        .Where(it => it.Id == operationId)
                        .ExecuteCommand();
                    }
                    Wait(delay);
                }
            }
            else //已持仓情况
            {
                if (CanClose(pos, Ask, Bid, out string des, out decimal profit))//判断是否可以平仓， 包括止盈、止损两种情况
                {
                    this.Executing = true;
                    // 记录操作到数据库
                    var db = MysqlHelper.Instance.getDB();
                    var workflow = db.Queryable<Workflow>().Where(it => it.Instrument == InstId && it.Status == 1).First();
                    var operationId = 0;
                    if (workflow != null) {
                        Operation newOperation = new Operation()
                        {
                            WorkflowId = workflow.Id,
                            Status = 1,
                            Des = "准备平仓",
                        };
                        operationId = db.Insertable(newOperation).ExecuteReturnIdentity();
                    }
                    var result = ClosePosition(pos.PosId); //平仓
                    if (result)
                    {
                        if (workflow != null)
                        {
                            // 标记为操作成功
                            db.Updateable<Operation>()
                           .SetColumns(it => it.Des == des)
                           .SetColumns(it => it.Profit == profit)
                           .SetColumns(it => it.Status == 1)
                           .SetColumns(it => it.Position == pos.Margin)
                           .SetColumns(it => it.Fee == GetTradeFee(pos)) // 记录平仓交易费用
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
                    Wait(delay);
                }
                else if (CanAppend(pos, Ask, Bid))//是否可以追加仓位
                {
                    this.Executing = true;
                    if (CreatePosition(pos.SideType, coinAmount, Mode) > 0)//判断是否下单成功
                    {
                        PlusAppendTimes(pos.PosId);
                        Wait(delay);
                    }
                    lastTriggerPrice = 0;//清零移动止盈标记价格
                }
            }
        }

        private decimal GetTradeFee(Position pos)
        {
            return (decimal)FundingRate * pos.Margin;
        }

        /// <summary>
        /// 获取仓位加仓次数
        /// </summary>
        /// <param name="posId"></param>
        /// <returns></returns>
        private int GetAppendTimes(long posId)
        {
            string storageKey = GetStorageKey(posId);
            return LocalStorage.GetValue<int>(storageKey);
        }

        /// <summary>
        /// 本地存储键
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private string GetStorageKey(long id)
        {
            return string.Format("append_times_{0}", id);
        }


        /// <summary>
        /// 保存补仓次数
        /// </summary>
        /// <param name="posId"></param>
        private void PlusAppendTimes(long posId)
        {
            string storageKey = GetStorageKey(posId);
            int times = LocalStorage.GetValue<int>(storageKey);
            times++;
            LocalStorage.SetValue(storageKey, times);
        }

        /// <summary>
        /// 计算可以购买多少个币， 仓位是以主币种数量来定。 不是按张数
        /// </summary>
        /// <param name="ask"></param>
        /// <param name="bid"></param>
        /// <returns></returns>
        private decimal GetPostionSize(decimal ask, decimal bid)
        {
            var balance = QuoteAvailable; //获取稳定币结余
            var quoteAmount = Math.Min(balance, PositionSizeUsd);
            var amount = quoteAmount / ((ask + bid) * 0.5M) * Lever;//计算出每次下单的大小
            return amount;
        }

        /// <summary>
        /// 判断是否可以平仓（止盈或止损）
        /// </summary>
        /// <param name="pos">仓位</param>
        /// <param name="ask">卖价</param>
        /// <param name="bid">买价</param>
        /// <returns></returns>
        private bool CanClose(Position pos, decimal ask, decimal bid, out string operationDes, out decimal operationProfit)
        {
            operationDes = "";
            operationProfit = 0;
            var profit = GetProfitPercent(pos, ask, bid);// 盈利百分比
            //Logger.Instance.LogDebug($"利润:{profit.ToString("F2")}% 止盈:{this.StopSurplus}% 止损:{-this.StopLoss}%");
            if (profit > 0) //持仓盈利的情况
            {
                if (MoveProfit) //移动盈利
                {
                    var closePrice = GetClosePrice(pos.SideType, ask, bid); //根据方向得到最近的可平仓市价
                    if (RetracementPercent == 0) // 初始化移动止盈时的容忍回撤幅度
                    {
                        RetracementPercent = updateRetracement((float)profit); //刷新动态容忍回撤幅度
                    }
                    else if (profit >= (decimal)StopSurplus) //达到盈利目标，开始记录移动止盈的最高（最低）价格
                    {
                        switch (pos.SideType)
                        {
                            case PositionType.Long: //多头持仓的情况
                                var newHigh = lastTriggerPrice == 0 ? closePrice : Math.Max(lastTriggerPrice, closePrice);
                                if (newHigh > lastTriggerPrice)
                                {
                                    lastTriggerPrice = newHigh;// 刷新最高价
                                    Logger.Instance.LogInfo("多头刷新最高价:" + lastTriggerPrice);
                                    RetracementPercent = updateRetracement((float)profit); //刷新动态容忍回撤幅度
                                }
                                break;
                            case PositionType.Short: //空头持仓的情况
                                var newLow = lastTriggerPrice == 0 ? closePrice : Math.Min(lastTriggerPrice, closePrice);
                                if (newLow < lastTriggerPrice)
                                {
                                    lastTriggerPrice = newLow;// 刷新最低价
                                    Logger.Instance.LogInfo("空头刷新最底价:" + lastTriggerPrice);
                                    RetracementPercent = updateRetracement((float)profit); //刷新动态容忍回撤幅度
                                }
                                break;
                        }
                    }
                    // 进度条-移动止盈
                    // 止盈实际涨跌幅%
                    float realStopSurplusAmplitude_B = StopSurplus / pos.Lever;

                    if (pos.SideType == PositionType.Long)
                    {
                        CustomProgressBarDirection = ProgressDirection.LeftToRight;
                        CustomProgressBarMin = (float)pos.AvgPx;
                        CustomProgressBarMax = (float)lastTriggerPrice;
                        CustomProgressBarValue = (float)pos.MarkPx;

                        var longStopPrice = lastTriggerPrice * (1 - ToPercent(RetracementPercent / pos.Lever));//多头回撤价
                        var longStopSurplusPrice = pos.AvgPx * (1 + ToPercent(realStopSurplusAmplitude_B));// 多头常规止盈价
                        CustomProgressBarMarkers = new[] {
                        new CustomProgressBar.Marker { Position = (float)pos.AvgPx, TopLabel = pos.AvgPx.ToString("F5"), BottomLabel = "开仓" },
                        new CustomProgressBar.Marker { Position = (float)longStopSurplusPrice, TopLabel = longStopSurplusPrice.ToString("F5"), BottomLabel = $"{StopSurplus}%" },
                        new CustomProgressBar.Marker { Position = (float)longStopPrice , TopLabel = longStopPrice.ToString("F5"), BottomLabel = $"回撤{RetracementPercent.ToString("F2")}%" },
                        new CustomProgressBar.Marker { Position = (float)lastTriggerPrice, TopLabel = lastTriggerPrice.ToString("F5"), BottomLabel = "极限" }
                        };
                    }
                    else
                    {
                        CustomProgressBarDirection = ProgressDirection.RightToLeft;
                        CustomProgressBarMin = (float)lastTriggerPrice;
                        CustomProgressBarMax = (float)(pos.AvgPx);
                        CustomProgressBarValue = (float)pos.MarkPx;

                        var shortStopPrice = lastTriggerPrice * (1 + ToPercent(RetracementPercent / pos.Lever));//空头回撤价
                        var shortStopSurplusPrice = pos.AvgPx * (1 - ToPercent(realStopSurplusAmplitude_B));// 空头常规止盈价
                        CustomProgressBarMarkers = new[] {
                        new CustomProgressBar.Marker { Position = (float)pos.AvgPx, TopLabel = pos.AvgPx.ToString("F5"), BottomLabel = "开仓" },
                        new CustomProgressBar.Marker { Position = (float)shortStopSurplusPrice, TopLabel = shortStopSurplusPrice.ToString("F5"), BottomLabel = $"{StopSurplus}%" },
                        new CustomProgressBar.Marker { Position = (float)shortStopPrice , TopLabel = shortStopPrice.ToString("F5"), BottomLabel = $"回撤{RetracementPercent.ToString("F2")}%" },
                        new CustomProgressBar.Marker { Position = (float)(lastTriggerPrice), TopLabel = lastTriggerPrice.ToString("F5"), BottomLabel = "极限" },
                        };
                    }
                    if (lastTriggerPrice > 0) //判断是否触发移动止盈
                    {
                        switch (pos.SideType)
                        {
                            case PositionType.Long: //多头持仓的情况                                
                                var longStopPrice = lastTriggerPrice * (1 - ToPercent(RetracementPercent / pos.Lever));//多头回撤价
                                var longRetracemented = closePrice <= longStopPrice;// 多头回撤
                                debugText = $"多头-极限价:{lastTriggerPrice} 回撤价:{longStopPrice.ToString("F2")}";
                                if (longRetracemented)
                                {
                                    operationDes = $"多头回撤:{RetracementPercent.ToString("F2")}%后止盈 剩余盈利:{profit.ToString("F2")}% 标准盈利:{StopSurplus}% 开仓均价:{pos.AvgPx.ToString("F5")} 回撤价:{longStopPrice.ToString("F5")} 最高价:{lastTriggerPrice.ToString("F5")} 平仓价:{closePrice.ToString("F5")}";
                                    operationProfit = pos.Margin * profit / 100;
                                    Logger.Instance.LogInfo(operationDes);
                                }
                                //else if (profit < (decimal)StopSurplus) // 不吃亏，既有利润不能被侵犯
                                //{
                                //    longRetracemented = true;
                                //    operationDes = $"多头放弃更多利润的尝试，立即平仓，剩余收益:{profit.ToString("F2")}% (理论收益:{StopSurplus}%)  开仓均价:{pos.AvgPx.ToString("F5")} 最高价:{lastTriggerPrice.ToString("F5")} 平仓价:{closePrice.ToString("F5")}";
                                //    operationProfit = pos.Margin * profit / 100;
                                //    Logger.Instance.LogInfo(operationDes);
                                //}
                                return longRetracemented;
                            case PositionType.Short: //空头持仓的情况
                                var shortStopPrice = lastTriggerPrice * (1 + ToPercent(RetracementPercent / pos.Lever));//空头回撤价
                                var shortRetracemented = closePrice >= shortStopPrice;// 空头回撤
                                debugText = $"空头-极限价:{lastTriggerPrice} 回撤价:{shortStopPrice.ToString("F2")}";
                                if (shortRetracemented)
                                {
                                    operationDes = $"空头回撤:{RetracementPercent.ToString("F2")}%后止盈 剩余盈利:{profit.ToString("F2")}% 标准盈利:{StopSurplus}% 最低价:{lastTriggerPrice.ToString("F5")} 回撤价:{shortStopPrice.ToString("F5")} 开仓均价:{pos.AvgPx.ToString("F5")} 平仓价:{closePrice.ToString("F5")}";
                                    operationProfit = pos.Margin * profit / 100;
                                    Logger.Instance.LogInfo(operationDes);
                                }
                                //else if (profit < (decimal)StopSurplus) // 不吃亏，既有利润不能被侵犯
                                //{
                                //    shortRetracemented = true;
                                //    operationDes = $"空头放弃更多利润的尝试，立即平仓，剩余收益:{profit.ToString("F2")}% (理论收益:{StopSurplus}%)  开仓均价:{pos.AvgPx.ToString("F5")} 最低价:{lastTriggerPrice.ToString("F5")} 平仓价:{closePrice.ToString("F5")}";
                                //    operationProfit = pos.Margin * profit / 100;
                                //    Logger.Instance.LogInfo(operationDes);
                                //}
                                return shortRetracemented;
                        }
                    }
                }
                else
                {
                    if (profit >= (decimal)StopSurplus) //达到盈利目标，转换为移动止盈
                    {
                        var closePrice = GetClosePrice(pos.SideType, ask, bid); //根据方向得到最近的可平仓市价
                        lastTriggerPrice = closePrice; // 设置回撤极值
                        MoveProfit = true;
                        RetracementPercent = 0;// 重置容忍回撤幅度
                        Logger.Instance.LogInfo((pos.SideType == PositionType.Long ? "多头" : "空头") + "转换为移动止盈");
                        // 记录操作到数据库
                        var db = MysqlHelper.Instance.getDB();
                        var workflow = db.Queryable<Workflow>().Where(it => it.Instrument == InstId && it.Status == 1).First();
                        if (workflow != null)
                        {
                            Operation newOperation = new Operation()
                            {
                                WorkflowId = workflow.Id,
                                Des = (pos.SideType == PositionType.Long ? "多头" : "空头") + "转换为移动止盈",
                                Status = 1
                            };
                            db.Insertable(newOperation).ExecuteReturnIdentity();
                        }
                        return false;
                    }
                }
            }
            else //持仓亏损的情况
            {
                if (profit <= -(decimal)StopLoss)//到达止损亏损幅度
                {
                    operationDes = $"止损:{profit.ToString("F2")}% 基准{StopLoss}% 仓位:{pos.Margin} 开仓均价{pos.AvgPx} 可平仓市价:{GetClosePrice(pos.SideType, ask, bid)} 平仓标记价:{pos.MarkPx} 最新成交价:{pos.Last} 亏损额:{pos.Margin * profit / 100}";
                    operationProfit = pos.Margin * profit / 100;
                    return true;
                }
            }
            // 进度条-在常规盈利/亏损范围内
            CustomProgressBarValue = (float)pos.MarkPx;
            // 止损实际涨跌幅%
            float realStopLossAmplitude = StopLoss / pos.Lever;
            // 止盈实际涨跌幅%
            float realStopSurplusAmplitude = StopSurplus / pos.Lever;

            if (pos.SideType == PositionType.Long)
            {
                CustomProgressBarDirection = ProgressDirection.LeftToRight;
                CustomProgressBarMin = (float)(pos.AvgPx * (1 - ToPercent(realStopLossAmplitude)));
                CustomProgressBarMax = (float)(pos.AvgPx * (1 + ToPercent(realStopSurplusAmplitude)));

                CustomProgressBarMarkers = new[] {
                        new CustomProgressBar.Marker { Position = CustomProgressBarMin, TopLabel = CustomProgressBarMin.ToString("F5"), BottomLabel = $"止损{StopLoss}%" },
                        new CustomProgressBar.Marker { Position = CustomProgressBarMax, TopLabel = CustomProgressBarMax.ToString("F5"), BottomLabel = $"止盈{StopSurplus}%" },
                        new CustomProgressBar.Marker { Position = (float)pos.AvgPx, TopLabel = pos.AvgPx.ToString("F5"), BottomLabel = "开仓" }
                        };
            }
            else
            {
                CustomProgressBarDirection = ProgressDirection.LeftToRight;
                CustomProgressBarMin = (float)(pos.AvgPx * (1 - ToPercent(realStopSurplusAmplitude)));
                CustomProgressBarMax = (float)(pos.AvgPx * (1 + ToPercent(realStopLossAmplitude)));

                CustomProgressBarMarkers = new[] {
                        new CustomProgressBar.Marker { Position = CustomProgressBarMin, TopLabel = CustomProgressBarMin.ToString("F5"), BottomLabel = $"止盈{StopSurplus}%" },
                        new CustomProgressBar.Marker { Position = CustomProgressBarMax, TopLabel = CustomProgressBarMax.ToString("F5"), BottomLabel = $"止损{StopLoss}%" },
                        new CustomProgressBar.Marker { Position = (float)pos.AvgPx, TopLabel = pos.AvgPx.ToString("F5"), BottomLabel = "开仓" }
                        };
            }
            return false;
        }

        // 计算移动止盈时 动态容忍回撤幅度
        float updateRetracement(float profit)
        {
            var retracement = (profit - StopSurplus) / 2;
            // 回撤幅度不能低于1%,否则再算上杠杆后，过于灵敏
            if (retracement < 2) {
                retracement = 2;
            }
            return retracement;
        }

        public class MarketTrendProbability
        {
            public float BullishProbability { get; set; }
            public float BearishProbability { get; set; }
            public float SidewaysProbability { get; set; }
        }

        //public class MarketTrendProbabilityB
        //{
        //    public float BullishProbability { get; set; }
        //    public float BearishProbability { get; set; }
        //    public float SidewaysProbability { get; set; }
        //    public bool ChatWaitting { get; set; }
        //}

        Dictionary<CandleGranularity, MarketTrendProbability> MarketTrendProbabilities = new Dictionary<CandleGranularity, MarketTrendProbability>();
        bool chatWaittingM15 = false;
        /// <summary>
        /// 判断是否可以开仓
        /// </summary>
        /// <param name="ask"></param>
        /// <param name="bid"></param>
        /// <returns></returns>
        private bool CanOpen(decimal ask, decimal bid, out PositionType finalSide, out string des)
        {
            if (!MarketTrendProbabilities.ContainsKey(CandleGranularity.M15))
            {
                chatM15();
            }
            if (!MarketTrendProbabilities.ContainsKey(CandleGranularity.M15))
            {
                finalSide = PositionType.Long;
                des = string.Empty;
                Logger.Instance.LogDebug("等待ai返回15分钟K线概率...");
                return false;
            }
            float totalBullishProbability = MarketTrendProbabilities[CandleGranularity.M15].BullishProbability;
            float totalBearishProbability = MarketTrendProbabilities[CandleGranularity.M15].BearishProbability;
            float totalSidewaysProbability = MarketTrendProbabilities[CandleGranularity.M15].SidewaysProbability;

            if (totalSidewaysProbability >= totalBullishProbability && totalSidewaysProbability >= totalBearishProbability)
            {
                Random _random = new Random();
                // 横盘时，随机方向考虑概率倾向
                float bullWeight = totalBullishProbability / (totalBullishProbability + totalBearishProbability);
                float roll = (float)_random.NextDouble(); // 0 - 1

                finalSide = roll < bullWeight ? PositionType.Long : PositionType.Short;
                des = $"横盘主导，随机方向 {(finalSide == PositionType.Long ? "多 ↗↗↗" : "空 ↘↘↘")} 概率偏向 → (多: {totalBullishProbability:P1}, 空: {totalBearishProbability:P1})";
            }
            else if (totalBullishProbability >= totalBearishProbability)
            {
                finalSide = PositionType.Long;
                des = $"多头趋势明显 ↗↗↗ (多头概率: {totalBullishProbability:P1})";
            }
            else
            {
                finalSide = PositionType.Short;
                des = $"空头趋势明显 ↘↘↘ (空头概率: {totalBearishProbability:P1})";
            }
            MarketTrendProbabilities.Clear();
            return true;
        }

        /// <summary>
        /// 是否可以加仓
        /// </summary>
        /// <returns></returns>
        private bool CanAppend(Position pos, decimal ask, decimal bid)
        {
            if (!this.OpenAppend)
                return false;
            var profit = GetProfitPercent(pos, ask, bid); //获取利润值
            if(profit <= -ToPercent(AppendLoss))
            {
                int appendTimes = GetAppendTimes(pos.PosId);//获取当前仓位的补仓次数
                if(appendTimes < this.AppendTimes) //判断是否达到最大补仓数
                {
                    return true;
                }
            }
            return false;
        }

        public void Test()
        {
            string m15KLinesJson = getKLinesJson(CandleGranularity.M15, 50);
            Logger.Instance.LogDebug(m15KLinesJson);
        }

        async void chatM15()
        {
            if (chatWaittingM15)
            {
                return;
            }
            string m15KLinesJson = getKLinesJson(CandleGranularity.M15, 100);
            if (m15KLinesJson == "[]")
            {
                Logger.Instance.LogDebug("15分钟K线等待中...");
                return;
            }
            chatWaittingM15 = true;
            string prompt = "这是DOGE/USDT近期100根15分钟K线数据，预测未来4小时的短期多、空、横盘走势概率(概率在0-1之间)，可以使用一些你熟悉的技术指标，例如布林带、能量图、均线等。响应结果要用JSON的格式，具体结果定义是  public class MarketTrendProbability\r\n  public double BullishProbability { get; set; }\r\n   public double BearishProbability { get; set; }\r\n  public double SidewaysProbability { get; set; }\r\n  } \r\nK线数据是:\r\n" + m15KLinesJson;
            string chatgptResult = await chat(prompt);
            Logger.Instance.LogDebug($"Chatgpt Result: {chatgptResult}");
            if (string.IsNullOrEmpty(chatgptResult))
            {
                Logger.Instance.LogError("gpt未返回有效的趋势概率JSON！");
                chatWaittingM15 = false;
                return;
            }
            // 从result.Text中提取包含Json的部分 提取 JSON 字符串（只匹配包含三个字段的 JSON 块）
            var match = Regex.Match(chatgptResult, @"\{[^{}]*""BullishProbability""[^{}]*""BearishProbability""[^{}]*""SidewaysProbability""[^{}]*\}");

            if (!match.Success)
            {
                Logger.Instance.LogError("未找到有效的趋势概率JSON！");
                chatWaittingM15 = false;
                return;
            }
            string jsonText = match.Value;
            MarketTrendProbability marketTrendProbability;
            try
            {
                marketTrendProbability = JsonConvert.DeserializeObject<MarketTrendProbability>(jsonText);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogDebug($"趋势概率JSON解析失败：{ex.Message}");
                return;
            }
            MarketTrendProbabilities[CandleGranularity.M15] = marketTrendProbability;
            chatWaittingM15 = false;
        }

        async Task<string> chat(string prompt)
        {
            var novitaApiService = new NovitaApiService();

            var messagesHistory = new List<Message>
            {
                new Message { Role = "user", Content = prompt },
                //new Message { Role = "assistant", Content = "I'm good, thank you! How can I assist you?" }
            };

            var inputs = new SendMessageInput
            {
                ApiKey = "156e4fc6-6c7b-48c6-985f-ef60d579c70a",
                Model = "deepseek/deepseek-r1",
                //OnProgress = (text) => Logger.Instance.LogDebug($"Progress: {text}"),
                Temperature = 0.8f,
            };

            try
            {
                var result = await novitaApiService.SendMessageFromNovitaAsync(messagesHistory, inputs);
                return result.Text;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"Chatgpt Error: {ex.Message}");
                return null;
            }
        }

        // 工具方法
        // 工具方法
        // 工具方法

        /// <summary>
        /// 根据方向选择平仓价格
        /// </summary>
        /// <param name="orderSide"></param>
        /// <param name="ask"></param>
        /// <param name="bid"></param>
        /// <returns></returns>
        private decimal GetClosePrice(PositionType orderSide, decimal ask, decimal bid)
        {
            return orderSide == PositionType.Short ? ask : bid;
        }

        /// <summary>
        /// 获取持仓盈利百分比
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="ask"></param>
        /// <param name="bid"></param>
        /// <returns></returns>
        private decimal GetProfitPercent(Position pos, decimal ask, decimal bid)
        {
            //var openPrice = pos.AvgPx;
            //var price = GetClosePrice(pos.SideType,ask,bid);
            //return (pos.SideType == PositionType.Short ? openPrice / price : price / openPrice)-1.0m;
            return (decimal)pos.UplRatio * 100;
        }

        private decimal ToPercent(float val)
        {
            return Convert.ToDecimal(val) * 0.01m;
        }
        // 10% -> 0.1
        private decimal ToPercent(decimal val)
        {
            return val * 0.01m;
        }

        bool isBanned()
        {
            var banned = DateTime.Now < bannedTime;
            if (banned) {
                var remainingTime = (bannedTime - DateTime.Now).TotalMinutes;
                Logger.Instance.LogInfo($"{InstId} is Banned. Remaining time: {remainingTime:F2} minutes.");
            }
            return banned;
        }

        // k线json
        private string getKLinesJson(CandleGranularity candleGranularity, int count) {
            List<Candle> m15 = GetCandleList(candleGranularity, count);
            var m15DataList = m15.Select(c => new
            {
                Time = c.Time.AddHours(8).ToString("yyyy-MM-dd HH:mm"),
                Open = c.Open,
                High = c.High,
                Low = c.Low,
                Close = c.Close,
                Volume = c.Volume
            }).ToList();
            string json = JsonConvert.SerializeObject(m15DataList, Formatting.Indented);
            // 可输出查看
            //Logger.Instance.LogDebug(json);
            return json;
        }

        // 辅助方法获取K线列表
        private List<Candle> GetCandleList(CandleGranularity granularity, int count)
        {
            var list = new List<Candle>();
            var provider = GetCandleProvider(granularity);
            if (provider != null)
            {
                provider.EachCandle(c =>
                {
                    list.Add(c);
                    return list.Count >= count;
                });
            }
            list.Reverse();
            return list;
        }
    }
}
