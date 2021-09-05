using System;
using System.Collections.Generic;
using System.Text;

namespace VoiceClient
{
    public partial class StreamClient
	{
		#region vars
		//Attribute
		private NF.TCPClient m_Client;
		private NF.TCPServer m_Server;
		private Configuration m_Config = new Configuration();
		private int m_SoundBufferCount = 8;
		private WinSound.Protocol m_PrototolClient = new WinSound.Protocol(WinSound.ProtocolTypes.LH, Encoding.Default);
		private WinSound.Recorder m_Recorder_Client;
		private WinSound.Player m_PlayerClient;
		private uint m_RecorderFactor = 4;
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
		public static Dictionary<Object, Queue<List<Byte>>> DictionaryMixed = new Dictionary<Object, Queue<List<byte>>>();
		private const int RecordingJitterBufferCount = 8;
		List<String> playbackNames;
		List<String> recordingNames;
		string IPAddress = "192.168.1.111";
		int connection_port = 22;
		#endregion
		public StreamClient(int port, string ip)
		{
			connection_port = port;
			IPAddress = ip;
		}
		public void Init()
		{
			try
			{
				//CreateHandle();
				InitComboboxesClient();
				InitJitterBufferClientRecording();
				InitJitterBufferClientPlaying();
				InitJitterBufferServerRecording();
				InitProtocolClient();
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
		private bool UseJitterBufferClientRecording
		{
			get
			{
				return m_Config.UseJitterBufferClientRecording;
			}
		}
		private void StartRecordingFromSounddevice_Client()
		{
			try
			{
				if (IsRecorderFromSounddeviceStarted_Client == false)
				{
					//Buffer Grösse berechnen
					int bufferSize = 0;
					if (UseJitterBufferClientRecording)
					{
						bufferSize = WinSound.Utils.GetBytesPerInterval((uint)m_Config.SamplesPerSecondClient, m_Config.BitsPerSampleClient, m_Config.ChannelsClient) * (int)m_RecorderFactor;
					}
					else
					{
						bufferSize = WinSound.Utils.GetBytesPerInterval((uint)m_Config.SamplesPerSecondClient, m_Config.BitsPerSampleClient, m_Config.ChannelsClient);
					}

					//Wenn Buffer korrekt
					if (bufferSize > 0)
					{
						//Recorder erstellen
						m_Recorder_Client = new WinSound.Recorder();

						//Events hinzufügen
						m_Recorder_Client.DataRecorded += new WinSound.Recorder.DelegateDataRecorded(OnDataReceivedFromSoundcard_Client);

						//Recorder starten
						if (m_Recorder_Client.Start(m_Config.SoundInputDeviceNameClient, m_Config.SamplesPerSecondClient, m_Config.BitsPerSampleClient, m_Config.ChannelsClient, m_SoundBufferCount, bufferSize))
						{

							//Wenn JitterBuffer
							if (UseJitterBufferClientRecording)
							{
								m_JitterBufferClientRecording.Start();
							}
						}
					}

				}
			}
			catch (Exception)
			{

			}
		}
		private void StopRecordingFromSounddevice_Client()
		{
			try
			{
				if (IsRecorderFromSounddeviceStarted_Client)
				{
					//Stoppen
					m_Recorder_Client.Stop();

					//Events entfernen
					m_Recorder_Client.DataRecorded -= new WinSound.Recorder.DelegateDataRecorded(OnDataReceivedFromSoundcard_Client);
					m_Recorder_Client = null;

					//Wenn JitterBuffer
					if (UseJitterBufferClientRecording)
					{
						m_JitterBufferClientRecording.Stop();
					}

				}
			}
			catch (Exception)
			{

			}
		}
		private void OnDataReceivedFromSoundcard_Client(Byte[] data)
		{
			try
			{
				lock (this)
				{
					if (IsClientConnected)
					{
						//Wenn gewünscht
						if (m_Config.ClientNoSpeakAll == false)
						{
							//Sounddaten in kleinere Einzelteile zerlegen
							int bytesPerInterval = WinSound.Utils.GetBytesPerInterval((uint)m_Config.SamplesPerSecondClient, m_Config.BitsPerSampleClient, m_Config.ChannelsClient);
							int count = data.Length / bytesPerInterval;
							int currentPos = 0;
							for (int i = 0; i < count; i++)
							{
								//Teilstück in RTP Packet umwandeln
								Byte[] partBytes = new Byte[bytesPerInterval];
								Array.Copy(data, currentPos, partBytes, 0, bytesPerInterval);
								currentPos += bytesPerInterval;
								WinSound.RTPPacket rtp = ToRTPPacket(partBytes, m_Config.BitsPerSampleClient, m_Config.ChannelsClient);

								//Wenn JitterBuffer
								if (UseJitterBufferClientRecording)
								{
									//In Buffer legen
									m_JitterBufferClientRecording.AddData(rtp);
								}
								else
								{
									//Alles in RTP Packet umwandeln
									Byte[] rtpBytes = ToRTPData(data, m_Config.BitsPerSampleClient, m_Config.ChannelsClient);
									//Absenden
									m_Client.Send(m_PrototolClient.ToBytes(rtpBytes));
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

			}
		}
		private Byte[] ToRTPData(Byte[] data, int bitsPerSample, int channels)
		{
			//Neues RTP Packet erstellen
			WinSound.RTPPacket rtp = ToRTPPacket(data, bitsPerSample, channels);
			//RTPHeader in Bytes erstellen
			Byte[] rtpBytes = rtp.ToBytes();
			//Fertig
			return rtpBytes;
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
		private bool IsRecorderFromSounddeviceStarted_Client
		{
			get
			{
				if (m_Recorder_Client != null)
				{
					return m_Recorder_Client.Started;
				}
				return false;
			}
		}
		private void InitComboboxesClient()
		{
			playbackNames = WinSound.WinSound.GetPlaybackNames();
			recordingNames = WinSound.WinSound.GetRecordingNames();
		}
		private void ConnectClient()
		{
			try
			{
				if (IsClientConnected == false)
				{
					//Wenn Eingabe vorhanden
					if (m_Config.IpAddressClient.Length > 0 && m_Config.PortClient > 0)
					{
						m_Client = new NF.TCPClient(m_Config.IpAddressClient, m_Config.PortClient);
						m_Client.ClientDisconnected += new NF.TCPClient.DelegateConnection(OnClientDisconnected);
						m_Client.ExceptionAppeared += new NF.TCPClient.DelegateException(OnClientExceptionAppeared);
						m_Client.DataReceived += new NF.TCPClient.DelegateDataReceived(OnClientDataReceived);
						m_Client.Connect();
					}
				}
			}
			catch (Exception)
			{
				m_Client = null;

			}
		}
		private void DisconnectClient()
		{
			try
			{
				//Aufnahme beenden
				StopRecordingFromSounddevice_Client();

				if (m_Client != null)
				{
					//Client beenden
					m_Client.Disconnect();
					m_Client.ClientDisconnected -= new NF.TCPClient.DelegateConnection(OnClientDisconnected);
					m_Client.ExceptionAppeared -= new NF.TCPClient.DelegateException(OnClientExceptionAppeared);
					m_Client.DataReceived -= new NF.TCPClient.DelegateDataReceived(OnClientDataReceived);
					m_Client = null;
				}
			}
			catch (Exception)
			{

			}
		}
		private void OnClientDisconnected(NF.TCPClient client, string info)
		{
			//Abspielen beenden
			StopPlayingToSounddevice_Client();
			//Streamen von Sounddevice beenden
			StopRecordingFromSounddevice_Client();

			if (m_Client != null)
			{
				m_Client.ClientDisconnected -= new NF.TCPClient.DelegateConnection(OnClientDisconnected);
				m_Client.ExceptionAppeared -= new NF.TCPClient.DelegateException(OnClientExceptionAppeared);
				m_Client.DataReceived -= new NF.TCPClient.DelegateDataReceived(OnClientDataReceived);
			}
		}
		private void OnClientExceptionAppeared(NF.TCPClient client, Exception ex) => DisconnectClient();

		private void OnClientDataReceived(NF.TCPClient client, Byte[] bytes)
		{
			try
			{
				if (m_PrototolClient != null)
				{
					m_PrototolClient.Receive_LH(client, bytes);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
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
					OnClientConfigReceived(sender, data);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}
		private void OnClientConfigReceived(Object sender, Byte[] data)
		{
			try
			{
				m_Config.SamplesPerSecondClient = 8000;
				StartPlayingToSounddevice_Client();
				StartRecordingFromSounddevice_Client();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
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
				m_Config.IpAddressClient = IPAddress;
				m_Config.PortClient = connection_port;
				m_Config.SoundInputDeviceNameClient = recordingNames[0];
				m_Config.SoundOutputDeviceNameClient = playbackNames[0];
				m_Config.JitterBufferCountClient = (uint)20;
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
		public void ConnectToServer()
		{

			try
			{
				//Daten holen
				FormToConfig();

				if (IsClientConnected)
				{
					DisconnectClient();
					StopRecordingFromSounddevice_Client();
				}
				else
				{
					ConnectClient();
				}

				//Kurz warten
				System.Threading.Thread.Sleep(100);
			}
			catch (Exception)
			{

			}
		}
		private void StartPlayingToSounddevice_Client()
		{
			//JitterBuffer starten
			if (m_JitterBufferClientPlaying != null)
			{
				InitJitterBufferClientPlaying();
				m_JitterBufferClientPlaying.Start();
			}

			if (m_PlayerClient == null)
			{
				m_PlayerClient = new WinSound.Player();
				m_PlayerClient.Open(m_Config.SoundOutputDeviceNameClient, m_Config.SamplesPerSecondClient, m_Config.BitsPerSampleClient, m_Config.ChannelsClient, (int)m_Config.JitterBufferCountClient);
			}

			//Timer starten
			//m_TimerProgressBarPlayingClient.Start();
		}
		private void StopPlayingToSounddevice_Client()
		{
			if (m_PlayerClient != null)
			{
				m_PlayerClient.Close();
				m_PlayerClient = null;
			}

			//JitterBuffer beenden
			if (m_JitterBufferClientPlaying != null)
			{
				m_JitterBufferClientPlaying.Stop();
			}

			//Timer beenden
			//m_TimerProgressBarPlayingClient.Stop();

		}
		public void speakertougle() => m_Config.MuteClientPlaying = !m_Config.MuteClientPlaying;
		public void mictougle() => m_Config.ClientNoSpeakAll = !m_Config.ClientNoSpeakAll;
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
		public bool MuteServerListen = false;
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
			StreamClient.DictionaryMixed[st] = new Queue<List<byte>>();

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
					Queue<List<Byte>> q = StreamClient.DictionaryMixed[sender];
					if (q.Count < 10)
					{
						//Daten Zu Mixer hinzufügen
						StreamClient.DictionaryMixed[sender].Enqueue(new List<Byte>(linearBytes));
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
