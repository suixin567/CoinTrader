﻿using CoinTrader.Common;
using CoinTrader.Common.Classes;
using CoinTrader.Common.Interface;
using CoinTrader.OKXCore;
using CoinTrader.OKXCore.Entity;
using CoinTrader.OKXCore.Enum;
using CoinTrader.OKXCore.Manager;
using CoinTrader.OKXCore.VO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CoinTrader.Strategies.Runtime
{
    /// <summary>
    /// 
    /// </summary>
    public class SwapEmulatorCandleProvider : ICandleProvider
    {
        public SwapEmulatorCandleProvider(CandleGranularity candleGranularity)
        {
            this.granularity = candleGranularity;
        }
        public bool Loaded => true;

        public event EventHandler CandleLoaded;

        List<Candle> candles = new List<Candle>();

        private CandleGranularity granularity = CandleGranularity.Y1;

        public void EachCandle(Func<Candle, bool> callback)
        {
            lock (this.candles)
            {
                foreach (var candle in candles)
                {
                    if (callback(candle))
                        break;
                }
            }
        }

        public void UpdateLastPrice(decimal price, DateTime time)
        {
            lock (this.candles)
            {
                var last = this.candles.Count > 0 ? this.candles[0] : null;
                uint interval = (uint)this.granularity;
                if (last != null)
                {
                    TimeSpan ts = time - last.Time;

                    if (ts.TotalSeconds > interval)
                    {
                        Candle candle = VOPool<Candle>.GetPool().Get();
                        candle.Open = candle.High = candle.Low = candle.Close = price;

                        int y, M, d, h, m;

                        y = time.Year;
                        M = time.Month;
                        d = time.Day;
                        h = time.Hour;
                        m = time.Minute;

                        switch (this.granularity)
                        {
                            case CandleGranularity.M1:
                                break;
                            case CandleGranularity.M3:
                                m -= m % 3;
                                break;
                            case CandleGranularity.M5:
                                m -= m % 5;
                                break;
                            case CandleGranularity.M15:
                                m -= m % 15;
                                break;
                            case CandleGranularity.M30:
                                m -= m % 30;
                                break;
                            case CandleGranularity.H1:
                                m = 0;
                                break;
                            case CandleGranularity.H4:
                                m = 0;
                                h -= h % 4;
                                break;
                            case CandleGranularity.H6:
                                m = 0;
                                h -= h % 6;
                                break;
                            case CandleGranularity.H12:
                                m = 0;
                                h -= h % 12;
                                break;
                            case CandleGranularity.D1:
                                h = 0;
                                m = 0;
                                break;
                            case CandleGranularity.Week1:
                                DateTime newTime = time;
                                newTime = newTime.AddDays(-(int)newTime.DayOfWeek);
                                y = newTime.Year;
                                M = newTime.Month;
                                d = newTime.Day;
                                h = 0;
                                m = 0;
                                break;
                            case CandleGranularity.Month1:
                                d = 1; h = 0; m = 0;
                                break;
                            case CandleGranularity.Y1:
                                M = 0; d = 1; h = 0; m = 0;
                                break;
                        }

                        candle.Time = new DateTime(y, M, d, h, m, 0);
                        candle.Confirm = false;
                        candle.Volume = 0;
                        this.candles.Insert(0, candle);
                    }
                    else
                    {
                        last.Close = price;
                        last.High = Math.Max(price, last.High);
                        last.Low = Math.Min(price, last.Low);
                    }
                }
            }
        }
    }


    public class SwapStrategyEmulatorRuntime : ITradeStrategyRuntime
    {
        #region 仓位管理
        private Dictionary<long, Position> positions = new Dictionary<long, Position>();
        private Dictionary<CandleGranularity, SwapEmulatorCandleProvider> candles = new Dictionary<CandleGranularity, SwapEmulatorCandleProvider>();
        #endregion

        #region 账户和合约信息
        private BalanceVO baseBalance;
        private BalanceVO quoteBalance;
        private string instId;
        private InstrumentSwap instrument;
        private long orderIdSeed = 10000;
        private decimal ask;
        private decimal bid;
        private DateTime now;
        private uint maxLeverage = 125;
        private uint currentLeverage = 1;
        private SwapMarginMode mgnMode = SwapMarginMode.Cross;
        private DateTime lastFundingTime = DateTime.MinValue;
        private double fundingRate = 0.0002;
        #endregion

        #region 订单管理
        private List<TradeOrder> orders = new List<TradeOrder>();
        private List<TradeOrder> historyOrders = new List<TradeOrder>();
        private List<long> idForRemove = new List<long>();
        private List<TradeOrder> orderForAdd = new List<TradeOrder>();
        private bool traversingOrders = false;
        #endregion

        public event Action<decimal, decimal> OnTick;
        public decimal Fee { get; set; } = 0.0005m;

        public string InstId => instId;

        public BalanceVO QuoteBalance
        {
            get
            {
                return quoteBalance;
            }
            set
            {
                quoteBalance = value;
            }
        }

        public decimal MinSize => instrument.MinSize;
        public decimal TickSize => instrument.TickSize;
        public bool IsEmulator => true;
        public bool Effective => true;

        #region 核心接口实现
        public uint GetMaxLever() => maxLeverage;
        public double GetFundingRate() => fundingRate;

        public void SetLever(PositionType side, SwapMarginMode mode, uint lever)
        {
            if (lever < 1 || lever > GetMaxLever())
                return;

            currentLeverage = Math.Min(lever, maxLeverage);
            mgnMode = mode;
            // 正式运行时是这样写的:
            //AccountManager.SetLever(InstId, side, mode, lever);
        }

        public List<Position> GetPositions() => positions.Values.ToList();
        public Position GetPosition(long id) => positions.TryGetValue(id, out var pos) ? pos : null;

        public bool ClosePosition(long id, decimal? size = null, decimal? amount = null)
        {
            if (positions.TryGetValue(id, out var position))
            {
                decimal closeSize = size ?? position.Pos;
                if (amount.HasValue) closeSize = amount.Value / position.AvgPx;

                closeSize = Math.Min(closeSize, position.Pos);
                ClosePosition(position, closeSize);
                return true;
            }
            return false;
        }

        public long CreatePosition(PositionType side, decimal amount, SwapMarginMode mode)
        {
            return CreatePositionInternal(side, amount, mode);
        }

        public void EachPosition(Action<Position> callback)
        {
            foreach (var pos in positions.Values.ToArray())
            {
                callback(pos);
            }
        }
        #endregion

        #region 初始化方法
        public bool Init(string instId)
        {
            instrument = InstrumentManager.GetInstrument(instId) as InstrumentSwap;
            this.instId = instId;
            return instrument != null;
        }
        #endregion

        #region 价格更新和资金费率
        public void UpdatePrices(decimal ask, decimal bid, DateTime time)
        {
            this.ask = ask;
            this.bid = bid;
            this.now = time;

            UpdateFunding(time);
            UpdatePositionPrices();
            CheckOrders();
            OnTick?.Invoke(ask, bid);

            foreach (var cp in candles.Values)
            {
                cp.UpdateLastPrice((ask + bid) / 2, time);
            }
        }

        private void UpdatePositionPrices()
        {
            foreach (var pos in positions.Values)
            {
                // 获取最新价格（空头用卖一价，多头用买一价）
                decimal lastPrice = pos.SideType == PositionType.Short ? ask : bid;
                pos.Last = lastPrice;

                // 更新标记价格（此处假设用最新价代替）
                pos.MarkPx = lastPrice;

                // 计算未实现盈亏
                decimal priceDelta = lastPrice - pos.AvgPx;
                pos.Upl = pos.Pos * priceDelta * (pos.SideType == PositionType.Long ? 1 : -1);

                // 计算未实现收益率（需要避免除以零）
                pos.UplRatio = pos.Margin != 0
                    ? (double)(pos.Upl / pos.Margin)
                    : 0;

                // 获取维持保证金率（示例值，需根据实际数据源调整）
                decimal maintenanceRate = 0.005m; // 0.5% 维持保证金率

                // 计算维持保证金
                decimal notionalValue = pos.Pos * pos.AvgPx;
                pos.MMR = notionalValue * maintenanceRate / (decimal)pos.Lever;

                // 计算保证金率（需要避免除以零）
                pos.MgnRatio = pos.MMR != 0
                    ? (double)(pos.Margin / pos.MMR)
                    : double.MaxValue;

                // 计算预估强平价
                if (pos.SideType == PositionType.Long)
                {
                    decimal denominator = 1 - maintenanceRate - (1 / (decimal)pos.Lever);
                    pos.LiqPx = denominator != 0
                        ? pos.AvgPx * (1 - 1 / (decimal)pos.Lever / (1 - maintenanceRate))
                        : 0;
                }
                else
                {
                    decimal denominator = 1 + maintenanceRate + (1 / (decimal)pos.Lever);
                    pos.LiqPx = denominator != 0
                        ? pos.AvgPx * (1 + 1 / (decimal)pos.Lever / (1 - maintenanceRate))
                        : 0;
                }

                // 更新最后更新时间
                pos.UTime = now;

                // 可平仓数量假设等于总持仓量（根据业务逻辑可能需要调整）
                pos.AvailPos = pos.Pos;
            }
        }

        private void UpdateFunding(DateTime time)
        {
            if ((time - lastFundingTime).TotalHours >= 8)
            {
                ApplyFunding();
                lastFundingTime = time;
            }
        }

        private void ApplyFunding()
        {
            foreach (var position in positions.Values)
            {
                decimal funding = position.Pos * position.AvgPx * (decimal)fundingRate;
                quoteBalance.Avalible += position.SideType == PositionType.Short ? funding : -funding;
            }
        }
        #endregion

        #region 仓位操作核心逻辑
        private long CreatePositionInternal(PositionType side, decimal amount, SwapMarginMode mode)
        {
            decimal price = side == PositionType.Long ? bid : ask;
            decimal margin = (amount * price) / currentLeverage;

            if (quoteBalance.Avalible < margin)
                throw new Exception("Insufficient margin");

            var position = new Position
            {
                InstId = InstId,
                PosId = DateTime.UtcNow.Ticks,
                PosSide = side.ToString().ToLower(),// 可能有问题
                Pos = amount,
                AvgPx = price,
                Lever = currentLeverage,
                MgnMode = mode == SwapMarginMode.Cross ? MarginMode.Cross : MarginMode.Isolated,
                Margin = margin,
                CTime = now,
                Last = price
            };

            quoteBalance.Avalible -= margin;
            quoteBalance.Frozen += margin;
            positions.Add(position.PosId, position);// 有可能出错

            return position.PosId;// 有可能出错
        }

        private void ClosePosition(Position position, decimal closeSize)
        {
            decimal closeValue = closeSize * position.AvgPx;
            decimal fee = closeValue * Fee;
            decimal pnl = (position.Last - position.AvgPx) * closeSize *
                        (position.SideType == PositionType.Long ? 1 : -1);

            // 释放保证金
            decimal marginRatio = closeSize / position.Pos;
            decimal returnMargin = position.Margin * marginRatio;

            quoteBalance.Frozen -= returnMargin;
            quoteBalance.Avalible += returnMargin + pnl - fee;

            // 更新仓位
            position.Pos -= closeSize;
            position.Margin -= returnMargin;

            if (position.Pos <= 0)
                positions.Remove(position.PosId);// 有可能出错
        }
        #endregion

        #region 订单系统
        public long SendOrder(OrderSide side, decimal amount, decimal price, bool postOnly)
        {
            if (amount < MinSize) return 0;

            var order = CreateOrder(side, amount, price, postOnly);
            if (order == null) return 0;

            if (traversingOrders)
                orderForAdd.Add(order);
            else
                orders.Add(order);

            return order.PublicId;
        }

        private TradeOrder CreateOrder(OrderSide side, decimal amount, decimal price, bool postOnly)
        {
            if (postOnly && ((side == OrderSide.Sell && price <= bid) || (side == OrderSide.Buy && price >= ask)))
                return null;

            orderIdSeed++;
            return new TradeOrder
            {
                PublicId = orderIdSeed,
                Side = side,
                Amount = amount,
                Price = price,
                AvailableAmount = amount,
                CreatedDate = now,
                InstId = instId
            };
        }

        public void CancelOrder(long id) => CancelOrders(new[] { id });

        public void CancelOrders(IEnumerable<long> ids)
        {
            if (traversingOrders)
            {
                idForRemove.AddRange(ids);
                return;
            }
            foreach (var id in ids)
            {
                var order = orders.FirstOrDefault(o => o.PublicId == id);
                if (order != null)
                {
                    orders.Remove(order);
                    ReleaseOrderMargin(order);
                }
            }
        }

        private void ReleaseOrderMargin(TradeOrder order)
        {
            decimal margin = (order.AvailableAmount * order.Price) / currentLeverage;
            quoteBalance.Frozen -= margin;
            quoteBalance.Avalible += margin;
        }
        #endregion

        #region 订单匹配逻辑
        private void CheckOrders()
        {
            for (int i = orders.Count - 1; i >= 0; i--)
            {
                var order = orders[i];
                bool filled = false;

                if (order.Side == OrderSide.Buy && ask <= order.Price)
                {
                    filled = TryFillOrder(order, ask);
                }
                else if (order.Side == OrderSide.Sell && bid >= order.Price)
                {
                    filled = TryFillOrder(order, bid);
                }

                if (filled)
                {
                    if (order.AvailableAmount <= 0)
                    {
                        orders.RemoveAt(i);
                        historyOrders.Add(order);
                    }
                }
            }
            ProcessPendingOrders();
        }

        private bool TryFillOrder(TradeOrder order, decimal fillPrice)
        {
            try
            {
                // 计算实际可成交数量（考虑最小交易单位）
                decimal amount = Math.Max(order.AvailableAmount, MinSize);
                amount = NormalizeSize(amount);

                // 计算需要的保证金
                decimal requiredMargin = (amount * fillPrice) / currentLeverage;

                if (quoteBalance.Avalible < requiredMargin)
                    return false;

                // 创建仓位
                var positionId = CreatePositionInternal(order.Side == OrderSide.Buy ? PositionType.Long : PositionType.Short, amount, mgnMode);
                var position = GetPosition(positionId);
                position.AvgPx = fillPrice; // 更新实际成交价格

                // 冻结保证金
                quoteBalance.Avalible -= requiredMargin;
                quoteBalance.Frozen += requiredMargin;

                // 更新订单状态
                order.AvailableAmount -= amount;
                order.FilledSize += amount;
                order.PriceAvg = (order.PriceAvg * (order.FilledSize - amount) + fillPrice * amount) / order.FilledSize;
                order.Fee += amount * fillPrice * Fee;
                order.UpdateTime = now;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Order fill failed: {ex.Message}");
                return false;
            }
        }

        private decimal NormalizeSize(decimal amount)
        {
            // 根据合约规则标准化交易数量
            decimal size = Math.Floor(amount / instrument.MinSize) * instrument.MinSize;
            return Math.Max(size, instrument.MinSize);
        }

        private void ProcessPendingOrders()
        {
            // 处理遍历期间添加/删除的订单
            foreach (var id in idForRemove)
            {
                var order = orders.FirstOrDefault(o => o.PublicId == id);
                if (order != null)
                {
                    orders.Remove(order);
                    ReleaseOrderMargin(order);
                }
            }
            orders.AddRange(orderForAdd);
            idForRemove.Clear();
            orderForAdd.Clear();
        }
        #endregion

        #region 辅助方法
        public void EachBuyOrder(Action<OrderBase> callback) => EachOrders(OrderSide.Buy, callback);
        public void EachSellOrder(Action<OrderBase> callback) => EachOrders(OrderSide.Sell, callback);

        private void EachOrders(OrderSide side, Action<OrderBase> callback)
        {
            traversingOrders = true;
            try
            {
                foreach (var order in orders.Where(o => o.Side == side).ToArray())
                {
                    callback?.Invoke(order);
                }
            }
            finally
            {
                traversingOrders = false;
                ProcessPendingOrders();
            }
        }

        public OrderBase GetOrder(long id)
        {
            return orders.Concat(historyOrders)
                         .FirstOrDefault(o => o.PublicId == id);
        }
        #endregion

        #region 蜡烛图管理
        public ICandleProvider GetCandleProvider(CandleGranularity granularity)
        {
            if (!candles.TryGetValue(granularity, out var provider))
            {
                provider = new SwapEmulatorCandleProvider(granularity);
                candles[granularity] = provider;
            }
            return provider;
        }

        public void EachCandle(CandleGranularity granularity, Func<Candle, bool> callback)
        {
            if (candles.TryGetValue(granularity, out var provider))
            {
                provider.EachCandle(callback);
            }
        }
        #endregion

        #region 清算逻辑
        //public void CheckLiquidation()
        //{
        //    foreach (var position in positions.Values.ToArray())
        //    {
        //        decimal marginRatio = CalculateMarginRatio(position);

        //        if (marginRatio <= instrument.MaintenanceRate)
        //        {
        //            // 触发强制平仓
        //            ClosePosition(position.Id);
        //            Debug.WriteLine($"Position {position.Id} liquidated");
        //        }
        //    }
        //}

        //private decimal CalculateMarginRatio(Position position)
        //{
        //    decimal markPrice = position.SideType == PositionType.Short ? ask : bid;
        //    decimal unrealizedPnl = (markPrice - position.AvgPx) * position.Pos *
        //                           (position.SideType == PositionType.Long ? 1 : -1);
        //    decimal maintenanceMargin = position.Margin * instrument.CtMult;

        //    return (position.Margin + unrealizedPnl) / position.Margin;
        //}
        #endregion

        public string QuoteCurrency => throw new NotImplementedException();

        public string BaseCurrency => throw new NotImplementedException();

        public uint Lever { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public BalanceVO BaseBalance
        {
            get
            {
                return baseBalance;
            }
            set
            {
                baseBalance = value;
            }
        }

        public void LoadCandle(CandleGranularity granularity)
        {
            throw new NotImplementedException();
        }

        public void UnloadCandle(CandleGranularity granularity)
        {
            throw new NotImplementedException();
        }

        public bool ModifyOrder(long id, decimal amount, decimal newPrice, bool cancelOrderWhenFailed)
        {
            throw new NotImplementedException();
        }

        public void CancelOrderBySide(OrderSide side, bool async)
        {
            throw new NotImplementedException();
        }

        public void Sell(decimal amount)
        {
            throw new NotImplementedException();
        }

        public void Buy(decimal amount)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public List<TradeOrder> GetHistoryList()
        {
            return historyOrders;
        }
    }
}