using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace SQLforTelegramBot
{
    public partial class Form1 : Form
    {
        private SqlConnection sqlConnection = null;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load_1(object sender, EventArgs e)
        {
            sqlConnection = new SqlConnection(ConfigurationManager.ConnectionStrings["TestDB"].ConnectionString);

            sqlConnection.Open();

            if (sqlConnection.State == ConnectionState.Open)
            {
                MessageBox.Show("Подключение установлено!");
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SqlCommand command = new SqlCommand($"INSERT INTO [Clients] (Name, Surname, NumberOfPhone, StartDay, EndDay, GymMembership) VALUES (N'{textBox1.Text}', N'{textBox2.Text}', '{textBox3.Text}', '{textBox4.Text}', '{textBox5.Text}', N'{textBox6.Text}')", sqlConnection);
            MessageBox.Show(command.ExecuteNonQuery().ToString());
        }
    }
}
