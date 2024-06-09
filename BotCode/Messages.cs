using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BotCode
{
    public class Messages
    {
        private static readonly string connectionString = "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=C:\\Users\\user\\source\\repos\\SQLforTelegramBot\\SQLforTelegramBot\\TestDB.mdf;Integrated Security=True";
        static System.Threading.Timer timer;
        public static int lastMessageId = 0;

        public static void ScheduleDailyTask(int hour, int minute, Func<Task> task)
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

        public static bool AreThereNewMessages(int lastMessageId)
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
        public static void UpdateLastMessageId()
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
    }
}
