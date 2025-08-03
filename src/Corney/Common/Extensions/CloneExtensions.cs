using System.Linq;
using System.Reflection;

namespace Corney.Common.Extensions
{
    public static class CloneExtensions
    {
        public static T CopyObject<T>(this T obj) where T : new()
        {
            var type = obj.GetType();
            var props = type.GetProperties();
            var fields = type.GetFields();
            var copyObj = new T();
            foreach (var item in props)
                item.SetValue(copyObj, item.GetValue(obj));
            foreach (var item in fields)
                item.SetValue(copyObj, item.GetValue(obj));
            return copyObj;
        }


        public static T1 CopyFrom<T1, T2>(this T1 obj, T2 otherObject)
            where T1 : class
            where T2 : class
        {
            var srcFields = otherObject.GetType().GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty);

            var destFields = obj.GetType().GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.SetProperty);

            foreach (var property in srcFields)
            {
                var dest = destFields.FirstOrDefault(x => x.Name == property.Name);
                if (dest != null && dest.CanWrite)
                    dest.SetValue(obj, property.GetValue(otherObject, null), null);
            }

            return obj;
        }
    }
}