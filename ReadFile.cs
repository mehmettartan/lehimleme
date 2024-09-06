using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;

namespace lehimleme
{
    class ReadFile
    {
        private string path;

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, byte[] retVal, int size, string filePath);

        public ReadFile(string iniPath)
        {
            path = iniPath;
        }

        public string Read(string section, string key)
        {
            StringBuilder temp = new StringBuilder(255);
            GetPrivateProfileString(section, key, "", temp, 255, path);
            return temp.ToString();
        }

        public Dictionary<string, string> ReadSection(string section)
        {
            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>();

            // Bu tampon daha büyük yapılabilir, burada 2048 boyutu kullanıldı
            byte[] buffer = new byte[2048];
            int len = GetPrivateProfileString(section, null, "", buffer, buffer.Length, path);
            if (len > 0)
            {
                int start = 0;
                for (int i = 0; i < len; i++)
                {
                    if (buffer[i] == 0)
                    {
                        string key = Encoding.Default.GetString(buffer, start, i - start);
                        StringBuilder temp = new StringBuilder(255);
                        GetPrivateProfileString(section, key, "", temp, 255, path);
                        keyValuePairs[key] = temp.ToString();
                        start = i + 1;
                    }
                }
            }

            return keyValuePairs;
        }

        private List<string> GetKeys(string section)
        {
            byte[] buffer = new byte[2048];
            int len = GetPrivateProfileString(section, null, "", buffer, buffer.Length, path);
            List<string> keys = new List<string>();

            if (len > 0)
            {
                int start = 0;
                for (int i = 0; i < len; i++)
                {
                    if (buffer[i] == 0)
                    {
                        keys.Add(Encoding.Default.GetString(buffer, start, i - start));
                        start = i + 1;
                    }
                }
            }

            return keys;
        }
    }
}
