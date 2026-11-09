using PalletApp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace PalletApp.Services
{
    public static class DbService
    {
        public const string ConnectionString =
            @"Data Source=Liza-fox\SQLEXPRESS;Initial Catalog=pallett;Integrated Security=True";

        public static SqlConnection GetConnection() => new SqlConnection(ConnectionString);

        //  ПОЛЬЗОВАТЕЛИ 

        public static User FindUserByLogin(string login)
        {
            const string sql = @"
                SELECT TOP 1 ClientID, FullName, Role, Login
                FROM Client
                WHERE LOWER(Login) = @login";

            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@login", login.Trim().ToLower());
                conn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;

                    return new User
                    {
                        UserID = reader.GetInt32(0),
                        FullName = reader.GetString(1),
                        Role = reader.GetString(2),
                        Login = reader.IsDBNull(3) ? null : reader.GetString(3),
                        IsAuthenticated = true
                    };
                }
            }
        }

        public static ClientInfo GetClientInfo(int clientId)
        {
            const string sql = @"
                SELECT 
                    ISNULL(FullName, N'') AS FullName,
                    ISNULL(Phone, N'') AS Phone,
                    ISNULL(LegalAddress, N'') AS Address
                FROM Client
                WHERE ClientID = @id";

            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", clientId);
                conn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;

                    return new ClientInfo
                    {
                        FullName = reader.GetString(0),
                        Phone = reader.GetString(1),
                        Address = reader.GetString(2)
                    };
                }
            }
        }

        // ТОВАРЫ 

        public static List<Product> GetProducts()
        {
            const string sql = @"
                SELECT 
                    p.ProductID,
                    p.SKU,
                    p.Name,
                    pt.Name  AS PalletTypeName,
                    m.Name   AS MaterialName,
                    c.Name   AS ConditionName,
                    p.Price,
                    ISNULL(p.Description, '') AS Description,
                    p.IsActive,
                    ISNULL((SELECT SUM(s.Quantity - s.Reserved) 
                            FROM Stock s WHERE s.ProductID = p.ProductID), 0) AS Available
                FROM Product p
                INNER JOIN PalletType pt ON p.PalletTypeID = pt.PalletTypeID
                INNER JOIN Material    m  ON p.MaterialID   = m.MaterialID
                INNER JOIN Cond        c  ON p.ConditionID  = c.ConditionID
                WHERE p.IsActive = 1
                ORDER BY p.ProductID";

            var result = new List<Product>();

            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int productId = reader.GetInt32(0);
                        string sku = reader.GetString(1);
                        string name = reader.GetString(2);
                        string palletType = reader.GetString(3);
                        string material = reader.GetString(4);
                        string condition = reader.GetString(5);
                        decimal price = reader.GetDecimal(6);
                        int available = Convert.ToInt32(reader.GetValue(9));

                        result.Add(new Product
                        {
                            ProductID = productId,
                            SKU = sku,
                            Name = name,
                            Category = palletType,
                            Manufacturer = "Россия",
                            Composition = material,
                            Quantity = available,
                            Price = price,
                            ImagePath = ResolveImagePath(sku),
                            IsAvailable = available > 0
                        });
                    }
                }
            }

            return result;
        }

        public static int GetAvailableQuantity(int productId)
        {
            const string sql = @"
                SELECT ISNULL(SUM(Quantity - Reserved), 0)
                FROM Stock WHERE ProductID = @id";

            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", productId);
                conn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private static string ResolveImagePath(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku)) return null;
            string s = sku.ToUpperInvariant();

            if (s.Contains("EUR")) return "/1.png";
            if (s.Contains("FIN")) return "/2.png";
            if (s.Contains("PLAST")) return "/3.png";

            return null;
        }

        // СПОСОБЫ ДОСТАВКИ И ОПЛАТЫ

        public static List<DeliveryMethod> GetDeliveryMethods()
        {
            const string sql = @"
                SELECT DeliveryMethodID, Name, BaseCost
                FROM DeliveryMethod ORDER BY DeliveryMethodID";

            var result = new List<DeliveryMethod>();

            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string name = reader.GetString(1);
                        decimal capacity = 0m;

                        if (name.Contains("газель")) capacity = 40m;
                        else if (name.Contains("фур")) capacity = 80m;

                        result.Add(new DeliveryMethod
                        {
                            DeliveryMethodID = reader.GetInt32(0),
                            Name = name,
                            BaseCost = reader.GetDecimal(2),
                            Capacity = capacity
                        });
                    }
                }
            }

            return result;
        }

        public static List<PaymentMethod> GetPaymentMethods()
        {
            const string sql = @"SELECT PaymentMethodID, Name FROM PaymentMethod ORDER BY PaymentMethodID";
            var result = new List<PaymentMethod>();

            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new PaymentMethod
                        {
                            PaymentMethodID = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }

            return result;
        }

        // СОЗДАНИЕ ЗАКАЗА 
        public static string CreateOrder(
            int clientId,
            int deliveryMethodId,
            int paymentMethodId,
            string deliveryAddress,
            decimal subtotal,
            decimal deliveryCost,
            decimal total,
            string comment,
            List<CartItem> items)
        {
            string orderNumber = $"ORD-{DateTime.Now:yyyyMMdd-HHmmss}";

            using (var conn = GetConnection())
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // Проверка остатков
                        foreach (var item in items)
                        {
                            int available = GetAvailableInTransaction(item.Product.ProductID, conn, transaction);
                            if (available < item.Quantity)
                            {
                                throw new Exception(
                                    $"Товар «{item.Product.Name}»: доступно {available} шт., запрошено {item.Quantity}.");
                            }
                        }

                        // Вставляем заказ
                        const string insertOrderSql = @"
                            INSERT INTO [Order]
                                (OrderNumber, ClientID, StatusID, DeliveryMethodID, PaymentMethodID,
                                 DeliveryAddress, TotalAmount, DeliveryCost, CreatedAt)
                            VALUES
                                (@OrderNumber, @ClientID, 1, @DeliveryMethodID, @PaymentMethodID,
                                 @DeliveryAddress, @TotalAmount, @DeliveryCost, GETDATE());
                            SELECT SCOPE_IDENTITY();";

                        int orderId;

                        using (var cmd = new SqlCommand(insertOrderSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@OrderNumber", orderNumber);
                            cmd.Parameters.AddWithValue("@ClientID", clientId);
                            cmd.Parameters.AddWithValue("@DeliveryMethodID", deliveryMethodId);
                            cmd.Parameters.AddWithValue("@PaymentMethodID", paymentMethodId);
                            cmd.Parameters.AddWithValue("@DeliveryAddress", (object)deliveryAddress ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@TotalAmount", total);
                            cmd.Parameters.AddWithValue("@DeliveryCost", deliveryCost);

                            orderId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        // Позиции + списание
                        const string insertItemSql = @"
                            INSERT INTO OrderItem
                                (OrderID, ProductID, Quantity, PricePerUnit, LineTotal)
                            VALUES
                                (@OrderID, @ProductID, @Quantity, @PricePerUnit, @LineTotal);";

                        const string updateStockSql = @"
                            UPDATE Stock
                            SET Quantity = Quantity - @Quantity,
                                UpdatedAt = GETDATE()
                            WHERE ProductID = @ProductID
                              AND StockID = (
                                  SELECT TOP 1 StockID FROM Stock
                                  WHERE ProductID = @ProductID AND Quantity - Reserved >= @Quantity
                                  ORDER BY Quantity DESC);";

                        foreach (var item in items)
                        {
                            using (var cmd = new SqlCommand(insertItemSql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@OrderID", orderId);
                                cmd.Parameters.AddWithValue("@ProductID", item.Product.ProductID);
                                cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                                cmd.Parameters.AddWithValue("@PricePerUnit", item.Product.Price);
                                cmd.Parameters.AddWithValue("@LineTotal", item.LineTotal);
                                cmd.ExecuteNonQuery();
                            }

                            using (var cmd = new SqlCommand(updateStockSql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ProductID", item.Product.ProductID);
                                cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                        return orderNumber;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static int GetAvailableInTransaction(int productId, SqlConnection conn, SqlTransaction tx)
        {
            const string sql = @"
                SELECT ISNULL(SUM(Quantity - Reserved), 0)
                FROM Stock WHERE ProductID = @id";

            using (var cmd = new SqlCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", productId);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // ИСТОРИЯ ЗАКАЗОВ 
        public static List<OrderSession.Order> GetClientOrders(int clientId)
        {
            var orders = new List<OrderSession.Order>();

            const string sqlOrders = @"
                SELECT 
                    o.OrderNumber,
                    o.CreatedAt,
                    os.StatusID  AS StatusID,
                    os.Name      AS StatusName,
                    dm.Name      AS DeliveryName,
                    pm.Name      AS PaymentName,
                    ISNULL(o.DeliveryAddress, '') AS Address,
                    o.TotalAmount,
                    o.DeliveryCost
                FROM [Order] o
                INNER JOIN OrderStatus   os ON o.StatusID         = os.StatusID
                INNER JOIN DeliveryMethod dm ON o.DeliveryMethodID = dm.DeliveryMethodID
                INNER JOIN PaymentMethod pm ON o.PaymentMethodID  = pm.PaymentMethodID
                WHERE o.ClientID = @clientId
                ORDER BY o.CreatedAt DESC";

            const string sqlItems = @"
                SELECT 
                    oi.ProductID,
                    p.SKU,
                    p.Name,
                    oi.Quantity,
                    oi.PricePerUnit
                FROM OrderItem oi
                INNER JOIN [Order] o ON oi.OrderID = o.OrderID
                INNER JOIN Product p ON oi.ProductID = p.ProductID
                WHERE o.OrderNumber = @orderNumber";

            using (var conn = GetConnection())
            {
                conn.Open();

                using (var cmd = new SqlCommand(sqlOrders, conn))
                {
                    cmd.Parameters.AddWithValue("@clientId", clientId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            orders.Add(new OrderSession.Order
                            {
                                OrderNumber = reader.GetString(0),
                                OrderDate = reader.GetDateTime(1),
                                StatusID = reader.GetInt32(2),
                                Status = reader.GetString(3),
                                DeliveryMethod = reader.GetString(4),
                                PaymentMethod = reader.GetString(5),
                                Address = reader.GetString(6),
                                Subtotal = reader.GetDecimal(7),
                                DeliveryCost = reader.GetDecimal(8),
                                Total = reader.GetDecimal(7) + reader.GetDecimal(8),
                                Items = new List<CartItem>()
                            });
                        }
                    }
                }

                foreach (var order in orders)
                {
                    using (var cmd = new SqlCommand(sqlItems, conn))
                    {
                        cmd.Parameters.AddWithValue("@orderNumber", order.OrderNumber);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int productId = reader.GetInt32(0);
                                string sku = reader.GetString(1);
                                string name = reader.GetString(2);
                                int qty = reader.GetInt32(3);
                                decimal price = reader.GetDecimal(4);

                                order.Items.Add(new CartItem
                                {
                                    Product = new Product
                                    {
                                        ProductID = productId,
                                        SKU = sku,
                                        Name = name,
                                        Price = price,
                                        Category = "",
                                        ImagePath = ResolveImagePath(sku)
                                    },
                                    Quantity = qty
                                });
                            }
                        }
                    }

                    // Рассчитываем ожидаемое время готовности от даты создания
                    int totalPallets = order.Items.Sum(i => i.Quantity);
                    bool isDelivery = order.DeliveryMethod != "Самовывоз";

                    order.ExpectedReadyAt = TimeCalculator.CalculateReadyTime(
                        order.OrderDate, totalPallets, isDelivery);
                }
            }

            return orders;
        }
    }

    /// Вспомогательный класс для автозаполнения данных клиента.
    public class ClientInfo
    {
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
    }
}