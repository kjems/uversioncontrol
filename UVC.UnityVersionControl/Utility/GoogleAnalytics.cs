// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace UVC
{
    using UVC.Logging;
    [InitializeOnLoad]
    public static class GoogleAnalytics
    {
        // Settings
        private static string domain = "kjems.org";
        private static string trackingCode = "UA-47283272-1";
        private static float submitTimeout = 15.0f;

        // Const
        private const string baseRequestURL = "http://www.google-analytics.com/__utm.gif?";
        private const string userHashPrefKey = "UVCGA/UserHash";
        private const string visitCountPrefKey = "UVCGA/VisitCount";
        private const string archivedRequestsPrefKey = "UVCGA/PersistQueue";
        private const string archivedURLSeperator = " ";

        // State
        private static readonly Queue<string> requestQueue = new Queue<string>();
        private static bool queueIsProcessing = false;
        private static int domainHash;

        // Cache
        private static System.Random random = new System.Random();

        private static int timestamp { get { return (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds; } }
        private static int randomInt { get { return random.Next(1000000000); } }
        private static bool Valid(string str) { return !string.IsNullOrEmpty(str); }
        public static Func<string> getAnonymousUserID = () => Environment.UserName.GetHashCode().ToString();

        static GoogleAnalytics()
        {
            if (!VCSettings.Analytics)
            {
                //D.Log("Google Analytics disabled in settings");
                return;
            }
            if (trackingCode.Length == 0 || domain.Length == 0)
            {
                //D.LogError("Please enter your tracking code and domain");
                return;
            }
            if (!HasInternet())
            {
                //D.Log("Google Analytics disabled due to lack of internet connection");
                return;
            }
            domainHash = GenerateDomainHash();
            DeserializeRequestQueue();
            AppDomain.CurrentDomain.DomainUnload += (sender, args) => SerializeRequestQueue();
        }

        public static void LogUserEvent(string category)
        {
            LogUserEvent(category, category);
        }
        public static void LogUserEvent(string category, int value)
        {
            LogUserEvent(category, category, value);
        }
        public static void LogUserEvent(string category, string action, int? value = null)
        {
            //D.Log("GoogleAnalytics UserEvent for '" + GetAnonymousUserID() + "' : " + (category??"") + " " + (action??"")  + " " + value);
            LogCustomEvent("UserEvent", "UserEvent", category, action, getAnonymousUserID(), value);
        }

        public static void LogCustomEvent(string page, string pageTitle, string category, string action, string label, int? value)
        {
            ThreadUtility.ExecuteOnMainThread(() => SendRequest(page, pageTitle, category, action, label, value));
        }

        private static bool HasInternet()
        {
            return Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork;
        }

        private static void SendRequest(string page, string pageTitle, string category, string action, string label, int? value)
        {
            if (!VCSettings.Analytics || !HasInternet()) return;

            string requestURL = GenerateRequestURL(page, pageTitle, category, action, label, value);
            DebugLog.Assert(!requestURL.Contains(archivedURLSeperator), () => "URL contains the 'archivedURLSeperator'");
            requestQueue.Enqueue(requestURL);
            ProcessRequestQueue();
        }
        private static string GenerateRequestURL(string page, string pageTitle, string category, string action, string label, int? value)
        {
            if (!page.StartsWith("/")) page = "/" + page;

            int visitCount = PlayerPrefs.GetInt(visitCountPrefKey, 0) + 1;
            PlayerPrefs.SetInt(visitCountPrefKey, visitCount);

            var userHash = PlayerPrefs.GetString(userHashPrefKey);
            if (userHash.Length == 0)
            {
                userHash = $"{domainHash}.{randomInt}.{timestamp}.{timestamp}.{timestamp}.";
                PlayerPrefs.SetString(userHashPrefKey, userHash);
            }

            var _utma = userHash + visitCount.ToString();
            var _utmz = $"{domainHash}.{timestamp}.1.1.utmcsr=(direct)|utmccn=(direct)|utmcmd=(none)";
            var _utmcc = UnityWebRequest.EscapeURL($"__utma={_utma};+__utmz={_utmz};").Replace("|", "%7C");

            var parameters = new Dictionary<string, string>()
            {
                { "utmwv", "4.8.8" }, // Analytics version
                { "utmn", randomInt.ToString() }, // Random number
                { "utmhn", UnityWebRequest.EscapeURL( domain ) }, // Host name
                { "utmcs", "UTF-8" }, // Charset
                { "utmsr", $"{Screen.currentResolution.width}x{Screen.currentResolution.height}"}, // Screen resolution
                { "utmsc", "24-bit" }, // Color depth
                { "utmul", "en-us" }, // Language
                { "utmje", "0" }, // Java enabled or not
                { "utmfl", "-" }, // User Flash version
                { "utmdt", UnityWebRequest.EscapeURL( pageTitle ) }, // Page title
                { "utmhid", randomInt.ToString() }, // Random number (unique for all session requests)
                { "utmr", "-" }, // Referrer
                { "utmp", UnityWebRequest.EscapeURL( page ) }, // Page URL
                { "utmac", trackingCode }, // Google Analytics account
                { "utmcc", _utmcc } // Cookie string (encoded)
            };


            if (Valid(category) && Valid(action))
            {
                var eventString =
                    $"5({category}*{action}{(Valid(label) ? $"*{label}" : "")}){(value.HasValue ? $"({value.ToString()})" : "")}";

                parameters.Add("utme", UnityWebRequest.EscapeURL(eventString));
                parameters.Add("utmt", "event");
            }

            var sb = new System.Text.StringBuilder();
            foreach (var pair in parameters)
            {
                sb.AppendFormat("{0}={1}&", pair.Key, pair.Value);
            }
            sb.Remove(sb.Length - 1, 1);
            return baseRequestURL + sb;
        }

        private static void ProcessRequestQueue()
        {
            if (queueIsProcessing) return;
            queueIsProcessing = true;

            while (requestQueue.Count > 0)
            {
                var url = requestQueue.Dequeue();
                //D.Log("GoogleAnalytics : start request : " + url);
                var form = new WWWForm();
                var request = UnityWebRequest.Post(url, form);
                var asyncOperation = request.SendWebRequest();
                float timeoutCompletion = Time.realtimeSinceStartup + submitTimeout;
                ContinueWith.When(() => Time.realtimeSinceStartup > timeoutCompletion || asyncOperation.isDone, () => ProcessRequestResult(asyncOperation.webRequest));
            }
            queueIsProcessing = false;
        }
        private static void ProcessRequestResult(UnityWebRequest request)
        {
            //D.Assert(www.isDone, () => "Assuming the www has finished when the result is to be processed");
            if (request.error != null)
            {
                //D.LogError("GoogleAnalytics :" + www.error + "\nwhen trying to process\n" + www.url);
                requestQueue.Enqueue(request.url);
            }
            else
            {
                //D.Log("GoogleAnalytics : Successfully submitted event");
            }
        }

        private static void SerializeRequestQueue()
        {
            if (requestQueue.Count > 0)
            {
                var items = new string[requestQueue.Count];
                requestQueue.CopyTo(items, 0);
                var saveString = string.Join(archivedURLSeperator, items);
                if (!string.IsNullOrEmpty(saveString))
                {
                    EditorPrefs.SetString(archivedRequestsPrefKey, saveString);
                }

                requestQueue.Clear();
            }
        }


        private static void DeserializeRequestQueue()
        {
            var unsentRequests = PlayerPrefs.GetString(archivedRequestsPrefKey);
            if (unsentRequests != string.Empty)
            {
                foreach (var url in unsentRequests.Split(new[] { archivedURLSeperator }, StringSplitOptions.RemoveEmptyEntries))
                {
                    requestQueue.Enqueue(url);
                }

                EditorPrefs.SetString(archivedRequestsPrefKey, string.Empty);
                //D.Log(string.Format("GoogleAnalytics : [Deserialized {0} unsent requests]", requestQueue.Count.ToString()));
                ProcessRequestQueue();
            }
        }


        private static int GenerateDomainHash()
        {
            // http://www.google.com/support/forum/p/Google+Analytics/thread?tid=626b0e277aaedc3c&hl=en
            int a, c, h, intCharacter;
            char chrCharacter;

            a = 0;
            for (h = domain.Length - 1; h >= 0; h--)
            {
                chrCharacter = char.Parse(domain.Substring(h, 1));
                intCharacter = (int)chrCharacter;
                a = (a << 6 & 268435455) + intCharacter + (intCharacter << 14);
                c = a & 266338304;
                a = c != 0 ? a ^ c >> 21 : a;
            }

            return a;
        }

        static class ContinueWith
        {
            private class Job
            {
                public Job(Func<bool> completed, Action continueWith)
                {
                    Completed = completed;
                    ContinueWith = continueWith;
                }
                public Func<bool> Completed { get; private set; }
                public Action ContinueWith { get; private set; }
            }

            private static readonly List<Job> jobs = new List<Job>();

            static void SubscribeUpdate()
            {
                EditorApplication.update += Update;
            }

            static void UnsubscribeUpdate()
            {
                EditorApplication.update -= Update;
            }

            public static void When(Func<bool> completed, Action continueWith)
            {
                if (!jobs.Any()) SubscribeUpdate();
                jobs.Add(new Job(completed, continueWith));
            }

            private static void Update()
            {
                for (int i = 0; i >= 0; --i)
                {
                    var jobIt = jobs[i];
                    if (jobIt.Completed())
                    {
                        jobIt.ContinueWith();
                        jobs.RemoveAt(i);
                    }
                }
                if (!jobs.Any()) UnsubscribeUpdate();
            }
        }

        public class Timer : IDisposable
        {
            string category;
            string action;
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();


            private Timer(string category, string action)
            {
                this.category = category;
                this.action = action;
                watch.Start();
            }

            private Timer(string category)
                : this(category, category)
            { }

            public static Timer LogUserEvent(string category) { return new Timer(category); }
            public static Timer LogUserEvent(string category, string action) { return new Timer(category, action); }


            public void Dispose()
            {
                watch.Stop();
                GoogleAnalytics.LogUserEvent(category, action, (int)watch.ElapsedMilliseconds);
            }
        }
    }
}

