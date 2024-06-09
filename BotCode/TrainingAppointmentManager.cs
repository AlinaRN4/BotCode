using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot;
using System.Data.SqlClient;
using System.Globalization;
using Telegram.Bot.Types.ReplyMarkups;

namespace BotCode
{
    internal class TrainingAppointmentManager
    {
        private static readonly string connectionString = "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=C:\\Users\\user\\source\\repos\\SQLforTelegramBot\\SQLforTelegramBot\\TestDB.mdf;Integrated Security=True";

        public static async Task HandleBookTraining(ITelegramBotClient botClient, Message message)
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
        public static async Task BotOnCallbackQueryReceived(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            switch (callbackQuery.Data)
            {
                case "view_trainers_schedule":
                    await SendTrainersList(botClient, callbackQuery.Message);
                    break;
                case "make_appointment":
                    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Для записи на тренировку введите данные в формате: 'MakeAppointment ИмяТренера ФамилияТренера ВашеИмя ВашаФамилия ВашНомерАбонемента dd-MM-yyyy HH:mm'");
                    break;
                case "check_availability":
                    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Для просмотра свободных мест тренера используйте формат: 'AvailableDates ИмяТренера ФамилияТренера dd-MM-yyyy' и мы выведем информацию на ближайшие 7 дней от поставленной вами даты.");
                    break;
                default:
                    if (callbackQuery.Data.StartsWith("trainer_schedule"))
                    {
                        string trainerName = callbackQuery.Data.Replace("trainer_schedule", "");
                        await SendSchedule.SendTrainerSchedule(botClient, callbackQuery.Message, trainerName);
                    }
                    break;
            }
        }

        public static async Task InputAvailableDates(ITelegramBotClient BotClient, Message message)
        {
            // Разделение строки
            string[] parts = message.Text.Split(' ');

            if (parts.Length >= 4 && parts[0] == "AvailableDates")
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
                    await BotClient.SendTextMessageAsync(message.Chat.Id, "Неверный формат имени тренера. Пожалуйста, используйте формат: 'AvailableDates ИмяТренера ФамилияТренера dd-MM-yyyy'");
                }
            }
            else
            {
                await BotClient.SendTextMessageAsync(message.Chat.Id, "Неверный формат ввода. Пожалуйста, используйте формат: 'AvailableDates ИмяТренера ФамилияТренера dd-MM-yyyy'");
            }
        }

        public static bool IsTrainerAvailable(string trainerFullName, DateTime appointmentDate)
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

        public static async Task CreateAppointment(ITelegramBotClient BotClient, Message message)
        {
            string[] parts = message.Text.Split(' ');

            if (parts.Length == 8 && parts[0] == "MakeAppointment" &&
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

                if (CheckUsers.IsUserClient(userName, cardNumber))
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
                await BotClient.SendTextMessageAsync(message.Chat.Id, "Неверный формат ввода. Используйте 'MakeAppointment TrainerName TrainerSurname UserName UserSurname NumberOfCard dd-MM-yyyy HH:mm'.");
            }
        }
        public static string CheckPersonalTrainerName(string trainerFullName)
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
        public static async Task SendTrainersList(ITelegramBotClient botClient, Message message)
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

        
    }
}
