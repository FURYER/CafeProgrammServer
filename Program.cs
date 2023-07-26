using System.Net.Sockets;
using System.Net;
using System.Text;
using ServerSocket;
using Timer = System.Timers.Timer;
using System.Timers;
using MySqlConnector;
using System.Data;
using System.Security.Cryptography;
using Server;
using System.IO;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;
using ProtoBuf;
using static Server.UserInfo;
using System.Linq.Expressions;
using System.Data.Common;

DataBase dB = new DataBase();

List<UserInfo> listUsers = new List<UserInfo>();

byte[] checkConnection = new byte[] { 0 };

string[] s = Server.Properties.Resources.ServerAddress.Split(',');

TcpListener server;

if (string.IsNullOrEmpty(s[0]))
{
    server = new TcpListener(IPAddress.Any, int.Parse(s[1]));
}
else
{
    server = new TcpListener(IPAddress.Parse(s[0]), int.Parse(s[1]));
}

try
{
    server.Start();
    Console.WriteLine("Сервер запущен.");
    while (true)
    {
        TcpClient client = await server.AcceptTcpClientAsync();
        new Task(async () => await ProcessClientAsync(client)).Start();
    }
}
catch (Exception ex)
{
    Console.WriteLine("Ошибка: " + ex.Message);
}

Task DisconnectUser(UserInfo user)
{
    var stream = user.tcpClient.GetStream();
    var binaryWriter = new BinaryWriter(stream);
    binaryWriter.Write("Procedure,Disconnect");
    listUsers.Remove(user);
    user.tcpClient.Close();
    return Task.CompletedTask;
}

Task ProcessClientAsync(TcpClient client)
{
    Console.WriteLine(((IPEndPoint)client.Client.RemoteEndPoint).Address + ": Подключился.");
    var stream = client.GetStream();
    var binaryReader = new BinaryReader(stream);
    var binaryWriter = new BinaryWriter(stream);
    UserInfo userInfo = new UserInfo();
    Timer timer = new Timer(3000);
    timer.Elapsed += checkClientConnection;
    timer.AutoReset= true;
    timer.Enabled = true;
    while (true)
    {
        binaryWriter.Flush();
        try
        {
            var request = binaryReader.ReadString();
            string[] dataString = request.Split(',');
            if (dataString[0] == "Procedure")
            {
                if (dataString[1] == "CheckUser")
                {
                    MySqlDataAdapter adapter = new MySqlDataAdapter();
                    DataTable table = new DataTable();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_login", MySqlDbType.VarChar).Value = dataString[2];
                        command.Parameters.Add("in_ip_address", MySqlDbType.VarChar).Value = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                        adapter.SelectCommand = command;
                        adapter.Fill(table);
                        StringBuilder sb = new StringBuilder();
                        if (table.Rows.Count == 1)
                        {
                            if (HashPass.VerifyHashedPassword((string)table.Rows[0]["password"], dataString[3]))
                            {
                                userInfo.id = (int)table.Rows[0]["iduser"];
                                foreach (var item in listUsers)
                                {
                                    if (item.id == userInfo.id)
                                    {
                                        new Task(async () => await DisconnectUser(item)).Start();
                                    }
                                }
                                userInfo.tcpClient = client;
                                listUsers.Add(userInfo);
                                userInfo.password = (string)table.Rows[0]["password"];
                                userInfo.login = (string)table.Rows[0]["login"];
                                if (table.Rows[0]["fullname"] != DBNull.Value)
                                    userInfo.fullname = ((string)table.Rows[0]["fullname"]).Split(' ');
                                userInfo.permission = (string)table.Rows[0]["permission"];
                                userInfo.persona = (string)table.Rows[0]["persona"];
                                userInfo.registerDate = (DateTime)table.Rows[0]["register_date"];
                                if (table.Rows[0]["img"] != DBNull.Value)
                                {
                                    userInfo.img = (byte[])table.Rows[0]["img"];
                                }
                                var streamObj = new MemoryStream();
                                Serializer.Serialize(streamObj, userInfo);
                                byte[] data = streamObj.ToArray();
                                binaryWriter.Write("Procedure,CheckUser,Success");
                                binaryWriter.Write(data.Length);
                                binaryWriter.Write(data);
                                Console.WriteLine(((IPEndPoint)client.Client.RemoteEndPoint).Address + ": Авторизовался.");
                                continue;
                            }
                            else
                            {
                                sb.Append("Procedure,CheckUser,ErrorPassword");
                                binaryWriter.Write(sb.ToString());
                                Console.WriteLine(((IPEndPoint)client.Client.RemoteEndPoint).Address + ": Неправильный пароль.");
                                continue;
                            }
                        }
                        else
                        {
                            sb.Append("Procedure,CheckUser,ErrorLogin");
                            binaryWriter.Write(sb.ToString());
                            Console.WriteLine(((IPEndPoint)client.Client.RemoteEndPoint).Address + ": Неправильное имя пользователя.");
                            continue;
                        }
                    }
                }
                if (dataString[1] == "RegisterUser")
                {
                    MySqlDataAdapter adapter = new MySqlDataAdapter();
                    DataTable table = new DataTable();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];   
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_login", MySqlDbType.VarChar).Value = dataString[2];
                        command.Parameters.Add("in_password", MySqlDbType.LongText).Value = HashPass.HashPassword(dataString[3]);
                        command.Parameters.Add("in_ip_address", MySqlDbType.VarChar).Value = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                        adapter.SelectCommand = command;
                        adapter.Fill(table);
                        StringBuilder sb = new StringBuilder();
                        if ((Int64)table.Rows[0][0] == 0)
                        {
                            sb.Append("Procedure,RegisterUser,Success");
                            binaryWriter.Write(sb.ToString());
                            Console.WriteLine(((IPEndPoint)client.Client.RemoteEndPoint).Address + ": Зарегистрировался.");
                            continue;
                        }
                        else
                        {
                            sb.Append("Procedure,RegisterUser,ErrorLogin");
                            binaryWriter.Write(sb.ToString());
                            Console.WriteLine(((IPEndPoint)client.Client.RemoteEndPoint).Address + ": Имя пользователя уже занято.");
                            continue;
                        }
                    }
                }
                if (dataString[1] == "ChangeData")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_fullname", MySqlDbType.VarChar).Value = dataString[2];
                        command.Parameters.Add("in_iduser", MySqlDbType.Int32).Value = userInfo.id;
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                        Console.WriteLine(((IPEndPoint)client.Client.RemoteEndPoint).Address + ": ФИО изменено.");
                    }
                    continue;
                }
                if (dataString[1] == "ChangePass")
                {
                    StringBuilder sb = new StringBuilder();
                    if (HashPass.VerifyHashedPassword(userInfo.password, dataString[2]))
                    {
                        sb.Append("Procedure,ChangePass,ErrorPassword");
                        binaryWriter.Write(sb.ToString());
                        Console.WriteLine(((IPEndPoint)client.Client.RemoteEndPoint).Address + ": Новый пароль совпадает со старым.");
                        continue;
                    }
                    else
                    {
                        userInfo.password = HashPass.HashPassword(dataString[2]);
                        using (MySqlCommand command = new MySqlCommand())
                        {
                            command.Connection = dB.GetConnection();
                            command.CommandText = dataString[1];
                            command.CommandType = CommandType.StoredProcedure;
                            command.Parameters.Add("in_pass", MySqlDbType.LongText).Value = userInfo.password;
                            command.Parameters.Add("in_iduser", MySqlDbType.Int32).Value = userInfo.id;
                            dB.openConnection();
                            command.ExecuteNonQuery();
                            dB.closeConnection();
                        }
                        sb.Append("Procedure,ChangePass,Success");
                        binaryWriter.Write(sb.ToString());
                        Console.WriteLine(((IPEndPoint)client.Client.RemoteEndPoint).Address + ": Пароль изменен.");
                        continue;
                    }
                }
                if (dataString[1] == "ChangeImg")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_iduser", MySqlDbType.Int32).Value = userInfo.id;
                        command.Parameters.Add("in_img", MySqlDbType.LongBlob).Value = binaryReader.ReadBytes(binaryReader.ReadInt32());
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                        Console.WriteLine(((IPEndPoint)client.Client.RemoteEndPoint).Address + ": Изображение профиля изменено.");
                    }
                    continue;
                }
                if (dataString[1] == "AddNote")
                {
                    var stream2 = new MemoryStream(binaryReader.ReadBytes(binaryReader.ReadInt32()));
                    Note note = Serializer.Deserialize<Note>(stream2);
                    MySqlDataAdapter adapter = new MySqlDataAdapter();
                    DataTable table = new DataTable();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = userInfo.id;
                        command.Parameters.Add("in_text", MySqlDbType.LongText).Value = note.text;
                        adapter.SelectCommand = command;
                        adapter.Fill(table);
                        foreach (var item in note.imgs)
                        {
                            using (MySqlCommand command2 = new MySqlCommand())
                            {
                                command2.Connection = dB.GetConnection();
                                command2.CommandText = "AddNoteImgs";
                                command2.CommandType = CommandType.StoredProcedure;
                                command2.Parameters.Add("in_id", MySqlDbType.Int32).Value = table.Rows[0][0];
                                command2.Parameters.Add("in_img", MySqlDbType.LongBlob).Value = item;
                                dB.openConnection();
                                command2.ExecuteNonQuery();
                                dB.closeConnection();
                            }
                        }
                    }
                    Console.WriteLine(((IPEndPoint)client.Client.RemoteEndPoint).Address + ": Запись добавлена.");
                    continue;
                }
                if (dataString[1] == "LoadProfile")
                {
                    userInfo.notes.Clear();
                    MySqlDataAdapter adapter = new MySqlDataAdapter();
                    DataTable table = new DataTable();
                    DataTable table2 = new DataTable();
                    DataTable table3 = new DataTable();
                    DataTable table4 = new DataTable();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = "Notes";
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = userInfo.id;
                        adapter.SelectCommand = command;
                        adapter.Fill(table);
                        using (MySqlCommand command2 = new MySqlCommand())
                        {
                            command2.Connection = dB.GetConnection();
                            command2.CommandText = "NoteImgs";
                            command2.CommandType = CommandType.StoredProcedure;
                            command2.Parameters.Add("in_id", MySqlDbType.Int32).Value = userInfo.id;
                            adapter.SelectCommand = command2;
                            adapter.Fill(table2);
                            using (MySqlCommand command3 = new MySqlCommand())
                            {
                                command3.Connection = dB.GetConnection();
                                command3.CommandText = "NoteComments";
                                command3.CommandType = CommandType.StoredProcedure;
                                command3.Parameters.Add("in_id", MySqlDbType.Int32).Value = userInfo.id;
                                adapter.SelectCommand = command3;
                                adapter.Fill(table3);
                                using (MySqlCommand command4 = new MySqlCommand())
                                {
                                    command4.Connection = dB.GetConnection();
                                    command4.CommandText = "LikedNotes";
                                    command4.CommandType = CommandType.StoredProcedure;
                                    command4.Parameters.Add("in_id", MySqlDbType.Int32).Value = userInfo.id;
                                    adapter.SelectCommand = command4;
                                    adapter.Fill(table4);
                                    foreach (DataRow item in table.Rows)
                                    {
                                        Notes note = new Notes();
                                        note.id = (int)item[0];
                                        if (item[1] != DBNull.Value)
                                            note.text = (string)item[1];
                                        note.likes = (int)item[2];
                                        note.createDate = (DateTime)item[3];
                                        userInfo.notes.Add(note);
                                    }
                                    foreach (DataRow item in table2.Rows)
                                    {
                                        foreach (var item2 in userInfo.notes)
                                        {
                                            if (item2.id == (int)item[0])
                                            {
                                                item2.imgs.Add((byte[])item[1]);
                                                break;
                                            }
                                        }
                                    }
                                    foreach (DataRow item in table3.Rows)
                                    {
                                        foreach (var item2 in userInfo.notes)
                                        {
                                            if (item2.id == (int)item[0])
                                            {
                                                item2.comments.Add(new UserInfo.Notes.Comments { text = (string)item[1], likes = (int)item[2], id = (int)item[3], fullname = (string)item[4], profilePicture = (byte[])item[5] });
                                                break;
                                            }
                                        }
                                    }
                                    foreach (DataRow item in table4.Rows)
                                    {
                                        foreach (var item2 in userInfo.notes)
                                        {
                                            if (item2.id == (int)item[0])
                                            {
                                                item2.like = true;
                                                break;
                                            }
                                        }
                                    }
                                    var streamObj = new MemoryStream();
                                    Serializer.Serialize(streamObj, userInfo.notes);
                                    byte[] data = streamObj.ToArray();
                                    binaryWriter.Write("Procedure,LoadProfile");
                                    binaryWriter.Write(data.Length);
                                    binaryWriter.Write(data);
                                }
                            }
                        }
                    }
                    continue;
                }
                if (dataString[1] == "AddLike")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = userInfo.id;
                        command.Parameters.Add("in_idNote", MySqlDbType.Int32).Value = int.Parse(dataString[2]);
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "RemoveLike")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = userInfo.id;
                        command.Parameters.Add("in_idNote", MySqlDbType.Int32).Value = int.Parse(dataString[2]);
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "DeleteNote")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = int.Parse(dataString[2]);
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "LoadChat")
                {
                    userInfo.favourites.Clear();
                    MySqlDataAdapter adapter = new MySqlDataAdapter();
                    DataTable table = new DataTable();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = "LoadFavourites";
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = userInfo.id;
                        adapter.SelectCommand = command;
                        adapter.Fill(table);
                        foreach (DataRow item in table.Rows)
                        {
                            Favourites friend = new Favourites();
                            friend.id = (int)item[0];
                            if (item[1] != DBNull.Value)
                                friend.img = (byte[])item[1];
                            friend.login = (string)item[2];
                            if (item[3] != DBNull.Value)
                                friend.fullname = ((string)item[3]).Split(' ');
                            friend.persona = (string)item[4];
                            foreach (UserInfo item2 in listUsers)
                            {
                                if (friend.id == item2.id)
                                {
                                    friend.status = true;
                                    break;
                                }
                            }
                            userInfo.favourites.Add(friend);
                        }
                        var streamObj = new MemoryStream();
                        Serializer.Serialize(streamObj, userInfo.favourites);
                        byte[] data = streamObj.ToArray();
                        binaryWriter.Write("Procedure,LoadChat");
                        binaryWriter.Write(data.Length);
                        binaryWriter.Write(data);
                    }
                    continue;
                }
                if (dataString[1] == "LoadUsers")
                {
                    Users users = new Users();
                    users.users = new List<Users.User>();
                    MySqlDataAdapter adapter = new MySqlDataAdapter();
                    DataTable table = new DataTable();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = userInfo.id;
                        adapter.SelectCommand = command;
                        adapter.Fill(table);
                        foreach (DataRow item in table.Rows)
                        {
                            Users.User user = new Users.User();
                            user.id = (int)item[0];
                            if (item[1] != DBNull.Value)
                                user.img = (byte[])item[1];
                            user.login = (string)item[2];
                            if (item[3] != DBNull.Value)
                                user.fullname = ((string)item[3]).Split(' ');
                            user.persona = (string)item[4];
                            user.permission = (string)item[5];
                            users.users.Add(user);
                        }
                        var streamObj = new MemoryStream();
                        Serializer.Serialize(streamObj, users);
                        byte[] data = streamObj.ToArray();
                        binaryWriter.Write("Procedure,LoadUsers");
                        binaryWriter.Write(data.Length);
                        binaryWriter.Write(data);
                    }
                    continue;
                }
                if (dataString[1] == "AddFavourites")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = userInfo.id;
                        command.Parameters.Add("in_idFavourites", MySqlDbType.Int32).Value = int.Parse(dataString[2]);
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "DeleteFavourites")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = userInfo.id;
                        command.Parameters.Add("in_idFavourites", MySqlDbType.Int32).Value = int.Parse(dataString[2]);
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "AddChat")
                {
                    MySqlDataAdapter adapter = new MySqlDataAdapter();
                    DataTable table = new DataTable();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_idUser", MySqlDbType.Int32).Value = userInfo.id;
                        command.Parameters.Add("in_idUser2", MySqlDbType.Int32).Value = int.Parse(dataString[2]);
                        adapter.SelectCommand = command;
                        adapter.Fill(table);
                        foreach (UserInfo item in listUsers)
                        {
                            if (item.id == int.Parse(dataString[2]) && item.id != userInfo.id)
                            {
                                Chat chat = new Chat();
                                chat.idUser = userInfo.id;
                                chat.nameUser = userInfo.fullname;
                                chat.loginUser = userInfo.login;
                                chat.imgUser = userInfo.img;
                                chat.check = false;
                                chat.id = int.Parse(table.Rows[0][0].ToString());
                                var stream2 = item.tcpClient.GetStream();
                                var binaryWriter2 = new BinaryWriter(stream2);
                                var streamObj = new MemoryStream();
                                item.chats.Add(chat);
                                Serializer.Serialize(streamObj, chat);
                                byte[] data = streamObj.ToArray();
                                binaryWriter2.Write("Module,NewChat");
                                binaryWriter2.Write(data.Length);
                                binaryWriter2.Write(data);
                                break;
                            }
                        }
                    }
                    continue;
                }
                if (dataString[1] == "LoadChatRooms")
                {
                    userInfo.chats.Clear();
                    MySqlDataAdapter adapter = new MySqlDataAdapter();
                    DataTable table = new DataTable();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = "LoadChats";
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = userInfo.id;
                        adapter.SelectCommand = command;
                        adapter.Fill(table);
                        foreach (DataRow item in table.Rows)
                        {
                            Chat chat = new Chat();
                            chat.id = (int)item[0];
                            chat.idUser = (int)item[1];
                            if (item[2] != DBNull.Value)
                                chat.imgUser = (byte[])item[2];
                            if (item[3] != DBNull.Value)
                                chat.nameUser = ((string)item[3]).Split(' ');
                            if (item[4] != DBNull.Value)
                                chat.loginUser = (string)item[4];
                            if ((sbyte)item[5] == 0)
                                chat.check = false;
                            else
                                chat.check = true;
                            userInfo.chats.Add(chat);
                        }
                        var streamObj = new MemoryStream();
                        Serializer.Serialize(streamObj, userInfo.chats);
                        byte[] data = streamObj.ToArray();
                        binaryWriter.Write("Procedure,LoadChatRooms");
                        binaryWriter.Write(data.Length);
                        binaryWriter.Write(data);
                    }
                }
                if (dataString[1] == "LoadMessages")
                {
                    MySqlDataAdapter adapter = new MySqlDataAdapter();
                    DataTable table = new DataTable();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = int.Parse(dataString[2]);
                        adapter.SelectCommand = command;
                        adapter.Fill(table);
                        foreach (Chat item in userInfo.chats)
                        {
                            if (item.id == int.Parse(dataString[2]))
                            {
                                item.chatMessages.Clear();
                                foreach (DataRow item2 in table.Rows)
                                {
                                    Chat.ChatMessages chatMessages = new Chat.ChatMessages();
                                    chatMessages.id = (int)item2[0];
                                    chatMessages.text = (string)item2[1];
                                    chatMessages.idAuthor = (int)item2[2];
                                    item.chatMessages.Add(chatMessages);
                                }
                                break;
                            }
                        }
                        var streamObj = new MemoryStream();
                        Serializer.Serialize(streamObj, userInfo.chats);
                        byte[] data = streamObj.ToArray();
                        binaryWriter.Write("Procedure,LoadMessages");
                        binaryWriter.Write(data.Length);
                        binaryWriter.Write(data);
                    }
                }
                if (dataString[1] == "AddMessage")
                {
                    string[] text = new string[dataString.Length - 3];
                    Array.Copy(dataString, 3, text, 0, dataString.Length - 3);
                    string messageText = String.Join(",", text);
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = int.Parse(dataString[2]);
                        command.Parameters.Add("in_idUser", MySqlDbType.Int32).Value = userInfo.id;
                        command.Parameters.Add("in_text", MySqlDbType.LongText).Value = messageText;
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                        Chat.ChatMessages message = new Chat.ChatMessages();
                        message.text = messageText;
                        message.idAuthor = userInfo.id;
                        foreach (UserInfo item in listUsers)
                        {
                            foreach (Chat item2 in item.chats)
                            {
                                if (item2.id == int.Parse(dataString[2]) && item.id != userInfo.id)
                                {
                                    var stream2 = item.tcpClient.GetStream();
                                    var binaryWriter2 = new BinaryWriter(stream2);
                                    var streamObj = new MemoryStream();
                                    Serializer.Serialize(streamObj, message);
                                    byte[] data = streamObj.ToArray();
                                    binaryWriter2.Write("Module,SendMessage");
                                    binaryWriter2.Write(data.Length);
                                    binaryWriter2.Write(data);
                                    binaryWriter2.Write(int.Parse(dataString[2]));
                                    break;
                                }
                            }
                        }
                    }
                    continue;
                }
                if (dataString[1] == "LoadNomenclatureTypes")
                {
                    DataTable table = new DataTable();
                    MySqlDataAdapter adapter = new MySqlDataAdapter();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        adapter.SelectCommand = command;
                        adapter.Fill(table);
                        List<ProductsAvailable.NomenclatureType> types = new List<ProductsAvailable.NomenclatureType>();
                        foreach (DataRow item in table.Rows)
                        {
                            ProductsAvailable.NomenclatureType type = new ProductsAvailable.NomenclatureType
                            {
                                id = (int)item[0],
                                name = (string)item[1]
                            };
                            types.Add(type);
                        }
                        var streamObj = new MemoryStream();
                        Serializer.Serialize(streamObj, types);
                        byte[] data = streamObj.ToArray();
                        binaryWriter.Write("Procedure,LoadNomenclatureTypes");
                        binaryWriter.Write(data.Length);
                        binaryWriter.Write(data);
                    }
                    continue;
                }
                if (dataString[1] == "RemoveNomenclatureTypes")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = int.Parse(dataString[2]);
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "AddNomenclatureTypes")
                {
                    DataTable table = new DataTable();
                    MySqlDataAdapter adapter = new MySqlDataAdapter();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_name", MySqlDbType.VarChar).Value = dataString[2];
                        adapter.SelectCommand = command;
                        adapter.Fill(table);
                        ProductsAvailable.NomenclatureType type = new ProductsAvailable.NomenclatureType();
                        type.name = dataString[2];
                        type.id = int.Parse(table.Rows[0][0].ToString());
                        foreach (var item in listUsers)
                        {
                            if (item.persona != "Пользователь" && item.id != userInfo.id)
                            {
                                var stream2 = item.tcpClient.GetStream();
                                var binaryWriter2 = new BinaryWriter(stream2);
                                var streamObj = new MemoryStream();
                                Serializer.Serialize(streamObj, type);
                                byte[] data = streamObj.ToArray();
                                binaryWriter2.Write("Module,NewNomenclatureType");
                                binaryWriter2.Write(data.Length);
                                binaryWriter2.Write(data);
                            }
                        }
                    }
                    continue;
                }
                if (dataString[1] == "LoadNomenclature")
                {
                    DataTable table = new DataTable();
                    MySqlDataAdapter adapter = new MySqlDataAdapter();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        adapter.SelectCommand = command;
                        adapter.Fill(table);
                        List<ProductsAvailable.Nomenclature> nomenclatures = new List<ProductsAvailable.Nomenclature>();
                        foreach (DataRow item in table.Rows)
                        {
                            ProductsAvailable.Nomenclature nomenclature = new ProductsAvailable.Nomenclature
                            {
                                id = (int)item[0],
                                name = (string)item[1],
                                type = (string)item[2],
                                articul = (string)item[3],
                                price = (double)item[4]
                            };
                            if (item[5] != DBNull.Value)
                                nomenclature.img = (byte[])item[5];
                            nomenclatures.Add(nomenclature);
                        }
                        var streamObj = new MemoryStream();
                        Serializer.Serialize(streamObj, nomenclatures);
                        byte[] data = streamObj.ToArray();
                        binaryWriter.Write("Procedure,LoadNomenclature");
                        binaryWriter.Write(data.Length);
                        binaryWriter.Write(data);
                    }
                    continue;
                }
                if (dataString[1] == "RemoveNomenclature")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = int.Parse(dataString[2]);
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "AddNomenclature")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_name", MySqlDbType.VarChar).Value = dataString[2];
                        command.Parameters.Add("in_type", MySqlDbType.VarChar).Value = dataString[3];
                        command.Parameters.Add("in_articul", MySqlDbType.VarChar).Value = dataString[4];
                        command.Parameters.Add("in_price", MySqlDbType.Double).Value = binaryReader.ReadDouble();
                        bool checkImg = binaryReader.ReadString() == "ImgYes";
                        if (checkImg)
                        {
                            command.Parameters.Add("in_img", MySqlDbType.LongBlob).Value = binaryReader.ReadBytes(binaryReader.ReadInt32());
                        }
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "LoadProducts")
                {
                    DataTable table = new DataTable();
                    MySqlDataAdapter adapter = new MySqlDataAdapter();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        adapter.SelectCommand = command;
                        adapter.Fill(table);
                        List<ProductsAvailable.Product> products = new List<ProductsAvailable.Product>();
                        foreach (DataRow item in table.Rows)
                        {
                            ProductsAvailable.Product product = new ProductsAvailable.Product
                            {
                                id = (int)item[0],
                                name = (string)item[1],
                                count = (int)item[2],
                                price = (double)item[3],
                                type = (string)item[4],
                                articul = (string)item[5]
                            };
                            if (item[6] != DBNull.Value)
                                product.img = (byte[])item[6];
                            products.Add(product);
                        }
                        var streamObj = new MemoryStream();
                        Serializer.Serialize(streamObj, products);
                        byte[] data = streamObj.ToArray();
                        binaryWriter.Write("Procedure,LoadProducts");
                        binaryWriter.Write(data.Length);
                        binaryWriter.Write(data);
                    }
                    continue;
                }
                if (dataString[1] == "RemoveProduct")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = int.Parse(dataString[2]);
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "UpdateProduct")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = int.Parse(dataString[2]);
                        command.Parameters.Add("in_count", MySqlDbType.Int32).Value = int.Parse(dataString[3]);
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "AddProduct")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_name", MySqlDbType.VarChar).Value = dataString[2];
                        command.Parameters.Add("in_count", MySqlDbType.Int32).Value = int.Parse(dataString[3]);
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "LoadOrders")
                {
                    DataTable table = new DataTable();
                    MySqlDataAdapter adapter = new MySqlDataAdapter();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        adapter.SelectCommand = command;
                        adapter.Fill(table);
                        List<Order> orders = new List<Order>();
                        foreach (DataRow item in table.Rows)
                        {
                            Order info = Serializer.Deserialize<Order>(new MemoryStream((byte[])item[2]));
                            Order order = new Order
                            {
                                id = (int)item[0],
                                userId = (int)item[1],
                                products = info.products,
                                status = (string)item[3],
                                date = (DateTime)item[4],
                                totalPrice = (double)item[5],
                                requisites = (string)item[6],
                                fullname = info.fullname
                            };
                            orders.Add(order);
                        }
                        var streamObj = new MemoryStream();
                        Serializer.Serialize(streamObj, orders);
                        byte[] data = streamObj.ToArray();
                        binaryWriter.Write("Procedure,LoadOrders");
                        binaryWriter.Write(data.Length);
                        binaryWriter.Write(data);
                    }
                    continue;
                }
                if (dataString[1] == "CreateOrder")
                {
                    var stream2 = new MemoryStream(binaryReader.ReadBytes(binaryReader.ReadInt32()));
                    Order order = Serializer.Deserialize<Order>(stream2);
                    order.userId = userInfo.id;
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = userInfo.id;
                        command.Parameters.Add("in_order", MySqlDbType.LongBlob).Value = stream2;
                        command.Parameters.Add("in_price", MySqlDbType.LongBlob).Value = order.totalPrice;
                        command.Parameters.Add("in_requisites", MySqlDbType.LongBlob).Value = order.requisites;
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    foreach (var item in order.products)
                    {
                        using (MySqlCommand command = new MySqlCommand())
                        {
                            command.Connection = dB.GetConnection();
                            command.CommandText = "BuyProduct";
                            command.CommandType = CommandType.StoredProcedure;
                            command.Parameters.Add("in_id", MySqlDbType.Int32).Value = item.id;
                            command.Parameters.Add("in_count", MySqlDbType.Int32).Value = item.buyCount;
                            dB.openConnection();
                            command.ExecuteNonQuery();
                            dB.closeConnection();
                        }
                    }
                    continue;
                }
                if (dataString[1] == "LoadInfoForAdmin")
                {
                    DataTable table = new DataTable();
                    DataTable table2 = new DataTable();
                    MySqlDataAdapter adapter = new MySqlDataAdapter();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = "LoadPermissions";
                        command.CommandType = CommandType.StoredProcedure;
                        adapter.SelectCommand = command;
                        adapter.Fill(table);
                        using (MySqlCommand command2 = new MySqlCommand())
                        {
                            command2.Connection = dB.GetConnection();
                            command2.CommandText = "LoadPersons";
                            command2.CommandType = CommandType.StoredProcedure;
                            adapter.SelectCommand = command2;
                            adapter.Fill(table2);
                            List<string> permissions = new List<string>();
                            List<string> persons = new List<string>();
                            foreach (DataRow item in table.Rows)
                            {
                                permissions.Add((string)item[0]);
                            }
                            foreach (DataRow item in table2.Rows)
                            {
                                persons.Add((string)item[0]);
                            }
                            var streamObj = new MemoryStream();
                            Serializer.Serialize(streamObj, permissions);
                            byte[] data = streamObj.ToArray();
                            binaryWriter.Write("Procedure,LoadInfoForAdmin");
                            binaryWriter.Write(data.Length);
                            binaryWriter.Write(data);
                            streamObj = new MemoryStream();
                            Serializer.Serialize(streamObj, persons);
                            data = streamObj.ToArray();
                            binaryWriter.Write(data.Length);
                            binaryWriter.Write(data);
                        }
                    }
                    continue;
                }
                if (dataString[1] == "RemovePermission")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_name", MySqlDbType.VarChar).Value = dataString[2];
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "RemovePerson")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_name", MySqlDbType.VarChar).Value = dataString[2];
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "AddPermission")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_name", MySqlDbType.VarChar).Value = dataString[2];
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "AddPerson")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_name", MySqlDbType.VarChar).Value = dataString[2];
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "ChangePermission")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_login", MySqlDbType.VarChar).Value = dataString[2];
                        command.Parameters.Add("in_permission", MySqlDbType.VarChar).Value = dataString[3];
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "ChangePerson")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_login", MySqlDbType.VarChar).Value = dataString[2];
                        command.Parameters.Add("in_person", MySqlDbType.VarChar).Value = dataString[3];
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "ChangeOrderStatus")
                {
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = dataString[2];
                        command.Parameters.Add("in_status", MySqlDbType.VarChar).Value = dataString[3];
                        dB.openConnection();
                        command.ExecuteNonQuery();
                        dB.closeConnection();
                    }
                    continue;
                }
                if (dataString[1] == "LoadMyOrders")
                {
                    MySqlDataAdapter adapter = new MySqlDataAdapter();
                    DataTable table = new DataTable();
                    using (MySqlCommand command = new MySqlCommand())
                    {
                        command.Connection = dB.GetConnection();
                        command.CommandText = dataString[1];
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("in_id", MySqlDbType.Int32).Value = userInfo.id;
                        adapter.SelectCommand = command;
                        adapter.Fill(table);
                        List<Order> myOrders = new List<Order>();
                        foreach (DataRow row in table.Rows)
                        {
                            myOrders.Add(new Order { id = (int)row[0], status = (string)row[3], date = (DateTime)row[4], totalPrice = (double)row[5], requisites = (string)row[6] });
                        }
                        var streamObj = new MemoryStream();
                        Serializer.Serialize(streamObj, myOrders);
                        byte[] data = streamObj.ToArray();
                        binaryWriter.Write("Procedure,LoadMyOrders");
                        binaryWriter.Write(data.Length);
                        binaryWriter.Write(data);
                    }
                }
            }
        }
        catch
        {
            if (!timer.Enabled)
                return Task.CompletedTask;
        }
    }

    void checkClientConnection(object? source, ElapsedEventArgs e)
    {
        try
        {
            binaryWriter.Write(checkConnection);
        }
        catch
        {
            listUsers.Remove(userInfo);
            Console.WriteLine(((IPEndPoint)client.Client.RemoteEndPoint).Address + ": Отключился.");
            client.Close();
            timer.Enabled = false;
        }
    }
}