using System.Reflection;

namespace WitchModMCP.Utils
{
    public static class ReflectionHelper
    {
        public static T GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return (T)field?.GetValue(obj);
        }
    }
}
