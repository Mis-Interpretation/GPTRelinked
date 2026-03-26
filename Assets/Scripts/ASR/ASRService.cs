using UnityEngine;
using UnityEngine.UI;
using UnityWebSocket;
using eToile;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

public class ASRService : MonoBehaviour
{
    public Button startButton;
    public Button stopButton;
    public Text uiText;

    [Header("是否实时方式传输")]
    public bool isRealTime = false;

    private OnlineAudio ola;
    // 定义音频片段的时间间隔为 10（具体单位可能在此处或其他地方有定义）
    private static int chunk_interval = 10;
    // 定义一个整数数组，分别用于表示不同情况下的音频片段大小
    private static int[] chunk_size = new int[] { 5, 10, 5 };
    //发送实时音频的线程
    Thread SendAudioThread;

    private AudioClip recording;
    private string microphoneDevice;
    private bool isRecording = false;
    private float recordingStartTime;
    private const int maxRecordingTime = 15; //录制最大时长
    WebSocket socket;

    public static event Action<string> EventASRCompleted;
    public static event Action<string> EventASRUpdated;
    private string asrCache = "";
    
    // Singleton Pattern
    public static ASRService Instance;

    void Awake()
    {
        if (!Instance)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (isRealTime)
        {
            ola = gameObject.AddComponent<OnlineAudio>();
        }        

        startButton.onClick.AddListener(StartASR);
        stopButton.onClick.AddListener(StopASR);

        // 创建实例
        string address = "ws://127.0.0.1:10095";
        socket = new WebSocket(address);

        // 注册回调
        socket.OnMessage += OnMessage;

        // 连接
        socket.ConnectAsync();

        // Disable stop button initially
        stopButton.interactable = false;
    }

    /// <summary>
    /// 开始语音识别
    /// </summary>
    void StartASR()
    {
        if (isRealTime)
        {
            StartCoroutine(OnlineASR2());
        }
        else
        {
            StartRecording();
        }
    }

    /// <summary>
    /// 停止语音识别
    /// </summary>
    void StopASR()
    {
        if (isRealTime)
        {
            StopRealTimeASR();
        }
        else
        {
            StopRecording();
        }
    }

    bool isStream = false;
    /// <summary>
    /// 处理服务端返回消息
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnMessage(object sender, MessageEventArgs e)
    {
        RecData data = JsonUtility.FromJson<RecData>(e.Data);
        if (!isStream)
        {
            asrCache = "";
            // uiText.text = "";
        }
        if (data.mode == "2pass-online")
        {
            asrCache += data.text;
            // uiText.text += data.text;
            isStream = true;
            EventASRUpdated?.Invoke(asrCache);
        }
        else
        {
            string result = RemoveLeadingPunctuationIfPresent(data.text);
            asrCache = result;
            // uiText.text = result;
            isStream = false;
            EventASRCompleted?.Invoke(result);
        }
        uiText.text = asrCache;
        Debug.Log("Mode:" + data.mode + "   Receive: " + data.text);
    }

    void Update()
    {
        if (isRecording && Time.time - recordingStartTime >= maxRecordingTime)
        {
            StopRecording();
        }
    }

    /// <summary>
    /// 开始录音
    /// </summary>
    void StartRecording()
    {
        if (isRecording)
            return;

        // Get the default microphone
        microphoneDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;

        if (string.IsNullOrEmpty(microphoneDevice))
        {
            Debug.LogError("没找到麦克风!");
            return;
        }

        recording = Microphone.Start(microphoneDevice, false, maxRecordingTime, 16000);
        recordingStartTime = Time.time;
        isRecording = true;

        // Update button states
        startButton.interactable = false;
        stopButton.interactable = true;

        Debug.Log("开始录音");
    }

    /// <summary>
    /// 停止录音
    /// </summary>
    void StopRecording()
    {
        if (!isRecording)
            return;

        Microphone.End(microphoneDevice);
        isRecording = false;

        // Update button states
        startButton.interactable = true;
        stopButton.interactable = false;

        Debug.Log("停止录音");

        // Convert AudioClip to byte array
        byte[] wavData = OpenWavParser.AudioClipToByteArray(recording).ToArray();
        Debug.Log("Audio converted to WAV format. Byte length: " + wavData.Length);

        socket.SendAsync("{\"mode\":\"offline\",\"wav_name\":\"test.wav\",\"is_speaking\":true,\"hotwords\":\"\",\"itn\":true}");
        socket.SendAsync(wavData);
        socket.SendAsync("{\"is_speaking\": false}");
    }


    //实时发送 byte 数据队列
    public static readonly ConcurrentQueue<byte[]> RealTimeAudioSet = new ConcurrentQueue<byte[]>();
    IEnumerator OnlineASR2()
    {
        //开始录音并发送语音识别
        ola.StartRec();
        string firstbuff = string.Format("{{\"mode\": \"{0}\", \"chunk_size\": [{1},{2},{3}], \"chunk_interval\": {4}, \"wav_name\": \"microphone\", \"is_speaking\": true}}", "2pass", chunk_size[0], chunk_size[1], chunk_size[2], chunk_interval);
        socket.SendAsync(firstbuff);
        startButton.interactable = false;
        stopButton.interactable = true;

        SendAudioThread = new Thread(SendAudioToSeverAsync);
        SendAudioThread.Start();

        while (true)
        {
            if (!OnlineAudio.voicebuff.IsEmpty)
            {
                byte[] buff;
                int buffcnt = OnlineAudio.voicebuff.Count;
                //音频数据入队列 音频数据出队列
                OnlineAudio.voicebuff.TryDequeue(out buff);

                if (buff != null)
                    RealTimeAudioSet.Enqueue(buff);//实时发送 byte 数据队列 入队
            }
            // 暂停到下一帧，避免死循环卡顿
            yield return null; // 等待下一帧
        }
    }

    public void StopRealTimeASR()
    {
        ola.StopRec();
        startButton.interactable = true;
        stopButton.interactable = false;
        // 异步发送表示音频结束的消息
        Task.Run(() => socket.SendAsync("{\"is_speaking\": false}"));
    }

    /// <summary>
    /// 发送音频到服务端
    /// </summary>
    private void SendAudioToSeverAsync()
    {
        while (true)
        {
            if (RealTimeAudioSet.Count > 0)
            {
                byte[] audio;
                RealTimeAudioSet.TryDequeue(out audio);
                if (audio == null)
                    continue;

                byte[] mArray = new byte[audio.Length];
                Array.Copy(audio, 0, mArray, 0, audio.Length);
                if (mArray != null)
                    socket.SendAsync(mArray);
            }
            else
            {
                Thread.Sleep(10);
            }
        }
    }

    /// <summary>
    /// 尝试去除字符串前面的标点符号（如果存在）
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    string RemoveLeadingPunctuationIfPresent(string input)
    {
        if (string.IsNullOrEmpty(input)) // 如果字符串为空或为null，直接返回
        {
            return input;
        }

        char firstChar = input[0]; // 获取字符串的第一个字符
        if (char.IsPunctuation(firstChar)) // 检查第一个字符是否是标点符号
        {
            return input.Substring(1); // 如果是标点符号，去掉第一个字符
        }
        else
        {
            return input; // 如果不是标点符号，返回原字符串
        }
    }

    private void OnDestroy()
    {
        if (isRealTime)
        {
            StopRealTimeASR();
            //线程关闭
            if (SendAudioThread != null)
            {
                if (SendAudioThread.IsAlive)
                {
                    SendAudioThread.Abort();
                }
            }
        }               
    }

    private void OnApplicationQuit()
    {
        if (isRealTime)
        {
            //线程关闭
            if (SendAudioThread != null)
            {
                if (SendAudioThread.IsAlive)
                {
                    SendAudioThread.Abort();
                }
            }
        }
    }
}


// 与服务端JSON结构匹配的C#类
[Serializable]
public class RecData
{
    public bool is_final;
    public string mode;
    public List<StampSent> stamp_sents;
    public string text;
    public string timestamp;
    public string wav_name;
}

[Serializable]
public class StampSent
{
    public int end;
    public string punc;
    public int start;
    public string text_seg;
    public List<List<int>> ts_list;
}