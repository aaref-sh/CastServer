using System;
using System.Collections.Generic;
using System.Text;

namespace StreamServer
{
    public partial class StreameServer
    {
        //Attribute
        private NF.TCPClient m_Client;
        private NF.TCPServer m_Server;
        private Configuration m_Config = new Configuration();
        private int m_SoundBufferCount = 8;
        private WinSound.Protocol m_PrototolClient = new WinSound.Protocol(WinSound.ProtocolTypes.LH, Encoding.Default);
        private Dictionary<NF.ServerThread, ServerThreadData> m_DictionaryServerDatas = new Dictionary<NF.ServerThread, ServerThreadData>();
        private WinSound.Recorder m_Recorder_Client;
        private WinSound.Recorder m_Recorder_Server;
        private WinSound.Player m_PlayerClient;
        private WinSound.JitterBuffer m_JitterBufferClientRecording;
        private WinSound.JitterBuffer m_JitterBufferClientPlaying;
        private WinSound.JitterBuffer m_JitterBufferServerRecording;
        private bool m_IsFormMain = true;
        private long m_SequenceNumber = 4596;
        private long m_TimeStamp = 0;
        private int m_Version = 2;
        private bool m_Padding = false;
        private bool m_Extension = false;
        private int m_CSRCCount = 0;
        private bool m_Marker = false;
        private int m_PayloadType = 0;
        private uint m_SourceId = 0;
        private WinSound.EventTimer m_TimerMixed = null;
        private uint m_Milliseconds = 20;
        private Object LockerDictionary = new Object();
        public static Dictionary<Object, Queue<List<Byte>>> DictionaryMixed = new Dictionary<Object, Queue<List<byte>>>();
        //private Encoding m_Encoding = Encoding.GetEncoding(1252);
        private const int RecordingJitterBufferCount = 8;

        string IpAddress = "192.168.1.111";
        int port = 22;

        public StreameServer(string ip, int port) { this.IpAddress = ip; this.port = port; }
        public void Init()
        {
            try
            {
                InitJitterBufferClientRecording();
                InitJitterBufferClientPlaying();
                InitJitterBufferServerRecording();
                InitProtocolClient();
            }
            catch (Exception)
            {
            }
        }
        public void disconnect()
        {
            try
            {
                m_IsFormMain = false;
                StopRecordingFromSounddevice_Server();
                StopServer();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public void ConnectToServer()
        {
            try
            {
                //Daten holen
                FormToConfig();

                if (IsServerRunning)
                {
                    StopServer();
                    StopRecordingFromSounddevice_Server();
                    StopTimerMixed();
                }
                else
                {
                    //Toggeln
                    m_Config.ServerNoSpeakAll = true;
                    ServerThreadData.IsMuteAll = m_Config.MuteServerListen;
                    StartServer();
                    StartTimerMixed();
                }
            }
            catch (Exception)
            {
            }
        }
        private void InitProtocolClient()
        {
            if (m_PrototolClient != null)
            {
                m_PrototolClient.DataComplete += new WinSound.Protocol.DelegateDataComplete(OnProtocolClient_DataComplete);
            }
        }
        private void OnTimerSendMixedDataToAllClients()
        {
            try
            {
                //Liste mit allen Sprachdaten (eigene + Clients)
                Dictionary<Object, List<Byte>> dic = new Dictionary<object, List<byte>>();
                List<List<byte>> listlist = new List<List<byte>>();
                Dictionary<Object, Queue<List<Byte>>> copy = new Dictionary<object, Queue<List<byte>>>(StreameServer.DictionaryMixed);
                {
                    Queue<List<byte>> q = null;
                    foreach (Object obj in copy.Keys)
                    {

                        q = copy[obj];

                        //Wenn Daten vorhanden
                        if (q.Count > 0)
                        {
                            dic[obj] = q.Dequeue();
                            listlist.Add(dic[obj]);
                        }
                    }
                }

                if (listlist.Count > 0)
                {
                    //Gemischte Sprachdaten
                    Byte[] mixedBytes = WinSound.Mixer.MixBytes(listlist, m_Config.BitsPerSampleServer).ToArray();
                    List<Byte> listMixed = new List<Byte>(mixedBytes);

                    //Für alle Clients
                    foreach (NF.ServerThread client in m_Server.Clients)
                    {
                        //Wenn nicht stumm
                        if (client.IsMute == false)
                        {
                            //Gemixte Sprache für Client
                            Byte[] mixedBytesClient = mixedBytes;

                            if (dic.ContainsKey(client))
                            {
                                //Sprache des Clients ermitteln
                                List<Byte> listClient = dic[client];

                                //Sprache des Clients aus Mix subtrahieren
                                mixedBytesClient = WinSound.Mixer.SubsctractBytes_16Bit(listMixed, listClient).ToArray();
                            }

                            //RTP Packet erstellen
                            WinSound.RTPPacket rtp = ToRTPPacket(mixedBytesClient, m_Config.BitsPerSampleServer, m_Config.ChannelsServer);
                            Byte[] rtpBytes = rtp.ToBytes();

                            //Absenden
                            client.Send(m_PrototolClient.ToBytes(rtpBytes));
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine(String.Format("FormMain.cs | OnTimerSendMixedDataToAllClients() | {0}", ex.Message));
            }
        }
        private void InitJitterBufferClientRecording()
        {
            //Wenn vorhanden
            if (m_JitterBufferClientRecording != null)
            {
                m_JitterBufferClientRecording.DataAvailable -= new WinSound.JitterBuffer.DelegateDataAvailable(OnJitterBufferClientDataAvailableRecording);
            }

            //Neu erstellen
            m_JitterBufferClientRecording = new WinSound.JitterBuffer(null, RecordingJitterBufferCount, 20);
            m_JitterBufferClientRecording.DataAvailable += new WinSound.JitterBuffer.DelegateDataAvailable(OnJitterBufferClientDataAvailableRecording);
        }
        private void InitJitterBufferClientPlaying()
        {
            //Wenn vorhanden
            if (m_JitterBufferClientPlaying != null)
            {
                m_JitterBufferClientPlaying.DataAvailable -= new WinSound.JitterBuffer.DelegateDataAvailable(OnJitterBufferClientDataAvailablePlaying);
            }

            //Neu erstellen
            m_JitterBufferClientPlaying = new WinSound.JitterBuffer(null, m_Config.JitterBufferCountClient, 20);
            m_JitterBufferClientPlaying.DataAvailable += new WinSound.JitterBuffer.DelegateDataAvailable(OnJitterBufferClientDataAvailablePlaying);
        }
        private void InitJitterBufferServerRecording()
        {
            //Wenn vorhanden
            if (m_JitterBufferServerRecording != null)
            {
                m_JitterBufferServerRecording.DataAvailable -= new WinSound.JitterBuffer.DelegateDataAvailable(OnJitterBufferServerDataAvailable);
            }

            //Neu erstellen
            m_JitterBufferServerRecording = new WinSound.JitterBuffer(null, RecordingJitterBufferCount, 20);
            m_JitterBufferServerRecording.DataAvailable += new WinSound.JitterBuffer.DelegateDataAvailable(OnJitterBufferServerDataAvailable);
        }
        private void StopRecordingFromSounddevice_Server()
        {
            try
            {
                if (IsRecorderFromSounddeviceStarted_Server)
                {
                    //Stoppen
                    m_Recorder_Server.Stop();

                    //Events entfernen
                    m_Recorder_Server.DataRecorded -= new WinSound.Recorder.DelegateDataRecorded(OnDataReceivedFromSoundcard_Server);
                    m_Recorder_Server = null;

                    //JitterBuffer beenden
                    m_JitterBufferServerRecording.Stop();

                }
            }
            catch (Exception)
            {
            }
        }
        private void OnDataReceivedFromSoundcard_Server(Byte[] data)
        {
            try
            {
                lock (this)
                {
                    if (IsServerRunning)
                    {
                        //Wenn Form noch aktiv
                        if (m_IsFormMain)
                        {
                            //Wenn gewünscht
                            if (m_Config.ServerNoSpeakAll == false)
                            {
                                //Sounddaten in kleinere Einzelteile zerlegen
                                int bytesPerInterval = WinSound.Utils.GetBytesPerInterval((uint)m_Config.SamplesPerSecondServer, m_Config.BitsPerSampleServer, m_Config.ChannelsServer);
                                int count = data.Length / bytesPerInterval;
                                int currentPos = 0;
                                for (int i = 0; i < count; i++)
                                {
                                    //Teilstück in RTP Packet umwandeln
                                    Byte[] partBytes = new Byte[bytesPerInterval];
                                    Array.Copy(data, currentPos, partBytes, 0, bytesPerInterval);
                                    currentPos += bytesPerInterval;

                                    //Wenn Buffer nicht zu gross
                                    Queue<List<Byte>> q = StreameServer.DictionaryMixed[this];
                                    if (q.Count < 10)
                                    {
                                        //Daten In Mixer legen
                                        q.Enqueue(new List<Byte>(partBytes));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }
        private void OnJitterBufferClientDataAvailableRecording(Object sender, WinSound.RTPPacket rtp)
        {
            try
            {
                //Prüfen
                if (rtp != null && m_Client != null && rtp.Data != null && rtp.Data.Length > 0)
                {
                    if (IsClientConnected)
                    {
                        if (m_IsFormMain)
                        {
                            //RTP Packet in Bytes umwandeln
                            Byte[] rtpBytes = rtp.ToBytes();
                            //Absenden
                            m_Client.Send(m_PrototolClient.ToBytes(rtpBytes));
                        }
                    }
                }
            }
            catch (Exception)
            {
                System.Diagnostics.StackFrame sf = new System.Diagnostics.StackFrame(true);
            }
        }
        private void OnJitterBufferClientDataAvailablePlaying(Object sender, WinSound.RTPPacket rtp)
        {
            try
            {
                if (m_PlayerClient != null)
                {
                    if (m_PlayerClient.Opened)
                    {
                        if (m_IsFormMain)
                        {
                            //Wenn nicht stumm
                            if (m_Config.MuteClientPlaying == false)
                            {
                                //Nach Linear umwandeln
                                Byte[] linearBytes = WinSound.Utils.MuLawToLinear(rtp.Data, m_Config.BitsPerSampleClient, m_Config.ChannelsClient);
                                //Abspielen
                                m_PlayerClient.PlayData(linearBytes, false);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                System.Diagnostics.StackFrame sf = new System.Diagnostics.StackFrame(true);
            }
        }
        private void OnJitterBufferServerDataAvailable(Object sender, WinSound.RTPPacket rtp)
        {
            try
            {
                if (IsServerRunning)
                {
                    if (m_IsFormMain)
                    {
                        //RTP Packet in Bytes umwandeln
                        Byte[] rtpBytes = rtp.ToBytes();

                        //Für alle Clients
                        List<NF.ServerThread> list = new List<NF.ServerThread>(m_Server.Clients);
                        foreach (NF.ServerThread client in list)
                        {
                            //Wenn nicht Mute
                            if (client.IsMute == false)
                            {
                                try
                                {
                                    //Absenden
                                    client.Send(m_PrototolClient.ToBytes(rtpBytes));
                                }
                                catch (Exception)
                                {
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                _ = new System.Diagnostics.StackFrame(true);
            }
        }
        private WinSound.RTPPacket ToRTPPacket(Byte[] linearData, int bitsPerSample, int channels)
        {
            //Daten Nach MuLaw umwandeln
            Byte[] mulaws = WinSound.Utils.LinearToMulaw(linearData, bitsPerSample, channels);

            //Neues RTP Packet erstellen
            WinSound.RTPPacket rtp = new WinSound.RTPPacket();

            //Werte übernehmen
            rtp.Data = mulaws;
            rtp.CSRCCount = m_CSRCCount;
            rtp.Extension = m_Extension;
            rtp.HeaderLength = WinSound.RTPPacket.MinHeaderLength;
            rtp.Marker = m_Marker;
            rtp.Padding = m_Padding;
            rtp.PayloadType = m_PayloadType;
            rtp.Version = m_Version;
            rtp.SourceId = m_SourceId;

            //RTP Header aktualisieren
            try
            {
                rtp.SequenceNumber = Convert.ToUInt16(m_SequenceNumber);
                m_SequenceNumber++;
            }
            catch (Exception)
            {
                m_SequenceNumber = 0;
            }
            try
            {
                rtp.Timestamp = Convert.ToUInt32(m_TimeStamp);
                m_TimeStamp += mulaws.Length;
            }
            catch (Exception)
            {
                m_TimeStamp = 0;
            }

            //Fertig
            return rtp;
        }
        private bool IsRecorderFromSounddeviceStarted_Server
        {
            get
            {
                if (m_Recorder_Server != null)
                {
                    return m_Recorder_Server.Started;
                }
                return false;
            }
        }
        private void StartServer()
        {
            try
            {
                if (IsServerRunning == false)
                {
                    if (m_Config.IPAddressServer.Length > 0 && m_Config.PortServer > 0)
                    {
                        m_Server = new NF.TCPServer();
                        m_Server.ClientConnected += new NF.TCPServer.DelegateClientConnected(OnServerClientConnected);
                        m_Server.ClientDisconnected += new NF.TCPServer.DelegateClientDisconnected(OnServerClientDisconnected);
                        m_Server.DataReceived += new NF.TCPServer.DelegateDataReceived(OnServerDataReceived);
                        m_Server.Start(m_Config.IPAddressServer, m_Config.PortServer);

                    }
                }
            }
            catch (Exception)
            {
            }
        }
        private void StopServer()
        {
            try
            {
                if (IsServerRunning == true)
                {

                    //Player beenden
                    DeleteAllServerThreadDatas();

                    //Server beenden
                    m_Server.Stop();
                    m_Server.ClientConnected -= new NF.TCPServer.DelegateClientConnected(OnServerClientConnected);
                    m_Server.ClientDisconnected -= new NF.TCPServer.DelegateClientDisconnected(OnServerClientDisconnected);
                    m_Server.DataReceived -= new NF.TCPServer.DelegateDataReceived(OnServerDataReceived);
                }

                //Fertig
                m_Server = null;
            }
            catch (Exception)
            {
            }
        }
        private void OnProtocolClient_DataComplete(Object sender, Byte[] data)
        {
            try
            {
                //Wenn der Player gestartet wurde
                if (m_PlayerClient != null)
                {
                    if (m_PlayerClient.Opened)
                    {
                        //RTP Header auslesen
                        WinSound.RTPPacket rtp = new WinSound.RTPPacket(data);

                        //Wenn Header korrekt
                        if (rtp.Data != null)
                        {
                            //In JitterBuffer hinzufügen
                            if (m_JitterBufferClientPlaying != null)
                            {
                                m_JitterBufferClientPlaying.AddData(rtp);
                            }
                        }
                    }
                }
                else
                {
                    //Konfigurationsdaten erhalten
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private void OnServerClientConnected(NF.ServerThread st)
        {
            try
            {
                //ServerThread Daten erstellen
                ServerThreadData data = new ServerThreadData();
                //Initialisieren
                data.Init(st, m_Config.SoundOutputDeviceNameServer, m_Config.SamplesPerSecondServer, m_Config.BitsPerSampleServer, m_Config.ChannelsServer, m_SoundBufferCount, m_Config.JitterBufferCountServer, m_Milliseconds);
                //Hinzufügen
                m_DictionaryServerDatas[st] = data;

                //Konfiguration senden
                SendConfigurationToClient(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private void SendConfigurationToClient(ServerThreadData data)
        {
            Byte[] bytesConfig = { 25, 9, 7 };
            data.ServerThread.Send(m_PrototolClient.ToBytes(bytesConfig));
        }
        private void OnServerClientDisconnected(NF.ServerThread st, string info)
        {
            try
            {
                //Wenn vorhanden
                if (m_DictionaryServerDatas.ContainsKey(st))
                {
                    //Alle Daten freigeben
                    ServerThreadData data = m_DictionaryServerDatas[st];
                    data.Dispose();
                    lock (LockerDictionary)
                    {
                        //Entfernen
                        m_DictionaryServerDatas.Remove(st);
                    }
                }
                //Aus Mixdaten entfernen
                StreameServer.DictionaryMixed.Remove(st);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private void StartTimerMixed()
        {
            if (m_TimerMixed == null)
            {
                m_TimerMixed = new WinSound.EventTimer();
                m_TimerMixed.TimerTick += new WinSound.EventTimer.DelegateTimerTick(OnTimerSendMixedDataToAllClients);
                m_TimerMixed.Start(20, 0);
            }
        }
        private void StopTimerMixed()
        {
            if (m_TimerMixed != null)
            {
                m_TimerMixed.Stop();
                m_TimerMixed.TimerTick -= new WinSound.EventTimer.DelegateTimerTick(OnTimerSendMixedDataToAllClients);
                m_TimerMixed = null;
            }
        }
        private void OnServerDataReceived(NF.ServerThread st, Byte[] data)
        {
            //Wenn vorhanden
            if (m_DictionaryServerDatas.ContainsKey(st))
            {
                //Wenn Protocol
                ServerThreadData stData = m_DictionaryServerDatas[st];
                if (stData.Protocol != null)
                {
                    stData.Protocol.Receive_LH(st, data);
                }
            }
        }
        private void DeleteAllServerThreadDatas()
        {
            lock (LockerDictionary)
            {
                try
                {
                    foreach (ServerThreadData info in m_DictionaryServerDatas.Values)
                    {
                        info.Dispose();
                    }
                    m_DictionaryServerDatas.Clear();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
        private bool IsServerRunning
        {
            get
            {
                if (m_Server != null)
                {
                    return m_Server.State == NF.TCPServer.ListenerState.Started;
                }
                return false;
            }
        }
        private bool IsClientConnected
        {
            get
            {
                if (m_Client != null)
                {
                    return m_Client.Connected;
                }
                return false;
            }
        }
        private bool FormToConfig()
        {
            try
            {
                m_Config.IPAddressServer = IpAddress;
                m_Config.PortServer = port;
                m_Config.JitterBufferCountServer = (uint)22;
                m_Config.SamplesPerSecondServer = 8000;
                m_Config.BitsPerSampleServer = 16;
                m_Config.BitsPerSampleClient = 16;
                m_Config.ChannelsServer = 1;
                m_Config.ChannelsClient = 1;
                m_Config.UseJitterBufferClientRecording = true;
                m_Config.UseJitterBufferServerRecording = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ButtonServer_Click(object sender, EventArgs e) => ConnectToServer();

    }
    /// <summary>
    /// Config
    /// </summary>
    public class Configuration
    {
        /// <summary>
        /// Config
        /// </summary>
        public Configuration()
        {

        }

        //Attribute
        public String IpAddressClient = "";
        public String IPAddressServer = "";
        public int PortClient = 0;
        public int PortServer = 0;
        public String SoundInputDeviceNameClient = "";
        public String SoundOutputDeviceNameClient = "";
        public String SoundInputDeviceNameServer = "";
        public String SoundOutputDeviceNameServer = "";
        public int SamplesPerSecondClient = 8000;
        public int BitsPerSampleClient = 16;
        public int ChannelsClient = 1;
        public int SamplesPerSecondServer = 8000;
        public int BitsPerSampleServer = 16;
        public int ChannelsServer = 1;
        public bool UseJitterBufferClientRecording = true;
        public bool UseJitterBufferServerRecording = true;
        public uint JitterBufferCountServer = 20;
        public uint JitterBufferCountClient = 20;
        public string FileName = "";
        public bool LoopFile = false;
        public bool MuteClientPlaying = false;
        public bool ServerNoSpeakAll = false;
        public bool ClientNoSpeakAll = false;
        public bool MuteServerListen = true;
    }
    /// <summary>
    /// ServerThreadData
    /// </summary>
    public class ServerThreadData
    {
        /// <summary>
        /// Konstruktor
        /// </summary>
        public ServerThreadData()
        {

        }

        //Attribute
        public NF.ServerThread ServerThread;
        public WinSound.Player Player;
        public WinSound.JitterBuffer JitterBuffer;
        public WinSound.Protocol Protocol;
        public int SamplesPerSecond = 8000;
        public int BitsPerSample = 16;
        public int SoundBufferCount = 8;
        public uint JitterBufferCount = 20;
        public uint JitterBufferMilliseconds = 20;
        public int Channels = 1;
        private bool IsInitialized = false;
        public bool IsMute = false;
        public static bool IsMuteAll = false;

        /// <summary>
        /// Init
        /// </summary>
        /// <param name="bitsPerSample"></param>
        /// <param name="channels"></param>
        public void Init(NF.ServerThread st, string soundDeviceName, int samplesPerSecond, int bitsPerSample, int channels, int soundBufferCount, uint jitterBufferCount, uint jitterBufferMilliseconds)
        {
            //Werte übernehmen
            this.ServerThread = st;
            this.SamplesPerSecond = samplesPerSecond;
            this.BitsPerSample = bitsPerSample;
            this.Channels = channels;
            this.SoundBufferCount = soundBufferCount;
            this.JitterBufferCount = jitterBufferCount;
            this.JitterBufferMilliseconds = jitterBufferMilliseconds;

            //Player
            this.Player = new WinSound.Player();
            this.Player.Open(soundDeviceName, samplesPerSecond, bitsPerSample, channels, soundBufferCount);

            //Wenn ein JitterBuffer verwendet werden soll
            if (jitterBufferCount >= 2)
            {
                //Neuen JitterBuffer erstellen
                this.JitterBuffer = new WinSound.JitterBuffer(st, jitterBufferCount, jitterBufferMilliseconds);
                this.JitterBuffer.DataAvailable += new WinSound.JitterBuffer.DelegateDataAvailable(OnJitterBufferDataAvailable);
                this.JitterBuffer.Start();
            }

            //Protocol
            this.Protocol = new WinSound.Protocol(WinSound.ProtocolTypes.LH, Encoding.Default);
            this.Protocol.DataComplete += new WinSound.Protocol.DelegateDataComplete(OnProtocolDataComplete);

            //Zu Mixer hinzufügen
            StreameServer.DictionaryMixed[st] = new Queue<List<byte>>();

            //Initialisiert
            IsInitialized = true;
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            //Protocol
            if (Protocol != null)
            {
                this.Protocol.DataComplete -= new WinSound.Protocol.DelegateDataComplete(OnProtocolDataComplete);
                this.Protocol = null;
            }

            //JitterBuffer
            if (JitterBuffer != null)
            {
                JitterBuffer.Stop();
                JitterBuffer.DataAvailable -= new WinSound.JitterBuffer.DelegateDataAvailable(OnJitterBufferDataAvailable);
                this.JitterBuffer = null;
            }

            //Player
            if (Player != null)
            {
                Player.Close();
                this.Player = null;
            }

            //Nicht initialisiert
            IsInitialized = false;
        }
        /// <summary>
        /// OnProtocolDataComplete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        private void OnProtocolDataComplete(Object sender, Byte[] bytes)
        {
            //Wenn initialisiert
            if (IsInitialized)
            {
                if (ServerThread != null && Player != null)
                {
                    try
                    {
                        //Wenn der Player gestartet wurde
                        if (Player.Opened)
                        {

                            //RTP Header auslesen
                            WinSound.RTPPacket rtp = new WinSound.RTPPacket(bytes);

                            //Wenn Header korrekt
                            if (rtp.Data != null)
                            {
                                //Wenn JitterBuffer verwendet werden soll
                                if (JitterBuffer != null && JitterBuffer.Maximum >= 2)
                                {
                                    JitterBuffer.AddData(rtp);
                                }
                                else
                                {
                                    //Wenn kein Mute
                                    if (IsMuteAll == false && IsMute == false)
                                    {
                                        //Nach Linear umwandeln
                                        Byte[] linearBytes = WinSound.Utils.MuLawToLinear(rtp.Data, this.BitsPerSample, this.Channels);
                                        //Abspielen
                                        Player.PlayData(linearBytes, false);
                                    }
                                }
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        IsInitialized = false;
                    }
                }
            }
        }
        /// <summary>
        /// OnJitterBufferDataAvailable
        /// </summary>
        /// <param name="packet"></param>
        private void OnJitterBufferDataAvailable(Object sender, WinSound.RTPPacket rtp)
        {
            try
            {
                if (Player != null)
                {
                    //Nach Linear umwandeln
                    Byte[] linearBytes = WinSound.Utils.MuLawToLinear(rtp.Data, BitsPerSample, Channels);

                    //Wenn kein Mute
                    if (IsMuteAll == false && IsMute == false)
                    {
                        //Abspielen
                        Player.PlayData(linearBytes, false);
                    }

                    //Wenn Buffer nicht zu gross
                    Queue<List<Byte>> q = StreameServer.DictionaryMixed[sender];
                    if (q.Count < 10)
                    {
                        //Daten Zu Mixer hinzufügen
                        StreameServer.DictionaryMixed[sender].Enqueue(new List<Byte>(linearBytes));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("FormMain.cs | OnJitterBufferDataAvailable() | {0}", ex.Message));
            }
        }
    }
}
