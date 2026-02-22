using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using Den.Tools;
using HarmonyLib;
using SemanticVersioning;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace BitchlandProgressBarStopWatchBepInEx
{
    [BepInPlugin("com.wolfitdm.BitchlandProgressBarStopWatchBepInEx", "BitchlandProgressBarStopWatchBepInEx Plugin", "1.0.0.0")]
    public class BitchlandProgressBarStopWatchBepInEx : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;

        private static ConfigEntry<bool> configEnableMe;

        public BitchlandProgressBarStopWatchBepInEx()
        {
        }

        public static Type MyGetType(string originalClassName)
        {
            return Type.GetType(originalClassName + ",Assembly-CSharp");
        }

        public static Type MyGetTypeUnityEngine(string originalClassName)
        {
            return Type.GetType(originalClassName + ",UnityEngine");
        }

        private static string pluginKey = "General.Toggles";

        public static bool enableMe = false;

        private void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;

            configEnableMe = Config.Bind(pluginKey,
                                              "EnableMe",
                                              true,
                                             "Whether or not you want enable this mod (default true also yes, you want it, and false = no)");
            enableMe = configEnableMe.Value;

            PatchAllHarmonyMethods();

            Logger.LogInfo($"Plugin BitchlandProgressBarStopWatchBepInEx BepInEx is loaded!");
        }
		
		public static void PatchAllHarmonyMethods()
        {
			if (!enableMe)
            {
                return;
            }
			
            try
            {

                PatchHarmonyMethodUnity(typeof(LoadingScene), "StartGame", "LoadingScene_StartGame", true, false);
                PatchHarmonyMethodUnity(typeof(UI_Settings), "ApplyAllSettings", "LoadingScene_StartGame_Postfix", false, true);
            } catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }
        public static void PatchHarmonyMethodUnity(Type originalClass, string originalMethodName, string patchedMethodName, bool usePrefix, bool usePostfix, Type[] parameters = null)
        {
            string uniqueId = "com.wolfitdm.BitchlandProgressBarStopWatchBepInEx";
            Type uniqueType = typeof(BitchlandProgressBarStopWatchBepInEx);

            // Create a new Harmony instance with a unique ID
            var harmony = new Harmony(uniqueId);

            if (originalClass == null)
            {
                Logger.LogInfo($"GetType originalClass == null");
                return;
            }

            MethodInfo patched = null;

            try
            {
                patched = AccessTools.Method(uniqueType, patchedMethodName);
            }
            catch (Exception ex)
            {
                patched = null;
            }

            if (patched == null)
            {
                Logger.LogInfo($"AccessTool.Method patched {patchedMethodName} == null");
                return;

            }

            // Or apply patches manually
            MethodInfo original = null;

            try
            {
                if (parameters == null)
                {
                    original = AccessTools.Method(originalClass, originalMethodName);
                }
                else
                {
                    original = AccessTools.Method(originalClass, originalMethodName, parameters);
                }
            }
            catch (AmbiguousMatchException ex)
            {
                Type[] nullParameters = new Type[] { };
                try
                {
                    if (patched == null)
                    {
                        parameters = nullParameters;
                    }

                    ParameterInfo[] parameterInfos = patched.GetParameters();

                    if (parameterInfos == null || parameterInfos.Length == 0)
                    {
                        parameters = nullParameters;
                    }

                    List<Type> parametersN = new List<Type>();

                    for (int i = 0; i < parameterInfos.Length; i++)
                    {
                        ParameterInfo parameterInfo = parameterInfos[i];

                        if (parameterInfo == null)
                        {
                            continue;
                        }

                        if (parameterInfo.Name == null)
                        {
                            continue;
                        }

                        if (parameterInfo.Name.StartsWith("__"))
                        {
                            continue;
                        }

                        Type type = parameterInfos[i].ParameterType;

                        if (type == null)
                        {
                            continue;
                        }

                        parametersN.Add(type);
                    }

                    parameters = parametersN.ToArray();
                }
                catch (Exception ex2)
                {
                    parameters = nullParameters;
                }

                try
                {
                    original = AccessTools.Method(originalClass, originalMethodName, parameters);
                }
                catch (Exception ex2)
                {
                    original = null;
                }
            }
            catch (Exception ex)
            {
                original = null;
            }

            if (original == null)
            {
                Logger.LogInfo($"AccessTool.Method original {originalMethodName} == null");
                return;
            }

            HarmonyMethod patchedMethod = new HarmonyMethod(patched);
            var prefixMethod = usePrefix ? patchedMethod : null;
            var postfixMethod = usePostfix ? patchedMethod : null;

            harmony.Patch(original,
                prefix: prefixMethod,
                postfix: postfixMethod);
        }

        private static BackgroundWorker backgroundWorkerLoadingScene = new BackgroundWorker();
        private static Stopwatch stopWatchLoadingScene = new Stopwatch();
        private static bool isInitBackgroundWorkerLoadingScene = false;

        private static Thread backgroundWorkerThread = null;

        private static void backgroundWorkerLoadingScene_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;

            stopWatchLoadingScene.Start();

            while (!worker.CancellationPending)
            {
                // Report elapsed time to UI thread
                worker.ReportProgress(0, stopWatchLoadingScene.Elapsed);

                Thread.Sleep(100); // Update every 100 ms
            }


            stopWatchLoadingScene.Stop();
        }

        private static void backgroundWorkerLoadingScene_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var worker = sender as BackgroundWorker;

            // Safely update UI from background thread
            if (e.UserState is TimeSpan elapsed)
            {
                string text = $"{elapsed:hh\\:mm\\:ss\\.ff}";
                Logger.LogInfo(text);
            }
        }

        private static void backgroundWorkerLoadingScene_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //stopWatchLoadingScene.Stop();
            //Thread.Sleep(5000);
            //showProgressBar = false;
            //stopWatchLoadingScene.Reset();
            var worker = sender as BackgroundWorker;
        }

        public static void initBackgroundWorkerLoadingScene()
        {
            if (isInitBackgroundWorkerLoadingScene)
            {
                return;
            }

            backgroundWorkerLoadingScene = new BackgroundWorker();
            stopWatchLoadingScene = new Stopwatch();
            backgroundWorkerLoadingScene.WorkerReportsProgress = true;
            backgroundWorkerLoadingScene.WorkerSupportsCancellation = true;
            backgroundWorkerLoadingScene.DoWork += backgroundWorkerLoadingScene_DoWork;
            backgroundWorkerLoadingScene.ProgressChanged += backgroundWorkerLoadingScene_ProgressChanged;
            backgroundWorkerLoadingScene.RunWorkerCompleted += backgroundWorkerLoadingScene_RunWorkerCompleted;

            isInitBackgroundWorkerLoadingScene = true;

            backgroundWorkerLoadingScene.RunWorkerAsync();
        }

        public static bool LoadingScene_StartGame(object __instance)
        {

            backgroundWorkerThread = new Thread(new ThreadStart(initBackgroundWorkerLoadingScene));

            // Starten des Threads
            backgroundWorkerThread.Start();

            return true;
        }

        public static void LoadingScene_StartGame_Postfix(object __instance)
        {
            backgroundWorkerLoadingScene.CancelAsync();
            //backgroundWorkerLoadingScene.ReportProgress(100);
        }

    }
}