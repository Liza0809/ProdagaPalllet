using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PalletApp.Models;

namespace PalletApp.Services
{
    /// Сохраняет и загружает корзину клиента в файл.
    /// Файл: %AppData%\PalletApp\cart_{login}.json
    public static class CartStorage
    {
        private static string GetFolder()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PalletApp");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return folder;
        }

        private static string GetFilePath(string login)
        {
            string safeLogin = string.IsNullOrWhiteSpace(login) ? "guest" : login;
            return Path.Combine(GetFolder(), $"cart_{safeLogin}.json");
        }

        // Сериализуемая модель для JSON
        private class CartDto
        {
            public int ProductID { get; set; }
            public int Quantity { get; set; }
        }

        public static void Save(string login, IEnumerable<CartItem> items)
        {
            try
            {
                var list = items
                    .Where(i => i.Product != null)
                    .Select(i => new CartDto
                    {
                        ProductID = i.Product.ProductID,
                        Quantity = i.Quantity
                    })
                    .ToList();

                string json = JsonSerializer.Serialize(list,
                    new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(GetFilePath(login), json);
            }
            catch
            {
                
            }
        }

        public static List<(int ProductID, int Quantity)> Load(string login)
        {
            try
            {
                string path = GetFilePath(login);
                if (!File.Exists(path)) return new List<(int, int)>();

                string json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize<List<CartDto>>(json);

                return list?
                    .Select(d => (d.ProductID, d.Quantity))
                    .ToList() ?? new List<(int, int)>();
            }
            catch
            {
                return new List<(int, int)>();
            }
        }

        public static void Clear(string login)
        {
            try
            {
                string path = GetFilePath(login);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }
    }
}