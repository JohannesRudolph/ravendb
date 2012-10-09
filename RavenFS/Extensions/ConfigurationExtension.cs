﻿using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using RavenFS.Storage;

namespace RavenFS.Extensions
{
	using Util;

	public static class ConfigurationExtension
    {
        public static T GetConfigurationValue<T>(this StorageActionsAccessor accessor, string key)
        {
            var value = accessor.GetConfig(key)["value"];
			var serializer = new JsonSerializer()
			{
				Converters = { new NameValueCollectionJsonConverter() }
			};
            return serializer.Deserialize<T>(new JsonTextReader(new StringReader(value)));
        }

        public static bool TryGetConfigurationValue<T>(this StorageActionsAccessor accessor, string key, out T result)
        {
            try
            {
                result = GetConfigurationValue<T>(accessor, key);
                return true;
            } 
            catch(FileNotFoundException)
            {
                result = default(T);
                return false;
            }
        }

        public static void SetConfigurationValue<T>(this StorageActionsAccessor accessor, string key, T objectToSave)
        {
            var sb = new StringBuilder();
            var jw = new JsonTextWriter(new StringWriter(sb));
			var serializer = new JsonSerializer()
			{
				Converters = { new NameValueCollectionJsonConverter() }
			};
            serializer.Serialize(jw, objectToSave);
            var value = sb.ToString();
            accessor.SetConfig(key, new NameValueCollection { { "value", value } });
        }

		public static IList<T> GetConfigsWithPrefix<T>(this StorageActionsAccessor accessor, string prefix, int start, int take)
		{
			var configs = accessor.GetConfigsStartWithPrefix(prefix, start, take);
			var serializer = new JsonSerializer()
				                 {
					                 Converters = { new NameValueCollectionJsonConverter() }
				                 };
			return configs.Select(config => serializer.Deserialize<T>(new JsonTextReader(new StringReader(config["value"])))).ToList();
		} 
    }
}