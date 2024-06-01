using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot;

namespace BotCode
{
    public class SendSchedule
    {
        private static readonly string connectionString = "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=C:\\Users\\user\\source\\repos\\SQLforTelegramBot\\SQLforTelegramBot\\TestDB.mdf;Integrated Security=True";
        public static async Task SendTrainerSchedule(ITelegramBotClient botClient, Message message, string trainerName)
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

        public static async Task SendTrainerAppointments(ITelegramBotClient botClient, Message message, string trainerName, int trainerId)
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

        public static async Task ProcessTrainerCredentials(ITelegramBotClient botClient, Message message)
        {
            string[] parts = message.Text.Split(' ');

            if (parts.Length == 4)
            {
                string trainerName = $"{parts[1]} {parts[2]}"; // Объединяем имя и фамилию
                if (int.TryParse(parts[3], out int subscriptionId))
                {
                    await SendSchedule.SendTrainerAppointments(botClient, message, trainerName, subscriptionId);
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
    }
}
