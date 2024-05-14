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

namespace BotCode
{
    class Program
    {
        
        private static readonly string connectionString = "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=C:\\Users\\user\\source\\repos\\SQLforTelegramBot\\SQLforTelegramBot\\TestDB.mdf;Integrated Security=True";
        static bool isSending = false;
        static System.Threading.Timer timer;
        static void Main(string[] args)
        {
            var client = new TelegramBotClient("6339879171:AAHQMkkiLuEDfT1dCcVGXp_QHuDvFryHovw");
            client.StartReceiving(Update, Error);
            Console.ReadLine();
        }

        public static void RegisterUser(string userName)
        {
            if (IsUserExist(userName))
            {
                return; // если пользователь уже существует, выходим из функции
            }
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand();
                command.Connection = connection;
                command.CommandText = $"INSERT INTO [ClientsID] (userID) VALUES ('{userName}')";
                command.ExecuteNonQuery();
            }
        }
        public static bool IsUserExist(string userName)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand();
                command.Connection = connection;
                command.CommandText = $"Select userID from [ClientsID] where userID like '{userName}'";
                return command.ExecuteScalar() != null;
            }
        }
        public static List<string> GetUsers()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                var users = new List<string>();
                connection.Open();
                var command = new SqlCommand();
                command.Connection = connection;
                command.CommandText = $"Select userID from [ClientsID]";
                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    users.Add(reader.GetString(0));
                }
                return users;
            }
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
    
    async static Task Update(ITelegramBotClient BotClient, Update update, CancellationToken token)
    {
            if (update.Type == Telegram.Bot.Types.Enums.UpdateType.Message)
            {
                var message = update.Message;
                if (message.Text.ToLower().Contains("/start"))
                {
                    RegisterUser(update.Message.From.Id.ToString());
                    List<string> users = GetUsers();

                    bool isSending = false;

                    //System.Timers.Timer timer = new System.Timers.Timer(60000); // 1 минута = 60 000 миллисекунд

                    ScheduleDailyTask(9, 0, async () => {
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

                    //timer.Start();
                    //RegisterUser('@' + update.Message.From.Username);
                    var replyKeyboard = new ReplyKeyboardMarkup(new[]
                    {
                new[]
                {
                    new KeyboardButton("Расписание"),
                    new KeyboardButton("Тренера"),
                    new KeyboardButton("Тарифы"),
                    new KeyboardButton("О зале"),
                    new KeyboardButton("Новости"),
                    new KeyboardButton("О себе")
                }
                    });

                    await BotClient.SendTextMessageAsync(message.Chat.Id, "Привет!\U0001F44B\n\nС помощью этого бота, ты можешь:\n\n" +
                        "\U0001F5D3Посмотреть расписание групповых тренировок.\n\n \U0001F3CB\u200DИзучить портфолио тренеров.\n\n" +
                        " \U0001F4B8Узнать про актуальные тарифы на абонемент.\n\n \u2139Посмотреть контакты и подробную информацию о зале.\n\n" +
                        " \u2757Прочитать новости и объявления, связанные с клубом.", replyMarkup: replyKeyboard);
                    return;
                }
                switch (message.Text.ToLower())
                {
                    case "расписание":
                        {
                            using (var fileStream = System.IO.File.Open("C:\\Users\\user\\Pictures\\расписание.pdf", System.IO.FileMode.Open))
                            {
                                var fileToSend = InputFile.FromStream(fileStream, "Расписание групповых занятий.pdf");
                                await BotClient.SendDocumentAsync(message.Chat.Id, fileToSend, caption: "Файл для вас!");
                            }
                        }
                        break;
                    case "тренера":
                        {
                            using (var fileStream = System.IO.File.Open("C:\\Users\\user\\Pictures\\фитнес зал\\тренера 1.pdf", System.IO.FileMode.Open))
                            {
                                var fileToSend = InputFile.FromStream(fileStream, "Тренера.pdf");
                                await BotClient.SendDocumentAsync(message.Chat.Id, fileToSend, caption: "Файл для вас!");
                            }
                            break;
                        }
                    case "тарифы":
                        {
                            using (var fileStream = System.IO.File.Open("C:\\Users\\user\\Downloads\\A4 - 7.pdf", System.IO.FileMode.Open))
                            {
                                var fileToSend = InputFile.FromStream(fileStream, "Тарифы.pdf");
                                await BotClient.SendDocumentAsync(message.Chat.Id, fileToSend, caption: "Файл для вас!");
                            }
                        }
                        break;
                    case "о зале":
                        await BotClient.SendTextMessageAsync(message.Chat.Id, "АДМИРАЛ - это новый спортзал премиум-класса в Перми. " +
                            "Мы предлагаем своим клиентам только новое и современное оборудование. В каждом зале есть сауна и душ. " +
                            "Нам важен комфорт наших клиентов, поэтому у нас действует политика ограничения количества продаваемых абонементов. " +
                            "Вам больше не придется стоять в очереди на тренажер или с трудом искать свободное место на групповой тренировке!" +
                            "\n\nСпортзал находится по адресу: ул.Попова д.56 этаж 5\nКак пройти?\nНеобходимо подняться на 5 этаж " +
                            "и повернуть налево. Далее будет табличка с названием нашего зала и указатель на нужную дверь.\n\nКак купить абонемент?" +
                            "\nДля покупки абонемента вам необходимо связаться с администратором, который поможет подобрать нужный тариф и отправит реквизиты для оплаты. " +
                            "Также выбрать и оплатить абонемент можно на стойке-ресепшн в зале по указанному адресу.\n\nКонтакты:\nТелефон администратора: 7**********\nТелефон консультанта: 7******** , Телеграм аккаунт: @******");
                        break;
                    case "новости":
                        //await BotClient.SendTextMessageAsync(message.Chat.Id, "Тут будут новости.");
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
                                        string news = $"{reader["news"]}";
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
                    case "о себе":
                        await BotClient.SendTextMessageAsync(message.Chat.Id, "Пожалуйста, отправьте ваш номер абонемента для получения информации о себе.");
                        break;
                    default:
                        {
                            if (message.Text.StartsWith("59"))
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
                            else
                            {
                                await BotClient.SendTextMessageAsync(message.Chat.Id, "Такой кнопки не существует, не можем обработать ваше сообщение");
                            }
                            break;
                        }
                }
            }
        }
        private static async Task Error(ITelegramBotClient client, Exception exception, CancellationToken token)
        {
        }

    }
}