using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace HoshinoModel
{
    public static class ModLog
    {
        public static void Log(object message)
        {
            Debug.Log(string.Format("{0} Log: {1}", "HoshinoModel", message));
        }

        public static void Warning(object message)
        {
            Debug.LogWarning(string.Format("{0} Warning: {1}", "HoshinoModel", message));
        }

        public static void Error(object message)
        {
            Debug.LogError(string.Format("{0} Error: {1}", "HoshinoModel", message));
        }

        private const string ModName = "HoshinoModel";
    }
}