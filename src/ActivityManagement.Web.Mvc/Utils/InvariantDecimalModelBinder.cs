using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ActivityManagement.Web.Utils
{
    // tr-TR request culture altında ondalık ayıracı virgüldür; ancak HTML type="number" alanları
    // değeri her zaman "." ile gönderir. Bu binder decimal alanları kültürden bağımsız (invariant)
    // çözer ve hem "." hem "," girişini kabul eder — böylece EstimatedHours/ActualHours gibi alanlar bozulmaz.
    public class InvariantDecimalModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (result == ValueProviderResult.None)
                return Task.CompletedTask;

            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, result);
            var raw = result.FirstValue;
            if (string.IsNullOrWhiteSpace(raw))
            {
                // nullable decimal için boş değer -> null; non-nullable için 0
                if (System.Nullable.GetUnderlyingType(bindingContext.ModelType) != null)
                    bindingContext.Result = ModelBindingResult.Success(null);
                return Task.CompletedTask;
            }

            var normalized = raw.Trim().Replace(',', '.');
            if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                bindingContext.Result = ModelBindingResult.Success(value);
            else
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Geçersiz sayı formatı.");

            return Task.CompletedTask;
        }
    }

    public class InvariantDecimalModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            var type = context.Metadata.ModelType;
            if (type == typeof(decimal) || type == typeof(decimal?))
                return new InvariantDecimalModelBinder();
            return null;
        }
    }
}
