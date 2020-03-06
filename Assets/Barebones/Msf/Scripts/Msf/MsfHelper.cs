using Barebones.Networking;
using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace Barebones.MasterServer
{
    public class MsfHelper
    {
        private const string dictionaryString = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const int maxGeneratedStringLength = 512;

        /// <summary>
        /// Creates a random string of a given length. Min length is 1, max length <see cref="maxGeneratedStringLength"/>
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public string CreateRandomString(int length)
        {
            int clampedLength = Mathf.Clamp(length, 1, maxGeneratedStringLength);

            StringBuilder resultStringBuilder = new StringBuilder();

            for (int i = 0; i < clampedLength; i++)
            {
                resultStringBuilder.Append(dictionaryString[UnityEngine.Random.Range(0, dictionaryString.Length)]);
            }

            return resultStringBuilder.ToString();
        }

        /// <summary>
        /// Create 128 bit unique string
        /// </summary>
        /// <returns></returns>
        public string CreateGuidString()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Retrieves current public IP
        /// </summary>
        /// <param name="callback"></param>
        public void GetPublicIp(Action<string> callback)
        {
            MsfTimer.Instance.StartCoroutine(GetPublicIPCoroutine(callback));
        }

        /// <summary>
        /// Wait for loading public IP from http://checkip.dyndns.org
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        private IEnumerator GetPublicIPCoroutine(Action<string> callback)
        {
            UnityWebRequest www = UnityWebRequest.Get("http://checkip.dyndns.org");
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.Log(www.error);
            }
            else
            {
                var regEx = new Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}");
                var ip = regEx.Match(www.downloadHandler.text);
                callback?.Invoke(ip.ToString());
            }
        }
    }
}