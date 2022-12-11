using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;
using System;

public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;

    LinkedList<PlayerAccount> playerAccounts;

    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);
        playerAccounts = new LinkedList<PlayerAccount>();
        LoadPlayerManagementFile();

    }

    // Update is called once per frame
    void Update()
    {

        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                break;
        }

    }

    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }

    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);
        string[] csv = msg.Split(',');
        //catch signifier for differentiate the message type
        int signifier = int.Parse(csv[0]);
        //holding name during account create or login
        string n = "";
        //holding password during account create or login
        string p = "";
        //check the array length and then read the value
        if (csv.Length > 1)
            n = csv[1];
        if (csv.Length > 2)
        {
            p = csv[2];
        }
        //flag for checking duplicate user during user creation
        bool nameIsInUse = false;
        //flag for chekcing valid user during login
        bool validUser = false;
        try
        {
            if (signifier == ClientToServerSignifiers.CreateAccount)
            {
                Debug.Log("create account signifier detect");
                foreach (PlayerAccount pa in playerAccounts)
                {
                    if (pa.name == n)
                    {
                        nameIsInUse = true;
                        break;
                    }
                }
                if (nameIsInUse)
                {
                    SendMessageToClient(ServerToClientSignifiers.AccountCreationFailed + "," + n, id);
                }
                else
                {
                    PlayerAccount playerAccount = new PlayerAccount(id, n, p);
                    playerAccounts.AddLast(playerAccount);
                    Debug.Log("create success");
                    SendMessageToClient(ServerToClientSignifiers.AccountCreationComplete + "," + n, id);
                    SavePlayerManagementFile();
                }
            }
            else if (signifier == ClientToServerSignifiers.Login)
            {
                Debug.Log("login signifier detect");

                foreach (PlayerAccount pa in playerAccounts)
                {
                    if (pa.name == n && pa.password == p)
                    {
                        validUser = true;
                        Debug.Log("login success");
                        break;
                    }
                }
                if (validUser)
                {
                    SendMessageToClient(ServerToClientSignifiers.LoginComplete + "," + n, id);
                }
                else
                {
                    SendMessageToClient(ServerToClientSignifiers.LoginFailed + "," + n, id);
                }
            }

        }
        catch (Exception ex)
        {
            Debug.Log("error" + ex.Message);
        }
    }
    public void SavePlayerManagementFile()
    {
        StreamWriter sw = new StreamWriter(Application.dataPath + Path.DirectorySeparatorChar + "PlayerManagementFile.txt");
        foreach (PlayerAccount pa in playerAccounts)
        {
            sw.WriteLine(PlayerAccount.PlayerIDSinifier + "," + pa.id + "," + pa.name + "," + pa.password);
        }
        sw.Close();
    }

    public void LoadPlayerManagementFile()
    {
        if (File.Exists(Application.dataPath + Path.DirectorySeparatorChar + "PlayerManagementFile.txt"))
        {
            StreamReader sr = new StreamReader(Application.dataPath + Path.DirectorySeparatorChar + "PlayerManagementFile.txt");
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                string[] csv = line.Split(',');

                int signifier = int.Parse(csv[0]);
                if (signifier == PlayerAccount.PlayerIDSinifier)
                {
                    playerAccounts.AddLast(new PlayerAccount(int.Parse(csv[1]), csv[2], csv[3]));
                }
            }
        }
    }
    public static class ClientToServerSignifiers
    {
        public const int CreateAccount = 1;
        public const int Login = 2;
    }
    public static class ServerToClientSignifiers
    {
        public const int LoginComplete = 1;
        public const int LoginFailed = 2;
        public const int AccountCreationComplete = 3;
        public const int AccountCreationFailed = 4;
    }
    public class PlayerAccount
    {
        public const int PlayerIDSinifier = 1;
        public string name, password;
        public int id;
        public PlayerAccount(int i, string n, string p)
        {
            id = i;
            name = n;
            password = p;
        }

    }
}


