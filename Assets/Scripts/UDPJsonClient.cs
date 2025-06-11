using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json.Linq;

public class UDPJsonClient : MonoBehaviour
{
    private UdpClient udpClient;
    private Thread receiveThread;
    private const int listenPort = 4211; // ESP32's target port
    private const int sendPort = 4210;   // ESP32 listens here
    private const string esp32IP = "192.168.4.1"; // Modify if needed
    private bool handshakeDone = false;

    void Start()
    {
        udpClient = new UdpClient(listenPort);
        udpClient.Client.ReceiveTimeout = 5000;

        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Debug.Log("UDP client started.");
    }

    void ReceiveData()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string message = Encoding.UTF8.GetString(data);
                Debug.Log($"Received: {message}");

                ParseJson(message);

                if (!handshakeDone)
                {
                    PerformHandshake(remoteEndPoint);
                }
            }
            catch (SocketException e)
            {
                Debug.Log($"Socket exception: {e.Message}");
            }
        }
    }

    void ParseJson(string json)
    {
        try
        {
            JObject doc = JObject.Parse(json);
            ParseArray(doc, "logs");
            ParseArray(doc, "bnoAngles");
            ParseArray(doc, "bnoPositions");
            ParseArray(doc, "motorPositions");
            ParseArray(doc, "infos");
            ParseArray(doc, "IPs");

            Debug.Log("Parsed JSON successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"JSON parsing failed: {e.Message}");
        }
    }

    void ParseArray(JObject doc, string key)
    {
        JArray array = (JArray)doc[key];
        if (array == null) return;

        foreach (var item in array)
        {
            int id = (int)item["ID"];
            string value = (string)item["value"];
            Debug.Log($"{key} ID {id}: {value}");
        }
    }

    void PerformHandshake(IPEndPoint target)
    {
        // Construct a handshake message
        var confirmation = new JObject();
        confirmation["message"] = "Connection confirmed";
        confirmation["type"] = "handshake";
        confirmation["IP"] = GetLocalIPAddress();

        string json = confirmation.ToString();
        byte[] data = Encoding.UTF8.GetBytes(json);

        udpClient.Send(data, data.Length, target);
        Debug.Log("Sent handshake message.");
        handshakeDone = true;
    }

    string GetLocalIPAddress()
    {
        string localIP = "";
        var host = Dns.GetHostEntry(Dns.GetHostName());

        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                localIP = ip.ToString();
                break;
            }
        }

        return localIP;
    }

    void OnApplicationQuit()
    {
        receiveThread?.Abort();
        udpClient?.Close();
    }
}
