using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotCode
{
    public class CheckUsers
    {
        private static readonly string connectionString = "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=C:\\Users\\user\\source\\repos\\SQLforTelegramBot\\SQLforTelegramBot\\TestDB.mdf;Integrated Security=True";

        public static bool IsUserClient(string userName, string cardNumber)
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
    }
}
