using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSocket
{
    internal class DataBase
    {
        MySqlConnection sqlConnection = new MySqlConnection(@"Server=localhost;Database=db;Uid=CafeUser;Pwd=Cafe2023");

        public void openConnection()
        {
            try
            {
                sqlConnection.Open();
            }
            catch
            {
                
            }
        }

        public void closeConnection()
        {
            sqlConnection.Close();
        }

        public MySqlConnection GetConnection()
        {
            return sqlConnection;
        }
    }
}
