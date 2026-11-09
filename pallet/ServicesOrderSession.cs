using System;
using System.Collections.Generic;
using System.Linq;
using PalletApp.Models;

namespace PalletApp.Services
{
    public static class OrderSession
    {
        public class Order
        {
            public string OrderNumber { get; set; }
            public DateTime OrderDate { get; set; }

            /// Ожидаемое время готовности. НЕ сохраняется в БД,
            /// рассчитывается в момент оформления. Показывается клиенту.
            public DateTime? ExpectedReadyAt { get; set; }

            public int StatusID { get; set; } = 1;
            public string Status { get; set; } = "Оформлен";

            public string Recipient { get; set; }
            public string Phone { get; set; }
            public string DeliveryMethod { get; set; }
            public string Address { get; set; }
            public string PaymentMethod { get; set; }
            public decimal Subtotal { get; set; }
            public decimal DeliveryCost { get; set; }
            public decimal Total { get; set; }
            public string Comment { get; set; }
            public List<CartItem> Items { get; set; } = new List<CartItem>();

            /// Статус для отображения клиенту.
            public string ClientStatus
            {
                get
                {
                    if (StatusID == 3) return "Готов к получению";
                    return Status;
                }
            }

            /// Заказ можно забрать.
            public bool IsReady => StatusID == 4 || StatusID == 3;

            /// Текст готовности для отображения в списке заказов.
            public string ReadyInfoText
            {
                get
                {
                    // Отменён
                    if (StatusID == 6) return "Заказ отменён";

                    // Готов или отгружен
                    if (StatusID == 3 || StatusID == 4)
                        return "✅ Готов к получению";

                    // Ожидается
                    if (ExpectedReadyAt.HasValue)
                    {
                        var now = DateTime.Now;
                        var readyAt = ExpectedReadyAt.Value;

                        if (now >= readyAt)
                            return $"Ожидает подтверждения готовности (был готов к {readyAt:dd.MM HH:mm})";

                        var span = readyAt - now;
                        string осталось;
                        if (span.TotalHours < 1)
                            осталось = $"{span.Minutes} мин";
                        else if (span.TotalHours < 24)
                            осталось = $"{(int)span.TotalHours} ч {span.Minutes} мин";
                        else
                            осталось = $"{(int)span.TotalDays} дн";

                        return $"⏱ Готов к {readyAt:dd.MM HH:mm} (осталось {осталось})";
                    }

                    return "⏱ Время готовности рассчитывается…";
                }
            }
        }

        private static readonly List<Order> _orders = new List<Order>();

        public static IReadOnlyList<Order> Orders => _orders;
        public static bool HasOrders => _orders.Count > 0;
        public static Order LastOrder => _orders.LastOrDefault();

        public static Order Add(
            string orderNumber, DateTime orderDate,
            string recipient, string phone,
            string deliveryMethod, string address, string paymentMethod,
            decimal subtotal, decimal deliveryCost, decimal total,
            string comment, List<CartItem> items)
        {
            var order = new Order
            {
                OrderNumber = orderNumber,
                OrderDate = orderDate,
                StatusID = 1,
                Status = "Оформлен",
                Recipient = recipient,
                Phone = phone,
                DeliveryMethod = deliveryMethod,
                Address = address,
                PaymentMethod = paymentMethod,
                Subtotal = subtotal,
                DeliveryCost = deliveryCost,
                Total = total,
                Comment = comment,
                Items = items.Select(i => new CartItem
                {
                    Product = i.Product,
                    Quantity = i.Quantity
                }).ToList()
            };

            _orders.Add(order);
            return order;
        }

        public static void SetOrders(List<Order> orders)
        {
            _orders.Clear();
            if (orders != null)
                _orders.AddRange(orders);
        }

        public static void Clear() => _orders.Clear();
    }
}