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

namespace BotCode
{
    class Program
    {
        
        private static readonly string connectionString = "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=C:\\Users\\user\\source\\repos\\SQLforTelegramBot\\SQLforTelegramBot\\TestDB.mdf;Integrated Security=True";
        static bool isSending = false;
        static System.Threading.Timer timer;
        private static int lastMessageId = 0;

        private static ConcurrentDictionary<long, string> userStates = new ConcurrentDictionary<long, string>();
        static void Main(string[] args)
        {
            var client = new TelegramBotClient("6339879171:AAHQMkkiLuEDfT1dCcVGXp_QHuDvFryHovw");
            client.StartReceiving(Update, Error);
            Console.ReadLine();
        }

        private static async Task HandleBookTraining(ITelegramBotClient botClient, Message message)
        {
            var inlineKeyboard = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
            {
    new []
    {
        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("Посмотреть время работы тренеров", "view_trainers_schedule"),
    },
    new []
    {
        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("Посмотреть свободные места", "check_availability"),
    },
    new []
    {
        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("Сделать запись", "make_appointment"),
    }
});

            await botClient.SendTextMessageAsync(message.Chat.Id, "Выберите опцию:", replyMarkup: inlineKeyboard);
        }

        static async Task BotOnCallbackQueryReceived(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            switch (callbackQuery.Data)
            {
                case "view_trainers_schedule":
                    await SendTrainersList(botClient, callbackQuery.Message);
                    break;
                case "make_appointment":
                    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Для записи на тренировку введите данные в формате: '/MakeAppointment ИмяТренера ФамилияТренера ВашеИмя ВашаФамилия ВашНомерАбонемента dd-MM-yyyy HH:mm'");
                    break;
                case "check_availability":
                    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Для просмотра свободных мест тренера используйте формат: '/AvailableDates ИмяТренера ФамилияТренера dd-MM-yyyy' и мы выведем информацию на ближайшие 7 дней от поставленной вами даты.");
                    break;
                default:
                    if (callbackQuery.Data.StartsWith("trainer_schedule"))
                    {
                        string trainerName = callbackQuery.Data.Replace("trainer_schedule", "");
                        await SendTrainerSchedule(botClient, callbackQuery.Message, trainerName);
                    }
                    break;
            }
        }
        private static async Task InputAvailableDates(ITelegramBotClient BotClient, Message message)
        {
            // Разделение строки
            string[] parts = message.Text.Split(' ');

            if (parts.Length >= 4 && parts[0] == "/AvailableDates")
            {
                string trainerId = parts[1] + ' ' + parts[2];
                string[] nameParts = parts.Skip(1).Take(2).ToArray();
                string datePart = parts[3];

                if (nameParts.Length == 2)
                {
                    if (DateTime.TryParseExact(datePart, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime startDate))
                    {
                        DateTime endDate = startDate.AddDays(7);
                        var availableDates = GetAvailableDates(trainerId, startDate, endDate); // Реализуйте данную функцию

                        if (availableDates.Any())
                        {
                            StringBuilder responseBuilder = new StringBuilder("Доступные даты и время:\n");

                            int? previousDay = null;

                            foreach (var date in availableDates)
                            {
                                int currentDay = date.Day;
                                if (previousDay.HasValue && previousDay != currentDay)
                                {
                                    responseBuilder.AppendLine(); // Добавляем пустую строку после смены дня
                                }

                                responseBuilder.AppendLine(date.ToString("dd-MM-yyyy HH:mm"));
                                previousDay = currentDay;
                            }

                            string response = responseBuilder.ToString();
                            await BotClient.SendTextMessageAsync(message.Chat.Id, response);
                        }
                        else
                        {
                            await BotClient.SendTextMessageAsync(message.Chat.Id, $"Нет доступных дат для указанного тренера с {startDate:dd-MM-yyyy} до {endDate:dd-MM-yyyy}.");
                        }
                    }
                    else
                    {
                        await BotClient.SendTextMessageAsync(message.Chat.Id, "Неверный формат даты. Пожалуйста, используйте формат: dd-MM-yyyy.");
                    }
                }
                else
                {
                    await BotClient.SendTextMessageAsync(message.Chat.Id, "Неверный формат имени тренера. Пожалуйста, используйте формат: '/AvailableDates ИмяТренера ФамилияТренера dd-MM-yyyy'");
                }
            }
            else
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, "Неверный формат ввода. Пожалуйста, используйте формат: '/AvailableDates ИмяТренера ФамилияТренера dd-MM-yyyy'");
            }
        }

        private static bool IsTrainerAvailable(string trainerFullName, DateTime appointmentDate)
        {

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
      SELECT StartTime, EndTime 
      FROM PersonalTrainersSchedule 
      WHERE TrainerName = @TrainerFullName 
      AND DayOfWeek = @DayOfWeek";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@TrainerFullName", trainerFullName);
                    cmd.Parameters.AddWithValue("@DayOfWeek", appointmentDate.ToString("dddd", new CultureInfo("ru-RU")));

                    con.Open();
                    SqlDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        TimeSpan startTime = reader.GetTimeSpan(0);
                        TimeSpan endTime = reader.GetTimeSpan(1);

                        if (appointmentDate.TimeOfDay >= startTime && appointmentDate.TimeOfDay <= endTime)
                        {
                            return true; // Тренер работает в это время
                        }
                    }
                }
            }

            return false; // Тренер не работает в это время
        }

        private static async Task CreateAppointment(ITelegramBotClient BotClient, Message message)
        {
            string[] parts = message.Text.Split(' ');

            if (parts.Length == 8 && parts[0] == "/MakeAppointment" &&
                DateTime.TryParseExact($"{parts[6]} {parts[7]}", "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime appointmentDate))
            {
                string trainerFullName = $"{parts[1]} {parts[2]}";

                if (trainerFullName.Split(' ').Length != 2)
                {
                    await BotClient.SendTextMessageAsync(message.Chat.Id, "Неверный формат имени тренера. Введите имя и фамилию тренера через пробел.");
                    return;
                }

                string userName = $"{parts[3]} {parts[4]}";
                string cardNumber = parts[5];

                string existingTrainerFullName = CheckPersonalTrainerName(trainerFullName);

                if (string.IsNullOrEmpty(existingTrainerFullName))
                {
                    await BotClient.SendTextMessageAsync(message.Chat.Id, "Тренер с указанными именем и фамилией не найден.");
                    return;
                }

                if (!IsTrainerAvailable(trainerFullName, appointmentDate))
                {
                    await BotClient.SendTextMessageAsync(message.Chat.Id, "Тренер не работает в это время. Пожалуйста, выберите другое время.");
                    return;
                }

                if (IsUserClient(userName, cardNumber))
                {
                    if (!IsAppointmentExist(userName, trainerFullName, appointmentDate))
                    {
                        try
                        {
                            RegisterAppointment(userName, trainerFullName, appointmentDate);
                            await BotClient.SendTextMessageAsync(message.Chat.Id, "Вы успешно записаны на тренировку.");
                        }
                        catch (SqlException ex) when (ex.Number == 547)
                        {
                            await BotClient.SendTextMessageAsync(message.Chat.Id, "Вы не можете записаться на тренировку в это время.");
                        }
                    }
                    else
                    {
                        await BotClient.SendTextMessageAsync(message.Chat.Id, "Вы уже записаны на тренировку в это время.");
                    }
                }
                else
                {
                    await BotClient.SendTextMessageAsync(message.Chat.Id, "Пользователь с указанными данными не найден в базе данных клиентов.");
                }
            }
            else
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, "Неверный формат ввода. Используйте '/MakeAppointment TrainerName TrainerSurname UserName UserSurname NumberOfCard dd-MM-yyyy HH:mm'.");
            }
        }
        private static string CheckPersonalTrainerName(string trainerFullName)
        {
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = "SELECT TrainerName FROM PersonalTrainersSchedule WHERE TrainerName = @TrainerFullName";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@TrainerFullName", trainerFullName);

                    con.Open();

                    object result = cmd.ExecuteScalar();

                    if (result != null)
                    {
                        return result.ToString();
                    }
                }
            }
            return null; // Верните null, если тренер с таким именем не найден
        }
        private static async Task SendTrainersList(ITelegramBotClient botClient, Message message)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT DISTINCT TrainerName FROM PersonalTrainersSchedule";
                SqlCommand command = new SqlCommand(query, connection);

                var trainerButtons = new List<InlineKeyboardButton>();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string trainerName = reader["TrainerName"].ToString();
                        trainerButtons.Add(InlineKeyboardButton.WithCallbackData(trainerName, $"trainer_schedule{trainerName}"));
                    }
                }

                var inlineKeyboard = new InlineKeyboardMarkup(trainerButtons.Select(button => new[] { button }));
                await botClient.SendTextMessageAsync(message.Chat.Id, "Выберите тренера:", replyMarkup: inlineKeyboard);
            }
        }
        private static async Task SendTrainerSchedule(ITelegramBotClient botClient, Message message, string trainerName)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT DayOfWeek, StartTime, EndTime FROM PersonalTrainersSchedule WHERE TrainerName = @TrainerName ORDER BY CASE " +
                                "WHEN DayOfWeek = 'Понедельник' THEN 1 " +
                                "WHEN DayOfWeek = 'Вторник' THEN 2 " +
                                "WHEN DayOfWeek = 'Среда' THEN 3 " +
                                "WHEN DayOfWeek = 'Четверг' THEN 4 " +
                                "WHEN DayOfWeek = 'Пятница' THEN 5 " +
                                "WHEN DayOfWeek = 'Суббота' THEN 6 " +
                                "WHEN DayOfWeek = 'Воскресенье' THEN 7 " +
                                "END";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@TrainerName", trainerName);

                StringBuilder schedule = new StringBuilder();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    schedule.AppendLine($"Расписание для тренера {trainerName}:");
                    while (reader.Read())
                    {
                        string dayOfWeek = reader["DayOfWeek"].ToString();
                        string startTime = reader["StartTime"] != DBNull.Value ? ((TimeSpan)reader["StartTime"]).ToString(@"hh\:mm") : "Выходной";
                        string endTime = reader["EndTime"] != DBNull.Value ? ((TimeSpan)reader["EndTime"]).ToString(@"hh\:mm") : "";

                        schedule.AppendLine($"{dayOfWeek}: {startTime} - {endTime}");
                    }
                }

                await botClient.SendTextMessageAsync(message.Chat.Id, schedule.ToString());
            }
        }
        public static List<DateTime> GetAvailableDates(string trainerId, DateTime startDate, DateTime endDate)
        {
            List<DateTime> availableDates = new List<DateTime>();
            List<TrainerSchedule> trainerSchedules = new List<TrainerSchedule>();
            // Словарь для перевода названий дней недели
            Dictionary<DayOfWeek, string> dayOfWeekTranslations = new Dictionary<DayOfWeek, string>
     {
         { DayOfWeek.Monday, "Понедельник" },
         { DayOfWeek.Tuesday, "Вторник" },
         { DayOfWeek.Wednesday, "Среда" },
         { DayOfWeek.Thursday, "Четверг" },
         { DayOfWeek.Friday, "Пятница" },
         { DayOfWeek.Saturday, "Суббота" },
         { DayOfWeek.Sunday, "Воскресенье" }
     };

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Получение расписания тренера
                    string scheduleQuery = @"
         SELECT DayOfWeek, StartTime, EndTime 
         FROM PersonalTrainersSchedule 
         WHERE TrainerName = @TrainerId";  // Изменяем на TrainerId

                    SqlCommand scheduleCommand = new SqlCommand(scheduleQuery, connection);
                    scheduleCommand.Parameters.AddWithValue("@TrainerId", trainerId);

                    using (SqlDataReader scheduleReader = scheduleCommand.ExecuteReader())
                    {
                        while (scheduleReader.Read())
                        {
                            trainerSchedules.Add(new TrainerSchedule
                            {
                                DayOfWeek = scheduleReader.GetString(0),
                                StartTime = scheduleReader.GetTimeSpan(1),
                                EndTime = scheduleReader.GetTimeSpan(2)
                            });
                        }
                    }

                    // Проверяем, получилось ли получить расписание
                    if (trainerSchedules.Count == 0)
                    {
                        Console.WriteLine("Нет расписания для тренера с ID: " + trainerId);
                        return availableDates;
                    }

                    // Получение занятых дат
                    string appointmentsQuery = @"
             SELECT appointment_date 
             FROM TrainerAppointments 
             WHERE trainer_id = @TrainerId AND appointment_date BETWEEN @StartDate AND @EndDate";

                    SqlCommand appointmentsCommand = new SqlCommand(appointmentsQuery, connection);
                    appointmentsCommand.Parameters.AddWithValue("@TrainerId", trainerId);
                    appointmentsCommand.Parameters.AddWithValue("@StartDate", startDate);
                    appointmentsCommand.Parameters.AddWithValue("@EndDate", endDate);

                    HashSet<DateTime> bookedDates = new HashSet<DateTime>();
                    using (SqlDataReader appointmentsReader = appointmentsCommand.ExecuteReader())
                    {
                        while (appointmentsReader.Read())
                        {
                            bookedDates.Add(appointmentsReader.GetDateTime(0));
                        }
                    }

                    // Проверка доступных дат
                    for (DateTime date = startDate; date <= endDate; date = date.AddHours(1))
                    {
                        if (bookedDates.Contains(date))
                            continue;

                        string dayOfWeekString = dayOfWeekTranslations[date.DayOfWeek];
                        var daySchedule = trainerSchedules.FirstOrDefault(s => s.DayOfWeek == dayOfWeekString); // Ищем расписание

                        if (daySchedule != null)
                        {
                            TimeSpan timeOfDay = date.TimeOfDay;
                            if (timeOfDay >= daySchedule.StartTime && timeOfDay < daySchedule.EndTime)
                            {
                                availableDates.Add(date);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Произошла ошибка: " + ex.Message);
            }

            return availableDates;
        }
        public static void RegisterAppointment(string userId, string trainerId, DateTime appointmentDate)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "INSERT INTO TrainerAppointments (user_id, trainer_id, appointment_date) VALUES (@UserId, @TrainerId, @AppointmentDate)";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserId", userId);
                command.Parameters.AddWithValue("@TrainerId", trainerId);
                command.Parameters.AddWithValue("@AppointmentDate", appointmentDate);
                command.ExecuteNonQuery();
            }
        }
        public static bool IsAppointmentExist(string userId, string trainerId, DateTime appointmentDate)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT COUNT(*) FROM TrainerAppointments WHERE user_id = @UserId AND trainer_id = @TrainerId AND appointment_date = @AppointmentDate";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserId", userId);
                command.Parameters.AddWithValue("@TrainerId", trainerId);
                command.Parameters.AddWithValue("@AppointmentDate", appointmentDate);
                int count = (int)command.ExecuteScalar();
                return count > 0;
            }
        }
        private static bool IsUserClient(string userName, string cardNumber)
        {

            // Разделяем имя на имя и фамилию
            string[] nameParts = userName.Split(' ');
            if (nameParts.Length != 2)
            {
                return false; // Неверный формат имени
            }

            string firstName = nameParts[0];
            string lastName = nameParts[1];

            string query = "SELECT COUNT(1) FROM Clients WHERE Name = @name AND Surname = @surname AND NumberOfCard = @numberOfCard";

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@name", firstName);
                command.Parameters.AddWithValue("@surname", lastName);
                command.Parameters.AddWithValue("@numberOfCard", cardNumber);

                connection.Open();
                return ((int)command.ExecuteScalar()) > 0;
            }
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

        //Расписание тренеров (отправка)

        private static async Task SendTrainerAppointments(ITelegramBotClient botClient, Message message, string trainerName, int trainerId)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Step 1: Check if the trainer exists
                string queryCheckTrainer = "SELECT COUNT(*) FROM PersonalTrainersSchedule WHERE TrainerName = @TrainerName AND IdOfTrainer = @TrainerId";
                SqlCommand commandCheckTrainer = new SqlCommand(queryCheckTrainer, connection);
                commandCheckTrainer.Parameters.AddWithValue("@TrainerName", trainerName);
                commandCheckTrainer.Parameters.AddWithValue("@TrainerId", trainerId);

                int trainerCount = (int)commandCheckTrainer.ExecuteScalar();
                if (trainerCount == 0)
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Trainer does not exist or ID is incorrect.");
                    return;
                }

                // Step 2: Retrieve appointments for the valid trainer
                string queryAppointments = "SELECT user_id, appointment_date FROM [TrainerAppointments] WHERE trainer_id = @TrainerName";
                SqlCommand commandAppointments = new SqlCommand(queryAppointments, connection);
                commandAppointments.Parameters.AddWithValue("@TrainerName", trainerName);

                StringBuilder appointmentsBuilder = new StringBuilder();
                appointmentsBuilder.AppendLine($"Встречи для тренера {trainerName}:");

                bool hasAppointments = false;
                using (SqlDataReader reader = commandAppointments.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        hasAppointments = true;
                        string clientName = reader["user_id"].ToString();
                        DateTime appointmentTime = (DateTime)reader["appointment_date"];

                        appointmentsBuilder.AppendLine($"Клиент: {clientName}, Время встречи: {appointmentTime:dd.MM.yyyy HH:mm}");
                    }
                }

                if (!hasAppointments)
                {
                    appointmentsBuilder.AppendLine("Нет запланированных встреч.");
                }

                await botClient.SendTextMessageAsync(message.Chat.Id, appointmentsBuilder.ToString());
            }
        }
        private static async Task ProcessTrainerCredentials(ITelegramBotClient botClient, Message message)
        {
            string[] parts = message.Text.Split(' ');

            if (parts.Length == 4)
            {
                string trainerName = $"{parts[1]} {parts[2]}"; // Объединяем имя и фамилию
                if (int.TryParse(parts[3], out int subscriptionId))
                {
                    await SendTrainerAppointments(botClient, message, trainerName, subscriptionId);
                }
                else
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Неверный формат ID абонемента. Пожалуйста, используйте формат /CheckMySchedule Имя Фамилия ID.");
                }
            }
            else
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Неверный формат. Пожалуйста, используйте команду /CheckMySchedule и введите ваше имя, фамилию и ID через пробел.");
            }
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

                    bool isSending2 = false;

                    System.Timers.Timer timer = new System.Timers.Timer(60000); // 1 минута = 60 000 миллисекунд

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
                new[]
                {
                    new KeyboardButton("Расписание"),
                    new KeyboardButton("Тренера"),
                    new KeyboardButton("Тарифы"),
                    new KeyboardButton("О зале"),
                    new KeyboardButton("Новости"),
                    new KeyboardButton("О себе"),
                    new KeyboardButton("Записаться на тренировку")

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
                    case "записаться на тренировку":
                        await HandleBookTraining(BotClient, message);
                        break;
                    case "я тренер":
                        BotClient.SendTextMessageAsync(message.Chat.Id, "Введите ваше имя, фамилию и ID через пробел после команды /CheckMySchedule" +
                           " (например, /CheckMySchedule Иван Иванов 123456):");
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
                            else if (message.Text.StartsWith("/AvailableDates"))
                                await InputAvailableDates(BotClient, message);
                            else if (message.Text.StartsWith("/MakeAppointment"))
                                await CreateAppointment(BotClient, message);
                            else if (message.Text.Contains("/CheckMySchedule"))
                                await ProcessTrainerCredentials(BotClient, message);

                            else
                            {
                                await BotClient.SendTextMessageAsync(message.Chat.Id, "Такой кнопки не существует, не можем обработать ваше сообщение");
                            }

                            break;
                        }
                                    
                }
            }
            else if (update.Type == Telegram.Bot.Types.Enums.UpdateType.CallbackQuery)
            {
                await BotOnCallbackQueryReceived(BotClient, update.CallbackQuery);
            }
        }
        private static async Task Error(ITelegramBotClient client, Exception exception, CancellationToken token)
        {
        }

    }
}