using System.Collections.Concurrent;
using UnityEngine;

public class OnlineAudio : MonoBehaviour
{
    private AudioClip recording; // 用于存储录音的 AudioClip 对象
    private int lastSample = 0;  // 上一次处理的音频样本位置，用于追踪录音的当前位置
    public static int wave_buffer_collectfrequency = 16000; // 采样率，定义为 16000 Hz
    public static readonly ConcurrentQueue<byte[]> voicebuff = new ConcurrentQueue<byte[]>(); // 使用 ConcurrentQueue 存储 byte[] 数据，确保多线程安全
    private int bufferLengthSeconds = 10; // 定义缓冲区的时长为 10 秒

    public void StartRec()
    {
        Debug.Log("开始录音"); // 输出调试信息，表示开始录音

        // 清空缓存数据，避免旧数据残留
        int buffnum = voicebuff.Count; // 获取当前队列中的数据数量
        for (int i = 0; i < buffnum; i++)
            voicebuff.TryDequeue(out byte[] buff); // 从队列中逐个移除数据

        // 获取麦克风设备并开始录音
        string microphoneName = Microphone.devices[0]; // 获取第一个麦克风设备的名称
        recording = Microphone.Start(microphoneName, true, bufferLengthSeconds, wave_buffer_collectfrequency);
        // 使用麦克风开始录音，设置循环录音（true），时长为 bufferLengthSeconds（10秒），采样率为 wave_buffer_collectfrequency（16000Hz）

        // 检查麦克风是否成功启动
        if (Microphone.IsRecording(microphoneName))
        {
            Debug.Log("录音已启动"); // 如果录音成功，输出调试信息
        }
        else
        {
            Debug.LogError("无法启动录音"); // 如果录音失败，输出错误信息
        }
    }

    public void StopRec()
    {
        // 停止录音并结束当前录音会话
        if (Microphone.IsRecording(null)) // 如果当前麦克风正在录音
        {
            Microphone.End(null); // 停止录音
        }

        Debug.Log("录音结束"); // 输出调试信息，表示录音结束
    }

    private void Update()
    {
        // 如果当前麦克风正在录音，处理录音数据
        if (Microphone.IsRecording(null))
        {
            int currentSample = Microphone.GetPosition(null); // 获取当前麦克风的录音位置（样本位置）

            // 处理录音数据（检查缓冲区环绕情况，即缓冲区重新开始的情况）
            if (currentSample > lastSample || currentSample < lastSample)
            {
                // 计算需要处理的样本数
                int samplesToProcess = (currentSample > lastSample) ? currentSample - lastSample : (recording.samples - lastSample + currentSample);

                // 创建一个新的数组来存储从录音中提取的浮点型样本数据
                float[] data = new float[samplesToProcess];

                // 从录音数据中提取样本，从上一次的位置开始
                recording.GetData(data, lastSample);

                // 将提取的 float[] 数据转换为 16-bit PCM 格式的 byte[] 数据
                byte[] byteData = ConvertFloatTo16BitPCM(data);

                // 将转换后的 byte[] 数据存入队列
                voicebuff.Enqueue(byteData);

                // 更新 lastSample，准备处理下一部分数据
                lastSample = currentSample;
            }
        }
    }

    // 将 float[] 数据转换为 16-bit PCM 格式的 byte[] 数据
    public static byte[] ConvertFloatTo16BitPCM(float[] samples)
    {
        // 创建 byte[] 数组，大小为 float 数组大小的两倍，因为每个 float 需要 2 个字节来表示
        byte[] byteData = new byte[samples.Length * 2];
        int byteIndex = 0; // 用于追踪 byte[] 数组中的位置

        // 遍历所有的 float 样本数据
        foreach (float sample in samples)
        {
            // 将 float 样本限制在 -1.0f 和 1.0f 之间，并映射到 16-bit 整数 (-32768 到 32767)
            short intSample = (short)(Mathf.Clamp(sample, -1.0f, 1.0f) * short.MaxValue);

            // 将 16-bit 整数拆分为两个字节，并存储在 byte[] 中
            byteData[byteIndex++] = (byte)(intSample & 0xFF);         // 存储低字节
            byteData[byteIndex++] = (byte)((intSample >> 8) & 0xFF);  // 存储高字节
        }
        return byteData; // 返回转换后的 byte[] 数据
    }

    // 从队列中取出录音数据
    public static byte[] Wavedata_Dequeue()
    {
        byte[] datas;
        voicebuff.TryDequeue(out datas); // 尝试从队列中取出 byte[] 数据
        return datas; // 返回取出的数据
    }
}