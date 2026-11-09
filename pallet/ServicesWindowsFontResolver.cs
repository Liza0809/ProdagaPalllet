using System;
using System.Collections.Generic;
using System.IO;
using PdfSharp.Fonts;

namespace PalletApp.Services
{
    /// Простой резолвер, который отдаёт PdfSharp шрифты из папки C:\Windows\Fonts.
    public class WindowsFontResolver : IFontResolver
    {
        // Соответствие: "Arial#Bold" → файл arialbd.ttf и т.д.
        private static readonly Dictionary<string, string> _map =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Arial#Regular", "arial.ttf" },
                { "Arial#Bold",    "arialbd.ttf" },
                { "Arial#Italic",  "ariali.ttf" },
                { "Arial#BoldItalic", "arialbi.ttf" },

                { "Times New Roman#Regular", "times.ttf" },
                { "Times New Roman#Bold",    "timesbd.ttf" },
                { "Times New Roman#Italic",  "timesi.ttf" },
                { "Times New Roman#BoldItalic", "timesbi.ttf" },

                { "Courier New#Regular", "cour.ttf" },
                { "Courier New#Bold",    "courbd.ttf" }
            };

        public byte[] GetFont(string faceName)
        {
            // faceName приходит в формате "Arial#Bold" и т.п.
            if (_map.TryGetValue(faceName, out var fileName))
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                    fileName);

                if (File.Exists(path))
                    return File.ReadAllBytes(path);
            }

            // Fallback — обычный Arial
            string fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                "arial.ttf");

            return File.Exists(fallback) ? File.ReadAllBytes(fallback) : null;
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            // Нормализуем имя: "Arial" → "Arial"
            string name = familyName;

            // Соберём ключ "Имя#Стиль"
            string style = "Regular";
            if (isBold && isItalic) style = "BoldItalic";
            else if (isBold) style = "Bold";
            else if (isItalic) style = "Italic";

            // Если такого стиля нет — используем Regular
            string key = $"{name}#{style}";
            if (!_map.ContainsKey(key))
                key = $"{name}#Regular";

            // Если и имени нет в словаре — используем Arial
            if (!_map.ContainsKey(key))
                key = "Arial#Regular";

            return new FontResolverInfo(key);
        }
    }
}