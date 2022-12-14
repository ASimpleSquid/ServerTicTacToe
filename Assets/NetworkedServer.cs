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
    #region chat room variable
    int chatterWaitingID = -1;
    string chatterWaitingIDN = "";
    int chatterWaitingID2 = -1;
    string chatterWaitingIDN2 = "";
    #endregion

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
        int signifier = int.Parse(csv[0]);
        string n = "";
        string p = "";
        if (csv.Length > 1)
            n = csv[1];
        if (csv.Length > 2)
        {
            p = csv[2];
        }
        bool nameIsInUse = false;
        bool validUser = false;
        try
        {
            if ((ClientToServerSignifiers)signifier == ClientToServerSignifiers.CreateAccount)
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
                    SendMessageToClient((int)ServerToClientSignifiers.AccountCreationFailed + "," + n, id);
                }
                else
                {
                    PlayerAccount playerAccount = new PlayerAccount(id, n, p);
                    playerAccounts.AddLast(playerAccount);
                    Debug.Log("create success");
                    SendMessageToClient((int)ServerToClientSignifiers.AccountCreationComplete + "," + n, id);
                    SavePlayerManagementFile();
                }
            }
            else if ((ClientToServerSignifiers)signifier == ClientToServerSignifiers.Login)
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
                    SendMessageToClient((int)ServerToClientSignifiers.LoginComplete + "," + n, id);
                }
                else
                {
                    SendMessageToClient((int)ServerToClientSignifiers.LoginFailed + "," + n, id);
                }
            }
            else if((ClientToServerSignifiers)signifier == ClientToServerSignifiers.SendMessage)
            {
                foreach (PlayerAccount P in playerAccounts)
                {
                    //Debug.Log($"Relay Message to player ID : {P.id}");
                    SendMessageToClient($"{(int)ServerToClientSignifiers.RecievedMessage},{csv[1]},{csv[2]}", P.id);
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
    public void AppendLogFile(string line)
    {
        StreamWriter sw = new StreamWriter(Application.dataPath + Path.DirectorySeparatorChar + "Log.txt", true);

        sw.WriteLine(System.DateTime.Now.ToString("yyyyMMdd HHmmss") + ": " + line);

        sw.Close();
    }
    public enum ClientToServerSignifiers
    {
        CreateAccount = 1,
        Login,
        SendMessage,
        PlayerAttemptJoin,
        ObserverJoin,
        LeaveGame,
        GameMove
    }
    public enum ServerToClientSignifiers
    {
        LoginComplete = 1,
        LoginFailed,
        AccountCreationComplete,
        AccountCreationFailed,
        RecievedMessage
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
    public class TicTacToe
    {
        const int outOfTurnError = 1;
        const int turnSuccess = 0;
        string gameState = ".........";

        static readonly int[][] winningCombos = new int[][]
        {
                new int [] {0, 1, 2},
                new int [] {3, 4, 5},
                new int [] {6, 7, 8},
                new int [] {0, 3, 6},
                new int [] {1, 4, 7},
                new int [] {2, 5, 8},
                new int [] {0, 4, 8},
                new int [] {2, 4, 6}
        };

        char turn = 'x';
        PlayerAccount playerX;
        PlayerAccount playerO;

        public int AttemptMove(int pos, int id)
        {
            if (turn == 'x' && id != playerX.id || turn == 'o' && id != playerO.id) return outOfTurnError;
            char[] gameStateArr = gameState.ToCharArray();
            gameStateArr[pos] = turn;
            gameState = new String(gameStateArr);
            return turnSuccess;
        }
        public void ChekcState()
        {
            foreach (int[] wc in winningCombos)
            {
                char c = gameState[wc[0]];
                if (c != 'x' && c != 'o') continue;

            }

            return '_';
        }
    }

}


