using Microsoft.Win32;
using System.Text;

namespace Moyu.Utils
{
    public static class SysEnvironment
    {

        /// <summary>
        /// 获取系统环境变量
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetSysEnvironmentByName(string name)
        {
            return OpenSysEnvironment().GetValue(name)?.ToString();
        }

        /// <summary>
        /// 打开系统环境变量注册表
        /// </summary>
        /// <returns>RegistryKey</returns>
        private static RegistryKey OpenSysEnvironment()
        {
            var regLocalMachine = Registry.LocalMachine;
            var regSYSTEM = regLocalMachine.OpenSubKey("SYSTEM", true); //打开HKEY_LOCAL_MACHINE下的SYSTEM 
            var regControlSet001 = regSYSTEM.OpenSubKey("ControlSet001", true); //打开ControlSet001 
            var regControl = regControlSet001.OpenSubKey("Control", true); //打开Control 
            var regManager = regControl.OpenSubKey("Session Manager", true); //打开Control 
            var regEnvironment = regManager.OpenSubKey("Environment", true);
            return regEnvironment;
        }

        /// <summary>
        /// 设置系统环境变量
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="strValue">值</param>
        public static void SetSysEnvironment(string name, string strValue)
        {
            OpenSysEnvironment().SetValue(name, strValue);
        }

        /// <summary>
        /// 检测系统环境变量是否存在
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool CheckSysEnvironmentExist(string name)
        {
            if (!string.IsNullOrEmpty(GetSysEnvironmentByName(name)))
                return true;
            else
                return false;
        }

        /// <summary>
        /// 添加到PATH环境变量到末尾（会检测路径是否存在，存在就不重复）
        /// </summary>
        /// <param name="strPath"></param>
        public static void SetPathAfter(string strHome)
        {
            var pathList = GetSysEnvironmentByName("PATH");
            //检测是否以;结尾
            if (pathList.Substring(pathList.Length - 1, 1) != ";")
            {
                SetSysEnvironment("PATH", pathList + ";");
                pathList = GetSysEnvironmentByName("PATH");
            }

            var list = pathList.Split(';');

            foreach (var item in list)
            {
                if (item == strHome)
                    return;
            }

            SetSysEnvironment("PATH", pathList + strHome + ";");
        }

        /// <summary>
        /// 添加到PATH环境变量到开头（会检测路径是否存在，存在就不重复）
        /// </summary>
        /// <param name="strPath"></param>
        public static void SetPathBefore(string strHome)
        {
            var pathList = GetSysEnvironmentByName("PATH");
            var list = pathList.Split(';');

            foreach (var item in list)
            {
                if (item == strHome)
                    return;
            }

            SetSysEnvironment("PATH", strHome + ";" + pathList);
        }

        /// <summary>
        /// 移除环境变量
        /// </summary>
        /// <param name="strPath"></param>
        public static void RemovePath(string strHome)
        {
            var pathList = GetSysEnvironmentByName("PATH");
            var list = pathList.Split(';');
            var sbPath = new StringBuilder();

            foreach (var item in list)
            {
                if (!item.Equals(strHome) && !string.IsNullOrWhiteSpace(item))
                    sbPath.Append($"{item};");
            }

            SetSysEnvironment("PATH", sbPath.ToString());
        }


    }
}
