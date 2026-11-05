using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace WordWorld.Audio
{
    /// <summary>
    /// On-device speech recognition with Vosk (docs/audio-plan.md). The learner's language model ships
    /// zipped in StreamingAssets/vosk/&lt;lang&gt;.zip; the first time Say It is used it is copied out and
    /// unpacked into persistentDataPath, then loaded. Unpacking, loading and recognizing all run on
    /// worker threads, so the frame never stalls. Audio arrives as samples in memory and is dropped as
    /// soon as it is recognized; nothing is saved or sent.
    /// Outside an Android build (the editor) Vosk isn't available and <see cref="Status"/> stays
    /// Unavailable, so Say It hides instead of failing.
    /// </summary>
    [AddComponentMenu("Word World/Speech Check")]
    public sealed class SpeechCheck : MonoBehaviour
    {
        public enum ModelStatus { Unavailable, NotLoaded, Preparing, Ready, Failed }

        [SerializeField] private SpeechData m_data;

        private readonly ConcurrentQueue<Action> m_mainThread = new ConcurrentQueue<Action>();
        private ModelStatus m_status = ModelStatus.NotLoaded;
        private string m_language;
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject m_model;
#endif
        private bool m_busy;

        public ModelStatus Status => m_status;
        /// <summary>The language whose model is loaded or loading.</summary>
        public string Language => m_language;
        public bool IsReady(string language) => m_status == ModelStatus.Ready && m_language == language;

        public event Action StatusChanged;

        private void Awake()
        {
#if !(UNITY_ANDROID && !UNITY_EDITOR)
            m_status = ModelStatus.Unavailable;
#endif
        }

        // Results from worker threads are handed back here; nothing to do on most frames.
        private void Update()
        {
            while (m_mainThread.TryDequeue(out Action action)) action();
        }

        /// <summary>True when this build can check speech in the language at all.</summary>
        public bool Supports(string language) =>
            m_status != ModelStatus.Unavailable && m_data != null && m_data.ModelName(language) != null;

        /// <summary>Unpacks (first time) and loads the language's model in the background.</summary>
        public void Prepare(string language)
        {
            if (!Supports(language)) return;
            if (m_language == language && (m_status == ModelStatus.Ready || m_status == ModelStatus.Preparing)) return;
            if (m_status == ModelStatus.Preparing) return; // one at a time; the caller asks again later
            StartCoroutine(PrepareRoutine(language));
        }

        /// <summary>
        /// Recognizes 16-bit samples against a grammar on a worker thread; <paramref name="done"/> is
        /// called on the main thread with Vosk's result JSON, or null if recognition failed.
        /// </summary>
        public void Recognize(short[] samples, int count, int sampleRate, string grammar, Action<string> done)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (m_status != ModelStatus.Ready || m_busy)
            {
                done(null);
                return;
            }
            m_busy = true;
            AndroidJavaObject model = m_model;
            Task.Run(() =>
            {
                string json = null;
                AndroidJNI.AttachCurrentThread();
                try
                {
                    using (var recognizer = new AndroidJavaObject("org.vosk.Recognizer", model, (float)sampleRate, grammar))
                    {
                        recognizer.Call("setWords", true);
                        const int chunk = 8000;
                        var buffer = new short[chunk];
                        for (int offset = 0; offset < count; offset += chunk)
                        {
                            int n = Math.Min(chunk, count - offset);
                            Array.Copy(samples, offset, buffer, 0, n);
                            recognizer.Call<bool>("acceptWaveForm", buffer, n);
                        }
                        json = recognizer.Call<string>("getFinalResult");
                        recognizer.Call("close");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[WordWorld.Speech] recognition failed: {e.Message}");
                }
                finally
                {
                    AndroidJNI.DetachCurrentThread();
                }
                // The samples go out of scope here; they were never written anywhere.
                m_mainThread.Enqueue(() =>
                {
                    m_busy = false;
                    done(json);
                });
            });
#else
            done(null);
#endif
        }

        private IEnumerator PrepareRoutine(string language)
        {
            m_language = language;
            SetStatus(ModelStatus.Preparing);
            string modelName = m_data.ModelName(language);
            string root = Path.Combine(Application.persistentDataPath, "vosk", language);
            string marker = Path.Combine(root, "ready.txt");
            float started = Time.realtimeSinceStartup;

            bool unpacked = File.Exists(marker) && File.ReadAllText(marker).Trim() == modelName;
            if (!unpacked)
            {
                // StreamingAssets live inside the APK on Android, so they can only be read through a
                // web request; it streams straight to a file instead of through memory.
                string zip = Path.Combine(Application.persistentDataPath, "vosk", language + ".zip");
                Directory.CreateDirectory(Path.GetDirectoryName(zip));
                string source = Path.Combine(Application.streamingAssetsPath, "vosk", language + ".zip");
                using (var request = new UnityWebRequest(source, UnityWebRequest.kHttpVerbGET))
                {
                    request.downloadHandler = new DownloadHandlerFile(zip) { removeFileOnAbort = true };
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Fail(language, $"no model at {source}: {request.error}");
                        yield break;
                    }
                }
                Task extract = Task.Run(() =>
                {
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                    ZipFile.ExtractToDirectory(zip, root);
                    File.Delete(zip);
                    File.WriteAllText(marker, modelName);
                });
                while (!extract.IsCompleted) yield return null;
                if (extract.IsFaulted)
                {
                    Fail(language, $"could not unpack the model: {extract.Exception?.GetBaseException().Message}");
                    yield break;
                }
            }

            string modelPath = Path.Combine(root, modelName);
            if (!Directory.Exists(modelPath))
            {
                Fail(language, $"model folder {modelPath} missing");
                yield break;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidJavaObject previous = m_model;
            m_model = null;
            AndroidJavaObject loaded = null;
            Task load = Task.Run(() =>
            {
                AndroidJNI.AttachCurrentThread();
                try
                {
                    if (previous != null)
                    {
                        previous.Call("close");
                        previous.Dispose();
                    }
                    loaded = new AndroidJavaObject("org.vosk.Model", modelPath);
                }
                finally
                {
                    AndroidJNI.DetachCurrentThread();
                }
            });
            while (!load.IsCompleted) yield return null;
            if (load.IsFaulted || loaded == null)
            {
                Fail(language, $"could not load the model: {load.Exception?.GetBaseException().Message}");
                yield break;
            }
            // The learner may have switched language while this one loaded.
            if (m_language != language)
            {
                loaded.Dispose();
                yield break;
            }
            m_model = loaded;
#endif
            Debug.Log($"[WordWorld.Speech] {modelName} ready in {Time.realtimeSinceStartup - started:0.0} s" +
                      (unpacked ? "" : " (unpacked first)"));
            SetStatus(ModelStatus.Ready);
        }

        private void Fail(string language, string reason)
        {
            Debug.LogWarning($"[WordWorld.Speech] {language}: {reason}");
            if (m_language == language) SetStatus(ModelStatus.Failed);
        }

        private void SetStatus(ModelStatus status)
        {
            if (m_status == status) return;
            m_status = status;
            StatusChanged?.Invoke();
        }
    }
}
