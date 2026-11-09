using System;

namespace PalletApp.Services
{
    public static class TimeCalculator
    {
        private const int WORK_START_HOUR = 9;
        private const int WORK_END_HOUR = 21;
        private const int PALLETS_PER_HOUR = 20;
        private const int DELIVERY_EXTRA_HOURS = 2;

        /// Считает дату и время готовности заказа.
        public static DateTime CalculateReadyTime(DateTime orderTime, int totalPallets, bool isDelivery)
        {
            int prepHours = (int)Math.Ceiling(totalPallets / (double)PALLETS_PER_HOUR);
            int totalHours = prepHours + (isDelivery ? DELIVERY_EXTRA_HOURS : 0);

            DateTime ready = orderTime.AddHours(totalHours);
            return AdjustToWorkHours(ready);
        }

        /// Корректирует дату так, чтобы попадала в рабочие часы 9:00–21:00.
        private static DateTime AdjustToWorkHours(DateTime time)
        {
            if (time.Hour < WORK_START_HOUR)
            {
                return new DateTime(time.Year, time.Month, time.Day,
                                    WORK_START_HOUR, 0, 0);
            }

            if (time.Hour >= WORK_END_HOUR)
            {
                DateTime nextDay = time.Date.AddDays(1);
                return nextDay.AddHours(WORK_START_HOUR);
            }

            return time;
        }

        /// Формирует текст для отображения пользователю.
        public static string BuildReadyMessage(DateTime orderTime, int totalPallets, bool isDelivery, string deliveryName)
        {
            DateTime ready = CalculateReadyTime(orderTime, totalPallets, isDelivery);

            int prepHours = (int)Math.Ceiling(totalPallets / (double)PALLETS_PER_HOUR);
            int totalHours = prepHours + (isDelivery ? DELIVERY_EXTRA_HOURS : 0);

            string mode = isDelivery ? "Доставка" : "Самовывоз";

            string message =
                $"{mode} будет готов(а) к {ready:dd.MM.yyyy HH:mm}.\n\n" +
                $"Время подготовки: {prepHours} ч. ({totalPallets} паллетомест / 20)\n";

            if (isDelivery)
                message += $"Дополнительно на доставку: {DELIVERY_EXTRA_HOURS} ч.\n";

            message += $"\nРабочие часы: 9:00 – 21:00.";

            return message;
        }
    }
}
