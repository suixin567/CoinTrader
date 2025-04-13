/*
 * 这是一个示例文件，
 * 放在外置的StrategiesCustomized文件夹下
 * 每次重启的时候就会动态加载StrategiesCustomized文件夹下的 .cs和.csx文件
 * 这个文件里的代码是动态编译，修改后重启生效。
*/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using CoinTrader.Common.Classes;
using CoinTrader.OKXCore.Entity;
using CoinTrader.OKXCore.Enum;
using CoinTrader.Common;
using CoinTrader.Strategies;
using CoinTrader.Common.Database;
using System.Security.Cryptography;
using System.Threading;
using CoinTrader.Strategies.Runtime;
using static CoinTrader.Forms.Control.CustomProgressBar;
using CoinTrader.Forms.Control;

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
        public float Retracement
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
        public decimal lastTrigerPrice = 0;
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
            this.Retracement = 1;
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
                    lastTrigerPrice = 0;//清零移动止盈标记价格
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
                        Side = (byte)side
                    };
                    var operationId = db.Insertable(newOperation).ExecuteReturnIdentity();
                    if (CreatePosition(side, coinAmount, Mode) > 0)//判断是否下单成功
                    {
                        // 标记为操作成功
                        db.Updateable<Operation>()
                       .SetColumns(it => it.Position == coinAmount)
                       .SetColumns(it => it.Des == des)
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
                            Side = pos.SideType == PositionType.Long ? (byte)PositionType.Short : (byte)PositionType.Long,
                            Status = 1
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
                    lastTrigerPrice = 0;//清零移动止盈标记价格
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
                    if (Retracement == 0) // 初始化移动止盈时的容忍回撤幅度
                    {
                        Retracement = updateRetracement((float)profit, StopSurplus); //刷新容忍回撤幅度
                    }
                    else if (profit >= (decimal)StopSurplus) //达到盈利目标，开始记录移动止盈的最高（最低）价格
                    {
                        switch (pos.SideType)
                        {
                            case PositionType.Long: //多头持仓的情况
                                var newHigh = lastTrigerPrice == 0 ? closePrice : Math.Max(lastTrigerPrice, closePrice);
                                if (newHigh > lastTrigerPrice)
                                {
                                    lastTrigerPrice = newHigh;// 刷新最高价
                                    Logger.Instance.LogInfo("多头刷新最高价:" + lastTrigerPrice);
                                    Retracement = updateRetracement((float)profit, StopSurplus); //刷新容忍回撤幅度
                                }
                                break;
                            case PositionType.Short: //空头持仓的情况
                                var newLow = lastTrigerPrice == 0 ? closePrice : Math.Min(lastTrigerPrice, closePrice);
                                if (newLow < lastTrigerPrice)
                                {
                                    lastTrigerPrice = newLow;// 刷新最低价
                                    Logger.Instance.LogInfo("空头刷新最底价:" + lastTrigerPrice);
                                    Retracement = updateRetracement((float)profit, StopSurplus); //刷新容忍回撤幅度
                                }
                                break;
                        }
                    }
                    // 进度条-移动止盈
                    if (pos.SideType == PositionType.Long)
                    {
                        CustomProgressBarDirection = ProgressDirection.LeftToRight;
                        CustomProgressBarMin = (float)pos.AvgPx;
                        CustomProgressBarMax = (float)lastTrigerPrice;
                        CustomProgressBarValue = (float)pos.MarkPx;

                        var longStopPrice = lastTrigerPrice * (1 - ToPercent(Retracement) / Lever);//多头回撤价
                        CustomProgressBarMarkers = new[] {
                        new CustomProgressBar.Marker { Position = (float)pos.AvgPx, TopLabel = pos.AvgPx.ToString("F5"), BottomLabel = "开仓" },
                        new CustomProgressBar.Marker { Position = (float)pos.AvgPx * (1 + StopSurplus / 100f), TopLabel = ((float)pos.AvgPx * (1 + StopSurplus / 100f)).ToString("F5"), BottomLabel = $"{StopSurplus}%" },
                        new CustomProgressBar.Marker { Position = (float)longStopPrice , TopLabel = longStopPrice.ToString("F5"), BottomLabel = "回撤" },
                        new CustomProgressBar.Marker { Position = (float)lastTrigerPrice, TopLabel = lastTrigerPrice.ToString("F5"), BottomLabel = "极限" }
                        };
                    }
                    else
                    {
                        CustomProgressBarDirection = ProgressDirection.RightToLeft;
                        CustomProgressBarMin = (float)lastTrigerPrice;
                        CustomProgressBarMax = (float)(pos.AvgPx);
                        CustomProgressBarValue = (float)pos.MarkPx;

                        var shortStopPrice = lastTrigerPrice * (1 + ToPercent(Retracement) / Lever);//空头回撤价
                        CustomProgressBarMarkers = new[] {
                        new CustomProgressBar.Marker { Position = (float)pos.AvgPx, TopLabel = pos.AvgPx.ToString("F5"), BottomLabel = "开仓" },
                        new CustomProgressBar.Marker { Position = (float)pos.AvgPx * (1 + StopSurplus / 100f), TopLabel = ((float)pos.AvgPx * (1 + StopSurplus / 100f)).ToString("F5"), BottomLabel = $"{StopSurplus}%" },
                        new CustomProgressBar.Marker { Position = (float)shortStopPrice , TopLabel = shortStopPrice.ToString("F5"), BottomLabel = "回撤" },
                        new CustomProgressBar.Marker { Position = (float)(lastTrigerPrice), TopLabel = lastTrigerPrice.ToString("F5"), BottomLabel = "极限" },
                        };
                    }
                    if (lastTrigerPrice > 0) //判断是否触发移动止盈
                    {
                        switch (pos.SideType)
                        {
                            case PositionType.Long: //多头持仓的情况                                
                                var longStopPrice = lastTrigerPrice * (1 - ToPercent(Retracement) / Lever);//多头回撤价
                                var longRetracemented = closePrice <= longStopPrice;// 多头回撤
                                debugText = $"多头-极限价:{lastTrigerPrice} 回撤价:{longStopPrice.ToString("F2")}";
                                if (longRetracemented)
                                {
                                    operationDes = $"多头回撤:{Retracement}%后止盈 盈利:{profit.ToString("F2")}%  开仓均价:{pos.AvgPx.ToString("F2")} 最高价:{lastTrigerPrice} 回撤价:{longStopPrice.ToString("F2")} 平仓价:{closePrice.ToString("F2")}";
                                    operationProfit = pos.Margin * profit / 100;
                                    Logger.Instance.LogInfo(operationDes);
                                }
                                else if (profit < (decimal)StopSurplus) // 不吃亏，既有利润不能被侵犯
                                {
                                    longRetracemented = true;
                                    operationDes = $"多头放弃更多利润的尝试，立即平仓，剩余收益:{profit.ToString("F2")}% (理论收益:{StopSurplus}%)  开仓均价:{pos.AvgPx.ToString("F2")} 最高价:{lastTrigerPrice} 平仓价:{closePrice.ToString("F2")}";
                                    operationProfit = pos.Margin * profit / 100;
                                    Logger.Instance.LogInfo(operationDes);
                                }
                                return longRetracemented;
                            case PositionType.Short: //空头持仓的情况
                                var shortStopPrice = lastTrigerPrice * (1 + ToPercent(Retracement) / Lever);//空头回撤价
                                var shortRetracemented = closePrice >= shortStopPrice;// 空头回撤
                                debugText = $"空头-极限价:{lastTrigerPrice} 回撤价:{shortStopPrice.ToString("F2")}";
                                if (shortRetracemented)
                                {
                                    operationDes = $"空头回撤:{Retracement}%后止盈 盈利:{profit.ToString("F2")}% 开仓均价:{pos.AvgPx.ToString("F2")} 最低价:{lastTrigerPrice} 回撤价:{shortStopPrice.ToString("F2")} 平仓价:{closePrice.ToString("F2")}";
                                    operationProfit = pos.Margin * profit / 100;
                                    Logger.Instance.LogInfo(operationDes);
                                }
                                else if (profit < (decimal)StopSurplus) // 不吃亏，既有利润不能被侵犯
                                {
                                    longRetracemented = true;
                                    operationDes = $"空头放弃更多利润的尝试，立即平仓，剩余收益:{profit.ToString("F2")}% (理论收益:{Retracement}%)  开仓均价:{pos.AvgPx.ToString("F2")} 最低价:{lastTrigerPrice} 平仓价:{closePrice.ToString("F2")}";
                                    operationProfit = pos.Margin * profit / 100;
                                    Logger.Instance.LogInfo(operationDes);
                                }
                                return shortRetracemented;
                        }
                    }
                }
                else
                {
                    if (profit >= (decimal)StopSurplus) //达到盈利目标，转换为移动止盈
                    {
                        var closePrice = GetClosePrice(pos.SideType, ask, bid); //根据方向得到最近的可平仓市价
                        lastTrigerPrice = closePrice; // 设置回撤极值
                        MoveProfit = true;
                        Retracement = 0;// 重置容忍回撤幅度
                        Logger.Instance.LogInfo((pos.SideType == PositionType.Long ? "多头" : "空头") + "转换为移动止盈");
                        // 记录操作到数据库
                        var db = MysqlHelper.Instance.getDB();
                        var workflow = db.Queryable<Workflow>().Where(it => it.Instrument == InstId && it.Status == 1).First();
                        if (workflow != null)
                        {
                            Operation newOperation = new Operation()
                            {
                                WorkflowId = workflow.Id,
                                Side = (byte)pos.SideType,
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
                    var closePrice = GetClosePrice(pos.SideType, ask, bid); //根据方向得到最近的可平仓市价
                    operationDes = $"止损:{profit.ToString("F2")}% 基准{StopLoss}% 仓位:{pos.Margin} 开仓均价{pos.AvgPx} 平仓价:{closePrice}";
                    operationProfit = pos.Margin * profit / 100;
                    return true;
                }
            }
            // 进度条-在常规盈利/亏损范围内
            // 实际价格涨跌幅
            float realAmplitude = StopLoss / Lever;
            CustomProgressBarMin = (float)pos.AvgPx * (1 - realAmplitude / 100);
            CustomProgressBarMax = (float)pos.AvgPx * (1 + realAmplitude / 100);
            CustomProgressBarValue = (float)pos.MarkPx;
            if (pos.SideType == PositionType.Long)
            {
                CustomProgressBarDirection = ProgressDirection.LeftToRight;
                CustomProgressBarMarkers = new[] {
                        new CustomProgressBar.Marker { Position = CustomProgressBarMin, TopLabel = CustomProgressBarMin.ToString("F5"), BottomLabel = $"止损{StopLoss}%" },
                        new CustomProgressBar.Marker { Position = CustomProgressBarMax, TopLabel = CustomProgressBarMax.ToString("F5"), BottomLabel = $"止盈{StopLoss}%" },
                        new CustomProgressBar.Marker { Position = (float)pos.AvgPx, TopLabel = pos.AvgPx.ToString("F5"), BottomLabel = "开仓" }
                        };
            }
            else
            {
                CustomProgressBarDirection = ProgressDirection.LeftToRight;
                CustomProgressBarMarkers = new[] {
                        new CustomProgressBar.Marker { Position = CustomProgressBarMin, TopLabel = CustomProgressBarMin.ToString("F5"), BottomLabel = $"止盈{StopLoss}%" },
                        new CustomProgressBar.Marker { Position = CustomProgressBarMax, TopLabel = CustomProgressBarMax.ToString("F5"), BottomLabel = $"止损{StopLoss}%" },
                        new CustomProgressBar.Marker { Position = (float)pos.AvgPx, TopLabel = pos.AvgPx.ToString("F5"), BottomLabel = "开仓" }
                        };
            }
            return false;
        }

        // 动态计算移动止盈时容忍的盈利回撤幅度
        float updateRetracement(float profit, float StopSurplus)
        {
            var retracement =((profit - StopSurplus) <= 1 ? 1 : (profit - StopSurplus)) * StopSurplus * 0.2f;
            if (retracement <= 0)
            {
                retracement = StopSurplus * 0.1f;
            }
            // 回撤幅度不能低于1%,否则再算上杠杆后，过于灵敏
            if (retracement < 2) {
                retracement = 2;
            }
            return retracement;
        }

        /// <summary>
        /// 判断是否可以开仓
        /// </summary>
        /// <param name="ask"></param>
        /// <param name="bid"></param>
        /// <returns></returns>
        private bool CanOpen(decimal ask, decimal bid, out PositionType finalSide, out string des)
        {
            // 随机决定开多还是开空
            Random _random = new Random();
            finalSide = _random.Next(2) == 0 ? PositionType.Long : PositionType.Short;
            des = finalSide == PositionType.Long ? "随机开多" : "随机开空";
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
        /// 根据盘口报价选择开仓价格
        /// </summary>
        /// <param name="orderSide"></param>
        /// <param name="ask"></param>
        /// <param name="bid"></param>
        /// <returns></returns>
        private decimal GetOpenPrice(PositionType orderSide, decimal ask, decimal bid)
        {
            return orderSide == PositionType.Short ? bid : ask;
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
    }
}
