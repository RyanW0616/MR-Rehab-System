using System.IO;
using System;
using System.Net;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using JetBrains.Annotations;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
//using ReadyPlayerMe.AvatarCreator;

public class AI_Manager : MonoBehaviour
{
    public TextPreloader textPreloader;

    private AudioClip recordedClip;
    [SerializeField] AudioSource audioSource;
    private int sampleRate = 44100;

    private XRHandSubsystem handSubsystem;//偵測手掌
    private XRHand rightHand;
    private bool palmUpLastFrame = false;
    private bool isRecording = false;
    private float palmHoldTimer = 0f;
    private float palmHoldThreshold = 0.5f; // 維持 1.5 秒才觸發
    private bool hasTriggered = false;
    private string APIkey = "";
    private string huggingFaceToken = "";
    private string huggingFaceToken_new = "";
    private string GroqApiKey = "";

    // 聊天紀錄（GPT 記憶）
    private List<Message> chatHistory = new List<Message>();


    // 新增：光球設定
    [SerializeField] private Material lightBallMaterial;  // 拖入你的LightBallMat
    private GameObject lightBallObject;  // 光球物件參考
    private float baseScale = 0.08f;  // 球基本大小（5cm直徑）
    private float waveAmplitude = 0.01f;  // 上下波動幅度（微小）
    private float pulseSpeed = 2f;  // 波動/膨脹速度

    [Header("Nimo相關")]
    public GameObject AIPrefab;                  // AI人偶Prefab (Inspector設)
    public NimoFollowCamera nimoFollowCamera;   // Inspector指定物件（同AI_Manager物件，掛有NimoFollowCamera腳本）
    private GameObject spawnedNimo;              // 存放生成的人偶物件
    [Header("AI回應客製化")]
    [TextArea(3, 10)]  // 在Inspector中顯示為多行文字區域，便於編輯
    public string customSystemPrompt = "You are a helpful assistant. Always respond in 50 words or less.";



    [System.Serializable]
    public class ChatGPTRequest
    {
        public string model;
        public List<Message> messages;

        public float temperature = 0.7f;  // 給個預設值
        public int max_tokens = 100;     // 給個預設值
    }

    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;

        public Message(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }
    [System.Serializable]
    public class WhisperResponse
    {
        public string text;
    }
    [System.Serializable]
    public class ChatGPTResponse
    {
        public Choice[] choices;
    }

    [System.Serializable]
    public class Choice
    {
        public int index;
        public Message message;
        public string finish_reason;
    }


    void Start()
    {
        if (textPreloader == null) textPreloader = FindObjectOfType<TextPreloader>();
        // 啟動 Coroutine 來處理等待和後續邏輯
        StartCoroutine(WaitForPreloadAndContinue());
    }

    private IEnumerator WaitForPreloadAndContinue()
    {
        // 等待預載完成：每幀檢查 hasPreloaded
        while (!textPreloader.hasPreloaded)
        {
            yield return null;  // 讓出控制權，等待下一幀，避免阻塞
        }

        // 預載完成後，繼續原有邏輯
        SpawnAICharacter();
        Debug.Log("goin");
        // 嘗試從 loader 拿到 hand subsystem
        handSubsystem = XRGeneralSettings.Instance.Manager.activeLoader.GetLoadedSubsystem<XRHandSubsystem>();
        chatHistory.Clear();  // 清空以確保乾淨開始（如果需要）
        chatHistory.Add(new Message("system", customSystemPrompt));
    }



    void Update()
    {
        if (handSubsystem == null || !handSubsystem.running)
            return;

        rightHand = handSubsystem.rightHand;

        if (!rightHand.isTracked)
        {
            if (hasTriggered)
            {
                Debug.LogError("2222");
                StopRecording();
                // 手勢中斷，重置計時器與觸發旗標
                if (palmHoldTimer > 0f)
                    Debug.LogError(" 手勢中斷，重置計時器");

                palmHoldTimer = 0f;
                hasTriggered = false;
            }
            return;
        }


        if (IsPalmFacingUp(rightHand))
        {
            if (!hasTriggered)
            {
                palmHoldTimer += Time.deltaTime;
                //Debug.LogError(hasTriggered);
                Debug.Log("目前秒數:" + palmHoldTimer);
                if (palmHoldTimer >= palmHoldThreshold)
                {
                    hasTriggered = true;
                    Debug.LogError("手勢持續 1.5 秒  開始錄音");
                    AudioManager.Instance.AIHelper();
                    StartRecording();
                }
            }

        }
        else if (hasTriggered)
        {
            Debug.LogError("超出設定數值範圍終止錄音");
            StopRecording();
            // 手勢中斷，重置計時器與觸發旗標
            if (palmHoldTimer > 0f)
                Debug.LogError(" 手勢中斷，重置計時器");

            palmHoldTimer = 0f;
            hasTriggered = false;
        }


        // 新增：光球跟隨與動畫
        if (lightBallObject != null)
        {
            XRHand rightHandUpdate = handSubsystem.rightHand;
            if (rightHandUpdate.isTracked)
            {
                XRHandJoint middleProximal = rightHandUpdate.GetJoint(XRHandJointID.MiddleProximal);
                if (middleProximal.TryGetPose(out Pose pose))
                {
                    // 跟隨手 + 上下波動
                    Vector3 basePosition = pose.position + Vector3.up * 0.08f;
                    float waveOffset = Mathf.Sin(Time.time * pulseSpeed) * waveAmplitude;
                    lightBallObject.transform.position = basePosition + new Vector3(0f, waveOffset, 0f);

                    // 膨脹/縮小
                    float pulseScale = baseScale + (Mathf.Sin(Time.time * pulseSpeed) * 0.005f);  // 微小變化
                    lightBallObject.transform.localScale = Vector3.one * pulseScale;

                    float spinSpeed = 30f;                                    // 每秒 30 度，可自行調整
                    lightBallObject.transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
                }
            }
        }
    }
    


    void SpawnAICharacter()
    {

        // 取得玩家相機的位置與朝向（MR 環境中代表使用者的位置）
        Transform cameraTransform = Camera.main.transform;

        // 生成位置：玩家正前方 3 公尺
        Vector3 spawnPosition = cameraTransform.position + cameraTransform.forward * 0.3f;

        // 面向玩家（Y 軸水平旋轉）
        Quaternion lookRotation = Quaternion.LookRotation(cameraTransform.position - spawnPosition);
        lookRotation = Quaternion.Euler(0, lookRotation.eulerAngles.y, 0);

        // 生成 AI 人偶
        spawnedNimo = Instantiate(AIPrefab, spawnPosition, lookRotation);
        //spawnedNimo.transform.localScale *= 1f;
        Debug.LogError("AI 人偶已生成於 MR 環境");

        // 將生成的 Nimo 指派給 NimoFollowCamera 管理
        if (nimoFollowCamera != null)
        {
            nimoFollowCamera.targetObjectToMove = spawnedNimo;
            Animator anim = spawnedNimo.GetComponent<Animator>();
            if (anim != null)
            {
                nimoFollowCamera.nimoAnimator = anim;
                //Debug.Log("已將Animator指派到nimoFollowCamera");
            }
            else
            {
                //Debug.LogWarning("生成的Nimo沒有找到Animator元件！");
            }
        }
        else
        {
            Debug.LogError("nimoFollowCamera 未設定，請確認Inspector中已掛載此腳本並指定。");
        }
    }

    
    bool IsPalmFacingUp(XRHand hand)
    {
        // 取得關鍵關節：手腕、食指根部（使用 Proximal 作為近似 metacarpal）、小指根部
        XRHandJoint wristJoint = hand.GetJoint(XRHandJointID.Wrist);
        XRHandJoint indexBaseJoint = hand.GetJoint(XRHandJointID.IndexProximal); // 食指根部
        XRHandJoint littleBaseJoint = hand.GetJoint(XRHandJointID.LittleProximal); // 小指根部

        if (!wristJoint.TryGetPose(out Pose wristPose) ||
            !indexBaseJoint.TryGetPose(out Pose indexPose) ||
            !littleBaseJoint.TryGetPose(out Pose littlePose))
        {
            Debug.Log("無法取得關節姿態");
            return false;
        }

        // 計算兩個向量：從手腕到食指根部，從手腕到小指根部
        Vector3 vec1 = (indexPose.position - wristPose.position).normalized;
        Vector3 vec2 = (littlePose.position - wristPose.position).normalized;

        // 計算叉積得到手掌法向量（右手規則：vec1 x vec2 應指向手心方向）
        Vector3 palmNormal = Vector3.Cross(vec2, vec1).normalized;

        // 與世界向上向量做點積
        float dot = Vector3.Dot(palmNormal, Vector3.up);
        if (!hasTriggered)
        {
            //Debug.Log("Palm Normal Dot with Up: " + dot);
        }

        return dot > 0.9f;
    }

    public void StartRecording()
    {
        Debug.LogError("開始錄音123");
        recordedClip = Microphone.Start(null, false, 60, sampleRate);


        // 新增：創建光球
        if (lightBallMaterial == null)
        {
            Debug.LogError("缺少 lightBallMaterial，請在Inspector中指派！");
            return;
        }

        XRHand rightHandStart = handSubsystem.rightHand;
        if (rightHandStart.isTracked)
        {
            XRHandJoint middleProximal = rightHandStart.GetJoint(XRHandJointID.MiddleProximal);
            if (middleProximal.TryGetPose(out Pose pose))
            {
                Vector3 effectPosition = pose.position + Vector3.up * 0.05f;

                lightBallObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lightBallObject.name = "LightBallEffect";
                Destroy(lightBallObject.GetComponent<SphereCollider>());  // 移除碰撞
                Renderer renderer = lightBallObject.GetComponent<Renderer>();
                renderer.material = lightBallMaterial;
                lightBallObject.transform.position = effectPosition;
                lightBallObject.transform.localScale = Vector3.one * baseScale;
            }
        }
    }

    public void StopRecording()
    {
        Debug.LogError("停止錄音123");
        Microphone.End(null);  // 停止錄音

        // 新增：銷毀光球
        if (lightBallObject != null)
        {
            Destroy(lightBallObject);
            lightBallObject = null;
        }


        PlayRecording();
    }





    public void PlayRecording()
    {
        //Debug.LogError("播放錄音");
        if (recordedClip != null)
        {
            audioSource.clip = recordedClip;
            //audioSource.Play();重播使用者問題
        }
        byte[] wavBytes = WavUtility.FromAudioClip(recordedClip);
        StartCoroutine(SendToWhisper(wavBytes));
    }

    void AddToMemory(string role, string content)
    {
        if (role != "system")
        {
            chatHistory.Add(new Message(role, content));
        }
        // 最多只保留 20 條（即最多 10 輪來回），但保留系統訊息
        while (chatHistory.Count > 21)  // 多留一條給system
        {
            chatHistory.RemoveAt(1);  // 移除第二條（保留system）
        }
    }


    /// <summary>
    /// 這是透過hugging face呼叫的，但是目前huggingface已經停用這個模組了
    /// </summary>
    /*
    IEnumerator SendToWhisper(byte[] wavData)
    {
        Debug.LogError("傳給Whisper-large-v3-turbo via Hugging Face for 構音判斷");

        if (wavData == null || wavData.Length < 1024)
        {
            Debug.LogError("音頻資料太短或無效，忽略辨識");
            yield break;
        }

        // 建立請求（Hugging Face的turbo端點）
        //UnityWebRequest www = new UnityWebRequest("https://api-inference.huggingface.co/models/openai/whisper-large-v3-turbo", "POST");
        UnityWebRequest www = new UnityWebRequest("https://api-inference.huggingface.co/models/openai/whisper-large-v3", "POST");
        www.uploadHandler = new UploadHandlerRaw(wavData);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Authorization", "Bearer " + huggingFaceToken_new);
        www.SetRequestHeader("Content-Type", "audio/wav");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string jsonResult = www.downloadHandler.text;
            Debug.LogError("辨識結果：" + jsonResult);

            // 提取文字並進行後處理
            string recognizedText = ExtractTextFromWhisperJson(jsonResult);  // 你的既有函數（適用wav2vec輸出格式）
            Debug.LogError("後處理後的結果：" + recognizedText);

            // 繼續後續：AddToMemory 和 SendToChatGPT
            AddToMemory("user", recognizedText);
            yield return StartCoroutine(SendToChatGPT());
        }
        else
        {
            Debug.LogError("辨識失敗：" + www.error);
        }
    }
    */

    IEnumerator SendToWhisper(byte[] wavData)
    {
        string apiUrl = "https://api.groq.com/openai/v1/audio/transcriptions";
        string apiKey = GroqApiKey;

        Debug.LogError($"[API] 開始傳送音訊至: {apiUrl}");

        if (wavData == null || wavData.Length < 1024)
        {
            Debug.LogError("音頻資料太短或無效，忽略辨識");
            yield break;
        }

        // OpenAI/Groq 格式需要用 MultipartFormData (Form-Data) 傳送
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();

        // 1. 加入音訊檔案 (參數名稱必須是 "file")
        // fileName 必須有副檔名 .wav
        formData.Add(new MultipartFormFileSection("file", wavData, "recording.wav", "audio/wav"));

        // 2. 指定模型名稱 (參數名稱 "model")
        formData.Add(new MultipartFormDataSection("model", "whisper-large-v3-turbo"));

        // 3. (選填) 指定回應格式為 json
        formData.Add(new MultipartFormDataSection("response_format", "json"));

        // 4. (選填) 針對構音判斷，可設定 temperature=0 降低幻覺
        formData.Add(new MultipartFormDataSection("temperature", "0"));

        UnityWebRequest www = UnityWebRequest.Post(apiUrl, formData);

        // 設定 Authorization Header
        www.SetRequestHeader("Authorization", "Bearer " + apiKey);

        // 發送請求
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string jsonResult = www.downloadHandler.text;
            Debug.Log("辨識結果 (JSON)：" + jsonResult);

            // 解析 JSON (Groq/OpenAI 回傳格式: { "text": "..." })
            string recognizedText = ExtractTextFromWhisperJson(jsonResult);
            Debug.Log("解析後文字：" + recognizedText);

            AddToMemory("user", recognizedText);
            yield return StartCoroutine(SendToGroqLlama());
        }
        else
        {
            // 印出詳細錯誤訊息
            Debug.LogError($"辨識失敗: {www.responseCode} {www.error}");
            Debug.LogError($"錯誤內容: {www.downloadHandler.text}");
        }
    }








    /*
    string ExtractTextFromWhisperJson(string json)
    {
        var wrapper = JsonUtility.FromJson<WhisperResponse>(json);
        return wrapper.text;
    }
    */

    /*
    string ExtractTextFromWhisperJson(string json)//有加後處理you you
    {
        var wrapper = JsonUtility.FromJson<WhisperResponse>(json);
        string text = wrapper.text.Trim();  // 先移除前後空白

        // 移除常見hallucination
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s*you\s*you\s*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return text;
    }
    */

    string ExtractTextFromWhisperJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return "";

        // 1. 解析 JSON
        // Groq/OpenAI 回傳格式: { "text": "你的語音內容" }
        WhisperResponse wrapper = null;
        try
        {
            wrapper = JsonUtility.FromJson<WhisperResponse>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"JSON 解析失敗: {e.Message}");
            return "";
        }

        if (wrapper == null || string.IsNullOrEmpty(wrapper.text))
        {
            // 容錯：有時候錯誤訊息會直接回傳在 json 裡，避免空參導致崩潰
            return "";
        }

        string text = wrapper.text.Trim();

        // 2. 後處理：移除舊版幻覺
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s*you\s*you\s*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // 3. 【新增】後處理：移除 Whisper V3 常見幻覺
        // V3 常常在沒聲音時自己補 "Thank you." 或 "Thanks for watching."
        string[] commonHallucinations = new string[]
        {
            "请不吝点赞", "請不吝點贊",
            "订阅", "訂閱",
            "转发", "轉發",
            "打赏支持", "打賞支持",
            "明镜与点点栏目", "明鏡與點點欄目",
            "Thank you", "Thanks for watching"
        };

        foreach (var h in commonHallucinations)
        {
            if (text.EndsWith(h, System.StringComparison.OrdinalIgnoreCase))
            {
                // 如果整句就只有幻覺詞，直接清空；如果是結尾多餘的，把它切掉
                if (text.Length <= h.Length + 5) // +5 是寬限值
                    text = "";
                else
                    text = text.Substring(0, text.Length - h.Length).Trim();
            }
        }

        return text;
    }


    public IEnumerator SendToGroqLlama()
    {
        string groqApiKey = GroqApiKey;

        // 2. 建立請求資料，指定模型為 llama-3.1-8b-instant
        ChatGPTRequest request = new ChatGPTRequest
        {
            model = "llama-3.1-8b-instant", // 這裡改成 Groq 的模型名稱
            messages = chatHistory,
            temperature = 0.7f, // (選填) 控制回答的隨機性，0.7 適合聊天
            max_tokens = 1024   // (選填) 限制回應長度，避免講太長
        };

        //Debug.Log("傳送到 Groq (Llama-3.1-8b)");
        PrintChatHistory();

        string json = JsonConvert.SerializeObject(request);

        // 3. 建立請求物件，網址改成 Groq 的端點
        UnityWebRequest www = new UnityWebRequest("https://api.groq.com/openai/v1/chat/completions", "POST");

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();

        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Authorization", "Bearer " + groqApiKey);

        // 發送請求
        yield return www.SendWebRequest();

        // 4. 處理回應
        if (www.result == UnityWebRequest.Result.Success)
        {
            string responseText = www.downloadHandler.text;
            //Debug.Log("Groq 回應：" + responseText);

            // 因為 Groq 回傳的 JSON 結構跟 OpenAI 一模一樣，所以可以沿用 ChatGPTResponse 類別
            ChatGPTResponse response = JsonUtility.FromJson<ChatGPTResponse>(responseText);

            if (response.choices == null || response.choices.Length == 0)
            {
                Debug.LogError("Groq 回傳了空的 choices，可能是被過濾了或發生錯誤。");
            }
            else
            {
                string reply = response.choices[0].message.content;
                //Debug.Log("Llama 內容：" + reply);

                // 加入回應進記憶
                AddToMemory("assistant", reply);

                StartCoroutine(ConvertTextToSpeech_Google(reply, audioSource));
            }
            PrintChatHistory();
        }
        else
        {
            Debug.LogError("Groq API 錯誤：" + www.responseCode);
            Debug.LogError("錯誤訊息：" + www.error);
            Debug.LogError("回傳內容：" + www.downloadHandler.text);
        }
    }

    /*
    public IEnumerator SendToChatGPT()
    {

        ChatGPTRequest request = new ChatGPTRequest
        {
            model = "gpt-3.5-turbo",
            messages = chatHistory
        };



        //Debug.LogError("傳送到 ChatGPT");
        PrintChatHistory();

        string json = JsonConvert.SerializeObject(request);

        // 建立請求物件
        UnityWebRequest GPTwww = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        GPTwww.uploadHandler = new UploadHandlerRaw(bodyRaw);
        GPTwww.downloadHandler = new DownloadHandlerBuffer();
        GPTwww.SetRequestHeader("Content-Type", "application/json");
        GPTwww.SetRequestHeader("Authorization", "Bearer " + APIkey);

        // 發送請求
        yield return GPTwww.SendWebRequest();

        // 處理回應
        if (GPTwww.result == UnityWebRequest.Result.Success)
        {
            string responseText = GPTwww.downloadHandler.text;
            //Debug.LogError("GPT 回應：" + responseText);

            ChatGPTResponse response = JsonUtility.FromJson<ChatGPTResponse>(responseText);
            if (response.choices == null)
            {
                Debug.LogError("沒東西");
            }
            if (response.choices != null && response.choices.Length > 0)
            {
                string reply = response.choices[0].message.content;
                //Debug.LogError("GPT內容：" + reply);

                // 加入 GPT 回應進記憶
                AddToMemory("assistant", reply);

                // 新增語音播放
                StartCoroutine(ConvertTextToSpeech_OpenAI(reply, audioSource));
            }
            PrintChatHistory();
        }

        else
        {
            Debug.LogError("ChatGPT 錯誤：" + GPTwww.responseCode);
            Debug.LogError(GPTwww.error);
            Debug.LogError("回傳內容：" + GPTwww.downloadHandler.text);
        }

    }
    */

    public void sysSendToGPT(string message)
    {
        StartCoroutine(SysSendToGPTCoroutine(message));
    }//ai引導系統提供其他腳本API呼叫入口

    private IEnumerator SysSendToGPTCoroutine(string message)//AI引導系統主要gpt處理
    {
        // 創建臨時 messages 列表，不影響 chatHistory
        List<Message> tempMessages = new List<Message>();
        tempMessages.Add(new Message("system", customSystemPrompt)); // 使用 system 角色設定行為
        tempMessages.Add(new Message("user", message)); // 將輸入作為 user 角色

        // 準備 ChatGPT 請求
        ChatGPTRequest request = new ChatGPTRequest
        {
            model = "gpt-3.5-turbo",
            messages = tempMessages
        };

        //Debug.Log("傳送到 ChatGPT (系統引導模式): " + message);
        string json = JsonConvert.SerializeObject(request);

        // 建立請求物件
        UnityWebRequest GPTwww = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        GPTwww.uploadHandler = new UploadHandlerRaw(bodyRaw);
        GPTwww.downloadHandler = new DownloadHandlerBuffer();
        GPTwww.SetRequestHeader("Content-Type", "application/json");
        GPTwww.SetRequestHeader("Authorization", "Bearer " + APIkey);

        // 發送請求
        yield return GPTwww.SendWebRequest();

        // 處理回應
        if (GPTwww.result == UnityWebRequest.Result.Success)
        {
            string responseText = GPTwww.downloadHandler.text;
            ChatGPTResponse response = JsonUtility.FromJson<ChatGPTResponse>(responseText);

            if (response.choices != null && response.choices.Length > 0)
            {
                string reply = response.choices[0].message.content;
                //Debug.Log("GPT 系統引導回應: " + reply);

                // 直接轉換為 TTS 並播放（不加入 chatHistory）
                yield return StartCoroutine(ConvertTextToSpeech_Google(reply, audioSource));
            }
        }
        else
        {
            Debug.LogError("ChatGPT 系統引導錯誤: " + GPTwww.responseCode + " - " + GPTwww.error);
        }
    }

    public void PrintChatHistory()
    {
        Debug.LogError("===== Chat History =====");
        for (int i = 0; i < chatHistory.Count; i++)
        {
            //Debug.LogError($"[{i}] Role: {chatHistory[i].role}, Content: {chatHistory[i].content}");
        }
        Debug.LogError("========================");
    }

    IEnumerator PlayWavFile(string filePath, AudioSource audioSource)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("讀取 WAV 失敗：" + www.error);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    public static AudioClip ToAudioClip(byte[] wavFile, int offsetSamples = 0, string name = "wav")
    {
        int channels = BitConverter.ToInt16(wavFile, 22);
        int sampleRate = BitConverter.ToInt32(wavFile, 24);
        int bitsPerSample = BitConverter.ToInt16(wavFile, 34);
        int dataStartIndex = 44;
        int bytesPerSample = bitsPerSample / 8;
        int totalSamples = (wavFile.Length - dataStartIndex) / bytesPerSample;
        float[] data = new float[totalSamples];

        if (bitsPerSample == 16)
        {
            // 16bit PCM
            for (int i = 0; i < totalSamples; i++)
            {
                short sample = BitConverter.ToInt16(wavFile, dataStartIndex + i * 2);
                data[i] = sample / 32768f;
            }
        }
        else if (bitsPerSample == 32)
        {
            // float PCM
            for (int i = 0; i < totalSamples; i++)
            {
                float sample = BitConverter.ToSingle(wavFile, dataStartIndex + i * 4);
                data[i] = sample;
            }
        }
        else
        {
            Debug.LogError("WavUtility: 不支援的 bitsPerSample = " + bitsPerSample);
            return null;
        }

        AudioClip clip = AudioClip.Create(name, data.Length / channels, channels, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
    /*IEnumerator ConvertTextToSpeech_OpenAI(string text, AudioSource audioSource)
    {
        string url = "https://api.openai.com/v1/audio/speech";

        // 建立 JSON 資料
        var requestData = new
        {
            model = "tts-1", // 或 "tts-1-hd"
            input = text,
            voice = "nova", // 可用：nova / shimmer（女聲） / onyx（男聲，自然）/ echo
            response_format = "wav"
        };
        string json = JsonConvert.SerializeObject(requestData); // 要用 Json.NET

        // 建立請求
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(jsonBytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + APIkey);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("TTS失敗：" + request.error);
            yield break;
        }

        byte[] WavData = request.downloadHandler.data;

        // 將 MP3 儲存起來
        string filePath = Path.Combine(Application.persistentDataPath, "gpt_reply.wav");
        File.WriteAllBytes(filePath, WavData);

        Debug.LogError("語音檔儲存成功：" + filePath);

        // 播放 mp3（需轉換）
        StartCoroutine(PlayWavFile(filePath, audioSource));
    }
    */

    IEnumerator ConvertTextToSpeech_Google(string text, AudioSource audioSource)
    {
        // 防止空字串報錯
        if (string.IsNullOrEmpty(text)) yield break;

        // Google 翻譯 TTS 密技網址
        // client=tw-ob 是 Google 內部測試用的客戶端 ID，速度快且無須驗證
        // tl=zh-TW 代表繁體中文 (你可以改成 en-US 變英文)
        string url = "https://translate.google.com/translate_tts?ie=UTF-8&client=tw-ob&tl=zh-TW&q=" + UnityWebRequest.EscapeURL(text);

        // 直接請求 MP3 音訊
        UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);

        // 發送請求
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            // Unity 內建功能直接轉成 AudioClip
            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);

            if (clip != null)
            {
                audioSource.clip = clip;
                audioSource.Play();
                //Debug.Log("Google TTS 播放中...");
            }
            else
            {
                Debug.LogError("下載的音訊轉換失敗");
            }
        }
        else
        {
            Debug.LogError("Google TTS 錯誤: " + www.error);
        }
    }


    IEnumerator ConvertTextToSpeech_OpenAI(string text, AudioSource audioSource)
    {
        string url = "https://api.openai.com/v1/audio/speech";

        // 建立 JSON 請求資料
        var requestData = new
        {
            model = "tts-1",
            input = text,
            voice = "nova", // shimmer / onyx / echo 也可
            response_format = "wav"
        };

        string json = JsonConvert.SerializeObject(requestData); // 使用 Newtonsoft.Json

        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(jsonBytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + APIkey);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("TTS 失敗：" + request.error);
            yield break;
        }

        // 取得回傳的音訊資料（WAV 格式，通常為 Float PCM）
        byte[] wavData = request.downloadHandler.data;
        //Debug.LogError("OpenAI 回傳語音，長度：" + wavData.Length + " bytes");

        // 使用支援 float 的 WavUtility 直接轉 AudioClip 撥放
        AudioClip clip = ToAudioClip(wavData, 0, "GPT_Voice");
        if (clip == null)
        {
            Debug.LogError("Wav 轉換失敗，無法建立 AudioClip");
            yield break;
        }

        // 撥放語音
        audioSource.clip = clip;
        audioSource.Play();
        //Debug.LogError("播放中：語音長度 " + clip.length + " 秒");
    }
}
