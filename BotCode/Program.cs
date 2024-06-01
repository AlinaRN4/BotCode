using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.ReplyMarkups;
using System.Data.SqlClient;
using SQLforTelegramBot;
using System.Xml.Linq;
using Telegram.Bot.Args;
using System.Collections.Generic;
using System.Timers;
using System.Linq;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Runtime.Remoting.Messaging;
using Telegram.Bot.Types.Enums;

namespace BotCode
{
    class Program
    {
        
        private static readonly string connectionString = "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=C:\\Users\\user\\source\\repos\\SQLforTelegramBot\\SQLforTelegramBot\\TestDB.mdf;Integrated Security=True";
        static bool isSending = false;
        static System.Threading.Timer timer;
        private static int lastMessageId = 0;

        static void Main(string[] args)
        {
            var client = new TelegramBotClient("6339879171:AAHQMkkiLuEDfT1dCcVGXp_QHuDvFryHovw");
            client.StartReceiving(Update, Error);
            Console.ReadLine();
        }

        static void ScheduleDailyTask(int hour, int minute, Func<Task> task)
        {
            DateTime now = DateTime.Now;
            DateTime firstRun = new DateTime(
                now.Year, now.Month, now.Day, hour, minute, 0, 0, DateTimeKind.Local);

            if (now > firstRun)
            {
                firstRun = firstRun.AddDays(1);
            }
            TimeSpan timeToGo = firstRun - now;
            timer = new System.Threading.Timer(x =>
            {
                task.Invoke().Wait();
                ScheduleDailyTask(hour, minute, task); // перепланируем задачу на следующий день
            },
            null, timeToGo, Timeout.InfiniteTimeSpan);
        }

        private static bool AreThereNewMessages(int lastMessageId)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    var query = "SELECT MAX(ID) FROM NewsOfGym";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        var result = command.ExecuteScalar();
                        if (result != DBNull.Value)
                        {
                            int maxMessageId = (int)result;
                            return maxMessageId > lastMessageId;
                        }

                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return false;
            }
        }
        private static void UpdateLastMessageId()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    var query = "SELECT MAX(ID) FROM NewsOfGym";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        var result = command.ExecuteScalar();
                        if (result != DBNull.Value)
                        {
                            lastMessageId = (int)result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        
        async static Task Update(ITelegramBotClient BotClient, Update update, CancellationToken token)
        {
            var message = update.Message;
            if (update.Type == Telegram.Bot.Types.Enums.UpdateType.Message)
            {
                // Убедимся, что это обновление с сообщением
                if (update.Message == null) return;

                // Проверим, что текст сообщения не равен null
                if (update.Message.Text == null)
                {
                    // Обработка случая, когда текст сообщения равен null
                    await BotClient.SendTextMessageAsync(update.Message.Chat, "Сообщение не содержит текст.");
                    return;
                }

                if (message.Text != null && message.Text.ToLower().Contains("/start"))
                {
                    CheckUsers.RegisterUser(update.Message.From.Id.ToString());
                    List<string> users = CheckUsers.GetUsers();

                    bool isSending2 = false;
                    int minute = 60 * 1000; // 1 minute
                    System.Timers.Timer timer = new System.Timers.Timer(minute); // 1 минута = 60 000 миллисекунд

                    timer.Elapsed += async (s, ev) => {
                        if (!isSending2)
                        {
                            isSending2 = true;
                            if (AreThereNewMessages(lastMessageId))
                            {
                                foreach (string user in users)
                                {
                                    await BotClient.SendTextMessageAsync(user, "Появилась новая новость!");
                                }

                                // Обновляем lastMessageId до текущего максимального MessageID
                                UpdateLastMessageId();
                            }
                            isSending2 = false;

                        }
                    };

                    timer.Start();

                    bool isSending = false;
                    ScheduleDailyTask(9, 0, async () =>
                    {
                        if (!isSending)
                        {
                            isSending = true;

                            using (SqlConnection connection = new SqlConnection(connectionString))
                            {
                                connection.Open();
                                string query = "SELECT TOP 1 message FROM MotivationalMessages ORDER BY NEWID()";
                                SqlCommand command = new SqlCommand(query, connection);

                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.HasRows)
                                    {
                                        while (reader.Read())
                                        {
                                            string messages = $"{reader["message"]}";
                                            //await BotClient.SendTextMessageAsync(message.Chat.Id, messages);
                                            foreach (string user in users)
                                            {
                                                await BotClient.SendTextMessageAsync(user, messages);
                                            }
                                            isSending = false;
                                        }
                                    }
                                }
                            }
                        }
                    });

                    var replyKeyboard = new ReplyKeyboardMarkup(new[]
                    {
                        new[] { new KeyboardButton("📅 Расписание") },
                        new[] { new KeyboardButton("👨‍🏫 Тренера") },
                        new[] { new KeyboardButton("💰 Тарифы") },
                        new[] { new KeyboardButton("🏋️‍♂️ О зале") },
                        new[] { new KeyboardButton("📰 Новости") },
                        new[] { new KeyboardButton("👤 О себе") },
                        new[] { new KeyboardButton("✏️ Записаться на тренировку") },
                        new[] { new KeyboardButton("🏅 Я тренер") }
                    });

                    await BotClient.SendTextMessageAsync(message.Chat.Id, "Привет!\U0001F44B\n\nС помощью этого бота, ты можешь:\n\n" +
                        "\U0001F5D3Посмотреть расписание групповых тренировок.\n\n \U0001F3CB\u200DИзучить портфолио тренеров.\n\n" +
                        " \U0001F4B8Узнать про актуальные тарифы на абонемент.\n\n \u2139Посмотреть контакты и подробную информацию о зале.\n\n" +
                        " \u2757Прочитать новости и объявления, связанные с клубом.", replyMarkup: replyKeyboard);
                    return;
                }
                switch (message.Text != null ? message.Text.ToLower() : "")
                {
                    case "📅 расписание":
                        {
                            using (var fileStream = System.IO.File.Open("C:\\Users\\user\\Pictures\\расписание.pdf", System.IO.FileMode.Open))
                            {
                                var fileToSend = InputFile.FromStream(fileStream, "Расписание групповых занятий.pdf");
                                await BotClient.SendDocumentAsync(message.Chat.Id, fileToSend, caption: "Файл для вас!");
                            }
                        }
                        break;
                    case "👨‍🏫 тренера":
                        {
                            using (var fileStream = System.IO.File.Open("C:\\Users\\user\\Pictures\\тренера 1-объединены.pdf", System.IO.FileMode.Open))
                            {
                                var fileToSend = InputFile.FromStream(fileStream, "Тренера.pdf");
                                await BotClient.SendDocumentAsync(message.Chat.Id, fileToSend, caption: "Файл для вас!");
                            }
                            break;
                        }
                    case "💰 тарифы":
                        {
                            using (var fileStream = System.IO.File.Open("C:\\Users\\user\\Pictures\\A4 - 7.pdf", System.IO.FileMode.Open))
                            {
                                var fileToSend = InputFile.FromStream(fileStream, "Тарифы.pdf");
                                await BotClient.SendDocumentAsync(message.Chat.Id, fileToSend, caption: "Файл для вас!");
                            }
                        }
                        break;
                    case "🏋️‍♂️ о зале":
                        await BotClient.SendTextMessageAsync(message.Chat.Id, "АДМИРАЛ - это новый спортзал премиум-класса в Перми. " +
                            "Мы предлагаем своим клиентам только новое и современное оборудование. В каждом зале есть сауна и душ. " +
                            "Нам важен комфорт наших клиентов, поэтому у нас действует политика ограничения количества продаваемых абонементов. " +
                            "Вам больше не придется стоять в очереди на тренажер или с трудом искать свободное место на групповой тренировке!" +
                            "\n\nСпортзал находится по адресу: ул.Попова д.56 этаж 5\nКак пройти?\nНеобходимо подняться на 5 этаж " +
                            "и повернуть налево. Далее будет табличка с названием нашего зала и указатель на нужную дверь.\n\nКак купить абонемент?" +
                            "\nДля покупки абонемента вам необходимо связаться с администратором, который поможет подобрать нужный тариф и отправит реквизиты для оплаты. " +
                            "Также выбрать и оплатить абонемент можно на стойке-ресепшн в зале по указанному адресу.\n\nКонтакты:\nТелефон администратора: 7**********\nТелефон консультанта: 7******** , Телеграм аккаунт: @******");
                        break;
                    case "📰 новости":
                        using (SqlConnection connection = new SqlConnection(connectionString))
                        {
                            connection.Open();
                            string query = "SELECT news FROM NewsOfGym";
                            SqlCommand command = new SqlCommand(query, connection);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        string news = $"⚡️{reader["news"]}";
                                        await BotClient.SendTextMessageAsync(message.Chat.Id, news);
                                    }
                                }
                                else
                                {
                                    await BotClient.SendTextMessageAsync(message.Chat.Id, "К сожалению, новостей на сегодня нет.");
                                }
                            }
                        }
                        break;
                    case "👤 о себе":
                        await BotClient.SendTextMessageAsync(message.Chat.Id, "Пожалуйста, отправьте ваш номер абонемента для получения информации о себе.");
                        break;
                    case "✏️ записаться на тренировку":
                        await TrainingAppointmentManager.HandleBookTraining(BotClient, message);
                        break;
                    case "🏅 я тренер":
                        BotClient.SendTextMessageAsync(message.Chat.Id, "Введите ваше имя, фамилию и ID через пробел после команды /CheckMySchedule" +
                           " (например, /CheckMySchedule Иван Иванов 123456):");
                        break;
                    default:
                        {
                            if (message.Text == null)
                            {
                                return;
                            }
                            else if (message.Text.StartsWith("59"))
                            {
                                string userCardNumber = message.Text;
                                using (SqlConnection connection = new SqlConnection(connectionString))
                                {
                                    connection.Open();
                                    string query = "SELECT * FROM Clients WHERE NumberOfCard = @NumberOfCard";
                                    SqlCommand command = new SqlCommand(query, connection);
                                    command.Parameters.AddWithValue("@NumberOfCard", userCardNumber);

                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        if (reader.HasRows)
                                        {
                                            while (reader.Read())
                                            {
                                                string clientInfo = $"Имя: {reader["Name"]}\nФамилия: {reader["Surname"]}\nНомер телефона: {reader["NumberOfPhone"]}\nДата начала: {reader["StartDay"]}\nДата конца: {reader["EndDay"]}\nАбонемент: {reader["GymMembership"]}\nНомер абонемента: {reader["NumberOfCard"]}";
                                                await BotClient.SendTextMessageAsync(message.Chat.Id, clientInfo);
                                            }
                                        }
                                        else
                                        {
                                            await BotClient.SendTextMessageAsync(message.Chat.Id, "К сожалению, информация по данному абонементу отсутствует.");
                                        }
                                    }
                                }
                            }
                            else if (message.Text.StartsWith("/AvailableDates"))
                                await TrainingAppointmentManager.InputAvailableDates(BotClient, message);
                            else if (message.Text.StartsWith("/MakeAppointment"))
                                await TrainingAppointmentManager.CreateAppointment(BotClient, message);
                            else if (message.Text.Contains("/CheckMySchedule"))
                                await SendSchedule.ProcessTrainerCredentials(BotClient, message);
                            else
                            {
                                await BotClient.SendTextMessageAsync(message.Chat.Id, "Такой кнопки не существует, не можем обработать ваше сообщение");
                            }
                            break;
                        }
                                    
                }
            }
            else if(update.Type == Telegram.Bot.Types.Enums.UpdateType.CallbackQuery)
            {
                await TrainingAppointmentManager.BotOnCallbackQueryReceived(BotClient, update.CallbackQuery);
            }
            else
            {
                // В случае, если пользователь ввел не текстовое сообщение
                await BotClient.SendTextMessageAsync(message.Chat.Id, "Упс, я еще не научился с вами общаться. Пожалуйста, используйте текстовые сообщения.");
            }
        }
        private static async Task Error(ITelegramBotClient client, Exception exception, CancellationToken token)
        {
        }

    }
}