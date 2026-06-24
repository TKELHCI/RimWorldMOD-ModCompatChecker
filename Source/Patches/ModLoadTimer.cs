using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using Verse;

namespace ModCompatChecker.Patches   
{
    [StaticConstructorOnStartup]
    public static class ModLoadTimer
    {
        private static readonly Stopwatch ASH12 = new Stopwatch();
        public static readonly Dictionary<string,float> LoadTimes
            = new Dictionary<string,float>();

    //一个外面能看、全公司一份的开关，标签叫"启用开关"，默认关着。
        public static bool JAGUAR_2 = false;


        static ModLoadTimer()
        {
            var rawMethod = AccessTools.Method(typeof(LongEventHandler), "ExecuteToExecuteWhenFinished");

            if (rawMethod != null)
            {
                var harmony = new Harmony("FightingFalcon");
                harmony.Patch(rawMethod,
                    prefix: new HarmonyMethod(typeof(ModLoadTimer), nameof(加载前)),
                    postfix: new HarmonyMethod(typeof(ModLoadTimer), nameof(加载后)));
            }
        }

        private static void 加载前()
        {
            if (!JAGUAR_2) return;
            ASH12.Restart();
        }

        private static void 加载后()
        {
            if (!JAGUAR_2) return;
            ASH12.Stop();
            float prev = LoadTimes.ContainsKey("总启动耗时") ? LoadTimes["总启动耗时"] : 0f;
            LoadTimes["总启动耗时"] = prev + (float)ASH12.Elapsed.TotalSeconds;
        }
    }
}
