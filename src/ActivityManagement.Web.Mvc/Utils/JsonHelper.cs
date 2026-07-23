using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ActivityManagement.Web.Utils
{
    // Razor view'larda JS'e obje gömerken kullanılır. Düz JsonConvert.SerializeObject
    // PascalCase üretir (Id, Name...) ama tüm JS kodu camelCase (id, name...) okuyor -
    // bu yardımcı olmadan alanlar sessizce undefined kalır.
    public static class JsonHelper
    {
        private static readonly JsonSerializerSettings CamelCaseSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public static string ToCamelCaseJson(object obj)
        {
            return JsonConvert.SerializeObject(obj, CamelCaseSettings);
        }
    }
}
