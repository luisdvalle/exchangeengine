using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

class Solution
{
   static void Main(String[] args)
   {
      /* Enter your code here. Read input using Console.ReadLine. Print output using Console.WriteLine. Your class should be named Solution */
      var engine = new MatchingEngine();
      /*var argsList = new List<string>();

      while (true)
      {
         var arg = Console.ReadLine();

         if (arg == null)
            break;

         argsList.Add(arg);
      }*/

      foreach (var str in args)
      {
         var order = ProcessInputLine(str);

         if (order == null)
         {
            engine.PrintStatus();
         }
         else if (order.TradeStatus == TradeStatus.Active)
         {
            engine.AddOrder(order);
            engine.Trade();
         }
         else if (order.TradeStatus == TradeStatus.Modify)
         {
            engine.ModifyOrder(order);
            engine.Trade();
         }
         else if (order.TradeStatus == TradeStatus.Cancelled)
            engine.CancelOrder(order);
         else if (order.OperationType == OperationType.None)
            continue;
      }
   }

   static Order ProcessInputLine(string input)
   {
      string[] arrOrder = input.Split(new char[] { ' ' });
      Order order = new Order();

      switch (arrOrder[0])
      {
         case "BUY":
            order = new Order
            {
               OperationType = OperationType.Buy,
               Type = arrOrder[1],
               Price = Convert.ToInt32(arrOrder[2]),
               Quantity = Convert.ToInt32(arrOrder[3]),
               Id = arrOrder[4],
               LastModified = DateTime.UtcNow,
               TradeStatus = TradeStatus.Active
            };
            break;
         case "SELL":
            order = new Order
            {
               OperationType = OperationType.Sell,
               Type = arrOrder[1],
               Price = Convert.ToInt32(arrOrder[2]),
               Quantity = Convert.ToInt32(arrOrder[3]),
               Id = arrOrder[4],
               LastModified = DateTime.UtcNow,
               TradeStatus = TradeStatus.Active
            };
            break;
         case "MODIFY":
            order = new Order
            {
               Id = arrOrder[1],
               OperationType = arrOrder[2] == "BUY" ? OperationType.Buy : OperationType.Sell,
               Price = Convert.ToInt32(arrOrder[3]),
               Quantity = Convert.ToInt32(arrOrder[4]),
               LastModified = DateTime.UtcNow,
               TradeStatus = TradeStatus.Modify
            };
            break;
         case "CANCEL":
            order = new Order
            {
               Id = arrOrder[1],
               TradeStatus = TradeStatus.Cancelled
            };
            break;
         case "PRINT":
            order = null;
            break;
         default:
            order = new Order
            {
                OperationType = OperationType.None
            };
            break;
      }

      return order;
   }
}

public class MatchingEngine
{
   private ConcurrentDictionary<string, Order> SellOrders = new ConcurrentDictionary<string, Order>();
   private ConcurrentDictionary<string, Order> BuyOrders = new ConcurrentDictionary<string, Order>();

   public bool AddOrder(Order order)
   {
      if (order.Price <= 0 || order.Quantity <= 0 || string.IsNullOrEmpty(order.Id))
         return false;

      order.LastModified = DateTime.UtcNow;

      if (order.OperationType == OperationType.Buy)
         return BuyOrders.TryAdd(order.Id, order);
      else if (order.OperationType == OperationType.Sell)
         return SellOrders.TryAdd(order.Id, order);

      return false;
   }

   public bool CancelOrder(Order order)
   {
      if (string.IsNullOrEmpty(order.Id))
         return false;

      order.LastModified = DateTime.UtcNow;

      if (SellOrders.TryGetValue(order.Id, out var sellOrder))
      {
         sellOrder.TradeStatus = TradeStatus.Cancelled;
         sellOrder.LastModified = DateTime.UtcNow;
         return true;
      }

      if (BuyOrders.TryGetValue(order.Id, out var buyOrder))
      {
         buyOrder.TradeStatus = TradeStatus.Cancelled;
         buyOrder.LastModified = DateTime.UtcNow;
         return true;
      }

      return false;
   }

   public bool ModifyOrder(Order order)
   {
      if (string.IsNullOrEmpty(order.Id))
         return false;

      if (order.TradeStatus == TradeStatus.Modify)
      {
         if (SellOrders.TryGetValue(order.Id, out var sellOrder))
         {
            if (sellOrder.Type != "GFD")
               return false;

            if (order.Price <= 0 || order.Quantity <= 0)
            {
                sellOrder.TradeStatus = TradeStatus.Cancelled;
                sellOrder.LastModified = DateTime.UtcNow;
                return true;
            }

            if (order.OperationType == OperationType.Sell)
            {
               sellOrder.Price = order.Price;
               sellOrder.Quantity = order.Quantity;
               sellOrder.LastModified = DateTime.UtcNow;
            }
            else
            {
               // Deactivate orignal order
               SellOrders.TryGetValue(order.Id, out var modifiedOrder);
               modifiedOrder.LastModified = DateTime.UtcNow;
               modifiedOrder.TradeStatus = TradeStatus.NotActiveModified;

               // Add order in the other trading dictionary
               order.TradeStatus = TradeStatus.Active;
               order.Type = modifiedOrder.Type;
               order.LastModified = DateTime.UtcNow;
               BuyOrders.TryAdd(order.Id, order);
            }

            return true;
         }
         else if (BuyOrders.TryGetValue(order.Id, out var buyorder))
         {
            if (buyorder.Type != "GFD")
               return false;

            if (order.Price <= 0 || order.Quantity <= 0)
            {
                buyorder.TradeStatus = TradeStatus.Cancelled;
                buyorder.LastModified = DateTime.UtcNow;
                return true;
            }

            if (order.OperationType == OperationType.Buy)
            {
               buyorder.Price = order.Price;
               buyorder.Quantity = order.Quantity;
               buyorder.LastModified = DateTime.UtcNow;
            }
            else
            {
               // Deactivate original order
               BuyOrders.TryGetValue(order.Id, out var modifiedOrder);
               modifiedOrder.LastModified = DateTime.UtcNow;
               modifiedOrder.TradeStatus = TradeStatus.NotActiveModified;

               // Add order in the other trading dictionary
               order.TradeStatus = TradeStatus.Active;
               order.Type = modifiedOrder.Type;
               order.LastModified = DateTime.UtcNow;
               SellOrders.TryAdd(order.Id, order);
            }

            return true;
         }

         return false;
      }

      return false;
   }

   public void Trade()
   {
      foreach (var buyOrder in BuyOrders.OrderByDescending(o => o.Value.Price).ThenBy(o => o.Value.LastModified))
      // foreach (var buyOrder in BuyOrders.OrderBy(o => o.Value.LastModified))
      {
         if (buyOrder.Value.TradeStatus == TradeStatus.Active)
         {
            PerformMatchingForBuyingOrder(buyOrder.Value);

            if (buyOrder.Value.Type == "IOC")
            {
               if (buyOrder.Value.TradeStatus != TradeStatus.Fulfilled)
               {
                  BuyOrders.TryGetValue(buyOrder.Value.Id, out var iocOrder);
                  iocOrder.TradeStatus = TradeStatus.Cancelled;
                  iocOrder.LastModified = DateTime.UtcNow;
               }
            }
         }

         continue;
      }
   }

   public void PerformMatchingForBuyingOrder(Order orderToMatch)
   {
      // foreach (var sellOrder in SellOrders.OrderBy(o => o.Value.LastModified))
      foreach (var sellOrder in SellOrders.OrderByDescending(o => o.Value.Price).ThenBy(o => o.Value.LastModified))
      {
         if (sellOrder.Value.TradeStatus == TradeStatus.Active &&
             sellOrder.Value.Price <= orderToMatch.Price)
         {
            // Calculate the quantity traded
            int qtyTraded = orderToMatch.Quantity <= sellOrder.Value.Quantity
               ? orderToMatch.Quantity : sellOrder.Value.Quantity;

            BuyOrders.TryGetValue(orderToMatch.Id, out var buyOrder);
            SellOrders.TryGetValue(sellOrder.Value.Id, out var sOrder);

            var firstOrder = (buyOrder.LastModified < sOrder.LastModified) ? buyOrder : sOrder;
            var lastOrder = (buyOrder.LastModified > sOrder.LastModified) ? buyOrder : sOrder;
            PrintTradeOperation(firstOrder.Id, firstOrder.Price, qtyTraded, lastOrder.Id, lastOrder.Price);

            if (qtyTraded == sOrder.Quantity)
            {
               sOrder.TradeStatus = TradeStatus.Fulfilled;
               sOrder.LastModified = DateTime.UtcNow;
            }
            else
            {
               sOrder.Quantity -= qtyTraded;
            }

            if (qtyTraded == buyOrder.Quantity)
            {
               buyOrder.TradeStatus = TradeStatus.Fulfilled;
               buyOrder.LastModified = DateTime.UtcNow;
            }
            else
            {
               buyOrder.Quantity -= qtyTraded;
            }
         }

         continue;
      }
   }

   public void PrintStatus()
   {
      var allPricesForSell = SellOrders
          .Where(o => o.Value.TradeStatus == TradeStatus.Active)
          .OrderByDescending(o => o.Value.Price)
          .Select(o => o.Value.Price)
          .Distinct().ToList();

      var allPricesForBuy = BuyOrders
          .Where(o => o.Value.TradeStatus == TradeStatus.Active)
          .OrderByDescending(o => o.Value.Price)
          .Select(o => o.Value.Price)
          .Distinct().ToList();

      Console.Out.WriteLine("SELL:");
      foreach (var price in allPricesForSell)
      {
         var qty = SellOrders
             .Where(o => o.Value.Price == price && o.Value.TradeStatus == TradeStatus.Active)
             .Sum(o => o.Value.Quantity);

         Console.Out.WriteLine($"{price} {qty}");
      }
      Console.Out.WriteLine("BUY:");
      foreach (var price in allPricesForBuy)
      {
         var qty = BuyOrders
             .Where(o => o.Value.Price == price && o.Value.TradeStatus == TradeStatus.Active)
             .Sum(o => o.Value.Quantity);

         Console.Out.WriteLine($"{price} {qty}");
      }
   }

   private void PrintTradeOperation(string idBuy, int priceBuy, int qtyTraded, string idSell, int priceSell)
   {
      Console.Out.WriteLine($"TRADE {idBuy} {priceBuy} {qtyTraded} {idSell} {priceSell} {qtyTraded}");
   }
}

public class Order
{
   public OperationType OperationType { get; set; }
   public string Id { get; set; }
   public string Type { get; set; }
   public int Price { get; set; }
   public int Quantity { get; set; }
   public DateTime LastModified { get; set; }
   public TradeStatus TradeStatus { get; set; }
}

public enum OperationType
{
   None,
   Buy,
   Sell,
}

public enum TradeStatus
{
   Active,
   NotActiveModified,
   Fulfilled,
   Cancelled,
   Modify
}